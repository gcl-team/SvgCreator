namespace SvgCreator;

/// <summary>
/// Makes travel map SVG documents: numbered stops at their real relative positions, joined by
/// arrows in travel order, with each stop's name, time and description beside it.
/// </summary>
public interface ITravelMapGenerator
{
    /// <summary>Makes a travel map.</summary>
    /// <param name="stops">The stops, in any order; arrows follow their <see cref="TravelStop.Sequence"/>.</param>
    /// <param name="options">The map's size and colours.</param>
    /// <returns>The SVG document as a string.</returns>
    string Create(IReadOnlyList<TravelStop> stops, TravelMapOptions options);
}
