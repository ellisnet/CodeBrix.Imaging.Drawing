namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The mipmap behavior requested when sampling a bitmap, API-compatible with the SkiaSharp
/// <c>DrawingMipmapMode</c> enumeration. This managed implementation accepts the value for API
/// compatibility but does not build mipmap chains; sampling quality follows the
/// <see cref="DrawingFilterMode"/> alone.
/// </summary>
public enum DrawingMipmapMode
{
    /// <summary>Do not use mipmaps.</summary>
    None = 0,

    /// <summary>Sample from the nearest mipmap level.</summary>
    Nearest = 1,

    /// <summary>Interpolate between mipmap levels.</summary>
    Linear = 2,
}
