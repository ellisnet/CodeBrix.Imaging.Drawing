using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// One connected run of a <see cref="PathGeometry"/>: a start point followed by segments, each
/// starting where the previous one ended, optionally closed back to the start. A closed
/// contour whose last segment does not end at the start point has an implicit straight
/// closing line (as in SVG), which counts towards its <see cref="Length"/> and is drawn.
/// Immutable.
/// </summary>
public sealed class PathContour
{
    private readonly PathSegment[] _segments;
    private Measure _measure;

    /// <summary>
    /// Creates a contour from a start point and segments.
    /// </summary>
    /// <param name="startPoint">The point the first segment starts at.</param>
    /// <param name="segments">The segments, in order; each must start exactly where the previous one ends (the first at <paramref name="startPoint"/>).</param>
    /// <param name="isClosed"><c>true</c> when the contour closes back to its start point.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there are no segments, a segment does not start where the previous one ended, or a point is not finite.</exception>
    public PathContour(PointF startPoint, IReadOnlyList<PathSegment> segments, bool isClosed)
    {
        if (segments == null) { throw new ArgumentNullException(nameof(segments)); }
        if (segments.Count < 1) { throw new ArgumentException("A contour needs at least one segment.", nameof(segments)); }
        var copy = new PathSegment[segments.Count];
        PointF previous = startPoint;
        for (int i = 0; i < copy.Length; i++)
        {
            PathSegment segment = segments[i];
            if (!segment.Start.Equals(previous))
            {
                throw new ArgumentException($"Segment {i} does not start where the previous segment ended.", nameof(segments));
            }
            if (!IsFinite(segment.Control1) || !IsFinite(segment.Control2) || !IsFinite(segment.End))
            {
                throw new ArgumentException($"Segment {i} has a point that is not finite.", nameof(segments));
            }
            copy[i] = segment;
            previous = segment.End;
        }
        if (!IsFinite(startPoint)) { throw new ArgumentException("The start point is not finite.", nameof(startPoint)); }
        StartPoint = startPoint;
        _segments = copy;
        IsClosed = isClosed;
    }

    //Wraps segments this library built and already checked (no copy)
    internal PathContour(PointF startPoint, PathSegment[] segments, bool isClosed, bool trusted)
    {
        StartPoint = startPoint;
        _segments = segments;
        IsClosed = isClosed;
    }

    /// <summary>The point the contour starts at.</summary>
    public PointF StartPoint { get; }

    /// <summary>The point the last segment ends at (before any implicit closing line).</summary>
    public PointF EndPoint => _segments[_segments.Length - 1].End;

    /// <summary>Indicates whether the contour closes back to its start point.</summary>
    public bool IsClosed { get; }

    /// <summary>The number of segments (the implicit closing line of a closed contour is not counted).</summary>
    public int SegmentCount => _segments.Length;

    /// <summary>Gets a segment.</summary>
    /// <param name="index">The segment's index, 0 to <see cref="SegmentCount"/> - 1.</param>
    /// <returns>The segment.</returns>
    public PathSegment this[int index] => _segments[index];

    /// <summary>Returns a copy of the segments, in order.</summary>
    /// <returns>A new array holding the segments.</returns>
    public PathSegment[] GetSegments() => (PathSegment[])_segments.Clone();

    /// <summary>
    /// The length of the contour, calibrated units, including the implicit closing line of a
    /// closed contour - measured on chords within 0.01 units of the curves, in double precision.
    /// </summary>
    public float Length => (float)GetMeasure().Total;

    internal PathSegment[] SegmentArray => _segments;

    internal double LengthValue => GetMeasure().Total;

    private static bool IsFinite(PointF p) => Single.IsFinite(p.X) && Single.IsFinite(p.Y);

    //Every segment that is drawn - with the closing line of a closed contour whose end is not
    //  its start - in double precision
    internal PathMath.Seg[] GetDrawnSegments() => GetMeasure().Segments;

    internal Measure GetMeasure()
    {
        Measure measure = _measure;
        if (measure == null)
        {
            measure = new Measure(this);
            _measure = measure;
        }
        return measure;
    }

    //The arc-length tables of one contour. Each curve is divided into Wang's-formula steps
    //  (MeasureTolerance), and the cumulative chord length is kept at each step; lines need
    //  no table. Built once, on first use; immutable afterwards
    internal sealed class Measure
    {
        public readonly PathMath.Seg[] Segments;
        public readonly double[][] Tables;
        public readonly double[] Starts;
        public readonly double Total;

        public Measure(PathContour contour)
        {
            var segments = new List<PathMath.Seg>(contour._segments.Length + 1);
            foreach (PathSegment segment in contour._segments)
            {
                segments.Add(PathMath.Seg.From(segment));
            }
            if (contour.IsClosed && !contour.EndPoint.Equals(contour.StartPoint))
            {
                PointF end = contour.EndPoint;
                segments.Add(PathMath.Seg.Line(end.X, end.Y, contour.StartPoint.X, contour.StartPoint.Y));
            }
            Segments = segments.ToArray();
            Tables = new double[Segments.Length][];
            Starts = new double[Segments.Length];
            double total = 0.0;
            for (int i = 0; i < Segments.Length; i++)
            {
                Starts[i] = total;
                PathMath.Seg s = Segments[i];
                if (s.Kind == PathSegmentKind.Line)
                {
                    double length = PathMath.Length(s.X3 - s.X0, s.Y3 - s.Y0);
                    Tables[i] = new[] { 0.0, length };
                    total += length;
                    continue;
                }
                int n = PathMath.Subdivisions(s, PathMath.MeasureTolerance);
                var table = new double[n + 1];
                double px = s.X0, py = s.Y0, sum = 0.0;
                for (int k = 1; k <= n; k++)
                {
                    PathMath.Evaluate(s, k / (double)n, out double x, out double y);
                    sum += PathMath.Length(x - px, y - py);
                    table[k] = sum;
                    px = x;
                    py = y;
                }
                Tables[i] = table;
                total += sum;
            }
            Total = total;
        }

        //The segment holding a distance (clamped to the contour) and the parameter there
        public void Locate(double distance, out int segmentIndex, out double t)
        {
            if (Segments.Length == 0) { segmentIndex = -1; t = 0.0; return; }
            if (distance <= 0.0) { segmentIndex = 0; t = 0.0; return; }
            if (distance >= Total) { segmentIndex = Segments.Length - 1; t = 1.0; return; }

            //The last segment starting at or before the distance
            int low = 0, high = Segments.Length - 1;
            while (low < high)
            {
                int mid = (low + high + 1) >> 1;
                if (Starts[mid] <= distance) { low = mid; } else { high = mid - 1; }
            }
            int index = low;
            segmentIndex = index;
            double[] table = Tables[index];
            double local = distance - Starts[index];
            int n = table.Length - 1;
            if (local >= table[n]) { t = 1.0; return; }

            //The last table entry at or below the distance
            int lo = 0, hi = n;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (table[mid] <= local) { lo = mid; } else { hi = mid; }
            }
            double span = table[lo + 1] - table[lo];
            double fraction = span > 0.0 ? (local - table[lo]) / span : 0.0;
            t = (lo + fraction) / n;
        }

        //The point and unit direction at a distance along the contour
        public void PointAt(double distance, out double x, out double y, out double tx, out double ty)
        {
            Locate(distance, out int index, out double t);
            PathMath.Seg s = Segments[index];
            PathMath.Evaluate(s, t, out x, out y);
            PathMath.Derivative(s, t, out double dx, out double dy);
            double length = PathMath.Length(dx, dy);
            if (length < 1e-9)
            {
                //A cusp or a control point on an end: the direction of the chord through t
                double[] table = Tables[index];
                int n = table.Length - 1;
                double t0 = Math.Max(0.0, t - (1.0 / n)), t1 = Math.Min(1.0, t + (1.0 / n));
                PathMath.Evaluate(s, t0, out double ax, out double ay);
                PathMath.Evaluate(s, t1, out double bx, out double by);
                dx = bx - ax;
                dy = by - ay;
                length = PathMath.Length(dx, dy);
                if (length < 1e-12)
                {
                    dx = s.X3 - s.X0;
                    dy = s.Y3 - s.Y0;
                    length = PathMath.Length(dx, dy);
                }
            }
            if (length < 1e-12) { tx = 1.0; ty = 0.0; return; }
            tx = dx / length;
            ty = dy / length;
        }

        //The part of the contour between two distances (clamped, a <= b), as segments;
        //  empty when the part has no length and zero-length parts are not wanted
        public List<PathMath.Seg> Extract(double a, double b, bool keepZeroLength)
        {
            var result = new List<PathMath.Seg>();
            if (Segments.Length == 0) { return result; }
            a = Math.Clamp(a, 0.0, Total);
            b = Math.Clamp(b, 0.0, Total);
            if (b < a || (b == a && !keepZeroLength)) { return result; }

            Locate(a, out int first, out double t0);
            Locate(b, out int last, out double t1);
            if (b == a)
            {
                PathMath.Evaluate(Segments[first], t0, out double x, out double y);
                result.Add(PathMath.Seg.Line(x, y, x, y));
                return result;
            }
            //A distance exactly at a segment boundary belongs to the next segment; draw the
            //  previous one to its end instead so no zero-length piece starts the part
            if (last > first && t1 <= 0.0)
            {
                last--;
                t1 = 1.0;
            }
            for (int i = first; i <= last; i++)
            {
                double from = i == first ? t0 : 0.0;
                double to = i == last ? t1 : 1.0;
                if (to <= from && !(first == last)) { continue; }
                result.Add(PathMath.Sub(Segments[i], from, to));
            }
            return result;
        }
    }
}
