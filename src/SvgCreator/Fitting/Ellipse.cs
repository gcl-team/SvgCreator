namespace SvgCreator.Fitting;

/// <summary>
/// Ellipse in geometric form, canonicalized so <see cref="RadiusX"/> ≥ <see cref="RadiusY"/> and
/// <see cref="AngleDegrees"/> (rotation of the X radius, clockwise in y-down image space, as SVG's
/// <c>rotate()</c>) lies in [0, 180).
/// </summary>
internal readonly record struct Ellipse(PointD Center, double RadiusX, double RadiusY, double AngleDegrees)
{
    public double Area => Math.PI * RadiusX * RadiusY;

    /// <summary>
    /// Half the width and half the height of the ellipse's axis-aligned bounding box: how far it
    /// reaches left and right, and up and down, of its centre once rotated.
    /// </summary>
    public (double Width, double Height) HalfExtents
    {
        get
        {
            var angle = AngleDegrees * Math.PI / 180;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            return (
                Math.Sqrt(Math.Pow(RadiusX * cos, 2) + Math.Pow(RadiusY * sin, 2)),
                Math.Sqrt(Math.Pow(RadiusX * sin, 2) + Math.Pow(RadiusY * cos, 2)));
        }
    }

    /// <summary>Ramanujan's approximation, accurate to well under 0.1% for any eccentricity used here.</summary>
    public double Perimeter
    {
        get
        {
            var h = Math.Pow(RadiusX - RadiusY, 2) / Math.Pow(RadiusX + RadiusY, 2);
            return Math.PI * (RadiusX + RadiusY) * (1 + (3 * h / (10 + Math.Sqrt(4 - (3 * h)))));
        }
    }

    public bool IsCircle(double tolerance) => RadiusX - RadiusY <= tolerance;

    public PointD PointAt(double t)
    {
        var angle = AngleDegrees * Math.PI / 180;
        double x = RadiusX * Math.Cos(t), y = RadiusY * Math.Sin(t);
        return Center + new PointD((x * Math.Cos(angle)) - (y * Math.Sin(angle)), (x * Math.Sin(angle)) + (y * Math.Cos(angle)));
    }

    /// <summary>Approximate distance from <paramref name="p"/> to the ellipse (Sampson distance).</summary>
    public double DistanceTo(PointD p)
    {
        var angle = AngleDegrees * Math.PI / 180;
        var d = p - Center;
        var x = (d.X * Math.Cos(angle)) + (d.Y * Math.Sin(angle));
        var y = (-d.X * Math.Sin(angle)) + (d.Y * Math.Cos(angle));
        double a2 = RadiusX * RadiusX, b2 = RadiusY * RadiusY;
        var f = (x * x / a2) + (y * y / b2) - 1;
        var gx = 2 * x / a2;
        var gy = 2 * y / b2;
        var g = Math.Sqrt((gx * gx) + (gy * gy));
        return g < 1e-12 ? Math.Min(RadiusX, RadiusY) : Math.Abs(f) / g;
    }

    /// <summary>
    /// Treats <paramref name="outer"/> and <paramref name="inner"/> (the outline and hole of one ink
    /// component) as a band of uniform width, which SVG draws natively as a stroked ellipse.
    /// </summary>
    public static bool TryMakeRing(Ellipse outer, Ellipse inner, double tolerance, out Ellipse middle, out double width)
    {
        middle = default;
        width = 0;

        if ((outer.Center - inner.Center).Length > tolerance)
        {
            return false;
        }

        // Axis directions only matter when the ellipses are noticeably non-circular.
        var bothRound = outer.RadiusX / outer.RadiusY < 1.05 && inner.RadiusX / inner.RadiusY < 1.05;
        var angleGap = Math.Abs(outer.AngleDegrees - inner.AngleDegrees) % 180;
        if (!bothRound && Math.Min(angleGap, 180 - angleGap) > 5)
        {
            return false;
        }

        double gapX = outer.RadiusX - inner.RadiusX, gapY = outer.RadiusY - inner.RadiusY;
        if (gapX <= 0 || gapY <= 0 || Math.Abs(gapX - gapY) > tolerance)
        {
            return false;
        }

        width = (gapX + gapY) / 2;
        middle = new Ellipse(
            (outer.Center + inner.Center) * 0.5,
            (outer.RadiusX + inner.RadiusX) / 2,
            (outer.RadiusY + inner.RadiusY) / 2,
            outer.AngleDegrees);
        return true;
    }
}
