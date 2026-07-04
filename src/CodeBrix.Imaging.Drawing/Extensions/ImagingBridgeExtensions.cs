using System;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Extensions;

/// <summary>
/// Extension methods that bridge SkiaSharp image types to CodeBrix.Imaging
/// <see cref="Image{TPixel}"/> instances, so a drawing produced by this library can flow
/// into any CodeBrix.Imaging processing pipeline (resize, crop, format conversion, etc.).
/// </summary>
public static class ImagingBridgeExtensions
{
    /// <summary>
    /// Converts a SkiaSharp image to a CodeBrix.Imaging <see cref="Image{TPixel}"/> with
    /// <see cref="Rgba32"/> pixels, using CodeBrix.Imaging's SIMD-optimized BGRA ingestion path.
    /// </summary>
    /// <param name="image">The SkiaSharp image to convert.</param>
    /// <returns>A new <see cref="Image{TPixel}"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the image's pixels cannot be read.</exception>
    public static Image<Rgba32> ToImagingImage(this SKImage image)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }

        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bgraBitmap = new SKBitmap(info);

        if (!image.ReadPixels(info, bgraBitmap.GetPixels(), info.RowBytes, 0, 0))
        {
            throw new InvalidOperationException("The pixels of the SkiaSharp image could not be read.");
        }

        return Image.LoadPixelDataFromBgra(bgraBitmap.Bytes, image.Width, image.Height, PngFormat.Instance);
    }

    /// <summary>
    /// Converts a SkiaSharp bitmap to a CodeBrix.Imaging <see cref="Image{TPixel}"/> with
    /// <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <param name="bitmap">The SkiaSharp bitmap to convert.</param>
    /// <returns>A new <see cref="Image{TPixel}"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the bitmap's pixels cannot be read.</exception>
    public static Image<Rgba32> ToImagingImage(this SKBitmap bitmap)
    {
        if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }

        using SKImage image = SKImage.FromBitmap(bitmap);
        return image.ToImagingImage();
    }

    /// <summary>
    /// Renders the completed drawing of a <see cref="DrawingSession"/> directly to a
    /// CodeBrix.Imaging <see cref="Image{TPixel}"/> with <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <param name="session">The drawing session to export.</param>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>A new <see cref="Image{TPixel}"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    public static Image<Rgba32> ExportImagingImage(this DrawingSession session, SKSizeI outputSize, bool includeBackground = true)
    {
        if (session == null) { throw new ArgumentNullException(nameof(session)); }

        using SKImage image = session.ExportImage(outputSize, includeBackground);
        return image.ToImagingImage();
    }

    /// <summary>
    /// Renders the completed drawing of a <see cref="DrawingSession"/> directly to a
    /// CodeBrix.Imaging <see cref="Image{TPixel}"/> with <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <param name="session">The drawing session to export.</param>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>A new <see cref="Image{TPixel}"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    public static Image<Rgba32> ExportImagingImage(this DrawingSession session, Size outputSize, bool includeBackground = true)
    {
        if (session == null) { throw new ArgumentNullException(nameof(session)); }

        using SKImage image = session.ExportImage(outputSize, includeBackground);
        return image.ToImagingImage();
    }
}
