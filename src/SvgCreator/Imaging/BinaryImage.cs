namespace SvgCreator.Imaging;

/// <summary>Ink mask stored row-major in a flat buffer: 1 = ink, 0 = background.</summary>
internal sealed class BinaryImage
{
    public BinaryImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNotEqual(pixels.Length, width * height, nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public bool IsInk(int x, int y) => Pixels[(y * Width) + x] != 0;
}
