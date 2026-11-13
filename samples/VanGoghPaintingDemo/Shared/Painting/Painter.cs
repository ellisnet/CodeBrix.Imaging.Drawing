using System;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Planes;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// Makes one painting: the scene at a moment, turned into brushwork.
/// <list type="number">
///   <item>The <see cref="SceneRenderer" /> paints the underpainting.</item>
///   <item>Every stroke of the chosen <see cref="StrokeMap" /> takes the average colour beneath it (the ownership
///   map's colour pickup), and the <see cref="Palette" /> repaints it in ultramarine and gold.</item>
///   <item>Bristle ridges and dark stroke outlines go on, a little of the raw lights is added back so small ones
///   stay crisp, the lights bloom, and the background stars are dotted in.</item>
///   <item>A vignette and a gentle tone curve finish it.</item>
/// </list>
/// Everything is CodeBrix.Imaging.Drawing plane arithmetic, so the painting is the same, bit for bit, whichever
/// drawing package the program is built against. A painter is not safe to use from two threads at once.
/// </summary>
internal sealed class Painter : IDisposable
{
    private const int W = Timeline.Width;
    private const int H = Timeline.Height;

    // The bloom: the lights at a quarter of the underpainting's size, blurred tight and wide, stretched back.
    private static readonly (float Sigma, float Weight)[] BloomBlurs = { (3f, 0.8f), (14f, 0.9f) };

    private readonly SceneRenderer scene = new SceneRenderer();
    private readonly StrokeMap[] maps = new StrokeMap[Timeline.LayoutCount];
    private readonly DrawingPlane[] inks = new DrawingPlane[Timeline.LayoutCount];
    private readonly Palette palette;
    private readonly DrawingPlane vignette = DrawingPlane.CreateVignette(W, H, W / 2.0, H * 0.48, W * 0.75, H * 0.62, 0.38, 0.4);
    private readonly (double X, double Y, double Brightness, double Phase)[] stars;

    /// <summary>Creates the painter: lays out the brushstrokes and builds everything that never changes.</summary>
    /// <param name="log">Receives progress messages; may be null.</param>
    public Painter(Action<string> log = null)
    {
        int most = 0;
        for (int k = 0; k < maps.Length; k++)
        {
            log?.Invoke("building stroke layout " + (k + 1) + " of " + maps.Length + " ...");
            maps[k] = StrokeMap.Build(StrokeLayout.Generate(k));
            inks[k] = maps[k].Outline.Clone().Multiply(0.75f);
            log?.Invoke("stroke layout " + (k + 1) + ": " + maps[k].StrokeCount + " strokes");
            most = Math.Max(most, maps[k].StrokeCount);
        }

        palette = new Palette(most);
        DrawingRandom random = new DrawingRandom(7);
        stars = new (double, double, double, double)[220];
        for (int i = 0; i < stars.Length; i++)
        {
            double brightness = random.Uniform(0.2, 1);
            stars[i] = (random.Uniform(0, W), random.Uniform(0, Timeline.Horizon - 40), brightness * brightness, random.Uniform(0, 2 * Math.PI));
        }
    }

    /// <summary>Makes the painting of a moment.</summary>
    /// <param name="seconds">The animation time.</param>
    /// <param name="layout">Which brushstroke layout to paint it on (taken modulo the number of layouts).</param>
    /// <param name="rgba">Receives the painting: <see cref="Timeline.Width" /> x <see cref="Timeline.Height" /> pixels, four bytes (R, G, B, A) each, opaque.</param>
    public void Paint(double seconds, int layout, Span<byte> rgba)
    {
        (DrawingPlaneImage under, DrawingPlaneImage light, double fade) = scene.Render(seconds);
        int which = ((layout % maps.Length) + maps.Length) % maps.Length;
        StrokeMap map = maps[which];

        // Every stroke takes the average colour beneath it, and is repainted from the palette.
        Vector4[] sampled = map.Map.GetMeanColors(under);
        Vector4[] paint = new Vector4[sampled.Length];
        for (int i = 0; i < paint.Length; i++)
        {
            (double r, double g, double b) = palette.Repaint(i, sampled[i].X, sampled[i].Y, sampled[i].Z);
            paint[i] = new Vector4((float)(r * fade), (float)(g * fade), (float)(b * fade), 1f);
        }

        DrawingPlaneImage painting = new DrawingPlaneImage(W, H);
        map.Map.ApplyColors(painting, paint);

        // Bristle ridges, dark ink along the stroke outlines, a little of the raw lights, and their bloom.
        painting.Multiply(map.Bristle);
        painting.BlendColor((float)(0.02 * fade), (float)(0.03 * fade), (float)(0.16 * fade), 1f, inks[which]);
        painting.Add(light.Resized(W, H, DrawingPlaneResampling.Nearest), 0.15f);
        painting.Add(light.CreateBloom(4, BloomBlurs, 0.6f, new Size(W, H), DrawingPlaneResampling.BilinearAlignedCorners));

        DotStars(painting, seconds, fade);

        // Vignette, tone curve, and down to bytes.
        painting.Multiply(vignette).ApplyToneCurve(0.15f, 1.08f).ToRgba(rgba);
    }

    /// <summary>Releases the scene renderer.</summary>
    public void Dispose() => scene.Dispose();

    // The background stars: small soft dots that twinkle, fading with the dawn.
    private void DotStars(DrawingPlaneImage painting, double seconds, double fade)
    {
        double dawn = Timeline.Dawn.Progress(seconds);
        double exposure = ((Timeline.FadeIn.Progress(seconds) * 0.85) + 0.15) * fade;
        double visible = (1 - dawn) * exposure;
        if (visible <= 0.01) return;
        const double Radius = 1.3;
        float[] outR = painting.R.Data, outG = painting.G.Data, outB = painting.B.Data;
        foreach ((double x, double y, double brightness, double phase) in stars)
        {
            if (scene.IsSilhouette(x, y)) continue;
            double strength = brightness * visible * (0.65 + (0.35 * Math.Sin((seconds * 2.0) + phase))) * 0.55;
            int x0 = (int)Math.Max(0, x - (3 * Radius)), x1 = (int)Math.Min(W, x + (3 * Radius) + 1);
            int y0 = (int)Math.Max(0, y - (3 * Radius)), y1 = (int)Math.Min(H, y + (3 * Radius) + 1);
            for (int py = y0; py < y1; py++)
            {
                for (int px = x0; px < x1; px++)
                {
                    float dot = (float)(Math.Exp(-(((px - x) * (px - x)) + ((py - y) * (py - y))) / (Radius * Radius)) * strength);
                    int i = (py * W) + px;
                    outR[i] += dot * 0.75f;
                    outG[i] += dot * 0.8f;
                    outB[i] += dot;
                }
            }
        }
    }
}
