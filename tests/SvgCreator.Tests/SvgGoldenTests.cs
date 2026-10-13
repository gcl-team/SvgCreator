using System.Runtime.CompilerServices;
using SvgCreator.Fitting;

namespace SvgCreator.Tests;

/// <summary>
/// Pins the exact SVG text for each output style, so refactoring the writer cannot change the
/// output unnoticed. After an intentional output change, regenerate the files with
/// <c>UPDATE_GOLDEN=1 dotnet test</c> and review their diff.
/// </summary>
public class SvgGoldenTests
{
    // A ring frame with a square and a curved blob inside: exercises the native circle, path data
    // and (for Postmark) the circular frame.
    private static readonly CurveContour[] Seal =
    [
        new(1, false, [], new Ellipse(new(60, 50), 41, 41, 0)),
        new(1, true, [], new Ellipse(new(60, 50), 38, 38, 0)),
        Polygon(2, false, new(40, 35), new(60, 35), new(60, 55), new(40, 55)),
        Polygon(2, true, new(45, 40), new(45, 50), new(55, 50), new(55, 40)),
        new(3, false,
        [
            new CubicBezier(new(65, 60), new(66.2, 64.7), new(75, 65), new(75, 60.5)),
            new CubicBezier(new(75, 60.5), new(75, 57), new(65.5, 57), new(65, 60)),
        ]),
    ];

    // No circle: Postmark falls back to a rectangular frame and widens the canvas.
    private static readonly CurveContour[] Square = [Polygon(1, false, new(10, 10), new(70, 10), new(70, 70), new(10, 70))];

    [Fact]
    public void Clean() => AssertGolden("clean.svg", TestSvg.Write(120, 100, Seal, "#000"));

    [Fact]
    public void Stamp() => AssertGolden("stamp.svg", TestSvg.Write(120, 100, Seal, "navy", OutputStyle.Stamp, 7, 1.5));

    [Fact]
    public void PostmarkWithCircle() => AssertGolden("postmark-circle.svg", TestSvg.Write(120, 100, Seal, "#c0392b", OutputStyle.Postmark, 3, 1));

    [Fact]
    public void PostmarkWithoutCircle() => AssertGolden("postmark-rect.svg", TestSvg.Write(80, 80, Square, "#c0392b", OutputStyle.Postmark, 3, 1));

    internal static void AssertGolden(string name, string actual, [CallerFilePath] string testFile = "")
    {
        // XmlWriter uses the platform newline; git may check the files out with either.
        actual = actual.ReplaceLineEndings("\n") + "\n";
        var path = Path.Combine(Path.GetDirectoryName(testFile)!, "Golden", name);
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
        }

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    private static CurveContour Polygon(int label, bool isHole, params PointD[] points) =>
        new(label, isHole, [.. points.Select((p, i) => CubicBezier.Line(p, points[(i + 1) % points.Length]))]);
}
