using SvgCreator.Imaging;

namespace SvgCreator.Tracing;

/// <summary>
/// Two-pass connected-component labeling (8-connectivity) using union-find.
/// Components smaller than a minimum area are dropped as noise.
/// </summary>
internal static class ComponentLabeler
{
    public static ComponentMap Label(BinaryImage image, int minArea)
    {
        int width = image.Width, height = image.Height;
        var ink = image.Pixels;
        var labels = new int[ink.Length];
        var parent = new int[64];
        var next = 1;

        // Pass 1: provisional labels, merging with already-visited neighbours (W, NW, N, NE).
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var i = row + x;
                if (ink[i] == 0)
                {
                    continue;
                }

                var label = 0;
                if (x > 0)
                {
                    label = Merge(parent, label, labels[i - 1]);
                }

                if (y > 0)
                {
                    var up = i - width;
                    if (x > 0)
                    {
                        label = Merge(parent, label, labels[up - 1]);
                    }

                    label = Merge(parent, label, labels[up]);
                    if (x < width - 1)
                    {
                        label = Merge(parent, label, labels[up + 1]);
                    }
                }

                if (label == 0)
                {
                    if (next == parent.Length)
                    {
                        Array.Resize(ref parent, parent.Length * 2);
                    }

                    label = next++;
                    parent[label] = label;
                }

                labels[i] = label;
            }
        }

        // Pass 2: resolve to roots and measure area.
        var rootArea = new int[next];
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] != 0)
            {
                var root = Find(parent, labels[i]);
                labels[i] = root;
                rootArea[root]++;
            }
        }

        // Compact surviving roots to 1..Count.
        var finalId = new int[next];
        var areas = new List<int> { 0 };
        for (var root = 1; root < next; root++)
        {
            if (rootArea[root] > 0 && rootArea[root] >= minArea)
            {
                finalId[root] = areas.Count;
                areas.Add(rootArea[root]);
            }
        }

        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = finalId[labels[i]];
        }

        return new ComponentMap(width, height, labels, [.. areas]);
    }

    private static int Merge(int[] parent, int current, int neighbour)
    {
        if (neighbour == 0)
        {
            return current;
        }

        if (current == 0)
        {
            return neighbour;
        }

        var a = Find(parent, current);
        var b = Find(parent, neighbour);
        if (a != b)
        {
            // Keep the smaller label as root so roots are stable in raster order.
            if (a < b)
            {
                parent[b] = a;
            }
            else
            {
                parent[a] = b;
            }
        }

        return current;
    }

    private static int Find(int[] parent, int label)
    {
        while (parent[label] != label)
        {
            parent[label] = parent[parent[label]]; // path halving
            label = parent[label];
        }

        return label;
    }
}
