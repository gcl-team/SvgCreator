using System.Text;
using SvgCreator.Svg;

namespace SvgCreator.Composition;

/// <summary>
/// Newsletter text with accented spans: <c>[[like this]]</c> is printed in the accent colour.
/// Wrapping works on the plain text (<see cref="TextWrap"/>), then each line is matched back to
/// it character by character to find where its accented runs fall.
/// </summary>
internal static class AccentText
{
    private const string Open = "[[";
    private const string Close = "]]";

    /// <summary>A stretch of a line, accented or not.</summary>
    internal readonly record struct Run(string Text, bool Accent);

    /// <summary>
    /// The text without its markers, and for each of its characters whether it's accented.
    /// Throws if a marker is left open, closed without opening, or opened twice.
    /// </summary>
    public static (string Plain, bool[] Accent) Parse(string text)
    {
        var plain = new StringBuilder(text.Length);
        var accent = new List<bool>(text.Length);
        var inside = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (string.CompareOrdinal(text, i, Open, 0, Open.Length) == 0)
            {
                inside = !inside ? true : throw Unbalanced(text, "[[ opened inside another [[");
                i++;
            }
            else if (string.CompareOrdinal(text, i, Close, 0, Close.Length) == 0)
            {
                inside = inside ? false : throw Unbalanced(text, "]] without a [[ before it");
                i++;
            }
            else
            {
                plain.Append(text[i]);
                accent.Add(inside);
            }
        }

        if (inside)
        {
            throw Unbalanced(text, "[[ without a ]] after it");
        }

        return (plain.ToString(), [.. accent]);
    }

    /// <summary>The plain text, markers removed.</summary>
    public static string Plain(string text) => Parse(text).Plain;

    /// <summary>The text broken into lines no wider than <paramref name="maxWidth"/>, each as its runs.</summary>
    public static List<IReadOnlyList<Run>> Wrap(string text, double size, double maxWidth, FontMetrics? metrics = null)
    {
        var (plain, accent) = Parse(text);
        var lines = new List<IReadOnlyList<Run>>();
        var position = 0;
        foreach (var line in TextWrap.Wrap(plain, size, maxWidth, metrics))
        {
            // Wrapping drops the spaces at breaks and squeezes runs of them into one.
            while (position < plain.Length && char.IsWhiteSpace(plain[position]))
            {
                position++;
            }

            var runs = new List<Run>();
            var current = new StringBuilder();
            var currentAccent = false;
            foreach (var c in line)
            {
                var flag = position < plain.Length && accent[position];
                if (c == ' ' && position < plain.Length && char.IsWhiteSpace(plain[position]))
                {
                    while (position < plain.Length && char.IsWhiteSpace(plain[position]))
                    {
                        position++;
                    }
                }
                else
                {
                    position++;
                }

                if (current.Length > 0 && flag != currentAccent)
                {
                    runs.Add(new Run(current.ToString(), currentAccent));
                    current.Clear();
                }

                currentAccent = flag;
                current.Append(c);
            }

            if (current.Length > 0)
            {
                runs.Add(new Run(current.ToString(), currentAccent));
            }

            lines.Add(runs);
        }

        return lines;
    }

    private static ArgumentException Unbalanced(string text, string problem) =>
        new($"Unbalanced accent markers ({problem}) in \"{text}\". Mark accented text [[like this]].", nameof(text));
}
