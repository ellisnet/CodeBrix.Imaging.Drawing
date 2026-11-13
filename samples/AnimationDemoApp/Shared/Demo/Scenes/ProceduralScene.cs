using System;
using System.Globalization;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// A per-pixel procedural fill: a sunset over water painted by one pure function of (x, y) and the time, through
/// <see cref="DrawingPlaneImage.FillParallel(Func{int, int, Vector4}, int)" />. The sun is brighter than 1.0 and its
/// glow adds past 1.0 too; <see cref="DrawingPlaneImage.ApplyToneCurve(float, float)" /> brings the values back
/// before the frame is converted to bytes.
/// </summary>
internal sealed class ProceduralScene : DemoScene
{
    private const int PanelX = 32;
    private const int PanelY = 112;
    private const int PanelWidth = 1216;
    private const int PanelHeight = 520;
    private const float Horizon = 330f;

    private readonly DrawingSession frame;
    private readonly DrawingLayer notes;
    private readonly DrawingPlaneImage panel = new DrawingPlaneImage(PanelWidth, PanelHeight);
    private readonly byte[] scratch = new byte[PanelWidth * PanelHeight * 4];

    /// <summary>Builds the frame around the painted panel.</summary>
    public ProceduralScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "new DrawingPlaneImage(w, h).FillParallel((x, y) => colour) - one pure function per pixel, rows in parallel, the same result as Fill",
            PanelX, PanelY + PanelHeight + 26, 14, Dim);
        notes = frame.AddLayer("Notes", Accent);
    }

    /// <inheritdoc />
    public override string Name => "procedural";

    /// <inheritdoc />
    public override string Title => "Per-pixel procedural fill";

    /// <inheritdoc />
    public override string Description => "Sky, sun, glow and rippling water from one formula; values past 1.0 tone-mapped with ApplyToneCurve";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        float t = (float)(seconds / DurationSeconds);
        float sunX = (float)Easing.Lerp(260.0, 960.0, Easing.SineInOut(t));
        float sunY = (float)Easing.Lerp(150.0, 300.0, t);
        float time = (float)seconds;

        panel.FillParallel((x, y) => Sky(x, y, sunX, sunY, time));
        panel.ApplyToneCurve(0.15f, 1.08f);

        notes.Clear();
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "sun at ({0:0}, {1:0}); sun core 2.4, glow added on top; ApplyToneCurve(0.15f, 1.08f)", sunX, sunY),
            PanelX, PanelY + PanelHeight + 50, 15, Accent);
        ExportFrame(frame, rgba);
        PlaceImage(panel, scratch, rgba, PanelX, PanelY);
    }

    //The scene's colour at one pixel. Above the horizon: a warm-to-deep vertical gradient, the sun disc and its
    //  glow. Below it: the sky mirrored about the horizon, with the reflected row displaced by a travelling ripple
    //  and darkened with depth.
    private static Vector4 Sky(int x, int y, float sunX, float sunY, float time)
    {
        float sampleY = y;
        float shade = 1f;
        if (y >= Horizon)
        {
            float depth = (y - Horizon) / (PanelHeight - Horizon);
            float ripple = MathF.Sin((x * 0.045f) + (y * 0.6f) - (time * 3.1f)) * (2f + (14f * depth));
            sampleY = Math.Clamp(Horizon - (y - Horizon) - ripple, 0f, Horizon - 1f);
            shade = 0.72f - (0.35f * depth);
        }

        float h = sampleY / Horizon;
        var colour = new Vector3(0.10f + (0.85f * h * h), 0.08f + (0.38f * h), 0.32f - (0.12f * h));
        float dx = x - sunX, dy = sampleY - sunY;
        float distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance < 46f) colour = new Vector3(2.4f, 2.0f, 1.3f);
        else colour += new Vector3(1.0f, 0.55f, 0.25f) * MathF.Exp(-(distance - 46f) / 70f);

        colour *= shade;
        return new Vector4(colour, 1f);
    }

    /// <inheritdoc />
    public override void Dispose() => frame.Dispose();
}
