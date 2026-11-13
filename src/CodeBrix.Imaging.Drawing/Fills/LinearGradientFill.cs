using System;
using System.Collections.Generic;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Fills;

/// <summary>
/// A gradient that changes color along the straight line from <see cref="Start"/> (stop
/// position 0) to <see cref="End"/> (stop position 1), constant along every line
/// perpendicular to it. Both points are in calibrated drawing units.
/// </summary>
public sealed class LinearGradientFill : GradientFill
{
    /// <summary>Where stop position 0 lies, in calibrated drawing units.</summary>
    public PointF Start { get; }

    /// <summary>Where stop position 1 lies, in calibrated drawing units.</summary>
    public PointF End { get; }

    /// <summary>
    /// Creates a linear gradient through the given stops.
    /// </summary>
    /// <param name="start">Where stop position 0 lies, in calibrated drawing units.</param>
    /// <param name="end">Where stop position 1 lies, in calibrated drawing units; must differ from <paramref name="start"/>.</param>
    /// <param name="stops">At least two stops, in non-decreasing position order.</param>
    /// <param name="spread">How the area beyond the end stops is painted; <see cref="GradientSpread.Pad"/> by default.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when fewer than two stops are given, the stops are out of order, or
    /// <paramref name="start"/> equals <paramref name="end"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="spread"/> is not a defined value.</exception>
    public LinearGradientFill(PointF start, PointF end, IReadOnlyList<ColorStop> stops, GradientSpread spread = GradientSpread.Pad)
        : base(stops, spread, nameof(stops))
    {
        if (start.Equals(end)) { throw new ArgumentException("A linear gradient needs two different points.", nameof(end)); }
        Start = start;
        End = end;
    }

    /// <summary>
    /// Creates a two-color linear gradient from <paramref name="startColor"/> at
    /// <paramref name="start"/> to <paramref name="endColor"/> at <paramref name="end"/>.
    /// </summary>
    /// <param name="start">Where <paramref name="startColor"/> lies, in calibrated drawing units.</param>
    /// <param name="end">Where <paramref name="endColor"/> lies, in calibrated drawing units; must differ from <paramref name="start"/>.</param>
    /// <param name="startColor">The color at the start point.</param>
    /// <param name="endColor">The color at the end point.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="start"/> equals <paramref name="end"/>.</exception>
    public LinearGradientFill(PointF start, PointF end, Color startColor, Color endColor)
        : this(start, end, new[] { new ColorStop(0f, startColor), new ColorStop(1f, endColor) })
    {
    }

    internal override SKShader CreateShader()
        => SKShader.CreateLinearGradient(
            GraphicsInterop.ToGraphics(Start),
            GraphicsInterop.ToGraphics(End),
            GetBackendColors(),
            GetPositions(),
            GetTileMode());
}
