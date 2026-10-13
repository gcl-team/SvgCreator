using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace SvgCreator;

/// <summary>
/// Validates the CSS colours options accept for ink. Hex and names are checked fully: a name must
/// be one of the CSS named colours, because a renderer silently drops a fill or stroke it doesn't
/// recognize. <c>rgb()</c>/<c>hsl()</c> are checked for their shape only.
/// </summary>
internal static partial class CssColor
{
    // The 148 CSS Color Module 4 named colours, plus the two colour keywords.
    private static readonly FrozenSet<string> Names = new[]
    {
        "aliceblue", "antiquewhite", "aqua", "aquamarine", "azure", "beige", "bisque", "black",
        "blanchedalmond", "blue", "blueviolet", "brown", "burlywood", "cadetblue", "chartreuse",
        "chocolate", "coral", "cornflowerblue", "cornsilk", "crimson", "cyan", "darkblue", "darkcyan",
        "darkgoldenrod", "darkgray", "darkgreen", "darkgrey", "darkkhaki", "darkmagenta",
        "darkolivegreen", "darkorange", "darkorchid", "darkred", "darksalmon", "darkseagreen",
        "darkslateblue", "darkslategray", "darkslategrey", "darkturquoise", "darkviolet", "deeppink",
        "deepskyblue", "dimgray", "dimgrey", "dodgerblue", "firebrick", "floralwhite", "forestgreen",
        "fuchsia", "gainsboro", "ghostwhite", "gold", "goldenrod", "gray", "green", "greenyellow",
        "grey", "honeydew", "hotpink", "indianred", "indigo", "ivory", "khaki", "lavender",
        "lavenderblush", "lawngreen", "lemonchiffon", "lightblue", "lightcoral", "lightcyan",
        "lightgoldenrodyellow", "lightgray", "lightgreen", "lightgrey", "lightpink", "lightsalmon",
        "lightseagreen", "lightskyblue", "lightslategray", "lightslategrey", "lightsteelblue",
        "lightyellow", "lime", "limegreen", "linen", "magenta", "maroon", "mediumaquamarine",
        "mediumblue", "mediumorchid", "mediumpurple", "mediumseagreen", "mediumslateblue",
        "mediumspringgreen", "mediumturquoise", "mediumvioletred", "midnightblue", "mintcream",
        "mistyrose", "moccasin", "navajowhite", "navy", "oldlace", "olive", "olivedrab", "orange",
        "orangered", "orchid", "palegoldenrod", "palegreen", "paleturquoise", "palevioletred",
        "papayawhip", "peachpuff", "peru", "pink", "plum", "powderblue", "purple", "rebeccapurple",
        "red", "rosybrown", "royalblue", "saddlebrown", "salmon", "sandybrown", "seagreen",
        "seashell", "sienna", "silver", "skyblue", "slateblue", "slategray", "slategrey", "snow",
        "springgreen", "steelblue", "tan", "teal", "thistle", "tomato", "turquoise", "violet",
        "wheat", "white", "whitesmoke", "yellow", "yellowgreen",
        "transparent", "currentcolor",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static void ThrowIfInvalid(string color, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(color, paramName);
        if (!Pattern().IsMatch(color) && !Names.Contains(color))
        {
            throw new ArgumentException(
                $"'{color}' is not a valid colour. Use hex (#c0392b), a CSS colour name (navy) or rgb()/hsl().",
                paramName);
        }
    }

    // Hex (#rgb, #rgba, #rrggbb, #rrggbbaa), or rgb()/rgba()/hsl()/hsla(). Names are checked
    // against the list above. Source-generated, so it is trimming- and Native AOT-safe.
    [GeneratedRegex(@"^(#([0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|(rgba?|hsla?)\([0-9a-z.,%/+\- ]+\))$")]
    private static partial Regex Pattern();
}
