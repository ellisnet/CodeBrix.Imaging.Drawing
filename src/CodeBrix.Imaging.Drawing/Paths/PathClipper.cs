using System;
using System.Collections.Generic;
using CodeBrix.PolygonTools;
using CodeBrix.PolygonTools.Enumerations;
using CodeBrix.PolygonTools.Models;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// The polygon side of the path tools: boolean combination, simplification, stroke outlining
/// and inflating, done by the CodeBrix.PolygonTools clipping engine in exact 64-bit integer
/// arithmetic. Curves are flattened (Wang's formula, the caller's tolerance) in double
/// precision, every point is rounded once to a grid of <see cref="GridScale"/> steps per
/// calibrated unit, and results come back as closed polygons whose corners are those grid
/// points - exactly representable as floats - so both packages produce identical output.
/// </summary>
internal static class PathClipper
{
    //Grid steps per calibrated unit; a power of two, so a grid point divides back to an exact float
    internal const double GridScale = 1024.0;

    //The largest coordinate magnitude accepted, calibrated units (keeps the engine inside its
    //  fast, artifact-free 64-bit range with room to spare)
    internal const double MaxCoordinate = 1000000.0;

    private const long MaxGrid = (long)(MaxCoordinate * GridScale);

    //The two-geometry boolean operations
    internal static PathGeometry Combine(PathGeometry subject, PathGeometry clip, PathBooleanOperation operation, double tolerance)
    {
        var engine = new PolyClip();
        engine.AddPaths(ToPolygons(subject, tolerance), PolyType.ptSubject, true);
        engine.AddPaths(ToPolygons(clip, tolerance), PolyType.ptClip, true);
        var solution = new List<List<IntPoint>>();
        engine.Execute(ToClipType(operation), solution, ToFillType(subject.FillRule), ToFillType(clip.FillRule));
        return ToGeometry(solution);
    }

    //The area a geometry fills, as non-overlapping polygons (holes wound against their outlines)
    internal static List<List<IntPoint>> Region(PathGeometry geometry, double tolerance)
    {
        var engine = new PolyClip();
        engine.AddPaths(ToPolygons(geometry, tolerance), PolyType.ptSubject, true);
        var solution = new List<List<IntPoint>>();
        engine.Execute(ClipType.ctUnion, solution, ToFillType(geometry.FillRule), ToFillType(geometry.FillRule));
        return solution;
    }

    internal static PathGeometry Simplify(PathGeometry geometry, double tolerance) => ToGeometry(Region(geometry, tolerance));

    //Many geometries: union (anything covered), intersection (covered by all), difference (the
    //  first less all the others) or exclusive-or (covered an odd number of times)
    internal static PathGeometry CombineAll(PathBooleanOperation operation, IReadOnlyList<PathGeometry> geometries, double tolerance)
    {
        if (geometries.Count == 0) { return PathGeometry.Empty; }
        if (operation == PathBooleanOperation.Union || operation == PathBooleanOperation.Difference)
        {
            int start = operation == PathBooleanOperation.Union ? 0 : 1;
            var others = new List<List<IntPoint>>();
            for (int i = start; i < geometries.Count; i++) { others.AddRange(Region(geometries[i], tolerance)); }
            var engine = new PolyClip();
            if (operation == PathBooleanOperation.Union)
            {
                engine.AddPaths(others, PolyType.ptSubject, true);
            }
            else
            {
                engine.AddPaths(Region(geometries[0], tolerance), PolyType.ptSubject, true);
                engine.AddPaths(others, PolyType.ptClip, true);
            }
            var solution = new List<List<IntPoint>>();
            engine.Execute(operation == PathBooleanOperation.Union ? ClipType.ctUnion : ClipType.ctDifference, solution,
                PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return ToGeometry(solution);
        }

        List<List<IntPoint>> result = Region(geometries[0], tolerance);
        for (int i = 1; i < geometries.Count; i++)
        {
            var engine = new PolyClip();
            engine.AddPaths(result, PolyType.ptSubject, true);
            engine.AddPaths(ToPolygons(geometries[i], tolerance), PolyType.ptClip, true);
            var solution = new List<List<IntPoint>>();
            engine.Execute(ToClipType(operation), solution, PolyFillType.pftNonZero, ToFillType(geometries[i].FillRule));
            result = solution;
        }
        return ToGeometry(result);
    }

    //The area a stroke of this style covers (dashes cut first), as polygons
    internal static PathGeometry StrokeOutline(PathGeometry geometry, PathStrokeStyle style, double tolerance)
    {
        PathGeometry source = style.IsDashed ? geometry.Dash(style.DashPatternValue, style.DashOffset) : geometry;
        var stroker = new Stroker(style.Width / 2.0, style.Cap, style.Join, style.MiterLimit, tolerance);
        foreach (PathContour contour in source.Contours)
        {
            stroker.AddContour(Flatten(contour, tolerance), contour.IsClosed);
        }
        return ToGeometry(stroker.Union());
    }

    //The fill grown (distance > 0) or shrunk (distance < 0) by a distance, corners per the join
    internal static PathGeometry Inflate(PathGeometry geometry, double distance, PathStrokeJoin join, double miterLimit, double tolerance)
    {
        List<List<IntPoint>> region = Region(geometry, tolerance);
        if (distance == 0.0 || region.Count == 0) { return ToGeometry(region); }

        //The boundary stroked to twice the distance: added to the region it grows it, taken away
        //  it shrinks it - with exactly a stroke's join geometry at every corner
        var stroker = new Stroker(Math.Abs(distance), PathStrokeCap.Butt, join, miterLimit, tolerance);
        foreach (List<IntPoint> polygon in region)
        {
            var points = new List<FlatPoint>(polygon.Count);
            foreach (IntPoint p in polygon) { points.Add(new FlatPoint(p.X / GridScale, p.Y / GridScale, false)); }
            stroker.AddContour(points, true);
        }
        var engine = new PolyClip();
        engine.AddPaths(region, PolyType.ptSubject, true);
        engine.AddPaths(stroker.Pieces, PolyType.ptClip, true);
        var solution = new List<List<IntPoint>>();
        engine.Execute(distance > 0.0 ? ClipType.ctUnion : ClipType.ctDifference, solution,
            PolyFillType.pftNonZero, PolyFillType.pftNonZero);
        return ToGeometry(solution);
    }

    private static ClipType ToClipType(PathBooleanOperation operation) => operation switch
    {
        PathBooleanOperation.Intersection => ClipType.ctIntersection,
        PathBooleanOperation.Difference => ClipType.ctDifference,
        PathBooleanOperation.ExclusiveOr => ClipType.ctXor,
        _ => ClipType.ctUnion,
    };

    private static PolyFillType ToFillType(PathFillRule rule)
        => rule == PathFillRule.EvenOdd ? PolyFillType.pftEvenOdd : PolyFillType.pftNonZero;

    //Every contour as a closed polygon on the grid (an open contour fills as if closed)
    private static List<List<IntPoint>> ToPolygons(PathGeometry geometry, double tolerance)
    {
        var polygons = new List<List<IntPoint>>(geometry.Contours.Count);
        foreach (PathContour contour in geometry.Contours)
        {
            List<FlatPoint> points = Flatten(contour, tolerance);
            var polygon = new List<IntPoint>(points.Count);
            foreach (FlatPoint p in points) { AddDistinct(polygon, ToGrid(p.X, p.Y)); }
            if (polygon.Count > 1 && polygon[polygon.Count - 1] == polygon[0]) { polygon.RemoveAt(polygon.Count - 1); }
            if (polygon.Count >= 3) { polygons.Add(polygon); }
        }
        return polygons;
    }

    //A contour as double points: its corners, and the points between them where a curve was
    //  divided (marked smooth). A closed contour does not repeat its start
    internal static List<FlatPoint> Flatten(PathContour contour, double tolerance)
    {
        var points = new List<FlatPoint> { new FlatPoint(contour.StartPoint.X, contour.StartPoint.Y, false) };
        foreach (PathMath.Seg s in contour.GetDrawnSegments())
        {
            if (s.Kind != PathSegmentKind.Line)
            {
                int n = PathMath.Subdivisions(s, tolerance);
                for (int k = 1; k < n; k++)
                {
                    PathMath.Evaluate(s, k / (double)n, out double x, out double y);
                    points.Add(new FlatPoint(x, y, true));
                }
            }
            points.Add(new FlatPoint(s.X3, s.Y3, false));
        }
        if (contour.IsClosed && points.Count > 1 && points[points.Count - 1].X == points[0].X && points[points.Count - 1].Y == points[0].Y)
        {
            points.RemoveAt(points.Count - 1);
        }
        return points;
    }

    internal static IntPoint ToGrid(double x, double y)
    {
        long gx = (long)Math.Round(x * GridScale, MidpointRounding.AwayFromZero);
        long gy = (long)Math.Round(y * GridScale, MidpointRounding.AwayFromZero);
        if (Math.Abs(gx) > MaxGrid || Math.Abs(gy) > MaxGrid || Double.IsNaN(x) || Double.IsNaN(y))
        {
            throw new ArgumentException(
                $"A path coordinate ({x}, {y}) lies beyond the {MaxCoordinate} units the polygon tools accept.");
        }
        return new IntPoint(gx, gy);
    }

    private static void AddDistinct(List<IntPoint> polygon, IntPoint point)
    {
        if (polygon.Count == 0 || polygon[polygon.Count - 1] != point) { polygon.Add(point); }
    }

    //Grid polygons back to a geometry: one closed contour of lines per polygon, non-zero rule
    private static PathGeometry ToGeometry(List<List<IntPoint>> polygons)
    {
        var contours = new List<PathContour>(polygons.Count);
        foreach (List<IntPoint> polygon in polygons)
        {
            if (polygon.Count < 3) { continue; }
            var segments = new PathSegment[polygon.Count - 1];
            PointF previous = ToPoint(polygon[0]);
            for (int i = 1; i < polygon.Count; i++)
            {
                PointF next = ToPoint(polygon[i]);
                segments[i - 1] = PathSegment.Line(previous, next);
                previous = next;
            }
            contours.Add(new PathContour(ToPoint(polygon[0]), segments, true, true));
        }
        return contours.Count == 0 ? PathGeometry.Empty : new PathGeometry(contours.ToArray(), PathFillRule.NonZero);
    }

    private static PointF ToPoint(IntPoint p) => new PointF((float)(p.X / GridScale), (float)(p.Y / GridScale));

    internal readonly struct FlatPoint
    {
        internal FlatPoint(double x, double y, bool smooth)
        {
            X = x;
            Y = y;
            Smooth = smooth;
        }

        internal double X { get; }

        internal double Y { get; }

        //A point inside a divided curve (its corner is drawn round, whatever the join)
        internal bool Smooth { get; }
    }

    //Builds a stroke as overlapping simple pieces - a quadrilateral per segment, a wedge per
    //  corner on its outer side (bevel triangle, miter point within the limit, or round fan),
    //  caps at the ends of open contours, dots for zero-length ones - and unites them. Pieces
    //  that meet share EXACT edges: every point two pieces have in common is computed once, and
    //  each segment's end edges run through the grid point of the contour point itself (the
    //  apex of the corner wedges and caps there), so rounding never leaves a hairline gap.
    //  The union of the segment quadrilaterals and the outer corner wedges is exactly the set of
    //  points within half the width of the polyline for round joins (a point nearest a corner
    //  lies in that corner's outer cone), and the bevel/miter variants of it otherwise
    private sealed class Stroker
    {
        private readonly double _half;
        private readonly PathStrokeCap _cap;
        private readonly PathStrokeJoin _join;
        private readonly double _miterLimit;
        private readonly double _arcStep;

        internal Stroker(double halfWidth, PathStrokeCap cap, PathStrokeJoin join, double miterLimit, double tolerance)
        {
            _half = halfWidth;
            _cap = cap;
            _join = join;
            _miterLimit = miterLimit;
            //Chords of an arc of radius r stay within the tolerance when each spans 2 acos(1 - tol / r)
            int steps = tolerance < halfWidth
                ? (int)Math.Ceiling(2.0 * Math.PI / (2.0 * Math.Acos(1.0 - (tolerance / halfWidth))))
                : 8;
            _arcStep = 2.0 * Math.PI / Math.Clamp(steps, 8, 4096);
        }

        internal List<List<IntPoint>> Pieces { get; } = new List<List<IntPoint>>();

        internal void AddContour(List<FlatPoint> source, bool closed)
        {
            //Drop repeated points (a corner wins over a smooth point at the same place)
            var points = new List<FlatPoint>(source.Count);
            foreach (FlatPoint p in source)
            {
                if (points.Count > 0 && points[points.Count - 1].X == p.X && points[points.Count - 1].Y == p.Y)
                {
                    if (!p.Smooth) { points[points.Count - 1] = p; }
                    continue;
                }
                points.Add(p);
            }
            if (closed && points.Count > 1 && points[points.Count - 1].X == points[0].X && points[points.Count - 1].Y == points[0].Y)
            {
                points.RemoveAt(points.Count - 1);
            }
            if (points.Count == 0) { return; }
            if (points.Count == 1)
            {
                if (!closed) { AddDot(points[0]); }
                return;
            }
            if (closed && points.Count == 2) { closed = false; }

            int n = points.Count;
            int segmentCount = closed ? n : n - 1;
            var ux = new double[segmentCount];
            var uy = new double[segmentCount];
            var centres = new IntPoint[n];
            for (int i = 0; i < n; i++) { centres[i] = ToGrid(points[i].X, points[i].Y); }
            var leftStart = new IntPoint[segmentCount];
            var rightStart = new IntPoint[segmentCount];
            var leftEnd = new IntPoint[segmentCount];
            var rightEnd = new IntPoint[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                FlatPoint a = points[i], b = points[(i + 1) % n];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double length = Math.Sqrt((dx * dx) + (dy * dy));
                ux[i] = dx / length;
                uy[i] = dy / length;
                double nx = -uy[i] * _half, ny = ux[i] * _half;
                leftStart[i] = ToGrid(a.X + nx, a.Y + ny);
                rightStart[i] = ToGrid(a.X - nx, a.Y - ny);
                leftEnd[i] = ToGrid(b.X + nx, b.Y + ny);
                rightEnd[i] = ToGrid(b.X - nx, b.Y - ny);
                AddPiece(new List<IntPoint> { leftStart[i], leftEnd[i], centres[(i + 1) % n], rightEnd[i], rightStart[i], centres[i] });
            }

            int firstJoin = closed ? 0 : 1;
            int lastJoin = closed ? n - 1 : n - 2;
            for (int v = firstJoin; v <= lastJoin; v++)
            {
                int inSegment = (v - 1 + segmentCount) % segmentCount;
                int outSegment = v % segmentCount;
                AddJoin(points[v], centres[v], ux[inSegment], uy[inSegment], ux[outSegment], uy[outSegment],
                    leftEnd[inSegment], rightEnd[inSegment], leftStart[outSegment], rightStart[outSegment]);
            }

            if (!closed)
            {
                //The start cap faces back along the first segment, the end cap on along the last
                AddCap(points[0], centres[0], -ux[0], -uy[0], rightStart[0], leftStart[0]);
                int last = segmentCount - 1;
                AddCap(points[n - 1], centres[n - 1], ux[last], uy[last], leftEnd[last], rightEnd[last]);
            }
        }

        internal List<List<IntPoint>> Union()
        {
            var engine = new PolyClip();
            engine.AddPaths(Pieces, PolyType.ptSubject, true);
            var solution = new List<List<IntPoint>>();
            engine.Execute(ClipType.ctUnion, solution, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return solution;
        }

        private void AddJoin(FlatPoint p, IntPoint centre, double ax, double ay, double bx, double by,
            IntPoint inLeft, IntPoint inRight, IntPoint outLeft, IntPoint outRight)
        {
            double cross = (ax * by) - (ay * bx);
            double dot = (ax * bx) + (ay * by);
            if (cross == 0.0 && dot > 0.0) { return; }
            //Turning towards the left normal puts the corner's outside on the right
            bool outsideRight = cross > 0.0;
            IntPoint from = outsideRight ? inRight : inLeft;
            IntPoint to = outsideRight ? outRight : outLeft;
            PathStrokeJoin join = p.Smooth ? PathStrokeJoin.Round : _join;

            if (join == PathStrokeJoin.Miter)
            {
                //Miter length / width = 1 / cos(turn / 2); cos(turn / 2)^2 = (1 + cos(turn)) / 2
                double halfCos2 = (1.0 + dot) / 2.0;
                if (halfCos2 > 0.0 && 1.0 / Math.Sqrt(halfCos2) <= _miterLimit)
                {
                    double sign = outsideRight ? -1.0 : 1.0;
                    double mx = sign * (-ay - by), my = sign * (ax + bx);
                    double k = _half / (1.0 + dot);
                    AddPiece(new List<IntPoint> { centre, from, ToGrid(p.X + (mx * k), p.Y + (my * k)), to });
                    return;
                }
                join = PathStrokeJoin.Bevel;
            }
            if (join == PathStrokeJoin.Bevel)
            {
                AddPiece(new List<IntPoint> { centre, from, to });
                return;
            }

            //Round: a fan from the incoming outer point to the outgoing one, round the corner
            double turn = Math.Atan2(cross, dot);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(turn) / _arcStep));
            var fan = new List<IntPoint>(steps + 2) { centre, from };
            double sx = outsideRight ? ay : -ay, sy = outsideRight ? -ax : ax;
            for (int j = 1; j < steps; j++)
            {
                double angle = turn * j / steps;
                double cos = Math.Cos(angle), sin = Math.Sin(angle);
                fan.Add(ToGrid(p.X + (((sx * cos) - (sy * sin)) * _half), p.Y + (((sx * sin) + (sy * cos)) * _half)));
            }
            fan.Add(to);
            AddPiece(fan);
        }

        //A cap at an end point facing direction (dx, dy); `left` and `right` are the stroke's
        //  edge points there, seen looking along the facing direction
        private void AddCap(FlatPoint p, IntPoint centre, double dx, double dy, IntPoint left, IntPoint right)
        {
            if (_cap == PathStrokeCap.Butt) { return; }
            if (_cap == PathStrokeCap.Square)
            {
                double ex = dx * _half, ey = dy * _half;
                double nx = dy * _half, ny = -dx * _half;
                AddPiece(new List<IntPoint>
                {
                    left, ToGrid(p.X - nx + ex, p.Y - ny + ey), ToGrid(p.X + nx + ex, p.Y + ny + ey), right, centre,
                });
                return;
            }
            //Round: a half-disc from the left edge point, round the front, to the right one
            int steps = Math.Max(2, (int)Math.Ceiling(Math.PI / _arcStep));
            var fan = new List<IntPoint>(steps + 1) { left };
            double sx = -dy, sy = dx;
            for (int j = 1; j < steps; j++)
            {
                double angle = -Math.PI * j / steps;
                double cos = Math.Cos(angle), sin = Math.Sin(angle);
                fan.Add(ToGrid(p.X + (((sx * cos) - (sy * sin)) * _half), p.Y + (((sx * sin) + (sy * cos)) * _half)));
            }
            fan.Add(right);
            fan.Add(centre);
            AddPiece(fan);
        }

        //A zero-length contour: a disc for round caps, a square (axis-aligned) for square caps
        private void AddDot(FlatPoint p)
        {
            if (_cap == PathStrokeCap.Butt) { return; }
            if (_cap == PathStrokeCap.Square)
            {
                AddPiece(new List<IntPoint>
                {
                    ToGrid(p.X - _half, p.Y - _half), ToGrid(p.X + _half, p.Y - _half),
                    ToGrid(p.X + _half, p.Y + _half), ToGrid(p.X - _half, p.Y + _half),
                });
                return;
            }
            int steps = Math.Max(8, (int)Math.Ceiling(2.0 * Math.PI / _arcStep));
            var disc = new List<IntPoint>(steps);
            for (int j = 0; j < steps; j++)
            {
                double angle = 2.0 * Math.PI * j / steps;
                disc.Add(ToGrid(p.X + (Math.Cos(angle) * _half), p.Y + (Math.Sin(angle) * _half)));
            }
            AddPiece(disc);
        }

        //Adds a piece wound the positive way (so the non-zero union never cancels two pieces)
        private void AddPiece(List<IntPoint> piece)
        {
            var clean = new List<IntPoint>(piece.Count);
            foreach (IntPoint point in piece) { AddDistinct(clean, point); }
            if (clean.Count > 1 && clean[clean.Count - 1] == clean[0]) { clean.RemoveAt(clean.Count - 1); }
            if (clean.Count < 3) { return; }
            double area = PolyClip.Area(clean);
            if (area == 0.0) { return; }
            if (area < 0.0) { clean.Reverse(); }
            Pieces.Add(clean);
        }
    }
}
