using System;
using CodeBrix.Imaging.Drawing.Models;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A straight arrow from a start point to a tip point in calibrated drawing coordinates,
/// with a V-shaped head at the tip - the classic "telestrator" annotation for pointing at
/// something on a video frame or image.
/// </summary>
public sealed class ArrowShape : DrawingShape
{
    private const double HeadAngleRadians = Math.PI / 6; //30 degrees off the shaft

    /// <summary>The horizontal position of the arrow's start (tail) point, in calibrated drawing units.</summary>
    public float X1 { get; }

    /// <summary>The vertical position of the arrow's start (tail) point, in calibrated drawing units.</summary>
    public float Y1 { get; }

    /// <summary>The horizontal position of the arrow's tip, in calibrated drawing units.</summary>
    public float X2 { get; }

    /// <summary>The vertical position of the arrow's tip, in calibrated drawing units.</summary>
    public float Y2 { get; }

    /// <summary>The length of each side of the arrow head, in calibrated drawing units.</summary>
    public float HeadLength { get; }

    /// <summary>
    /// Creates an arrow pointing from (<paramref name="x1"/>, <paramref name="y1"/>) to
    /// (<paramref name="x2"/>, <paramref name="y2"/>).
    /// </summary>
    /// <param name="x1">The horizontal position of the tail, in calibrated drawing units.</param>
    /// <param name="y1">The vertical position of the tail, in calibrated drawing units.</param>
    /// <param name="x2">The horizontal position of the tip, in calibrated drawing units.</param>
    /// <param name="y2">The vertical position of the tip, in calibrated drawing units.</param>
    /// <param name="strokeThickness">The line thickness, in calibrated drawing units.</param>
    /// <param name="color">The arrow's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="headLength">
    /// The length of each side of the arrow head, in calibrated drawing units; when
    /// omitted, three times the stroke thickness (at least 30 units) is used.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="headLength"/> is provided but not positive.</exception>
    public ArrowShape(float x1, float y1, float x2, float y2,
        float strokeThickness = Stroke.DefaultWidth, SKColor? color = null, float? headLength = null)
        : base(strokeThickness, color)
    {
        if (headLength.HasValue && headLength.Value <= 0) { throw new ArgumentOutOfRangeException(nameof(headLength)); }
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
        HeadLength = headLength ?? Math.Max(30f, strokeThickness * 3f);
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using SKPaint paint = CreateOutlinePaint(color);

        canvas.DrawLine(X1, Y1, X2, Y2, paint);

        //The head: two segments from the tip, angled back along the shaft direction
        double shaftAngle = Math.Atan2(Y1 - Y2, X1 - X2);
        foreach (double angle in new[] { shaftAngle + HeadAngleRadians, shaftAngle - HeadAngleRadians })
        {
            canvas.DrawLine(
                X2, Y2,
                X2 + (float)(HeadLength * Math.Cos(angle)),
                Y2 + (float)(HeadLength * Math.Sin(angle)),
                paint);
        }
    }
}
