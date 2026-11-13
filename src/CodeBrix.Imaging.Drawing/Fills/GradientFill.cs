using System;
using System.Collections.Generic;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Fills;

/// <summary>
/// The common part of the gradient fills: an ordered list of <see cref="ColorStop"/> values
/// and a <see cref="GradientSpread"/>. Colors are interpolated per channel in straight
/// (not premultiplied) sRGB, alpha included - the same rule
/// <see cref="CodeBrix.Imaging.Drawing.Animation.ColorRamp"/> uses, so a ramp's stops passed
/// to a gradient paint the colors the ramp reports.
/// </summary>
public abstract class GradientFill : ShapeFill
{
    private readonly ColorStop[] _stops;

    /// <summary>How the area beyond the first and last stop is painted.</summary>
    public GradientSpread Spread { get; }

    /// <summary>The number of color stops.</summary>
    public int StopCount => _stops.Length;

    private protected GradientFill(IReadOnlyList<ColorStop> stops, GradientSpread spread, string stopsParameterName)
    {
        _stops = ColorStop.ValidateAndCopy(stops, stopsParameterName);
        if (spread != GradientSpread.Pad && spread != GradientSpread.Repeat && spread != GradientSpread.Reflect)
        {
            throw new ArgumentOutOfRangeException(nameof(spread));
        }
        Spread = spread;
    }

    /// <summary>
    /// Returns a snapshot copy of the gradient's color stops, in order.
    /// </summary>
    /// <returns>A new array holding the stops.</returns>
    public ColorStop[] GetStops() => (ColorStop[])_stops.Clone();

    internal SKColor[] GetBackendColors()
    {
        var colors = new SKColor[_stops.Length];
        for (int i = 0; i < _stops.Length; i++)
        {
            colors[i] = GraphicsInterop.ToGraphics(_stops[i].Color);
        }
        return colors;
    }

    internal float[] GetPositions()
    {
        var positions = new float[_stops.Length];
        for (int i = 0; i < _stops.Length; i++)
        {
            positions[i] = _stops[i].Position;
        }
        return positions;
    }

    internal SKShaderTileMode GetTileMode() => Spread switch
    {
        GradientSpread.Repeat => SKShaderTileMode.Repeat,
        GradientSpread.Reflect => SKShaderTileMode.Mirror,
        _ => SKShaderTileMode.Clamp,
    };
}
