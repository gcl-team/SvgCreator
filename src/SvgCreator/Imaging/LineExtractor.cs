namespace SvgCreator.Imaging;

/// <summary>
/// Extracts drawn lines with a center-surround Difference of Gaussians (the core of XDoG):
/// a pixel is ink when it is darker than its neighbourhood by at least a contrast threshold,
/// or (optionally) when its colour differs from its neighbourhood by at least a colour threshold.
/// </summary>
/// <remarks>
/// Responds to <em>local</em> darkness rather than absolute darkness, so coloured lines on light
/// areas (e.g. dark-pink hair lines on pink hair) are found, while flat regions of any brightness
/// (a dark background, a pale face) and soft gradual shading produce nothing. Step edges between
/// regions yield a thin band on their darker side, which outlines colour boundaries.
/// <para>
/// Thresholding uses hysteresis (as in Canny): pixels at least <c>contrast</c> darker start lines,
/// which then grow through connected pixels at least <c>weakContrast</c> darker. Lines whose
/// response fades along the way (e.g. edges of hair strands thinning to a tip) stay closed, while
/// isolated faint texture that touches no real line is still ignored. Weak pixels must also lie on
/// the response's ridge (a local maximum across the line), so lines grow along their length rather
/// than thickening sideways.
/// </para>
/// <para>
/// Boundaries between hues of similar brightness (pink hair on pale skin) are found in the CIE Lab
/// a*/b* chroma planes. The colour line goes on the darker side (where a luminance line would be),
/// or the more saturated side when brightness is equal; both are where an artist would draw it
/// (hair, not skin) and keep the line one-sided. Most colour boundaries in artwork already carry a
/// luminance line, though, and a colour band beside it would only thicken or double it. So colour
/// pixels are grouped into connected pieces and a piece is kept only if it mostly runs away from
/// luminance lines: a genuine colour-only boundary touches luminance lines at its ends at most.
/// </para>
/// </remarks>
internal static class LineExtractor
{
    // Surround blur relative to the center blur; wide enough that lines up to ~2×SurroundScale×sigma
    // thick are filled solid instead of coming out hollow.
    private const double SurroundScale = 3.0;

    // A colour piece with at least this fraction of its pixels beside a luminance line is the same
    // boundary seen twice and is dropped.
    private const double MaxShadowingFraction = 0.5;

    // Peak center-surround response to a step edge, as a fraction of the step height: max over d of
    // Φ(d/σ) − Φ(d/(SurroundScale·σ)), reached ~1.6σ from the edge; independent of σ.
    private const float StepResponse = 0.24f;

    // Luminance difference (levels) below which two sides count as equally bright.
    private const float EqualBrightness = 0.5f;

    /// <summary>Luminance-only extraction.</summary>
    public static BinaryImage Extract(GrayImage image, double sigma, double contrast, double? weakContrast = null) =>
        Extract(image, null, sigma, contrast, weakContrast ?? contrast, 0);

    /// <param name="image">Luminance.</param>
    /// <param name="chroma">Lab a*/b* planes, or <see langword="null"/> for luminance only.</param>
    /// <param name="sigma">Center blur (Gaussian sigma, pixels).</param>
    /// <param name="contrast">Luminance drop (0-255 levels) below the surroundings that starts a line.</param>
    /// <param name="weakContrast">Smaller drop that continues a line connected to a stronger one.</param>
    /// <param name="colorContrast">Minimum a*b* colour difference (ΔE) between two regions for their boundary to count; 0 disables.</param>
    public static BinaryImage Extract(
        GrayImage image, (float[] A, float[] B)? chroma, double sigma, double contrast, double weakContrast, double colorContrast)
    {
        int width = image.Width, height = image.Height;
        var luminance = new float[image.Pixels.Length];
        for (var i = 0; i < luminance.Length; i++)
        {
            luminance[i] = image.Pixels[i];
        }

        var mask = new byte[luminance.Length];
        var center = GaussianBlur.Apply(luminance, width, height, sigma);
        var surround = GaussianBlur.Apply(luminance, width, height, sigma * SurroundScale);
        ApplyHysteresis(center, surround, width, height, (float)contrast, (float)Math.Min(weakContrast, contrast), mask);

        if (chroma is { } planes && colorContrast > 0)
        {
            MarkColorEdges(planes.A, planes.B, width, height, sigma, (float)colorContrast, center, surround, mask);
        }

        return new BinaryImage(width, height, mask);
    }

    private static void ApplyHysteresis(
        float[] center, float[] surround, int width, int height, float strong, float weak, byte[] mask)
    {
        var stack = new Stack<int>();
        for (var i = 0; i < mask.Length; i++)
        {
            if (surround[i] - center[i] >= strong)
            {
                mask[i] = 1;
                stack.Push(i);
            }
        }

        // Grow from strong pixels through 8-connected weak pixels.
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            int x = i % width, y = i / width;
            for (var dy = -1; dy <= 1; dy++)
            {
                var ny = y + dy;
                if ((uint)ny >= (uint)height)
                {
                    continue;
                }

                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    if ((uint)nx >= (uint)width)
                    {
                        continue;
                    }

                    var n = (ny * width) + nx;
                    if (mask[n] == 0 && surround[n] - center[n] >= weak && IsRidge(center, surround, width, height, nx, ny))
                    {
                        mask[n] = 1;
                        stack.Push(n);
                    }
                }
            }
        }
    }

    // Local maximum of the response in at least two of the four directions (E–W, N–S, and both
    // diagonals). A ridge pixel peaks across the line in several directions; a pixel on a line's flank
    // only "peaks" along the line's own direction.
    private static bool IsRidge(float[] center, float[] surround, int width, int height, int x, int y)
    {
        var value = Response(center, surround, width, height, x, y);
        var peaks = 0;
        ReadOnlySpan<int> dx = [1, 0, 1, 1];
        ReadOnlySpan<int> dy = [0, 1, 1, -1];
        for (var k = 0; k < 4; k++)
        {
            if (value >= Response(center, surround, width, height, x + dx[k], y + dy[k])
                && value >= Response(center, surround, width, height, x - dx[k], y - dy[k]))
            {
                peaks++;
            }
        }

        return peaks >= 2;
    }

    private static float Response(float[] center, float[] surround, int width, int height, int x, int y)
    {
        x = Math.Clamp(x, 0, width - 1);
        y = Math.Clamp(y, 0, height - 1);
        var i = (y * width) + x;
        return surround[i] - center[i];
    }

    private static void MarkColorEdges(
        float[] a, float[] b, int width, int height, double sigma, float colorContrast,
        float[] luminanceCenter, float[] luminanceSurround, byte[] mask)
    {
        var centerA = GaussianBlur.Apply(a, width, height, sigma);
        var centerB = GaussianBlur.Apply(b, width, height, sigma);
        var surroundA = GaussianBlur.Apply(a, width, height, sigma * SurroundScale);
        var surroundB = GaussianBlur.Apply(b, width, height, sigma * SurroundScale);
        // colorContrast is the ΔE between the two regions; the blurred difference only reaches a fraction of it.
        var threshold = colorContrast * StepResponse;
        var thresholdSquared = threshold * threshold;

        var color = new byte[mask.Length];
        for (var i = 0; i < mask.Length; i++)
        {
            if (mask[i] != 0)
            {
                continue;
            }

            float da = centerA[i] - surroundA[i], db = centerB[i] - surroundB[i];
            if ((da * da) + (db * db) < thresholdSquared)
            {
                continue;
            }

            // Positive = darker than surroundings.
            var luminanceDelta = luminanceSurround[i] - luminanceCenter[i];
            var onLineSide = MathF.Abs(luminanceDelta) >= EqualBrightness
                ? luminanceDelta > 0
                : (centerA[i] * centerA[i]) + (centerB[i] * centerB[i]) >= (surroundA[i] * surroundA[i]) + (surroundB[i] * surroundB[i]);

            if (onLineSide)
            {
                color[i] = 1;
            }
        }

        // Keep only colour pieces that mostly run away from luminance lines.
        var radius = (int)Math.Ceiling(sigma * SurroundScale) + 1;
        var nearLuminance = Dilate(mask, width, height, radius);
        var pieces = Tracing.ComponentLabeler.Label(new BinaryImage(width, height, color), minArea: 0);
        var near = new int[pieces.Count + 1];
        for (var i = 0; i < mask.Length; i++)
        {
            if (pieces.Labels[i] != 0 && nearLuminance[i])
            {
                near[pieces.Labels[i]]++;
            }
        }

        for (var i = 0; i < mask.Length; i++)
        {
            var label = pieces.Labels[i];
            if (label != 0 && near[label] < MaxShadowingFraction * pieces.Areas[label])
            {
                mask[i] = 1;
            }
        }
    }

    // True where any ink lies within the (2r+1)² square around the pixel; separable sliding counts.
    private static bool[] Dilate(byte[] ink, int width, int height, int radius)
    {
        var rows = new int[ink.Length];
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var count = 0;
            for (var x = -radius; x < width; x++)
            {
                if (x + radius < width && ink[row + x + radius] != 0)
                {
                    count++;
                }

                if (x - radius - 1 >= 0 && ink[row + x - radius - 1] != 0)
                {
                    count--;
                }

                if (x >= 0)
                {
                    rows[row + x] = count;
                }
            }
        }

        var result = new bool[ink.Length];
        for (var x = 0; x < width; x++)
        {
            var count = 0;
            for (var y = -radius; y < height; y++)
            {
                if (y + radius < height)
                {
                    count += rows[((y + radius) * width) + x];
                }

                if (y - radius - 1 >= 0)
                {
                    count -= rows[((y - radius - 1) * width) + x];
                }

                if (y >= 0)
                {
                    result[(y * width) + x] = count > 0;
                }
            }
        }

        return result;
    }
}
