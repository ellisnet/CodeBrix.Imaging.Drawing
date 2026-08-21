using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How pixels are sampled when a bitmap is drawn or rescaled, API-compatible with the
/// SkiaSharp <c>SKSamplingOptions</c> type. Cubic sampling is honored by
/// <c>DrawingBitmap.ScalePixels</c> (which rescales through CodeBrix.Imaging's resamplers);
/// bitmap draws through <c>DrawingCanvas</c> treat cubic sampling as linear.
/// </summary>
public struct DrawingSamplingOptions : IEquatable<DrawingSamplingOptions>
{
    /// <summary>The default sampling: nearest-neighbor filtering, no mipmaps.</summary>
    public static readonly DrawingSamplingOptions Default = new DrawingSamplingOptions(DrawingFilterMode.Nearest);

    /// <summary>The filter mode used when sampling.</summary>
    public DrawingFilterMode Filter { get; }

    /// <summary>The mipmap mode used when sampling (informational; no mipmaps are built).</summary>
    public DrawingMipmapMode Mipmap { get; }

    /// <summary>The cubic resampler, when cubic sampling was requested.</summary>
    public DrawingCubicResampler? Cubic { get; }

    /// <summary>Indicates whether cubic sampling was requested.</summary>
    public readonly bool UseCubic => Cubic.HasValue;

    /// <summary>
    /// Creates sampling options with the given filter mode and no mipmaps.
    /// </summary>
    /// <param name="filter">The filter mode.</param>
    public DrawingSamplingOptions(DrawingFilterMode filter)
        : this(filter, DrawingMipmapMode.None)
    {
    }

    /// <summary>
    /// Creates sampling options with the given filter and mipmap modes.
    /// </summary>
    /// <param name="filter">The filter mode.</param>
    /// <param name="mipmap">The mipmap mode.</param>
    public DrawingSamplingOptions(DrawingFilterMode filter, DrawingMipmapMode mipmap)
    {
        Filter = filter;
        Mipmap = mipmap;
        Cubic = null;
    }

    /// <summary>
    /// Creates sampling options that use a cubic resampler.
    /// </summary>
    /// <param name="resampler">The cubic resampler to use.</param>
    public DrawingSamplingOptions(DrawingCubicResampler resampler)
    {
        Filter = DrawingFilterMode.Linear;
        Mipmap = DrawingMipmapMode.None;
        Cubic = resampler;
    }

    /// <summary>Compares two sampling options for equality.</summary>
    /// <param name="left">The first options value.</param>
    /// <param name="right">The second options value.</param>
    /// <returns><c>true</c> when the options are equal.</returns>
    public static bool operator ==(DrawingSamplingOptions left, DrawingSamplingOptions right)
        => left.Filter == right.Filter && left.Mipmap == right.Mipmap
        && Nullable.Equals(left.Cubic, right.Cubic);

    /// <summary>Compares two sampling options for inequality.</summary>
    /// <param name="left">The first options value.</param>
    /// <param name="right">The second options value.</param>
    /// <returns><c>true</c> when the options differ.</returns>
    public static bool operator !=(DrawingSamplingOptions left, DrawingSamplingOptions right) => !(left == right);

    /// <inheritdoc />
    public readonly bool Equals(DrawingSamplingOptions other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingSamplingOptions other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Filter, Mipmap, Cubic);

    /// <inheritdoc />
    public override readonly string ToString() => $"{{Filter={Filter}, Mipmap={Mipmap}, Cubic={Cubic}}}";
}
