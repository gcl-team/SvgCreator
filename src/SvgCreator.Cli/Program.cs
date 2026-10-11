using System.CommandLine;
using SvgCreator;

var inputArgument = new Argument<FileInfo>("input")
{
    Description = "Image to convert (PNG, JPEG, WebP, GIF, BMP, ...).",
}.AcceptExistingOnly();

var outputOption = new Option<FileInfo?>("--output", "-o")
{
    Description = "Output SVG path. Defaults to the input path with an .svg extension.",
};

var modeOption = new Option<ExtractionMode>("--mode", "-m")
{
    Description = "Lines: find drawn lines by local contrast (coloured art). Threshold: everything darker than --threshold is ink (clean line art).",
    DefaultValueFactory = _ => SvgConversionOptions.Default.Mode,
};

var lineContrastOption = new Option<double>("--line-contrast", "-c")
{
    Description = "Lines mode: how much darker (0-255 levels) than its surroundings a pixel must be. Lower finds fainter lines.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.LineContrast,
};

var weakLineContrastOption = new Option<double>("--weak-line-contrast")
{
    Description = "Lines mode: weaker contrast that continues a line started by --line-contrast, keeping fading lines closed. Equal to --line-contrast disables.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.WeakLineContrast,
};

var lineSigmaOption = new Option<double>("--line-sigma")
{
    Description = "Lines mode: blur radius in pixels; raise for thick lines or high-resolution images.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.LineSigma,
};

var colorContrastOption = new Option<double>("--color-contrast")
{
    Description = "Lines mode: colour difference (CIE ΔE, ignoring brightness) between two regions that makes their boundary a line, e.g. pink hair on pale skin. 0 disables.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.ColorContrast,
};

var thresholdOption = new Option<int>("--threshold", "-t")
{
    Description = "Threshold mode: luminance (0-255) below which a pixel counts as ink.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.Threshold,
};

var minAreaOption = new Option<int>("--min-area")
{
    Description = "Discard ink specks with fewer pixels than this.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.MinComponentArea,
};

var toleranceOption = new Option<double>("--tolerance")
{
    Description = "Maximum curve-fitting error in pixels; larger gives fewer, smoother curves.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.CurveTolerance,
};

var cornerAngleOption = new Option<double>("--corner-angle")
{
    Description = "Turns sharper than this angle in degrees (180 = straight) stay crisp corners.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.CornerAngle,
};

var ellipsesOption = new Option<bool>("--ellipses")
{
    Description = "Write outlines that are circles or ellipses as native <circle>/<ellipse> shapes (use '--ellipses false' to keep curves).",
    DefaultValueFactory = _ => SvgConversionOptions.Default.DetectEllipses,
};

var styleOption = new Option<OutputStyle>("--style")
{
    Description = "Clean: crisp flat ink. Stamp: rubber-stamp impression (rough edges, worn ink) via a native SVG filter.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.Style,
};

var stampSeedOption = new Option<int>("--stamp-seed")
{
    Description = "Stamp style: texture seed; change it for a differently worn impression.",
    DefaultValueFactory = _ => SvgConversionOptions.Default.StampSeed,
};

var inkColorOption = new Option<string>("--ink-color")
{
    Description = "Ink colour: hex (#c0392b), a CSS name (navy) or rgb()/hsl().",
    DefaultValueFactory = _ => SvgConversionOptions.Default.FillColor,
};

var rootCommand = new RootCommand("Convert an image into a line-art SVG (black ink outlines, no colouring).")
{
    inputArgument,
    outputOption,
    modeOption,
    lineContrastOption,
    weakLineContrastOption,
    lineSigmaOption,
    colorContrastOption,
    thresholdOption,
    minAreaOption,
    toleranceOption,
    cornerAngleOption,
    ellipsesOption,
    styleOption,
    stampSeedOption,
    inkColorOption,
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var input = parseResult.GetRequiredValue(inputArgument);
    var output = parseResult.GetValue(outputOption) ?? new FileInfo(Path.ChangeExtension(input.FullName, ".svg"));
    var options = new SvgConversionOptions
    {
        Mode = parseResult.GetValue(modeOption),
        LineContrast = parseResult.GetValue(lineContrastOption),
        WeakLineContrast = parseResult.GetValue(weakLineContrastOption),
        LineSigma = parseResult.GetValue(lineSigmaOption),
        ColorContrast = parseResult.GetValue(colorContrastOption),
        Threshold = parseResult.GetValue(thresholdOption),
        MinComponentArea = parseResult.GetValue(minAreaOption),
        CurveTolerance = parseResult.GetValue(toleranceOption),
        CornerAngle = parseResult.GetValue(cornerAngleOption),
        DetectEllipses = parseResult.GetValue(ellipsesOption),
        Style = parseResult.GetValue(styleOption),
        StampSeed = parseResult.GetValue(stampSeedOption),
        FillColor = parseResult.GetRequiredValue(inkColorOption),
    };

    try
    {
        await using var stream = input.OpenRead();
        var svg = await new SvgConverter().ConvertAsync(stream, options, cancellationToken);
        await File.WriteAllTextAsync(output.FullName, svg, cancellationToken);
        Console.WriteLine($"Wrote {output.FullName}");
        return 0;
    }
    catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
    {
        await Console.Error.WriteLineAsync($"Error: {ex.Message}");
        return 1;
    }
});

return await rootCommand.Parse(args).InvokeAsync();
