namespace SvgCreator;

/// <summary>
/// Makes newsletter SVG documents: a header, hero and intro, a list of blocks (ribbons, text,
/// picture cards, buttons, stats, callouts and travel maps), and a footer, in one tall column.
/// </summary>
public interface INewsletterGenerator
{
    /// <summary>Makes a newsletter.</summary>
    /// <param name="newsletter">The content.</param>
    /// <param name="options">The column's width and the colours.</param>
    /// <param name="openImage">
    /// Opens a picture the newsletter names, such as by resolving a path against the newsletter
    /// file's folder. Each name is opened once, however often it's used; the stream is disposed after.
    /// </param>
    /// <returns>The SVG document as a string.</returns>
    string Create(Newsletter newsletter, NewsletterOptions options, Func<string, Stream> openImage);

    /// <summary>Makes a newsletter, opening and reading its pictures asynchronously.</summary>
    /// <param name="newsletter">The content.</param>
    /// <param name="options">The column's width and the colours.</param>
    /// <param name="openImage">Opens a picture the newsletter names; see <see cref="Create"/>.</param>
    /// <param name="cancellationToken">Cancels reading the pictures and the layout.</param>
    /// <returns>The SVG document as a string.</returns>
    Task<string> CreateAsync(Newsletter newsletter, NewsletterOptions options, Func<string, CancellationToken, Task<Stream>> openImage, CancellationToken cancellationToken = default);
}
