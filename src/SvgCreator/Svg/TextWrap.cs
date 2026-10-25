using System.Text;

namespace SvgCreator.Svg;

/// <summary>
/// Breaks text into lines for native <c>&lt;text&gt;</c>, which never wraps by itself: at spaces,
/// and between CJK characters, which need none. Widths are estimated with a
/// <see cref="FontMetrics"/> (<see cref="FontMetrics.Sans"/> unless another is given), so they err wide.
/// </summary>
internal static class TextWrap
{
    // Breaks text into lines no wider than maxWidth: at spaces, and between CJK characters, which
    // need none (see Tokens). Spaces come first: a run of CJK characters, such as a name's
    // Chinese half, moves to the next line whole when it fits there, and only one wider than a
    // whole line is broken inside. A piece wider than that on its own keeps its own line rather
    // than being split.
    internal static List<string> Wrap(string text, double size, double maxWidth, FontMetrics? metrics = null)
    {
        metrics ??= FontMetrics.Sans;
        var lines = new List<string>();
        var current = new StringBuilder();
        bool Fits(string line) => metrics.Width(line, size) <= maxWidth;

        void Add(string token, bool spaced)
        {
            var candidate = current.Length == 0 ? token : spaced ? $"{current} {token}" : $"{current}{token}";
            if (current.Length > 0 && !Fits(candidate))
            {
                lines.Add(current.ToString());
                current.Clear().Append(token);
            }
            else
            {
                current.Clear().Append(candidate);
            }
        }

        // Words: the pieces between spaces, each a run of tokens that join without one.
        var words = new List<(List<string> Tokens, bool Spaced)>();
        foreach (var (token, spaced) in Tokens(text))
        {
            if (spaced || words.Count == 0)
            {
                words.Add(([token], spaced));
            }
            else
            {
                words[^1].Tokens.Add(token);
            }
        }

        foreach (var (tokens, spaced) in words)
        {
            var word = string.Concat(tokens);
            var candidate = current.Length == 0 ? word : spaced ? $"{current} {word}" : $"{current}{word}";
            if (Fits(candidate) || current.Length == 0 && tokens.Count == 1)
            {
                current.Clear().Append(candidate);
            }
            else if (Fits(word))
            {
                lines.Add(current.ToString());
                current.Clear().Append(word);
            }
            else
            {
                // Too wide for a line of its own: start it on a fresh line all the same, so a
                // name's two halves stay apart, and break it between its CJK characters.
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                foreach (var token in tokens)
                {
                    Add(token, spaced: false);
                }
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    /// <summary>
    /// The pieces a line may break between, each with whether a space came before it. Words end at
    /// spaces; a CJK character is a piece on its own, as Chinese and Japanese break between any two
    /// characters. Brackets and punctuation stay with what they belong to, so no line starts with
    /// a closing mark or ends with an opening one: 汉庭酒店(上海…店) breaks as 店 / (上 / … / 店).
    /// </summary>
    internal static IEnumerable<(string Token, bool Spaced)> Tokens(string text)
    {
        var current = new StringBuilder();
        var (spaced, pendingSpace, lastCjk, lastOpener) = (false, false, false, false);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (current.Length > 0)
                {
                    yield return (current.ToString(), spaced);
                    current.Clear();
                }

                pendingSpace = true;
                continue;
            }

            var cjk = IsCjk(rune);
            if (current.Length > 0 && (cjk || lastCjk) && !lastOpener && !ClosingMarks.Contains(rune.ToString(), StringComparison.Ordinal))
            {
                yield return (current.ToString(), spaced);
                current.Clear();
            }

            if (current.Length == 0)
            {
                (spaced, pendingSpace) = (pendingSpace, false);
            }

            current.Append(rune.ToString());
            (lastCjk, lastOpener) = (cjk, OpeningMarks.Contains(rune.ToString(), StringComparison.Ordinal));
        }

        if (current.Length > 0)
        {
            yield return (current.ToString(), spaced);
        }
    }

    private const string OpeningMarks = "([{（「『【〔〈《“‘";
    private const string ClosingMarks = ")]}）」』】〕〉》、。，．：；！？”’,.:;!?·・";

    // Han (with radicals, symbols and punctuation), kana, Hangul, and full-width forms.
    internal static bool IsCjk(Rune rune) => rune.Value is
        (>= 0x2E80 and <= 0x9FFF) or (>= 0xAC00 and <= 0xD7AF) or (>= 0xF900 and <= 0xFAFF) or
        (>= 0xFF00 and <= 0xFFEF) or (>= 0x20000 and <= 0x3FFFF);
}
