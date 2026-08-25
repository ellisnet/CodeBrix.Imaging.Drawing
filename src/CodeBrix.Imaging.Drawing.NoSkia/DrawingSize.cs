using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A floating-point (width, height) size.
/// </summary>
public struct DrawingSize : IEquatable<DrawingSize>
{
    /// <summary>An empty size (0, 0).</summary>
    public static readonly DrawingSize Empty = new DrawingSize(0, 0);

    /// <summary>The width.</summary>
    public float Width { get; set; }

    /// <summary>The height.</summary>
    public float Height { get; set; }

    /// <summary>
    /// Creates a size with the given dimensions.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public DrawingSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>Indicates whether both dimensions are zero.</summary>
    public readonly bool IsEmpty => Width == 0 && Height == 0;

    /// <summary>Compares two sizes for equality.</summary>
    /// <param name="left">The first size.</param>
    /// <param name="right">The second size.</param>
    /// <returns><c>true</c> when both dimensions are equal.</returns>
    public static bool operator ==(DrawingSize left, DrawingSize right) => left.Width == right.Width && left.Height == right.Height;

    /// <summary>Compares two sizes for inequality.</summary>
    /// <param name="left">The first size.</param>
    /// <param name="right">The second size.</param>
    /// <returns><c>true</c> when either dimension differs.</returns>
    public static bool operator !=(DrawingSize left, DrawingSize right) => !(left == right);

    /// <inheritdoc />
    public readonly bool Equals(DrawingSize other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingSize other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Width, Height);

    /// <inheritdoc />
    public override readonly string ToString() => $"{{Width={Width}, Height={Height}}}";
}
