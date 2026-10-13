namespace SvgCreator;

/// <summary>
/// A postage stamp for <see cref="MailPieceGenerator"/>: the image for its centre and the stamp's
/// text, as <see cref="PostageStampGenerator"/> takes them.
/// </summary>
/// <param name="Image">Stream containing the encoded image (PNG, JPEG, WebP, GIF, BMP, ...).</param>
/// <param name="Options">The stamp's text, placement and ink colour.</param>
public sealed record PostageStampPiece(Stream Image, PostageStampOptions Options);

/// <summary>
/// A postmark for <see cref="MailPieceGenerator"/>: an image traced into line art, by default in
/// <see cref="OutputStyle.Postmark"/>, whose circular frame becomes the datestamp ring next to the
/// postage stamp and whose cancellation waves run across it.
/// </summary>
/// <param name="Image">Stream containing the encoded image, ideally a circular seal.</param>
public sealed record PostmarkPiece(Stream Image)
{
    /// <summary>Default postmark tracing: <see cref="OutputStyle.Postmark"/> in dark postmark ink.</summary>
    public static SvgConversionOptions DefaultOptions { get; } = new() { Style = OutputStyle.Postmark, FillColor = "#2b2b2b" };

    /// <summary>How the image is traced and inked. Default: <see cref="DefaultOptions"/>.</summary>
    public SvgConversionOptions Options { get; init; } = DefaultOptions;
}
