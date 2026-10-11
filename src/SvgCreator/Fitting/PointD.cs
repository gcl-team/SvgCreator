namespace SvgCreator.Fitting;

/// <summary>Floating-point 2D point / vector.</summary>
internal readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);

    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);

    public static PointD operator -(PointD a) => new(-a.X, -a.Y);

    public static PointD operator *(PointD a, double s) => new(a.X * s, a.Y * s);

    public double Length => Math.Sqrt(LengthSquared);

    public double LengthSquared => (X * X) + (Y * Y);

    public double Dot(PointD other) => (X * other.X) + (Y * other.Y);

    /// <summary>Unit vector in the same direction, or <paramref name="fallback"/> for a zero vector.</summary>
    public PointD Normalize(PointD fallback = default)
    {
        var length = Length;
        return length < 1e-12 ? fallback : new PointD(X / length, Y / length);
    }
}
