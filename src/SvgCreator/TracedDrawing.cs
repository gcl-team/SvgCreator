using SvgCreator.Fitting;

namespace SvgCreator;

/// <summary>
/// The traced line art of one image: fitted outlines on a canvas of the image's size. What gets
/// rendered, independent of how it looks (<see cref="Svg.SvgStyle"/>).
/// </summary>
internal sealed record TracedDrawing(int Width, int Height, IReadOnlyList<CurveContour> Contours);
