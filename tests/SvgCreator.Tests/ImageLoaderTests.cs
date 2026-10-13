using SkiaSharp;
using SvgCreator.Imaging;

namespace SvgCreator.Tests;

public class ImageLoaderTests
{
    // The first 40 bytes of a HEIC from macOS sips: major brand 'heic', compatible 'mif1' and others.
    private const string HeicHeaderHex = "000000246674797068656963000000006d6966314d6950726d6961664d6948426865696300000186";

    [Fact]
    public void DescribeFailure_NamesHeif()
    {
        var message = ImageLoader.DescribeFailure(Convert.FromHexString(HeicHeaderHex), SKCodecResult.Unimplemented);

        Assert.StartsWith("HEIF images aren't supported", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeFailure_RandomBytes_StayGeneric()
    {
        byte[] garbage = [0x13, 0x37, 0, 0, (byte)'f', (byte)'t', (byte)'y', (byte)'x', 1, 2, 3, 4, 5, 6, 7, 8];

        Assert.Equal("Unsupported or corrupt image data.", ImageLoader.DescribeFailure(garbage, SKCodecResult.Unimplemented));
    }

    [Fact]
    public void Load_TruncatedImage_SaysSo()
    {
        var jpeg = Convert.FromBase64String(ImageOrientationTests.QuadrantJpegBase64)[..200];

        var ex = Assert.Throws<InvalidDataException>(() => ImageLoader.Load(new MemoryStream(jpeg)));

        Assert.Equal("The image data is incomplete; the file may be truncated.", ex.Message);
    }

    [Fact]
    public void Load_NonSeekableStream_IsBufferedAndDecodes()
    {
        using var png = TestImages.PngWithBlackRect(8, 6, new SKRect(1, 1, 3, 3));

        var image = ImageLoader.Load(new ForwardOnlyStream(png));

        Assert.Equal((8, 6), (image.Width, image.Height));
    }

    private sealed class ForwardOnlyStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
