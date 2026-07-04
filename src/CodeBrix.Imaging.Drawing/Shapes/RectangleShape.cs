using System;
using CodeBrix.Imaging.Drawing.Models;
using SkiaSharp;

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
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
    /// <param name="color">The rectangle's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="isFilled"><c>true</c> to fill the rectangle solid; <c>false</c> (the default) for an outline.</param>
    /// <param name="cornerRadius">The corner radius, in calibrated drawing units; zero (the default) for square corners.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is zero or negative,
    /// or <paramref name="cornerRadius"/> is negative.
    /// </exception>
    public RectangleShape(float x, float y, float width, float height,
        float strokeThickness = Stroke.DefaultWidth, SKColor? color = null,
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

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = IsFilled ? CreateFillPaint(color) : CreateOutlinePaint(color);
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
