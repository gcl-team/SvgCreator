using System.Xml;

namespace SvgCreator.Svg;

/// <summary>
/// A render-time effect on the ink: an SVG <c>&lt;filter&gt;</c> applied to the traced drawing and
/// to every decoration, so they all look printed the same way. The geometry stays untouched.
/// </summary>
internal interface IInkFilter
{
    /// <summary>
    /// The filter's id on a canvas of this size. Ids are global once several SVGs share a page or
    /// a canvas, so the id names everything the definition depends on: two filters only share an id
    /// when their definitions are identical.
    /// </summary>
    string Id(int canvasWidth, int canvasHeight);

    /// <summary>Writes the <c>&lt;defs&gt;</c> holding the filter, covering the whole canvas.</summary>
    void WriteDefinition(XmlWriter xml, int canvasWidth, int canvasHeight);
}
