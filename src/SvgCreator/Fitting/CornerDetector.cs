namespace SvgCreator.Fitting;

/// <summary>
/// Finds sharp corners on a closed, densely sampled outline so curve fitting can keep them crisp
/// (hair tips, chins, eye corners) instead of rounding them off.
/// </summary>
internal static class CornerDetector
{
    /// <summary>
    /// Returns indices (ascending) where the outline turns sharper than <paramref name="cornerAngle"/>.
    /// </summary>
    /// <param name="points">Closed outline (last point connects back to the first).</param>
    /// <param name="cornerAngle">Interior angle in degrees; 180 = straight, smaller = sharper.</param>
    public static List<int> Find(ReadOnlySpan<PointD> points, double cornerAngle)
    {
        var n = points.Length;
        var corners = new List<int>();

        // Measuring the angle across a few points ignores the ±0.5 px pixel staircase.
        var k = Math.Min(4, n / 4);
        if (k < 1)
        {
            return corners;
        }

        // cos of the angle at each point; larger = sharper, straight lines give -1. Averaging two spans
        // keeps the pixel-noise immunity of the wide one while the narrow one separates corners on
        // sides shorter than the wide span (where the wide span alone scores a flat plateau).
        var narrow = Math.Max(1, k / 2);
        var sharpness = new double[n];
        for (var i = 0; i < n; i++)
        {
            sharpness[i] = 0.5 * (Cosine(points, i, k) + Cosine(points, i, narrow));
        }

        var threshold = Math.Cos(cornerAngle * Math.PI / 180);

        // Suppression radius is narrower than the measuring span so two corners a few pixels apart
        // (e.g. both ends of a short side) are not merged.
        var radius = narrow;
        for (var i = 0; i < n; i++)
        {
            if (sharpness[i] <= threshold)
            {
                continue;
            }

            // Non-maximum suppression: keep only the sharpest point within ±radius (ties go to the lower index).
            var isMax = true;
            for (var j = -radius; j <= radius && isMax; j++)
            {
                var other = (i + j + n) % n;
                if (other != i && (sharpness[other] > sharpness[i] || (sharpness[other] == sharpness[i] && other < i)))
                {
                    isMax = false;
                }
            }

            if (isMax)
            {
                corners.Add(i);
            }
        }

        return corners;
    }

    private static double Cosine(ReadOnlySpan<PointD> points, int i, int span)
    {
        var n = points.Length;
        var a = points[(i - span + n) % n] - points[i];
        var b = points[(i + span) % n] - points[i];
        return a.Normalize().Dot(b.Normalize());
    }
}
