namespace SvgCreator.Tracing;

/// <summary>
/// Traces closed outlines along pixel edges (crack following) so filled outlines reproduce the
/// ink coverage exactly; even 1-pixel lines keep their width. Each boundary edge is visited once,
/// so tracing is O(pixels).
/// </summary>
/// <remarks>
/// Edges are walked with ink on the right. At each lattice vertex the walker prefers turning left,
/// then straight, then right, which treats diagonally touching ink as connected (8-connectivity),
/// matching <see cref="ComponentLabeler"/>.
/// </remarks>
internal static class ContourTracer
{
    // Directions in clockwise order (y down): East, South, West, North.
    private static readonly int[] Dx = [1, 0, -1, 0];
    private static readonly int[] Dy = [0, 1, 0, -1];

    // Offsets from an edge's start vertex to the pixel on its right (ink) and left (background).
    private static readonly int[] RightDx = [0, -1, -1, 0];
    private static readonly int[] RightDy = [0, 0, -1, -1];
    private static readonly int[] LeftDx = [0, 0, -1, -1];
    private static readonly int[] LeftDy = [-1, 0, 0, -1];

    // Offsets from a pixel to the start vertex of its edge in each direction (top, right, bottom, left).
    private static readonly int[] StartDx = [0, 1, 1, 0];
    private static readonly int[] StartDy = [0, 0, 1, 1];

    public static IReadOnlyList<Contour> Trace(ComponentMap map)
    {
        var width = map.Width;
        var labels = map.Labels;
        var visited = new byte[labels.Length];
        var contours = new List<Contour>();

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                if (labels[i] == 0)
                {
                    continue;
                }

                for (var d = 0; d < 4; d++)
                {
                    if ((visited[i] & (1 << d)) != 0)
                    {
                        continue;
                    }

                    var vx = x + StartDx[d];
                    var vy = y + StartDy[d];
                    if (IsBoundary(map, vx, vy, d))
                    {
                        contours.Add(TraceLoop(map, visited, vx, vy, d, labels[i]));
                    }
                }
            }
        }

        return contours;
    }

    private static Contour TraceLoop(ComponentMap map, byte[] visited, int startX, int startY, int startDir, int label)
    {
        var points = new List<PointI>();
        int vx = startX, vy = startY, d = startDir;

        do
        {
            visited[((vy + RightDy[d]) * map.Width) + vx + RightDx[d]] |= (byte)(1 << d);

            vx += Dx[d];
            vy += Dy[d];

            var left = (d + 3) & 3;
            var next = IsBoundary(map, vx, vy, left) ? left
                : IsBoundary(map, vx, vy, d) ? d
                : (d + 1) & 3;

            if (next != d)
            {
                points.Add(new PointI(vx, vy));
            }

            d = next;
        }
        while (vx != startX || vy != startY || d != startDir);

        return new Contour(label, SignedArea(points) < 0, points);
    }

    private static bool IsBoundary(ComponentMap map, int vx, int vy, int d) =>
        map.IsInk(vx + RightDx[d], vy + RightDy[d]) && !map.IsInk(vx + LeftDx[d], vy + LeftDy[d]);

    // Twice the signed area; positive for clockwise loops in y-down coordinates.
    internal static long SignedArea(IReadOnlyList<PointI> points)
    {
        long sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += ((long)a.X * b.Y) - ((long)b.X * a.Y);
        }

        return sum;
    }
}
