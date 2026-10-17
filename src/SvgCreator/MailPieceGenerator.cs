using SvgCreator.Composition;
using SvgCreator.Imaging;

namespace SvgCreator;

/// <summary>
/// Default <see cref="IMailPieceGenerator"/>: makes each piece with its own generator and places
/// them on the envelope (<see cref="MailPieceComposer"/>).
/// </summary>
public sealed class MailPieceGenerator : IMailPieceGenerator
{
    /// <inheritdoc/>
    public string Create(EnvelopeOptions envelope, PostageStampPiece? postageStamp = null, PostmarkPiece? postmark = null)
    {
        Validate(envelope, postageStamp, postmark);

        return MailPieceComposer.Compose(
            envelope,
            postageStamp is null ? null : (ImageLoader.LoadEncoded(postageStamp.Image), postageStamp.Options),
            postmark is null ? null : (ImageLoader.Load(postmark.Image), postmark.Options),
            CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<string> CreateAsync(EnvelopeOptions envelope, PostageStampPiece? postageStamp = null, PostmarkPiece? postmark = null, CancellationToken cancellationToken = default)
    {
        Validate(envelope, postageStamp, postmark);

        var stamp = postageStamp is null
            ? ((EncodedImage, PostageStampOptions)?)null
            : (await ImageLoader.LoadEncodedAsync(postageStamp.Image, cancellationToken).ConfigureAwait(false), postageStamp.Options);
        var mark = postmark is null
            ? ((RgbImage, SvgConversionOptions)?)null
            : (await ImageLoader.LoadAsync(postmark.Image, cancellationToken).ConfigureAwait(false), postmark.Options);
        return MailPieceComposer.Compose(envelope, stamp, mark, cancellationToken);
    }

    // Every option is checked before any image is read, so a bad option fails fast.
    private static void Validate(EnvelopeOptions envelope, PostageStampPiece? postageStamp, PostmarkPiece? postmark)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        envelope.Validate();
        if (postageStamp is not null)
        {
            ArgumentNullException.ThrowIfNull(postageStamp.Image);
            ArgumentNullException.ThrowIfNull(postageStamp.Options);
            postageStamp.Options.Validate();
        }

        if (postmark is not null)
        {
            ArgumentNullException.ThrowIfNull(postmark.Image);
            ArgumentNullException.ThrowIfNull(postmark.Options);
            postmark.Options.Validate();
        }
    }
}
