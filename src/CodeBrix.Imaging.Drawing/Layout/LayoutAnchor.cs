using System;

namespace CodeBrix.Imaging.Drawing.Layout;

/// <summary>
/// A point of a rectangle given as fractions of its width and height: (0, 0) is the top-left
/// corner, (0.5, 0.5) the centre, (1, 1) the bottom-right corner. The nine usual points are
/// provided (<see cref="TopLeft"/> ... <see cref="BottomRight"/>); fractions outside 0..1 lie
/// outside the rectangle.
/// </summary>
public readonly struct LayoutAnchor : IEquatable<LayoutAnchor>
{
    /// <summary>The top-left corner.</summary>
    public static readonly LayoutAnchor TopLeft = new LayoutAnchor(0f, 0f);

    /// <summary>The middle of the top edge.</summary>
    public static readonly LayoutAnchor TopCenter = new LayoutAnchor(0.5f, 0f);

    /// <summary>The top-right corner.</summary>
    public static readonly LayoutAnchor TopRight = new LayoutAnchor(1f, 0f);

    /// <summary>The middle of the left edge.</summary>
    public static readonly LayoutAnchor CenterLeft = new LayoutAnchor(0f, 0.5f);

    /// <summary>The centre.</summary>
    public static readonly LayoutAnchor Center = new LayoutAnchor(0.5f, 0.5f);

    /// <summary>The middle of the right edge.</summary>
    public static readonly LayoutAnchor CenterRight = new LayoutAnchor(1f, 0.5f);

    /// <summary>The bottom-left corner.</summary>
    public static readonly LayoutAnchor BottomLeft = new LayoutAnchor(0f, 1f);

    /// <summary>The middle of the bottom edge.</summary>
    public static readonly LayoutAnchor BottomCenter = new LayoutAnchor(0.5f, 1f);

    /// <summary>The bottom-right corner.</summary>
    public static readonly LayoutAnchor BottomRight = new LayoutAnchor(1f, 1f);

    /// <summary>
    /// Creates an anchor.
    /// </summary>
    /// <param name="x">The fraction of the width from the left edge.</param>
    /// <param name="y">The fraction of the height from the top edge.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a fraction is not finite.</exception>
    public LayoutAnchor(float x, float y)
    {
        if (!Single.IsFinite(x)) { throw new ArgumentOutOfRangeException(nameof(x)); }
        if (!Single.IsFinite(y)) { throw new ArgumentOutOfRangeException(nameof(y)); }
        X = x;
        Y = y;
    }

    /// <summary>The fraction of the width from the left edge.</summary>
    public float X { get; }

    /// <summary>The fraction of the height from the top edge.</summary>
    public float Y { get; }

    /// <summary>The anchor's point in a rectangle.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The point.</returns>
    public PointF PointIn(RectangleF rectangle)
        => new PointF(rectangle.X + (X * rectangle.Width), rectangle.Y + (Y * rectangle.Height));

    /// <inheritdoc />
    public bool Equals(LayoutAnchor other) => X == other.X && Y == other.Y;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is LayoutAnchor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>Compares two anchors by value.</summary>
    /// <param name="left">The first anchor.</param>
    /// <param name="right">The second anchor.</param>
    /// <returns><c>true</c> when they are the same point.</returns>
    public static bool operator ==(LayoutAnchor left, LayoutAnchor right) => left.Equals(right);

    /// <summary>Compares two anchors by value.</summary>
    /// <param name="left">The first anchor.</param>
    /// <param name="right">The second anchor.</param>
    /// <returns><c>true</c> when they differ.</returns>
    public static bool operator !=(LayoutAnchor left, LayoutAnchor right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y})";
}
