using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// An axis-aligned rectangle in calibrated drawing coordinates, with optional rounded
/// corners, drawn as an outline or filled.
/// </summary>
public sealed class RectangleShape : DrawingShape
{
    /// <summary>The horizontal position of the rectangle's left edge, in calibrated drawing units.</summary>
    public float X { get; }

    /// <summary>The vertical position of the rectangle's top edge, in calibrated drawing units.</summary>
    public float Y { get; }

    /// <summary>The rectangle's width, in calibrated drawing units.</summary>
    public float Width { get; }

    /// <summary>The rectangle's height, in calibrated drawing units.</summary>
    public float Height { get; }

    /// <summary>The corner radius, in calibrated drawing units; zero for square corners.</summary>
    public float CornerRadius { get; }

    /// <summary>Indicates whether the rectangle is filled solid rather than drawn as an outline.</summary>
    public bool IsFilled { get; }

    /// <summary>
    /// Creates a rectangle with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    /// <param name="x">The horizontal position of the left edge, in calibrated drawing units.</param>
    /// <param name="y">The vertical position of the top edge, in calibrated drawing units.</param>
    /// <param name="width">The width, in calibrated drawing units; must be positive.</param>
    /// <param name="height">The height, in calibrated drawing units; must be positive.</param>
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units (not used when filled, but it must still be positive).</param>
    /// <param name="color">The rectangle's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="isFilled"><c>true</c> to fill the rectangle solid; <c>false</c> (the default) for an outline.</param>
    /// <param name="cornerRadius">The corner radius, in calibrated drawing units; zero (the default) for square corners.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is zero or negative,
    /// or <paramref name="cornerRadius"/> is negative.
    /// </exception>
    public RectangleShape(float x, float y, float width, float height,
        float strokeThickness = Stroke.DefaultWidth, Color? color = null,
        bool isFilled = false, float cornerRadius = 0f)
        : base(strokeThickness, color)
    {
        if (width <= 0) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height <= 0) { throw new ArgumentOutOfRangeException(nameof(height)); }
        if (cornerRadius < 0) { throw new ArgumentOutOfRangeException(nameof(cornerRadius)); }
        X = x;
        Y = y;
        Width = width;
        Height = height;
        CornerRadius = cornerRadius;
        IsFilled = isFilled;
    }

    /// <summary>
    /// Creates a filled rectangle painted by <paramref name="fill"/> instead of a flat
    /// color - for example a full-frame gradient backdrop.
    /// </summary>
    /// <param name="x">The horizontal position of the left edge, in calibrated drawing units.</param>
    /// <param name="y">The vertical position of the top edge, in calibrated drawing units.</param>
    /// <param name="width">The width, in calibrated drawing units; must be positive.</param>
    /// <param name="height">The height, in calibrated drawing units; must be positive.</param>
    /// <param name="fill">The fill that paints the rectangle.</param>
    /// <param name="cornerRadius">The corner radius, in calibrated drawing units; zero (the default) for square corners.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fill"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is zero or negative,
    /// or <paramref name="cornerRadius"/> is negative.
    /// </exception>
    public RectangleShape(float x, float y, float width, float height, ShapeFill fill, float cornerRadius = 0f)
        : base(Stroke.DefaultWidth, (Color?)null, fill ?? throw new ArgumentNullException(nameof(fill)))
    {
        if (width <= 0) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height <= 0) { throw new ArgumentOutOfRangeException(nameof(height)); }
        if (cornerRadius < 0) { throw new ArgumentOutOfRangeException(nameof(cornerRadius)); }
        X = x;
        Y = y;
        Width = width;
        Height = height;
        CornerRadius = cornerRadius;
        IsFilled = true;
    }

    /// <summary>The rectangle (with circular corners when it has a corner radius) as a closed geometry.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline()
    {
        var rectangle = new RectangleF(X, Y, Width, Height);
        return CornerRadius > 0 ? PathGeometry.RoundedRectangle(rectangle, CornerRadius) : PathGeometry.Rectangle(rectangle);
    }

    /// <summary>The area the rectangle covers: its outline when filled; else the band its outline's thickness covers (round joins).</summary>
    /// <param name="tolerance">How closely an outline's band is followed, calibrated units.</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
        => IsFilled ? new[] { GetOutline() } : OutlineArea(GetOutline(), tolerance);

    /// <summary>The rectangle's axes: centred, along +x, its width as the length and its height as the width.</summary>
    /// <returns>The axes.</returns>
    public override ElementAxes? GetAxes() => new ElementAxes(new PointF(X + (Width / 2f), Y + (Height / 2f)), 0f, Width, Height);

    /// <inheritdoc />
    public override RectangleF? GetBounds()
    {
        var rect = new RectangleF(X, Y, Width, Height);
        return IsFilled ? rect : Inflate(rect, StrokeThickness / 2f);
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = IsFilled ? CreateFillPaint(color, Fill) : CreateOutlinePaint(color);
        var rect = SKRect.Create(X, Y, Width, Height);

        if (CornerRadius > 0)
        {
            canvas.DrawRoundRect(rect, CornerRadius, CornerRadius, paint);
        }
        else
        {
            canvas.DrawRect(rect, paint);
        }
    }
}
