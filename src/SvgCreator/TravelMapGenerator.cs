using SvgCreator.Svg;

namespace SvgCreator;

/// <summary>
/// Default <see cref="ITravelMapGenerator"/>: projects the stops and lays out the map
/// (<see cref="TravelMapWriter"/>). It takes no image or map tiles; everything is drawn.
/// </summary>
public sealed class TravelMapGenerator : ITravelMapGenerator
{
    /// <inheritdoc/>
    public string Create(IReadOnlyList<TravelStop> stops, TravelMapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        TravelMapOptions.Validate(stops);

        return TravelMapWriter.Write(stops, options);
    }
}
