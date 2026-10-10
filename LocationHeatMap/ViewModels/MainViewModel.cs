using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocationHeatMap.Models;
using LocationHeatMap.Services;

namespace LocationHeatMap.ViewModels;

/// <summary>
/// Drives <c>MainView</c>: owns the tracking state, the recorded-point count, and the
/// decision about when the heat map overlay needs rebuilding.
/// </summary>
/// <remarks>
/// The view model deliberately knows nothing about the <c>Map</c> control. It raises
/// <see cref="HeatMapChanged"/> with freshly aggregated cells and lets the view apply them,
/// which keeps every MAUI-Maps type out of this class.
/// </remarks>
public partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// Rebuild the overlay once every this many new fixes. Rebuilding clears and refills the
    /// map's element collection, which is native work on both platforms, so doing it per fix
    /// would make the map stutter while tracking.
    /// </summary>
    private const int RebuildEveryNFixes = 5;

    private readonly ILocationDatabase _database;
    private readonly ILocationTrackingService _trackingService;

    private int _fixesSinceLastRebuild;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrackingButtonText))]
    private bool _isTracking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordedPointLabel))]
    private int _recordedPointCount;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainViewModel(ILocationDatabase database, ILocationTrackingService trackingService)
    {
        _database = database;
        _trackingService = trackingService;
        _trackingService.LocationRecorded += OnLocationRecorded;
    }

    /// <summary>
    /// Raised when the overlay should be redrawn, carrying the cells to draw.
    /// </summary>
    public event EventHandler<IReadOnlyList<HeatCell>>? HeatMapChanged;

    /// <summary>
    /// Raised once on startup with the position the map should open at, when one is known.
    /// </summary>
    public event EventHandler<Location>? InitialLocationResolved;

    /// <summary>Label for the start/stop button.</summary>
    public string TrackingButtonText => IsTracking ? "Stop" : "Start";

    /// <summary>The recorded-point count, pluralised for display.</summary>
    public string RecordedPointLabel =>
        RecordedPointCount == 1 ? "1 point" : $"{RecordedPointCount} points";

    /// <summary>
    /// Loads any previously recorded data and centres the map. Called from the view's
    /// <c>OnAppearing</c>.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        RecordedPointCount = await _database.GetCountAsync();
        await RefreshHeatMapAsync();

        // Prefer the last recorded fix so a relaunch reopens on the existing heat map; fall
        // back to a live reading when the database is empty.
        var mostRecent = await _database.GetMostRecentAsync();
        var startAt = mostRecent is not null
            ? new Location(mostRecent.Latitude, mostRecent.Longitude)
            : await _trackingService.GetCurrentLocationAsync();

        if (startAt is not null)
        {
            InitialLocationResolved?.Invoke(this, startAt);
        }

        StatusMessage = RecordedPointCount == 0
            ? "No data yet — press Start"
            : "Loaded from saved data";
    }

    /// <summary>Starts tracking if stopped, stops it if running.</summary>
    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        if (IsTracking)
        {
            StopTracking();
            return;
        }

        var result = await _trackingService.StartAsync();

        IsTracking = result == TrackingStartResult.Started;
        StatusMessage = result switch
        {
            TrackingStartResult.Started => "Tracking…",
            TrackingStartResult.PermissionDenied => "Location permission denied",
            TrackingStartResult.LocationUnavailable => "Location services are turned off",
            _ => "Could not start tracking"
        };
    }

    /// <summary>Deletes all recorded data and clears the overlay.</summary>
    [RelayCommand]
    private async Task ClearAsync()
    {
        await _database.ClearAsync();

        RecordedPointCount = 0;
        _fixesSinceLastRebuild = 0;
        HeatMapChanged?.Invoke(this, []);
        StatusMessage = "Cleared";
    }

    /// <summary>
    /// Stops the listener. Called by the toggle command and by the view when it disappears, so
    /// that the listener can never outlive the page that started it.
    /// </summary>
    public void StopTracking()
    {
        _trackingService.Stop();
        IsTracking = false;
        StatusMessage = "Stopped";
    }

    /// <summary>Re-aggregates the stored fixes and asks the view to redraw.</summary>
    private async Task RefreshHeatMapAsync()
    {
        var cells = await _database.GetHeatCellsAsync(HeatMapRenderer.CellSizeDegrees);
        HeatMapChanged?.Invoke(this, cells);
    }

    /// <summary>
    /// Handles a newly persisted fix: bumps the counter, and rebuilds the overlay on every
    /// <see cref="RebuildEveryNFixes"/>th one.
    /// </summary>
    private async void OnLocationRecorded(object? sender, Location location)
    {
        RecordedPointCount++;
        StatusMessage = "Tracking…";

        if (++_fixesSinceLastRebuild < RebuildEveryNFixes)
        {
            return;
        }

        _fixesSinceLastRebuild = 0;

        try
        {
            await RefreshHeatMapAsync();
        }
        catch (Exception ex)
        {
            // async void: nothing can observe a fault here, so swallow it rather than crash.
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Heat map refresh failed: {ex}");
        }
    }
}
