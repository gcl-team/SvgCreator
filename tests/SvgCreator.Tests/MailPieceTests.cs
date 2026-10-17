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

        var rect = MailPieceComposer.PostmarkRect((180, 120), ring, 120, stamp, 1100);
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

        var rect = MailPieceComposer.PostmarkRect((180, 120), ring, 120, stamp, 1100);

        Assert.Equal(stamp.X, rect.X + (50 * rect.Height / 120), 9);
    }

    [Theory]
    [InlineData(0, 40, 80)]   // 80 wide, 40 tall: reaches twice as far sideways as up
    [InlineData(90, 80, 40)]  // the same ellipse turned upright: 40 wide, 80 tall (fits, so it doesn't move)
    [InlineData(45, 63.2456, 63.2456)] // turned 45°: √((80² + 40²) / 2) each way
    public void PostmarkRect_EllipticalFrame_SizesItsHeightAndBoundsItsWidth(double angle, double halfHeight, double halfWidth)
    {
        // A portrait stamp, 150 wide and 200 tall: the frame's vertical reach is sized to
        // 0.42 × 200 = 84, and its horizontal reach at that size may cover only 150 / 3 = 50.
        var stamp = new MailPieceComposer.Rect(800, 70, 150, 200);
        var ring = new Ellipse(new PointD(100, 100), 80, 40, angle);
        Assert.Equal((halfWidth, halfHeight), ring.HalfExtents, new ExtentComparer());

        var rect = MailPieceComposer.PostmarkRect((300, 200), ring, 200, stamp, 1200); // wide enough not to narrow it
        var scale = rect.Height / 200;
        var centreX = rect.X + (100 * scale);

        Assert.Equal(0.42 * 200, halfHeight * scale, 3);                    // sized by how far it reaches up and down
        // Covers at most a third, however wide: a frame that already fits stays on the stamp's edge.
        Assert.Equal(Math.Min(halfWidth * scale, 150.0 / 3), centreX + (halfWidth * scale) - stamp.X, 3);
    }

    [Fact]
    public void PostmarkRect_WideEllipticalFrame_IsMadeSmallerToFitTheEnvelope()
    {
        // A 550 × 550 envelope with no postage stamp: its stamp is 148.5 × 198 at x = 330. A
        // frame 180 × 60 centred in a 400 × 200 drawing would scale by 0.42 × 198 / 60 = 1.386,
        // and after the cover rule its ring would span x = −119.46 to 379.5. The canvas is 420
        // wide, as the Postmark style widens it for the waves.
        var stamp = MailPieceComposer.StampRect(null, 550, 550);
        var ring = new Ellipse(new PointD(200, 100), 180, 60, 0);

        var rect = MailPieceComposer.PostmarkRect((420, 200), ring, 200, stamp, 550);
        var scale = rect.Width / 420;
        var centreX = rect.X + (200 * scale);

        Assert.True(scale < 0.42 * 198 / 60);
        // The canvas, ring and waves, is on the envelope: here its left edge is what limits it.
        Assert.Equal(0, rect.X, 9);
        Assert.InRange(rect.X + rect.Width, 0, 550);
        Assert.InRange(centreX - (180 * scale), 0, stamp.X);         // so is the ring's left edge
        Assert.Equal(Math.Min(180 * scale, stamp.Width / 3), centreX + (180 * scale) - stamp.X, 9); // and it still covers a third
        Assert.Equal(stamp.Y + (0.62 * stamp.Height), rect.Y + (100 * scale), 9);
    }

    [Fact]
    public void Create_WideEllipticalPostmark_StaysOnTheEnvelope()
    {
        // The same frame traced from a picture: a band 6 px wide, so the real waves set the canvas.
        var root = XDocument.Parse(new MailPieceGenerator().Create(new EnvelopeOptions { Width = 550, Height = 550 }, postmark: new PostmarkPiece(EllipticalSeal())
        {
            Options = PostmarkPiece.DefaultOptions with { Mode = ExtractionMode.Threshold },
        })).Root!;
        var postmark = Piece(root, "mail-postmark");
        var (x, w) = (Number(postmark, "x"), Number(postmark, "width"));
        var stamp = MailPieceComposer.StampRect(null, 550, 550);

        Assert.Empty(postmark.Descendants(Svg + "rect"));                 // the ellipse was taken as the frame
        Assert.InRange(x, 0, 550);
        Assert.InRange(x + w, 0, 550.01);
        Assert.True(x + w > stamp.X);                                      // the postmark still reaches the stamp
    }

    // A 400 × 200 picture of an elliptical band, outer radii 180 × 60 and inner 174 × 54.
    private static MemoryStream EllipticalSeal()
    {
        using var bitmap = new SKBitmap(400, 200);
        for (var y = 0; y < 200; y++)
        {
            for (var x = 0; x < 400; x++)
            {
                double Reach(double rx, double ry) => Math.Pow((x + 0.5 - 200) / rx, 2) + Math.Pow((y + 0.5 - 100) / ry, 2);
                bitmap.SetPixel(x, y, Reach(180, 60) <= 1 && Reach(174, 54) >= 1 ? SKColors.Black : SKColors.White);
            }
        }

        var png = new MemoryStream();
        bitmap.Encode(png, SKEncodedImageFormat.Png, 100);
        png.Position = 0;
        return png;
    }

    [Fact]
    public void PostmarkRect_NoFrame_ScalesThePictureAndOverlapsAQuarter()
    {
        // A 150 × 200 stamp: the picture is 0.8 × 200 = 160 tall (its 240 × 120 canvas, drawn at
        // 120 tall, scales by 4/3 to 320 wide), and its right quarter, 80, lies over the stamp.
        var stamp = new MailPieceComposer.Rect(800, 70, 150, 200);

        var rect = MailPieceComposer.PostmarkRect((240, 120), null, 120, stamp, 1100);

        Assert.Equal(0.8 * 200, rect.Height, 9);
        Assert.Equal(240 * rect.Height / 120, rect.Width, 9);
        Assert.Equal(rect.Width / 4, rect.X + rect.Width - stamp.X, 9);
        Assert.Equal(stamp.Y + (stamp.Height / 2), rect.Y + (rect.Height / 2), 9);
    }

    [Fact]
    public void Create_PostmarkWithoutCircleOrStamp_OverlapsWhereTheStampWouldBe()
    {
        // Three bars and no ring: the postmark is the whole picture, laid against the stamp a
        // 440 × 220 envelope would have. That stamp is 3:4, 0.36 × 220 tall, inset 0.13 × 220.
        var root = XDocument.Parse(new MailPieceGenerator().Create(Envelope, postmark: new PostmarkPiece(Bars())
        {
            Options = PostmarkPiece.DefaultOptions with { Mode = ExtractionMode.Threshold },
        })).Root!;
        var postmark = Piece(root, "mail-postmark");
        var (stampHeight, stampY) = (0.36 * 220, 0.13 * 220);
        var stampX = 440 - stampY - (0.75 * stampHeight);
        var (x, y, w, h) = (Number(postmark, "x"), Number(postmark, "y"), Number(postmark, "width"), Number(postmark, "height"));

        Assert.Equal(["mail-envelope", "mail-postmark"], root.Elements(Svg + "svg").Select(e => (string?)e.Attribute("class")));
        Assert.Empty(postmark.Descendants(Svg + "circle"));
        Assert.Equal(0.8 * stampHeight, h, 1);
        Assert.Equal(w / 4, x + w - stampX, 1);
        Assert.Equal(stampY + (stampHeight / 2), y + (h / 2), 1);
    }

    [Fact]
    public void Create_WidePostmarkWithoutCircle_StaysOnTheEnvelope()
    {
        // A 1000 × 100 picture at 0.8 of the default envelope's 198 px stamp height would be at
        // least 1584 wide, from x ≤ −308 to x ≥ 1276. It is narrowed to the room right of the
        // stamp's left edge (x = 880), 220, which takes its quarter: 880 wide, from 220 to 1100.
        var root = XDocument.Parse(new MailPieceGenerator().Create(new EnvelopeOptions(), postmark: new PostmarkPiece(Bars(1000, 100))
        {
            Options = PostmarkPiece.DefaultOptions with { Mode = ExtractionMode.Threshold },
        })).Root!;
        var postmark = Piece(root, "mail-postmark");
        var (x, w) = (Number(postmark, "x"), Number(postmark, "width"));
        const double stampX = 1100 - 71.5 - 148.5; // a 3:4 stamp, 0.36 × 550 tall, inset 0.13 × 550

        Assert.True(Number(postmark, "height") < 0.8 * 198);
        Assert.InRange(x, 0, 1100);
        Assert.InRange(x + w, 0, 1100.01);
        Assert.Equal(w / 4, x + w - stampX, 1);
    }

    // Three black bars on white, too thin to be taken for a frame.
    private static MemoryStream Bars(int width = 120, int height = 80)
    {
        using var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, x >= 10 && x < width - 10 && y is >= 15 and < 65 && (y - 15) % 20 < 10 ? SKColors.Black : SKColors.White);
            }
        }

        var png = new MemoryStream();
        bitmap.Encode(png, SKEncodedImageFormat.Png, 100);
        png.Position = 0;
        return png;
    }

    private sealed class ExtentComparer : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) a, (double, double) b) => Math.Abs(a.Item1 - b.Item1) < 1e-3 && Math.Abs(a.Item2 - b.Item2) < 1e-3;

        public int GetHashCode((double, double) obj) => 0;
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
