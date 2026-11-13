using System;
using System.Globalization;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// The light filters, one step per panel on the same night scene: the raw float image (lights at 3.0 clip to white
/// in 8-bit), a Gaussian blur whose sigma animates, the scene plus its bloom, and the finished frame with a vignette
/// and a tone curve. The windows twinkle on and off from a seeded <see cref="DrawingRandom" /> per window, so every
/// run (and both packages) show the same sequence.
/// </summary>
internal sealed class LightScene : DemoScene
{
    private const int PanelWidth = 592;
    private const int PanelHeight = 250;
    private const int LeftX = 32;
    private const int RightX = 656;
    private const int TopY = 108;
    private const int BottomY = 400;
    private const int WindowCount = 70;

    private static readonly (float Sigma, float Weight)[] Glow = { (2f, 0.8f), (8f, 0.9f) };

    private readonly DrawingSession frame;
    private readonly DrawingLayer notes;
    private readonly DrawingPlaneImage night = new DrawingPlaneImage(PanelWidth, PanelHeight);
    private readonly Rectangle[] windows = new Rectangle[WindowCount];
    private readonly double[] phases = new double[WindowCount];
    private readonly byte[] scratch = new byte[PanelWidth * PanelHeight * 4];

    /// <summary>Lays out the skyline's windows and the four captions.</summary>
    public LightScene()
    {
        var random = new DrawingRandom(2026);
        for (int i = 0; i < WindowCount; i++)
        {
            windows[i] = new Rectangle(random.NextInt(20, PanelWidth - 30), random.NextInt(130, PanelHeight - 20), random.NextInt(5, 11), random.NextInt(6, 13));
            phases[i] = random.NextDouble();
        }

        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "1  the float image: lights at 3.0 (clipped to white by ToRgba)", LeftX, TopY + PanelHeight + 20, 13, Dim);
        Label(captions, frame, "3  + the bloom of the highlights (values past 1.0): CreateBloom(4, {(2, 0.8), (8, 0.9)}, 1.2)", LeftX, BottomY + PanelHeight + 20, 13, Dim);
        Label(captions, frame, "4  + ApplyVignette(...) + ApplyToneCurve(0.15f, 1.08f): the finished frame", RightX, BottomY + PanelHeight + 20, 13, Dim);
        notes = frame.AddLayer("Notes", Accent);
    }

    /// <inheritdoc />
    public override string Name => "light";

    /// <inheritdoc />
    public override string Title => "Blur, bloom, vignette and tone curve";

    /// <inheritdoc />
    public override string Description => "DrawingPlaneImage filters on one night scene - bit-identical in both packages";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        PaintNight(seconds);
        float sigma = (float)(0.5 + (5.5 * PingPong(seconds, 4)));

        notes.Clear();
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "2  GaussianBlur({0:0.0}f) - in place, premultiplied, mirrored edges", sigma),
            RightX, TopY + PanelHeight + 20, 13, Accent);
        ExportFrame(frame, rgba);

        PlaceImage(night, scratch, rgba, LeftX, TopY);
        PlaceImage(night.Clone().GaussianBlur(sigma), scratch, rgba, RightX, TopY);
        DrawingPlaneImage highlights = night.Clone().Add(-1f, -1f, -1f).Max(0f);   // only what is brighter than white
        DrawingPlaneImage bloomed = night.Clone().Add(highlights.CreateBloom(4, Glow, 1.2f));
        PlaceImage(bloomed, scratch, rgba, LeftX, BottomY);
        bloomed.ApplyVignette(PanelWidth / 2.0, PanelHeight * 0.48, PanelWidth * 0.75, PanelHeight * 0.62, 0.38, 0.4).ApplyToneCurve(0.15f, 1.08f);
        PlaceImage(bloomed, scratch, rgba, RightX, BottomY);
    }

    //A deep-blue sky, a moon, a dark skyline and its lit windows (3.0 = three times brighter than white)
    private void PaintNight(double seconds)
    {
        night.FillParallel((x, y) =>
        {
            float h = y / (float)PanelHeight;
            float dx = x - 470f, dy = y - 52f;
            float moon = (dx * dx) + (dy * dy) < 22f * 22f ? 2.2f : 0f;
            bool building = y > 120 + (int)(30 * MathF.Abs(MathF.Sin(x * 0.021f)) + (25 * ((x / 70) % 3)));
            Vector3 colour = building ? new Vector3(0.025f, 0.03f, 0.05f) : new Vector3(0.03f + (0.06f * h), 0.05f + (0.08f * h), 0.16f + (0.12f * h));
            return new Vector4(colour + new Vector3(moon, moon, moon * 0.9f), 1f);
        });
        for (int i = 0; i < WindowCount; i++)
        {
            bool lit = ((seconds * 0.35) + phases[i]) % 1.0 < 0.7;
            if (lit) night.Fill(windows[i], (x, y) => new Vector4(3.0f, 2.3f, 1.3f, 1f));
        }
    }

    /// <inheritdoc />
    public override void Dispose() => frame.Dispose();
}
