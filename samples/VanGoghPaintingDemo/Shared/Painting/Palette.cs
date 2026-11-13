using System;
using CodeBrix.Imaging.Drawing.Planes;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// The Van Gogh palette: every stroke's sampled colour is repainted from an ultramarine ramp (cool colours) or
/// a gold ramp (warm colours) by its brightness, and about one stroke in eight takes a surprise accent - teal,
/// violet or Prussian blue among the cool strokes; vermilion, lemon or ochre among the warm ones. Which strokes
/// get an accent is fixed per stroke, so accents never flicker from one painting to the next.
/// </summary>
internal sealed class Palette
{
    private static readonly double[][] Cool = { new[] { 0.03, 0.04, 0.20 }, new[] { 0.08, 0.12, 0.52 }, new[] { 0.14, 0.26, 0.78 }, new[] { 0.30, 0.55, 0.88 }, new[] { 0.78, 0.88, 0.96 } };
    private static readonly double[][] Warm = { new[] { 0.30, 0.10, 0.06 }, new[] { 0.82, 0.33, 0.06 }, new[] { 0.97, 0.58, 0.10 }, new[] { 1.00, 0.82, 0.30 }, new[] { 1.00, 0.96, 0.74 } };
    private static readonly double[][] CoolAccents = { new[] { 0.10, 0.45, 0.48 }, new[] { 0.32, 0.18, 0.55 }, new[] { 0.05, 0.30, 0.62 } };
    private static readonly double[][] WarmAccents = { new[] { 0.85, 0.20, 0.10 }, new[] { 0.95, 0.88, 0.25 }, new[] { 0.70, 0.45, 0.10 } };

    private readonly float[] jitter;
    private readonly bool[] accented;
    private readonly byte[] accent;

    /// <summary>Creates the palette.</summary>
    /// <param name="strokes">The most strokes any layout has.</param>
    public Palette(int strokes)
    {
        DrawingRandom random = new DrawingRandom(5);
        jitter = new float[strokes];
        accented = new bool[strokes];
        accent = new byte[strokes];
        for (int i = 0; i < strokes; i++)
        {
            jitter[i] = (float)((random.NextDouble() - 0.5) * 0.16);
            accented[i] = random.NextDouble() < 0.13;
            accent[i] = (byte)Math.Min((int)(random.NextDouble() * 3), 2);
        }
    }

    /// <summary>Repaints a stroke's sampled colour.</summary>
    /// <param name="stroke">The stroke's index.</param>
    /// <param name="r">The sampled red.</param>
    /// <param name="g">The sampled green.</param>
    /// <param name="b">The sampled blue.</param>
    /// <returns>The colour the stroke is painted in.</returns>
    public (double R, double G, double B) Repaint(int stroke, double r, double g, double b)
    {
        double brightness = (r * 0.30) + (g * 0.59) + (b * 0.11);
        double t = Math.Pow(Math.Clamp(brightness * 2.4, 0, 1), 0.55) + jitter[stroke];
        double warmth = Math.Clamp((((r - b) / (brightness + 0.03)) - 0.35) / 0.9, 0, 1);
        warmth = warmth * warmth * (3 - (2 * warmth));
        (double coolR, double coolG, double coolB) = Ramp(Cool, t);
        (double warmR, double warmG, double warmB) = Ramp(Warm, (t * 0.95) + 0.05);
        double cr = (coolR * (1 - warmth)) + (warmR * warmth);
        double cg = (coolG * (1 - warmth)) + (warmG * warmth);
        double cb = (coolB * (1 - warmth)) + (warmB * warmth);
        if (accented[stroke])
        {
            double[] surprise = (warmth > 0.5 ? WarmAccents : CoolAccents)[accent[stroke]];
            double strength = (0.55 + (0.6 * t)) * 0.55;
            cr = (cr * 0.45) + (surprise[0] * strength);
            cg = (cg * 0.45) + (surprise[1] * strength);
            cb = (cb * 0.45) + (surprise[2] * strength);
        }

        return (cr, cg, cb);
    }

    private static (double R, double G, double B) Ramp(double[][] ramp, double t)
    {
        double x = Math.Clamp(t, 0, 1) * (ramp.Length - 1);
        int i = Math.Min((int)x, ramp.Length - 2);
        double f = x - i;
        return ((ramp[i][0] * (1 - f)) + (ramp[i + 1][0] * f), (ramp[i][1] * (1 - f)) + (ramp[i + 1][1] * f), (ramp[i][2] * (1 - f)) + (ramp[i + 1][2] * f));
    }
}
