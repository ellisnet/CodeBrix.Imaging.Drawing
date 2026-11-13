using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AnimationDemoApp.Demo;

/// <summary>
/// Small helpers shared by the painterly scenes: a seeded layout of brushstrokes that follow a swirling flow, and a
/// set of distinct colours to tell elements apart in an ownership map.
/// </summary>
internal static class BrushworkKit
{
    /// <summary>
    /// Lays out brushstrokes over an area, row by row with jitter, each turned along a gentle swirl - enough of them,
    /// and long and wide enough, to cover most of the area.
    /// </summary>
    /// <param name="width">The area's width (calibrated units = pixels of the map it is made for).</param>
    /// <param name="height">The area's height.</param>
    /// <param name="size">The typical stroke length; widths are about a third of it.</param>
    /// <param name="seed">The layout's seed.</param>
    /// <returns>The strokes, in painting order.</returns>
    public static List<BrushStrokeShape> Layout(int width, int height, float size, long seed)
    {
        DrawingRandom random = new DrawingRandom(seed);
        List<BrushStrokeShape> strokes = new List<BrushStrokeShape>();
        for (double y = -size * 0.2; y < height + (size * 0.3); y += size * 0.28)
        {
            for (double x = -size * 0.3 + random.Uniform(0, size * 0.3); x < width + (size * 0.4); x += size * 0.5)
            {
                double cx = x + random.Normal(0, size * 0.08), cy = y + random.Normal(0, size * 0.06);
                double angle = Flow(cx / width, cy / height) + random.Normal(0, 0.12);
                double length = size * random.Uniform(0.75, 1.25);
                strokes.Add(new BrushStrokeShape((float)cx, (float)cy, (float)length, (float)(length * random.Uniform(0.28, 0.4)),
                    (float)(angle * (180 / Math.PI)), (float)random.Normal(0, 0.1)));
            }
        }

        // A shuffled painting order, so no row always covers the one above it.
        for (int i = strokes.Count - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            (strokes[i], strokes[j]) = (strokes[j], strokes[i]);
        }

        return strokes;
    }

    /// <summary>A gentle swirl: the stroke direction at a point of the unit square, radians (turn it into the degrees a brushstroke takes with <c>* 180 / Math.PI</c>).</summary>
    /// <param name="u">Across, 0..1.</param>
    /// <param name="v">Down, 0..1.</param>
    /// <returns>The direction.</returns>
    public static double Flow(double u, double v) => (0.5 * Math.Sin((u * 4.0) + (v * 2.5))) + (0.35 * Math.Cos((v * 5.0) - (u * 1.5)));

    /// <summary>Distinct, fairly bright colours, one per element (golden-ratio hues), opaque.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The colours.</returns>
    public static Vector4[] DistinctColors(int count)
    {
        Vector4[] colors = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            double hue = (i * 0.618033988749895) % 1.0;
            colors[i] = new Vector4(Channel(hue), Channel(hue + (1.0 / 3)), Channel(hue + (2.0 / 3)), 1f);
        }

        return colors;
    }

    /// <summary>A plane image of one flat colour.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The colour.</param>
    /// <returns>The image.</returns>
    public static DrawingPlaneImage Flat(int width, int height, Color color)
    {
        DrawingPlaneImage image = new DrawingPlaneImage(width, height);
        image.Fill(color);
        return image;
    }

    private static float Channel(double hue) => (float)(0.45 + (0.4 * Math.Cos(2 * Math.PI * hue)));
}
