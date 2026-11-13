using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A straight line segment between two points in calibrated drawing coordinates.
/// </summary>
public sealed class LineShape : DrawingShape
{
    /// <summary>The horizontal position of the line's start point, in calibrated drawing units.</summary>
    public float X1 { get; }

    /// <summary>The vertical position of the line's start point, in calibrated drawing units.</summary>
    public float Y1 { get; }

    /// <summary>The horizontal position of the line's end point, in calibrated drawing units.</summary>
    public float X2 { get; }

    /// <summary>The vertical position of the line's end point, in calibrated drawing units.</summary>
    public float Y2 { get; }

    /// <summary>
    /// Creates a line from (<paramref name="x1"/>, <paramref name="y1"/>) to
    /// (<paramref name="x2"/>, <paramref name="y2"/>).
    /// </summary>
    /// <param name="x1">The horizontal position of the start point, in calibrated drawing units.</param>
    /// <param name="y1">The vertical position of the start point, in calibrated drawing units.</param>
    /// <param name="x2">The horizontal position of the end point, in calibrated drawing units.</param>
    /// <param name="y2">The vertical position of the end point, in calibrated drawing units.</param>
    /// <param name="strokeThickness">The line thickness, in calibrated drawing units.</param>
    /// <param name="color">The line's color; or <c>null</c> to use the owning layer's color.</param>
    public LineShape(float x1, float y1, float x2, float y2,
        float strokeThickness = Stroke.DefaultWidth, Color? color = null)
        : base(strokeThickness, color)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    /// <summary>The line from (X1, Y1) to (X2, Y2) as an open geometry.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline() => PathGeometry.Line(new PointF(X1, Y1), new PointF(X2, Y2));

    /// <summary>The area the line's thickness covers: the line outlined with round caps.</summary>
    /// <param name="tolerance">How closely the round caps are followed, calibrated units.</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance) => OutlineArea(GetOutline(), tolerance);

    /// <summary>The line's axes: centred on its midpoint, along the line from (X1, Y1) to (X2, Y2), its length end to end and its thickness as the width.</summary>
    /// <returns>The axes.</returns>
    public override ElementAxes? GetAxes()
    {
        double dx = X2 - X1, dy = Y2 - Y1;
        return new ElementAxes(new PointF((X1 + X2) / 2f, (Y1 + Y2) / 2f), AngleUnits.ToDegrees(Math.Atan2(dy, dx)),
            (float)Math.Sqrt((dx * dx) + (dy * dy)), StrokeThickness);
    }

    /// <inheritdoc />
    public override RectangleF? GetBounds()
        => Inflate(BoundsOf(new PointF(X1, Y1), new PointF(X2, Y2)), StrokeThickness / 2f);

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = CreateOutlinePaint(color);
        canvas.DrawLine(X1, Y1, X2, Y2, paint);
    }
}
