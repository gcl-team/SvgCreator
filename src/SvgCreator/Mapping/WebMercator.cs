using SvgCreator.Fitting;

namespace SvgCreator.Mapping;

/// <summary>
/// The Web Mercator projection (EPSG:3857), the one Google Maps and OpenStreetMap draw with, so
/// stops sit where they would on those maps. Coordinates are normalised to the world square:
/// x runs 0 → 1 west to east from the antimeridian, y runs 0 → 1 north to south.
/// </summary>
internal static class WebMercator
{
    /// <summary>The projection reaches infinity at the poles; web maps cut the world off here, where it becomes a square.</summary>
    public const double MaxLatitude = 85.05112878;

    public static PointD Project(double latitude, double longitude)
    {
        var phi = double.DegreesToRadians(latitude);
        var x = (longitude + 180) / 360;
        var y = (1 - (Math.Log(Math.Tan(phi) + (1 / Math.Cos(phi))) / Math.PI)) / 2;
        return new PointD(x, y);
    }
}
