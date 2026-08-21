using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A floating-point (x, y) position, API-compatible with the SkiaSharp <c>SKPoint</c> type.
/// </summary>
public struct DrawingPoint : IEquatable<DrawingPoint>
{
    /// <summary>An empty point (0, 0).</summary>
    public static readonly DrawingPoint Empty = new DrawingPoint(0, 0);

    /// <summary>The horizontal position.</summary>
    public float X { get; set; }

    /// <summary>The vertical position.</summary>
    public float Y { get; set; }

    /// <summary>
    /// Creates a point at the given position.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    public DrawingPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Indicates whether both coordinates are zero.</summary>
    public readonly bool IsEmpty => X == 0 && Y == 0;

    /// <summary>Compares two points for equality.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><c>true</c> when both coordinates are equal.</returns>
    public static bool operator ==(DrawingPoint left, DrawingPoint right) => left.X == right.X && left.Y == right.Y;

    /// <summary>Compares two points for inequality.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><c>true</c> when either coordinate differs.</returns>
    public static bool operator !=(DrawingPoint left, DrawingPoint right) => !(left == right);

    /// <summary>Adds the coordinates of two points.</summary>
    /// <param name="point">The first point.</param>
    /// <param name="offset">The offset to add.</param>
    /// <returns>The translated point.</returns>
    public static DrawingPoint operator +(DrawingPoint point, DrawingPoint offset) => new DrawingPoint(point.X + offset.X, point.Y + offset.Y);

    /// <summary>Subtracts the coordinates of one point from another.</summary>
    /// <param name="point">The point to subtract from.</param>
    /// <param name="offset">The offset to subtract.</param>
    /// <returns>The translated point.</returns>
    public static DrawingPoint operator -(DrawingPoint point, DrawingPoint offset) => new DrawingPoint(point.X - offset.X, point.Y - offset.Y);

    /// <inheritdoc />
    public readonly bool Equals(DrawingPoint other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingPoint other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc />
    public override readonly string ToString() => $"{{X={X}, Y={Y}}}";
}
