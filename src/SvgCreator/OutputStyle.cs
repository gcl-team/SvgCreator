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
}
