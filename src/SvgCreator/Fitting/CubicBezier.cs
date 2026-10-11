namespace SvgCreator.Fitting;

/// <summary>Cubic Bézier segment from <see cref="P0"/> to <see cref="P3"/>.</summary>
internal readonly record struct CubicBezier(PointD P0, PointD P1, PointD P2, PointD P3)
{
    /// <summary>A straight segment expressed as a cubic.</summary>
    public static CubicBezier Line(PointD from, PointD to) =>
        new(from, from + ((to - from) * (1.0 / 3)), from + ((to - from) * (2.0 / 3)), to);

    public PointD Evaluate(double t)
    {
        var mt = 1 - t;
        return (P0 * (mt * mt * mt)) + (P1 * (3 * mt * mt * t)) + (P2 * (3 * mt * t * t)) + (P3 * (t * t * t));
    }

    /// <summary>True when both control points lie within <paramref name="tolerance"/> of the chord.</summary>
    public bool IsNearlyLine(double tolerance) =>
        DistanceToChord(P1) <= tolerance && DistanceToChord(P2) <= tolerance;

    private double DistanceToChord(PointD p)
    {
        var chord = P3 - P0;
        var lengthSquared = chord.LengthSquared;
        var t = lengthSquared < 1e-12 ? 0 : Math.Clamp((p - P0).Dot(chord) / lengthSquared, 0, 1);
        return (p - (P0 + (chord * t))).Length;
    }
}
