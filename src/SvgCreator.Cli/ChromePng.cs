using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml;

namespace SvgCreator.Cli;

/// <summary>
/// Renders an SVG file to PNG with headless Chrome (or Chromium or Edge, the same engine), so the
/// PNG looks exactly as the SVG does in a browser. macOS's own renderer, which <c>sips</c> and
/// Quick Look use, ignores the position of each line of text (<c>&lt;tspan x y&gt;</c>) and piles
/// a block's lines on one spot, so it can't be used for these.
/// </summary>
internal static class ChromePng
{
    // Chrome's screenshots stop at the GPU's largest texture; past this, a long newsletter at 2×
    // would come out cut off or blank, so the scale is lowered to fit.
    private const int MaxPixels = 16384;

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Writes <paramref name="svg"/> as a PNG beside it (same name, <c>.png</c>), at
    /// <paramref name="scale"/> pixels per SVG pixel; <paramref name="transparent"/> keeps
    /// whatever the SVG doesn't paint see-through, as a template's holes. Returns the PNG's path
    /// and the scale used.
    /// </summary>
    public static async Task<(string Path, double Scale)> WriteAsync(FileInfo svg, double scale, bool transparent, CancellationToken cancellationToken)
    {
        var (width, height) = Size(svg);
        var fitted = Math.Min(scale, (double)MaxPixels / Math.Max(width, height));
        var png = Path.ChangeExtension(svg.FullName, ".png");
        var chrome = Find()
            ?? throw new IOException("--png needs Google Chrome, Chromium or Microsoft Edge to render the SVG. Install one, or set CHROME_PATH to its program.");

        // Its own profile folder, so it doesn't clash with a Chrome that's already open.
        var profile = Directory.CreateTempSubdirectory("svgcreator-chrome-");
        try
        {
            var start = new ProcessStartInfo(chrome)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[]
            {
                "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
                $"--user-data-dir={profile.FullName}",
                $"--window-size={width},{height}",
                $"--force-device-scale-factor={fitted.ToString(CultureInfo.InvariantCulture)}",
                $"--screenshot={png}",
            })
            {
                start.ArgumentList.Add(argument);
            }

            if (transparent)
            {
                start.ArgumentList.Add("--default-background-color=00000000");
            }

            start.ArgumentList.Add(new Uri(svg.FullName).AbsoluteUri);

            File.Delete(png);
            using var process = Process.Start(start) ?? throw new IOException($"Couldn't start {chrome}.");
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            _ = process.StandardOutput.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new IOException($"{chrome} didn't finish rendering {svg.Name} within {Timeout.TotalMinutes} minutes.");
            }

            if (process.ExitCode != 0 || !File.Exists(png))
            {
                throw new IOException($"{chrome} couldn't render {svg.Name} (exit code {process.ExitCode}). {(await errors).Trim()}");
            }

            return (png, fitted);
        }
        catch (Win32Exception ex)
        {
            throw new IOException($"Couldn't start {chrome}: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                profile.Delete(recursive: true);
            }
            catch (IOException)
            {
                // Chrome may still be letting go of its files; the temp folder is cleaned later.
            }
        }
    }

    // The canvas size, from the document's root: the window is made exactly this size.
    private static (int Width, int Height) Size(FileInfo svg)
    {
        using var reader = XmlReader.Create(svg.FullName);
        reader.MoveToContent();
        return (int.Parse(reader.GetAttribute("width")!, CultureInfo.InvariantCulture), int.Parse(reader.GetAttribute("height")!, CultureInfo.InvariantCulture));
    }

    /// <summary>CHROME_PATH if set, else the first Chrome, Chromium or Edge in its usual place or on the PATH.</summary>
    private static string? Find()
    {
        if (Environment.GetEnvironmentVariable("CHROME_PATH") is { Length: > 0 } configured)
        {
            return configured;
        }

        string[] known = OperatingSystem.IsMacOS()
            ?
            [
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium",
                "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            ]
            : OperatingSystem.IsWindows()
                ?
                [
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                ]
                : [];
        if (known.FirstOrDefault(File.Exists) is { } found)
        {
            return found;
        }

        // On the PATH: how Linux installs them.
        var names = new[] { "google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "microsoft-edge" };
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return folders.SelectMany(folder => names.Select(name => Path.Combine(folder, name))).FirstOrDefault(File.Exists);
    }
}
