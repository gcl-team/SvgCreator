namespace SvgCreator.Fitting;

/// <summary>
/// Closed outline made of chained cubic segments; see <see cref="Tracing.Contour"/>. When the outline
/// was recognized as an ellipse, <see cref="Ellipse"/> is set and <see cref="Segments"/> is empty.
/// </summary>
internal sealed record CurveContour(int Label, bool IsHole, IReadOnlyList<CubicBezier> Segments, Ellipse? Ellipse = null);
