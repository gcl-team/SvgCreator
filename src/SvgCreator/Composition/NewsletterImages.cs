using System.Xml;
using SvgCreator.Imaging;
using SvgCreator.Mapping;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Composition;

/// <summary>
/// The newsletter's pictures, each embedded once however often it's drawn. Layout first records
/// every place a picture goes (<see cref="Need"/>); each picture is then scaled down to what its
/// largest place needs and written once as an <c>&lt;image&gt;</c> in the definitions, at its
/// displayed size whatever its pixels, so every <c>&lt;use&gt;</c> of it places it the same way.
/// </summary>
internal sealed class NewsletterImages(IReadOnlyDictionary<string, EncodedImage> images, double density)
{
    private readonly Dictionary<string, double> needed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> ids = new(StringComparer.Ordinal);
    private int clips;

    /// <summary>The picture's displayed size: its shape, for layout.</summary>
    public (double Width, double Height) Size(string name) => (images[name].Width, images[name].Height);

    /// <summary>The picture itself, as read.</summary>
    public EncodedImage this[string name] => images[name];

    /// <summary>
    /// Records that the picture is drawn into a box this size: filling it, cropped
    /// (<paramref name="cover"/>), or whole inside it.
    /// </summary>
    public void Need(string name, double width, double height, bool cover)
    {
        var scale = Scale(name, width, height, cover);
        needed[name] = Math.Max(needed.GetValueOrDefault(name), scale);
    }

    /// <summary>The picture at the most pixels a box this size shows with the density asked for.</summary>
    public EncodedImage Sized(string name, double width, double height, bool cover)
    {
        var image = images[name];
        var scale = Scale(name, width, height, cover) * density;
        return density > 0 && scale < 1
            ? ImageLoader.Downscale(image, (int)Math.Ceiling(image.Width * scale), (int)Math.Ceiling(image.Height * scale))
            : image;
    }

    /// <summary>Writes each recorded picture once, scaled down to what its largest box needs.</summary>
    public void WriteDefinitions(XmlWriter xml)
    {
        foreach (var (name, scale) in needed)
        {
            var image = images[name];
            var embedded = density > 0 && scale * density < 1
                ? ImageLoader.Downscale(image, (int)Math.Ceiling(image.Width * scale * density), (int)Math.Ceiling(image.Height * scale * density))
                : image;
            var id = $"newsletter-image-{ids.Count + 1}";
            ids[name] = id;

            // At the displayed size, whatever its pixels, so scaling down doesn't move anything.
            xml.WriteStartElement("image", Namespace);
            xml.WriteAttributeString("id", id);
            xml.WriteAttributeString("width", Integer(image.Width));
            xml.WriteAttributeString("height", Integer(image.Height));
            xml.WriteAttributeString("preserveAspectRatio", "none");
            xml.WriteAttributeString("href", embedded.DataUri);
            xml.WriteEndElement();
        }
    }

    /// <summary>
    /// Draws the picture into <paramref name="box"/>, centred: filling it and cropped to it,
    /// rounded by <paramref name="radius"/> (<paramref name="cover"/>), or whole inside it.
    /// Returns where the picture went: the box, or the part of it the whole picture fills.
    /// </summary>
    public RectD Draw(XmlWriter xml, string name, RectD box, bool cover, double radius = 0)
    {
        var (width, height) = Size(name);
        var scale = Scale(name, box.Width, box.Height, cover);
        var (w, h) = (width * scale, height * scale);
        var (x, y) = (box.X + ((box.Width - w) / 2), box.Y + ((box.Height - h) / 2));
        var drawn = cover ? box : new RectD(x, y, w, h);

        string? clip = null;
        if (cover || radius > 0)
        {
            clip = NextClipId();
            xml.WriteStartElement("clipPath", Namespace);
            xml.WriteAttributeString("id", clip);
            Rect(xml, drawn, radius);
            xml.WriteEndElement();
        }

        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("class", "newsletter-picture");
        if (clip is not null)
        {
            xml.WriteAttributeString("clip-path", $"url(#{clip})");
        }

        xml.WriteStartElement("use", Namespace);
        xml.WriteAttributeString("href", $"#{ids[name]}");
        xml.WriteAttributeString("transform", $"translate({Number(x)} {Number(y)}) scale({Number(scale)})");
        xml.WriteEndElement();
        xml.WriteEndElement();
        return drawn;
    }

    /// <summary>A new id for a clip path, unique in the document.</summary>
    public string NextClipId() => $"newsletter-clip-{++clips}";

    /// <summary>
    /// Writes a rectangle, rounded when <paramref name="radius"/> is above 0; with
    /// <paramref name="close"/> false, the element is left open for more attributes.
    /// </summary>
    internal static void Rect(XmlWriter xml, RectD rect, double radius, bool close = true)
    {
        xml.WriteStartElement("rect", Namespace);
        xml.WriteAttributeString("x", Number(rect.X));
        xml.WriteAttributeString("y", Number(rect.Y));
        xml.WriteAttributeString("width", Number(rect.Width));
        xml.WriteAttributeString("height", Number(rect.Height));
        if (radius > 0)
        {
            xml.WriteAttributeString("rx", Number(radius));
        }

        if (close)
        {
            xml.WriteEndElement();
        }
    }

    private double Scale(string name, double width, double height, bool cover)
    {
        var (w, h) = Size(name);
        return cover ? Math.Max(width / w, height / h) : Math.Min(width / w, height / h);
    }
}
