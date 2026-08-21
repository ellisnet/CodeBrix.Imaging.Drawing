using System;
using System.Collections.Generic;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Builds the filled outline of a stroked polyline as a set of consistently wound polygons
/// (per-segment rectangles plus cap and join geometry). Filling the set with the non-zero
/// winding rule produces their union, so overlapping pieces never double-blend - which is
/// exactly Skia's stroke compositing behavior.
/// </summary>
internal static class StrokeBuilder
{
    private const float Epsilon = 1e-6f;

    /// <summary>
    /// Builds the outline polygons for stroking the given flattened contours.
    /// </summary>
    /// <param name="contours">The flattened contours to stroke (in the space the stroke width is expressed in).</param>
    /// <param name="strokeWidth">The stroke width; must be positive.</param>
    /// <param name="cap">How open contour ends are capped.</param>
    /// <param name="join">How corners are joined.</param>
    /// <param name="miterLimit">The miter limit for <see cref="DrawingStrokeJoin.Miter"/> joins.</param>
    /// <param name="arcTolerance">
    /// The maximum deviation allowed when round caps and joins are polygonized, in the same
    /// space as the contours.
    /// </param>
    /// <returns>The outline polygons, each wound positively.</returns>
    public static List<List<Vector2>> BuildOutline(
        List<FlattenedContour> contours,
        float strokeWidth,
        DrawingStrokeCap cap,
        DrawingStrokeJoin join,
        float miterLimit,
        float arcTolerance)
    {
        var polygons = new List<List<Vector2>>();
        float half = strokeWidth / 2f;
        if (half <= 0) { return polygons; }

        foreach (FlattenedContour contour in contours)
        {
            List<Vector2> points = RemoveRepeatedPoints(contour.Points, contour.IsClosed);

            if (points.Count == 1)
            {
                //A degenerate (single-point) contour: round and square caps still paint a dot
                if (cap == DrawingStrokeCap.Round)
                {
                    AddPolygon(polygons, BuildCirclePolygon(points[0], half, arcTolerance));
                }
                else if (cap == DrawingStrokeCap.Square)
                {
                    AddPolygon(polygons, new List<Vector2>
                    {
                        new Vector2(points[0].X - half, points[0].Y - half),
                        new Vector2(points[0].X + half, points[0].Y - half),
                        new Vector2(points[0].X + half, points[0].Y + half),
                        new Vector2(points[0].X - half, points[0].Y + half),
                    });
                }
                continue;
            }

            if (points.Count < 2) { continue; }

            int segmentCount = contour.IsClosed ? points.Count : points.Count - 1;

            //One rectangle per segment
            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 p = points[i];
                Vector2 q = points[(i + 1) % points.Count];
                Vector2 direction = q - p;
                float length = direction.Length();
                if (length < Epsilon) { continue; }

                direction /= length;
                var normal = new Vector2(-direction.Y, direction.X) * half;
                AddPolygon(polygons, new List<Vector2> { p + normal, q + normal, q - normal, p - normal });
            }

            //Joins at interior corners (every vertex when the contour is closed)
            int firstJoint = contour.IsClosed ? 0 : 1;
            int lastJoint = contour.IsClosed ? points.Count - 1 : points.Count - 2;
            for (int i = firstJoint; i <= lastJoint; i++)
            {
                Vector2 previous = points[(i - 1 + points.Count) % points.Count];
                Vector2 vertex = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                AddJoin(polygons, previous, vertex, next, half, join, miterLimit, arcTolerance);
            }

            //Caps at the open ends
            if (!contour.IsClosed)
            {
                AddCap(polygons, points[0], points[1], half, cap, arcTolerance);
                AddCap(polygons, points[points.Count - 1], points[points.Count - 2], half, cap, arcTolerance);
            }
        }

        return polygons;
    }

    /// <summary>
    /// Builds a polygon approximating a circle, with enough segments that the chord
    /// deviation stays within the tolerance.
    /// </summary>
    /// <param name="center">The circle's center.</param>
    /// <param name="radius">The circle's radius.</param>
    /// <param name="tolerance">The maximum chord deviation.</param>
    /// <returns>The circle polygon.</returns>
    public static List<Vector2> BuildCirclePolygon(Vector2 center, float radius, float tolerance)
    {
        int segments = SegmentsForRadius(radius, tolerance);
        var polygon = new List<Vector2>(segments);
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)(2 * Math.PI * i / segments);
            polygon.Add(center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius));
        }
        return polygon;
    }

    private static int SegmentsForRadius(float radius, float tolerance)
    {
        if (radius <= tolerance) { return 8; }

        //Chord deviation for a step angle t is r * (1 - cos(t / 2))
        double stepAngle = 2 * Math.Acos(Math.Clamp(1 - (tolerance / radius), -1, 1));
        int segments = stepAngle > 0 ? (int)Math.Ceiling((2 * Math.PI) / stepAngle) : 256;
        return Math.Clamp(segments, 8, 256);
    }

    private static List<Vector2> RemoveRepeatedPoints(List<Vector2> points, bool isClosed)
    {
        var cleaned = new List<Vector2>(points.Count);
        foreach (Vector2 point in points)
        {
            if (cleaned.Count == 0 || (point - cleaned[cleaned.Count - 1]).LengthSquared() > Epsilon * Epsilon)
            {
                cleaned.Add(point);
            }
        }
        if (isClosed && cleaned.Count > 1
            && (cleaned[0] - cleaned[cleaned.Count - 1]).LengthSquared() <= Epsilon * Epsilon)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }
        return cleaned;
    }

    private static void AddCap(List<List<Vector2>> polygons, Vector2 endPoint, Vector2 towardPoint,
        float half, DrawingStrokeCap cap, float arcTolerance)
    {
        if (cap == DrawingStrokeCap.Butt) { return; }

        if (cap == DrawingStrokeCap.Round)
        {
            //A full circle at the endpoint is the union-equivalent of the semicircular cap
            AddPolygon(polygons, BuildCirclePolygon(endPoint, half, arcTolerance));
            return;
        }

        //Square: a half-width extension rectangle past the endpoint
        Vector2 outward = endPoint - towardPoint;
        float length = outward.Length();
        if (length < Epsilon) { return; }
        outward /= length;

        var normal = new Vector2(-outward.Y, outward.X) * half;
        Vector2 extended = endPoint + (outward * half);
        AddPolygon(polygons, new List<Vector2> { endPoint + normal, extended + normal, extended - normal, endPoint - normal });
    }

    private static void AddJoin(List<List<Vector2>> polygons, Vector2 previous, Vector2 vertex, Vector2 next,
        float half, DrawingStrokeJoin join, float miterLimit, float arcTolerance)
    {
        Vector2 incoming = vertex - previous;
        Vector2 outgoing = next - vertex;
        float incomingLength = incoming.Length();
        float outgoingLength = outgoing.Length();
        if (incomingLength < Epsilon || outgoingLength < Epsilon) { return; }
        incoming /= incomingLength;
        outgoing /= outgoingLength;

        if (join == DrawingStrokeJoin.Round)
        {
            //A full circle at the vertex is the union-equivalent of the round join wedge
            AddPolygon(polygons, BuildCirclePolygon(vertex, half, arcTolerance));
            return;
        }

        float cross = (incoming.X * outgoing.Y) - (incoming.Y * outgoing.X);
        if (MathF.Abs(cross) < Epsilon) { return; } //Collinear segments need no join

        //The outer side is the one the outgoing segment bends away from
        var incomingNormal = new Vector2(-incoming.Y, incoming.X);
        float side = Vector2.Dot(incomingNormal, outgoing) < 0 ? 1f : -1f;
        Vector2 outerIncoming = vertex + (incomingNormal * half * side);
        var outgoingNormal = new Vector2(-outgoing.Y, outgoing.X);
        Vector2 outerOutgoing = vertex + (outgoingNormal * half * side);

        if (join == DrawingStrokeJoin.Miter)
        {
            //Miter ratio is 1 / cos(halfTurnAngle); fall back to bevel past the limit
            float cosTurn = Math.Clamp(Vector2.Dot(incoming, outgoing), -1f, 1f);
            float cosHalfTurn = MathF.Sqrt(Math.Max(0f, (1f + cosTurn) / 2f));
            if (cosHalfTurn > Epsilon)
            {
                float miterRatio = 1f / cosHalfTurn;
                if (miterRatio <= miterLimit)
                {
                    Vector2 bisector = (outerIncoming - vertex) + (outerOutgoing - vertex);
                    float bisectorLength = bisector.Length();
                    if (bisectorLength > Epsilon)
                    {
                        Vector2 apex = vertex + ((bisector / bisectorLength) * half * miterRatio);
                        AddPolygon(polygons, new List<Vector2> { vertex, outerIncoming, apex, outerOutgoing });
                        return;
                    }
                }
            }
        }

        //Bevel (and the miter fallback): the flat wedge between the two outer edges
        AddPolygon(polygons, new List<Vector2> { vertex, outerIncoming, outerOutgoing });
    }

    private static void AddPolygon(List<List<Vector2>> polygons, List<Vector2> polygon)
    {
        if (polygon.Count < 3) { return; }

        //Wind every emitted polygon the same way, so non-zero filling unions them
        if (SignedArea(polygon) < 0)
        {
            polygon.Reverse();
        }
        polygons.Add(polygon);
    }

    private static float SignedArea(List<Vector2> polygon)
    {
        float sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }
        return sum / 2f;
    }
}
