using System;
using CodeBrix.Imaging.Drawing.Models;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// An axis-aligned ellipse with a center point and two radii in calibrated drawing
/// coordinates, drawn as an outline or filled.
/// </summary>
public sealed class EllipseShape : DrawingShape
{
    /// <summary>The horizontal position of the ellipse's center, in calibrated drawing units.</summary>
    public float CenterX { get; }

    /// <summary>The vertical position of the ellipse's center, in calibrated drawing units.</summary>
    public float CenterY { get; }

    /// <summary>The ellipse's horizontal radius, in calibrated drawing units.</summary>
    public float RadiusX { get; }

    /// <summary>The ellipse's vertical radius, in calibrated drawing units.</summary>
    public float RadiusY { get; }

    /// <summary>Indicates whether the ellipse is filled solid rather than drawn as an outline.</summary>
    public bool IsFilled { get; }

    /// <summary>
    /// Creates an ellipse centered at (<paramref name="centerX"/>, <paramref name="centerY"/>)
    /// with the given radii.
    /// </summary>
    /// <param name="centerX">The horizontal position of the center, in calibrated drawing units.</param>
    /// <param name="centerY">The vertical position of the center, in calibrated drawing units.</param>
    /// <param name="radiusX">The horizontal radius, in calibrated drawing units; must be positive.</param>
    /// <param name="radiusY">The vertical radius, in calibrated drawing units; must be positive.</param>
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
    /// <param name="color">The ellipse's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="isFilled"><c>true</c> to fill the ellipse solid; <c>false</c> (the default) for an outline.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="radiusX"/> or <paramref name="radiusY"/> is zero or negative.
    /// </exception>
    public EllipseShape(float centerX, float centerY, float radiusX, float radiusY,
        float strokeThickness = Stroke.DefaultWidth, Color? color = null, bool isFilled = false)
        : base(strokeThickness, color)
    {
        if (radiusX <= 0) { throw new ArgumentOutOfRangeException(nameof(radiusX)); }
        if (radiusY <= 0) { throw new ArgumentOutOfRangeException(nameof(radiusY)); }
        CenterX = centerX;
        CenterY = centerY;
        RadiusX = radiusX;
        RadiusY = radiusY;
        IsFilled = isFilled;
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = IsFilled ? CreateFillPaint(color) : CreateOutlinePaint(color);
        canvas.DrawOval(CenterX, CenterY, RadiusX, RadiusY, paint);
    }
}
