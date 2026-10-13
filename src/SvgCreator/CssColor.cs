using System.Text.RegularExpressions;

namespace SvgCreator;

/// <summary>Validates the CSS colours options accept for ink.</summary>
internal static partial class CssColor
{
    public static void ThrowIfInvalid(string color, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(color, paramName);
        if (!Pattern().IsMatch(color))
        {
            throw new ArgumentException(
                $"'{color}' is not a valid colour. Use hex (#c0392b), a CSS name (navy) or rgb()/hsl().",
                paramName);
        }
    }

    // Hex (#rgb, #rgba, #rrggbb, #rrggbbaa), a CSS named colour, or rgb()/rgba()/hsl()/hsla().
    // Source-generated, so it is trimming- and Native AOT-safe.
    [GeneratedRegex(@"^(#([0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|[a-zA-Z]+|(rgba?|hsla?)\([0-9a-z.,%/+\- ]+\))$")]
    private static partial Regex Pattern();
}
