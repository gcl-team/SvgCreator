namespace SvgCreator.Imaging;

/// <summary>Turns a grayscale image into an ink mask using a global threshold.</summary>
/// <remarks>TODO: adaptive / Otsu thresholding for unevenly lit or coloured artwork.</remarks>
internal static class Binarizer
{
    public static BinaryImage Apply(GrayImage image, int threshold)
    {
        var source = image.Pixels;
        var mask = new byte[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            mask[i] = source[i] < threshold ? (byte)1 : (byte)0;
        }

        return new BinaryImage(image.Width, image.Height, mask);
    }
}
