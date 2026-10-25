using SvgCreator.Fitting;

namespace SvgCreator.Mapping;

/// <summary>
/// Places projected points on the canvas: their bounding box is scaled uniformly (so the geography
/// keeps its shape) to fill <c>area</c> as far as the tighter side allows, and centred in it.
/// </summary>
internal static class MapFit
{
    public static PointD[] Fit(IReadOnlyList<PointD> projected, RectD area)
    {
        if (projected.Count == 0)
        {
            return [];
        }

        var minX = projected.Min(p => p.X);
        var maxX = projected.Max(p => p.X);
        var minY = projected.Min(p => p.Y);
        var maxY = projected.Max(p => p.Y);
        var (spanX, spanY) = (maxX - minX, maxY - minY);

        // A single place (or all stops at it) has no extent to scale; stops in a line along one
        // axis have extent only on that one, which then decides the scale alone.
        double scale;
        if (spanX <= 0 && spanY <= 0)
        {
            scale = 0;
        }
        else if (spanX <= 0)
        {
            scale = area.Height / spanY;
        }
        else if (spanY <= 0)
        {
            scale = area.Width / spanX;
        }
        else
        {
            scale = Math.Min(area.Width / spanX, area.Height / spanY);
        }

        var source = new PointD((minX + maxX) / 2, (minY + maxY) / 2);
        var target = area.Center;
        return [.. projected.Select(p => target + ((p - source) * scale))];
    }
}
