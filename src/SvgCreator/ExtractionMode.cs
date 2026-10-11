namespace SvgCreator;

/// <summary>How ink is separated from the background before tracing.</summary>
public enum ExtractionMode
{
    /// <summary>
    /// Finds drawn lines and region boundaries by local contrast (Difference of Gaussians).
    /// Best for coloured artwork such as anime illustrations.
    /// </summary>
    Lines,

    /// <summary>
    /// Every pixel darker than <see cref="SvgConversionOptions.Threshold"/> is ink.
    /// Best for clean black-on-white line art or silhouettes.
    /// </summary>
    Threshold,
}
