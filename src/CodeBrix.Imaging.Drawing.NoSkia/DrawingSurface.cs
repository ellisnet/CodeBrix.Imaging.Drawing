using System;
using System.Runtime.InteropServices;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A drawable raster surface that owns a pixel buffer and the canvas that draws onto it
/// (CPU raster surfaces only - there is no GPU in this fully managed implementation).
/// </summary>
public sealed class DrawingSurface : IDisposable
{
    private readonly DrawingBitmap _bitmap;
    private readonly IntPtr _pixels;
    private readonly int _rowBytes;
    private bool _disposed;

    private DrawingSurface(DrawingBitmap bitmap)
    {
        _bitmap = bitmap;
        Canvas = new DrawingCanvas(bitmap);
    }

    private DrawingSurface(DrawingBitmap bitmap, IntPtr pixels, int rowBytes)
        : this(bitmap)
    {
        _pixels = pixels;
        _rowBytes = rowBytes;
    }

    /// <summary>The canvas that draws onto this surface.</summary>
    public DrawingCanvas Canvas { get; }

    /// <summary>
    /// Creates a raster surface with the given dimensions and format, cleared to transparent.
    /// </summary>
    /// <param name="info">The surface's dimensions and format.</param>
    /// <returns>A new surface that the caller must dispose.</returns>
    public static DrawingSurface Create(DrawingImageInfo info) => new DrawingSurface(new DrawingBitmap(info));

    /// <summary>
    /// Creates a raster surface over pixel memory the caller owns - a window's or texture's locked
    /// pixels, a buffer of an encoder - in the given format, with <paramref name="rowBytes"/> bytes
    /// from one row's start to the next.
    /// <para>
    /// The fully managed engine cannot draw straight into unmanaged memory, so the surface keeps its
    /// own buffer: the memory's current pixels are read in now (drawing starts over what is there),
    /// and the surface's pixels are written back into the memory by <see cref="Flush"/> and by
    /// <see cref="Dispose"/>. Call <see cref="Flush"/> before reading the memory (for example before
    /// presenting a frame). The memory must stay valid, and hold <paramref name="rowBytes"/> x
    /// height bytes, until the surface is disposed; only each row's own bytes (width x bytes per
    /// pixel) are read and written, so padding at row ends is left alone.
    /// </para>
    /// </summary>
    /// <param name="info">The surface's dimensions and format.</param>
    /// <param name="pixels">The address of the first pixel of the first row.</param>
    /// <param name="rowBytes">The bytes from one row's start to the next; at least the row's own bytes.</param>
    /// <returns>A new surface that the caller must dispose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pixels"/> is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rowBytes"/> is smaller than a row of the format.</exception>
    public static DrawingSurface Create(DrawingImageInfo info, IntPtr pixels, int rowBytes)
    {
        if (pixels == IntPtr.Zero) { throw new ArgumentNullException(nameof(pixels)); }
        var bitmap = new DrawingBitmap(info);
        if (rowBytes < bitmap.RowBytes)
        {
            bitmap.Dispose();
            throw new ArgumentOutOfRangeException(nameof(rowBytes), $"A row of this format needs at least {info.RowBytes} bytes.");
        }
        byte[] buffer = bitmap.PixelBuffer;
        int row = bitmap.RowBytes;
        for (int y = 0; y < bitmap.Height; y++)
        {
            Marshal.Copy(IntPtr.Add(pixels, y * rowBytes), buffer, y * row, row);
        }
        return new DrawingSurface(bitmap, pixels, rowBytes);
    }

    /// <summary>
    /// Writes the surface's pixels into the caller's memory, for a surface made over caller memory
    /// (<see cref="Create(DrawingImageInfo, IntPtr, int)"/>); does nothing for a surface that owns its
    /// pixels.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the surface has been disposed.</exception>
    public void Flush()
    {
        if (_disposed) { throw new ObjectDisposedException(nameof(DrawingSurface)); }
        if (_pixels == IntPtr.Zero) { return; }
        byte[] buffer = _bitmap.PixelBuffer;
        int row = _bitmap.RowBytes;
        for (int y = 0; y < _bitmap.Height; y++)
        {
            Marshal.Copy(buffer, y * row, IntPtr.Add(_pixels, y * _rowBytes), row);
        }
    }

    /// <summary>
    /// Captures the surface's current pixels as an immutable image.
    /// </summary>
    /// <returns>A new image that the caller must dispose.</returns>
    public DrawingImage Snapshot() => DrawingImage.FromBitmap(_bitmap);

    /// <summary>Releases the surface; a surface over caller memory writes its pixels back into it first (see <see cref="Flush"/>).</summary>
    public void Dispose()
    {
        if (_disposed) { return; }
        Flush();
        _disposed = true;
        Canvas.Dispose();
        _bitmap.Dispose();
    }
}
