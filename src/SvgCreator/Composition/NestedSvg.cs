using System.Globalization;
using System.Xml;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Composition;

/// <summary>
/// Places one generated SVG document inside another as an inner <c>&lt;svg&gt;</c>, which keeps
/// its own coordinates: how the composers combine pieces made by other generators, unchanged.
/// </summary>
internal static class NestedSvg
{
    // A generated document's canvas size, from its root.
    public static (double Width, double Height) Size(string svg)
    {
        using var reader = XmlReader.Create(new StringReader(svg));
        reader.MoveToContent();
        return (double.Parse(reader.GetAttribute("width")!, CultureInfo.InvariantCulture), double.Parse(reader.GetAttribute("height")!, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes a generated document as an inner <c>&lt;svg&gt;</c> filling <paramref name="rect"/>:
    /// its view box maps its own canvas onto the rectangle, and its children are copied as they are.
    /// </summary>
    public static void Nest(XmlWriter xml, string className, string svg, (double X, double Y, double Width, double Height) rect)
    {
        using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings { IgnoreWhitespace = true });
        reader.MoveToContent();

        xml.WriteStartElement("svg", Namespace);
        xml.WriteAttributeString("class", className);
        xml.WriteAttributeString("x", Number(rect.X));
        xml.WriteAttributeString("y", Number(rect.Y));
        xml.WriteAttributeString("width", Number(rect.Width));
        xml.WriteAttributeString("height", Number(rect.Height));
        xml.WriteAttributeString("viewBox", reader.GetAttribute("viewBox"));

        if (!reader.IsEmptyElement)
        {
            reader.Read();
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                xml.WriteNode(reader, defattr: true);
            }
        }

        xml.WriteEndElement();
    }
}
