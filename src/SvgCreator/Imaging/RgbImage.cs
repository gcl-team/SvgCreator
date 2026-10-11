namespace SvgCreator.Imaging;

/// <summary>
/// Opaque 8-bit RGB image (alpha already composited over white), stored row-major as interleaved
/// R, G, B bytes.
/// </summary>
internal sealed class RgbImage
{
    // sRGB byte → linear light, for the Lab conversion.
    private static readonly float[] SrgbToLinear = BuildSrgbToLinear();

    public RgbImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNotEqual(pixels.Length, width * height * 3, nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    /// <summary>Rec. 601 luma.</summary>
    public GrayImage ToGray()
    {
        var gray = new byte[Width * Height];
        for (var i = 0; i < gray.Length; i++)
        {
            var p = i * 3;
            gray[i] = (byte)(((Pixels[p] * 299) + (Pixels[p + 1] * 587) + (Pixels[p + 2] * 114)) / 1000);
        }

        return new GrayImage(Width, Height, gray);
    }

    /// <summary>
    /// CIE L*a*b* (D65) chroma planes: a* (green→red) and b* (blue→yellow). Euclidean distance in
    /// these units approximates perceived colour difference (ΔE) independent of brightness.
    /// </summary>
    public (float[] A, float[] B) ToLabChroma()
    {
        var a = new float[Width * Height];
        var b = new float[Width * Height];
        for (var i = 0; i < a.Length; i++)
        {
            var p = i * 3;
            float r = SrgbToLinear[Pixels[p]], g = SrgbToLinear[Pixels[p + 1]], bl = SrgbToLinear[Pixels[p + 2]];

            var x = ((0.4124564f * r) + (0.3575761f * g) + (0.1804375f * bl)) / 0.95047f;
            var y = (0.2126729f * r) + (0.7151522f * g) + (0.0721750f * bl);
            var z = ((0.0193339f * r) + (0.1191920f * g) + (0.9503041f * bl)) / 1.08883f;

            float fx = LabF(x), fy = LabF(y), fz = LabF(z);
            a[i] = 500 * (fx - fy);
            b[i] = 200 * (fy - fz);
        }

        return (a, b);
    }

    private static float LabF(float t) =>
        t > 216f / 24389f ? MathF.Cbrt(t) : ((24389f / 27f * t) + 16) / 116;

    private static float[] BuildSrgbToLinear()
    {
        var table = new float[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i / 255.0;
            table[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
        }

        return table;
    }
}
