using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// A single continuous drawing stroke: an ordered polyline of <see cref="StrokePoint"/> values
/// in calibrated drawing coordinates, plus the stroke width to draw it with. A stroke with a
/// single point is rendered as a dot.
/// </summary>
public sealed class Stroke : DrawingElement
{
    /// <summary>
    /// The default stroke width, in calibrated drawing units.
    /// </summary>
    public const float DefaultWidth = 15f;

    private readonly List<StrokePoint> _points = new List<StrokePoint>();
    private readonly object _pointsLocker = new object();

    /// <summary>
    /// The width of the stroke, in calibrated drawing units. The width is scaled
    /// proportionally when the stroke is rendered onto a canvas of any size.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// The moment (UTC) at which the stroke was started, when provided by the creator of
    /// the stroke; otherwise <c>null</c>.
    /// </summary>
    public DateTimeOffset? StartedAtUtc { get; }

    /// <summary>
    /// The number of points currently in the stroke.
    /// </summary>
    public int PointCount
    {
        get
        {
            lock (_pointsLocker) { return _points.Count; }
        }
    }

    /// <summary>
    /// The most recently added point of the stroke; or <c>null</c> when the stroke is empty.
    /// </summary>
    public StrokePoint? LastPoint
    {
        get
        {
            lock (_pointsLocker) { return _points.Count > 0 ? _points[_points.Count - 1] : null; }
        }
    }

    /// <summary>
    /// Creates a new, empty stroke.
    /// </summary>
    /// <param name="width">
    /// The width of the stroke in calibrated drawing units; when omitted,
    /// <see cref="DefaultWidth"/> is used.
    /// </param>
    /// <param name="startedAtUtc">
    /// The moment (UTC) at which the stroke was started; pass <c>null</c> when stroke
    /// timing is not needed.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> is zero or negative.
    /// </exception>
    public Stroke(float width = DefaultWidth, DateTimeOffset? startedAtUtc = null)
    {
        if (width <= 0) { throw new ArgumentOutOfRangeException(nameof(width)); }
        Width = width;
        StartedAtUtc = startedAtUtc;
    }

    /// <summary>
    /// Adds a point to the end of the stroke. A point at the same position as the current
    /// last point of the stroke is ignored, so callers can forward every pointer event
    /// without checking for duplicates.
    /// </summary>
    /// <param name="point">The calibrated point to add.</param>
    /// <returns><c>true</c> when the point was added; <c>false</c> when it was ignored as a duplicate.</returns>
    public bool AddPoint(StrokePoint point)
    {
        lock (_pointsLocker)
        {
            if (_points.Count > 0 && _points[_points.Count - 1].IsSamePositionAs(point))
            {
                return false;
            }
            _points.Add(point);
            return true;
        }
    }

    /// <summary>
    /// Adds a point to the end of the stroke. A point at the same position as the current
    /// last point of the stroke is ignored.
    /// </summary>
    /// <param name="x">The horizontal position of the point, in calibrated drawing coordinates.</param>
    /// <param name="y">The vertical position of the point, in calibrated drawing coordinates.</param>
    /// <param name="timeOffsetMs">The number of milliseconds elapsed since the start of the stroke.</param>
    /// <returns><c>true</c> when the point was added; <c>false</c> when it was ignored as a duplicate.</returns>
    public bool AddPoint(int x, int y, int timeOffsetMs = 0) => AddPoint(new StrokePoint(x, y, timeOffsetMs));

    /// <summary>
    /// Returns a snapshot copy of the points currently in the stroke, in the order they were added.
    /// </summary>
    /// <returns>A new array holding the stroke's points; the array is safe to enumerate while the stroke changes.</returns>
    public StrokePoint[] GetPoints()
    {
        lock (_pointsLocker) { return _points.ToArray(); }
    }
}
