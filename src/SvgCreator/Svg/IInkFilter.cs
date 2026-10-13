using System.Xml;

namespace SvgCreator.Svg;

/// <summary>
/// A render-time effect on the ink: an SVG <c>&lt;filter&gt;</c> applied to the traced drawing and
/// to every decoration, so they all look printed the same way. The geometry stays untouched.
/// </summary>
internal interface IInkFilter
{
    /// <summary>The filter's id. Ids are global when several SVGs are inlined into one HTML page.</summary>
    string Id { get; }

    /// <summary>Writes the <c>&lt;defs&gt;</c> holding the filter, covering the whole canvas.</summary>
    void WriteDefinition(XmlWriter xml, int canvasWidth, int canvasHeight);
}
