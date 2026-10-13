using System.Globalization;
using System.Text;
using SvgCreator.Fitting;

namespace SvgCreator.Svg;

/// <summary>Compact number formatting shared by everything that writes SVG.</summary>
internal static class SvgFormat
{
    public const string Namespace = "http://www.w3.org/2000/svg";

    public static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    // Up to four decimals, for filter parameters.
    public static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    // Tenths precision in the shortest form, for coordinates and lengths.
    public static string Format(double value)
    {
        var sb = new StringBuilder();
        AppendNumber(sb, ToTenths(value));
        return sb.ToString();
    }

    public static long ToTenths(double value) => (long)Math.Round(value * 10, MidpointRounding.AwayFromZero);

    public static (long X, long Y) Tenths(PointD p) => (ToTenths(p.X), ToTenths(p.Y));

    public static void AppendDelta(StringBuilder sb, (long X, long Y) point, (long X, long Y) origin)
    {
        AppendNumber(sb, point.X - origin.X);
        AppendNumber(sb, point.Y - origin.Y);
    }

    // Writes a value given in tenths in its shortest SVG form ("12", "1.5", ".5", "-.5"),
    // adding a space only where the previous number would otherwise run into this one.
    public static void AppendNumber(StringBuilder sb, long tenths)
    {
        var negative = tenths < 0;
        var magnitude = Math.Abs(tenths);
        long whole = magnitude / 10, fraction = magnitude % 10;

        if (negative)
        {
            sb.Append('-');
        }
        else if (sb.Length > 0 && char.IsAsciiDigit(sb[^1]))
        {
            sb.Append(' ');
        }

        if (whole != 0 || fraction == 0)
        {
            sb.Append(whole.ToString(CultureInfo.InvariantCulture));
        }

        if (fraction != 0)
        {
            sb.Append('.').Append((char)('0' + fraction));
        }
    }
}
