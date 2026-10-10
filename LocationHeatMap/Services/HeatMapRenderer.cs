using LocationHeatMap.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

// "Map" is ambiguous: MAUI Essentials has Microsoft.Maui.ApplicationModel.Map (the helper that
// opens the system maps app), and the control is Microsoft.Maui.Controls.Maps.Map. Alias the
// control so the two cannot be confused.
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace LocationHeatMap.Services;

/// <summary>
/// Turns aggregated <see cref="HeatCell"/> data into map overlays, and applies them to a
/// <see cref="Map"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class is where the .NET MAUI <see cref="Map"/> control gets extended into a heat map.
/// The control ships with no heat layer of its own, but it does expose
/// <see cref="Map.MapElements"/>, and <see cref="Circle"/> lets each element carry its own
/// centre, radius and fill colour. Drawing one translucent circle per occupied grid cell, with
/// the colour driven by that cell's visit count, produces a heat map out of primitives the
/// control already renders natively on both Apple Maps and Google Maps.
/// </para>
/// <para>
/// The alternative — a custom platform handler wrapping each vendor's own heat map tile overlay
/// — would mean two separate native implementations and would lose the shared rendering code.
/// </para>
/// </remarks>
public static class HeatMapRenderer
{
    /// <summary>
    /// Width and height of a grid cell in degrees of latitude. At roughly 111 km per degree,
    /// 0.0003 degrees is about 33 m, which is a sensible granularity for walking and driving
    /// traces: fine enough to follow a road, coarse enough that GPS jitter around a single spot
    /// still lands in one cell.
    /// </summary>
    public const double CellSizeDegrees = 0.0003;

    /// <summary>
    /// Radius drawn for each cell, in metres. Slightly larger than half a cell so that adjacent
    /// cells along a path overlap into a continuous ribbon rather than a dotted line.
    /// </summary>
    private const double CircleRadiusMeters = 22;

    /// <summary>Opacity of the circle fill, leaving the basemap readable underneath.</summary>
    private const float FillAlpha = 0.55f;

    /// <summary>
    /// Colour ramp from coldest to hottest. Intermediate colours are produced by interpolating
    /// between adjacent stops, which is what gives the smooth blue-to-red gradient.
    /// </summary>
    private static readonly Color[] Ramp =
    [
        Color.FromRgb(0, 80, 255),    // blue   - visited once
        Color.FromRgb(0, 200, 255),   // cyan
        Color.FromRgb(120, 230, 0),   // lime
        Color.FromRgb(255, 215, 0),   // yellow
        Color.FromRgb(255, 0, 0)      // red    - the busiest cell
    ];

    /// <summary>
    /// Replaces the map's overlays with one circle per supplied cell.
    /// </summary>
    /// <remarks>
    /// Clearing and refilling <see cref="Map.MapElements"/> is real native work on both
    /// platforms, so callers should debounce this rather than invoke it on every incoming fix.
    /// Must be called on the UI thread.
    /// </remarks>
    public static void Apply(Map map, IReadOnlyList<HeatCell> cells)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(cells);

        map.MapElements.Clear();

        if (cells.Count == 0)
        {
            return;
        }

        // Normalise against the busiest cell so the ramp always spans its full range, whether
        // the session recorded a dozen fixes or ten thousand.
        var maxCount = cells.Max(cell => cell.Count);

        foreach (var cell in cells)
        {
            var intensity = NormalizeIntensity(cell.Count, maxCount);
            var fill = Interpolate(intensity).WithAlpha(FillAlpha);

            map.MapElements.Add(new Circle
            {
                Center = new Location(cell.Latitude, cell.Longitude),
                Radius = Distance.FromMeters(CircleRadiusMeters),
                FillColor = fill,
                // A stroke of the same hue but fainter softens the edge where circles overlap.
                StrokeColor = fill.WithAlpha(FillAlpha * 0.5f),
                StrokeWidth = 1
            });
        }
    }

    /// <summary>
    /// Maps a visit count onto 0..1 using a logarithmic curve.
    /// </summary>
    /// <remarks>
    /// Linear normalisation behaves badly here. A single long stop produces a count orders of
    /// magnitude above everything else, which would push every other cell to the cold end of
    /// the ramp and flatten the map to a uniform blue. Compressing with a logarithm keeps the
    /// mid-range legible while still giving the hotspot the top of the ramp.
    /// </remarks>
    private static float NormalizeIntensity(int count, int maxCount)
    {
        if (maxCount <= 1)
        {
            // Every cell was visited exactly once: nothing to grade, so draw them all cold.
            return 0f;
        }

        var scaled = Math.Log(count) / Math.Log(maxCount);
        return (float)Math.Clamp(scaled, 0d, 1d);
    }

    /// <summary>
    /// Returns the colour at position <paramref name="intensity"/> (0..1) along
    /// <see cref="Ramp"/>, interpolating linearly in RGB between the two neighbouring stops.
    /// </summary>
    private static Color Interpolate(float intensity)
    {
        var clamped = Math.Clamp(intensity, 0f, 1f);

        // Position along the ramp expressed in "segments", e.g. 2.4 means 40% of the way from
        // stop 2 to stop 3.
        var position = clamped * (Ramp.Length - 1);
        var lowerIndex = (int)Math.Floor(position);

        if (lowerIndex >= Ramp.Length - 1)
        {
            return Ramp[^1];
        }

        var lower = Ramp[lowerIndex];
        var upper = Ramp[lowerIndex + 1];
        var fraction = position - lowerIndex;

        return Color.FromRgb(
            lower.Red + (upper.Red - lower.Red) * fraction,
            lower.Green + (upper.Green - lower.Green) * fraction,
            lower.Blue + (upper.Blue - lower.Blue) * fraction);
    }
}
