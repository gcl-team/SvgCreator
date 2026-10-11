namespace SvgCreator;

/// <summary>How the ink is rendered in the SVG.</summary>
public enum OutputStyle
{
    /// <summary>Crisp, flat ink.</summary>
    Clean,

    /// <summary>
    /// A rubber-stamp impression: a native SVG filter roughens the edges and wears the ink at render
    /// time. The stored geometry is unchanged, so removing the filter restores the clean drawing.
    /// </summary>
    Stamp,

    /// <summary>
    /// A postal cancellation: the <see cref="Stamp"/> impression plus wavy cancellation lines over
    /// the right side of the drawing's circular frame. Without a circle, a rectangular border around
    /// the canvas makes the whole picture the stamp. The frame and lines are generated, not traced, so
    /// they sit in their own group; the canvas widens slightly to fit the lines' overhang.
    /// </summary>
    Postmark,
}
