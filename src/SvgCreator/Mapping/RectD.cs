using SvgCreator.Fitting;

namespace SvgCreator.Mapping;

/// <summary>Axis-aligned floating-point rectangle, y down.</summary>
internal readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public PointD Center => new(X + (Width / 2), Y + (Height / 2));

    /// <summary>The rectangle shrunk by <paramref name="amount"/> on every side (never below zero size).</summary>
    public RectD Inset(double amount)
    {
        var dx = Math.Min(amount, Width / 2);
        var dy = Math.Min(amount, Height / 2);
        return new RectD(X + dx, Y + dy, Width - (2 * dx), Height - (2 * dy));
    }

    /// <summary>The rectangle grown by <paramref name="amount"/> on every side.</summary>
    public RectD Inflate(double amount) => new(X - amount, Y - amount, Width + (2 * amount), Height + (2 * amount));

    /// <summary>
    /// The rectangle moved the least distance that puts it inside <paramref name="outer"/>; along
    /// an axis where it is too big to fit, it lines up with <paramref name="outer"/>'s top or left.
    /// </summary>
    public RectD MoveInside(RectD outer) => this with
    {
        X = Math.Clamp(X, outer.X, Math.Max(outer.X, outer.Right - Width)),
        Y = Math.Clamp(Y, outer.Y, Math.Max(outer.Y, outer.Bottom - Height)),
    };

    public bool Contains(RectD other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>Area of the overlap with <paramref name="other"/>; zero when they only touch or are apart.</summary>
    public double OverlapArea(RectD other)
    {
        var w = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        var h = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return w > 0 && h > 0 ? w * h : 0;
    }
}
