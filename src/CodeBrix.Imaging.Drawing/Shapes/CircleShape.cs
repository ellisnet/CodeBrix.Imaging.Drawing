using System;
using CodeBrix.Imaging.Drawing.Models;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A circle with a center point and radius in calibrated drawing coordinates, drawn as an
/// outline or filled.
/// </summary>
public sealed class CircleShape : DrawingShape
{
    /// <summary>The horizontal position of the circle's center, in calibrated drawing units.</summary>
    public float CenterX { get; }

    /// <summary>The vertical position of the circle's center, in calibrated drawing units.</summary>
    public float CenterY { get; }

    /// <summary>The circle's radius, in calibrated drawing units.</summary>
    public float Radius { get; }

    /// <summary>Indicates whether the circle is filled solid rather than drawn as an outline.</summary>
    public bool IsFilled { get; }

    /// <summary>
    /// Creates a circle of radius <paramref name="radius"/> centered at
    /// (<paramref name="centerX"/>, <paramref name="centerY"/>).
    /// </summary>
    /// <param name="centerX">The horizontal position of the center, in calibrated drawing units.</param>
    /// <param name="centerY">The vertical position of the center, in calibrated drawing units.</param>
    /// <param name="radius">The radius, in calibrated drawing units; must be positive.</param>
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
    /// <param name="color">The circle's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="isFilled"><c>true</c> to fill the circle solid; <c>false</c> (the default) for an outline.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is zero or negative.</exception>
    public CircleShape(float centerX, float centerY, float radius,
        float strokeThickness = Stroke.DefaultWidth, Color? color = null, bool isFilled = false)
        : base(strokeThickness, color)
    {
        if (radius <= 0) { throw new ArgumentOutOfRangeException(nameof(radius)); }
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
        IsFilled = isFilled;
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = IsFilled ? CreateFillPaint(color) : CreateOutlinePaint(color);
        canvas.DrawCircle(CenterX, CenterY, Radius, paint);
    }
}
