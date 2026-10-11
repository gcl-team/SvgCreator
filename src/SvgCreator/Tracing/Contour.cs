namespace SvgCreator.Tracing;

/// <summary>
/// Closed outline traced along pixel edges. Outer borders run clockwise (in y-down image
/// coordinates) and holes counter-clockwise; <see cref="Label"/> is the connected component it bounds.
/// </summary>
internal sealed record Contour(int Label, bool IsHole, IReadOnlyList<PointI> Points);
