using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A picture drawn into a destination rectangle: all of an <see cref="ImageSource"/>, or part
/// of it, stretched to fit, sampled nearest or bilinear, optionally tinted. Like every element
/// it takes a transform, an opacity (the element's <see cref="DrawingElement.Opacity"/>), a time
/// window, effects, and may sit in groups; the layer colour is not used. The picture's edges are
/// anti-aliased when the image is turned or placed between pixels.
/// <para>
/// The shape REFERS to its source and never disposes it (see <see cref="ImageSource"/>). The
/// <see cref="Tint"/> may change while the shape is on a layer (bind a colour track with
/// AnimatedProperty.Tint); doing so makes the shape dynamic.
/// </para>
/// </summary>
public sealed class ImageShape : DrawingShape
{
    private SKColor? _tint;

    /// <summary>Creates a picture element.</summary>
    /// <param name="source">The picture.</param>
    /// <param name="destination">Where it is drawn, calibrated units.</param>
    /// <param name="sourceRectangle">The part of the picture to draw, in its pixels; or <c>null</c> (the default) for all of it.</param>
    /// <param name="sampling">How pixels are sampled when scaled or turned; bilinear by default.</param>
    /// <param name="tint">A colour each pixel is multiplied by, channel by channel, alpha included; or <c>null</c> (the default) for none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source rectangle is empty or reaches outside the picture.</exception>
    public ImageShape(ImageSource source, RectangleF destination, Rectangle? sourceRectangle = null,
        ImageSampling sampling = ImageSampling.Linear, Color? tint = null)
        : base(1f, (Color?)null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        if (!Single.IsFinite(destination.X) || !Single.IsFinite(destination.Y)
            || !Single.IsFinite(destination.Width) || !Single.IsFinite(destination.Height))
        {
            throw new ArgumentException("The destination must be finite.", nameof(destination));
        }
        if (sourceRectangle.HasValue)
        {
            Rectangle r = sourceRectangle.Value;
            if (r.Width < 1 || r.Height < 1 || r.X < 0 || r.Y < 0 || r.Right > source.Width || r.Bottom > source.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceRectangle));
            }
        }
        Destination = destination;
        SourceRectangle = sourceRectangle;
        Sampling = sampling;
        _tint = GraphicsInterop.ToGraphics(tint);
    }

    /// <summary>The picture.</summary>
    public ImageSource Source { get; }

    /// <summary>Where the picture is drawn, calibrated units.</summary>
    public RectangleF Destination { get; }

    /// <summary>The part of the picture drawn, in its pixels; <c>null</c> for all of it.</summary>
    public Rectangle? SourceRectangle { get; }

    /// <summary>How pixels are sampled.</summary>
    public ImageSampling Sampling { get; }

    /// <summary>
    /// A colour every pixel is multiplied by, channel by channel and alpha included (white = no
    /// change, a translucent white fades the picture); or <c>null</c> for none.
    /// </summary>
    public Color? Tint
    {
        get => GraphicsInterop.ToImaging(_tint);
        set
        {
            SKColor? tint = GraphicsInterop.ToGraphics(value);
            if (tint == _tint) { return; }
            _tint = tint;
            MarkChanged();
        }
    }

    /// <summary>The destination rectangle.</summary>
    /// <returns>The bounds, calibrated units.</returns>
    public override RectangleF? GetBounds() => Destination;

    /// <summary>The destination rectangle as geometry.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline() => PathGeometry.Rectangle(Destination);

    /// <summary>The area the picture covers: its whole destination rectangle (its own transparency is not looked at); nothing once its source is disposed.</summary>
    /// <param name="tolerance">Not used.</param>
    /// <returns>One geometry; none when the source is disposed.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
        => Source.IsDisposed ? Array.Empty<PathGeometry>() : new[] { GetOutline() };

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        Rectangle? source = SourceRectangle;
        DrawPicture(canvas, Source, Destination, source, Sampling, _tint);
    }

    //Draws a picture (or part of it) into a destination with optional sampling and tint
    internal static void DrawPicture(SKCanvas canvas, ImageSource source, RectangleF destination, Rectangle? part,
        ImageSampling sampling, SKColor? tint)
    {
        SKBitmap bitmap = source.Bitmap;
        if (bitmap == null || destination.Width == 0f || destination.Height == 0f) { return; }
        Rectangle r = part ?? new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var sourceRect = SKRect.Create(r.X, r.Y, r.Width, r.Height);
        var destinationRect = new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom);
        SKSamplingOptions options = sampling == ImageSampling.Nearest
            ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
            : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        using var paint = new SKPaint { IsAntialias = true };
        SKColorFilter filter = null;
        if (tint.HasValue)
        {
            SKColor t = tint.Value;
            filter = SKColorFilter.CreateColorMatrix(new[]
            {
                t.Red / 255f, 0f, 0f, 0f, 0f,
                0f, t.Green / 255f, 0f, 0f, 0f,
                0f, 0f, t.Blue / 255f, 0f, 0f,
                0f, 0f, 0f, t.Alpha / 255f, 0f,
            });
            paint.ColorFilter = filter;
        }
        try
        {
            canvas.DrawBitmap(bitmap, sourceRect, destinationRect, options, paint);
        }
        finally
        {
            (filter as IDisposable)?.Dispose();
        }
    }
}
