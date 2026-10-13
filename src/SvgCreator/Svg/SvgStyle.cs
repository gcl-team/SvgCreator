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

    /// <summary>
    /// The layers that make up <paramref name="style"/>. A postmark depends on what it cancels (the
    /// drawing's circular frame, or a postage stamp's paper), so the caller supplies it.
    /// </summary>
    public static SvgStyle For(OutputStyle style, int stampSeed, double stampScale, Func<IDecoration> postmark) => style switch
    {
        OutputStyle.Clean => Clean,
        OutputStyle.Stamp => new(new StampFilter(stampSeed, stampScale), []),
        OutputStyle.Postmark => new(new StampFilter(stampSeed, stampScale), [postmark()]),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown output style."),
    };
}
