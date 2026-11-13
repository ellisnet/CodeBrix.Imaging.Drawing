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
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units (not used when filled, but it must still be positive).</param>
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

    /// <summary>
    /// Creates a filled circle painted by <paramref name="fill"/> - for example a radial
    /// gradient glow - instead of a flat color.
    /// </summary>
    /// <param name="centerX">The horizontal position of the center, in calibrated drawing units.</param>
    /// <param name="centerY">The vertical position of the center, in calibrated drawing units.</param>
    /// <param name="radius">The radius, in calibrated drawing units; must be positive.</param>
    /// <param name="fill">The fill that paints the circle.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fill"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is zero or negative.</exception>
    public CircleShape(float centerX, float centerY, float radius, ShapeFill fill)
        : base(Stroke.DefaultWidth, (Color?)null, fill ?? throw new ArgumentNullException(nameof(fill)))
    {
        if (radius <= 0) { throw new ArgumentOutOfRangeException(nameof(radius)); }
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
        IsFilled = true;
    }

    /// <summary>The circle as a closed geometry of four cubic curves.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline() => PathGeometry.Circle(new PointF(CenterX, CenterY), Radius);

    /// <summary>The area the circle covers: its outline when filled; else the band its outline's thickness covers (round joins).</summary>
    /// <param name="tolerance">How closely an outline's band is followed, calibrated units.</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
        => IsFilled ? new[] { GetOutline() } : OutlineArea(GetOutline(), tolerance);

    /// <summary>The circle's axes: centred, along +x, its diameter as both length and width.</summary>
    /// <returns>The axes.</returns>
    public override ElementAxes? GetAxes() => new ElementAxes(new PointF(CenterX, CenterY), 0f, 2f * Radius, 2f * Radius);

    /// <inheritdoc />
    public override RectangleF? GetBounds()
    {
        float reach = IsFilled ? Radius : Radius + (StrokeThickness / 2f);
        return new RectangleF(CenterX - reach, CenterY - reach, 2f * reach, 2f * reach);
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = IsFilled ? CreateFillPaint(color, Fill) : CreateOutlinePaint(color);
        canvas.DrawCircle(CenterX, CenterY, Radius, paint);
    }
}
