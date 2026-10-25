using SvgCreator.Fitting;

namespace SvgCreator.Mapping;

/// <summary>
/// Pulls far-flung stops in towards the rest of the trip, so one airport 40 km out doesn't squeeze
/// a city's worth of stops into a corner. The legs to a pulled-in stop are drawn with a break: a
/// dotted middle, labelled with the real distance.
/// </summary>
/// <remarks>
/// <para>
/// Far is relative to the trip, not a fixed distance: a stop is far when it lies more than
/// <see cref="FarFactor"/> times the trip's median distance from its middle (the coordinate-wise
/// median, which a few far stops can't drag). A trip whose stops are all far apart, such as a road
/// trip, has a large median and breaks nothing.
/// </para>
/// <para>
/// A far stop keeps its direction from the middle and moves in to just outside the rest of the
/// trip. Far stops near each other (the airport and an airport hotel) move together, as a group, so
/// their arrangement and the legs between them stay true.
/// </para>
/// </remarks>
internal static class ScaleBreaks
{
    /// <summary>How many median distances from the middle make a stop far.</summary>
    public const double FarFactor = 3;

    /// <summary>Where a far group lands: this many times the near stops' reach from the middle.</summary>
    public const double LandingFactor = 1.6;

    /// <summary>
    /// And at least this many times the reach from every near stop, so the broken leg is long
    /// enough to show its dots and its distance.
    /// </summary>
    public const double ClearanceFactor = 0.8;

    /// <summary>
    /// The positions with far stops pulled in, and each stop's group: 0 for the near stops, 1 and up
    /// for each group of far stops that moved together. A leg between two groups is broken.
    /// </summary>
    public static (PointD[] Positions, int[] Groups) Apply(IReadOnlyList<PointD> projected)
    {
        var positions = projected.ToArray();
        var groups = new int[positions.Length];
        if (positions.Length < 3)
        {
            return (positions, groups);
        }

        var middle = new PointD(Median(positions.Select(p => p.X)), Median(positions.Select(p => p.Y)));
        var distances = positions.Select(p => (p - middle).Length).ToArray();
        var median = Median(distances);
        if (median <= 0)
        {
            return (positions, groups);
        }

        var far = Enumerable.Range(0, positions.Length).Where(i => distances[i] > FarFactor * median).ToList();
        if (far.Count == 0)
        {
            return (positions, groups);
        }

        // The near stops' reach: how far from the middle the trip proper extends.
        var reach = Enumerable.Range(0, positions.Length).Where(i => !far.Contains(i)).Max(i => distances[i]);

        // Far stops within that reach of each other move as one group (single linkage: an airport
        // and its hotel, or a string of stops on a day out).
        var group = 0;
        foreach (var start in far)
        {
            if (groups[start] != 0)
            {
                continue;
            }

            groups[start] = ++group;
            var queue = new Queue<int>([start]);
            while (queue.TryDequeue(out var i))
            {
                foreach (var j in far.Where(j => groups[j] == 0 && (positions[j] - positions[i]).Length <= reach))
                {
                    groups[j] = group;
                    queue.Enqueue(j);
                }
            }
        }

        // Each group moves in along the line from the middle to its centre, until that centre sits
        // just outside the trip proper, and clear of every near stop: a near stop in the same
        // direction (Longyang Road, on the way to Pudong Airport) would otherwise leave the broken
        // leg too short to read. Only ever inwards.
        var near = Enumerable.Range(0, positions.Length).Where(i => groups[i] == 0).Select(i => projected[i]).ToList();
        for (var g = 1; g <= group; g++)
        {
            var members = Enumerable.Range(0, positions.Length).Where(i => groups[i] == g).ToList();
            var centre = members.Aggregate(default(PointD), (sum, i) => sum + projected[i]) * (1.0 / members.Count);
            var offset = centre - middle;
            var direction = offset.Normalize();
            var landing = Math.Min(offset.Length, reach * LandingFactor);
            while (landing < offset.Length && near.Any(p => (middle + (direction * landing) - p).Length < reach * ClearanceFactor))
            {
                landing = Math.Min(offset.Length, landing + (reach * 0.05));
            }

            var shift = direction * (landing - offset.Length);
            foreach (var i in members)
            {
                positions[i] = projected[i] + shift;
            }
        }

        return (positions, groups);
    }

    /// <summary>Great-circle distance in kilometres (haversine, on a sphere of the Earth's mean radius).</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0088;
        var (phi1, phi2) = (double.DegreesToRadians(lat1), double.DegreesToRadians(lat2));
        var dPhi = phi2 - phi1;
        var dLambda = double.DegreesToRadians(lon2 - lon1);
        var a = (Math.Sin(dPhi / 2) * Math.Sin(dPhi / 2)) + (Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(dLambda / 2) * Math.Sin(dLambda / 2));
        return 2 * earthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>A distance for the break's label: "≈ 40 km", "≈ 6.5 km", "≈ 800 m".</summary>
    public static string FormatDistance(double km) => km switch
    {
        >= 10 => $"≈ {Math.Round(km).ToString(System.Globalization.CultureInfo.InvariantCulture)} km",
        >= 1 => $"≈ {Math.Round(km, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)} km",
        _ => $"≈ {(Math.Round(km * 10) * 100).ToString(System.Globalization.CultureInfo.InvariantCulture)} m",
    };

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
