using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A floating-point rectangle stored as left/top/right/bottom edges.
/// </summary>
public struct DrawingRect : IEquatable<DrawingRect>
{
    /// <summary>An empty rectangle (all edges zero).</summary>
    public static readonly DrawingRect Empty = new DrawingRect(0, 0, 0, 0);

    /// <summary>The left edge.</summary>
    public float Left { get; set; }

    /// <summary>The top edge.</summary>
    public float Top { get; set; }

    /// <summary>The right edge.</summary>
    public float Right { get; set; }

    /// <summary>The bottom edge.</summary>
    public float Bottom { get; set; }

    /// <summary>
    /// Creates a rectangle from its four edges.
    /// </summary>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    public DrawingRect(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>The width of the rectangle (right minus left).</summary>
    public readonly float Width => Right - Left;

    /// <summary>The height of the rectangle (bottom minus top).</summary>
    public readonly float Height => Bottom - Top;

    /// <summary>The horizontal center of the rectangle.</summary>
    public readonly float MidX => Left + (Width / 2f);

    /// <summary>The vertical center of the rectangle.</summary>
    public readonly float MidY => Top + (Height / 2f);

    /// <summary>
    /// Indicates whether the rectangle is empty - a rectangle is empty when its width or
    /// height is zero or negative.
    /// </summary>
    public readonly bool IsEmpty => Right <= Left || Bottom <= Top;

    /// <summary>
    /// Creates a rectangle from a position and size.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The rectangle spanning the given area.</returns>
    public static DrawingRect Create(float x, float y, float width, float height)
        => new DrawingRect(x, y, x + width, y + height);

    /// <summary>
    /// Creates a rectangle at the origin with the given size.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The rectangle spanning (0, 0) to (width, height).</returns>
    public static DrawingRect Create(float width, float height) => new DrawingRect(0, 0, width, height);

    /// <summary>
    /// Determines whether the given point lies inside this rectangle (left/top inclusive,
    /// right/bottom exclusive).
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c> when the point is inside the rectangle.</returns>
    public readonly bool Contains(DrawingPoint point)
        => point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    /// <summary>
    /// Determines whether the given position lies inside this rectangle (left/top
    /// inclusive, right/bottom exclusive).
    /// </summary>
    /// <param name="x">The horizontal position to test.</param>
    /// <param name="y">The vertical position to test.</param>
    /// <returns><c>true</c> when the position is inside the rectangle.</returns>
    public readonly bool Contains(float x, float y)
        => x >= Left && x < Right && y >= Top && y < Bottom;

    /// <summary>Compares two rectangles for equality.</summary>
    /// <param name="left">The first rectangle.</param>
    /// <param name="right">The second rectangle.</param>
    /// <returns><c>true</c> when all four edges are equal.</returns>
    public static bool operator ==(DrawingRect left, DrawingRect right)
        => left.Left == right.Left && left.Top == right.Top
        && left.Right == right.Right && left.Bottom == right.Bottom;

    /// <summary>Compares two rectangles for inequality.</summary>
    /// <param name="left">The first rectangle.</param>
    /// <param name="right">The second rectangle.</param>
    /// <returns><c>true</c> when any edge differs.</returns>
    public static bool operator !=(DrawingRect left, DrawingRect right) => !(left == right);

    /// <inheritdoc />
    public readonly bool Equals(DrawingRect other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingRect other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    /// <inheritdoc />
    public override readonly string ToString() => $"{{Left={Left}, Top={Top}, Right={Right}, Bottom={Bottom}}}";
}
