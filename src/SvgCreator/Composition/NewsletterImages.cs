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
/// In a <paramref name="template"/>, photos are slots instead (<see cref="Slot"/>): not embedded,
/// and holes in their place (<see cref="WriteHoles"/>).
/// </summary>
internal sealed class NewsletterImages(IReadOnlyDictionary<string, EncodedImage> images, double density, bool template = false)
{
    internal const string HolesId = "newsletter-template-holes";

    private readonly Dictionary<string, double> needed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> ids = new(StringComparer.Ordinal);
    private readonly List<(RectD Box, double Radius, string? Clip)> holes = [];
    private int clips;

    /// <summary>Whether photos are slots, not pictures.</summary>
    public bool Template => template;

    /// <summary>The picture's displayed size: its shape, for layout.</summary>
    public (double Width, double Height) Size(string name) => (images[name].Width, images[name].Height);

    /// <summary>The picture itself, as read.</summary>
    public EncodedImage this[string name] => images[name];

    /// <summary>
    /// Records that the picture is drawn into a box this size: filling it, cropped
    /// (<paramref name="cover"/>), or whole inside it. A <paramref name="photo"/> in a template is
    /// a slot, so it isn't recorded and isn't embedded.
    /// </summary>
    public void Need(string name, double width, double height, bool cover, bool photo = false)
    {
        if (template && photo)
        {
            return;
        }

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
    public RectD Draw(XmlWriter xml, string name, RectD box, bool cover, double radius = 0, Tint? tint = null)
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

        // A gradient blends with the picture alone, not with the page behind it.
        if (tint is { Mode: NewsletterPhotoTint.Gradient })
        {
            xml.WriteAttributeString("style", "isolation:isolate");
        }

        var transform = $"translate({Number(x)} {Number(y)}) scale({Number(scale)})";
        xml.WriteStartElement("use", Namespace);
        xml.WriteAttributeString("href", $"#{ids[name]}");
        xml.WriteAttributeString("transform", transform);
        if (tint is { Mode: NewsletterPhotoTint.Duotone, Strength: >= 1 })
        {
            xml.WriteAttributeString("filter", $"url(#{DuotoneId})");
        }

        xml.WriteEndElement();

        switch (tint)
        {
            // Weaker than full, the duotone is a second copy over the picture, partly see-through.
            case { Mode: NewsletterPhotoTint.Duotone, Strength: > 0 and < 1 }:
                xml.WriteStartElement("use", Namespace);
                xml.WriteAttributeString("href", $"#{ids[name]}");
                xml.WriteAttributeString("transform", transform);
                xml.WriteAttributeString("filter", $"url(#{DuotoneId})");
                xml.WriteAttributeString("opacity", Number(tint.Strength));
                xml.WriteEndElement();
                break;

            // The gradient over the picture, blended as "color": the gradient's hue and saturation
            // with the picture's brightness.
            case { Mode: NewsletterPhotoTint.Gradient, Strength: > 0 }:
                Rect(xml, drawn, 0, close: false);
                xml.WriteAttributeString("class", "newsletter-tint");
                xml.WriteAttributeString("fill", $"url(#{GradientId})");
                if (tint.Strength < 1)
                {
                    xml.WriteAttributeString("opacity", Number(tint.Strength));
                }

                xml.WriteAttributeString("style", "mix-blend-mode:color");
                xml.WriteEndElement();
                break;
        }

        xml.WriteEndElement();
        return drawn;
    }

    /// <summary>
    /// A photo's slot in a template: an invisible rectangle naming the photo, for finding it in an
    /// editor, and a hole of its shape recorded for <see cref="WriteHoles"/>, rounded by
    /// <paramref name="radius"/> and cut by <paramref name="clip"/> (the hero's edge), if given.
    /// </summary>
    public void Slot(XmlWriter xml, string name, RectD box, double radius = 0, string? clip = null)
    {
        holes.Add((box, radius, clip));
        Rect(xml, box, radius, close: false);
        xml.WriteAttributeString("class", "newsletter-slot");
        xml.WriteAttributeString("id", $"newsletter-slot-{holes.Count}");
        xml.WriteAttributeString("data-image", name);
        xml.WriteAttributeString("fill", "none");
        xml.WriteEndElement();
    }

    /// <summary>
    /// The template's mask: white (shown) everywhere, black (cut out) at each slot. The slots were
    /// recorded in the column's coordinates, so they move by <paramref name="margin"/>, as the
    /// column does; a hero edge's clip path, also in the column, applies there too.
    /// </summary>
    public void WriteHoles(XmlWriter xml, double width, double height, double margin)
    {
        xml.WriteStartElement("mask", Namespace);
        xml.WriteAttributeString("id", HolesId);
        xml.WriteAttributeString("maskUnits", "userSpaceOnUse");
        xml.WriteAttributeString("x", "0");
        xml.WriteAttributeString("y", "0");
        xml.WriteAttributeString("width", Number(width));
        xml.WriteAttributeString("height", Number(height));
        Rect(xml, new RectD(0, 0, width, height), 0, close: false);
        xml.WriteAttributeString("fill", "#ffffff");
        xml.WriteEndElement();

        xml.WriteStartElement("g", Namespace);
        xml.WriteAttributeString("fill", "#000000");
        if (margin > 0)
        {
            xml.WriteAttributeString("transform", $"translate({Number(margin)} {Number(margin)})");
        }

        foreach (var (box, radius, clip) in holes)
        {
            Rect(xml, box, radius, close: false);
            if (clip is not null)
            {
                xml.WriteAttributeString("clip-path", $"url(#{clip})");
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    /// <summary>How a photo is tinted: <see cref="NewsletterPhotoTint.Gradient"/> or <see cref="NewsletterPhotoTint.Duotone"/>, and how strongly.</summary>
    internal sealed record Tint(NewsletterPhotoTint Mode, double Strength);

    internal const string GradientId = "newsletter-tint-gradient";
    internal const string DuotoneId = "newsletter-tint-duotone";

    /// <summary>
    /// Writes what tinted photos refer to: a diagonal gradient from the start colour to the end
    /// colour, or a duotone filter that turns the picture grey (by luminance) and maps its greys
    /// from the start colour to the end colour.
    /// </summary>
    internal static void WriteTint(XmlWriter xml, NewsletterTheme theme)
    {
        if (theme.PhotoTint == NewsletterPhotoTint.Gradient)
        {
            xml.WriteStartElement("linearGradient", Namespace);
            xml.WriteAttributeString("id", GradientId);
            xml.WriteAttributeString("x1", "0");
            xml.WriteAttributeString("y1", "0");
            xml.WriteAttributeString("x2", "1");
            xml.WriteAttributeString("y2", "1");
            foreach (var (offset, color) in new[] { ("0", theme.TintStartColor!), ("1", theme.TintEndColor!) })
            {
                xml.WriteStartElement("stop", Namespace);
                xml.WriteAttributeString("offset", offset);
                xml.WriteAttributeString("stop-color", color);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }
        else if (theme.PhotoTint == NewsletterPhotoTint.Duotone)
        {
            var dark = NewsletterTheme.Rgb(theme.TintStartColor!, nameof(theme.TintStartColor));
            var light = NewsletterTheme.Rgb(theme.TintEndColor!, nameof(theme.TintEndColor));

            // In sRGB, as the colours are given; the default, linearRGB, would shift them.
            xml.WriteStartElement("filter", Namespace);
            xml.WriteAttributeString("id", DuotoneId);
            xml.WriteAttributeString("color-interpolation-filters", "sRGB");
            xml.WriteStartElement("feColorMatrix", Namespace);
            xml.WriteAttributeString("type", "matrix");
            const string Luminance = "0.2126 0.7152 0.0722 0 0";
            xml.WriteAttributeString("values", $"{Luminance} {Luminance} {Luminance} 0 0 0 1 0");
            xml.WriteEndElement();
            xml.WriteStartElement("feComponentTransfer", Namespace);
            foreach (var (channel, from, to) in new[] { ("feFuncR", dark.R, light.R), ("feFuncG", dark.G, light.G), ("feFuncB", dark.B, light.B) })
            {
                xml.WriteStartElement(channel, Namespace);
                xml.WriteAttributeString("type", "table");
                xml.WriteAttributeString("tableValues", $"{Number(from)} {Number(to)}");
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
        }
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
