namespace SvgCreator;

/// <summary>
/// Arranges the picture into a larger design instead of outputting the traced drawing alone.
/// See <see cref="PostageStampLayout"/>.
/// </summary>
public abstract record OutputLayout
{
    private protected OutputLayout()
    {
    }

    internal abstract void Validate();
}

/// <summary>A corner of a <see cref="PostageStampLayout"/>, for placing its text.</summary>
public enum StampCorner
{
    /// <summary>Above the picture, on the left.</summary>
    TopLeft,

    /// <summary>Above the picture, on the right.</summary>
    TopRight,

    /// <summary>Below the picture, on the left.</summary>
    BottomLeft,

    /// <summary>Below the picture, on the right.</summary>
    BottomRight,
}

/// <summary>
/// A postage stamp: the original image embedded unchanged as the central illustration, on paper
/// with a perforated edge, with the country name, the fee and a small caption above or below it.
/// Text is written as native <c>&lt;text&gt;</c> in <see cref="SvgConversionOptions.FillColor"/>,
/// so it stays editable and uses the viewer's serif font. <see cref="SvgConversionOptions.Style"/>
/// still applies: <see cref="OutputStyle.Stamp"/> wears the printing and
/// <see cref="OutputStyle.Postmark"/> adds cancellation lines across the stamp. The image is not
/// traced, so the line-extraction options have no effect.
/// </summary>
public sealed record PostageStampLayout : OutputLayout
{
    /// <summary>The country name, in the largest type.</summary>
    public required string Country { get; init; }

    /// <summary>The fee, such as <c>50c</c> or <c>$1.20</c>; none when <see langword="null"/>.</summary>
    public string? Fee { get; init; }

    /// <summary>A small caption describing the picture; none when <see langword="null"/>.</summary>
    public string? Caption { get; init; }

    /// <summary>Where the country name goes. Default: <see cref="StampCorner.TopLeft"/>.</summary>
    public StampCorner CountryCorner { get; init; } = StampCorner.TopLeft;

    /// <summary>Where the fee goes. Default: <see cref="StampCorner.TopRight"/>.</summary>
    public StampCorner FeeCorner { get; init; } = StampCorner.TopRight;

    /// <summary>Where the caption goes. Default: <see cref="StampCorner.BottomLeft"/>.</summary>
    public StampCorner CaptionCorner { get; init; } = StampCorner.BottomLeft;

    internal override void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Country);
        if (Fee is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Fee);
        }

        if (Caption is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Caption);
        }

        var corners = new HashSet<StampCorner>();
        foreach (var (corner, name) in Placed())
        {
            if (!Enum.IsDefined(corner))
            {
                throw new ArgumentOutOfRangeException(name, corner, "Unknown stamp corner.");
            }

            if (!corners.Add(corner))
            {
                throw new ArgumentException($"Two items of the stamp are placed at {corner}; give each its own corner.", name);
            }
        }
    }

    // The corners of the items that are shown.
    private IEnumerable<(StampCorner Corner, string Name)> Placed()
    {
        yield return (CountryCorner, nameof(CountryCorner));
        if (Fee is not null)
        {
            yield return (FeeCorner, nameof(FeeCorner));
        }

        if (Caption is not null)
        {
            yield return (CaptionCorner, nameof(CaptionCorner));
        }
    }
}
