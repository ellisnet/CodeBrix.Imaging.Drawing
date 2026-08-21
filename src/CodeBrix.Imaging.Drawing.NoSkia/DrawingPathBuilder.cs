using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A mutable builder that accumulates path segments and detaches them as an immutable
/// <see cref="DrawingPath"/>, API-compatible with the SkiaSharp 4.x <c>SKPathBuilder</c> type.
/// </summary>
public sealed class DrawingPathBuilder
{
    private readonly List<DrawingPathVerb> _verbs = new List<DrawingPathVerb>();
    private readonly List<DrawingPoint> _points = new List<DrawingPoint>();
    private DrawingPathFillType _fillType = DrawingPathFillType.Winding;
    private bool _hasCurrentContour;

    /// <summary>
    /// Sets the fill rule of the path being built.
    /// </summary>
    /// <param name="fillType">The fill rule to use.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder SetFillType(DrawingPathFillType fillType)
    {
        _fillType = fillType;
        return this;
    }

    /// <summary>
    /// Begins a new contour at the given point.
    /// </summary>
    /// <param name="point">The contour's starting point.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder MoveTo(DrawingPoint point)
    {
        _verbs.Add(DrawingPathVerb.Move);
        _points.Add(point);
        _hasCurrentContour = true;
        return this;
    }

    /// <summary>
    /// Begins a new contour at the given position.
    /// </summary>
    /// <param name="x">The horizontal position of the contour's starting point.</param>
    /// <param name="y">The vertical position of the contour's starting point.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder MoveTo(float x, float y) => MoveTo(new DrawingPoint(x, y));

    /// <summary>
    /// Adds a straight line from the current point to the given point.
    /// </summary>
    /// <param name="point">The line's end point.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder LineTo(DrawingPoint point)
    {
        EnsureContour(point);
        _verbs.Add(DrawingPathVerb.Line);
        _points.Add(point);
        return this;
    }

    /// <summary>
    /// Adds a straight line from the current point to the given position.
    /// </summary>
    /// <param name="x">The horizontal position of the line's end point.</param>
    /// <param name="y">The vertical position of the line's end point.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder LineTo(float x, float y) => LineTo(new DrawingPoint(x, y));

    /// <summary>
    /// Adds a quadratic Bezier curve from the current point.
    /// </summary>
    /// <param name="control">The curve's control point.</param>
    /// <param name="end">The curve's end point.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder QuadTo(DrawingPoint control, DrawingPoint end)
    {
        EnsureContour(control);
        _verbs.Add(DrawingPathVerb.Quad);
        _points.Add(control);
        _points.Add(end);
        return this;
    }

    /// <summary>
    /// Adds a cubic Bezier curve from the current point.
    /// </summary>
    /// <param name="control1">The curve's first control point.</param>
    /// <param name="control2">The curve's second control point.</param>
    /// <param name="end">The curve's end point.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder CubicTo(DrawingPoint control1, DrawingPoint control2, DrawingPoint end)
    {
        EnsureContour(control1);
        _verbs.Add(DrawingPathVerb.Cubic);
        _points.Add(control1);
        _points.Add(control2);
        _points.Add(end);
        return this;
    }

    /// <summary>
    /// Closes the current contour back to its starting point.
    /// </summary>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder Close()
    {
        if (_hasCurrentContour)
        {
            _verbs.Add(DrawingPathVerb.Close);
            _hasCurrentContour = false;
        }
        return this;
    }

    /// <summary>
    /// Adds an axis-aligned rectangle as its own closed contour.
    /// </summary>
    /// <param name="rect">The rectangle to add.</param>
    /// <returns>This same builder, so calls can be chained.</returns>
    public DrawingPathBuilder AddRect(DrawingRect rect)
    {
        MoveTo(rect.Left, rect.Top);
        LineTo(rect.Right, rect.Top);
        LineTo(rect.Right, rect.Bottom);
        LineTo(rect.Left, rect.Bottom);
        return Close();
    }

    /// <summary>
    /// Detaches the accumulated segments as an immutable <see cref="DrawingPath"/> and resets
    /// this builder to empty.
    /// </summary>
    /// <returns>The built path.</returns>
    public DrawingPath Detach()
    {
        var path = new DrawingPath(_verbs.ToArray(), _points.ToArray(), _fillType);
        _verbs.Clear();
        _points.Clear();
        _fillType = DrawingPathFillType.Winding;
        _hasCurrentContour = false;
        return path;
    }

    private void EnsureContour(DrawingPoint fallbackStart)
    {
        //Mirroring Skia: a segment verb with no open contour starts one implicitly
        if (!_hasCurrentContour)
        {
            MoveTo(_points.Count > 0 ? _points[_points.Count - 1] : fallbackStart);
        }
    }
}
