using System;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// One contour of a path flattened into straight segments (see
/// <see cref="PathGeometry.Flatten(float)"/>): its points in order, and whether the last
/// point joins back to the first. A closed polyline does not repeat its first point.
/// </summary>
public sealed class PathPolyline
{
    private readonly PointF[] _points;

    /// <summary>Creates a polyline from its points (copied).</summary>
    /// <param name="points">The points, in order.</param>
    /// <param name="isClosed"><c>true</c> when the last point joins back to the first.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    public PathPolyline(PointF[] points, bool isClosed)
    {
        if (points == null) { throw new ArgumentNullException(nameof(points)); }
        _points = (PointF[])points.Clone();
        IsClosed = isClosed;
    }

    //Wraps an array this library built (no copy)
    internal PathPolyline(PointF[] points, bool isClosed, bool wrap)
    {
        _points = points;
        IsClosed = isClosed;
    }

    /// <summary>Indicates whether the last point joins back to the first (a polygon).</summary>
    public bool IsClosed { get; }

    /// <summary>The number of points.</summary>
    public int Count => _points.Length;

    /// <summary>Gets a point.</summary>
    /// <param name="index">The point's index, 0 to <see cref="Count"/> - 1.</param>
    /// <returns>The point, in calibrated drawing units.</returns>
    public PointF this[int index] => _points[index];

    /// <summary>Returns a copy of the points, in order.</summary>
    /// <returns>A new array holding the points.</returns>
    public PointF[] GetPoints() => (PointF[])_points.Clone();
}
