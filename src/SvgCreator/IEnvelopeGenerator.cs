namespace SvgCreator;

/// <summary>
/// Makes airmail envelope SVG documents.
/// </summary>
public interface IEnvelopeGenerator
{
    /// <summary>Makes an envelope.</summary>
    /// <param name="options">The envelope's size, style and colours.</param>
    /// <returns>The SVG document as a string.</returns>
    string Create(EnvelopeOptions options);
}
