using System.Globalization;
using System.Text;

namespace SvgCreator.Svg;

/// <summary>
/// Estimates how wide native <c>&lt;text&gt;</c> renders in a font stack. The width is unknown until
/// the viewer picks a font, so each printable ASCII character's advance (em) is the widest measured
/// across the stack's fonts, regular and bold (and for the serif stack italic), rounded up.
/// Per-character values, not averages: a row of W and M runs half as wide again as a row of average
/// capitals, and layout has to hold for every row. Estimates err wide: overestimating only leaves a
/// little extra space, while underestimating lets text overrun.
/// </summary>
internal sealed class FontMetrics
{
    /// <summary>
    /// Georgia and Times New Roman (which Chrome's generic serif resolves to): the postage stamp's print.
    /// </summary>
    public static FontMetrics Serif { get; } = new("Georgia, 'Times New Roman', serif",
    [
        0.26, 0.38, 0.56, 0.71, 0.65, 1.00, 0.84, 0.28, // space ! " # $ % & '
        0.45, 0.45, 0.50, 0.71, 0.33, 0.38, 0.33, 0.48, // ( ) * + , - . /
        0.71, 0.50, 0.63, 0.63, 0.65, 0.60, 0.65, 0.56, // 0 1 2 3 4 5 6 7
        0.68, 0.65, 0.39, 0.39, 0.71, 0.71, 0.71, 0.55, // 8 9 : ; < = > ?
        0.97, 0.76, 0.76, 0.73, 0.84, 0.73, 0.68, 0.81, // @ A B C D E F G
        0.92, 0.45, 0.60, 0.82, 0.69, 1.03, 0.84, 0.82, // H I J K L M N O
        0.71, 0.82, 0.80, 0.65, 0.69, 0.84, 0.77, 1.13, // P Q R S T U V W
        0.81, 0.74, 0.69, 0.45, 0.48, 0.45, 0.71, 0.71, // X Y Z [ \\ ] ^ _
        0.50, 0.60, 0.65, 0.54, 0.67, 0.58, 0.40, 0.58, // ` a b c d e f g
        0.68, 0.36, 0.35, 0.64, 0.35, 1.02, 0.69, 0.64, // h i j k l m n o
        0.66, 0.65, 0.52, 0.52, 0.40, 0.68, 0.57, 0.87, // p q r s t u v w
        0.59, 0.57, 0.53, 0.50, 0.39, 0.50, 0.71, // x y z { | } ~
    ]);

    /// <summary>
    /// Helvetica, Helvetica Neue and Arial (generic sans-serif on macOS and Windows), measured with
    /// Skia. Linux's generic sans-serif is usually DejaVu Sans, a wide face; Verdana, which shares
    /// its wide humanist proportions and runs a little wider still, is measured in its place.
    /// </summary>
    public static FontMetrics Sans { get; } = new("Helvetica, Arial, sans-serif",
    [
        0.36, 0.41, 0.59, 0.87, 0.72, 1.28, 0.87, 0.34, // space ! " # $ % & '
        0.55, 0.55, 0.72, 0.87, 0.37, 0.48, 0.37, 0.69, // ( ) * + , - . /
        0.72, 0.72, 0.72, 0.72, 0.72, 0.72, 0.72, 0.72, // 0 1 2 3 4 5 6 7
        0.72, 0.72, 0.46, 0.46, 0.87, 0.87, 0.87, 0.62, // 8 9 : ; < = > ?
        1.02, 0.78, 0.77, 0.75, 0.84, 0.69, 0.66, 0.82, // @ A B C D E F G
        0.84, 0.55, 0.56, 0.78, 0.64, 0.95, 0.85, 0.86, // H I J K L M N O
        0.74, 0.86, 0.79, 0.72, 0.69, 0.82, 0.77, 1.13, // P Q R S T U V W
        0.77, 0.74, 0.70, 0.55, 0.69, 0.55, 0.87, 0.72, // X Y Z [ \\ ] ^ _
        0.72, 0.67, 0.70, 0.59, 0.70, 0.67, 0.43, 0.70, // ` a b c d e f g
        0.72, 0.35, 0.41, 0.68, 0.35, 1.06, 0.72, 0.69, // h i j k l m n o
        0.70, 0.70, 0.50, 0.60, 0.46, 0.72, 0.65, 0.98, // p q r s t u v w
        0.67, 0.66, 0.60, 0.72, 0.55, 0.72, 0.87, // x y z { | } ~
    ]);

    // Base letter of each U+00C0–U+024F character (é → e, Ö → O), so accented Latin takes its base
    // letter's advance; '*' where there is none (æ, ø, ß, ł), which takes the widest. From Unicode
    // canonical decomposition, as a table because the CLI runs with invariant globalization.
    private const string LatinBaseLetters =
        "AAAAAA*CEEEEIIII*NOOOOO**UUUUY**aaaaaa*ceeeeiiii" +
        "*nooooo**uuuuy*yAaAaAaCcCcCcCcDd**EeEeEeEeEeGgGg" +
        "GgGgHh**IiIiIiIiI***JjKk*LlLlLl****NnNnNn***OoOo" +
        "Oo**RrRrRrSsSsSsSsTtTt**UuUuUuUuUuUuWwYyYZzZzZz*" +
        "********************************Oo*************U" +
        "u****************************AaIiOoUuUuUuUuUu*Aa" +
        "Aa****GgKkOoOo**j***Gg**NnAa****AaAaEeEeIiIiOoOo" +
        "RrRrUuUuSsTt**Hh******AaEeOoOoOoOoYy************" +
        "****************";

    private readonly double[] asciiAdvance;

    private FontMetrics(string family, double[] asciiAdvance)
    {
        Family = family;
        this.asciiAdvance = asciiAdvance;

        // Anything not measured gets the widest measured glyph: 1 em is not an upper bound.
        WidestAdvance = asciiAdvance.Max();
    }

    /// <summary>The CSS <c>font-family</c> the advances were measured for.</summary>
    public string Family { get; }

    /// <summary>The widest measured advance (em), given to every character not in the table.</summary>
    public double WidestAdvance { get; }

    /// <summary>The estimated width of <paramref name="text"/> at font size <paramref name="size"/>.</summary>
    public double Width(string text, double size)
    {
        var em = 0.0;
        foreach (var rune in text.EnumerateRunes())
        {
            em += rune.Value switch
            {
                >= 0x20 and < 0x7F => asciiAdvance[rune.Value - 0x20],
                >= 0xC0 and <= 0x24F when LatinBaseLetters[rune.Value - 0xC0] is not '*' and var b => asciiAdvance[b - 0x20],
                _ when Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark => 0,
                < 0x20 or 0x7F => 0, // control characters
                _ => WidestAdvance,
            };
        }

        return em * size;
    }
}
