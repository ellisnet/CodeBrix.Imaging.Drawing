using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// An immutable sequence of path segments (moves, lines, and Bezier curves), API-compatible
/// with the SkiaSharp <c>SKPath</c> type as consumed through <see cref="DrawingPathBuilder"/>.
/// Build paths with <see cref="DrawingPathBuilder"/> and detach them, mirroring the modern
/// SkiaSharp 4.x pattern.
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

    internal IReadOnlyList<DrawingPathVerb> Verbs => _verbs;

    internal IReadOnlyList<DrawingPoint> Points => _points;

    /// <summary>
    /// Releases the path. The managed implementation holds no unmanaged resources; this
    /// exists for API compatibility with SkiaSharp's disposable paths.
    /// </summary>
    public void Dispose()
    {
    }
}
