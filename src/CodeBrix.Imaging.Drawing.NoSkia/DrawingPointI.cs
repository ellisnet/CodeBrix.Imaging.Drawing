using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// An integer (x, y) position, API-compatible with the SkiaSharp <c>SKPointI</c> type.
/// </summary>
public struct DrawingPointI : IEquatable<DrawingPointI>
{
    /// <summary>An empty point (0, 0).</summary>
    public static readonly DrawingPointI Empty = new DrawingPointI(0, 0);

    /// <summary>The horizontal position.</summary>
    public int X { get; set; }

    /// <summary>The vertical position.</summary>
    public int Y { get; set; }

    /// <summary>
    /// Creates a point at the given position.
    /// </summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    public DrawingPointI(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Indicates whether both coordinates are zero.</summary>
    public readonly bool IsEmpty => X == 0 && Y == 0;

    /// <summary>
    /// Converts an integer point to a floating-point <see cref="DrawingPoint"/>.
    /// </summary>
    /// <param name="point">The point to convert.</param>
    public static implicit operator DrawingPoint(DrawingPointI point) => new DrawingPoint(point.X, point.Y);

    /// <summary>Compares two points for equality.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><c>true</c> when both coordinates are equal.</returns>
    public static bool operator ==(DrawingPointI left, DrawingPointI right) => left.X == right.X && left.Y == right.Y;

    /// <summary>Compares two points for inequality.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><c>true</c> when either coordinate differs.</returns>
    public static bool operator !=(DrawingPointI left, DrawingPointI right) => !(left == right);

    /// <inheritdoc />
    public readonly bool Equals(DrawingPointI other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingPointI other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc />
    public override readonly string ToString() => $"{{X={X}, Y={Y}}}";
}
