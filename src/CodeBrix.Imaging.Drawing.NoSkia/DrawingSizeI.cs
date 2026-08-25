using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// An integer (width, height) size.
/// </summary>
public struct DrawingSizeI : IEquatable<DrawingSizeI>
{
    /// <summary>An empty size (0, 0).</summary>
    public static readonly DrawingSizeI Empty = new DrawingSizeI(0, 0);

    /// <summary>The width.</summary>
    public int Width { get; set; }

    /// <summary>The height.</summary>
    public int Height { get; set; }

    /// <summary>
    /// Creates a size with the given dimensions.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public DrawingSizeI(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>Indicates whether both dimensions are zero.</summary>
    public readonly bool IsEmpty => Width == 0 && Height == 0;

    /// <summary>
    /// Converts an integer size to a floating-point <see cref="DrawingSize"/>.
    /// </summary>
    /// <param name="size">The size to convert.</param>
    public static implicit operator DrawingSize(DrawingSizeI size) => new DrawingSize(size.Width, size.Height);

    /// <summary>Compares two sizes for equality.</summary>
    /// <param name="left">The first size.</param>
    /// <param name="right">The second size.</param>
    /// <returns><c>true</c> when both dimensions are equal.</returns>
    public static bool operator ==(DrawingSizeI left, DrawingSizeI right) => left.Width == right.Width && left.Height == right.Height;

    /// <summary>Compares two sizes for inequality.</summary>
    /// <param name="left">The first size.</param>
    /// <param name="right">The second size.</param>
    /// <returns><c>true</c> when either dimension differs.</returns>
    public static bool operator !=(DrawingSizeI left, DrawingSizeI right) => !(left == right);

    /// <inheritdoc />
    public readonly bool Equals(DrawingSizeI other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingSizeI other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Width, Height);

    /// <inheritdoc />
    public override readonly string ToString() => $"{{Width={Width}, Height={Height}}}";
}
