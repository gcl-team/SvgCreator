namespace SvgCreator.Fitting;

/// <summary>
/// Least-squares cubic Bézier fitting of digitized curves, after Philip J. Schneider,
/// "An Algorithm for Automatically Fitting Digitized Curves" (Graphics Gems, 1990).
/// Fits one cubic; if the worst point is too far off it re-parameterizes with Newton–Raphson,
/// and failing that splits at the worst point with a shared tangent and recurses.
/// </summary>
internal static class BezierFitter
{
    private const int MaxIterations = 4;

    /// <param name="points">Points to fit, in order (at least 2).</param>
    /// <param name="startTangent">Unit tangent leaving the first point.</param>
    /// <param name="endTangent">Unit tangent leaving the last point back into the curve.</param>
    /// <param name="tolerance">Maximum allowed distance from any point to the curve.</param>
    /// <param name="output">Receives the fitted segments in order.</param>
    public static void Fit(ReadOnlySpan<PointD> points, PointD startTangent, PointD endTangent, double tolerance, List<CubicBezier> output) =>
        FitCubic(points, startTangent, endTangent, tolerance * tolerance, output);

    /// <summary>
    /// Tangent at <paramref name="points"/>[0] pointing into the curve. Estimated over a few points to
    /// ride out pixel noise, skipping the endpoint itself: a corner endpoint snapped to a pixel corner
    /// sits up to half a pixel off the run's line and would tilt the tangent.
    /// </summary>
    public static PointD StartTangent(ReadOnlySpan<PointD> points)
    {
        var last = points.Length - 1;
        var fallback = (points[1] - points[0]).Normalize(new PointD(1, 0));
        return last >= 4 ? (points[4] - points[1]).Normalize(fallback) : (points[Math.Min(3, last)] - points[0]).Normalize(fallback);
    }

    /// <summary>Tangent at the last point pointing back into the curve; see <see cref="StartTangent"/>.</summary>
    public static PointD EndTangent(ReadOnlySpan<PointD> points)
    {
        var last = points.Length - 1;
        var fallback = (points[last - 1] - points[last]).Normalize(new PointD(-1, 0));
        return last >= 4 ? (points[last - 4] - points[last - 1]).Normalize(fallback) : (points[last - Math.Min(3, last)] - points[last]).Normalize(fallback);
    }

    private static void FitCubic(ReadOnlySpan<PointD> d, PointD tHat1, PointD tHat2, double errorSquared, List<CubicBezier> output)
    {
        if (d.Length == 2)
        {
            var dist = (d[1] - d[0]).Length / 3;
            output.Add(new CubicBezier(d[0], d[0] + (tHat1 * dist), d[1] + (tHat2 * dist), d[1]));
            return;
        }

        var u = ChordLengthParameterize(d);
        var curve = GenerateBezier(d, u, tHat1, tHat2);
        var (maxError, split) = ComputeMaxError(d, curve, u);
        if (maxError < errorSquared)
        {
            output.Add(curve);
            return;
        }

        // Close enough that a better parameterization may fix it before splitting.
        if (maxError < errorSquared * 4)
        {
            for (var i = 0; i < MaxIterations; i++)
            {
                Reparameterize(d, u, curve);
                curve = GenerateBezier(d, u, tHat1, tHat2);
                (maxError, split) = ComputeMaxError(d, curve, u);
                if (maxError < errorSquared)
                {
                    output.Add(curve);
                    return;
                }
            }
        }

        var center = CenterTangent(d, split);
        FitCubic(d[..(split + 1)], tHat1, center, errorSquared, output);
        FitCubic(d[split..], -center, tHat2, errorSquared, output);
    }

    // Solves for the control-point distances along the end tangents that minimise squared error.
    private static CubicBezier GenerateBezier(ReadOnlySpan<PointD> d, double[] u, PointD tHat1, PointD tHat2)
    {
        var first = d[0];
        var last = d[^1];
        double c00 = 0, c01 = 0, c11 = 0, x0 = 0, x1 = 0;

        for (var i = 0; i < d.Length; i++)
        {
            var t = u[i];
            var mt = 1 - t;
            double b0 = mt * mt * mt, b1 = 3 * t * mt * mt, b2 = 3 * t * t * mt, b3 = t * t * t;

            var a1 = tHat1 * b1;
            var a2 = tHat2 * b2;
            c00 += a1.Dot(a1);
            c01 += a1.Dot(a2);
            c11 += a2.Dot(a2);

            var tmp = d[i] - ((first * (b0 + b1)) + (last * (b2 + b3)));
            x0 += a1.Dot(tmp);
            x1 += a2.Dot(tmp);
        }

        var det = (c00 * c11) - (c01 * c01);
        var alphaL = Math.Abs(det) < 1e-12 ? 0 : ((x0 * c11) - (x1 * c01)) / det;
        var alphaR = Math.Abs(det) < 1e-12 ? 0 : ((c00 * x1) - (c01 * x0)) / det;

        // Degenerate or backwards solution: fall back to the standard heuristic.
        var segmentLength = (last - first).Length;
        var epsilon = 1e-6 * segmentLength;
        if (alphaL < epsilon || alphaR < epsilon)
        {
            alphaL = alphaR = segmentLength / 3;
        }

        return new CubicBezier(first, first + (tHat1 * alphaL), last + (tHat2 * alphaR), last);
    }

    private static double[] ChordLengthParameterize(ReadOnlySpan<PointD> d)
    {
        var u = new double[d.Length];
        for (var i = 1; i < d.Length; i++)
        {
            u[i] = u[i - 1] + (d[i] - d[i - 1]).Length;
        }

        var total = u[^1];
        for (var i = 1; i < d.Length; i++)
        {
            u[i] = total > 0 ? u[i] / total : (double)i / (d.Length - 1);
        }

        return u;
    }

    private static (double MaxError, int Split) ComputeMaxError(ReadOnlySpan<PointD> d, CubicBezier curve, double[] u)
    {
        var maxError = 0.0;
        var split = d.Length / 2;
        for (var i = 1; i < d.Length - 1; i++)
        {
            var error = (curve.Evaluate(u[i]) - d[i]).LengthSquared;
            if (error >= maxError)
            {
                maxError = error;
                split = i;
            }
        }

        return (maxError, split);
    }

    private static void Reparameterize(ReadOnlySpan<PointD> d, double[] u, CubicBezier curve)
    {
        for (var i = 0; i < d.Length; i++)
        {
            u[i] = NewtonRaphsonRootFind(curve, d[i], u[i]);
        }
    }

    // One Newton step towards the parameter of the curve point closest to p.
    private static double NewtonRaphsonRootFind(CubicBezier q, PointD p, double t)
    {
        PointD q1a = (q.P1 - q.P0) * 3, q1b = (q.P2 - q.P1) * 3, q1c = (q.P3 - q.P2) * 3;
        PointD q2a = (q1b - q1a) * 2, q2b = (q1c - q1b) * 2;

        var mt = 1 - t;
        var qt = q.Evaluate(t);
        var q1t = (q1a * (mt * mt)) + (q1b * (2 * mt * t)) + (q1c * (t * t));
        var q2t = (q2a * mt) + (q2b * t);

        var diff = qt - p;
        var numerator = diff.Dot(q1t);
        var denominator = q1t.Dot(q1t) + diff.Dot(q2t);
        return Math.Abs(denominator) < 1e-12 ? t : Math.Clamp(t - (numerator / denominator), 0, 1);
    }

    // Tangent at an interior split point, pointing backwards (end tangent of the left half).
    private static PointD CenterTangent(ReadOnlySpan<PointD> d, int split)
    {
        var k = Math.Max(1, Math.Min(3, Math.Min(split, d.Length - 1 - split)));
        return (d[split - k] - d[split + k]).Normalize((d[split - 1] - d[split + 1]).Normalize(new PointD(-1, 0)));
    }
}
