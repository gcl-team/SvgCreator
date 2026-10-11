using SvgCreator.Imaging;

namespace SvgCreator.Tests;

public class BinarizerTests
{
    [Fact]
    public void Apply_MarksPixelsDarkerThanThresholdAsInk()
    {
        var gray = new GrayImage(4, 1, [0, 127, 128, 255]);

        var mask = Binarizer.Apply(gray, threshold: 128);

        Assert.Equal([1, 1, 0, 0], mask.Pixels);
    }

    [Fact]
    public void ImageLoader_TreatsTransparentPixelsAsWhite()
    {
        // One opaque black pixel, one fully transparent black pixel.
        byte[] rgba = [0, 0, 0, 255, 0, 0, 0, 0];

        var gray = ImageLoader.ToRgb(rgba, width: 2, height: 1, rowBytes: 8).ToGray();

        Assert.Equal([0, 255], gray.Pixels);
    }
}
