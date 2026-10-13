using SvgCreator.Svg;

namespace SvgCreator;

/// <summary>
/// Default <see cref="IEnvelopeGenerator"/>: generates the envelope (<see cref="EnvelopeWriter"/>).
/// It takes no image; everything is drawn.
/// </summary>
public sealed class EnvelopeGenerator : IEnvelopeGenerator
{
    /// <inheritdoc/>
    public string Create(EnvelopeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return EnvelopeWriter.Write(options);
    }
}
