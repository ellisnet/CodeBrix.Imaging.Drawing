using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A connected series of line segments through a list of points in calibrated drawing
/// coordinates - optionally closed into a polygon, and optionally filled.
/// </summary>
public sealed class PolylineShape : DrawingShape
{
    private readonly SKPoint[] _points;

    /// <summary>Indicates whether the last point connects back to the first, forming a polygon.</summary>
    public bool IsClosed { get; }

    /// <summary>
    /// Indicates whether the polygon is filled solid rather than drawn as an outline.
    /// A filled polyline is always treated as closed.
    /// </summary>
    public bool IsFilled { get; }

    /// <summary>The number of points in the polyline.</summary>
    public int PointCount => _points.Length;

    /// <summary>
    /// Creates a polyline through the given points.
    /// </summary>
    /// <param name="points">The points, in calibrated drawing units; at least two are required.</param>
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
    /// <param name="color">The polyline's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="isClosed"><c>true</c> to connect the last point back to the first.</param>
    /// <param name="isFilled"><c>true</c> to fill the resulting polygon solid (implies closed).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fewer than two points are provided.</exception>
    public PolylineShape(IReadOnlyList<PointF> points,
        float strokeThickness = Stroke.DefaultWidth, Color? color = null,
        bool isClosed = false, bool isFilled = false)
        : base(strokeThickness, color)
    {
        if (points == null) { throw new ArgumentNullException(nameof(points)); }
        if (points.Count < 2) { throw new ArgumentException("A polyline requires at least two points.", nameof(points)); }

        _points = new SKPoint[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            _points[i] = SkiaInterop.ToSK(points[i]);
        }

        IsFilled = isFilled;
        IsClosed = isClosed || isFilled;
    }

    /// <summary>
    /// Returns a snapshot copy of the polyline's points.
    /// </summary>
    /// <returns>A new array holding the points, in order.</returns>
    public PointF[] GetPoints()
    {
        var result = new PointF[_points.Length];
        for (int i = 0; i < _points.Length; i++)
        {
            result[i] = SkiaInterop.ToImaging(_points[i]);
        }
        return result;
    }

    /// <summary>
    /// Returns a snapshot copy of the polyline's points as SkiaSharp points.
    /// </summary>
    /// <returns>A new array holding the points, in order.</returns>
    public SKPoint[] GetPointsAsSkia() => (SKPoint[])_points.Clone();

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(_points[0]);
        for (int i = 1; i < _points.Length; i++)
        {
            pathBuilder.LineTo(_points[i]);
        }
        if (IsClosed)
        {
            pathBuilder.Close();
        }
        using SKPath path = pathBuilder.Detach();

        using SKPaint paint = IsFilled ? CreateFillPaint(color) : CreateOutlinePaint(color);
        canvas.DrawPath(path, paint);
    }
}
