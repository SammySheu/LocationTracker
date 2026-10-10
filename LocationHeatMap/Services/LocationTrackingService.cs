using System.Diagnostics;

namespace LocationHeatMap.Services;

/// <summary>
/// Foreground location tracking built on <see cref="IGeolocation"/>, writing every accepted fix
/// to <see cref="ILocationDatabase"/>.
/// </summary>
/// <remarks>
/// <para>
/// Tracking is foreground-only: it runs while the app is on screen and stops when the page goes
/// away. That satisfies the brief without the extra machinery background location would need
/// (an Android foreground service with a persistent notification, and iOS "Always"
/// authorisation plus the <c>location</c> background mode).
/// </para>
/// <para>
/// Sampling is time-based rather than distance-based. A distance filter would discard every fix
/// taken while the user is stationary, which is precisely the data that makes a heat map
/// interesting — dwelling in one spot is what should turn that spot red.
/// </para>
/// </remarks>
public class LocationTrackingService : ILocationTrackingService
{
    /// <summary>How often to sample position while tracking.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(3);

    private readonly ILocationDatabase _database;
    private readonly IGeolocation _geolocation;

    public LocationTrackingService(ILocationDatabase database, IGeolocation geolocation)
    {
        _database = database;
        _geolocation = geolocation;
    }

    /// <inheritdoc />
    public event EventHandler<Location>? LocationRecorded;

    /// <inheritdoc />
    public bool IsTracking => _geolocation.IsListeningForeground;

    /// <inheritdoc />
    public async Task<TrackingStartResult> StartAsync()
    {
        if (IsTracking)
        {
            return TrackingStartResult.Started;
        }

        if (!await HasLocationPermissionAsync())
        {
            return TrackingStartResult.PermissionDenied;
        }

        // Subscribe before starting, so no fix can slip through between the two calls.
        _geolocation.LocationChanged += OnLocationChanged;

        try
        {
            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best, SampleInterval);
            var started = await _geolocation.StartListeningForegroundAsync(request);

            if (!started)
            {
                _geolocation.LocationChanged -= OnLocationChanged;
                return TrackingStartResult.Failed;
            }

            return TrackingStartResult.Started;
        }
        catch (FeatureNotEnabledException)
        {
            // Location services are turned off device-wide.
            _geolocation.LocationChanged -= OnLocationChanged;
            return TrackingStartResult.LocationUnavailable;
        }
        catch (FeatureNotSupportedException)
        {
            // No location hardware, or the platform has no geolocation provider.
            _geolocation.LocationChanged -= OnLocationChanged;
            return TrackingStartResult.LocationUnavailable;
        }
        catch (Exception ex)
        {
            _geolocation.LocationChanged -= OnLocationChanged;
            Debug.WriteLine($"[LocationTrackingService] Failed to start listening: {ex}");
            return TrackingStartResult.Failed;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        // Unsubscribing unconditionally is safe and guards against a leaked handler if Stop is
        // reached by a path where IsListeningForeground has already flipped to false.
        _geolocation.LocationChanged -= OnLocationChanged;
        _geolocation.StopListeningForeground();
    }

    /// <inheritdoc />
    public async Task<Location?> GetCurrentLocationAsync()
    {
        if (!await HasLocationPermissionAsync())
        {
            return null;
        }

        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            return await _geolocation.GetLocationAsync(request) ?? await _geolocation.GetLastKnownLocationAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationTrackingService] Could not read a one-off location: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Checks for the when-in-use location permission and prompts for it if it has not been
    /// granted yet. The prompt has to run on the main thread on both platforms.
    /// </summary>
    private static async Task<bool> HasLocationPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.LocationWhenInUse>);
        }

        return status == PermissionStatus.Granted;
    }

    /// <summary>
    /// Persists each incoming fix and re-raises it for the UI. The handler is <c>async void</c>
    /// because it is an event handler; every await inside it is wrapped so that a database
    /// failure can never surface as an unobserved exception that tears down the app.
    /// </summary>
    private async void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        try
        {
            var location = e.Location;

            await _database.AddLocationAsync(
                location.Latitude,
                location.Longitude,
                location.Accuracy,
                DateTime.UtcNow);

            // The view model updates observable properties in response, so hand it the fix on
            // the UI thread rather than on whichever thread the platform raised the event on.
            MainThread.BeginInvokeOnMainThread(() => LocationRecorded?.Invoke(this, location));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationTrackingService] Failed to record a fix: {ex}");
        }
    }
}
