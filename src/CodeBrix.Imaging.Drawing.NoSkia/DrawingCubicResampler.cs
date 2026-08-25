using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A cubic resampling kernel described by its B and C parameters.
/// <c>DrawingBitmap.ScalePixels</c> maps the well-known
/// kernels onto the matching CodeBrix.Imaging resampler.
/// </summary>
public struct DrawingCubicResampler : IEquatable<DrawingCubicResampler>
{
    /// <summary>The Mitchell-Netravali kernel (B = 1/3, C = 1/3).</summary>
    public static readonly DrawingCubicResampler Mitchell = new DrawingCubicResampler(1 / 3f, 1 / 3f);

    /// <summary>The Catmull-Rom kernel (B = 0, C = 1/2).</summary>
    public static readonly DrawingCubicResampler CatmullRom = new DrawingCubicResampler(0f, 1 / 2f);

    /// <summary>The kernel's B parameter.</summary>
    public float B { get; }

    /// <summary>The kernel's C parameter.</summary>
    public float C { get; }

    /// <summary>
    /// Creates a cubic resampler with the given kernel parameters.
    /// </summary>
    /// <param name="b">The kernel's B parameter.</param>
    /// <param name="c">The kernel's C parameter.</param>
    public DrawingCubicResampler(float b, float c)
    {
        B = b;
        C = c;
    }

    /// <summary>Compares two resamplers for equality.</summary>
    /// <param name="left">The first resampler.</param>
    /// <param name="right">The second resampler.</param>
    /// <returns><c>true</c> when both kernel parameters are equal.</returns>
    public static bool operator ==(DrawingCubicResampler left, DrawingCubicResampler right)
        => left.B == right.B && left.C == right.C;

    /// <summary>Compares two resamplers for inequality.</summary>
    /// <param name="left">The first resampler.</param>
    /// <param name="right">The second resampler.</param>
    /// <returns><c>true</c> when either kernel parameter differs.</returns>
    public static bool operator !=(DrawingCubicResampler left, DrawingCubicResampler right) => !(left == right);

    /// <inheritdoc />
    public readonly bool Equals(DrawingCubicResampler other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingCubicResampler other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(B, C);

    /// <inheritdoc />
    public override readonly string ToString() => $"{{B={B}, C={C}}}";
}
