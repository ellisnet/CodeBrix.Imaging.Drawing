using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// Builds a <see cref="PathGeometry"/> command by command - move, line, quadratic and cubic
/// curves, elliptical arcs and close - plus whole figures (rectangles, rounded rectangles,
/// ellipses, polygons). Coordinates are calibrated drawing units with y pointing down, so a
/// positive angle or a "clockwise" sweep turns clockwise on screen. Arcs are converted to cubic
/// Bezier segments as they are added (each covering at most a quarter turn). The builder can
/// keep going after <see cref="Build"/>; <see cref="Clear"/> starts over. Not thread-safe.
/// </summary>
public sealed class PathGeometryBuilder
{
    //4/3 * (sqrt(2) - 1): the cubic control distance, as a fraction of the radius, of a quarter circle
    internal const double Kappa = 0.55228474983079340;

    private readonly List<PathContour> _contours = new List<PathContour>();
    private readonly List<PathSegment> _segments = new List<PathSegment>();
    private PointF _start;
    private PointF _point;

    /// <summary>Creates an empty builder whose current point is (0, 0).</summary>
    public PathGeometryBuilder()
    {
    }

    /// <summary>The fill rule the built geometry carries; <see cref="PathFillRule.NonZero"/> by default.</summary>
    public PathFillRule FillRule { get; set; } = PathFillRule.NonZero;

    /// <summary>The point the next segment starts at.</summary>
    public PointF CurrentPoint => _point;

    /// <summary>The number of contours finished so far (a contour still being built is not counted).</summary>
    public int ContourCount => _contours.Count;

    /// <summary>
    /// Starts a new contour at a point (the contour being built, if any, is kept open).
    /// </summary>
    /// <param name="point">The new contour's start point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">Thrown when the point is not finite.</exception>
    public PathGeometryBuilder MoveTo(PointF point)
    {
        CheckFinite(point, nameof(point));
        FinishContour(false);
        _start = point;
        _point = point;
        return this;
    }

    /// <summary>Starts a new contour at a point.</summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <returns>This builder.</returns>
    public PathGeometryBuilder MoveTo(float x, float y) => MoveTo(new PointF(x, y));

    /// <summary>Adds a straight line from the current point.</summary>
    /// <param name="end">The line's end point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">Thrown when the point is not finite.</exception>
    public PathGeometryBuilder LineTo(PointF end)
    {
        CheckFinite(end, nameof(end));
        _segments.Add(PathSegment.Line(_point, end));
        _point = end;
        return this;
    }

    /// <summary>Adds a straight line from the current point.</summary>
    /// <param name="x">The end point's horizontal position.</param>
    /// <param name="y">The end point's vertical position.</param>
    /// <returns>This builder.</returns>
    public PathGeometryBuilder LineTo(float x, float y) => LineTo(new PointF(x, y));

    /// <summary>Adds a quadratic Bezier curve from the current point.</summary>
    /// <param name="control">The control point.</param>
    /// <param name="end">The end point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">Thrown when a point is not finite.</exception>
    public PathGeometryBuilder QuadraticTo(PointF control, PointF end)
    {
        CheckFinite(control, nameof(control));
        CheckFinite(end, nameof(end));
        _segments.Add(PathSegment.Quadratic(_point, control, end));
        _point = end;
        return this;
    }

    /// <summary>Adds a cubic Bezier curve from the current point.</summary>
    /// <param name="control1">The first control point.</param>
    /// <param name="control2">The second control point.</param>
    /// <param name="end">The end point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">Thrown when a point is not finite.</exception>
    public PathGeometryBuilder CubicTo(PointF control1, PointF control2, PointF end)
    {
        CheckFinite(control1, nameof(control1));
        CheckFinite(control2, nameof(control2));
        CheckFinite(end, nameof(end));
        _segments.Add(PathSegment.Cubic(_point, control1, control2, end));
        _point = end;
        return this;
    }

    /// <summary>
    /// Adds an elliptical arc from the current point to <paramref name="end"/>, described the
    /// way SVG's <c>A</c> command describes it: the ellipse's radii and rotation, and two flags
    /// that pick one of the four arcs through both points. Radii too small to reach are scaled
    /// up (as SVG requires); a zero radius gives a straight line; an arc to the current point
    /// adds nothing.
    /// </summary>
    /// <param name="radiusX">The ellipse's horizontal radius (before rotation).</param>
    /// <param name="radiusY">The ellipse's vertical radius (before rotation).</param>
    /// <param name="xAxisRotation">The ellipse's rotation, degrees, clockwise on screen.</param>
    /// <param name="largeArc"><c>true</c> for the arc longer than half the ellipse.</param>
    /// <param name="clockwise"><c>true</c> to travel clockwise on screen (SVG's sweep flag 1).</param>
    /// <param name="end">The arc's end point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is not finite.</exception>
    public PathGeometryBuilder ArcTo(float radiusX, float radiusY, float xAxisRotation, bool largeArc, bool clockwise, PointF end)
    {
        CheckFinite(end, nameof(end));
        if (!Single.IsFinite(radiusX) || !Single.IsFinite(radiusY) || !Single.IsFinite(xAxisRotation))
        {
            throw new ArgumentException("The arc's radii and rotation must be finite.");
        }
        double x1 = _point.X, y1 = _point.Y, x2 = end.X, y2 = end.Y;
        if (x1 == x2 && y1 == y2) { return this; }
        double rx = Math.Abs((double)radiusX), ry = Math.Abs((double)radiusY);
        if (rx == 0.0 || ry == 0.0) { return LineTo(end); }

        double phi = xAxisRotation * (Math.PI / 180.0);
        double cos = Math.Cos(phi), sin = Math.Sin(phi);
        double hx = (x1 - x2) / 2.0, hy = (y1 - y2) / 2.0;
        double x1p = (cos * hx) + (sin * hy);
        double y1p = (-sin * hx) + (cos * hy);

        double lambda = ((x1p * x1p) / (rx * rx)) + ((y1p * y1p) / (ry * ry));
        if (lambda > 1.0)
        {
            double scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        double numerator = (rx * rx * ry * ry) - (rx * rx * y1p * y1p) - (ry * ry * x1p * x1p);
        double denominator = (rx * rx * y1p * y1p) + (ry * ry * x1p * x1p);
        double coefficient = denominator == 0.0 ? 0.0 : Math.Sqrt(Math.Max(0.0, numerator / denominator));
        if (largeArc == clockwise) { coefficient = -coefficient; }
        double cxp = coefficient * (rx * y1p / ry);
        double cyp = coefficient * (-(ry * x1p) / rx);
        double cx = (cos * cxp) - (sin * cyp) + ((x1 + x2) / 2.0);
        double cy = (sin * cxp) + (cos * cyp) + ((y1 + y2) / 2.0);

        double ux = (x1p - cxp) / rx, uy = (y1p - cyp) / ry;
        double vx = (-x1p - cxp) / rx, vy = (-y1p - cyp) / ry;
        double theta1 = Math.Atan2(uy, ux);
        double delta = Math.Atan2((ux * vy) - (uy * vx), (ux * vx) + (uy * vy));
        if (!clockwise && delta > 0.0) { delta -= 2.0 * Math.PI; }
        else if (clockwise && delta < 0.0) { delta += 2.0 * Math.PI; }

        AppendArc(cx, cy, rx, ry, cos, sin, theta1, delta, end);
        return this;
    }

    /// <summary>
    /// Starts a new open contour that is an arc of an ellipse: from <paramref name="startAngle"/>
    /// through <paramref name="sweepAngle"/> degrees (positive = clockwise on screen; 0 degrees
    /// points right). A sweep of 360 or more is a whole ellipse (left open, so draw it closed
    /// with <see cref="Close"/>).
    /// </summary>
    /// <param name="center">The ellipse's centre.</param>
    /// <param name="radiusX">The horizontal radius.</param>
    /// <param name="radiusY">The vertical radius.</param>
    /// <param name="startAngle">Where the arc starts, degrees.</param>
    /// <param name="sweepAngle">How far it turns, degrees (clamped to -360..360).</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is not finite or a radius is negative.</exception>
    public PathGeometryBuilder AddArc(PointF center, float radiusX, float radiusY, float startAngle, float sweepAngle)
    {
        CheckFinite(center, nameof(center));
        if (!Single.IsFinite(radiusX) || !Single.IsFinite(radiusY) || radiusX < 0f || radiusY < 0f
            || !Single.IsFinite(startAngle) || !Single.IsFinite(sweepAngle))
        {
            throw new ArgumentException("The arc's radii must be finite and not negative, and its angles finite.");
        }
        double start = startAngle * (Math.PI / 180.0);
        double sweep = Math.Clamp((double)sweepAngle, -360.0, 360.0) * (Math.PI / 180.0);
        double sx = center.X + (radiusX * Math.Cos(start));
        double sy = center.Y + (radiusY * Math.Sin(start));
        MoveTo(PathMath.P(sx, sy));
        if (sweep == 0.0) { return this; }
        double endAngle = start + sweep;
        var end = PathMath.P(center.X + (radiusX * Math.Cos(endAngle)), center.Y + (radiusY * Math.Sin(endAngle)));
        AppendArc(center.X, center.Y, radiusX, radiusY, 1.0, 0.0, start, sweep, end);
        return this;
    }

    /// <summary>
    /// Closes the contour being built back to its start point (with a straight line when its
    /// last point is elsewhere). The next segment starts a new contour at that start point.
    /// Does nothing when the contour has no segments.
    /// </summary>
    /// <returns>This builder.</returns>
    public PathGeometryBuilder Close()
    {
        if (_segments.Count == 0) { return this; }
        FinishContour(true);
        _point = _start;
        return this;
    }

    /// <summary>Adds a closed or open polygon through points, as a new contour.</summary>
    /// <param name="points">The points, in order; at least two.</param>
    /// <param name="closed"><c>true</c> to close it.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fewer than two points are given or a point is not finite.</exception>
    public PathGeometryBuilder AddPolygon(IReadOnlyList<PointF> points, bool closed)
    {
        if (points == null) { throw new ArgumentNullException(nameof(points)); }
        if (points.Count < 2) { throw new ArgumentException("A polygon needs at least two points.", nameof(points)); }
        MoveTo(points[0]);
        for (int i = 1; i < points.Count; i++) { LineTo(points[i]); }
        if (closed) { Close(); } else { FinishContour(false); }
        return this;
    }

    /// <summary>Adds a rectangle as a new closed contour, clockwise from its top-left corner.</summary>
    /// <param name="rectangle">The rectangle, calibrated units.</param>
    /// <returns>This builder.</returns>
    public PathGeometryBuilder AddRectangle(RectangleF rectangle)
    {
        MoveTo(new PointF(rectangle.Left, rectangle.Top));
        LineTo(new PointF(rectangle.Right, rectangle.Top));
        LineTo(new PointF(rectangle.Right, rectangle.Bottom));
        LineTo(new PointF(rectangle.Left, rectangle.Bottom));
        return Close();
    }

    /// <summary>
    /// Adds a rectangle with circular corners as a new closed contour, clockwise from the end
    /// of its top-left corner. The radius is limited to half the shorter side; 0 gives a plain
    /// rectangle.
    /// </summary>
    /// <param name="rectangle">The rectangle, calibrated units.</param>
    /// <param name="cornerRadius">The corner radius.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is negative or not finite.</exception>
    public PathGeometryBuilder AddRoundedRectangle(RectangleF rectangle, float cornerRadius)
    {
        if (!Single.IsFinite(cornerRadius) || cornerRadius < 0f) { throw new ArgumentOutOfRangeException(nameof(cornerRadius)); }
        double r = Math.Min(cornerRadius, Math.Min(Math.Abs(rectangle.Width), Math.Abs(rectangle.Height)) / 2.0);
        if (r <= 0.0) { return AddRectangle(rectangle); }
        double l = rectangle.Left, t = rectangle.Top, rt = rectangle.Right, b = rectangle.Bottom;
        double k = r * Kappa;
        MoveTo(PathMath.P(l + r, t));
        LineTo(PathMath.P(rt - r, t));
        CubicTo(PathMath.P(rt - r + k, t), PathMath.P(rt, t + r - k), PathMath.P(rt, t + r));
        LineTo(PathMath.P(rt, b - r));
        CubicTo(PathMath.P(rt, b - r + k), PathMath.P(rt - r + k, b), PathMath.P(rt - r, b));
        LineTo(PathMath.P(l + r, b));
        CubicTo(PathMath.P(l + r - k, b), PathMath.P(l, b - r + k), PathMath.P(l, b - r));
        LineTo(PathMath.P(l, t + r));
        CubicTo(PathMath.P(l, t + r - k), PathMath.P(l + r - k, t), PathMath.P(l + r, t));
        return Close();
    }

    /// <summary>
    /// Adds an ellipse as a new closed contour of four cubic curves, clockwise from its
    /// rightmost point.
    /// </summary>
    /// <param name="center">The centre.</param>
    /// <param name="radiusX">The horizontal radius.</param>
    /// <param name="radiusY">The vertical radius.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a radius is negative or not finite.</exception>
    public PathGeometryBuilder AddEllipse(PointF center, float radiusX, float radiusY)
    {
        if (!Single.IsFinite(radiusX) || radiusX < 0f) { throw new ArgumentOutOfRangeException(nameof(radiusX)); }
        if (!Single.IsFinite(radiusY) || radiusY < 0f) { throw new ArgumentOutOfRangeException(nameof(radiusY)); }
        double cx = center.X, cy = center.Y, rx = radiusX, ry = radiusY;
        double kx = rx * Kappa, ky = ry * Kappa;
        MoveTo(PathMath.P(cx + rx, cy));
        CubicTo(PathMath.P(cx + rx, cy + ky), PathMath.P(cx + kx, cy + ry), PathMath.P(cx, cy + ry));
        CubicTo(PathMath.P(cx - kx, cy + ry), PathMath.P(cx - rx, cy + ky), PathMath.P(cx - rx, cy));
        CubicTo(PathMath.P(cx - rx, cy - ky), PathMath.P(cx - kx, cy - ry), PathMath.P(cx, cy - ry));
        CubicTo(PathMath.P(cx + kx, cy - ry), PathMath.P(cx + rx, cy - ky), PathMath.P(cx + rx, cy));
        return Close();
    }

    /// <summary>Adds a circle as a new closed contour, clockwise from its rightmost point.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="radius">The radius.</param>
    /// <returns>This builder.</returns>
    public PathGeometryBuilder AddCircle(PointF center, float radius) => AddEllipse(center, radius, radius);

    /// <summary>Adds every contour of a geometry (its fill rule is not taken).</summary>
    /// <param name="geometry">The geometry to add.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    public PathGeometryBuilder AddPath(PathGeometry geometry)
    {
        if (geometry == null) { throw new ArgumentNullException(nameof(geometry)); }
        FinishContour(false);
        foreach (PathContour contour in geometry.Contours)
        {
            _contours.Add(contour);
            _start = contour.StartPoint;
            _point = contour.IsClosed ? contour.StartPoint : contour.EndPoint;
        }
        return this;
    }

    /// <summary>
    /// Returns the geometry built so far (the contour being built is included, open). The
    /// builder keeps its state, so building can continue.
    /// </summary>
    /// <returns>A new immutable geometry.</returns>
    public PathGeometry Build()
    {
        var contours = new List<PathContour>(_contours);
        if (_segments.Count > 0)
        {
            contours.Add(new PathContour(_start, _segments.ToArray(), false, true));
        }
        return new PathGeometry(contours.ToArray(), FillRule);
    }

    /// <summary>Discards everything; the current point returns to (0, 0).</summary>
    /// <returns>This builder.</returns>
    public PathGeometryBuilder Clear()
    {
        _contours.Clear();
        _segments.Clear();
        _start = default;
        _point = default;
        return this;
    }

    private void FinishContour(bool closed)
    {
        if (_segments.Count == 0) { return; }
        _contours.Add(new PathContour(_start, _segments.ToArray(), closed, true));
        _segments.Clear();
    }

    //Appends an elliptical arc (centre form, the ellipse rotated by cos/sin) as cubic pieces of
    //  at most a quarter turn each; the last piece ends exactly at `end`
    private void AppendArc(double cx, double cy, double rx, double ry, double cos, double sin,
        double theta, double delta, PointF end)
    {
        int pieces = (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2.0) - 1e-9);
        if (pieces < 1) { pieces = 1; }
        double step = delta / pieces;
        double k = 4.0 / 3.0 * Math.Tan(step / 4.0);
        double a = theta;
        for (int i = 0; i < pieces; i++)
        {
            double b = a + step;
            double ca = Math.Cos(a), sa = Math.Sin(a), cb = Math.Cos(b), sb = Math.Sin(b);
            //Points and derivatives on the unrotated ellipse, then rotated and moved
            double ax = rx * ca, ay = ry * sa, adx = -rx * sa, ady = ry * ca;
            double bx = rx * cb, by = ry * sb, bdx = -rx * sb, bdy = ry * cb;
            PointF c1 = Rotate(cx, cy, cos, sin, ax + (k * adx), ay + (k * ady));
            PointF c2 = Rotate(cx, cy, cos, sin, bx - (k * bdx), by - (k * bdy));
            PointF p = i == pieces - 1 ? end : Rotate(cx, cy, cos, sin, bx, by);
            _segments.Add(PathSegment.Cubic(_point, c1, c2, p));
            _point = p;
            a = b;
        }
    }

    private static PointF Rotate(double cx, double cy, double cos, double sin, double x, double y)
        => PathMath.P(cx + (cos * x) - (sin * y), cy + (sin * x) + (cos * y));

    private static void CheckFinite(PointF point, string name)
    {
        if (!Single.IsFinite(point.X) || !Single.IsFinite(point.Y))
        {
            throw new ArgumentException("The point must be finite.", name);
        }
    }
}
