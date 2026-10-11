using SvgCreator.Tracing;

namespace SvgCreator.Fitting;

/// <summary>
/// Turns pixel-edge contours into smooth cubic outlines:
/// densify to unit steps → take edge midpoints (removes the pixel staircase) → detect corners
/// (snapped back to the exact pixel corner) → Schneider-fit each corner-to-corner run.
/// Optionally, outlines that are ellipses are recognized first and kept as exact ellipses.
/// </summary>
internal static class CurveBuilder
{
    // Outlines this short (e.g. a lone 1–2 px speck) are kept as their exact pixel polygon.
    private const int MinFitPoints = 8;

    public static IReadOnlyList<CurveContour> Build(
        IReadOnlyList<Contour> contours, double tolerance, double cornerAngle, bool detectEllipses = false)
    {
        var result = new List<CurveContour>(contours.Count);
        foreach (var contour in contours)
        {
            result.Add(BuildContour(contour, tolerance, cornerAngle, detectEllipses));
        }

        return result;
    }

    private static CurveContour BuildContour(Contour contour, double tolerance, double cornerAngle, bool detectEllipses)
    {
        var vertices = contour.Points;
        var lattice = Densify(vertices);
        if (lattice.Length <= MinFitPoints)
        {
            var polygon = new List<CubicBezier>(vertices.Count);
            for (var i = 0; i < vertices.Count; i++)
            {
                polygon.Add(CubicBezier.Line(ToD(vertices[i]), ToD(vertices[(i + 1) % vertices.Count])));
            }

            return new CurveContour(contour.Label, contour.IsHole, polygon);
        }

        var n = lattice.Length;
        var points = new PointD[n];
        for (var i = 0; i < n; i++)
        {
            points[i] = (ToD(lattice[i]) + ToD(lattice[(i + 1) % n])) * 0.5;
        }

        if (detectEllipses && EllipseFitter.TryFit(points, tolerance, out var ellipse))
        {
            return new CurveContour(contour.Label, contour.IsHole, [], ellipse);
        }

        return new CurveContour(contour.Label, contour.IsHole, FitSegments(points, lattice, tolerance, cornerAngle));
    }

    private static List<CubicBezier> FitSegments(PointD[] points, PointI[] lattice, double tolerance, double cornerAngle)
    {
        var n = points.Length;
        var segments = new List<CubicBezier>();
        var corners = CornerDetector.Find(points, cornerAngle);
        SnapCornersToLattice(points, lattice, corners);

        if (corners.Count == 0)
        {
            // Smooth closed loop: split into two halves joined with shared tangents.
            var half = n / 2;
            var t0 = Tangent(points, 0);
            var tHalf = Tangent(points, half);
            BezierFitter.Fit(Slice(points, 0, half), t0, -tHalf, tolerance, segments);
            BezierFitter.Fit(Slice(points, half, n), tHalf, -t0, tolerance, segments);
            return segments;
        }

        for (var c = 0; c < corners.Count; c++)
        {
            var start = corners[c];
            var end = c + 1 < corners.Count ? corners[c + 1] : corners[0] + n;
            var run = Slice(points, start, end);
            BezierFitter.Fit(run, BezierFitter.StartTangent(run), BezierFitter.EndTangent(run), tolerance, segments);
        }

        return segments;
    }

    // Expands corner vertices into every unit step along the pixel edges.
    private static PointI[] Densify(IReadOnlyList<PointI> vertices)
    {
        var dense = new List<PointI>();
        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
            for (var p = a; p != b; p = new PointI(p.X + dx, p.Y + dy))
            {
                dense.Add(p);
            }
        }

        return [.. dense];
    }

    // Midpoint i lies between lattice[i] and lattice[i+1]; move a corner onto that pixel corner when
    // exactly one of them is a turn. When both are (a 1 px wide tip) the midpoint is the better apex.
    private static void SnapCornersToLattice(PointD[] points, PointI[] lattice, List<int> corners)
    {
        var n = lattice.Length;
        foreach (var i in corners)
        {
            bool turnBefore = IsTurn(lattice, i), turnAfter = IsTurn(lattice, (i + 1) % n);
            if (turnAfter && !turnBefore)
            {
                points[i] = ToD(lattice[(i + 1) % n]);
            }
            else if (turnBefore && !turnAfter)
            {
                points[i] = ToD(lattice[i]);
            }
        }

        // Two corners snapped onto the same pixel corner: keep one.
        for (var c = corners.Count - 1; c >= 0 && corners.Count > 1; c--)
        {
            var next = corners[(c + 1) % corners.Count];
            if (points[next] == points[corners[c]])
            {
                corners.RemoveAt(c);
            }
        }
    }

    private static bool IsTurn(PointI[] lattice, int j)
    {
        var n = lattice.Length;
        PointI prev = lattice[(j - 1 + n) % n], cur = lattice[j], next = lattice[(j + 1) % n];
        return (cur.X - prev.X) != (next.X - cur.X) || (cur.Y - prev.Y) != (next.Y - cur.Y);
    }

    // Copies points[start..end] inclusive, wrapping around the closed loop.
    private static PointD[] Slice(PointD[] points, int start, int end)
    {
        var n = points.Length;
        var run = new PointD[end - start + 1];
        for (var i = 0; i < run.Length; i++)
        {
            run[i] = points[(start + i) % n];
        }

        return run;
    }

    // Forward tangent at index i of a closed loop.
    private static PointD Tangent(PointD[] points, int i)
    {
        var n = points.Length;
        var k = Math.Min(3, n / 4);
        return (points[(i + k) % n] - points[(i - k + n) % n]).Normalize(new PointD(1, 0));
    }

    private static PointD ToD(PointI p) => new(p.X, p.Y);
}
