using System;
using System.Runtime.InteropServices;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.Imaging.Processing;
using CodeBrix.Imaging.Processing.Processors.Transforms;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A mutable 32-bit raster pixel buffer. Pixels are stored in managed memory with straight
/// (unpremultiplied) alpha, in the
/// byte order declared by the bitmap's <see cref="DrawingColorType"/>
/// (<see cref="DrawingColorType.Rgba8888"/> or <see cref="DrawingColorType.Bgra8888"/>).
/// <see cref="GetPixels"/> pins the buffer so callers can fill it with
/// <see cref="Marshal"/> operations.
/// </summary>
public sealed class DrawingBitmap : IDisposable
{
    private readonly byte[] _pixels;
    private GCHandle _pinHandle;

    /// <summary>
    /// Creates a bitmap with the default format (<see cref="DrawingColorType.Rgba8888"/>,
    /// premultiplied-alpha declaration), cleared to transparent.
    /// </summary>
    /// <param name="width">The width, in pixels; must be positive.</param>
    /// <param name="height">The height, in pixels; must be positive.</param>
    public DrawingBitmap(int width, int height)
        : this(new DrawingImageInfo(width, height))
    {
    }

    /// <summary>
    /// Creates a bitmap with an explicit color and alpha type, cleared to transparent.
    /// </summary>
    /// <param name="width">The width, in pixels; must be positive.</param>
    /// <param name="height">The height, in pixels; must be positive.</param>
    /// <param name="colorType">The pixel byte order.</param>
    /// <param name="alphaType">The declared alpha interpretation.</param>
    public DrawingBitmap(int width, int height, DrawingColorType colorType, DrawingAlphaType alphaType)
        : this(new DrawingImageInfo(width, height, colorType, alphaType))
    {
    }

    /// <summary>
    /// Creates a bitmap described by the given image info, cleared to transparent.
    /// </summary>
    /// <param name="info">The bitmap's dimensions and format.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when the color type is not a supported 32-bit layout.</exception>
    public DrawingBitmap(DrawingImageInfo info)
    {
        if (info.Width < 1) { throw new ArgumentOutOfRangeException(nameof(info), "The bitmap width must be positive."); }
        if (info.Height < 1) { throw new ArgumentOutOfRangeException(nameof(info), "The bitmap height must be positive."); }
        if (info.ColorType == DrawingColorType.Unknown)
        {
            info.ColorType = DrawingColorType.Rgba8888;
        }
        if (info.ColorType != DrawingColorType.Rgba8888 && info.ColorType != DrawingColorType.Bgra8888)
        {
            throw new ArgumentException("Only the Rgba8888 and Bgra8888 color types are supported.", nameof(info));
        }

        Info = info;
        _pixels = new byte[info.BytesSize];
    }

    /// <summary>The bitmap's dimensions and format.</summary>
    public DrawingImageInfo Info { get; }

    /// <summary>The width, in pixels.</summary>
    public int Width => Info.Width;

    /// <summary>The height, in pixels.</summary>
    public int Height => Info.Height;

    /// <summary>The pixel byte order.</summary>
    public DrawingColorType ColorType => Info.ColorType;

    /// <summary>The declared alpha interpretation.</summary>
    public DrawingAlphaType AlphaType => Info.AlphaType;

    /// <summary>The number of bytes in one row of pixels.</summary>
    public int RowBytes => Info.RowBytes;

    /// <summary>
    /// A copy of the bitmap's pixel bytes, in the declared <see cref="ColorType"/> byte order.
    /// </summary>
    public byte[] Bytes => (byte[])_pixels.Clone();

    /// <summary>Indicates whether this bitmap has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// The bitmap's backing pixel array (not a copy), in the declared color-type byte
    /// order - for the rendering internals.
    /// </summary>
    internal byte[] PixelBuffer => _pixels;

    /// <summary>
    /// Indicates whether the backing pixels are stored blue-first
    /// (<see cref="DrawingColorType.Bgra8888"/>).
    /// </summary>
    internal bool IsBgra => Info.ColorType == DrawingColorType.Bgra8888;

    /// <summary>
    /// Pins the pixel buffer and returns its address, so raw pixel data can be copied in or
    /// out with <see cref="Marshal"/> operations. The buffer stays pinned until the bitmap
    /// is disposed.
    /// </summary>
    /// <returns>The address of the first pixel.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the bitmap has been disposed.</exception>
    public IntPtr GetPixels()
    {
        ThrowIfDisposed();
        if (!_pinHandle.IsAllocated)
        {
            _pinHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
        }
        return _pinHandle.AddrOfPinnedObject();
    }

    /// <summary>
    /// Reads the color of one pixel.
    /// </summary>
    /// <param name="x">The pixel's horizontal position.</param>
    /// <param name="y">The pixel's vertical position.</param>
    /// <returns>The pixel's color; a transparent empty color when the position is out of range.</returns>
    public DrawingColor GetPixel(int x, int y)
    {
        if (IsDisposed || x < 0 || y < 0 || x >= Width || y >= Height) { return DrawingColor.Empty; }

        int offset = (y * RowBytes) + (x * 4);
        byte c0 = _pixels[offset];
        byte c1 = _pixels[offset + 1];
        byte c2 = _pixels[offset + 2];
        byte a = _pixels[offset + 3];
        return IsBgra ? new DrawingColor(c2, c1, c0, a) : new DrawingColor(c0, c1, c2, a);
    }

    /// <summary>
    /// Sets the color of one pixel; positions outside the bitmap are ignored.
    /// </summary>
    /// <param name="x">The pixel's horizontal position.</param>
    /// <param name="y">The pixel's vertical position.</param>
    /// <param name="color">The color to store.</param>
    public void SetPixel(int x, int y, DrawingColor color)
    {
        if (IsDisposed || x < 0 || y < 0 || x >= Width || y >= Height) { return; }

        int offset = (y * RowBytes) + (x * 4);
        if (IsBgra)
        {
            _pixels[offset] = color.Blue;
            _pixels[offset + 1] = color.Green;
            _pixels[offset + 2] = color.Red;
        }
        else
        {
            _pixels[offset] = color.Red;
            _pixels[offset + 1] = color.Green;
            _pixels[offset + 2] = color.Blue;
        }
        _pixels[offset + 3] = color.Alpha;
    }

    /// <summary>
    /// Replaces every pixel with the given color (no blending).
    /// </summary>
    /// <param name="color">The color to fill with.</param>
    public void Erase(DrawingColor color)
    {
        ThrowIfDisposed();

        byte b0 = IsBgra ? color.Blue : color.Red;
        byte b1 = color.Green;
        byte b2 = IsBgra ? color.Red : color.Blue;
        byte b3 = color.Alpha;

        for (int offset = 0; offset < _pixels.Length; offset += 4)
        {
            _pixels[offset] = b0;
            _pixels[offset + 1] = b1;
            _pixels[offset + 2] = b2;
            _pixels[offset + 3] = b3;
        }
    }

    /// <summary>
    /// Rescales this bitmap's pixels into the destination bitmap (which keeps its own
    /// size), using the requested sampling. Cubic sampling maps onto the equivalent
    /// CodeBrix.Imaging resampler (Mitchell-Netravali or Catmull-Rom); linear and nearest
    /// filtering map onto triangle and nearest-neighbor resampling.
    /// </summary>
    /// <param name="destination">The bitmap to write the rescaled pixels into.</param>
    /// <param name="sampling">The sampling to use.</param>
    /// <returns><c>true</c> when the pixels were rescaled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    public bool ScalePixels(DrawingBitmap destination, DrawingSamplingOptions sampling)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        ThrowIfDisposed();
        destination.ThrowIfDisposed();

        IResampler resampler;
        if (sampling.UseCubic)
        {
            resampler = sampling.Cubic.Value == DrawingCubicResampler.CatmullRom
                ? KnownResamplers.CatmullRom
                : KnownResamplers.MitchellNetravali;
        }
        else
        {
            resampler = sampling.Filter == DrawingFilterMode.Linear
                ? KnownResamplers.Triangle
                : KnownResamplers.NearestNeighbor;
        }

        using Image<Rgba32> image = ToImagingRgba();
        image.Mutate(x => x.Resize(destination.Width, destination.Height, resampler));
        destination.LoadFromImagingRgba(image);
        return true;
    }

    /// <summary>
    /// Decodes encoded image bytes (PNG, JPEG, BMP, WebP, GIF, TIFF, etc. - any format
    /// CodeBrix.Imaging decodes) into a new <see cref="DrawingColorType.Rgba8888"/> bitmap.
    /// </summary>
    /// <param name="encodedBytes">The encoded image bytes.</param>
    /// <returns>The decoded bitmap; or <c>null</c> when the bytes cannot be decoded.</returns>
    public static DrawingBitmap Decode(byte[] encodedBytes)
    {
        if (encodedBytes == null || encodedBytes.Length == 0) { return null; }

        try
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(encodedBytes);
            var bitmap = new DrawingBitmap(new DrawingImageInfo(image.Width, image.Height, DrawingColorType.Rgba8888, DrawingAlphaType.Unpremul));
            bitmap.LoadFromImagingRgba(image);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a bitmap holding a copy of an image's pixels.
    /// </summary>
    /// <param name="image">The image to copy.</param>
    /// <returns>A new bitmap with the image's pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    public static DrawingBitmap FromImage(DrawingImage image)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        return image.CopyToBitmap();
    }

    /// <summary>
    /// Creates a deep copy of this bitmap (same format, copied pixels).
    /// </summary>
    /// <returns>A new bitmap with copied pixels.</returns>
    public DrawingBitmap Copy()
    {
        ThrowIfDisposed();
        var copy = new DrawingBitmap(Info);
        Buffer.BlockCopy(_pixels, 0, copy._pixels, 0, _pixels.Length);
        return copy;
    }

    /// <summary>
    /// Copies this bitmap's pixels into a new CodeBrix.Imaging RGBA image (converting from
    /// BGRA byte order when needed).
    /// </summary>
    /// <returns>A new image that the caller must dispose.</returns>
    internal Image<Rgba32> ToImagingRgba()
    {
        if (!IsBgra)
        {
            return Image.LoadPixelData<Rgba32>(_pixels, Width, Height, PngFormat.Instance);
        }

        var rgba = new byte[_pixels.Length];
        for (int offset = 0; offset < _pixels.Length; offset += 4)
        {
            rgba[offset] = _pixels[offset + 2];
            rgba[offset + 1] = _pixels[offset + 1];
            rgba[offset + 2] = _pixels[offset];
            rgba[offset + 3] = _pixels[offset + 3];
        }
        return Image.LoadPixelData<Rgba32>(rgba, Width, Height, PngFormat.Instance);
    }

    /// <summary>
    /// Overwrites this bitmap's pixels from a CodeBrix.Imaging RGBA image of the same size
    /// (converting to BGRA byte order when needed).
    /// </summary>
    /// <param name="image">The image whose pixels are copied in.</param>
    internal void LoadFromImagingRgba(Image<Rgba32> image)
    {
        image.CopyPixelDataTo(_pixels);
        if (!IsBgra) { return; }

        for (int offset = 0; offset < _pixels.Length; offset += 4)
        {
            byte red = _pixels[offset];
            _pixels[offset] = _pixels[offset + 2];
            _pixels[offset + 2] = red;
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) { throw new ObjectDisposedException(nameof(DrawingBitmap)); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;

        if (_pinHandle.IsAllocated)
        {
            _pinHandle.Free();
        }
    }
}
