using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// A multi-stop color ramp: <see cref="At(double)"/> reports the color at any position
/// t in 0..1 - for a color that changes over an animation, or across a set of items.
/// <para>
/// INTERPOLATION SPACE: straight (not premultiplied) 8-bit sRGB, channel by channel and alpha
/// included, linear between the two stops around t, each channel rounded to the nearest
/// level (halves away from zero). That is the same space the library's gradient fills
/// interpolate in, so <c>new LinearGradientFill(a, b, ramp.GetStops())</c> paints the colors
/// the ramp reports. No gamma or perceptual correction is applied.
/// </para>
/// <para>
/// Before the first stop the first color holds, after the last stop the last color holds;
/// where two stops share a position the color jumps there, and t exactly at that position
/// takes the later stop. Immutable and thread-safe.
/// </para>
/// </summary>
public sealed class ColorRamp
{
    private readonly ColorStop[] _stops;
    private readonly Rgba32[] _pixels;

    /// <summary>The number of stops.</summary>
    public int StopCount => _stops.Length;

    /// <summary>
    /// Creates a ramp through the given stops.
    /// </summary>
    /// <param name="stops">At least two stops, in non-decreasing position order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fewer than two stops are given or they are out of order.</exception>
    public ColorRamp(IReadOnlyList<ColorStop> stops)
    {
        _stops = ColorStop.ValidateAndCopy(stops, nameof(stops));
        _pixels = new Rgba32[_stops.Length];
        for (int i = 0; i < _stops.Length; i++)
        {
            _pixels[i] = _stops[i].Color.ToPixel<Rgba32>();
        }
    }

    /// <summary>
    /// Creates a ramp through the given colors, evenly spaced from position 0 to 1.
    /// </summary>
    /// <param name="colors">At least two colors.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fewer than two colors are given.</exception>
    public ColorRamp(params Color[] colors)
        : this(ColorStop.EvenlySpaced(colors, nameof(colors)))
    {
    }

    /// <summary>
    /// Returns a snapshot copy of the ramp's stops, in order - ready to hand to a gradient fill.
    /// </summary>
    /// <returns>A new array holding the stops.</returns>
    public ColorStop[] GetStops() => (ColorStop[])_stops.Clone();

    /// <summary>
    /// The ramp's color at a position, alpha included.
    /// </summary>
    /// <param name="t">The position, 0..1 (clamped; NaN is treated as 0).</param>
    /// <returns>The interpolated color.</returns>
    public Color At(double t)
    {
        Rgba32 pixel = Interpolate(t);
        return Color.FromRgba(pixel.R, pixel.G, pixel.B, pixel.A);
    }

    /// <summary>
    /// The ramp's color at a position, with its alpha replaced by <paramref name="alpha"/> -
    /// for a ramp of hues drawn at a separately animated opacity.
    /// </summary>
    /// <param name="t">The position, 0..1 (clamped; NaN is treated as 0).</param>
    /// <param name="alpha">The alpha of the returned color, 0..255.</param>
    /// <returns>The interpolated color at that alpha.</returns>
    public Color At(double t, byte alpha)
    {
        Rgba32 pixel = Interpolate(t);
        return Color.FromRgba(pixel.R, pixel.G, pixel.B, alpha);
    }

    private Rgba32 Interpolate(double t)
    {
        t = Easing.Clamp01(t);
        int last = _stops.Length - 1;
        if (t < _stops[0].Position) { return _pixels[0]; }
        if (t >= _stops[last].Position) { return _pixels[last]; }

        //The LAST stop at or before t starts the segment, so its successor lies strictly
        //  after t - and a position shared by two stops resolves to the later one
        int i = 0;
        while (_stops[i + 1].Position <= t) { i++; }

        double start = _stops[i].Position;
        double end = _stops[i + 1].Position;
        double u = (t - start) / (end - start);
        Rgba32 a = _pixels[i];
        Rgba32 b = _pixels[i + 1];
        return new Rgba32(Channel(a.R, b.R, u), Channel(a.G, b.G, u), Channel(a.B, b.B, u), Channel(a.A, b.A, u));
    }

    //Shared with ColorTrack, so a track and a ramp interpolate colors identically
    internal static byte Channel(byte from, byte to, double u)
        => (byte)Math.Clamp(Math.Round(from + ((to - from) * u), MidpointRounding.AwayFromZero), 0, 255);
}
