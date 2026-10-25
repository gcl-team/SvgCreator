using SvgCreator.Fitting;

namespace SvgCreator.Mapping;

/// <summary>Where a label sits around its marker, in order of preference.</summary>
internal enum LabelSide
{
    Below,
    Right,
    Left,
    Above,
    BelowRight,
    BelowLeft,
    AboveRight,
    AboveLeft,
}

/// <summary>A marker's label to place: the marker's shape and the label's size.</summary>
/// <param name="Center">The marker's centre.</param>
/// <param name="HalfWidth">Half the marker's width (its radius for a circle, more for a pill).</param>
/// <param name="Radius">The marker's radius: half its height.</param>
/// <param name="Width">The label's width.</param>
/// <param name="Height">The label's height.</param>
/// <param name="FirstLineHeight">Height of the label's first line, centred on the marker when the label sits beside it.</param>
internal sealed record LabelRequest(PointD Center, double HalfWidth, double Radius, double Width, double Height, double FirstLineHeight)
{
    public RectD MarkerBounds => new(Center.X - HalfWidth, Center.Y - Radius, 2 * HalfWidth, 2 * Radius);
}

/// <summary>Where a label went.</summary>
internal readonly record struct LabelPlacement(LabelSide Side, RectD Bounds);

/// <summary>
/// Places each marker's label at whichever of the spots around it (<see cref="LabelSide"/>) is
/// least in the way: each spot is scored by how much it covers arrows, markers and other labels,
/// and how far it runs off the canvas, plus a small preference for the earlier sides so that when
/// nothing is in the way, labels sit under their markers.
/// </summary>
/// <remarks>
/// Greedy in travel order, then refined: each label is placed again knowing where all the others
/// went, until nothing moves. Running off the canvas is weighed by the caller: while the map can
/// still shrink to make room, a label off the edge is cheap (shrinking fixes it), and covering an
/// arrow (which nothing later fixes) is not. Once the map can't shrink, the caller asks for labels
/// kept on the canvas: each spot is moved onto it before it is scored, so the spot is judged where
/// the label will actually be, at a small cost for each pixel it moved from beside its marker.
/// </remarks>
internal static class LabelPlacer
{
    private const double ArrowWeight = 5;
    private const double MarkerWeight = 5;
    private const double LabelWeight = 10;
    private const double DisplacementWeight = 20;   // per pixel a kept-on label moved
    private const int RefinementPasses = 4;

    public static LabelPlacement[] Place(
        IReadOnlyList<LabelRequest> requests,
        IReadOnlyList<(PointD From, PointD To)> arrows,
        double arrowWidth,
        double gap,
        RectD canvas,
        double offCanvasWeight,
        bool keepOnCanvas = false,
        IReadOnlyList<RectD>? obstacles = null)
    {
        obstacles ??= [];
        var placements = new LabelPlacement?[requests.Count];
        for (var i = 0; i < requests.Count; i++)
        {
            placements[i] = Best(i);
        }

        for (var pass = 0; pass < RefinementPasses; pass++)
        {
            var moved = false;
            for (var i = 0; i < requests.Count; i++)
            {
                var best = Best(i);
                if (best != placements[i])
                {
                    (placements[i], moved) = (best, true);
                }
            }

            if (!moved)
            {
                break;
            }
        }

        return [.. placements.Select(p => p!.Value)];

        LabelPlacement Best(int index)
        {
            var request = requests[index];
            var (best, bestScore) = (default(LabelPlacement), double.PositiveInfinity);
            foreach (var side in Enum.GetValues<LabelSide>())
            {
                var wanted = Candidate(request, side, gap);
                var bounds = keepOnCanvas ? wanted.MoveInside(canvas) : wanted;
                var moved = new PointD(bounds.X - wanted.X, bounds.Y - wanted.Y).Length;
                var score = Score(index, bounds) + (moved * DisplacementWeight) + (int)side;
                if (score < bestScore)
                {
                    (best, bestScore) = (new LabelPlacement(side, bounds), score);
                }
            }

            return best;
        }

        double Score(int index, RectD bounds)
        {
            var area = bounds.Width * bounds.Height;
            var score = (area - bounds.OverlapArea(canvas)) * offCanvasWeight;

            // Arrows count with clearance: the label keeps half a gap from the stroke's edge.
            var clearance = (arrowWidth / 2) + (gap / 2);
            var padded = bounds.Inflate(clearance);
            foreach (var (from, to) in arrows)
            {
                score += ClippedLength(from, to, padded) * 2 * clearance * ArrowWeight;
            }

            // Other fixed text (a break's distance) counts like a marker.
            score += obstacles.Sum(o => bounds.OverlapArea(o.Inflate(gap / 2))) * MarkerWeight;
            for (var j = 0; j < requests.Count; j++)
            {
                score += bounds.OverlapArea(requests[j].MarkerBounds.Inflate(gap / 2)) * MarkerWeight;
                if (j != index && placements[j] is { } other)
                {
                    score += bounds.OverlapArea(other.Bounds.Inflate(gap / 2)) * LabelWeight;
                }
            }

            return score;
        }
    }

    /// <summary>
    /// The label's bounds at <paramref name="side"/>: <paramref name="gap"/> clear of the marker.
    /// Beside the marker, the first line is centred on it; at a diagonal, the label's nearest
    /// corner sits off the marker's end at 45°.
    /// </summary>
    internal static RectD Candidate(LabelRequest r, LabelSide side, double gap)
    {
        var (c, w, h) = (r.Center, r.Width, r.Height);
        var reach = (r.Radius + gap) * Math.Sqrt(0.5);   // a diagonal's offset from the end cap's centre
        var cap = r.HalfWidth - r.Radius;                 // end cap's centre from the marker's centre
        return side switch
        {
            LabelSide.Below => new(c.X - (w / 2), c.Y + r.Radius + gap, w, h),
            LabelSide.Above => new(c.X - (w / 2), c.Y - r.Radius - gap - h, w, h),
            LabelSide.Right => new(c.X + r.HalfWidth + gap, c.Y - (r.FirstLineHeight / 2), w, h),
            LabelSide.Left => new(c.X - r.HalfWidth - gap - w, c.Y - (r.FirstLineHeight / 2), w, h),
            LabelSide.BelowRight => new(c.X + cap + reach, c.Y + reach, w, h),
            LabelSide.BelowLeft => new(c.X - cap - reach - w, c.Y + reach, w, h),
            LabelSide.AboveRight => new(c.X + cap + reach, c.Y - reach - h, w, h),
            LabelSide.AboveLeft => new(c.X - cap - reach - w, c.Y - reach - h, w, h),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown label side."),
        };
    }

    /// <summary>Length of the segment inside <paramref name="rect"/> (Liang–Barsky clipping).</summary>
    internal static double ClippedLength(PointD from, PointD to, RectD rect)
    {
        var d = to - from;
        var (t0, t1) = (0.0, 1.0);
        if (!Clip(-d.X, from.X - rect.X) || !Clip(d.X, rect.Right - from.X) ||
            !Clip(-d.Y, from.Y - rect.Y) || !Clip(d.Y, rect.Bottom - from.Y))
        {
            return 0;
        }

        return t1 > t0 ? (t1 - t0) * d.Length : 0;

        // One edge: p·t ≤ q keeps the part of the segment on the rectangle's side of it.
        bool Clip(double p, double q)
        {
            if (p == 0)
            {
                return q >= 0;
            }

            var t = q / p;
            if (p < 0)
            {
                t0 = Math.Max(t0, t);
            }
            else
            {
                t1 = Math.Min(t1, t);
            }

            return t0 <= t1;
        }
    }
}
