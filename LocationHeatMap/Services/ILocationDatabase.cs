using LocationHeatMap.Models;

namespace LocationHeatMap.Services;

/// <summary>
/// Persistence for recorded GPS fixes. Exposed as an interface so the view model depends on the
/// contract rather than on sqlite-net, which keeps the view model unit-testable.
/// </summary>
public interface ILocationDatabase
{
    /// <summary>Persists a single fix.</summary>
    Task AddLocationAsync(double latitude, double longitude, double? accuracyMeters, DateTime recordedAtUtc);

    /// <summary>Returns the total number of stored fixes.</summary>
    Task<int> GetCountAsync();

    /// <summary>Returns the most recently recorded fix, or <c>null</c> when the table is empty.</summary>
    Task<LocationRecord?> GetMostRecentAsync();

    /// <summary>
    /// Aggregates every stored fix onto a lat/lon grid of the given cell size (in degrees) and
    /// returns one <see cref="HeatCell"/> per occupied cell.
    /// </summary>
    Task<List<HeatCell>> GetHeatCellsAsync(double cellSizeDegrees);

    /// <summary>Deletes every stored fix.</summary>
    Task ClearAsync();
}
