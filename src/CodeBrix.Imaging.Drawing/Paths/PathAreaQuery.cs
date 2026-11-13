using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Paths;

//The point queries of one geometry at one flattening tolerance: inside tests, distances to the
//  edge, nearest points, area-to-area distances. The geometry is flattened ONCE (Flatten at the
//  tolerance) and every query is single-precision arithmetic in a fixed order on those polylines,
//  so both packages give identical results. Immutable once built; shared by threads freely.
internal sealed class PathAreaQuery
{
    private readonly PointF[][] _rings;
    private readonly bool[] _closed;
    private readonly bool _evenOdd;

    public PathAreaQuery(PathGeometry geometry, float tolerance)
    {
        Tolerance = tolerance;
        IReadOnlyList<PathPolyline> lines = geometry.Flatten(tolerance);
        _rings = new PointF[lines.Count][];
        _closed = new bool[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            _rings[i] = lines[i].GetPoints();
            _closed[i] = lines[i].IsClosed;
        }
        _evenOdd = geometry.FillRule == PathFillRule.EvenOdd;
        Bounds = geometry.GetBounds();
    }

    public float Tolerance { get; }

    public RectangleF Bounds { get; }

    public bool IsEmpty => _rings.Length == 0;

    //Winding number of the flattened area (every contour closed, as a fill closes it) about a
    //  point; inside by the geometry's own fill rule
    public bool Contains(float x, float y)
    {
        int winding = 0;
        foreach (PointF[] ring in _rings)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                PointF a = ring[i];
                PointF b = ring[(i + 1) % ring.Length];
                if (a.Y <= y)
                {
                    if (b.Y > y && Cross(a, b, x, y) > 0) { winding++; }
                }
                else if (b.Y <= y && Cross(a, b, x, y) < 0)
                {
                    winding--;
                }
            }
        }
        return _evenOdd ? (winding & 1) != 0 : winding != 0;
    }

    //The distance to the nearest edge of the filled area (closing lines of open contours count)
    public float DistanceToEdge(float x, float y)
    {
        float best = Single.PositiveInfinity;
        foreach (PointF[] ring in _rings)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                best = MathF.Min(best, SegmentDistance(ring[i], ring[(i + 1) % ring.Length], x, y));
            }
        }
        return best;
    }

    //The nearest point on the path as drawn (open contours stay open)
    public PointF NearestPoint(float x, float y)
    {
        float best = Single.PositiveInfinity;
        var nearest = new PointF(x, y);
        for (int r = 0; r < _rings.Length; r++)
        {
            PointF[] ring = _rings[r];
            int count = _closed[r] || ring.Length == 1 ? ring.Length : ring.Length - 1;
            for (int i = 0; i < count; i++)
            {
                PointF a = ring[i];
                PointF b = ring[(i + 1) % ring.Length];
                PointF p = SegmentNearest(a, b, x, y);
                float dx = p.X - x;
                float dy = p.Y - y;
                float d = MathF.Sqrt((dx * dx) + (dy * dy));
                if (d < best)
                {
                    best = d;
                    nearest = p;
                }
            }
        }
        return nearest;
    }

    //The smallest distance between this area and another, 0 when they overlap: no vertex of
    //  either inside the other and no two edges crossing; else the least vertex-to-edge distance
    //  both ways (which is where two separate polygons come closest)
    //  The other area is taken moved by (ox, oy): its points are offset going one way, this
    //  area's points offset back going the other (so neither geometry is rebuilt)
    public float DistanceTo(PathAreaQuery other, float ox, float oy)
    {
        if (IsEmpty || other.IsEmpty) { return Single.PositiveInfinity; }
        float best = Single.PositiveInfinity;
        foreach (PointF[] ring in other._rings)
        {
            foreach (PointF p in ring)
            {
                if (Contains(p.X + ox, p.Y + oy)) { return 0f; }
                best = MathF.Min(best, DistanceToEdge(p.X + ox, p.Y + oy));
            }
        }
        foreach (PointF[] ring in _rings)
        {
            foreach (PointF p in ring)
            {
                if (other.Contains(p.X - ox, p.Y - oy)) { return 0f; }
                best = MathF.Min(best, other.DistanceToEdge(p.X - ox, p.Y - oy));
            }
        }
        return best > 0f && EdgesCross(other, ox, oy) ? 0f : best;
    }

    private bool EdgesCross(PathAreaQuery other, float ox, float oy)
    {
        foreach (PointF[] ring in _rings)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                PointF a = ring[i];
                PointF b = ring[(i + 1) % ring.Length];
                foreach (PointF[] otherRing in other._rings)
                {
                    for (int j = 0; j < otherRing.Length; j++)
                    {
                        PointF c = otherRing[j];
                        PointF d = otherRing[(j + 1) % otherRing.Length];
                        if (SegmentsCross(a, b, new PointF(c.X + ox, c.Y + oy), new PointF(d.X + ox, d.Y + oy))) { return true; }
                    }
                }
            }
        }
        return false;
    }

    private static bool SegmentsCross(PointF a, PointF b, PointF c, PointF d)
    {
        double d1 = Orient(c, d, a);
        double d2 = Orient(c, d, b);
        double d3 = Orient(a, b, c);
        double d4 = Orient(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Orient(PointF a, PointF b, PointF p)
        => (((double)b.X - a.X) * ((double)p.Y - a.Y)) - (((double)p.X - a.X) * ((double)b.Y - a.Y));

    private static float Cross(PointF a, PointF b, float x, float y) => ((b.X - a.X) * (y - a.Y)) - ((x - a.X) * (b.Y - a.Y));

    private static float SegmentDistance(PointF a, PointF b, float x, float y)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float length = (dx * dx) + (dy * dy);
        float t = length <= 0 ? 0 : Math.Clamp((((x - a.X) * dx) + ((y - a.Y) * dy)) / length, 0, 1);
        float ex = a.X + (t * dx) - x;
        float ey = a.Y + (t * dy) - y;
        return MathF.Sqrt((ex * ex) + (ey * ey));
    }

    private static PointF SegmentNearest(PointF a, PointF b, float x, float y)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float length = (dx * dx) + (dy * dy);
        float t = length <= 0 ? 0 : Math.Clamp((((x - a.X) * dx) + ((y - a.Y) * dy)) / length, 0, 1);
        return new PointF(a.X + (t * dx), a.Y + (t * dy));
    }
}
