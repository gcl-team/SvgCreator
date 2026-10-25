using SvgCreator.Mapping;

namespace SvgCreator;

/// <summary>Where a travel map puts each stop's time and description.</summary>
public enum TravelMapLayout
{
    /// <summary>
    /// <see cref="Labels"/> when every label fits beside its marker without covering arrows,
    /// markers or other labels; <see cref="Panel"/> when it doesn't.
    /// </summary>
    Auto,

    /// <summary>All of a stop's text beside its marker, on the map.</summary>
    Labels,

    /// <summary>
    /// Markers named on the map; every stop's number, name, time and description listed in travel
    /// order in a panel beside it (on the right of a landscape canvas, at the bottom of a portrait one).
    /// </summary>
    Panel,
}

/// <summary>How a travel map shows the route from one place to the next.</summary>
public enum TravelMapRoute
{
    /// <summary>An arrow for each leg.</summary>
    Arrows,

    /// <summary>A line for each leg, without a head: the markers' numbers give the direction.</summary>
    Lines,

    /// <summary>No route: markers and their numbers alone, which leaves the most room for labels.</summary>
    None,
}

/// <summary>Whether a travel map pulls far stops in, breaking the legs to them.</summary>
public enum TravelMapBreaks
{
    /// <summary>
    /// A stop much further from the trip's middle than the rest (an airport 40 km out on a city
    /// day) is pulled in, keeping its direction; the legs to it are dotted in the middle and
    /// labelled with their real distance. A trip with no far stop is drawn to scale.
    /// </summary>
    Auto,

    /// <summary>Everything to scale, however far.</summary>
    None,
}

/// <summary>
/// Options for <see cref="TravelMapGenerator"/>: a travel map with no map on it. The stops sit where
/// they would on a web map (north up), numbered markers joined by straight arrows in travel order,
/// on a plain background; everything else a map would show is left out.
/// </summary>
public sealed record TravelMapOptions
{
    /// <summary>Width of the SVG canvas in pixels. Default: 1024.</summary>
    public int Width { get; init; } = 1024;

    /// <summary>Height of the SVG canvas in pixels. Default: 560.</summary>
    public int Height { get; init; } = 560;

    /// <summary>Background colour, as a CSS colour. Default: deep blue <c>#1446a0</c>.</summary>
    public string BackgroundColor { get; init; } = "#1446a0";

    /// <summary>Colour of the markers, arrows and text, as a CSS colour. Default: white <c>#ffffff</c>.</summary>
    public string ForegroundColor { get; init; } = "#ffffff";

    /// <summary>
    /// Colour of the waypoints (<see cref="TravelStop.Waypoint"/>): their dots and names on the map,
    /// and their steps in the panel, as a CSS colour. It sets them apart from the places; the route
    /// through them stays in <see cref="ForegroundColor"/>. Default: amber <c>#ffd166</c>.
    /// </summary>
    public string WaypointColor { get; init; } = "#ffd166";

    /// <summary>
    /// Size of the text, markers and arrows, as a multiple of the default. They keep their pixel
    /// size whatever the canvas size, so a bigger canvas gives the stops more room; raise this to
    /// make them bigger too (2 for a map drawn at twice the size). Default: 1.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <summary>How the route between places is drawn. Default: <see cref="TravelMapRoute.Arrows"/>.</summary>
    public TravelMapRoute Route { get; init; } = TravelMapRoute.Arrows;

    /// <summary>Whether far stops are pulled in, with a break in the legs to them. Default: <see cref="TravelMapBreaks.Auto"/>.</summary>
    public TravelMapBreaks Breaks { get; init; } = TravelMapBreaks.Auto;

    /// <summary>Where each stop's time and description go. Default: <see cref="TravelMapLayout.Auto"/>.</summary>
    public TravelMapLayout Layout { get; init; } = TravelMapLayout.Auto;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Height);
        if (!double.IsFinite(Scale) || Scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Scale), Scale, "Scale must be a positive number.");
        }

        if (!Enum.IsDefined(Route))
        {
            throw new ArgumentOutOfRangeException(nameof(Route), Route, "Unknown travel map route style.");
        }

        if (!Enum.IsDefined(Breaks))
        {
            throw new ArgumentOutOfRangeException(nameof(Breaks), Breaks, "Unknown travel map breaks setting.");
        }

        if (!Enum.IsDefined(Layout))
        {
            throw new ArgumentOutOfRangeException(nameof(Layout), Layout, "Unknown travel map layout.");
        }

        CssColor.ThrowIfInvalid(BackgroundColor, nameof(BackgroundColor));
        CssColor.ThrowIfInvalid(ForegroundColor, nameof(ForegroundColor));
        CssColor.ThrowIfInvalid(WaypointColor, nameof(WaypointColor));
    }

    internal static void Validate(IReadOnlyList<TravelStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("A travel map needs at least one stop.", nameof(stops));
        }

        if (stops.All(s => s is { Waypoint: true }))
        {
            throw new ArgumentException("A travel map needs at least one stop that isn't a waypoint: only places are numbered.", nameof(stops));
        }

        var sequences = new HashSet<int>();
        foreach (var stop in stops)
        {
            ArgumentNullException.ThrowIfNull(stop, nameof(stops));
            if (!sequences.Add(stop.Sequence))
            {
                throw new ArgumentException($"Two stops have sequence number {stop.Sequence}; each stop needs its own.", nameof(stops));
            }

            if (string.IsNullOrWhiteSpace(stop.Name))
            {
                throw new ArgumentException($"Stop {stop.Sequence} needs a name.", nameof(stops));
            }

            // Mercator stretches to infinity at the poles; web maps stop where the world is square.
            if (!double.IsFinite(stop.Latitude) || Math.Abs(stop.Latitude) > WebMercator.MaxLatitude)
            {
                throw new ArgumentException($"Stop {stop.Sequence} ({stop.Name}): latitude {stop.Latitude} is outside ±{WebMercator.MaxLatitude}°.", nameof(stops));
            }

            if (!double.IsFinite(stop.Longitude) || Math.Abs(stop.Longitude) > 180)
            {
                throw new ArgumentException($"Stop {stop.Sequence} ({stop.Name}): longitude {stop.Longitude} is outside ±180°.", nameof(stops));
            }

            if (stop.Description.Any(line => line is null))
            {
                throw new ArgumentException($"Stop {stop.Sequence} ({stop.Name}): description lines can't be null.", nameof(stops));
            }
        }
    }
}
