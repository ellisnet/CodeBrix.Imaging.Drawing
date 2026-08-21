using System;
using System.Collections.Generic;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Flattens path segments (lines and quadratic/cubic Bezier curves) into polyline
/// contours, subdividing curves adaptively until they deviate from their chord by less
/// than a tolerance.
/// </summary>
internal static class PathFlattener
{
    /// <summary>The default flattening tolerance, in device pixels.</summary>
    public const float DevicePixelTolerance = 0.2f;

    private const int MaxRecursionDepth = 24;

    /// <summary>
    /// Flattens a path into polyline contours, applying the given transform to every point
    /// BEFORE flattening (an affine transform of a Bezier's control points transforms the
    /// curve exactly, so flattening after transforming keeps the tolerance in the
    /// transformed space).
    /// </summary>
    /// <param name="path">The path to flatten.</param>
    /// <param name="transform">The transform to apply to the path's points.</param>
    /// <param name="tolerance">The maximum chord deviation, in transformed units.</param>
    /// <returns>The flattened contours (empty contours are omitted).</returns>
    public static List<FlattenedContour> Flatten(DrawingPath path, Matrix3x2 transform, float tolerance)
    {
        var contours = new List<FlattenedContour>();
        FlattenedContour current = null;
        Vector2 position = Vector2.Zero;
        int pointIndex = 0;

        IReadOnlyList<DrawingPathVerb> verbs = path.Verbs;
        IReadOnlyList<DrawingPoint> points = path.Points;

        for (int i = 0; i < verbs.Count; i++)
        {
            switch (verbs[i])
            {
                case DrawingPathVerb.Move:
                {
                    CommitContour(contours, current);
                    current = new FlattenedContour();
                    position = Transform(points[pointIndex++], transform);
                    current.Points.Add(position);
                    break;
                }
                case DrawingPathVerb.Line:
                {
                    Vector2 end = Transform(points[pointIndex++], transform);
                    current?.Points.Add(end);
                    position = end;
                    break;
                }
                case DrawingPathVerb.Quad:
                {
                    Vector2 control = Transform(points[pointIndex++], transform);
                    Vector2 end = Transform(points[pointIndex++], transform);
                    if (current != null)
                    {
                        FlattenQuad(current.Points, position, control, end, tolerance, 0);
                        current.Points.Add(end);
                    }
                    position = end;
                    break;
                }
                case DrawingPathVerb.Cubic:
                {
                    Vector2 control1 = Transform(points[pointIndex++], transform);
                    Vector2 control2 = Transform(points[pointIndex++], transform);
                    Vector2 end = Transform(points[pointIndex++], transform);
                    if (current != null)
                    {
                        FlattenCubic(current.Points, position, control1, control2, end, tolerance, 0);
                        current.Points.Add(end);
                    }
                    position = end;
                    break;
                }
                case DrawingPathVerb.Close:
                {
                    if (current != null)
                    {
                        current.IsClosed = true;
                        CommitContour(contours, current);
                        if (current.Points.Count > 0)
                        {
                            position = current.Points[0];
                        }
                        current = null;
                    }
                    break;
                }
            }
        }

        CommitContour(contours, current);
        return contours;
    }

    private static void CommitContour(List<FlattenedContour> contours, FlattenedContour contour)
    {
        if (contour != null && contour.Points.Count > 0 && !contours.Contains(contour))
        {
            contours.Add(contour);
        }
    }

    private static Vector2 Transform(DrawingPoint point, Matrix3x2 transform)
        => Vector2.Transform(new Vector2(point.X, point.Y), transform);

    private static void FlattenQuad(List<Vector2> output, Vector2 p0, Vector2 control, Vector2 p1,
        float tolerance, int depth)
    {
        if (depth >= MaxRecursionDepth || DistanceToChord(control, p0, p1) <= tolerance)
        {
            return; //The chord (p0 -> p1) is close enough; the caller appends p1
        }

        Vector2 q0 = (p0 + control) / 2;
        Vector2 q1 = (control + p1) / 2;
        Vector2 mid = (q0 + q1) / 2;

        FlattenQuad(output, p0, q0, mid, tolerance, depth + 1);
        output.Add(mid);
        FlattenQuad(output, mid, q1, p1, tolerance, depth + 1);
    }

    private static void FlattenCubic(List<Vector2> output, Vector2 p0, Vector2 control1, Vector2 control2,
        Vector2 p1, float tolerance, int depth)
    {
        if (depth >= MaxRecursionDepth
            || (DistanceToChord(control1, p0, p1) <= tolerance && DistanceToChord(control2, p0, p1) <= tolerance))
        {
            return; //The chord (p0 -> p1) is close enough; the caller appends p1
        }

        Vector2 q0 = (p0 + control1) / 2;
        Vector2 q1 = (control1 + control2) / 2;
        Vector2 q2 = (control2 + p1) / 2;
        Vector2 r0 = (q0 + q1) / 2;
        Vector2 r1 = (q1 + q2) / 2;
        Vector2 mid = (r0 + r1) / 2;

        FlattenCubic(output, p0, q0, r0, mid, tolerance, depth + 1);
        output.Add(mid);
        FlattenCubic(output, mid, r1, q2, p1, tolerance, depth + 1);
    }

    private static float DistanceToChord(Vector2 point, Vector2 chordStart, Vector2 chordEnd)
    {
        Vector2 chord = chordEnd - chordStart;
        float lengthSquared = chord.LengthSquared();
        if (lengthSquared < 1e-12f)
        {
            return (point - chordStart).Length();
        }

        //Perpendicular distance from the point to the infinite chord line
        float cross = (chord.X * (point.Y - chordStart.Y)) - (chord.Y * (point.X - chordStart.X));
        return MathF.Abs(cross) / MathF.Sqrt(lengthSquared);
    }
}
