using System;
using System.Collections.Generic;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Splits flattened contours into dashed sub-contours per a
/// <see cref="DrawingPathEffect"/> dash pattern, before stroke outlines are built.
/// </summary>
internal static class DashSplitter
{
    /// <summary>
    /// Splits the given contours into open dash segments.
    /// </summary>
    /// <param name="contours">The flattened contours to dash.</param>
    /// <param name="intervals">The alternating on/off dash lengths.</param>
    /// <param name="phase">The offset into the pattern at the start of each contour.</param>
    /// <returns>The dashed (open) contours.</returns>
    public static List<FlattenedContour> Split(List<FlattenedContour> contours, float[] intervals, float phase)
    {
        var result = new List<FlattenedContour>();

        float patternLength = 0;
        foreach (float interval in intervals) { patternLength += interval; }
        if (patternLength <= 0) { return contours; }

        foreach (FlattenedContour contour in contours)
        {
            List<Vector2> points = contour.Points;
            int segmentCount = contour.IsClosed ? points.Count : points.Count - 1;
            if (segmentCount < 1) { continue; }

            //Position within the dash pattern, advanced by the phase
            float patternPosition = ((phase % patternLength) + patternLength) % patternLength;
            var intervalIndex = 0;
            float remainingInInterval = NextInterval(intervals, ref intervalIndex, ref patternPosition);
            bool penDown = intervalIndex % 2 == 1; //index has advanced past the active interval
            FlattenedContour current = penDown ? StartDash(result, points[0]) : null;

            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 from = points[i];
                Vector2 to = points[(i + 1) % points.Count];
                float segmentLength = Vector2.Distance(from, to);
                if (segmentLength <= 0) { continue; }

                float traveled = 0;
                while (traveled < segmentLength)
                {
                    float step = Math.Min(remainingInInterval, segmentLength - traveled);
                    traveled += step;
                    remainingInInterval -= step;
                    Vector2 position = Vector2.Lerp(from, to, traveled / segmentLength);

                    if (remainingInInterval <= 0)
                    {
                        if (penDown)
                        {
                            current.Points.Add(position); //End of an "on" run
                            current = null;
                        }
                        else
                        {
                            current = StartDash(result, position); //Start of an "on" run
                        }
                        penDown = !penDown;
                        remainingInInterval = intervals[intervalIndex % intervals.Length];
                        intervalIndex++;
                    }
                    else if (traveled >= segmentLength && penDown)
                    {
                        current.Points.Add(position); //The "on" run continues into the next segment
                    }
                }
            }

            PruneEmpty(result);
        }

        return result;
    }

    private static float NextInterval(float[] intervals, ref int intervalIndex, ref float patternPosition)
    {
        //Locate where the phase lands within the pattern
        while (true)
        {
            float interval = intervals[intervalIndex % intervals.Length];
            intervalIndex++;
            if (patternPosition < interval)
            {
                return interval - patternPosition;
            }
            patternPosition -= interval;
        }
    }

    private static FlattenedContour StartDash(List<FlattenedContour> result, Vector2 start)
    {
        var dash = new FlattenedContour();
        dash.Points.Add(start);
        result.Add(dash);
        return dash;
    }

    private static void PruneEmpty(List<FlattenedContour> result)
    {
        for (int i = result.Count - 1; i >= 0; i--)
        {
            if (result[i].Points.Count < 2) { result.RemoveAt(i); }
        }
    }
}
