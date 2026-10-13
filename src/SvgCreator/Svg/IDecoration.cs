using System.Xml;

namespace SvgCreator.Svg;

/// <summary>
/// Generated (not traced) artwork drawn over the traced drawing, such as postmark cancellation
/// lines. Each decoration is its own group, so it can be removed or restyled separately.
/// </summary>
internal interface IDecoration
{
    /// <summary>Canvas width the decoration needs to be shown in full; the canvas grows to the widest.</summary>
    int RequiredWidth { get; }

    /// <summary>Writes the decoration's group in <paramref name="color"/>, under the ink filter if there is one.</summary>
    void Write(XmlWriter xml, string color, string? filterId);
}
