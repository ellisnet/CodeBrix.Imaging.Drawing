using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A modification applied to a path before it is stroked, assigned to
/// <see cref="DrawingPaint.PathEffect"/> - API-compatible with the SkiaSharp
/// <c>SKPathEffect</c> factory surface this managed implementation supports (dashing).
/// </summary>
public sealed class DrawingPathEffect
{
    private DrawingPathEffect(float[] intervals, float phase)
    {
        Intervals = intervals;
        Phase = phase;
    }

    /// <summary>The alternating on/off dash lengths.</summary>
    public float[] Intervals { get; }

    /// <summary>The offset into the dash pattern at the start of each contour.</summary>
    public float Phase { get; }

    /// <summary>
    /// Creates a dash effect with alternating on/off intervals.
    /// </summary>
    /// <param name="intervals">
    /// The alternating on/off lengths; must hold an even number of at least two
    /// non-negative values with a positive sum.
    /// </param>
    /// <param name="phase">The offset into the pattern at the start of each contour.</param>
    /// <returns>The dash effect.</returns>
    /// <exception cref="ArgumentException">Thrown when the intervals are unusable.</exception>
    public static DrawingPathEffect CreateDash(float[] intervals, float phase)
    {
        if (intervals == null || intervals.Length < 2 || intervals.Length % 2 != 0)
        {
            throw new ArgumentException("Dashing requires an even number of at least two intervals.", nameof(intervals));
        }
        float sum = 0;
        foreach (float interval in intervals)
        {
            if (interval < 0) { throw new ArgumentException("Dash intervals must not be negative.", nameof(intervals)); }
            sum += interval;
        }
        if (sum <= 0) { throw new ArgumentException("The dash intervals must sum to a positive length.", nameof(intervals)); }

        return new DrawingPathEffect((float[])intervals.Clone(), phase);
    }
}
