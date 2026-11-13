using System;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.PixelFormats;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Seeded coherent noise that evolves over time: value noise, gradient (Perlin-style) noise, and five octaves of
/// fractal gradient noise coloured through a <see cref="ColorRamp" /> - all filled with
/// <see cref="DrawingNoise.Fill" />, with the scene's time as the third noise coordinate so each frame flows smoothly
/// into the next. Below them, a row of dots placed by a seeded <see cref="DrawingRandom" /> (normal and uniform).
/// </summary>
internal sealed class NoiseScene : DemoScene
{
    private const int Panel = 384;
    private const int PanelHeight = 340;
    private const int PanelY = 112;
    private const int DotsY = 520;
    private const int DotsHeight = 150;
    private static readonly int[] PanelX = { 32, 448, 864 };

    private readonly DrawingSession frame;
    private readonly DrawingPlane noise = new DrawingPlane(Panel, PanelHeight);
    private readonly DrawingPlaneImage image = new DrawingPlaneImage(Panel, PanelHeight);
    private readonly DrawingPlaneImage dots = new DrawingPlaneImage(1216, DotsHeight);
    private readonly Vector4[] terrain = new Vector4[256];
    private readonly byte[] scratch = new byte[1216 * DotsHeight * 4];

    /// <summary>Builds the frame, its captions and the colour table for the fractal panel.</summary>
    public NoiseScene()
    {
        var ramp = new ColorRamp(Color.FromRgb(18, 40, 110), Color.FromRgb(40, 110, 190), Color.FromRgb(230, 210, 150),
            Color.FromRgb(70, 140, 60), Color.FromRgb(40, 80, 40), Color.FromRgb(240, 240, 250));
        for (int i = 0; i < terrain.Length; i++)
        {
            Rgba32 c = ramp.At(i / 255.0).ToPixel<Rgba32>();
            terrain[i] = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 1f);
        }

        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "DrawingNoise.Fill(plane, Value, seed, 0.025, time)", PanelX[0], PanelY + PanelHeight + 24, 13, Ink);
        Label(captions, frame, "DrawingNoise.Fill(plane, Gradient, seed, 0.025, time)", PanelX[1], PanelY + PanelHeight + 24, 13, Ink);
        Label(captions, frame, "Gradient, 5 octaves, through a ColorRamp", PanelX[2], PanelY + PanelHeight + 24, 13, Ink);
        Label(captions, frame, "DrawingRandom(seed): Normal(mean, deviation) above, Uniform below - the same sequence every run, in both packages",
            32, DotsY - 10, 13, Dim);
    }

    /// <inheritdoc />
    public override string Name => "noise";

    /// <inheritdoc />
    public override string Title => "Noise and seeded randomness";

    /// <inheritdoc />
    public override string Description => "Value, gradient and fractal noise evolving with time as the third coordinate; a fixed, documented random sequence";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        ExportFrame(frame, rgba);
        double time = seconds * 0.5;

        DrawingNoise.Fill(noise, DrawingNoiseKind.Value, 11, 0.025, time).Multiply(0.5f).Add(0.5f);
        Grey(new Vector3(1.0f, 0.85f, 0.6f));
        PlaceImage(image, scratch, rgba, PanelX[0], PanelY);

        DrawingNoise.Fill(noise, DrawingNoiseKind.Gradient, 11, 0.025, time).Multiply(0.5f).Add(0.5f);
        Grey(new Vector3(0.6f, 0.85f, 1.0f));
        PlaceImage(image, scratch, rgba, PanelX[1], PanelY);

        DrawingNoise.Fill(noise, DrawingNoiseKind.Gradient, 5, 0.006, time * 0.4, octaves: 5, offsetX: seconds * 6).Multiply(0.7f).Add(0.5f).Clamp(0f, 1f);
        float[] values = noise.Data;
        image.FillParallel((x, y) => terrain[(int)(values[(y * Panel) + x] * 255f)]);
        PlaceImage(image, scratch, rgba, PanelX[2], PanelY);

        PaintDots(seconds);
        PlaceImage(dots, scratch, rgba, 32, DotsY);
    }

    //The noise plane as a tinted grey image
    private void Grey(Vector3 tint)
    {
        float[] values = noise.Data;
        image.FillParallel((x, y) => new Vector4(tint * values[(y * Panel) + x], 1f));
    }

    //Points from one seeded stream: the upper band's heights follow Normal(0.5, 0.15), the lower band's Uniform;
    //  more appear as the scene plays, and the stream never changes, so neither do they
    private void PaintDots(double seconds)
    {
        dots.Fill(0.05f, 0.06f, 0.10f, 1f);
        var random = new DrawingRandom(7);
        int count = (int)(40 + (seconds * 45));
        for (int i = 0; i < count; i++)
        {
            int x = random.NextInt(4, dots.Width - 8);
            double normal = Math.Clamp(random.Normal(0.5, 0.15), 0, 1);
            double uniform = random.NextDouble();
            dots.Fill(new Rectangle(x, (int)(normal * 60) + 4, 4, 4), Accent);
            dots.Fill(new Rectangle(x, 80 + (int)(uniform * 60), 4, 4), Color.FromRgb(120, 190, 255));
        }
    }

    /// <inheritdoc />
    public override void Dispose() => frame.Dispose();
}
