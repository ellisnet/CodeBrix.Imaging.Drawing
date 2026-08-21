using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Describes the pixel dimensions and format of a bitmap or surface, API-compatible with
/// the SkiaSharp <c>SKImageInfo</c> type.
/// </summary>
public struct DrawingImageInfo : IEquatable<DrawingImageInfo>
{
    /// <summary>An empty image info (zero size, unknown formats).</summary>
    public static readonly DrawingImageInfo Empty = default;

    /// <summary>The width, in pixels.</summary>
    public int Width { get; set; }

    /// <summary>The height, in pixels.</summary>
    public int Height { get; set; }

    /// <summary>The pixel color layout.</summary>
    public DrawingColorType ColorType { get; set; }

    /// <summary>The alpha interpretation.</summary>
    public DrawingAlphaType AlphaType { get; set; }

    /// <summary>
    /// Creates an image info with the default color type (<see cref="DrawingColorType.Rgba8888"/>)
    /// and premultiplied alpha.
    /// </summary>
    /// <param name="width">The width, in pixels.</param>
    /// <param name="height">The height, in pixels.</param>
    public DrawingImageInfo(int width, int height)
        : this(width, height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul)
    {
    }

    /// <summary>
    /// Creates an image info with an explicit color and alpha type.
    /// </summary>
    /// <param name="width">The width, in pixels.</param>
    /// <param name="height">The height, in pixels.</param>
    /// <param name="colorType">The pixel color layout.</param>
    /// <param name="alphaType">The alpha interpretation.</param>
    public DrawingImageInfo(int width, int height, DrawingColorType colorType, DrawingAlphaType alphaType)
    {
        Width = width;
        Height = height;
        ColorType = colorType;
        AlphaType = alphaType;
    }

    /// <summary>The number of bytes per pixel (always 4 for the supported color types).</summary>
    public readonly int BytesPerPixel => 4;

    /// <summary>The number of bytes in one row of pixels.</summary>
    public readonly int RowBytes => Width * BytesPerPixel;

    /// <summary>The total number of bytes needed for the pixel buffer.</summary>
    public readonly int BytesSize => RowBytes * Height;

    /// <summary>The size (width and height) as an <see cref="DrawingSizeI"/>.</summary>
    public readonly DrawingSizeI Size => new DrawingSizeI(Width, Height);

    /// <summary>Indicates whether the pixel dimensions describe no pixels.</summary>
    public readonly bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Compares two image infos for equality.</summary>
    /// <param name="left">The first image info.</param>
    /// <param name="right">The second image info.</param>
    /// <returns><c>true</c> when the dimensions and formats are all equal.</returns>
    public static bool operator ==(DrawingImageInfo left, DrawingImageInfo right)
        => left.Width == right.Width && left.Height == right.Height
        && left.ColorType == right.ColorType && left.AlphaType == right.AlphaType;

    /// <summary>Compares two image infos for inequality.</summary>
    /// <param name="left">The first image info.</param>
    /// <param name="right">The second image info.</param>
    /// <returns><c>true</c> when any dimension or format differs.</returns>
    public static bool operator !=(DrawingImageInfo left, DrawingImageInfo right) => !(left == right);

    /// <inheritdoc />
    public readonly bool Equals(DrawingImageInfo other) => this == other;

    /// <inheritdoc />
    public override readonly bool Equals(object obj) => obj is DrawingImageInfo other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Width, Height, ColorType, AlphaType);

    /// <inheritdoc />
    public override readonly string ToString()
        => $"{{Width={Width}, Height={Height}, ColorType={ColorType}, AlphaType={AlphaType}}}";
}
