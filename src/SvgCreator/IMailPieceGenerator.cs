namespace SvgCreator;

/// <summary>
/// Composes a mail piece: an envelope with an optional postage stamp and an optional postmark on it.
/// </summary>
public interface IMailPieceGenerator
{
    /// <summary>Composes the pieces into one SVG document the size of the envelope.</summary>
    /// <param name="envelope">The envelope, as <see cref="EnvelopeGenerator"/> takes it.</param>
    /// <param name="postageStamp">The postage stamp in the top-right corner; none when <see langword="null"/>.</param>
    /// <param name="postmark">The postmark over the stamp's left edge; none when <see langword="null"/>.</param>
    /// <returns>The SVG document as a string.</returns>
    string Create(EnvelopeOptions envelope, PostageStampPiece? postageStamp = null, PostmarkPiece? postmark = null);

    /// <inheritdoc cref="Create(EnvelopeOptions, PostageStampPiece?, PostmarkPiece?)"/>
    /// <param name="envelope">The envelope, as <see cref="EnvelopeGenerator"/> takes it.</param>
    /// <param name="postageStamp">The postage stamp in the top-right corner; none when <see langword="null"/>.</param>
    /// <param name="postmark">The postmark over the stamp's left edge; none when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to cancel reading the images and tracing.</param>
    Task<string> CreateAsync(EnvelopeOptions envelope, PostageStampPiece? postageStamp = null, PostmarkPiece? postmark = null, CancellationToken cancellationToken = default);
}
