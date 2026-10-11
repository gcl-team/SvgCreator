using SvgCreator.Imaging;

namespace SvgCreator.Tests;

internal static partial class TestImages
{
    /// <summary>Filled disc of the given radius centred in a square canvas.</summary>
    public static BinaryImage Disc(int size, double radius)
    {
        var pixels = new byte[size * size];
        var c = size / 2.0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                double dx = x + 0.5 - c, dy = y + 0.5 - c;
                pixels[(y * size) + x] = (dx * dx) + (dy * dy) <= radius * radius ? (byte)1 : (byte)0;
            }
        }

        return new BinaryImage(size, size, pixels);
    }

    /// <summary>Right triangle whose hypotenuse is a pixel staircase with the given run per step.</summary>
    public static BinaryImage Staircase(int height, int run)
    {
        var width = height * run;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < (y + 1) * run; x++)
            {
                pixels[(y * width) + x] = 1;
            }
        }

        return new BinaryImage(width, height, pixels);
    }
}
