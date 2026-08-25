using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// An immutable sequence of path segments (moves, lines, and Bezier curves). Build paths
/// with <see cref="DrawingPathBuilder"/> and detach them as immutable instances.
/// </summary>
public sealed class DrawingPath : IDisposable
{
    private readonly DrawingPathVerb[] _verbs;
    private readonly DrawingPoint[] _points;

    internal DrawingPath(DrawingPathVerb[] verbs, DrawingPoint[] points, DrawingPathFillType fillType)
    {
        _verbs = verbs;
        _points = points;
        FillType = fillType;
    }

    /// <summary>
    /// The rule that decides which regions are inside the path when it is filled.
    /// </summary>
    public DrawingPathFillType FillType { get; set; }

    /// <summary>Indicates whether the path contains no segments.</summary>
    public bool IsEmpty => _verbs.Length == 0;

    /// <summary>The number of points stored across all segments.</summary>
    public int PointCount => _points.Length;

    /// <summary>
    /// The tight axis-aligned bounds of the path's points (control points included), or an
    /// empty rectangle for an empty path.
    /// </summary>
    public DrawingRect Bounds
    {
        get
        {
            if (_points.Length == 0) { return DrawingRect.Empty; }

            float minX = _points[0].X, minY = _points[0].Y, maxX = _points[0].X, maxY = _points[0].Y;
            for (int i = 1; i < _points.Length; i++)
            {
                minX = Math.Min(minX, _points[i].X);
                minY = Math.Min(minY, _points[i].Y);
                maxX = Math.Max(maxX, _points[i].X);
                maxY = Math.Max(maxY, _points[i].Y);
            }
            return new DrawingRect(minX, minY, maxX, maxY);
        }
    }

    /// <summary>
    /// Walks the path's segments in order, so a consumer can re-emit the geometry into its
    /// own model - a PDF content stream, another path builder, a hit-test - instead of
    /// rasterizing it. Together with <see cref="FillType"/> and <see cref="Bounds"/> this
    /// is everything the path holds.
    /// </summary>
    /// <returns>The segments, in order; empty for an empty path.</returns>
    public IEnumerable<DrawingPathSegment> GetVerbs()
    {
        var pointIndex = 0;
        foreach (DrawingPathVerb verb in _verbs)
        {
            int count = verb switch
            {
                DrawingPathVerb.Move => 1,
                DrawingPathVerb.Line => 1,
                DrawingPathVerb.Quad => 2,
                DrawingPathVerb.Cubic => 3,
                _ => 0,
            };

            var points = new DrawingPoint[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = _points[pointIndex + i];
            }
            pointIndex += count;

            yield return new DrawingPathSegment(verb, points);
        }
    }

    internal IReadOnlyList<DrawingPathVerb> Verbs => _verbs;

    internal IReadOnlyList<DrawingPoint> Points => _points;

    /// <summary>
    /// Releases the path. The managed implementation holds no unmanaged resources; this
    /// exists so callers can treat the path as disposable.
    /// </summary>
    public void Dispose()
    {
    }
}
