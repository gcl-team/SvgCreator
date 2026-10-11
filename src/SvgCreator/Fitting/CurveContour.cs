namespace SvgCreator.Fitting;

/// <summary>Closed outline made of chained cubic segments; see <see cref="Tracing.Contour"/>.</summary>
internal sealed record CurveContour(int Label, bool IsHole, IReadOnlyList<CubicBezier> Segments);
