namespace SvgCreator.Composition;

/// <summary>
/// The icons a newsletter's footer can show, each one SVG path on a 24 × 24 grid, filled. The
/// brand marks are from Simple Icons (https://simpleicons.org), whose drawings are CC0: free to
/// copy with no attribution, so the library stays MIT. The licence covers the drawings, not the
/// brands; using a mark to link to one's own page there is the use brands allow.
/// </summary>
internal static class NewsletterIcons
{
    /// <summary>The grid the paths are drawn on.</summary>
    public const double Grid = 24;

    private static readonly (string Name, string Path)[] Icons =
    [
        ("github", "M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"),
    ];

    /// <summary>The icons' names.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Icons.Select(i => i.Name)];

    /// <summary>The icon's path, by name in any case; null if there's no such icon.</summary>
    public static string? Path(string name) =>
        Icons.FirstOrDefault(i => string.Equals(i.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).Path;
}
