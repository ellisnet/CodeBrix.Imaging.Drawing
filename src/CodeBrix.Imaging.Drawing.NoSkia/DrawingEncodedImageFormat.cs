namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The encoded image formats that <c>DrawingImage.Encode</c> can produce, API-compatible with
/// the SkiaSharp <c>SKEncodedImageFormat</c> enumeration (reduced to the formats this
/// managed implementation encodes through CodeBrix.Imaging).
/// </summary>
public enum DrawingEncodedImageFormat
{
    /// <summary>The PNG format (lossless, with alpha).</summary>
    Png = 0,

    /// <summary>The JPEG format (lossy, no alpha).</summary>
    Jpeg = 1,

    /// <summary>The BMP format (lossless, uncompressed).</summary>
    Bmp = 2,

    /// <summary>The GIF format (lossless, palettized).</summary>
    Gif = 3,

    /// <summary>The WebP format.</summary>
    Webp = 4,
}
