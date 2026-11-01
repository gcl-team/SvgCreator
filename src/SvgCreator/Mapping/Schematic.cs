using SvgCreator.Fitting;

namespace SvgCreator.Mapping;

/// <summary>
/// Lays a trip out as a metro map: directions are kept, distances dropped. The places are joined
/// by the legs between them, ridden (<see cref="TravelStop.Line"/>) or walked, each pair once. The
/// stations and the rides between them, with any walk that links two of them, are the network:
/// each of its segments takes the nearest of eight compass directions to its real one, and the
/// positions are solved so that every ride is about one length. The places walked to from a single
/// station hang off it on a straight branch, in visit order, pointing the way they really lie when
/// that way is free. Positions come out in the projection's coordinates (y down), to be fitted.
/// </summary>
internal static class Schematic
{
    private const double RideLength = 1;
    private const double WalkLength = 0.6;         // a walk in the network: between stations, or through places that link two
    private const double BranchStep = 0.55;        // between the places on a walking branch
    private const double LengthWeight = 0.3;       // how hard each segment's length is held to its standard, against its direction
    private const double PinnedLengthWeight = 100; // for a segment the solve would shrink too far: its length wins
    private const double MinLengthFraction = 0.5;  // the shortest a segment may be solved to, of its standard
    private const double SplitTolerance = 0.3;     // how far off a ride a station on its line may lie and still be on the way, of the ride's length
    private const int Rounds = 20;

    /// <summary>A segment between two places, by index: ridden on <paramref name="Line"/>, or walked when it's null.</summary>
    internal sealed record Edge(int From, int To, string? Line);

    /// <summary>
    /// The laid-out map: each place's position, the segments to draw (walks and rides, each pair
    /// once), and which places are interchanges: stations on two lines or more.
    /// </summary>
    internal sealed record Map(IReadOnlyList<PointD> Positions, IReadOnlyList<Edge> Edges, IReadOnlyList<bool> Interchanges);

    // The eight directions, east first and counterclockwise, as screen vectors (y down).
    private static readonly PointD[] Directions =
        [.. Enumerable.Range(0, 8).Select(k => new PointD(Math.Cos(k * Math.PI / 4), -Math.Sin(k * Math.PI / 4)))];

    /// <param name="ordered">The stops, sorted by sequence.</param>
    /// <param name="placeOf">Each stop's place, as an index into <paramref name="geo"/>; places are indexed in order of first visit.</param>
    /// <param name="geo">Each place's projected position.</param>
    public static Map Build(IReadOnlyList<TravelStop> ordered, IReadOnlyList<int> placeOf, IReadOnlyList<PointD> geo)
    {
        var count = geo.Count;
        var walks = new List<(int, int)>();
        var rides = new List<Edge>();
        for (var i = 0; i + 1 < ordered.Count; i++)
        {
            var (a, b) = (placeOf[i], placeOf[i + 1]);
            if (a == b)
            {
                continue;
            }

            var line = string.IsNullOrWhiteSpace(ordered[i].Line) ? null : ordered[i].Line!.Trim();
            var pair = (Math.Min(a, b), Math.Max(a, b));
            if (line is null)
            {
                if (!walks.Contains(pair))
                {
                    walks.Add(pair);
                }
            }
            else if (!rides.Any(r => r.Line == line && (Math.Min(r.From, r.To), Math.Max(r.From, r.To)) == pair))
            {
                rides.Add(new Edge(a, b, line));
            }
        }

        // Shortest rides first, so a long ride along stretches already drawn (the ride home past
        // the morning's stations) is drawn by them, not as a second line across the map.
        var kept = new List<Edge>();
        foreach (var ride in rides.OrderBy(r => (geo[r.To] - geo[r.From]).Length))
        {
            AddRide(kept, ride.From, ride.To, ride.Line!, geo);
        }

        var transit = new bool[count];
        var lines = new HashSet<string>[count];
        for (var n = 0; n < count; n++)
        {
            lines[n] = [];
        }

        foreach (var ride in kept)
        {
            transit[ride.From] = transit[ride.To] = true;
            lines[ride.From].Add(ride.Line!);
            lines[ride.To].Add(ride.Line!);
        }

        var interchanges = lines.Select(l => l.Count > 1).ToArray();

        // The network, and the walking branches off it. With no rides at all, everything is network.
        var inNetwork = transit.Any(t => t) ? (bool[])transit.Clone() : Enumerable.Repeat(true, count).ToArray();
        var branches = new List<(int Anchor, List<int> Places)>();
        if (transit.Any(t => t))
        {
            // Places reached only on foot, grouped by the walks between them. A group walked to
            // from one station is a branch of it; one that links two stations is part of the network.
            var group = Enumerable.Range(0, count).ToArray();
            int Find(int n) => group[n] == n ? n : group[n] = Find(group[n]);
            foreach (var (a, b) in walks.Where(w => !transit[w.Item1] && !transit[w.Item2]))
            {
                group[Find(a)] = Find(b);
            }

            foreach (var places in Enumerable.Range(0, count).Where(n => !transit[n]).GroupBy(Find))
            {
                var members = places.Order().ToList();
                var anchors = walks
                    .Where(w => members.Contains(w.Item1) != members.Contains(w.Item2))
                    .Select(w => members.Contains(w.Item1) ? w.Item2 : w.Item1)
                    .Distinct().ToList();
                if (anchors.Count == 1)
                {
                    branches.Add((anchors[0], members));
                }
                else
                {
                    members.ForEach(m => inNetwork[m] = true);
                }
            }
        }

        var network = kept.Concat(walks.Where(w => inNetwork[w.Item1] && inNetwork[w.Item2]).Select(w => new Edge(w.Item1, w.Item2, null))).ToList();
        var positions = LayOutNetwork(network, inNetwork, geo);
        var edges = new List<Edge>(network);
        PlaceBranches(branches, network, inNetwork, geo, positions, edges);
        return new Map(positions, edges, interchanges);
    }

    // Adds a ride unless the line already joins its ends. A ride past a station already on its
    // line goes through it, so it's split there.
    private static void AddRide(List<Edge> kept, int a, int b, string line, IReadOnlyList<PointD> geo)
    {
        if (Connected(kept, a, b, line))
        {
            return;
        }

        var along = geo[b] - geo[a];
        var length = along.Length;
        int? via = null;
        var nearest = double.MaxValue;
        foreach (var c in kept.Where(e => e.Line == line).SelectMany(e => new[] { e.From, e.To }).Distinct())
        {
            if (c == a || c == b || length < 1e-12)
            {
                continue;
            }

            var offset = geo[c] - geo[a];
            var t = offset.Dot(along) / (length * length);
            var off = Math.Abs((along.X * offset.Y) - (along.Y * offset.X)) / length;
            if (t > 0.1 && t < 0.9 && off < SplitTolerance * length && off < nearest)
            {
                (via, nearest) = (c, off);
            }
        }

        if (via is { } v)
        {
            AddRide(kept, a, v, line, geo);
            AddRide(kept, v, b, line, geo);
            return;
        }

        kept.Add(new Edge(a, b, line));
    }

    private static bool Connected(List<Edge> kept, int a, int b, string line)
    {
        var seen = new HashSet<int> { a };
        var queue = new Queue<int>([a]);
        while (queue.TryDequeue(out var n))
        {
            if (n == b)
            {
                return true;
            }

            foreach (var e in kept.Where(e => e.Line == line && (e.From == n || e.To == n)))
            {
                var next = e.From == n ? e.To : e.From;
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    // The direction (0–7) nearest a vector's, and how far off it each direction is, in eighths of a turn.
    private static double Angle(PointD v) => Math.Atan2(-v.Y, v.X);

    private static double Error(int direction, double angle)
    {
        var off = Math.Abs(Math.IEEERemainder(angle - (direction * Math.PI / 4), 2 * Math.PI));
        return off / (Math.PI / 4);
    }

    private static int Nearest(double angle) => ((int)Math.Round(angle / (Math.PI / 4)) % 8 + 8) % 8;

    /// <summary>
    /// The network's positions: each segment takes the direction nearest its real one, two
    /// segments leaving a place the same way are told apart, and the positions are solved by
    /// least squares, each segment along its direction at about its standard length. A tree
    /// solves exactly; a loop closes by stretching some segments and shrinking others, and a
    /// segment the loop would shrink too far keeps its length and gives up its angle instead.
    /// </summary>
    private static PointD[] LayOutNetwork(List<Edge> network, bool[] inNetwork, IReadOnlyList<PointD> geo)
    {
        var positions = geo.ToArray();
        var nodes = Enumerable.Range(0, geo.Count).Where(n => inNetwork[n]).ToList();
        if (network.Count == 0)
        {
            return positions;
        }

        var angles = network.Select(e => Angle(geo[e.To] - geo[e.From])).ToArray();
        var direction = angles.Select(Nearest).ToArray();
        int At(int e, int node) => network[e].From == node ? direction[e] : (direction[e] + 4) % 8;

        for (var round = 0; round < Rounds; round++)
        {
            var changed = false;
            foreach (var node in nodes)
            {
                var incident = Enumerable.Range(0, network.Count).Where(e => network[e].From == node || network[e].To == node).ToList();
                foreach (var clash in incident.GroupBy(e => At(e, node)).Where(g => g.Count() > 1))
                {
                    // The segment closest to its real direction keeps it; the others move to the
                    // nearest direction free at both their ends.
                    foreach (var e in clash.OrderBy(e => Error(direction[e], angles[e])).Skip(1))
                    {
                        var (from, to) = (network[e].From, network[e].To);
                        bool Free(int end, int d) => Enumerable.Range(0, network.Count).All(o => o == e || (network[o].From != end && network[o].To != end) || At(o, end) != d);
                        var free = Enumerable.Range(0, 8)
                            .Where(d => Free(from, d) && Free(to, (d + 4) % 8))
                            .OrderBy(d => Error(d, angles[e]))
                            .Cast<int?>().FirstOrDefault();
                        if (free is { } d)
                        {
                            direction[e] = d;
                            changed = true;
                        }
                    }
                }
            }

            if (!changed)
            {
                break;
            }
        }

        var index = new Dictionary<int, int>();
        foreach (var n in nodes)
        {
            index[n] = index.Count;
        }

        var standard = network.Select(e => e.Line is null ? WalkLength : RideLength).ToArray();
        var weight = Enumerable.Repeat(LengthWeight, network.Count).ToArray();
        double[] solution = [];
        for (var round = 0; round < 4; round++)
        {
            solution = Solve(network, index, direction, standard, weight);
            var shrunk = Enumerable.Range(0, network.Count).Where(e => solution[(2 * index.Count) + e] < standard[e] * MinLengthFraction && weight[e] < PinnedLengthWeight).ToList();
            if (shrunk.Count == 0)
            {
                break;
            }

            shrunk.ForEach(e => weight[e] = PinnedLengthWeight);
        }

        foreach (var (n, i) in index)
        {
            positions[n] = new PointD(solution[2 * i], solution[(2 * i) + 1]);
        }

        return positions;
    }

    // Least squares over the network's positions and its segments' lengths: each segment's far
    // end at its near end plus its length along its direction, each length near its standard by
    // its weight, and the first place at the origin. Solved by the normal equations.
    private static double[] Solve(List<Edge> network, Dictionary<int, int> index, int[] direction, double[] standard, double[] weight)
    {
        var size = (2 * index.Count) + network.Count;
        var normal = new double[size, size];
        var rhs = new double[size];
        void Row(IReadOnlyList<(int Column, double Value)> terms, double target, double w)
        {
            foreach (var (i, a) in terms)
            {
                foreach (var (j, b) in terms)
                {
                    normal[i, j] += w * a * b;
                }

                rhs[i] += w * a * target;
            }
        }

        for (var e = 0; e < network.Count; e++)
        {
            var (from, to) = (index[network[e].From], index[network[e].To]);
            var d = Directions[direction[e]];
            var length = (2 * index.Count) + e;
            Row([(2 * to, 1), (2 * from, -1), (length, -d.X)], 0, 1);
            Row([((2 * to) + 1, 1), ((2 * from) + 1, -1), (length, -d.Y)], 0, 1);
            Row([(length, 1)], standard[e], weight[e]);
        }

        Row([(0, 1)], 0, 1);
        Row([(1, 1)], 0, 1);
        return GaussianElimination(normal, rhs);
    }

    private static double[] GaussianElimination(double[,] a, double[] b)
    {
        var n = b.Length;
        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var row = col + 1; row < n; row++)
            {
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                {
                    pivot = row;
                }
            }

            if (Math.Abs(a[pivot, col]) < 1e-12)
            {
                continue;
            }

            if (pivot != col)
            {
                for (var k = 0; k < n; k++)
                {
                    (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                }

                (b[col], b[pivot]) = (b[pivot], b[col]);
            }

            for (var row = 0; row < n; row++)
            {
                if (row == col || a[row, col] == 0)
                {
                    continue;
                }

                var factor = a[row, col] / a[col, col];
                for (var k = col; k < n; k++)
                {
                    a[row, k] -= factor * a[col, k];
                }

                b[row] -= factor * b[col];
            }
        }

        return [.. Enumerable.Range(0, n).Select(i => Math.Abs(a[i, i]) < 1e-12 ? 0 : b[i] / a[i, i])];
    }

    /// <summary>
    /// Hangs each walking branch off its station, its places a step apart in a straight line, the
    /// largest branches first. Each takes the direction nearest the way its places really lie
    /// from the station, among those the station has free, unless that runs into something
    /// already drawn: a place too close, or a segment crossed.
    /// </summary>
    private static void PlaceBranches(
        List<(int Anchor, List<int> Places)> branches, List<Edge> network, bool[] inNetwork,
        IReadOnlyList<PointD> geo, PointD[] positions, List<Edge> edges)
    {
        var placed = Enumerable.Range(0, geo.Count).Where(n => inNetwork[n]).ToList();
        var segments = network.Select(e => (positions[e.From], positions[e.To])).ToList();
        var used = new Dictionary<int, HashSet<int>>();
        HashSet<int> UsedAt(int n) => used.TryGetValue(n, out var set) ? set : used[n] = [];
        foreach (var e in network)
        {
            var d = Nearest(Angle(positions[e.To] - positions[e.From]));
            UsedAt(e.From).Add(d);
            UsedAt(e.To).Add((d + 4) % 8);
        }

        foreach (var (anchor, places) in branches.OrderByDescending(b => b.Places.Count))
        {
            var mean = places.Aggregate(default(PointD), (sum, p) => sum + geo[p]) * (1.0 / places.Count);
            var real = Angle(mean - geo[anchor]);
            var origin = positions[anchor];

            double Cost(int d)
            {
                var cost = Error(d, real) + (UsedAt(anchor).Contains(d) ? 100 : 0);
                var previous = origin;
                for (var step = 1; step <= places.Count; step++)
                {
                    var point = origin + (Directions[d] * (BranchStep * step));
                    cost += 10 * placed.Count(q => q != anchor && (positions[q] - point).Length < 0.7 * BranchStep);
                    cost += 10 * segments.Count(s => Crosses(previous, point, s.Item1, s.Item2));
                    previous = point;
                }

                return cost;
            }

            var best = Enumerable.Range(0, 8).OrderBy(Cost).First();
            UsedAt(anchor).Add(best);
            var from = anchor;
            for (var step = 1; step <= places.Count; step++)
            {
                var place = places[step - 1];
                positions[place] = origin + (Directions[best] * (BranchStep * step));
                edges.Add(new Edge(from, place, null));
                segments.Add((positions[from], positions[place]));
                placed.Add(place);
                from = place;
            }
        }
    }

    // Whether two segments cross properly: touching at a shared end doesn't count.
    private static bool Crosses(PointD a, PointD b, PointD c, PointD d)
    {
        static double Side(PointD p, PointD q, PointD r) => ((q.X - p.X) * (r.Y - p.Y)) - ((q.Y - p.Y) * (r.X - p.X));
        const double epsilon = 1e-9;
        var (d1, d2) = (Side(c, d, a), Side(c, d, b));
        var (d3, d4) = (Side(a, b, c), Side(a, b, d));
        return ((d1 > epsilon && d2 < -epsilon) || (d1 < -epsilon && d2 > epsilon))
            && ((d3 > epsilon && d4 < -epsilon) || (d3 < -epsilon && d4 > epsilon));
    }
}
