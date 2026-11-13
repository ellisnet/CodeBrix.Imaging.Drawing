using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Paths;

namespace CodeBrix.Imaging.Drawing.Rendering;

//An affine map in double precision, in the row-vector convention of Matrix3x2 (p' = p * M):
//  x' = x * M11 + y * M21 + M31, y' = x * M12 + y * M22 + M32. The hard-edged rasterizer maps
//  geometry with it, so every point it tests is computed once, in double, in a fixed order.
internal readonly struct HardEdgeMatrix
{
    internal readonly double M11, M12, M21, M22, M31, M32;

    internal HardEdgeMatrix(double m11, double m12, double m21, double m22, double m31, double m32)
    {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
        M31 = m31;
        M32 = m32;
    }

    internal static HardEdgeMatrix Identity => new HardEdgeMatrix(1, 0, 0, 1, 0, 0);

    internal static HardEdgeMatrix From(Matrix3x2 m) => new HardEdgeMatrix(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);

    //This map first, then next
    internal HardEdgeMatrix Then(HardEdgeMatrix next) => new HardEdgeMatrix(
        (M11 * next.M11) + (M12 * next.M21),
        (M11 * next.M12) + (M12 * next.M22),
        (M21 * next.M11) + (M22 * next.M21),
        (M21 * next.M12) + (M22 * next.M22),
        (M31 * next.M11) + (M32 * next.M21) + next.M31,
        (M31 * next.M12) + (M32 * next.M22) + next.M32);

    internal double Determinant => (M11 * M22) - (M12 * M21);

    //The largest factor by which the map stretches any direction (the larger singular value)
    internal double MaxStretch
    {
        get
        {
            double s = (M11 * M11) + (M12 * M12) + (M21 * M21) + (M22 * M22);
            double det = Determinant;
            double root = Math.Sqrt(Math.Max(0.0, (s * s) - (4.0 * det * det)));
            return Math.Sqrt((s + root) / 2.0);
        }
    }

    internal bool TryInvert(out HardEdgeMatrix inverse)
    {
        double det = Determinant;
        if (det == 0 || !double.IsFinite(det))
        {
            inverse = Identity;
            return false;
        }
        double i11 = M22 / det, i12 = -M12 / det, i21 = -M21 / det, i22 = M11 / det;
        inverse = new HardEdgeMatrix(i11, i12, i21, i22,
            -((M31 * i11) + (M32 * i21)), -((M31 * i12) + (M32 * i22)));
        return true;
    }

    internal void Transform(double x, double y, out double tx, out double ty)
    {
        tx = (x * M11) + (y * M21) + M31;
        ty = (x * M12) + (y * M22) + M32;
    }
}

//THE HARD-EDGED PIXEL RULE, implemented once in shared managed code (both packages compile this
//  same file, so they produce the same pixels bit for bit):
//  - A pixel (px, py) belongs to an area when its CENTRE (px + 0.5, py + 0.5), in output pixels, is
//    inside the area under the area's own fill rule (non-zero: winding number not 0; even-odd:
//    odd). Curves are first flattened to polylines; every contour counts as closed.
//  - Ties - a centre exactly on an edge - follow the "top-left" rule: an edge counts for a row when
//    yTop <= cy < yBottom, and a crossing counts for a centre when crossingX <= cx. So a centre on a
//    left or top edge is inside, on a right or bottom edge outside, and two areas that share an
//    edge never both take (or both miss) a centre lying on it. Horizontal edges never count.
//  - All arithmetic is double: each point is mapped once by the area's matrix; an edge from its
//    top end (xt, yt) to its bottom end (xb, yb) crosses row cy at
//    x = xt + (cy - yt) * ((xb - xt) / (yb - yt)).
internal static class HardEdgeRasterizer
{
    //Writes covered pixels of one row: [x0, x1) on row y (already clipped to the frame)
    internal delegate void SpanWriter(int y, int x0, int x1);

    private struct Edge
    {
        public double XTop, YTop, Slope;
        public int Direction, RowStart, RowEnd;
    }

    private struct Crossing
    {
        public double X;
        public int Direction;
    }

    //Fills one area: its flattened contours (in the area's own coordinates), mapped by matrix
    internal static void Fill(IReadOnlyList<PathPolyline> polylines, HardEdgeMatrix matrix, PathFillRule fillRule,
        int width, int height, SpanWriter writer)
    {
        var edges = new List<Edge>();
        foreach (PathPolyline polyline in polylines)
        {
            int count = polyline.Count;
            if (count < 2) { continue; }
            PointF first = polyline[0];
            matrix.Transform(first.X, first.Y, out double startX, out double startY);
            double previousX = startX, previousY = startY;
            for (int i = 1; i <= count; i++)
            {
                double x, y;
                if (i < count)
                {
                    PointF point = polyline[i];
                    matrix.Transform(point.X, point.Y, out x, out y);
                }
                else
                {
                    x = startX;
                    y = startY;
                }
                AddEdge(edges, previousX, previousY, x, y, height);
                previousX = x;
                previousY = y;
            }
        }
        if (edges.Count == 0) { return; }

        edges.Sort((a, b) => a.RowStart.CompareTo(b.RowStart));
        int firstRow = edges[0].RowStart;
        int lastRow = 0;
        foreach (Edge edge in edges) { lastRow = Math.Max(lastRow, edge.RowEnd); }

        var active = new List<Edge>();
        var crossings = new List<Crossing>();
        int next = 0;
        bool evenOdd = fillRule == PathFillRule.EvenOdd;
        for (int row = firstRow; row < lastRow; row++)
        {
            while (next < edges.Count && edges[next].RowStart <= row) { active.Add(edges[next++]); }
            active.RemoveAll(e => e.RowEnd <= row);
            if (active.Count == 0) { continue; }

            double cy = row + 0.5;
            crossings.Clear();
            foreach (Edge edge in active)
            {
                crossings.Add(new Crossing { X = edge.XTop + ((cy - edge.YTop) * edge.Slope), Direction = edge.Direction });
            }
            SortByX(crossings);

            int winding = 0;
            for (int k = 0; k < crossings.Count - 1; k++)
            {
                winding += crossings[k].Direction;
                bool inside = evenOdd ? (winding & 1) != 0 : winding != 0;
                if (!inside) { continue; }
                int x0 = FirstCentreAtOrAfter(crossings[k].X, width);
                int x1 = FirstCentreAtOrAfter(crossings[k + 1].X, width);
                if (x1 > x0) { writer(row, x0, x1); }
            }
        }
    }

    private static void AddEdge(List<Edge> edges, double x0, double y0, double x1, double y1, int height)
    {
        if (y0 == y1) { return; }
        if (!double.IsFinite(x0) || !double.IsFinite(y0) || !double.IsFinite(x1) || !double.IsFinite(y1)) { return; }
        int direction = y1 > y0 ? 1 : -1;
        double xt = x0, yt = y0, xb = x1, yb = y1;
        if (y1 < y0)
        {
            xt = x1; yt = y1; xb = x0; yb = y0;
        }
        int rowStart = FirstCentreAtOrAfter(yt, height);
        int rowEnd = FirstCentreAtOrAfter(yb, height);
        if (rowEnd <= rowStart) { return; }
        edges.Add(new Edge
        {
            XTop = xt,
            YTop = yt,
            Slope = (xb - xt) / (yb - yt),
            Direction = direction,
            RowStart = rowStart,
            RowEnd = rowEnd,
        });
    }

    //The smallest whole p in 0..limit with p + 0.5 >= value (limit when none below it qualifies);
    //  compared exactly in double, so a centre exactly on the value is "at or after" it
    internal static int FirstCentreAtOrAfter(double value, int limit)
    {
        if (!(value > 0.5)) { return 0; }
        if (value > limit - 0.5) { return limit; }
        int p = (int)Math.Ceiling(value - 0.5);
        while (p + 0.5 < value) { p++; }
        while (p > 0 && p - 0.5 >= value) { p--; }
        return Math.Min(Math.Max(p, 0), limit);
    }

    //Insertion sort by x (rows hold few crossings); the order among equal x values does not
    //  matter - the winding between two equal crossings spans no pixel centre
    private static void SortByX(List<Crossing> crossings)
    {
        for (int i = 1; i < crossings.Count; i++)
        {
            Crossing item = crossings[i];
            int j = i - 1;
            while (j >= 0 && crossings[j].X > item.X)
            {
                crossings[j + 1] = crossings[j];
                j--;
            }
            crossings[j + 1] = item;
        }
    }
}
