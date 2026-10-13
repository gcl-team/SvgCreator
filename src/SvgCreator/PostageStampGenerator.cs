using SvgCreator.Imaging;
using SvgCreator.Svg;

namespace SvgCreator;

/// <summary>
/// Default <see cref="IPostageStampGenerator"/>: embeds the image untraced and lays out the stamp
/// around it (<see cref="PostageStampWriter"/>).
/// </summary>
public sealed class PostageStampGenerator : IPostageStampGenerator
{
    /// <inheritdoc/>
    public string Create(Stream image, PostageStampOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return PostageStampWriter.Write(ImageLoader.LoadEncoded(image), options);
    }

    /// <inheritdoc/>
    public async Task<string> CreateAsync(Stream image, PostageStampOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var encoded = await ImageLoader.LoadEncodedAsync(image, cancellationToken).ConfigureAwait(false);
        return PostageStampWriter.Write(encoded, options);
    }

    /// <inheritdoc/>
    public string CreateFromFile(string path, PostageStampOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Create(stream, options);
    }
}
