namespace SvgCreator.Imaging;

/// <summary>Separable Gaussian blur over a flat float buffer, clamping at the borders.</summary>
internal static class GaussianBlur
{
    public static float[] Apply(ReadOnlySpan<float> source, int width, int height, double sigma)
    {
        var kernel = Kernel(sigma);
        var radius = kernel.Length / 2;
        var temp = new float[source.Length];
        var result = new float[source.Length];

        // Horizontal pass.
        for (var y = 0; y < height; y++)
        {
            var row = source.Slice(y * width, width);
            var target = temp.AsSpan(y * width, width);
            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += kernel[k + radius] * row[Math.Clamp(x + k, 0, width - 1)];
                }

                target[x] = sum;
            }
        }

        // Vertical pass.
        for (var y = 0; y < height; y++)
        {
            var target = result.AsSpan(y * width, width);
            for (var k = -radius; k <= radius; k++)
            {
                var weight = kernel[k + radius];
                var row = temp.AsSpan(Math.Clamp(y + k, 0, height - 1) * width, width);
                for (var x = 0; x < width; x++)
                {
                    target[x] += weight * row[x];
                }
            }
        }

        return result;
    }

    private static float[] Kernel(double sigma)
    {
        var radius = Math.Max(1, (int)Math.Ceiling(3 * sigma));
        var kernel = new float[(2 * radius) + 1];
        var sum = 0.0;
        for (var i = -radius; i <= radius; i++)
        {
            var value = Math.Exp(-(i * i) / (2 * sigma * sigma));
            kernel[i + radius] = (float)value;
            sum += value;
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] = (float)(kernel[i] / sum);
        }

        return kernel;
    }
}
