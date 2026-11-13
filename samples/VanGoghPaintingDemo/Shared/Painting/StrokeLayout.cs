using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// The brushstrokes of one painting, back to front: broad sky strokes that swirl around the star and roll away
/// from it, water and quay strokes that grow toward the viewer, a finer detail pass over the middle of the
/// picture, a ring of shrinking strokes around the star, and the star's own strokes, which run along its eight
/// rays and curl around its disc. Each stroke is a CodeBrix.Imaging.Drawing <see cref="BrushStrokeShape" /> (a
/// tapered, bowed lozenge) with a bristle phase of its own.
/// </summary>
internal sealed class StrokeLayout
{
    private const double Scale = 2.4;
    private const double W = Timeline.Width;
    private const double H = Timeline.Height;
    private const double YH = Timeline.Horizon;

    private StrokeLayout(BrushStrokeShape[] strokes, float[] phases)
    {
        Strokes = strokes;
        Phases = phases;
    }

    /// <summary>The strokes, in painting order (later strokes cover earlier ones).</summary>
    public BrushStrokeShape[] Strokes { get; }

    /// <summary>For each stroke, where its bristle ridges start, radians.</summary>
    public float[] Phases { get; }

    /// <summary>Lays out the strokes.</summary>
    /// <param name="seed">The layout's seed: the same seed always gives the same layout.</param>
    /// <returns>The layout.</returns>
    public static StrokeLayout Generate(int seed)
    {
        DrawingRandom random = new DrawingRandom(1000 + seed);
        List<(BrushStrokeShape, float)> broad = new List<(BrushStrokeShape, float)>();

        // Sky.
        double y = -12.0;
        while (y < YH + 14)
        {
            double width = random.Uniform(13, 21) * Scale;
            double x = -40 + random.Uniform(0, 40);
            while (x < W + 40)
            {
                double length = random.Uniform(50, 88) * Scale;
                double angle = SkyAngle(x, y) + random.Normal(0, 0.07);
                broad.Add(Stroke(random, x + random.Normal(0, 6), y + random.Normal(0, 4), length, width, angle));
                x += length * 0.5;
            }

            y += width * 0.55;
        }

        // Below the horizon: water, then the quay.
        y = YH - 2;
        while (y < H + 14)
        {
            double p = Math.Max(0.0, (y - YH) / (H - YH));
            double rowWidth = (3.0 + (12 * p)) * (1.6 + (1.2 * p));
            double x = -40 + random.Uniform(0, 30);
            while (x < W + 40)
            {
                double length, width, angle;
                if (y > Timeline.QuayY(x) + 4)
                {
                    length = random.Uniform(0.35, 0.6) * (20 + (80 * p)) * Scale;
                    width = rowWidth * random.Uniform(1.1, 1.6);
                    angle = random.Normal(0, 0.5);
                }
                else
                {
                    length = random.Uniform(0.7, 1.25) * (18 + (85 * p)) * Scale;
                    width = rowWidth * random.Uniform(0.8, 1.2);
                    angle = random.Normal(0, 0.05);
                }

                broad.Add(Stroke(random, x + random.Normal(0, 3), y + random.Normal(0, rowWidth * 0.3), length, width, angle));
                x += length * 0.5;
            }

            y += rowWidth * 0.5;
        }

        // A finer pass over the middle of the picture.
        List<(BrushStrokeShape, float)> detail = new List<(BrushStrokeShape, float)>();
        int detailCount = (int)(5000 / Math.Pow(Scale, 1.6));
        for (int i = 0; i < detailCount; i++)
        {
            double x = random.Uniform(150, W);
            y = random.Uniform(250, 1560);
            double length, width, angle;
            if (y < YH)
            {
                length = random.Uniform(26, 46) * Scale * 0.8;
                width = random.Uniform(7, 12) * Scale * 0.8;
                angle = SkyAngle(x, y) + random.Normal(0, 0.1);
            }
            else
            {
                double p = (y - YH) / (H - YH);
                length = (12 + (50 * p)) * random.Uniform(0.6, 1.1) * Scale * 0.8;
                width = (2.5 + (8 * p)) * random.Uniform(0.8, 1.2) * Scale * 0.8;
                angle = random.Normal(0, y < Timeline.QuayEdge ? 0.25 : 0.6);
            }

            detail.Add(Stroke(random, x, y, length, width, angle));
        }

        // Around the star: strokes shrink toward it.
        List<(BrushStrokeShape, float)> star = new List<(BrushStrokeShape, float)>();
        const double Rs = SceneRenderer.StarRadius;
        const double Cx = SceneRenderer.StarX;
        const double Cy = SceneRenderer.StarY;
        for (int i = 0; i < 900; i++)
        {
            double r = Rs * random.Uniform(0.95, 2.4);
            double theta = random.Uniform(0, 2 * Math.PI);
            double x = Cx + (r * Math.Cos(theta));
            y = Cy + (r * Math.Sin(theta));
            double shrink = 0.42 + (0.5 * Math.Min(1.0, ((r / Rs) - 0.95) / 1.45));
            star.Add(Stroke(random, x, y, random.Uniform(50, 88) * Scale * shrink, random.Uniform(13, 21) * Scale * shrink, SkyAngle(x, y) + random.Normal(0, 0.07)));
        }

        // The star itself: strokes run along each ray and curl around the disc.
        for (int i = 0; i < 1700; i++)
        {
            double r = Rs * 1.06 * Math.Sqrt(random.NextDouble());
            double theta = random.Uniform(0, 2 * Math.PI);
            double x = Cx + (r * Math.Sin(theta));
            y = Cy - (r * Math.Cos(theta));
            double length, width, angle;
            if (r < 0.25 * Rs)
            {
                angle = Math.Atan2(y - Cy, x - Cx) + (Math.PI / 2) + random.Normal(0, 0.15);
                length = random.Uniform(0.11, 0.17) * Rs;
                width = random.Uniform(0.045, 0.06) * Rs;
            }
            else if (r < SceneRenderer.StarEdge(theta, Rs) * 1.04)
            {
                double axis = Math.Round(theta / (Math.PI / 4)) * Math.PI / 4;
                angle = Math.Atan2(-Math.Cos(axis), Math.Sin(axis)) + random.Normal(0, 0.08);
                length = random.Uniform(0.17, 0.30) * Rs;
                width = random.Uniform(0.05, 0.075) * Rs * (1.1 - (0.4 * r / Rs));
            }
            else
            {
                angle = Math.Atan2(y - Cy, x - Cx) + (Math.PI / 2) + random.Normal(0, 0.1);
                length = random.Uniform(0.18, 0.26) * Rs;
                width = random.Uniform(0.06, 0.08) * Rs;
            }

            star.Add(Stroke(random, x, y, length, width, angle));
        }

        // Broad strokes go down first in a shuffled order, then the detail (shuffled), then the star.
        Shuffle(random, broad);
        Shuffle(random, detail);
        List<(BrushStrokeShape Shape, float Phase)> all = new List<(BrushStrokeShape, float)>(broad.Count + detail.Count + star.Count);
        all.AddRange(broad);
        all.AddRange(detail);
        all.AddRange(star);
        BrushStrokeShape[] strokes = new BrushStrokeShape[all.Count];
        float[] phases = new float[all.Count];
        for (int i = 0; i < all.Count; i++) (strokes[i], phases[i]) = all[i];
        return new StrokeLayout(strokes, phases);
    }

    // A stroke and its bristle phase; the bend (a fraction of the length) and the phase are drawn after its shape.
    private static (BrushStrokeShape, float) Stroke(DrawingRandom random, double x, double y, double length, double width, double angle)
    {
        double bend = random.Normal(0, 0.13);
        double phase = random.Uniform(0, 2 * Math.PI);
        // The layout works in radians (it is built from Math.Atan2 and friends); a brushstroke takes degrees.
        return (new BrushStrokeShape((float)x, (float)y, (float)length, (float)width, (float)(angle * (180.0 / Math.PI)), (float)bend), (float)phase);
    }

    // The sky's flow: a swirl around the star, blending into rolling horizontal waves further out.
    private static double SkyAngle(double x, double y)
    {
        double dx = x - SceneRenderer.StarX, dy = y - SceneRenderer.StarY;
        double r = Math.Sqrt((dx * dx) + (dy * dy)) + 1e-3;
        double tangent = Math.Atan2(dy, dx) + (Math.PI / 2);
        double w = Math.Exp(-Math.Max(0.0, r - 60) / 260);
        double roll = (0.55 * Math.Sin((x / 170) + (y / 140))) + (0.35 * Math.Sin((y / 90) - (x / 260)));
        double vx = ((1 - w) * Math.Cos(roll)) + (w * Math.Cos(tangent));
        double vy = ((1 - w) * Math.Sin(roll)) + (w * Math.Sin(tangent));
        return Math.Atan2(vy, vx);
    }

    private static void Shuffle(DrawingRandom random, List<(BrushStrokeShape, float)> strokes)
    {
        for (int i = strokes.Count - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            (strokes[i], strokes[j]) = (strokes[j], strokes[i]);
        }
    }
}
