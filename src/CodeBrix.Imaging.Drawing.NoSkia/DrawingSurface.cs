using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A drawable raster surface that owns a pixel buffer and the canvas that draws onto it
/// (CPU raster surfaces only - there is no GPU in this fully managed implementation).
/// </summary>
public sealed class DrawingSurface : IDisposable
{
    private readonly DrawingBitmap _bitmap;

    private DrawingSurface(DrawingBitmap bitmap)
    {
        _bitmap = bitmap;
        Canvas = new DrawingCanvas(bitmap);
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
    /// Captures the surface's current pixels as an immutable image.
    /// </summary>
    /// <returns>A new image that the caller must dispose.</returns>
    public DrawingImage Snapshot() => DrawingImage.FromBitmap(_bitmap);

    /// <inheritdoc />
    public void Dispose()
    {
        Canvas.Dispose();
        _bitmap.Dispose();
    }
}
