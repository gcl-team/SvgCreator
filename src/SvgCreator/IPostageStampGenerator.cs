namespace SvgCreator;

/// <summary>
/// Makes postage stamp SVG documents with an image as the central illustration.
/// </summary>
public interface IPostageStampGenerator
{
    /// <summary>Makes a postage stamp around an encoded image (PNG, JPEG, BMP, GIF, WebP, ...).</summary>
    /// <param name="image">Stream containing the encoded image.</param>
    /// <param name="options">The stamp's text and placement.</param>
    /// <returns>The SVG document as a string.</returns>
    string Create(Stream image, PostageStampOptions options);

    /// <inheritdoc cref="Create(Stream, PostageStampOptions)"/>
    /// <param name="image">Stream containing the encoded image.</param>
    /// <param name="options">The stamp's text and placement.</param>
    /// <param name="cancellationToken">Token to cancel reading the image.</param>
    Task<string> CreateAsync(Stream image, PostageStampOptions options, CancellationToken cancellationToken = default);

    /// <summary>Makes a postage stamp around the image file at <paramref name="path"/>.</summary>
    /// <param name="path">Path to the image file.</param>
    /// <param name="options">The stamp's text and placement.</param>
    /// <returns>The SVG document as a string.</returns>
    string CreateFromFile(string path, PostageStampOptions options);
}
