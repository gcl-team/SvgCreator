using System.CommandLine;
using SvgCreator;
using SvgCreator.Cli;

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
    Description = "Clean: crisp flat ink. Stamp: rubber-stamp impression (rough edges, worn ink) via a native SVG filter. Postmark: Stamp plus wavy cancellation lines over the circular frame (or a rectangular frame around the picture).",
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
    StampCommand(),
    EnvelopeCommand(),
    MailCommand(),
    MapCommand(),
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

    return await WriteSvgAsync(input, output, stream => new SvgConverter().ConvertAsync(stream, options, cancellationToken), cancellationToken);
});

// Response files are off: with them on, any argument starting with @ is read as a file of more
// arguments, so a fee or caption such as "@50" or "@handle" failed with "Response file not found".
return await rootCommand.Parse(args, new ParserConfiguration { ResponseFileTokenReplacer = null }).InvokeAsync();

// The stamp is always printed clean, so it takes none of the tracing or style options.
static Command StampCommand()
{
    var input = new Argument<FileInfo>("input")
    {
        Description = "Image for the centre of the stamp, embedded unchanged (PNG, JPEG, WebP, GIF, BMP, ...).",
    }.AcceptExistingOnly();

    var output = new Option<FileInfo?>("--output", "-o")
    {
        Description = "Output SVG path. Defaults to the input path with an .svg extension.",
    };

    var country = new Option<string>("--country")
    {
        Description = "Country name, in the largest type.",
        Required = true,
    };

    var fee = new Option<string?>("--fee") { Description = "The fee, such as 50c or $1.20." };
    var caption = new Option<string?>("--caption") { Description = "A small caption describing the picture." };
    var countryCorner = new Option<StampCorner?>("--country-corner") { Description = "Where the country name goes (default TopLeft)." };
    var feeCorner = new Option<StampCorner?>("--fee-corner") { Description = "Where the fee goes (default TopRight)." };
    var captionCorner = new Option<StampCorner?>("--caption-corner") { Description = "Where the caption goes (default BottomLeft)." };
    var inkColor = new Option<string?>("--ink-color") { Description = "Colour of the text and the picture frame: hex (#7a1f1f), a CSS name (navy) or rgb()/hsl()." };

    var command = new Command("stamp", "Make a postage stamp: the image on perforated paper, with the country, fee and caption.")
    {
        input, output, country, fee, caption, countryCorner, feeCorner, captionCorner, inkColor,
    };

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var file = parseResult.GetRequiredValue(input);
        var defaults = new PostageStampOptions { Country = parseResult.GetRequiredValue(country) };
        var options = defaults with
        {
            Fee = parseResult.GetValue(fee),
            Caption = parseResult.GetValue(caption),
            CountryCorner = parseResult.GetValue(countryCorner) ?? defaults.CountryCorner,
            FeeCorner = parseResult.GetValue(feeCorner) ?? defaults.FeeCorner,
            CaptionCorner = parseResult.GetValue(captionCorner) ?? defaults.CaptionCorner,
            InkColor = parseResult.GetValue(inkColor) ?? defaults.InkColor,
        };

        var target = parseResult.GetValue(output) ?? new FileInfo(Path.ChangeExtension(file.FullName, ".svg"));
        return await WriteSvgAsync(file, target, stream => new PostageStampGenerator().CreateAsync(stream, options, cancellationToken), cancellationToken);
    });

    return command;
}

// The envelope is drawn, not converted, so it takes no input image.
static Command EnvelopeCommand()
{
    var defaults = new EnvelopeOptions();
    var output = new Option<FileInfo>("--output", "-o") { Description = "Output SVG path.", Required = true };
    var width = new Option<int>("--width") { Description = "Envelope width in pixels.", DefaultValueFactory = _ => defaults.Width };
    var height = new Option<int>("--height") { Description = "Envelope height in pixels.", DefaultValueFactory = _ => defaults.Height };
    var style = new Option<EnvelopeStyle>("--style") { Description = "Flat: crisp paper. Crumpled: seeded creases, irregular edges and paper grain.", DefaultValueFactory = _ => defaults.Style };
    var seed = new Option<int>("--seed") { Description = "Crumpled style: crease seed; change it for a differently crumpled envelope.", DefaultValueFactory = _ => defaults.Seed };
    var paperColor = new Option<string>("--paper-color") { Description = "Paper colour: hex, a CSS name or rgb()/hsl().", DefaultValueFactory = _ => defaults.PaperColor };
    var strapColor = new Option<string>("--strap-color") { Description = "Colour of every other strap (default airmail red).", DefaultValueFactory = _ => defaults.StrapColor };
    var alternateColor = new Option<string>("--alternate-strap-color") { Description = "Colour of the straps in between (default airmail blue).", DefaultValueFactory = _ => defaults.AlternateStrapColor };

    var command = new Command("envelope", "Make an airmail envelope: paper with a red and blue strap border, flat or crumpled.")
    {
        output, width, height, style, seed, paperColor, strapColor, alternateColor,
    };

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var target = parseResult.GetRequiredValue(output);
        try
        {
            var svg = new EnvelopeGenerator().Create(new EnvelopeOptions
            {
                Width = parseResult.GetValue(width),
                Height = parseResult.GetValue(height),
                Style = parseResult.GetValue(style),
                Seed = parseResult.GetValue(seed),
                PaperColor = parseResult.GetRequiredValue(paperColor),
                StrapColor = parseResult.GetRequiredValue(strapColor),
                AlternateStrapColor = parseResult.GetRequiredValue(alternateColor),
            });
            await File.WriteAllTextAsync(target.FullName, svg, cancellationToken);
            Console.WriteLine($"Wrote {target.FullName}");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    });

    return command;
}

// An envelope with an optional postage stamp and postmark. Flags are prefixed per piece so the
// pieces' options can't collide.
static Command MailCommand()
{
    var envelopeDefaults = new EnvelopeOptions();
    var output = new Option<FileInfo>("--output", "-o") { Description = "Output SVG path.", Required = true };

    var width = new Option<int>("--width") { Description = "Envelope width in pixels.", DefaultValueFactory = _ => envelopeDefaults.Width };
    var height = new Option<int>("--height") { Description = "Envelope height in pixels.", DefaultValueFactory = _ => envelopeDefaults.Height };
    var envelopeStyle = new Option<EnvelopeStyle>("--envelope-style") { Description = "Flat or Crumpled.", DefaultValueFactory = _ => envelopeDefaults.Style };
    var envelopeSeed = new Option<int>("--envelope-seed") { Description = "Crumpled envelope: crease seed.", DefaultValueFactory = _ => envelopeDefaults.Seed };
    var paperColor = new Option<string>("--paper-color") { Description = "Envelope paper colour.", DefaultValueFactory = _ => envelopeDefaults.PaperColor };

    var stampImage = new Option<FileInfo>("--stamp-image") { Description = "Image for the postage stamp, embedded unchanged. Needs --country." }.AcceptExistingOnly();
    var country = new Option<string?>("--country") { Description = "Postage stamp: country name." };
    var fee = new Option<string?>("--fee") { Description = "Postage stamp: the fee, such as 50c or $1.20." };
    var caption = new Option<string?>("--caption") { Description = "Postage stamp: a small caption." };
    var stampInk = new Option<string?>("--stamp-ink-color") { Description = "Postage stamp: colour of its text and frame." };

    var postmarkImage = new Option<FileInfo>("--postmark-image") { Description = "Image traced into the postmark; its circle becomes the ring over the stamp's left edge." }.AcceptExistingOnly();
    var postmarkColor = new Option<string>("--postmark-color") { Description = "Postmark ink colour.", DefaultValueFactory = _ => PostmarkPiece.DefaultOptions.FillColor };
    var postmarkSeed = new Option<int>("--postmark-seed") { Description = "Postmark: worn-ink texture seed.", DefaultValueFactory = _ => PostmarkPiece.DefaultOptions.StampSeed };
    var postmarkMode = new Option<ExtractionMode>("--postmark-mode") { Description = "Postmark tracing: Lines (coloured art) or Threshold (clean black-on-white).", DefaultValueFactory = _ => PostmarkPiece.DefaultOptions.Mode };

    var command = new Command("mail", "Compose an envelope with an optional postage stamp (top right) and postmark (over the stamp).")
    {
        output, width, height, envelopeStyle, envelopeSeed, paperColor,
        stampImage, country, fee, caption, stampInk,
        postmarkImage, postmarkColor, postmarkSeed, postmarkMode,
    };

    command.Validators.Add(result =>
    {
        if ((result.GetValue(stampImage) is null) != (result.GetValue(country) is null))
        {
            result.AddError("--stamp-image and --country go together: the postage stamp needs both.");
        }
    });

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var target = parseResult.GetRequiredValue(output);
        var envelope = envelopeDefaults with
        {
            Width = parseResult.GetValue(width),
            Height = parseResult.GetValue(height),
            Style = parseResult.GetValue(envelopeStyle),
            Seed = parseResult.GetValue(envelopeSeed),
            PaperColor = parseResult.GetRequiredValue(paperColor),
        };

        try
        {
            await using var stampStream = parseResult.GetValue(stampImage)?.OpenRead();
            await using var postmarkStream = parseResult.GetValue(postmarkImage)?.OpenRead();

            PostageStampPiece? stamp = null;
            if (stampStream is not null)
            {
                var stampOptions = new PostageStampOptions
                {
                    Country = parseResult.GetRequiredValue(country)!,
                    Fee = parseResult.GetValue(fee),
                    Caption = parseResult.GetValue(caption),
                };
                stamp = new PostageStampPiece(stampStream, stampOptions with { InkColor = parseResult.GetValue(stampInk) ?? stampOptions.InkColor });
            }

            var postmark = postmarkStream is null ? null : new PostmarkPiece(postmarkStream)
            {
                Options = PostmarkPiece.DefaultOptions with
                {
                    FillColor = parseResult.GetRequiredValue(postmarkColor),
                    StampSeed = parseResult.GetValue(postmarkSeed),
                    Mode = parseResult.GetValue(postmarkMode),
                },
            };

            var svg = await new MailPieceGenerator().CreateAsync(envelope, stamp, postmark, cancellationToken);
            await File.WriteAllTextAsync(target.FullName, svg, cancellationToken);
            Console.WriteLine($"Wrote {target.FullName}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    });

    return command;
}

// A travel map from a JSON file of stops.
static Command MapCommand()
{
    var defaults = new TravelMapOptions();
    var input = new Argument<FileInfo>("stops")
    {
        Description = "JSON array of stops: sequence, name, latitude, longitude, and optionally time and description (an array of lines).",
    }.AcceptExistingOnly();
    var output = new Option<FileInfo>("--output", "-o") { Description = "Output SVG path.", Required = true };
    var width = new Option<int>("--width") { Description = "Map width in pixels.", DefaultValueFactory = _ => defaults.Width };
    var height = new Option<int>("--height") { Description = "Map height in pixels.", DefaultValueFactory = _ => defaults.Height };
    var background = new Option<string>("--background-color") { Description = "Background colour: hex, a CSS name or rgb()/hsl().", DefaultValueFactory = _ => defaults.BackgroundColor };
    var foreground = new Option<string>("--foreground-color") { Description = "Colour of the markers, arrows and text.", DefaultValueFactory = _ => defaults.ForegroundColor };
    var waypointColor = new Option<string>("--waypoint-color") { Description = "Colour of the waypoints' dots and names, setting them apart from the places.", DefaultValueFactory = _ => defaults.WaypointColor };
    var scale = new Option<double>("--scale") { Description = "Size of the text, markers and arrows, as a multiple of the default. They don't grow with the canvas, so a bigger canvas gives the stops more room.", DefaultValueFactory = _ => defaults.Scale };
    var route = new Option<TravelMapRoute>("--route") { Description = "Arrows: an arrow for each leg. Lines: a line without a head (the numbers give the direction). None: no route, markers only.", DefaultValueFactory = _ => defaults.Route };
    var breaks = new Option<TravelMapBreaks>("--breaks") { Description = "Auto: pull a far stop (an airport 40 km out) in towards the rest, with a dotted break labelled with the real distance. None: everything to scale.", DefaultValueFactory = _ => defaults.Breaks };
    var layout = new Option<TravelMapLayout>("--layout") { Description = "Labels: each stop's text beside its marker. Panel: names on the map, the full itinerary in a side panel. Auto: Labels when they fit, else Panel.", DefaultValueFactory = _ => defaults.Layout };

    var command = new Command("map", "Make a travel map: numbered stops at their real relative positions, joined by arrows in travel order, on a plain background.")
    {
        input, output, width, height, background, foreground, waypointColor, scale, route, breaks, layout,
    };

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        var target = parseResult.GetRequiredValue(output);
        try
        {
            var stops = await TravelStopsJson.ReadAsync(parseResult.GetRequiredValue(input), cancellationToken);
            var svg = new TravelMapGenerator().Create(stops, new TravelMapOptions
            {
                Width = parseResult.GetValue(width),
                Height = parseResult.GetValue(height),
                BackgroundColor = parseResult.GetRequiredValue(background),
                ForegroundColor = parseResult.GetRequiredValue(foreground),
                WaypointColor = parseResult.GetRequiredValue(waypointColor),
                Scale = parseResult.GetValue(scale),
                Route = parseResult.GetValue(route),
                Breaks = parseResult.GetValue(breaks),
                Layout = parseResult.GetValue(layout),
            });
            await File.WriteAllTextAsync(target.FullName, svg, cancellationToken);
            Console.WriteLine($"Wrote {target.FullName}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    });

    return command;
}

static async Task<int> WriteSvgAsync(FileInfo input, FileInfo output, Func<Stream, Task<string>> create, CancellationToken cancellationToken)
{
    try
    {
        await using var stream = input.OpenRead();
        var svg = await create(stream);
        await File.WriteAllTextAsync(output.FullName, svg, cancellationToken);
        Console.WriteLine($"Wrote {output.FullName}");
        return 0;
    }
    catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
    {
        await Console.Error.WriteLineAsync($"Error: {ex.Message}");
        return 1;
    }
}
