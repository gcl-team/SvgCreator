using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;
using SvgCreator.Composition;
using SvgCreator.Fitting;

namespace SvgCreator.Tests;

public class MailPieceTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static readonly EnvelopeOptions Envelope = new() { Width = 440, Height = 220 };

    private static double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);

    private static MemoryStream StampPicture() => new(Convert.FromBase64String(PostageStampTests.PictureBase64));

    // A datestamp-like seal: a black ring on white, so the postmark finds a circular frame. Pixels
    // are set directly rather than drawn, so they don't depend on Skia's anti-aliasing (and the
    // golden file stays the same on every platform).
    private static MemoryStream Seal()
    {
        using var bitmap = new SKBitmap(120, 120);
        for (var y = 0; y < 120; y++)
        {
            for (var x = 0; x < 120; x++)
            {
                var d = Math.Sqrt(Math.Pow(x + 0.5 - 60, 2) + Math.Pow(y + 0.5 - 60, 2));
                bitmap.SetPixel(x, y, d is >= 47 and <= 53 ? SKColors.Black : SKColors.White);
            }
        }

        var png = new MemoryStream();
        bitmap.Encode(png, SKEncodedImageFormat.Png, 100);
        png.Position = 0;
        return png;
    }

    private static PostageStampPiece Stamp() => new(StampPicture(), new PostageStampOptions { Country = "SINGAPORE", Fee = "$1.20" });

    private static PostmarkPiece Postmark() => new(Seal()) { Options = PostmarkPiece.DefaultOptions with { Mode = ExtractionMode.Threshold } };

    private static XElement Piece(XElement root, string className) =>
        root.Elements(Svg + "svg").Single(e => (string?)e.Attribute("class") == className);

    [Fact]
    public void Golden() => SvgGoldenTests.AssertGolden("mail-piece.svg", new MailPieceGenerator().Create(Envelope, Stamp(), Postmark()));

    [Fact]
    public void Create_NestsEachPieceInOrder_OnTheEnvelopeCanvas()
    {
        var root = XDocument.Parse(new MailPieceGenerator().Create(Envelope, Stamp(), Postmark())).Root!;

        Assert.Equal((440.0, 220.0), (Number(root, "width"), Number(root, "height")));
        Assert.Equal(["mail-envelope", "mail-postage-stamp", "mail-postmark"], root.Elements(Svg + "svg").Select(e => (string?)e.Attribute("class")));
    }

    [Fact]
    public void Create_EachPieceIsItsGeneratorsOutputUnchanged()
    {
        var root = XDocument.Parse(new MailPieceGenerator().Create(Envelope, Stamp())).Root!;
        var envelope = XDocument.Parse(new EnvelopeGenerator().Create(Envelope)).Root!;
        var stamp = XDocument.Parse(new PostageStampGenerator().Create(StampPicture(), Stamp().Options)).Root!;

        foreach (var (nested, alone) in new[] { (Piece(root, "mail-envelope"), envelope), (Piece(root, "mail-postage-stamp"), stamp) })
        {
            Assert.Equal((string?)alone.Attribute("viewBox"), (string?)nested.Attribute("viewBox"));
            Assert.Equal(alone.Elements().Select(e => e.ToString()), nested.Elements().Select(e => e.ToString()));
        }
    }

    [Fact]
    public void Create_EnvelopeOnly_HasNoOtherPieces()
    {
        var root = XDocument.Parse(new MailPieceGenerator().Create(Envelope)).Root!;

        Assert.Equal(["mail-envelope"], root.Elements(Svg + "svg").Select(e => (string?)e.Attribute("class")));
    }

    [Fact]
    public void Create_IdsAreUniqueAcrossPieces()
    {
        // A crumpled envelope and a worn postmark both define filters on one canvas.
        var root = XDocument.Parse(new MailPieceGenerator().Create(Envelope with { Style = EnvelopeStyle.Crumpled }, Stamp(), Postmark())).Root!;
        var ids = root.Descendants().Select(e => (string?)e.Attribute("id")).OfType<string>().ToList();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void StampRect_SitsInTheTopRightCornerInsideTheBorder()
    {
        // A 3:4 stamp on a 1100 × 550 envelope: 0.36 of the height tall, (0.07 + 0.06) inset.
        var stamp = MailPieceComposer.StampRect((300, 400), 1100, 550);

        Assert.Equal(0.36 * 550, stamp.Height, 9);
        Assert.Equal(0.13 * 550, stamp.Y, 9);
        Assert.Equal(1100 - (0.13 * 550), stamp.X + stamp.Width, 9);
    }

    [Fact]
    public void StampRect_NarrowsAVeryWidePicture()
    {
        var stamp = MailPieceComposer.StampRect((5000, 100), 1100, 550);

        Assert.Equal(0.45 * 1100, stamp.Width, 9);
        Assert.True(stamp.Height < 0.36 * 550);
    }

    [Fact]
    public void PostmarkRect_OnANarrowStamp_MovesTheRingLeftToCoverAThird()
    {
        // A portrait stamp, 150 wide and 200 tall: a ring of radius 0.42 × 200 = 84 centred on its
        // left edge would cover 84 of its 150 width. Moved left by 84 − 150/3 = 34, it covers 50.
        var stamp = new MailPieceComposer.Rect(800, 70, 150, 200);
        var ring = new Ellipse(new PointD(50, 60), 40, 40, 0); // in the traced drawing's coordinates

        var rect = MailPieceComposer.PostmarkRect((180, 120), ring, 120, stamp);
        var scale = rect.Height / 120;
        var centreX = rect.X + (50 * scale);

        Assert.Equal(0.42 * 200, 40 * scale, 9);                  // ring radius: a share of the stamp height
        Assert.Equal(stamp.X - 34, centreX, 9);
        Assert.Equal(150.0 / 3, centreX + (40 * scale) - stamp.X, 9); // the ring covers a third of the stamp
        Assert.Equal(stamp.Y + (0.62 * 200), rect.Y + (60 * scale), 9);
        Assert.Equal(180 * scale, rect.Width, 9);                  // the wider canvas (waves) keeps its aspect
    }

    [Fact]
    public void PostmarkRect_OnAWideStamp_CentresTheRingOnItsLeftEdge()
    {
        // 300 wide: a third is 100, more than the radius of 84, so the ring stays where it was.
        var stamp = new MailPieceComposer.Rect(700, 70, 300, 200);
        var ring = new Ellipse(new PointD(50, 60), 40, 40, 0);

        var rect = MailPieceComposer.PostmarkRect((180, 120), ring, 120, stamp);

        Assert.Equal(stamp.X, rect.X + (50 * rect.Height / 120), 9);
    }

    [Fact]
    public void Create_ThickRing_ItsVisibleOuterEdgeIsTheSizedRadius()
    {
        // The seal's ring is a band (radii 53 and 47), written as one stroked circle on its
        // midline (r = 50, stroke 6). What the eye sees, and what the cover rule must bound, runs
        // out to 53: so the layout sizes the outer edge, not the midline, to 0.42 of the stamp.
        var root = XDocument.Parse(new MailPieceGenerator().Create(Envelope, Stamp(), Postmark())).Root!;
        var stamp = Piece(root, "mail-postage-stamp");
        var postmark = Piece(root, "mail-postmark");
        var scale = Number(postmark, "width") / double.Parse(((string)postmark.Attribute("viewBox")!).Split(' ')[2], CultureInfo.InvariantCulture);
        var ring = postmark.Descendants(Svg + "circle").Single();

        var outerEdge = (Number(ring, "r") + (Number(ring, "stroke-width") / 2)) * scale;

        // The ring's r and stroke are traced geometry, written at tenths; the layout uses the exact
        // fitted radius. Agreement to 0.05 px still tells the outer edge from the midline (≈ 1.9 px in).
        Assert.Equal(0.42 * Number(stamp, "height"), outerEdge, 1);
        Assert.NotEqual(0.42 * Number(stamp, "height"), Number(ring, "r") * scale, 1);
    }

    [Fact]
    public void Create_InvalidOptions_FailBeforeAnyImageIsRead()
    {
        var unreadable = new ThrowingStream();

        Assert.ThrowsAny<ArgumentException>(() => new MailPieceGenerator().Create(
            Envelope, new PostageStampPiece(unreadable, new PostageStampOptions { Country = " " })));
        Assert.ThrowsAny<ArgumentException>(() => new MailPieceGenerator().Create(
            Envelope, Stamp(), new PostmarkPiece(unreadable) { Options = PostmarkPiece.DefaultOptions with { FillColor = "notacolour" } }));
        Assert.False(unreadable.WasRead);
    }

    [Fact]
    public async Task CreateAsync_MatchesCreate() =>
        Assert.Equal(
            new MailPieceGenerator().Create(Envelope, Stamp(), Postmark()),
            await new MailPieceGenerator().CreateAsync(Envelope, Stamp(), Postmark(), TestContext.Current.CancellationToken));

    private sealed class ThrowingStream : MemoryStream
    {
        public bool WasRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            WasRead = true;
            throw new InvalidOperationException("Read before validation.");
        }
    }
}
