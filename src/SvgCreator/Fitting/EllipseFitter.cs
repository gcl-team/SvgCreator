namespace SvgCreator.Fitting;

/// <summary>
/// Recognizes outlines that are ellipses (or circles) so they can be written as native SVG shapes.
/// </summary>
/// <remarks>
/// Fitting is the direct least-squares method of Fitzgibbon, Pilu &amp; Fisher, "Direct Least Square
/// Fitting of Ellipses" (1999), in the numerically stable form of Halíř &amp; Flusser (1998): minimise
/// the algebraic error of the conic A x² + B xy + C y² + D x + E y + F = 0 subject to
/// 4AC − B² = 1, which always yields an ellipse. That reduces to a 3×3 eigenproblem.
/// <para>
/// A fit is only accepted when it truly describes the outline: every point within tolerance, the
/// outline's area matching the ellipse's (rejects thin arcs whose two sides hug one ellipse), and a
/// minor radius of at least <see cref="MinRadius"/> (rejects thin straight strokes, whose outline is
/// within a pixel of a very flat ellipse).
/// </para>
/// </remarks>
internal static class EllipseFitter
{
    private const double MinRadius = 4;
    private const int MinPoints = 16;

    public static bool TryFit(ReadOnlySpan<PointD> points, double tolerance, out Ellipse ellipse)
    {
        ellipse = default;
        if (points.Length < MinPoints || !TryFitConic(points, out ellipse) || ellipse.RadiusY < MinRadius)
        {
            return false;
        }

        // Small shapes get a proportionally stricter bound so e.g. rounded squares stay squares.
        var allowed = Math.Min(tolerance, Math.Max(0.5, 0.08 * ellipse.RadiusY));
        foreach (var p in points)
        {
            if (ellipse.DistanceTo(p) > allowed)
            {
                return false;
            }
        }

        // A band of width `allowed` along the perimeter bounds the area difference.
        return Math.Abs(Math.Abs(SignedArea(points)) - ellipse.Area) <= allowed * ellipse.Perimeter;
    }

    private static bool TryFitConic(ReadOnlySpan<PointD> points, out Ellipse ellipse)
    {
        ellipse = default;

        // Normalize (centroid at origin, RMS radius ~1) for numerical stability.
        double mx = 0, my = 0;
        foreach (var p in points)
        {
            mx += p.X;
            my += p.Y;
        }

        mx /= points.Length;
        my /= points.Length;
        double spread = 0;
        foreach (var p in points)
        {
            spread += ((p.X - mx) * (p.X - mx)) + ((p.Y - my) * (p.Y - my));
        }

        var scale = Math.Sqrt(spread / points.Length);
        if (scale < 1e-9)
        {
            return false;
        }

        // Scatter matrices: D1 = [x², xy, y²] (quadratic part), D2 = [x, y, 1] (linear part).
        var s1 = new double[3, 3];
        var s2 = new double[3, 3];
        var s3 = new double[3, 3];
        Span<double> q = stackalloc double[3];
        Span<double> l = stackalloc double[3];
        foreach (var p in points)
        {
            double x = (p.X - mx) / scale, y = (p.Y - my) / scale;
            q[0] = x * x;
            q[1] = x * y;
            q[2] = y * y;
            l[0] = x;
            l[1] = y;
            l[2] = 1;
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    s1[i, j] += q[i] * q[j];
                    s2[i, j] += q[i] * l[j];
                    s3[i, j] += l[i] * l[j];
                }
            }
        }

        if (!TryInvert(s3, out var s3Inverse))
        {
            return false;
        }

        // T = −S3⁻¹ S2ᵀ maps the quadratic coefficients to the linear ones; M = S1 + S2 T.
        var t = new double[3, 3];
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                for (var k = 0; k < 3; k++)
                {
                    t[i, j] -= s3Inverse[i, k] * s2[j, k];
                }
            }
        }

        var m = new double[3, 3];
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                m[i, j] = s1[i, j];
                for (var k = 0; k < 3; k++)
                {
                    m[i, j] += s2[i, k] * t[k, j];
                }
            }
        }

        // Premultiply by C1⁻¹ for the constraint 4AC − B² = 1: rows become (M₂/2, −M₁, M₀/2).
        var c = new double[3, 3];
        for (var j = 0; j < 3; j++)
        {
            c[0, j] = m[2, j] / 2;
            c[1, j] = -m[1, j];
            c[2, j] = m[0, j] / 2;
        }

        // The ellipse is the eigenvector with 4AC − B² > 0.
        Span<double> quadratic = stackalloc double[3];
        var found = false;
        foreach (var lambda in RealEigenvalues(c))
        {
            if (TryEigenvector(c, lambda, quadratic) && (4 * quadratic[0] * quadratic[2]) - (quadratic[1] * quadratic[1]) > 0)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        Span<double> linear = stackalloc double[3];
        for (var i = 0; i < 3; i++)
        {
            linear[i] = (t[i, 0] * quadratic[0]) + (t[i, 1] * quadratic[1]) + (t[i, 2] * quadratic[2]);
        }

        if (!TryToGeometric(quadratic[0], quadratic[1], quadratic[2], linear[0], linear[1], linear[2], out var normalized))
        {
            return false;
        }

        ellipse = normalized with
        {
            Center = new PointD((normalized.Center.X * scale) + mx, (normalized.Center.Y * scale) + my),
            RadiusX = normalized.RadiusX * scale,
            RadiusY = normalized.RadiusY * scale,
        };
        return true;
    }

    // Conic coefficients → centre, radii and rotation.
    private static bool TryToGeometric(double a, double b, double c, double d, double e, double f, out Ellipse ellipse)
    {
        ellipse = default;
        var det = (4 * a * c) - (b * b);
        if (det <= 0)
        {
            return false;
        }

        // Centre: where the gradient vanishes.
        var x0 = ((b * e) - (2 * c * d)) / det;
        var y0 = ((b * d) - (2 * a * e)) / det;
        var f0 = (a * x0 * x0) + (b * x0 * y0) + (c * y0 * y0) + (d * x0) + (e * y0) + f;

        // Principal axes of the quadratic form [[A, B/2], [B/2, C]].
        var theta = 0.5 * Math.Atan2(b, a - c);
        double cos = Math.Cos(theta), sin = Math.Sin(theta);
        var lambda1 = (a * cos * cos) + (b * cos * sin) + (c * sin * sin);
        var lambda2 = (a * sin * sin) - (b * cos * sin) + (c * cos * cos);
        if (-f0 / lambda1 <= 0 || -f0 / lambda2 <= 0)
        {
            return false;
        }

        var rx = Math.Sqrt(-f0 / lambda1);
        var ry = Math.Sqrt(-f0 / lambda2);
        var degrees = theta * 180 / Math.PI;
        if (rx < ry)
        {
            (rx, ry) = (ry, rx);
            degrees += 90;
        }

        degrees = ((degrees % 180) + 180) % 180;
        ellipse = new Ellipse(new PointD(x0, y0), rx, ry, degrees);
        return true;
    }

    // Real roots of the characteristic polynomial λ³ − tr·λ² + m·λ − det = 0.
    private static List<double> RealEigenvalues(double[,] m)
    {
        var trace = m[0, 0] + m[1, 1] + m[2, 2];
        var minors = (m[0, 0] * m[1, 1]) - (m[0, 1] * m[1, 0])
            + (m[0, 0] * m[2, 2]) - (m[0, 2] * m[2, 0])
            + (m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1]);
        var det = Determinant(m);
        return SolveCubic(-trace, minors, -det);
    }

    // Roots of λ³ + aλ² + bλ + c = 0 (Numerical Recipes' trigonometric / Cardano form).
    private static List<double> SolveCubic(double a, double b, double c)
    {
        var q = ((a * a) - (3 * b)) / 9;
        var r = ((2 * a * a * a) - (9 * a * b) + (27 * c)) / 54;
        var roots = new List<double>(3);
        if ((r * r) < (q * q * q))
        {
            var angle = Math.Acos(Math.Clamp(r / Math.Sqrt(q * q * q), -1, 1));
            var factor = -2 * Math.Sqrt(q);
            roots.Add((factor * Math.Cos(angle / 3)) - (a / 3));
            roots.Add((factor * Math.Cos((angle + (2 * Math.PI)) / 3)) - (a / 3));
            roots.Add((factor * Math.Cos((angle - (2 * Math.PI)) / 3)) - (a / 3));
        }
        else
        {
            var big = -Math.Sign(r) * Math.Cbrt(Math.Abs(r) + Math.Sqrt((r * r) - (q * q * q)));
            var small = big == 0 ? 0 : q / big;
            roots.Add(big + small - (a / 3));
        }

        return roots;
    }

    // Null-space vector of (M − λI): the largest cross product of two of its rows.
    private static bool TryEigenvector(double[,] m, double lambda, Span<double> vector)
    {
        Span<double> best = stackalloc double[3];
        var bestNorm = 0.0;
        for (var i = 0; i < 3; i++)
        {
            for (var j = i + 1; j < 3; j++)
            {
                double r0 = m[i, 0] - (i == 0 ? lambda : 0), r1 = m[i, 1] - (i == 1 ? lambda : 0), r2 = m[i, 2] - (i == 2 ? lambda : 0);
                double s0 = m[j, 0] - (j == 0 ? lambda : 0), s1 = m[j, 1] - (j == 1 ? lambda : 0), s2 = m[j, 2] - (j == 2 ? lambda : 0);
                double x = (r1 * s2) - (r2 * s1), y = (r2 * s0) - (r0 * s2), z = (r0 * s1) - (r1 * s0);
                var norm = (x * x) + (y * y) + (z * z);
                if (norm > bestNorm)
                {
                    bestNorm = norm;
                    best[0] = x;
                    best[1] = y;
                    best[2] = z;
                }
            }
        }

        if (bestNorm < 1e-24)
        {
            return false;
        }

        var length = Math.Sqrt(bestNorm);
        for (var k = 0; k < 3; k++)
        {
            vector[k] = best[k] / length;
        }

        return true;
    }

    private static bool TryInvert(double[,] m, out double[,] inverse)
    {
        inverse = new double[3, 3];
        var det = Determinant(m);
        if (Math.Abs(det) < 1e-12)
        {
            return false;
        }

        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                // Cofactor of (j, i), i.e. the adjugate's (i, j) entry.
                int r0 = (j + 1) % 3, r1 = (j + 2) % 3, c0 = (i + 1) % 3, c1 = (i + 2) % 3;
                inverse[i, j] = ((m[r0, c0] * m[r1, c1]) - (m[r0, c1] * m[r1, c0])) / det;
            }
        }

        return true;
    }

    private static double Determinant(double[,] m) =>
        (m[0, 0] * ((m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1])))
        - (m[0, 1] * ((m[1, 0] * m[2, 2]) - (m[1, 2] * m[2, 0])))
        + (m[0, 2] * ((m[1, 0] * m[2, 1]) - (m[1, 1] * m[2, 0])));

    private static double SignedArea(ReadOnlySpan<PointD> points)
    {
        var sum = 0.0;
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum / 2;
    }
}
