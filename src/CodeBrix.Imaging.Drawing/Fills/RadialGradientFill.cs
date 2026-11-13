using System;
using System.Collections.Generic;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Fills;

/// <summary>
/// A gradient that changes color with distance from <see cref="Center"/>: stop position 0
/// at the center, stop position 1 on the circle of <see cref="Radius"/>. Center and radius
/// are in calibrated drawing units - the classic glow, halo and vignette fill.
/// </summary>
public sealed class RadialGradientFill : GradientFill
{
    /// <summary>The center (stop position 0), in calibrated drawing units.</summary>
    public PointF Center { get; }

    /// <summary>The distance from the center at which stop position 1 lies, in calibrated drawing units.</summary>
    public float Radius { get; }

    /// <summary>
    /// Creates a radial gradient through the given stops.
    /// </summary>
    /// <param name="center">The center (stop position 0), in calibrated drawing units.</param>
    /// <param name="radius">The radius at which stop position 1 lies, in calibrated drawing units; must be positive.</param>
    /// <param name="stops">At least two stops, in non-decreasing position order.</param>
    /// <param name="spread">How the area beyond the end stops is painted; <see cref="GradientSpread.Pad"/> by default.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fewer than two stops are given or the stops are out of order.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="radius"/> is not positive, or <paramref name="spread"/> is not a defined value.
    /// </exception>
    public RadialGradientFill(PointF center, float radius, IReadOnlyList<ColorStop> stops, GradientSpread spread = GradientSpread.Pad)
        : base(stops, spread, nameof(stops))
    {
        if (!(radius > 0f) || Single.IsInfinity(radius)) { throw new ArgumentOutOfRangeException(nameof(radius)); }
        Center = center;
        Radius = radius;
    }

    /// <summary>
    /// Creates a two-color radial gradient from <paramref name="centerColor"/> at the center
    /// to <paramref name="edgeColor"/> at <paramref name="radius"/>.
    /// </summary>
    /// <param name="center">The center, in calibrated drawing units.</param>
    /// <param name="radius">The radius at which <paramref name="edgeColor"/> lies; must be positive.</param>
    /// <param name="centerColor">The color at the center.</param>
    /// <param name="edgeColor">The color at the radius and beyond.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is not positive.</exception>
    public RadialGradientFill(PointF center, float radius, Color centerColor, Color edgeColor)
        : this(center, radius, new[] { new ColorStop(0f, centerColor), new ColorStop(1f, edgeColor) })
    {
    }

    internal override SKShader CreateShader()
        => SKShader.CreateRadialGradient(
            GraphicsInterop.ToGraphics(Center),
            Radius,
            GetBackendColors(),
            GetPositions(),
            GetTileMode());
}
