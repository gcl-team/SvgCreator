namespace SvgCreator.Tracing;

/// <summary>
/// Per-pixel connected-component labels: 0 = background, 1..<see cref="Count"/> = ink component.
/// </summary>
internal sealed class ComponentMap
{
    public ComponentMap(int width, int height, int[] labels, int[] areas)
    {
        Width = width;
        Height = height;
        Labels = labels;
        Areas = areas;
    }

    public int Width { get; }

    public int Height { get; }

    public int[] Labels { get; }

    /// <summary>Pixel count per label; index 0 (background) is unused.</summary>
    public int[] Areas { get; }

    public int Count => Areas.Length - 1;

    public bool IsInk(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height && Labels[(y * Width) + x] != 0;
}
