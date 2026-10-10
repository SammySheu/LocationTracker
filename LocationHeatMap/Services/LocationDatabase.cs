using LocationHeatMap.Models;
using SQLite;

namespace LocationHeatMap.Services;

/// <summary>
/// SQLite-backed implementation of <see cref="ILocationDatabase"/>, using sqlite-net's
/// asynchronous API so that database work never blocks the UI thread.
/// </summary>
/// <remarks>
/// Registered as a singleton in <c>MauiProgram</c>. A single long-lived connection is the
/// pattern sqlite-net is designed for; opening one per call would be both slower and prone to
/// "database is locked" errors.
/// </remarks>
public class LocationDatabase : ILocationDatabase
{
    /// <summary>File name of the database inside the app's private data directory.</summary>
    public const string DatabaseFileName = "locations.db3";

    // ReadWrite|Create: open or create the file.
    // SharedCache:      lets the connection be shared safely across the async calls below.
    private const SQLite.SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

    private readonly SemaphoreSlim _initGate = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    /// <summary>
    /// Full path to the database file. Lives under <see cref="FileSystem.AppDataDirectory"/>,
    /// which is private to the app and is backed up on both platforms.
    /// </summary>
    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    /// <summary>
    /// Opens the connection and creates the schema on first use. Guarded by a semaphore so that
    /// two concurrent callers cannot both run <c>CreateTableAsync</c>.
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _initGate.WaitAsync();
        try
        {
            // Re-check inside the lock: another caller may have initialised while we waited.
            if (_connection is null)
            {
                var connection = new SQLiteAsyncConnection(DatabasePath, Flags);
                await connection.CreateTableAsync<LocationRecord>();
                _connection = connection;
            }
        }
        finally
        {
            _initGate.Release();
        }

        return _connection;
    }

    /// <inheritdoc />
    public async Task AddLocationAsync(double latitude, double longitude, double? accuracyMeters, DateTime recordedAtUtc)
    {
        var connection = await GetConnectionAsync();
        await connection.InsertAsync(new LocationRecord
        {
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracyMeters,
            RecordedAtUtc = recordedAtUtc
        });
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<LocationRecord>().CountAsync();
    }

    /// <inheritdoc />
    public async Task<LocationRecord?> GetMostRecentAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.Table<LocationRecord>()
            .OrderByDescending(record => record.RecordedAtUtc)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The grouping is done in SQL rather than in C# on purpose. SQLite collapses thousands of
    /// rows down to the handful of occupied cells before any of it crosses into managed code,
    /// so the amount of data the UI has to touch stays proportional to the area covered rather
    /// than to how long the user has been tracking.
    ///
    /// <c>ROUND(Latitude / size) * size</c> snaps each fix to the nearest grid intersection,
    /// which is what makes two nearby fixes land in the same cell and have their counts merge.
    /// </remarks>
    public async Task<List<HeatCell>> GetHeatCellsAsync(double cellSizeDegrees)
    {
        if (cellSizeDegrees <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSizeDegrees), "Cell size must be greater than zero.");
        }

        var connection = await GetConnectionAsync();

        // Column aliases must match the property names on HeatCell: sqlite-net binds by name.
        const string sql = """
            SELECT ROUND(Latitude  / ?) * ? AS Latitude,
                   ROUND(Longitude / ?) * ? AS Longitude,
                   COUNT(*)                 AS Count
            FROM LocationRecord
            GROUP BY ROUND(Latitude / ?), ROUND(Longitude / ?)
            """;

        return await connection.QueryAsync<HeatCell>(
            sql,
            cellSizeDegrees, cellSizeDegrees,
            cellSizeDegrees, cellSizeDegrees,
            cellSizeDegrees, cellSizeDegrees);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        var connection = await GetConnectionAsync();
        await connection.DeleteAllAsync<LocationRecord>();
    }
}
