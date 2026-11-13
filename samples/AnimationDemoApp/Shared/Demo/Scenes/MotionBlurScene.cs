using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Motion blur by sub-frame averaging: a fast ball and a spinning bar are drawn over transparency at
/// <see cref="Samples" /> moments spread across one 1/24 s frame (<see cref="DrawingMotionBlur.GetSampleTimes" />,
/// a full shutter) and averaged with a <see cref="DrawingFrameAccumulator" />. The average is premultiplied, so the
/// streak's alpha fades correctly: the same averaged frame is shown over a backdrop and over a checkerboard. The
/// left panel is the single sharp frame for comparison.
/// </summary>
internal sealed class MotionBlurScene : DemoScene
{
    private const int Panel = 384;
    private const int PanelY = 128;
    private const int Samples = 16;
    private const double FrameDuration = 1.0 / 24.0;
    private static readonly int[] PanelX = { 32, 448, 864 };

    private readonly DrawingSession frame;
    private readonly DrawingSession drawing;
    private readonly DrawingLayer moving;
    private readonly DrawingFrameAccumulator accumulator = new DrawingFrameAccumulator(Panel, Panel);
    private readonly DrawingPlaneImage backdrop;
    private readonly DrawingPlaneImage checkerboard = CreateCheckerboard(Panel, Panel);
    private readonly byte[] subFrame = new byte[Panel * Panel * 4];
    private readonly byte[] scratch = new byte[Panel * Panel * 4];

    /// <summary>Builds the frame, its captions and the moving drawing's session.</summary>
    public MotionBlurScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "one sharp frame (no blur)", PanelX[0], PanelY + Panel + 26, 14, Ink);
        Label(captions, frame, "16 sub-frames averaged, over a backdrop", PanelX[1], PanelY + Panel + 26, 14, Ink);
        Label(captions, frame, "the same average over a checkerboard", PanelX[2], PanelY + Panel + 26, 14, Ink);
        Label(captions, frame, "DrawingMotionBlur.GetSampleTimes(t, 1/24 s, 16, shutter: 1) -> ExportPixels(includeBackground: false) for each sample",
            32, PanelY + Panel + 62, 13, Dim);
        Label(captions, frame, "-> DrawingFrameAccumulator.Add -> GetAverage: premultiplied, exact integer sums, so the streak fades into transparency",
            32, PanelY + Panel + 84, 13, Dim);

        backdrop = new DrawingPlaneImage(Panel, Panel).FillParallel((x, y) =>
            new Vector4(0.10f + (x * 0.0004f), 0.12f + (y * 0.0005f), 0.22f + (y * 0.0004f), 1f));

        drawing = CreateSession(Panel, Panel, Color.Transparent);
        moving = drawing.AddLayer("Moving", Color.White);
    }

    /// <inheritdoc />
    public override string Name => "motion-blur";

    /// <inheritdoc />
    public override string Title => "Motion blur";

    /// <inheritdoc />
    public override string Description => "Average sub-frames spread across the shutter; premultiplied averaging keeps a streak over transparency correct";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        ExportFrame(frame, rgba);

        DrawMoment(seconds);
        drawing.ExportPixels(subFrame, new Size(Panel, Panel), includeBackground: false);
        PlaceImage(backdrop.Clone().CompositeOver(DrawingPlaneImage.FromRgba(subFrame, Panel, Panel)), scratch, rgba, PanelX[0], PanelY);

        accumulator.Reset();
        foreach (double time in DrawingMotionBlur.GetSampleTimes(seconds, FrameDuration, Samples, shutter: 1.0))
        {
            DrawMoment(time);
            drawing.ExportPixels(subFrame, new Size(Panel, Panel), includeBackground: false);
            accumulator.Add(subFrame);
        }

        accumulator.GetAverage(subFrame);
        DrawingPlaneImage streak = DrawingPlaneImage.FromRgba(subFrame, Panel, Panel);
        PlaceImage(backdrop.Clone().CompositeOver(streak), scratch, rgba, PanelX[1], PanelY);
        PlaceImage(checkerboard.Clone().CompositeOver(streak), scratch, rgba, PanelX[2], PanelY);
    }

    //The drawing at one moment: a ball swinging fast from side to side and a bar spinning about the centre
    private void DrawMoment(double time)
    {
        moving.Clear();
        drawing.ActiveLayer = moving;
        float ballX = (float)(192 + (140 * Math.Sin(time * 5.0)));
        drawing.DrawCircle(ballX, 96, 30, filled: true, color: Accent);

        double angle = time * 9.0;
        float dx = (float)(120 * Math.Cos(angle)), dy = (float)(120 * Math.Sin(angle));
        var bar = new List<(float X, float Y)> { (192 - dx, 250 - dy), (192 + dx, 250 + dy) };
        drawing.DrawPolyline(bar, 18, Color.FromRgb(120, 200, 255));
        drawing.DrawCircle(192, 250, 12, filled: true, color: Ink);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        drawing.Dispose();
    }
}
