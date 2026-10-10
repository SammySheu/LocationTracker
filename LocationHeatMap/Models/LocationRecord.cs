using SQLite;

namespace LocationHeatMap.Models;

/// <summary>
/// A single GPS fix, as persisted in the SQLite database.
/// </summary>
/// <remarks>
/// One row is written per sampling tick while tracking is active. The heat map is built by
/// counting how many rows fall inside each cell of a lat/lon grid, so rows are deliberately
/// <em>not</em> de-duplicated by position: a user standing still keeps producing rows at the
/// same coordinates, and that accumulation is exactly what turns a cell from blue to red.
/// </remarks>
[Table("LocationRecord")]
public class LocationRecord
{
    /// <summary>Surrogate key assigned by SQLite.</summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Latitude in decimal degrees, WGS 84.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude in decimal degrees, WGS 84.</summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Horizontal accuracy of the fix in metres, as reported by the platform, or
    /// <c>null</c> when the platform did not supply one.
    /// </summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>
    /// When the fix was recorded, in UTC. Indexed because the app orders by it to find the
    /// most recent fix when restoring the map position on launch.
    /// </summary>
    [Indexed]
    public DateTime RecordedAtUtc { get; set; }
}
