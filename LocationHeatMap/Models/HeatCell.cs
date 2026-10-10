namespace LocationHeatMap.Models;

/// <summary>
/// One cell of the heat map grid: a quantised coordinate plus the number of
/// <see cref="LocationRecord"/> rows that fell inside it.
/// </summary>
/// <remarks>
/// Produced by the aggregation query in <c>LocationDatabase.GetHeatCellsAsync</c>. The property
/// names must match the column aliases of that query, because sqlite-net maps result columns
/// onto properties by name.
/// </remarks>
public class HeatCell
{
    /// <summary>Latitude of the cell centre, in decimal degrees.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the cell centre, in decimal degrees.</summary>
    public double Longitude { get; set; }

    /// <summary>How many recorded fixes fell inside this cell. Always at least 1.</summary>
    public int Count { get; set; }
}
