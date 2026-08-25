using System;
using System.IO;
using System.Runtime.InteropServices;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Formats.Bmp;
using CodeBrix.Imaging.Formats.Gif;
using CodeBrix.Imaging.Formats.Jpeg;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.Formats.Webp;
using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// An immutable raster image, as used for snapshots and encoded exports. Encoding runs
/// through CodeBrix.Imaging's fully
/// managed codecs.
/// </summary>
public sealed class DrawingImage : IDisposable
{
    private readonly DrawingBitmap _bitmap;

    private DrawingImage(DrawingBitmap ownedBitmap)
    {
        _bitmap = ownedBitmap;
    }

    /// <summary>The width, in pixels.</summary>
    public int Width => _bitmap.Width;

    /// <summary>The height, in pixels.</summary>
    public int Height => _bitmap.Height;

    /// <summary>The image's dimensions and format.</summary>
    public DrawingImageInfo Info => _bitmap.Info;

    /// <summary>
    /// Creates an image holding a copy of a bitmap's pixels.
    /// </summary>
    /// <param name="bitmap">The bitmap to copy.</param>
    /// <returns>A new image with the bitmap's pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    public static DrawingImage FromBitmap(DrawingBitmap bitmap)
    {
        if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }
        return new DrawingImage(bitmap.Copy());
    }

    /// <summary>
    /// Encodes the image to the given format.
    /// </summary>
    /// <param name="format">The encoded format to produce.</param>
    /// <param name="quality">The encoder quality, 1-100 (honored by the JPEG and WebP encoders).</param>
    /// <returns>The encoded bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the format is not a supported value.</exception>
    public DrawingData Encode(DrawingEncodedImageFormat format, int quality)
    {
        using Image<Rgba32> image = _bitmap.ToImagingRgba();
        using var stream = new MemoryStream();

        switch (format)
        {
            case DrawingEncodedImageFormat.Png:
                image.Save(stream, new PngEncoder());
                break;
            case DrawingEncodedImageFormat.Jpeg:
                image.Save(stream, new JpegEncoder { Quality = Math.Clamp(quality, 1, 100) });
                break;
            case DrawingEncodedImageFormat.Bmp:
                image.Save(stream, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel32 });
                break;
            case DrawingEncodedImageFormat.Gif:
                image.Save(stream, new GifEncoder());
                break;
            case DrawingEncodedImageFormat.Webp:
                image.Save(stream, new WebpEncoder { Quality = Math.Clamp(quality, 1, 100) });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }

        return new DrawingData(stream.ToArray());
    }

    /// <summary>
    /// Encodes the image as PNG.
    /// </summary>
    /// <returns>The PNG-encoded bytes.</returns>
    public DrawingData Encode() => Encode(DrawingEncodedImageFormat.Png, 100);

    /// <summary>
    /// Copies a region of the image's pixels into caller-supplied memory, converting to the
    /// requested color layout. Straight (unpremultiplied) alpha is written for
    /// <see cref="DrawingAlphaType.Unpremul"/> and <see cref="DrawingAlphaType.Opaque"/> requests;
    /// premultiplied alpha is written for <see cref="DrawingAlphaType.Premul"/>.
    /// </summary>
    /// <param name="dstInfo">The destination's dimensions and format.</param>
    /// <param name="dstPixels">The destination memory address.</param>
    /// <param name="dstRowBytes">The destination's stride, in bytes.</param>
    /// <param name="srcX">The left edge of the source region.</param>
    /// <param name="srcY">The top edge of the source region.</param>
    /// <returns><c>true</c> when the pixels were copied; <c>false</c> when the request is unusable.</returns>
    public bool ReadPixels(DrawingImageInfo dstInfo, IntPtr dstPixels, int dstRowBytes, int srcX, int srcY)
    {
        if (dstPixels == IntPtr.Zero || dstInfo.Width < 1 || dstInfo.Height < 1) { return false; }
        if (dstInfo.ColorType != DrawingColorType.Rgba8888 && dstInfo.ColorType != DrawingColorType.Bgra8888) { return false; }
        if (srcX < 0 || srcY < 0
            || srcX + dstInfo.Width > Width
            || srcY + dstInfo.Height > Height)
        {
            return false;
        }

        byte[] source = _bitmap.PixelBuffer;
        bool sourceBgra = _bitmap.IsBgra;
        bool destBgra = dstInfo.ColorType == DrawingColorType.Bgra8888;
        bool premultiply = dstInfo.AlphaType == DrawingAlphaType.Premul;

        var row = new byte[dstInfo.Width * 4];
        for (int y = 0; y < dstInfo.Height; y++)
        {
            int sourceOffset = (((srcY + y) * Width) + srcX) * 4;
            for (int x = 0; x < dstInfo.Width; x++)
            {
                int src = sourceOffset + (x * 4);
                byte c0 = source[src];
                byte c1 = source[src + 1];
                byte c2 = source[src + 2];
                byte a = source[src + 3];

                byte red = sourceBgra ? c2 : c0;
                byte blue = sourceBgra ? c0 : c2;

                if (premultiply && a != 255)
                {
                    red = (byte)(((red * a) + 127) / 255);
                    c1 = (byte)(((c1 * a) + 127) / 255);
                    blue = (byte)(((blue * a) + 127) / 255);
                }

                int dst = x * 4;
                row[dst] = destBgra ? blue : red;
                row[dst + 1] = c1;
                row[dst + 2] = destBgra ? red : blue;
                row[dst + 3] = a;
            }
            Marshal.Copy(row, 0, dstPixels + (y * dstRowBytes), row.Length);
        }

        return true;
    }

    /// <summary>
    /// Copies the image's pixels into a new bitmap.
    /// </summary>
    /// <returns>A new bitmap with the image's pixels.</returns>
    internal DrawingBitmap CopyToBitmap() => _bitmap.Copy();

    /// <summary>
    /// The image's backing bitmap (not a copy) - for the rendering internals, which only
    /// ever read from it.
    /// </summary>
    internal DrawingBitmap BitmapRef => _bitmap;

    /// <inheritdoc />
    public void Dispose() => _bitmap.Dispose();
}
