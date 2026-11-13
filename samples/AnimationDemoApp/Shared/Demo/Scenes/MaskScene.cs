using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// From a drawing session to planes: the same small drawing (a circle, a star and a word, moving, plus a ring on a
/// layer that fades in and out) exported four ways - as a float image over transparency, shown over a checkerboard;
/// as its COVERAGE mask (where the ink is - the fading layer stays solid, because layer opacity is not part of
/// coverage); as its VISIBLE-ALPHA mask (what a viewer sees - the ring fades with its layer); and with the coverage
/// used as data: a blurred copy paints a glow and the mask itself paints a moving stripe texture. Every export goes
/// into a plane or image made once and reused each frame (the allocation-free overloads).
/// </summary>
internal sealed class MaskScene : DemoScene
{
    private const int Drawing = 384;
    private const int Panel = 288;
    private const int PanelY = 190;
    private static readonly int[] PanelX = { 32, 344, 656, 968 };

    private readonly DrawingSession frame;
    private readonly DrawingSession drawing;
    private readonly DrawingLayer shapes;
    private readonly DrawingLayer fading;
    private readonly DrawingPlaneImage image = new DrawingPlaneImage(Panel, Panel);
    private readonly DrawingPlane coverage = new DrawingPlane(Panel, Panel);
    private readonly DrawingPlane visible = new DrawingPlane(Panel, Panel);
    private readonly DrawingPlaneImage checkerboard = CreateCheckerboard(Panel, Panel);
    private readonly byte[] scratch = new byte[Panel * Panel * 4];

    /// <summary>Builds the frame, its captions and the small drawing's session.</summary>
    public MaskScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "ExportPlaneImage(image, false)", PanelX[0], PanelY + Panel + 26, 14, Ink);
        Label(captions, frame, "straight alpha, over a checkerboard", PanelX[0], PanelY + Panel + 46, 13, Dim);
        Label(captions, frame, "ExportMask(plane) - COVERAGE", PanelX[1], PanelY + Panel + 26, 14, Ink);
        Label(captions, frame, "where ink is: the fading ring stays solid", PanelX[1], PanelY + Panel + 46, 13, Dim);
        Label(captions, frame, "ExportMask(plane, VisibleAlpha)", PanelX[2], PanelY + Panel + 26, 14, Ink);
        Label(captions, frame, "what shows: the ring fades with its layer", PanelX[2], PanelY + Panel + 46, 13, Dim);
        Label(captions, frame, "the coverage mask as data", PanelX[3], PanelY + Panel + 26, 14, Ink);
        Label(captions, frame, "blurred: BlendColor glow; sharp: texture", PanelX[3], PanelY + Panel + 46, 13, Dim);
        Label(captions, frame, "the ring's layer opacity", PanelX[0], PanelY - 18, 13, Dim);

        drawing = CreateSession(Drawing, Drawing, Color.Transparent);
        shapes = drawing.AddLayer("Shapes", Color.White);
        fading = drawing.AddLayer("Fading", Color.FromRgb(255, 214, 90));
        fading.AddShape(new CircleShape(300, 300, 54, 14, Color.FromRgb(255, 214, 90)));
    }

    /// <inheritdoc />
    public override string Name => "masks";

    /// <inheritdoc />
    public override string Title => "Session to planes: images and masks";

    /// <inheritdoc />
    public override string Description => "ExportPlaneImage reads a drawing into float colour; ExportMask gives coverage (where ink is) or visible alpha (what shows)";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        BuildDrawing(seconds);
        float ringOpacity = (float)(1 - (0.9 * PingPong(seconds, 4)));
        fading.Opacity = ringOpacity;
        drawing.ExportPlaneImage(image, includeBackground: false);
        drawing.ExportMask(coverage);
        drawing.ExportMask(visible, MaskMeaning.VisibleAlpha);
        DrawingPlane mask = coverage;

        ExportFrame(frame, rgba);
        DrawMeter(rgba, ringOpacity);

        PlaceImage(checkerboard.Clone().CompositeOver(image), scratch, rgba, PanelX[0], PanelY);
        PlaceImage(Grey(coverage), scratch, rgba, PanelX[1], PanelY);
        PlaceImage(Grey(visible), scratch, rgba, PanelX[2], PanelY);

        float shift = (float)(seconds * 40);
        var stripes = new DrawingPlaneImage(Panel, Panel).FillParallel((x, y) =>
        {
            float band = 0.5f + (0.5f * MathF.Sin(((x + y) * 0.12f) - (shift * 0.12f)));
            return new Vector4(0.95f, 0.45f + (0.4f * band), 0.15f + (0.6f * band), 1f);
        });
        var used = new DrawingPlaneImage(Panel, Panel).FillParallel((x, y) => new Vector4(0.06f, 0.07f + (y * 0.0003f), 0.14f + (y * 0.0004f), 1f));
        used.BlendColor(Color.FromRgb(120, 190, 255), mask.Clone().GaussianBlur(9f).Multiply(0.9f));
        used.BlendImage(stripes, mask);
        PlaceImage(used, scratch, rgba, PanelX[3], PanelY);
    }

    private static DrawingPlaneImage Grey(DrawingPlane mask)
        => new DrawingPlaneImage(mask.Clone(), mask.Clone(), mask.Clone(), new DrawingPlane(Panel, Panel, 1f));

    //A bar above the first panel showing the fading layer's opacity
    private static void DrawMeter(Span<byte> rgba, float opacity)
    {
        int left = PanelX[0] + 170, top = PanelY - 28, width = 118, height = 10;
        int filled = (int)MathF.Round(width * opacity);
        for (int y = top; y < top + height; y++)
        {
            for (int x = left; x < left + width; x++)
            {
                int o = ((y * DemoFrame.Width) + x) * 4;
                bool on = x - left < filled;
                rgba[o] = on ? (byte)255 : (byte)60;
                rgba[o + 1] = on ? (byte)214 : (byte)64;
                rgba[o + 2] = on ? (byte)90 : (byte)84;
                rgba[o + 3] = 255;
            }
        }
    }

    //A circle sweeping across, a star turning, and a word with a translucent colour - rebuilt for each frame
    private void BuildDrawing(double seconds)
    {
        shapes.Clear();
        float sweep = (float)PingPong(seconds, 4);
        drawing.DrawCircle(70 + (240 * sweep), 92, 52, filled: true, color: Color.FromRgb(255, 120, 90));

        var star = new List<(float X, float Y)>();
        double turn = seconds * 0.6;
        for (int i = 0; i < 10; i++)
        {
            double angle = turn + (i * Math.PI / 5);
            float radius = i % 2 == 0 ? 92f : 40f;
            star.Add(((float)(192 + (radius * Math.Cos(angle))), (float)(228 + (radius * Math.Sin(angle)))));
        }

        drawing.DrawPolyline(star, 1, Color.FromRgb(120, 220, 140), closed: true, filled: true);
        drawing.DrawText("MASK", 192, 362, 64, Color.FromRgba(200, 200, 255, 170), TextAlign.Center);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        drawing.Dispose();
    }
}
