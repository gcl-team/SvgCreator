namespace SvgCreator.Svg;

/// <summary>
/// How a traced drawing is rendered: an optional ink filter plus decorations drawn over it, in
/// order. Looks combine these layers rather than each being written separately; an
/// <see cref="OutputStyle"/> is a named combination of them.
/// </summary>
internal sealed record SvgStyle(IInkFilter? Filter, IReadOnlyList<IDecoration> Decorations)
{
    /// <summary>Crisp, flat ink.</summary>
    public static SvgStyle Clean { get; } = new(null, []);

    /// <summary>The layers that make up <paramref name="style"/> for this drawing.</summary>
    public static SvgStyle For(OutputStyle style, TracedDrawing drawing, int stampSeed, double stampScale) => style switch
    {
        OutputStyle.Clean => Clean,
        OutputStyle.Stamp => new(new StampFilter(stampSeed, stampScale), []),
        OutputStyle.Postmark => new(new StampFilter(stampSeed, stampScale), [PostmarkCancellation.For(drawing)]),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown output style."),
    };
}
