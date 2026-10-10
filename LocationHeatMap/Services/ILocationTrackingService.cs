namespace LocationHeatMap.Services;

/// <summary>Outcome of asking the platform to start location tracking.</summary>
public enum TrackingStartResult
{
    /// <summary>Tracking is running.</summary>
    Started,

    /// <summary>The user declined the location permission.</summary>
    PermissionDenied,

    /// <summary>Location services are switched off, or the device has no location hardware.</summary>
    LocationUnavailable,

    /// <summary>The platform rejected the request for some other reason.</summary>
    Failed
}

/// <summary>
/// Wraps MAUI Essentials' geolocation listener behind an interface, so the view model can be
/// exercised without a device and so the permission dance lives in exactly one place.
/// </summary>
public interface ILocationTrackingService
{
    /// <summary>Raised on each accepted fix, already marshalled to the UI thread.</summary>
    event EventHandler<Location>? LocationRecorded;

    /// <summary>Whether the listener is currently running.</summary>
    bool IsTracking { get; }

    /// <summary>Requests permission if necessary and begins listening for position updates.</summary>
    Task<TrackingStartResult> StartAsync();

    /// <summary>Stops listening. Safe to call when not tracking.</summary>
    void Stop();

    /// <summary>Reads a single fix without starting the listener, for centring the map on launch.</summary>
    Task<Location?> GetCurrentLocationAsync();
}
