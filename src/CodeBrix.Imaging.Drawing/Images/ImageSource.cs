using System;
using System.IO;
using System.Runtime.InteropServices;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.PixelFormats;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Images;

/// <summary>
/// A picture to draw with an <see cref="CodeBrix.Imaging.Drawing.Shapes.ImageShape"/> (or as the
/// frames of a <see cref="SpriteSheet"/>): a copy of straight-alpha RGBA pixels, made from encoded
/// image bytes (decoded by CodeBrix.Imaging, so both packages hold the same pixels), from raw
/// RGBA or BGRA pixels, from a CodeBrix.Imaging <c>Image&lt;Rgba32&gt;</c> or from a float plane
/// image.
/// <para>
/// OWNERSHIP. Every factory COPIES the pixels: change or dispose what you passed in at once.
/// The source owns its copy and releases it on <see cref="Dispose"/>. Shapes only REFER to a
/// source - they never dispose it - so keep it alive while any shape using it may still be
/// rendered or exported, and dispose it after the last render (a shape whose source is disposed
/// draws nothing). One source can be shared by any number of shapes, layers and sessions,
/// including sessions rendered on other threads (drawing only reads it).
/// </para>
/// </summary>
public sealed class ImageSource : IDisposable
{
    private SKBitmap _bitmap;

    private ImageSource(SKBitmap bitmap)
    {
        _bitmap = bitmap;
        Width = bitmap.Width;
        Height = bitmap.Height;
    }

    /// <summary>The width, in pixels.</summary>
    public int Width { get; }

    /// <summary>The height, in pixels.</summary>
    public int Height { get; }

    /// <summary>The size, in pixels.</summary>
    public Size Size => new Size(Width, Height);

    /// <summary>Indicates whether the source has been disposed (shapes using it then draw nothing).</summary>
    public bool IsDisposed => _bitmap == null;

    //The pixels as the backend draws them; null once disposed
    internal SKBitmap Bitmap => _bitmap;

    /// <summary>Decodes encoded image bytes (PNG, JPEG, BMP, GIF, WebP, ... - whatever CodeBrix.Imaging reads; the first frame).</summary>
    /// <param name="encodedImage">The encoded image.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encodedImage"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the bytes are not a readable image.</exception>
    public static ImageSource FromEncoded(byte[] encodedImage)
    {
        if (encodedImage == null) { throw new ArgumentNullException(nameof(encodedImage)); }
        try
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(encodedImage);
            return FromImagingImage(image);
        }
        catch (Exception exception) when (exception is not ArgumentException)
        {
            throw new ArgumentException("The bytes are not a readable image.", nameof(encodedImage), exception);
        }
    }

    /// <summary>Reads and decodes an image file.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the file is not a readable image.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    public static ImageSource FromFile(string path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        return FromEncoded(File.ReadAllBytes(path));
    }

    /// <summary>Copies straight-alpha RGBA pixels (R, G, B, A per pixel, rows top to bottom, no padding).</summary>
    /// <param name="rgba">The pixels; at least width x height x 4 bytes.</param>
    /// <param name="width">The width, in pixels.</param>
    /// <param name="height">The height, in pixels.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is below 1.</exception>
    /// <exception cref="ArgumentException">Thrown when the buffer is too small.</exception>
    public static ImageSource FromRgba(ReadOnlySpan<byte> rgba, int width, int height)
    {
        int length = CheckBuffer(rgba.Length, width, height, nameof(rgba));
        return new ImageSource(DrawingMask.CreateStraightBitmap(rgba.Slice(0, length).ToArray(), width, height));
    }

    /// <summary>Copies straight-alpha BGRA pixels (B, G, R, A per pixel - the layout cameras and many capture libraries produce).</summary>
    /// <param name="bgra">The pixels; at least width x height x 4 bytes.</param>
    /// <param name="width">The width, in pixels.</param>
    /// <param name="height">The height, in pixels.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is below 1.</exception>
    /// <exception cref="ArgumentException">Thrown when the buffer is too small.</exception>
    public static ImageSource FromBgra(ReadOnlySpan<byte> bgra, int width, int height)
    {
        int length = CheckBuffer(bgra.Length, width, height, nameof(bgra));
        var rgba = new byte[length];
        for (int i = 0; i < length; i += 4)
        {
            rgba[i] = bgra[i + 2];
            rgba[i + 1] = bgra[i + 1];
            rgba[i + 2] = bgra[i];
            rgba[i + 3] = bgra[i + 3];
        }
        return new ImageSource(DrawingMask.CreateStraightBitmap(rgba, width, height));
    }

    /// <summary>Copies a CodeBrix.Imaging image.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    public static ImageSource FromImagingImage(Image<Rgba32> image)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        var rgba = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(rgba);
        return new ImageSource(DrawingMask.CreateStraightBitmap(rgba, image.Width, image.Height));
    }

    /// <summary>Copies a float plane image, converted with the planes' float-to-byte rule.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    public static ImageSource FromPlaneImage(DrawingPlaneImage image)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        return new ImageSource(DrawingMask.CreateStraightBitmap(image.ToRgba(), image.Width, image.Height));
    }

#if NOSKIA
    /// <summary>Copies a backend bitmap's pixels (read as straight alpha).</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    public static ImageSource FromDrawingBitmap(DrawingBitmap bitmap)
#else
    /// <summary>Copies a SkiaSharp bitmap's pixels (read as straight alpha).</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is null.</exception>
    public static ImageSource FromSkiaBitmap(SKBitmap bitmap)
#endif
    {
        if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }
        return new ImageSource(DrawingMask.CreateStraightBitmap(EffectProcessor.ReadStraight(bitmap), bitmap.Width, bitmap.Height));
    }

    /// <summary>A new source holding a copy of part of this one (sprite sheets use it to cut their cells).</summary>
    /// <param name="area">The part, in pixels; it must lie inside the image.</param>
    /// <returns>The new source (dispose it separately).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this source has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the area is empty or reaches outside the image.</exception>
    public ImageSource Crop(Rectangle area)
    {
        ThrowIfDisposed();
        if (area.Width < 1 || area.Height < 1 || area.X < 0 || area.Y < 0 || area.Right > Width || area.Bottom > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(area));
        }
        byte[] all = _bitmap.Bytes;
        int sourceRow = _bitmap.RowBytes;
        var rgba = new byte[area.Width * area.Height * 4];
        for (int y = 0; y < area.Height; y++)
        {
            Buffer.BlockCopy(all, ((area.Y + y) * sourceRow) + (area.X * 4), rgba, y * area.Width * 4, area.Width * 4);
        }
        return new ImageSource(DrawingMask.CreateStraightBitmap(rgba, area.Width, area.Height));
    }

    /// <summary>Returns a copy of the pixels as straight-alpha RGBA bytes.</summary>
    /// <returns>A new array of width x height x 4 bytes.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the source has been disposed.</exception>
    public byte[] ToRgba()
    {
        ThrowIfDisposed();
        byte[] all = _bitmap.Bytes;
        int rowBytes = _bitmap.RowBytes;
        if (rowBytes == Width * 4) { return all; }
        var rgba = new byte[Width * Height * 4];
        for (int y = 0; y < Height; y++) { Buffer.BlockCopy(all, y * rowBytes, rgba, y * Width * 4, Width * 4); }
        return rgba;
    }

    /// <summary>Releases the pixels. Shapes still using the source draw nothing afterwards.</summary>
    public void Dispose()
    {
        SKBitmap bitmap = _bitmap;
        _bitmap = null;
        bitmap?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_bitmap == null) { throw new ObjectDisposedException(nameof(ImageSource)); }
    }

    private static int CheckBuffer(int available, int width, int height, string name)
    {
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height)); }
        long required = (long)width * height * 4;
        if (available < required)
        {
            throw new ArgumentException($"The buffer holds {available} bytes but a {width} x {height} image needs {required}.", name);
        }
        return (int)required;
    }
}
