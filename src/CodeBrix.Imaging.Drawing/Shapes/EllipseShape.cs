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
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units (not used when filled, but it must still be positive).</param>
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

    /// <summary>
    /// Creates a filled ellipse painted by <paramref name="fill"/> instead of a flat color.
    /// </summary>
    /// <param name="centerX">The horizontal position of the center, in calibrated drawing units.</param>
    /// <param name="centerY">The vertical position of the center, in calibrated drawing units.</param>
    /// <param name="radiusX">The horizontal radius, in calibrated drawing units; must be positive.</param>
    /// <param name="radiusY">The vertical radius, in calibrated drawing units; must be positive.</param>
    /// <param name="fill">The fill that paints the ellipse.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fill"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="radiusX"/> or <paramref name="radiusY"/> is zero or negative.
    /// </exception>
    public EllipseShape(float centerX, float centerY, float radiusX, float radiusY, ShapeFill fill)
        : base(Stroke.DefaultWidth, (Color?)null, fill ?? throw new ArgumentNullException(nameof(fill)))
    {
        if (radiusX <= 0) { throw new ArgumentOutOfRangeException(nameof(radiusX)); }
        if (radiusY <= 0) { throw new ArgumentOutOfRangeException(nameof(radiusY)); }
        CenterX = centerX;
        CenterY = centerY;
        RadiusX = radiusX;
        RadiusY = radiusY;
        IsFilled = true;
    }

    /// <summary>The ellipse as a closed geometry of four cubic curves.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline() => PathGeometry.Ellipse(new PointF(CenterX, CenterY), RadiusX, RadiusY);

    /// <summary>The area the ellipse covers: its outline when filled; else the band its outline's thickness covers (round joins).</summary>
    /// <param name="tolerance">How closely an outline's band is followed, calibrated units.</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
        => IsFilled ? new[] { GetOutline() } : OutlineArea(GetOutline(), tolerance);

    /// <summary>The ellipse's axes: centred, along +x, its horizontal diameter as the length and its vertical diameter as the width.</summary>
    /// <returns>The axes.</returns>
    public override ElementAxes? GetAxes() => new ElementAxes(new PointF(CenterX, CenterY), 0f, 2f * RadiusX, 2f * RadiusY);

    /// <inheritdoc />
    public override RectangleF? GetBounds()
    {
        float grow = IsFilled ? 0f : StrokeThickness / 2f;
        return new RectangleF(CenterX - RadiusX - grow, CenterY - RadiusY - grow,
            (2f * RadiusX) + (2f * grow), (2f * RadiusY) + (2f * grow));
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = IsFilled ? CreateFillPaint(color, Fill) : CreateOutlinePaint(color);
        canvas.DrawOval(CenterX, CenterY, RadiusX, RadiusY, paint);
    }
}
