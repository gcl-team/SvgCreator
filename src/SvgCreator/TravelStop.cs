namespace SvgCreator;

/// <summary>
/// One stop on a travel map: a place pinned at its coordinates, numbered in travel order, with the
/// text printed beside its marker.
/// </summary>
/// <param name="Sequence">Position in the travel order. Unique across the stops; arrows run from each stop to the next higher one.</param>
/// <param name="Name">The place's name, printed in bold.</param>
/// <param name="Latitude">Latitude in degrees, north positive.</param>
/// <param name="Longitude">Longitude in degrees, east positive.</param>
public sealed record TravelStop(int Sequence, string Name, double Latitude, double Longitude)
{
    /// <summary>Lines of description printed under the name and time. Default: none; null means none too.</summary>
    /// <remarks>
    /// Null is taken as none because JSON readers set it when the property is left out: with a
    /// constructor to call, System.Text.Json assigns every init property, missing ones as null.
    /// </remarks>
    public IReadOnlyList<string> Description { get; init => field = value ?? []; } = [];

    /// <summary>When the stop is visited, printed exactly as given (e.g. <c>Day 2 · 2 pm</c>). Default: none.</summary>
    public string? Time { get; init; }

    /// <summary>
    /// A stop on the way rather than a place to go: a station to change trains at, a bus stop, a
    /// pier. On the map it's a small unnumbered dot with its name, the route running through it;
    /// in the itinerary panel, its directions are a step under the place before it. Only places
    /// are numbered, consecutively. Default: false, a place.
    /// </summary>
    public bool Waypoint { get; init; }
}
