using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// A general path - one or more contours of straight lines and quadratic and cubic Bezier
/// curves (arcs are stored as cubic curves), each open or closed - in calibrated drawing units
/// with y pointing down. Immutable and thread-safe; build one with a
/// <see cref="PathGeometryBuilder"/>, a factory (<see cref="Rectangle(RectangleF)"/>,
/// <see cref="Star(PointF, float, float, int, float)"/>, <see cref="RoundedPolygon(IReadOnlyList{PointF}, float)"/>,
/// ...) or SVG path data (<see cref="Parse(string)"/>), and draw it with a
/// <see cref="CodeBrix.Imaging.Drawing.Shapes.PathShape"/>.
/// <para>
/// Everything a geometry COMPUTES - its length, the point and direction at any distance, trimmed
/// and dashed pieces, flattened polylines, morphs, bounds - is plain managed arithmetic in double
/// precision with a documented order of operations, so both packages give bit-identical results.
/// Lengths and positions are measured on chords within 0.01 units of the curves (Wang's formula
/// picks the steps), so a measured length can fall short of the true curve length by about that
/// much per curve.
/// </para>
/// </summary>
public sealed class PathGeometry
{
    /// <summary>The flattening tolerance <see cref="Flatten(float)"/> uses by default, calibrated units.</summary>
    public const float DefaultFlattenTolerance = 0.25f;

    /// <summary>A geometry with no contours.</summary>
    public static readonly PathGeometry Empty = new PathGeometry(Array.Empty<PathContour>(), PathFillRule.NonZero);

    private readonly PathContour[] _contours;
    private double[] _contourStarts;
    private double _total = -1.0;
    private PathAreaQuery _areaQuery;

    internal PathGeometry(PathContour[] contours, PathFillRule fillRule)
    {
        _contours = contours;
        FillRule = fillRule;
    }

    /// <summary>
    /// Creates a geometry from contours.
    /// </summary>
    /// <param name="contours">The contours, in drawing order.</param>
    /// <param name="fillRule">How overlapping contours fill.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contours"/> or one of its entries is null.</exception>
    public PathGeometry(IEnumerable<PathContour> contours, PathFillRule fillRule = PathFillRule.NonZero)
    {
        if (contours == null) { throw new ArgumentNullException(nameof(contours)); }
        var list = new List<PathContour>();
        foreach (PathContour contour in contours)
        {
            list.Add(contour ?? throw new ArgumentNullException(nameof(contours), "A contour is null."));
        }
        _contours = list.ToArray();
        FillRule = fillRule;
    }

    /// <summary>The contours, in drawing order.</summary>
    public IReadOnlyList<PathContour> Contours => _contours;

    /// <summary>How overlapping contours fill.</summary>
    public PathFillRule FillRule { get; }

    /// <summary>Indicates whether the geometry has no contours.</summary>
    public bool IsEmpty => _contours.Length == 0;

    /// <summary>The same contours with another fill rule.</summary>
    /// <param name="fillRule">The fill rule.</param>
    /// <returns>A geometry (this one when the rule is unchanged).</returns>
    public PathGeometry WithFillRule(PathFillRule fillRule)
        => fillRule == FillRule ? this : new PathGeometry(_contours, fillRule);

    /// <summary>
    /// The total length of every contour (each closed contour including its closing line),
    /// calibrated units.
    /// </summary>
    /// <returns>The length; 0 for an empty geometry.</returns>
    public float GetLength() => (float)LengthValue;

    internal double LengthValue
    {
        get
        {
            EnsureStarts();
            return _total;
        }
    }

    /// <summary>
    /// The point at a fraction of the total length: 0 is the start of the first contour, 1 the
    /// end of the last (the contours are measured one after another). The fraction is clamped
    /// to 0..1; NaN counts as 0.
    /// </summary>
    /// <param name="fraction">How far along, 0..1.</param>
    /// <returns>The point, its direction and its distance; for an empty geometry (0, 0) heading right on contour -1.</returns>
    public PathPoint GetPointAt(float fraction)
    {
        double f = Single.IsNaN(fraction) ? 0.0 : Math.Clamp((double)fraction, 0.0, 1.0);
        return PointAtDistance(f * LengthValue);
    }

    /// <summary>
    /// The point at a distance along the geometry (clamped to 0..length). A distance exactly
    /// where one contour ends and the next begins lies at the start of the next one.
    /// </summary>
    /// <param name="distance">How far along, calibrated units.</param>
    /// <returns>The point, its direction and its distance.</returns>
    public PathPoint GetPointAtDistance(float distance)
    {
        double d = Single.IsNaN(distance) ? 0.0 : distance;
        return PointAtDistance(d);
    }

    internal PathPoint PointAtDistance(double distance)
    {
        EnsureStarts();
        if (_contours.Length == 0) { return new PathPoint(new PointF(0f, 0f), new PointF(1f, 0f), 0f, -1); }
        distance = Math.Clamp(distance, 0.0, _total);
        int index = _contours.Length - 1;
        for (int i = 1; i < _contours.Length; i++)
        {
            if (distance < _contourStarts[i]) { index = i - 1; break; }
        }
        _contours[index].GetMeasure().PointAt(distance - _contourStarts[index],
            out double x, out double y, out double tx, out double ty);
        return new PathPoint(PathMath.P(x, y), PathMath.P(tx, ty), (float)distance, index);
    }

    /// <summary>
    /// The point at a fraction of the total length, like <see cref="GetPointAt(float)"/> - but with
    /// <paramref name="extendBeyondEnds"/> set, a fraction below 0 or above 1 is NOT clamped: the
    /// path is continued in a straight line along its first point's direction (backwards, before
    /// the start) or its last point's direction (after the end), so that an overshoot past either
    /// end keeps moving at the same rate. The point's <see cref="PathPoint.Distance"/> is then
    /// below 0 or above the length. Without it, this is exactly <see cref="GetPointAt(float)"/>.
    /// </summary>
    /// <param name="fraction">How far along, as a fraction of the length (any value when extending).</param>
    /// <param name="extendBeyondEnds"><c>true</c> to continue straight past either end instead of clamping.</param>
    /// <returns>The point, its direction and its distance.</returns>
    public PathPoint GetPointAt(float fraction, bool extendBeyondEnds)
    {
        if (!extendBeyondEnds || Single.IsNaN(fraction)) { return GetPointAt(fraction); }
        return PointAtDistanceExtended(fraction * LengthValue);
    }

    /// <summary>
    /// The point at a distance along the geometry, like <see cref="GetPointAtDistance(float)"/> -
    /// but with <paramref name="extendBeyondEnds"/> set, a distance below 0 or beyond the length
    /// continues straight along the first or last point's direction instead of being clamped.
    /// </summary>
    /// <param name="distance">How far along, calibrated units.</param>
    /// <param name="extendBeyondEnds"><c>true</c> to continue straight past either end instead of clamping.</param>
    /// <returns>The point, its direction and its distance.</returns>
    public PathPoint GetPointAtDistance(float distance, bool extendBeyondEnds)
    {
        if (!extendBeyondEnds || Single.IsNaN(distance)) { return GetPointAtDistance(distance); }
        return PointAtDistanceExtended(distance);
    }

    internal PathPoint PointAtDistanceExtended(double distance)
    {
        EnsureStarts();
        if (_contours.Length == 0 || (distance >= 0.0 && distance <= _total)) { return PointAtDistance(distance); }
        PathPoint end = PointAtDistance(distance < 0.0 ? 0.0 : _total);
        double beyond = distance < 0.0 ? distance : distance - _total;
        return new PathPoint(PathMath.P(end.Position.X + (end.Tangent.X * beyond), end.Position.Y + (end.Tangent.Y * beyond)),
            end.Tangent, (float)distance, end.ContourIndex);
    }

    /// <summary>
    /// Indicates whether a point lies inside the geometry's filled area under its own
    /// <see cref="FillRule"/> (non-zero winding or even-odd), every contour taken as closed - the
    /// area a filled <see cref="CodeBrix.Imaging.Drawing.Shapes.PathShape"/> paints. Curves are
    /// flattened within <paramref name="tolerance"/> first (so a point closer than that to a curved
    /// edge may be answered for the chord), and the test is single-precision arithmetic in a fixed
    /// order on those polylines - identical in both packages (a point exactly on an edge may fall
    /// either way). The flattened form is kept with the geometry, so asking many points of one
    /// geometry is cheap.
    /// </summary>
    /// <param name="point">The point, calibrated units.</param>
    /// <param name="tolerance">How closely curves are followed, calibrated units; must be positive.</param>
    /// <returns><c>true</c> when the point is inside; <c>false</c> for an empty geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    public bool Contains(PointF point, float tolerance = DefaultFlattenTolerance)
        => GetAreaQuery(tolerance).Contains(point.X, point.Y);

    /// <summary>
    /// The distance from a point to the nearest EDGE of the filled area (every contour taken as
    /// closed), whether the point is inside or outside - how deep inside, or how far away. Curves
    /// are flattened within <paramref name="tolerance"/>, so the answer is within that of the
    /// distance to the true curve; single-precision arithmetic in a fixed order, identical in both
    /// packages.
    /// </summary>
    /// <param name="point">The point, calibrated units.</param>
    /// <param name="tolerance">How closely curves are followed, calibrated units; must be positive.</param>
    /// <returns>The distance (0 on an edge); positive infinity for an empty geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    public float DistanceToEdge(PointF point, float tolerance = DefaultFlattenTolerance)
        => GetAreaQuery(tolerance).DistanceToEdge(point.X, point.Y);

    /// <summary>
    /// The signed distance from a point to the edge of the filled area: negative inside (by
    /// <see cref="Contains(PointF, float)"/>), positive outside - <see cref="DistanceToEdge(PointF, float)"/>
    /// with the sign of the side, the usual "signed distance field" convention.
    /// </summary>
    /// <param name="point">The point, calibrated units.</param>
    /// <param name="tolerance">How closely curves are followed, calibrated units; must be positive.</param>
    /// <returns>The signed distance; positive infinity for an empty geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    public float GetSignedDistance(PointF point, float tolerance = DefaultFlattenTolerance)
    {
        PathAreaQuery query = GetAreaQuery(tolerance);
        float distance = query.DistanceToEdge(point.X, point.Y);
        return query.Contains(point.X, point.Y) ? -distance : distance;
    }

    /// <summary>
    /// The point ON THE PATH nearest to a given point - on the contours as drawn (an open contour
    /// is not closed), curves flattened within <paramref name="tolerance"/>. Where several are
    /// equally near, the first along the contours wins.
    /// </summary>
    /// <param name="point">The point, calibrated units.</param>
    /// <param name="tolerance">How closely curves are followed, calibrated units; must be positive.</param>
    /// <returns>The nearest point on the path.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the geometry is empty.</exception>
    public PointF GetNearestPoint(PointF point, float tolerance = DefaultFlattenTolerance)
    {
        PathAreaQuery query = GetAreaQuery(tolerance);
        if (query.IsEmpty) { throw new InvalidOperationException("An empty geometry has no points."); }
        return query.NearestPoint(point.X, point.Y);
    }

    /// <summary>
    /// The smallest distance between this geometry's filled area and another's: 0 when they touch
    /// or overlap (one inside the other, or edges crossing), otherwise the gap between their
    /// nearest points - what spacing shapes "a fixed gap apart at their closest" needs. Both are
    /// flattened within <paramref name="tolerance"/>; single-precision arithmetic in a fixed
    /// order, identical in both packages.
    /// </summary>
    /// <param name="other">The other geometry.</param>
    /// <param name="tolerance">How closely curves are followed, calibrated units; must be positive.</param>
    /// <returns>The distance; positive infinity when either geometry is empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    public float Distance(PathGeometry other, float tolerance = DefaultFlattenTolerance)
        => Distance(other, new PointF(0f, 0f), tolerance);

    /// <summary>
    /// The distance between this geometry's filled area and another's, with the other one moved by
    /// <paramref name="otherOffset"/> - the question a spacing search asks over and over ("how far
    /// apart would they be if I put it there?") without building a moved geometry each time. See
    /// <see cref="Distance(PathGeometry, float)"/>.
    /// </summary>
    /// <param name="other">The other geometry.</param>
    /// <param name="otherOffset">How far the other geometry is taken to be moved, calibrated units.</param>
    /// <param name="tolerance">How closely curves are followed, calibrated units; must be positive.</param>
    /// <returns>The distance; positive infinity when either geometry is empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite, or the offset is not finite.</exception>
    public float Distance(PathGeometry other, PointF otherOffset, float tolerance = DefaultFlattenTolerance)
    {
        if (other == null) { throw new ArgumentNullException(nameof(other)); }
        if (!Single.IsFinite(otherOffset.X) || !Single.IsFinite(otherOffset.Y)) { throw new ArgumentOutOfRangeException(nameof(otherOffset)); }
        return GetAreaQuery(tolerance).DistanceTo(other.GetAreaQuery(tolerance), otherOffset.X, otherOffset.Y);
    }

    //The flattened form for point queries, kept for the last tolerance asked for
    internal PathAreaQuery GetAreaQuery(float tolerance)
    {
        if (!Single.IsFinite(tolerance) || tolerance <= 0f) { throw new ArgumentOutOfRangeException(nameof(tolerance)); }
        PathAreaQuery query = Volatile.Read(ref _areaQuery);
        if (query != null && query.Tolerance == tolerance) { return query; }
        query = new PathAreaQuery(this, tolerance);
        Volatile.Write(ref _areaQuery, query);
        return query;
    }

    /// <summary>
    /// The smallest axis-aligned rectangle around every contour, curves included exactly
    /// (their extreme points, not their control points), in calibrated units.
    /// </summary>
    /// <returns>The bounds; <see cref="RectangleF.Empty"/> for an empty geometry.</returns>
    public RectangleF GetBounds()
    {
        if (_contours.Length == 0) { return RectangleF.Empty; }
        double minX = Double.MaxValue, minY = Double.MaxValue, maxX = Double.MinValue, maxY = Double.MinValue;
        foreach (PathContour contour in _contours)
        {
            PathMath.Include(contour.StartPoint.X, contour.StartPoint.Y, ref minX, ref minY, ref maxX, ref maxY);
            foreach (PathSegment segment in contour.SegmentArray)
            {
                PathMath.ExtendBounds(PathMath.Seg.From(segment), ref minX, ref minY, ref maxX, ref maxY);
            }
        }
        return new RectangleF((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
    }

    /// <summary>
    /// The part of the geometry between two fractions of its total length, moved along by an
    /// offset - the "trim path" of animation tools, for revealing a stroke progressively
    /// (draw-on). Fractions are clamped to 0..1 and swapped when start is after end; the
    /// offset (any value; 1 = once round) shifts both, and a part pushed past the end wraps
    /// round to the start. Start 0, end 1 (any offset) returns this geometry unchanged, so a
    /// fully drawn closed contour stays closed; otherwise every piece is an open contour, and
    /// a wrapped part of a geometry that is ONE closed contour stays one piece across its start.
    /// A part with no length gives <see cref="Empty"/>.
    /// </summary>
    /// <param name="start">Where the kept part starts, 0..1.</param>
    /// <param name="end">Where it ends, 0..1.</param>
    /// <param name="offset">A shift of both, as a fraction of the length.</param>
    /// <returns>The trimmed geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is NaN or infinite.</exception>
    public PathGeometry Trim(float start, float end, float offset = 0f)
    {
        if (!Single.IsFinite(start)) { throw new ArgumentOutOfRangeException(nameof(start)); }
        if (!Single.IsFinite(end)) { throw new ArgumentOutOfRangeException(nameof(end)); }
        if (!Single.IsFinite(offset)) { throw new ArgumentOutOfRangeException(nameof(offset)); }
        double s = Math.Clamp((double)start, 0.0, 1.0), e = Math.Clamp((double)end, 0.0, 1.0);
        if (s > e) { (s, e) = (e, s); }
        if (s == 0.0 && e == 1.0) { return this; }
        double total = LengthValue;
        if (e - s <= 0.0 || total <= 0.0) { return Empty; }

        double shifted = s + offset;
        double from = shifted - Math.Floor(shifted);
        double to = from + (e - s);
        var contours = new List<PathContour>();
        if (to <= 1.0)
        {
            AddPart(contours, from * total, to * total, false);
        }
        else if (_contours.Length == 1 && _contours[0].IsClosed)
        {
            //One closed contour: the wrapped part runs on across the start as one piece
            PathContour.Measure measure = _contours[0].GetMeasure();
            List<PathMath.Seg> first = measure.Extract(from * total, total, false);
            first.AddRange(measure.Extract(0.0, (to - 1.0) * total, false));
            AddContour(contours, first, false);
        }
        else
        {
            AddPart(contours, from * total, total, false);
            AddPart(contours, 0.0, (to - 1.0) * total, false);
        }
        return new PathGeometry(contours.ToArray(), FillRule);
    }

    /// <summary>
    /// Splits every contour into dashes: <paramref name="pattern"/> alternates dash and gap
    /// lengths (calibrated units, starting with a dash), repeated along each contour from its
    /// start; <paramref name="offset"/> starts each contour that far into the pattern (SVG's
    /// <c>stroke-dashoffset</c>). A pattern with an odd number of entries is used twice over,
    /// as SVG does. Every dash is an open contour; a dash of zero length is kept as a single
    /// point, so a round or square cap draws it as a dot.
    /// </summary>
    /// <param name="pattern">Dash and gap lengths; none negative, at least one positive.</param>
    /// <param name="offset">How far into the pattern each contour starts, calibrated units.</param>
    /// <returns>The dashes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the pattern is empty, has a negative or non-finite entry, or sums to 0.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is not finite.</exception>
    public PathGeometry Dash(IReadOnlyList<float> pattern, float offset = 0f)
    {
        double[] intervals = PathStrokeStyle.NormalizeDashPattern(pattern, nameof(pattern));
        if (!Single.IsFinite(offset)) { throw new ArgumentOutOfRangeException(nameof(offset)); }
        double period = 0.0;
        foreach (double interval in intervals) { period += interval; }

        var contours = new List<PathContour>();
        foreach (PathContour contour in _contours)
        {
            PathContour.Measure measure = contour.GetMeasure();
            double length = measure.Total;
            double phase = offset - (Math.Floor(offset / period) * period);
            int index = 0;
            //An interval the phase reaches exactly is passed, unless it has no length (a dot)
            while (phase > intervals[index] || (phase == intervals[index] && intervals[index] > 0.0))
            {
                phase -= intervals[index];
                index = (index + 1) % intervals.Length;
            }
            double position = 0.0;
            double remaining = intervals[index] - phase;
            while (position <= length)
            {
                double next = position + remaining;
                if ((index & 1) == 0)
                {
                    double stop = Math.Min(next, length);
                    AddContour(contours, measure.Extract(position, stop, true), false);
                }
                if (next >= length) { break; }
                position = next;
                index = (index + 1) % intervals.Length;
                remaining = intervals[index];
            }
        }
        return new PathGeometry(contours.ToArray(), FillRule);
    }

    /// <summary>
    /// Flattens every contour into straight segments that stay within
    /// <paramref name="tolerance"/> of the curves (Wang's formula chooses how many uniform
    /// parameter steps each curve gets; lines keep their two points). The polylines are what a
    /// hard-edged mask or a polygon algorithm wants; a closed contour gives a closed polyline
    /// that does not repeat its start point.
    /// </summary>
    /// <param name="tolerance">The largest allowed distance between a curve and its chords, calibrated units; must be positive.</param>
    /// <returns>One polyline per contour, in order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    public IReadOnlyList<PathPolyline> Flatten(float tolerance = DefaultFlattenTolerance)
    {
        if (!Single.IsFinite(tolerance) || tolerance <= 0f) { throw new ArgumentOutOfRangeException(nameof(tolerance)); }
        var result = new PathPolyline[_contours.Length];
        for (int c = 0; c < _contours.Length; c++)
        {
            PathContour contour = _contours[c];
            var points = new List<PointF> { contour.StartPoint };
            foreach (PathMath.Seg s in contour.GetDrawnSegments())
            {
                if (s.Kind != PathSegmentKind.Line)
                {
                    int n = PathMath.Subdivisions(s, tolerance);
                    for (int k = 1; k < n; k++)
                    {
                        PathMath.Evaluate(s, k / (double)n, out double x, out double y);
                        points.Add(PathMath.P(x, y));
                    }
                }
                points.Add(PathMath.P(s.X3, s.Y3));
            }
            if (contour.IsClosed && points.Count > 1 && points[points.Count - 1].Equals(points[0]))
            {
                points.RemoveAt(points.Count - 1);
            }
            result[c] = new PathPolyline(points.ToArray(), contour.IsClosed, true);
        }
        return result;
    }

    /// <summary>
    /// The geometry with every point passed through a matrix (row-vector convention, as
    /// <see cref="CodeBrix.Imaging.Drawing.Models.DrawingTransform.ToMatrix"/> returns) - exact
    /// for curves, because an affine map of a Bezier curve is the curve of its mapped points.
    /// </summary>
    /// <param name="matrix">The transform.</param>
    /// <returns>A new geometry (this one for the identity).</returns>
    public PathGeometry Transformed(Matrix3x2 matrix)
    {
        if (matrix.IsIdentity) { return this; }
        var contours = new PathContour[_contours.Length];
        for (int c = 0; c < _contours.Length; c++)
        {
            PathContour contour = _contours[c];
            PathSegment[] source = contour.SegmentArray;
            var segments = new PathSegment[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                PathSegment s = source[i];
                segments[i] = new PathSegment(s.Kind, Map(s.Start, matrix), Map(s.Control1, matrix),
                    Map(s.Control2, matrix), Map(s.End, matrix));
            }
            contours[c] = new PathContour(Map(contour.StartPoint, matrix), segments, contour.IsClosed, true);
        }
        return new PathGeometry(contours, FillRule);
    }

    /// <summary>
    /// The geometry travelled the other way: each contour reversed (a closed contour keeps its
    /// start point, with its closing line made explicit), contours in the same order.
    /// </summary>
    /// <returns>A new geometry.</returns>
    public PathGeometry Reversed()
    {
        var contours = new PathContour[_contours.Length];
        for (int c = 0; c < _contours.Length; c++)
        {
            PathContour contour = _contours[c];
            PathMath.Seg[] drawn = contour.IsClosed ? contour.GetDrawnSegments() : Convert(contour.SegmentArray);
            var segments = new PathSegment[drawn.Length];
            for (int i = 0; i < drawn.Length; i++)
            {
                PathMath.Seg s = drawn[drawn.Length - 1 - i];
                segments[i] = new PathSegment(s.Kind, PathMath.P(s.X3, s.Y3), PathMath.P(s.X2, s.Y2),
                    PathMath.P(s.X1, s.Y1), PathMath.P(s.X0, s.Y0));
                if (s.Kind == PathSegmentKind.Quadratic)
                {
                    segments[i] = PathSegment.Quadratic(PathMath.P(s.X3, s.Y3), PathMath.P(s.X1, s.Y1), PathMath.P(s.X0, s.Y0));
                }
            }
            PointF start = contour.IsClosed ? contour.StartPoint : contour.EndPoint;
            contours[c] = new PathContour(start, segments, contour.IsClosed, true);
        }
        return new PathGeometry(contours, FillRule);
    }

    /// <summary>
    /// Everything this geometry or <paramref name="other"/> fills, as one outline - see
    /// <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/> for how the result is made.
    /// </summary>
    /// <param name="other">The other geometry.</param>
    /// <param name="tolerance">How closely curves are followed when they are flattened, calibrated units.</param>
    /// <returns>The union, as polygons.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry Union(PathGeometry other, float tolerance = DefaultFlattenTolerance)
        => CombineWith(other, PathBooleanOperation.Union, tolerance);

    /// <summary>Only what both this geometry and <paramref name="other"/> fill (see <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/>).</summary>
    /// <param name="other">The other geometry.</param>
    /// <param name="tolerance">How closely curves are followed when they are flattened, calibrated units.</param>
    /// <returns>The intersection, as polygons.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry Intersect(PathGeometry other, float tolerance = DefaultFlattenTolerance)
        => CombineWith(other, PathBooleanOperation.Intersection, tolerance);

    /// <summary>What this geometry fills less what <paramref name="other"/> fills - a cut-out (see <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/>).</summary>
    /// <param name="other">The geometry to take away.</param>
    /// <param name="tolerance">How closely curves are followed when they are flattened, calibrated units.</param>
    /// <returns>The difference, as polygons.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry Subtract(PathGeometry other, float tolerance = DefaultFlattenTolerance)
        => CombineWith(other, PathBooleanOperation.Difference, tolerance);

    /// <summary>What exactly one of this geometry and <paramref name="other"/> fills (see <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/>).</summary>
    /// <param name="other">The other geometry.</param>
    /// <param name="tolerance">How closely curves are followed when they are flattened, calibrated units.</param>
    /// <returns>The exclusive-or, as polygons.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry ExclusiveOr(PathGeometry other, float tolerance = DefaultFlattenTolerance)
        => CombineWith(other, PathBooleanOperation.ExclusiveOr, tolerance);

    /// <summary>
    /// Combines the AREAS this geometry and <paramref name="other"/> fill - each by its own
    /// <see cref="FillRule"/>, every contour taken as closed (an open contour fills as if closed,
    /// as when it is drawn filled) - into one outline. HOW: curves are flattened into chords
    /// within <paramref name="tolerance"/> (Wang's formula, as <see cref="Flatten(float)"/>), every
    /// point is rounded to the nearest 1/1024 of a unit, and the polygons are combined by the
    /// CodeBrix.PolygonTools clipping engine in exact integer arithmetic. The result is polygons
    /// (closed contours of straight lines, no curves) whose corners lie on that 1/1024 grid,
    /// non-overlapping, holes wound against their outlines, with the non-zero fill rule (even-odd
    /// fills them the same) - identical, bit for bit, in both packages. Coordinates must stay
    /// within a million units. To scale a result up a long way, combine at the larger size or
    /// pass a smaller tolerance, since the chords grow with it.
    /// </summary>
    /// <param name="other">The other geometry (the one taken away, for a difference).</param>
    /// <param name="operation">How to combine them.</param>
    /// <param name="tolerance">How closely curves are followed when they are flattened, calibrated units.</param>
    /// <returns>The combined area, as polygons; <see cref="Empty"/> when nothing is left.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the operation is undefined or <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry CombineWith(PathGeometry other, PathBooleanOperation operation, float tolerance = DefaultFlattenTolerance)
    {
        if (other == null) { throw new ArgumentNullException(nameof(other)); }
        if (!Enum.IsDefined(operation)) { throw new ArgumentOutOfRangeException(nameof(operation)); }
        return PathClipper.Combine(this, other, operation, CheckTolerance(tolerance));
    }

    /// <summary>
    /// Combines the areas of any number of geometries: <see cref="PathBooleanOperation.Union"/> -
    /// everything any of them fills; <see cref="PathBooleanOperation.Intersection"/> - only what all
    /// of them fill; <see cref="PathBooleanOperation.Difference"/> - the first less all the others;
    /// <see cref="PathBooleanOperation.ExclusiveOr"/> - what an odd number of them fill. Each one
    /// fills by its own fill rule; the result is made exactly as described for
    /// <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/>. One geometry gives its
    /// <see cref="Simplify(float)"/>; none gives <see cref="Empty"/>.
    /// </summary>
    /// <param name="geometries">The geometries, in order (the order matters for a difference only).</param>
    /// <param name="operation">How to combine them.</param>
    /// <param name="tolerance">How closely curves are followed when they are flattened, calibrated units.</param>
    /// <returns>The combined area, as polygons.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometries"/> or an entry is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the operation is undefined or <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public static PathGeometry CombineAll(IEnumerable<PathGeometry> geometries, PathBooleanOperation operation,
        float tolerance = DefaultFlattenTolerance)
    {
        if (geometries == null) { throw new ArgumentNullException(nameof(geometries)); }
        if (!Enum.IsDefined(operation)) { throw new ArgumentOutOfRangeException(nameof(operation)); }
        var list = new List<PathGeometry>();
        foreach (PathGeometry geometry in geometries)
        {
            list.Add(geometry ?? throw new ArgumentNullException(nameof(geometries), "A geometry is null."));
        }
        return PathClipper.CombineAll(operation, list, CheckTolerance(tolerance));
    }

    /// <summary>
    /// The area this geometry fills, by its fill rule, as clean polygons: self-intersections
    /// resolved and overlapping contours merged (REMOVE OVERLAPS - for example a font's glyph
    /// whose strokes are separate overlapping contours becomes one outline, so a centred stroke
    /// or a trim no longer traces the hidden overlaps). Made as described for
    /// <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/>.
    /// </summary>
    /// <param name="tolerance">How closely curves are followed when they are flattened, calibrated units.</param>
    /// <returns>The filled area, as polygons; <see cref="Empty"/> when it fills nothing.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry Simplify(float tolerance = DefaultFlattenTolerance)
        => PathClipper.Simplify(this, CheckTolerance(tolerance));

    /// <summary>
    /// STROKE TO OUTLINE: the area a stroke of this geometry in <paramref name="style"/> covers,
    /// as a fillable outline - width, caps (butt, round, square) and joins (miter within the
    /// miter limit and bevelled beyond it, as the stroke is drawn; round; bevel), the dash
    /// pattern cut first. Trim the geometry before (see <see cref="Trim(float, float, float)"/>,
    /// or <see cref="CodeBrix.Imaging.Drawing.Shapes.PathShape.GetStrokeOutline(float)"/>, which
    /// applies a shape's trim and dashes for you). Fill the result to draw the stroke as a shape -
    /// with a gradient, a mask, as a clip - or combine it with other areas. Curves are flattened
    /// first and the corner points inside a divided curve are joined round (the curve stays
    /// smooth whatever the join); round caps and joins are chords within the tolerance of the
    /// arcs. A zero-length open contour (a dot) gives a disc for round caps, an axis-aligned
    /// square for square caps, nothing for butt caps. Made as described for
    /// <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/> (one union of the pieces).
    /// </summary>
    /// <param name="style">The stroke's width, caps, joins, miter limit and dashes.</param>
    /// <param name="tolerance">How closely curves and arcs are followed, calibrated units.</param>
    /// <returns>The outline, as polygons; <see cref="Empty"/> when the stroke covers nothing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="style"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry ToStrokeOutline(PathStrokeStyle style, float tolerance = DefaultFlattenTolerance)
    {
        if (style == null) { throw new ArgumentNullException(nameof(style)); }
        return PathClipper.StrokeOutline(this, style, CheckTolerance(tolerance));
    }

    /// <summary>
    /// INFLATE: the area this geometry fills (by its fill rule) grown outward by
    /// <paramref name="distance"/> - or shrunk inward for a negative distance (see
    /// <see cref="Deflate(float, PathStrokeJoin, float, float)"/>). Corners that the growth (or, inside, the
    /// shrinking) rounds off take the join: round (the default - the exact offset), miter
    /// (sharp, but bevelled where the new corner's tip would lie more than
    /// <paramref name="miterLimit"/> times the distance from the old corner), or bevel. Made by uniting (or taking away) the
    /// outline of the fill's boundary stroked to twice the distance, so joins are exactly a
    /// stroke's; the boundary is already polygons by then, so its every corner takes the join
    /// (on a flattened curve those corners are tiny and look smooth). Holes shrink as the fill
    /// grows; parts thinner than twice the distance vanish when deflating. Made as described for
    /// <see cref="CombineWith(PathGeometry, PathBooleanOperation, float)"/>.
    /// </summary>
    /// <param name="distance">How far to grow, calibrated units; negative shrinks, 0 gives <see cref="Simplify(float)"/>.</param>
    /// <param name="join">How grown corners are shaped.</param>
    /// <param name="miterLimit">For miter joins: how far a corner's tip may reach, as a multiple of the distance (at least 1).</param>
    /// <param name="tolerance">How closely curves and round corners are followed, calibrated units.</param>
    /// <returns>The grown or shrunk area, as polygons; <see cref="Empty"/> when nothing is left.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite, the miter limit is below 1, the join is undefined or the tolerance is not positive.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry Inflate(float distance, PathStrokeJoin join = PathStrokeJoin.Round, float miterLimit = 4f,
        float tolerance = DefaultFlattenTolerance)
    {
        if (!Single.IsFinite(distance)) { throw new ArgumentOutOfRangeException(nameof(distance)); }
        if (!Enum.IsDefined(join)) { throw new ArgumentOutOfRangeException(nameof(join)); }
        if (!Single.IsFinite(miterLimit) || miterLimit < 1f) { throw new ArgumentOutOfRangeException(nameof(miterLimit)); }
        return PathClipper.Inflate(this, distance, join, miterLimit, CheckTolerance(tolerance));
    }

    /// <summary>
    /// DEFLATE: the area this geometry fills shrunk inward by <paramref name="distance"/> - the
    /// same as <see cref="Inflate(float, PathStrokeJoin, float, float)"/> with the distance negated
    /// (inside corners take the join; parts thinner than twice the distance vanish).
    /// </summary>
    /// <param name="distance">How far to shrink, calibrated units; negative grows.</param>
    /// <param name="join">How the shrunk inside corners are shaped.</param>
    /// <param name="miterLimit">For miter joins: how far a corner's tip may reach, as a multiple of the distance (at least 1).</param>
    /// <param name="tolerance">How closely curves and round corners are followed, calibrated units.</param>
    /// <returns>The shrunk area, as polygons; <see cref="Empty"/> when nothing is left.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite, the miter limit is below 1, the join is undefined or the tolerance is not positive.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate lies beyond a million units.</exception>
    public PathGeometry Deflate(float distance, PathStrokeJoin join = PathStrokeJoin.Round, float miterLimit = 4f,
        float tolerance = DefaultFlattenTolerance)
        => Inflate(-distance, join, miterLimit, tolerance);

    private static double CheckTolerance(float tolerance)
    {
        if (!Single.IsFinite(tolerance) || tolerance <= 0f) { throw new ArgumentOutOfRangeException(nameof(tolerance)); }
        return tolerance;
    }

    /// <summary>All the contours of several geometries, in order, with the first one's fill rule.</summary>
    /// <param name="geometries">The geometries.</param>
    /// <returns>The combined geometry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometries"/> or an entry is null.</exception>
    public static PathGeometry Combine(params PathGeometry[] geometries)
    {
        if (geometries == null) { throw new ArgumentNullException(nameof(geometries)); }
        var contours = new List<PathContour>();
        PathFillRule rule = PathFillRule.NonZero;
        for (int i = 0; i < geometries.Length; i++)
        {
            PathGeometry geometry = geometries[i] ?? throw new ArgumentNullException(nameof(geometries), "A geometry is null.");
            if (i == 0) { rule = geometry.FillRule; }
            contours.AddRange(geometry._contours);
        }
        return new PathGeometry(contours.ToArray(), rule);
    }

    /// <summary>A straight line as an open geometry.</summary>
    /// <param name="start">The start point.</param>
    /// <param name="end">The end point.</param>
    /// <returns>The geometry.</returns>
    public static PathGeometry Line(PointF start, PointF end) => new PathGeometryBuilder().MoveTo(start).LineTo(end).Build();

    /// <summary>A polyline or polygon through points.</summary>
    /// <param name="points">The points; at least two.</param>
    /// <param name="closed"><c>true</c> for a closed polygon.</param>
    /// <returns>The geometry.</returns>
    public static PathGeometry FromPoints(IReadOnlyList<PointF> points, bool closed)
        => new PathGeometryBuilder().AddPolygon(points, closed).Build();

    /// <summary>A rectangle, clockwise from its top-left corner.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The geometry.</returns>
    public static PathGeometry Rectangle(RectangleF rectangle) => new PathGeometryBuilder().AddRectangle(rectangle).Build();

    /// <summary>A rectangle with circular corners (radius limited to half the shorter side).</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <param name="cornerRadius">The corner radius.</param>
    /// <returns>The geometry.</returns>
    public static PathGeometry RoundedRectangle(RectangleF rectangle, float cornerRadius)
        => new PathGeometryBuilder().AddRoundedRectangle(rectangle, cornerRadius).Build();

    /// <summary>An ellipse of four cubic curves, clockwise from its rightmost point.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="radiusX">The horizontal radius.</param>
    /// <param name="radiusY">The vertical radius.</param>
    /// <returns>The geometry.</returns>
    public static PathGeometry Ellipse(PointF center, float radiusX, float radiusY)
        => new PathGeometryBuilder().AddEllipse(center, radiusX, radiusY).Build();

    /// <summary>A circle of four cubic curves, clockwise from its rightmost point.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="radius">The radius.</param>
    /// <returns>The geometry.</returns>
    public static PathGeometry Circle(PointF center, float radius) => Ellipse(center, radius, radius);

    /// <summary>
    /// A regular polygon with its first corner straight above the centre (turned clockwise by
    /// <paramref name="rotation"/> degrees), corners clockwise.
    /// </summary>
    /// <param name="center">The centre.</param>
    /// <param name="radius">The distance from the centre to each corner.</param>
    /// <param name="sides">The number of sides; at least 3.</param>
    /// <param name="rotation">A clockwise turn, degrees.</param>
    /// <returns>The geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sides"/> is below 3 or a value is not finite.</exception>
    public static PathGeometry RegularPolygon(PointF center, float radius, int sides, float rotation = 0f)
    {
        if (sides < 3) { throw new ArgumentOutOfRangeException(nameof(sides)); }
        return FromPoints(CornerPoints(center, radius, radius, sides, rotation), true);
    }

    /// <summary>
    /// A star: <paramref name="points"/> tips at <paramref name="outerRadius"/> alternating with
    /// inner corners at <paramref name="innerRadius"/>, the first tip straight above the centre
    /// (turned clockwise by <paramref name="rotation"/> degrees), clockwise.
    /// </summary>
    /// <param name="center">The centre.</param>
    /// <param name="outerRadius">The distance to each tip.</param>
    /// <param name="innerRadius">The distance to each inner corner.</param>
    /// <param name="points">The number of tips; at least 2.</param>
    /// <param name="rotation">A clockwise turn, degrees.</param>
    /// <returns>The geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="points"/> is below 2 or a value is not finite.</exception>
    public static PathGeometry Star(PointF center, float outerRadius, float innerRadius, int points, float rotation = 0f)
    {
        if (points < 2) { throw new ArgumentOutOfRangeException(nameof(points)); }
        return FromPoints(CornerPoints(center, outerRadius, innerRadius, points * 2, rotation), true);
    }

    /// <summary>
    /// A closed polygon whose corners are rounded with circular arcs of
    /// <paramref name="cornerRadius"/> - tangent to both edges at each corner, the radius made
    /// smaller where an edge is too short to hold two such roundings (each rounding then takes
    /// at most half of its edges). Corners of a straight run are kept sharp.
    /// </summary>
    /// <param name="points">The polygon's corners, in order; at least three.</param>
    /// <param name="cornerRadius">The rounding radius, calibrated units; 0 for sharp corners.</param>
    /// <returns>The geometry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fewer than three points are given.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is negative or not finite.</exception>
    public static PathGeometry RoundedPolygon(IReadOnlyList<PointF> points, float cornerRadius)
    {
        if (points == null) { throw new ArgumentNullException(nameof(points)); }
        if (points.Count < 3) { throw new ArgumentException("A polygon needs at least three points.", nameof(points)); }
        if (!Single.IsFinite(cornerRadius) || cornerRadius < 0f) { throw new ArgumentOutOfRangeException(nameof(cornerRadius)); }
        if (cornerRadius == 0f) { return FromPoints(points, true); }

        int n = points.Count;
        var builder = new PathGeometryBuilder();
        var entries = new PointF[n];
        var exits = new PointF[n];
        var controlsIn = new PointF[n];
        var controlsOut = new PointF[n];
        var rounded = new bool[n];
        for (int i = 0; i < n; i++)
        {
            PointF prev = points[(i + n - 1) % n], at = points[i], next = points[(i + 1) % n];
            double ax = prev.X - at.X, ay = prev.Y - at.Y, bx = next.X - at.X, by = next.Y - at.Y;
            double la = PathMath.Length(ax, ay), lb = PathMath.Length(bx, by);
            entries[i] = at;
            exits[i] = at;
            if (la <= 0.0 || lb <= 0.0) { continue; }
            double cos = Math.Clamp(((ax * bx) + (ay * by)) / (la * lb), -1.0, 1.0);
            double interior = Math.Acos(cos);
            if (interior <= 1e-6 || Math.PI - interior <= 1e-6) { continue; }
            double half = interior / 2.0;
            double tangent = cornerRadius / Math.Tan(half);
            tangent = Math.Min(tangent, Math.Min(la, lb) / 2.0);
            double radius = tangent * Math.Tan(half);
            double turn = Math.PI - interior;
            double handle = 4.0 / 3.0 * Math.Tan(turn / 4.0) * radius;
            double ux = ax / la, uy = ay / la, vx = bx / lb, vy = by / lb;
            entries[i] = PathMath.P(at.X + (ux * tangent), at.Y + (uy * tangent));
            exits[i] = PathMath.P(at.X + (vx * tangent), at.Y + (vy * tangent));
            controlsIn[i] = PathMath.P(entries[i].X - (ux * handle), entries[i].Y - (uy * handle));
            controlsOut[i] = PathMath.P(exits[i].X - (vx * handle), exits[i].Y - (vy * handle));
            rounded[i] = true;
        }
        builder.MoveTo(exits[0]);
        for (int k = 1; k <= n; k++)
        {
            int i = k % n;
            builder.LineTo(entries[i]);
            if (rounded[i]) { builder.CubicTo(controlsIn[i], controlsOut[i], exits[i]); }
        }
        builder.Close();
        return builder.Build();
    }

    /// <summary>
    /// Reads SVG path data (the <c>d</c> attribute of an SVG <c>path</c>): the M, L, H, V, C, S,
    /// Q, T, A and Z commands in absolute (upper-case) and relative (lower-case) form, with
    /// implicitly repeated commands and the compact number forms SVG allows ("1-2", ".5.5",
    /// "1e3", arc flags written without separators).
    /// </summary>
    /// <param name="svgPathData">The path data.</param>
    /// <returns>The geometry (non-zero fill rule).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="svgPathData"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the data is not valid path data (the message gives the position).</exception>
    public static PathGeometry Parse(string svgPathData)
    {
        if (svgPathData == null) { throw new ArgumentNullException(nameof(svgPathData)); }
        return SvgPathDataParser.Parse(svgPathData);
    }

    /// <summary>Reads SVG path data, reporting failure instead of throwing (see <see cref="Parse(string)"/>).</summary>
    /// <param name="svgPathData">The path data.</param>
    /// <param name="geometry">The geometry; <c>null</c> when the data is not valid.</param>
    /// <returns><c>true</c> when the data was read.</returns>
    public static bool TryParse(string svgPathData, out PathGeometry geometry)
    {
        geometry = null;
        if (svgPathData == null) { return false; }
        try
        {
            geometry = SvgPathDataParser.Parse(svgPathData);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the geometry as SVG path data with absolute M, L, Q, C and Z commands and
    /// round-trip number formatting, so <see cref="Parse(string)"/> reads back the same points.
    /// </summary>
    /// <returns>The path data; an empty string for an empty geometry.</returns>
    public string ToSvgPathData()
    {
        var text = new StringBuilder();
        foreach (PathContour contour in _contours)
        {
            if (text.Length > 0) { text.Append(' '); }
            text.Append('M');
            Append(text, contour.StartPoint);
            foreach (PathSegment s in contour.SegmentArray)
            {
                switch (s.Kind)
                {
                    case PathSegmentKind.Line:
                        text.Append(" L");
                        Append(text, s.End);
                        break;
                    case PathSegmentKind.Quadratic:
                        text.Append(" Q");
                        Append(text, s.Control1);
                        text.Append(',');
                        Append(text, s.End);
                        break;
                    default:
                        text.Append(" C");
                        Append(text, s.Control1);
                        text.Append(',');
                        Append(text, s.Control2);
                        text.Append(',');
                        Append(text, s.End);
                        break;
                }
            }
            if (contour.IsClosed) { text.Append(" Z"); }
        }
        return text.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => $"PathGeometry ({_contours.Length} contours, length {GetLength()})";

#if NOSKIA
    /// <summary>
    /// Reads a backend path (for example one built with a <see cref="DrawingPathBuilder"/>):
    /// its moves, lines, quadratic and cubic curves and closes, and its fill type.
    /// </summary>
    /// <param name="path">The path to read.</param>
    /// <returns>The equivalent geometry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    public static PathGeometry FromDrawingPath(DrawingPath path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        var builder = new PathGeometryBuilder
        {
            FillRule = path.FillType == DrawingPathFillType.EvenOdd ? PathFillRule.EvenOdd : PathFillRule.NonZero,
        };
        foreach (DrawingPathSegment segment in path.GetVerbs())
        {
            DrawingPoint[] p = segment.Points;
            switch (segment.Verb)
            {
                case DrawingPathVerb.Move: builder.MoveTo(ToPoint(p[0])); break;
                case DrawingPathVerb.Line: builder.LineTo(ToPoint(p[0])); break;
                case DrawingPathVerb.Quad: builder.QuadraticTo(ToPoint(p[0]), ToPoint(p[1])); break;
                case DrawingPathVerb.Cubic: builder.CubicTo(ToPoint(p[0]), ToPoint(p[1]), ToPoint(p[2])); break;
                case DrawingPathVerb.Close: builder.Close(); break;
            }
        }
        return builder.Build();
    }

    /// <summary>Builds a backend path holding this geometry (the caller disposes it).</summary>
    /// <returns>A new path.</returns>
    public DrawingPath ToDrawingPath() => ToBackendPath();
#else
    /// <summary>
    /// Reads a SkiaSharp path: its moves, lines, quadratic, conic (converted to quadratic
    /// curves) and cubic curves and closes, and its fill type (inverse fill types read as their
    /// plain counterparts).
    /// </summary>
    /// <param name="path">The path to read.</param>
    /// <returns>The equivalent geometry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    public static PathGeometry FromSkiaPath(SKPath path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        var builder = new PathGeometryBuilder
        {
            FillRule = path.FillType == SKPathFillType.EvenOdd || path.FillType == SKPathFillType.InverseEvenOdd
                ? PathFillRule.EvenOdd : PathFillRule.NonZero,
        };
        using SKPath.RawIterator iterator = path.CreateRawIterator();
        var p = new SKPoint[4];
        SKPathVerb verb;
        while ((verb = iterator.Next(p)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move: builder.MoveTo(ToPoint(p[0])); break;
                case SKPathVerb.Line: builder.LineTo(ToPoint(p[1])); break;
                case SKPathVerb.Quad: builder.QuadraticTo(ToPoint(p[1]), ToPoint(p[2])); break;
                case SKPathVerb.Conic:
                    SKPoint[] quads = SKPath.ConvertConicToQuads(p[0], p[1], p[2], iterator.ConicWeight(), 3);
                    for (int i = 1; i + 1 < quads.Length; i += 2)
                    {
                        builder.QuadraticTo(ToPoint(quads[i]), ToPoint(quads[i + 1]));
                    }
                    break;
                case SKPathVerb.Cubic: builder.CubicTo(ToPoint(p[1]), ToPoint(p[2]), ToPoint(p[3])); break;
                case SKPathVerb.Close: builder.Close(); break;
            }
        }
        return builder.Build();
    }

    /// <summary>Builds a SkiaSharp path holding this geometry (the caller disposes it).</summary>
    /// <returns>A new path.</returns>
    public SKPath ToSkiaPath() => ToBackendPath();
#endif

    //The backend path this library draws: every contour, with its closing, and the fill rule
    internal SKPath ToBackendPath()
    {
        var builder = new SKPathBuilder();
        foreach (PathContour contour in _contours)
        {
            builder.MoveTo(ToBackend(contour.StartPoint));
            foreach (PathSegment s in contour.SegmentArray)
            {
                switch (s.Kind)
                {
                    case PathSegmentKind.Line: builder.LineTo(ToBackend(s.End)); break;
                    case PathSegmentKind.Quadratic: builder.QuadTo(ToBackend(s.Control1), ToBackend(s.End)); break;
                    default: builder.CubicTo(ToBackend(s.Control1), ToBackend(s.Control2), ToBackend(s.End)); break;
                }
            }
            if (contour.IsClosed) { builder.Close(); }
        }
        SKPath path = builder.Detach();
        path.FillType = FillRule == PathFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        return path;
    }

    private static SKPoint ToBackend(PointF point) => new SKPoint(point.X, point.Y);

    private static PointF ToPoint(SKPoint point) => new PointF(point.X, point.Y);

    private void EnsureStarts()
    {
        if (_total >= 0.0) { return; }
        var starts = new double[_contours.Length];
        double total = 0.0;
        for (int i = 0; i < _contours.Length; i++)
        {
            starts[i] = total;
            total += _contours[i].LengthValue;
        }
        _contourStarts = starts;
        _total = total;
    }

    //Adds the pieces of the geometry between two distances along the whole of it: a contour
    //  covered completely is kept as it is (closed contours stay closed), others become open
    private void AddPart(List<PathContour> contours, double from, double to, bool keepZeroLength)
    {
        EnsureStarts();
        for (int i = 0; i < _contours.Length; i++)
        {
            double start = _contourStarts[i];
            double length = _contours[i].LengthValue;
            double end = start + length;
            if (end < from || start > to) { continue; }
            if (end == from && length > 0.0) { continue; }
            if (start == to && length > 0.0) { continue; }
            if (from <= start && to >= end)
            {
                contours.Add(_contours[i]);
                continue;
            }
            List<PathMath.Seg> piece = _contours[i].GetMeasure().Extract(from - start, to - start, keepZeroLength);
            AddContour(contours, piece, false);
        }
    }

    private static void AddContour(List<PathContour> contours, List<PathMath.Seg> piece, bool closed)
    {
        if (piece.Count == 0) { return; }
        var segments = new PathSegment[piece.Count];
        for (int i = 0; i < piece.Count; i++)
        {
            PathMath.Seg s = piece[i];
            PathSegment segment = s.ToSegment();
            //Chain exactly: every segment starts at the previous one's (float) end
            if (i > 0) { segment = new PathSegment(segment.Kind, segments[i - 1].End, segment.Control1, segment.Control2, segment.End); }
            segments[i] = segment;
        }
        contours.Add(new PathContour(segments[0].Start, segments, closed, true));
    }

    private static PathMath.Seg[] Convert(PathSegment[] segments)
    {
        var result = new PathMath.Seg[segments.Length];
        for (int i = 0; i < segments.Length; i++) { result[i] = PathMath.Seg.From(segments[i]); }
        return result;
    }

    private static PointF Map(PointF p, Matrix3x2 m)
        => PathMath.P((p.X * (double)m.M11) + (p.Y * (double)m.M21) + m.M31,
            (p.X * (double)m.M12) + (p.Y * (double)m.M22) + m.M32);

    private static PointF[] CornerPoints(PointF center, float outer, float inner, int count, float rotation)
    {
        if (!Single.IsFinite(outer) || !Single.IsFinite(inner) || !Single.IsFinite(rotation) || !Single.IsFinite(center.X) || !Single.IsFinite(center.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(center), "Every value must be finite.");
        }
        var points = new PointF[count];
        double first = (rotation - 90.0) * (Math.PI / 180.0);
        for (int i = 0; i < count; i++)
        {
            double angle = first + (2.0 * Math.PI * i / count);
            double radius = (i & 1) == 0 ? outer : inner;
            points[i] = PathMath.P(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)));
        }
        return points;
    }

    private static void Append(StringBuilder text, PointF point)
    {
        text.Append(point.X.ToString("R", CultureInfo.InvariantCulture));
        text.Append(',');
        text.Append(point.Y.ToString("R", CultureInfo.InvariantCulture));
    }
}
