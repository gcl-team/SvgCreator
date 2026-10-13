using System.Xml;
using static SvgCreator.Svg.SvgFormat;

namespace SvgCreator.Svg;

/// <summary>
/// A rubber-stamp impression made only of native SVG filter primitives, applied at render time:
/// fractal noise displaces the ink edges (rough print), and two noise layers turned into alpha
/// masks remove blotches and pinholes of ink (worn print). The id carries the seed because ids
/// are global when several SVGs are inlined into one HTML page.
/// </summary>
/// <param name="Seed">Seed for the texture; the same seed always gives the same impression.</param>
/// <param name="Scale">Line thickness relative to <c>LineSigma</c> = 1; the texture scales with it.</param>
internal sealed record StampFilter(int Seed, double Scale) : IInkFilter
{
    // Tuned for LineSigma = 1 and scaled with the line thickness (not the canvas size: Lines-mode
    // strokes are a few pixels wide at any resolution, so a canvas-scaled texture frays them on
    // large images). Frequencies divide by the scale, distances multiply by it.
    private const double EdgeNoiseFrequency = 0.8;  // fine noise that wobbles the ink edges
    private const double EdgeDisplacement = 2.5;    // how far (px) edges are pushed by that noise
    private const double WearNoiseFrequency = 0.06; // coarse blotches where ink didn't transfer
    private const double SpeckNoiseFrequency = 0.9; // small pinholes in the ink

    // Noise → alpha as alpha = Bias − Gain·R. Fractal noise centres on R ≈ 0.5, so ink stays fully
    // opaque below R = (Bias − 1)/Gain and vanishes above R = Bias/Gain: blotches where R > 0.63–0.70,
    // pinholes where R > 0.73–0.78. Tuned so the print looks worn but stays readable.
    private const double WearGain = 14;
    private const double WearBias = 9.8;
    private const double SpeckGain = 20;
    private const double SpeckBias = 15.6;

    // The definition depends on the seed, the texture scale and the canvas it covers.
    public string Id(int canvasWidth, int canvasHeight) =>
        $"svgcreator-stamp-{Integer(Seed)}-{Number(Scale)}-{Integer(canvasWidth)}x{Integer(canvasHeight)}";

    public void WriteDefinition(XmlWriter xml, int canvasWidth, int canvasHeight)
    {
        xml.WriteStartElement("defs", Namespace);
        xml.WriteStartElement("filter", Namespace);
        xml.WriteAttributeString("id", Id(canvasWidth, canvasHeight));
        xml.WriteAttributeString("filterUnits", "userSpaceOnUse");
        xml.WriteAttributeString("x", "0");
        xml.WriteAttributeString("y", "0");
        xml.WriteAttributeString("width", Integer(canvasWidth));
        xml.WriteAttributeString("height", Integer(canvasHeight));
        xml.WriteAttributeString("color-interpolation-filters", "sRGB");

        // Rough edges.
        WriteTurbulence(xml, EdgeNoiseFrequency / Scale, octaves: 2, Seed, "edgeNoise");
        xml.WriteStartElement("feDisplacementMap", Namespace);
        xml.WriteAttributeString("in", "SourceGraphic");
        xml.WriteAttributeString("in2", "edgeNoise");
        xml.WriteAttributeString("scale", Number(EdgeDisplacement * Scale));
        xml.WriteAttributeString("xChannelSelector", "R");
        xml.WriteAttributeString("yChannelSelector", "G");
        xml.WriteAttributeString("result", "rough");
        xml.WriteEndElement();

        // Worn ink: coarse blotches and fine pinholes, each as an alpha mask.
        WriteTurbulence(xml, WearNoiseFrequency / Scale, octaves: 3, Seed + 1, "wearNoise");
        WriteNoiseToAlpha(xml, "wearNoise", WearGain, WearBias, "wear");
        WriteTurbulence(xml, SpeckNoiseFrequency / Scale, octaves: 1, Seed + 2, "speckNoise");
        WriteNoiseToAlpha(xml, "speckNoise", SpeckGain, SpeckBias, "speck");

        WriteComposite(xml, "wear", "speck", "mask");
        WriteComposite(xml, "rough", "mask", null);

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void WriteTurbulence(XmlWriter xml, double frequency, int octaves, int seed, string result)
    {
        xml.WriteStartElement("feTurbulence", Namespace);
        xml.WriteAttributeString("type", "fractalNoise");
        xml.WriteAttributeString("baseFrequency", Number(frequency));
        xml.WriteAttributeString("numOctaves", Integer(octaves));
        xml.WriteAttributeString("seed", Integer(seed));
        xml.WriteAttributeString("result", result);
        xml.WriteEndElement();
    }

    // alpha = bias − gain·R; colour channels are irrelevant for a mask.
    private static void WriteNoiseToAlpha(XmlWriter xml, string input, double gain, double bias, string result)
    {
        xml.WriteStartElement("feColorMatrix", Namespace);
        xml.WriteAttributeString("in", input);
        xml.WriteAttributeString("type", "matrix");
        xml.WriteAttributeString("values", $"0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 {Number(-gain)} 0 0 0 {Number(bias)}");
        xml.WriteAttributeString("result", result);
        xml.WriteEndElement();
    }

    // Keeps `input` only where `mask` is opaque (alphas multiply).
    private static void WriteComposite(XmlWriter xml, string input, string mask, string? result)
    {
        xml.WriteStartElement("feComposite", Namespace);
        xml.WriteAttributeString("in", input);
        xml.WriteAttributeString("in2", mask);
        xml.WriteAttributeString("operator", "in");
        if (result is not null)
        {
            xml.WriteAttributeString("result", result);
        }

        xml.WriteEndElement();
    }
}
