using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Hard-edged versus anti-aliased masks: one tiny drawing (a turning star, a circle, a brushstroke and a thin line,
/// 80 x 60 pixels) exported as its anti-aliased coverage mask and as its HARD-EDGED mask, both blown up five times
/// so every pixel shows, and the pixels where the two differ. The hard-edged mask follows one written-down rule -
/// a pixel is in when its centre is inside the shape - computed from geometry in shared code, so it is identical,
/// bit for bit, in both packages; the anti-aliased one is each package's own rasterizer.
/// </summary>
internal sealed class HardEdgeScene : DemoScene
{
    private const int DrawingWidth = 80;
    private const int DrawingHeight = 60;
    private const int Zoom = 5;
    private const int PanelY = 170;
    private static readonly int[] PanelX = { 32, 448, 864 };

    private readonly DrawingSession frame;
    private readonly DrawingSession drawing;
    private readonly DrawingLayer shapes;
    private readonly DrawingPlane soft = new DrawingPlane(DrawingWidth, DrawingHeight);
    private readonly DrawingPlane hard = new DrawingPlane(DrawingWidth, DrawingHeight);
    private readonly byte[] scratch = new byte[DrawingWidth * Zoom * DrawingHeight * Zoom * 4];

    /// <summary>Builds the frame, its captions and the tiny drawing's session.</summary>
    public HardEdgeScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        float below = PanelY + (DrawingHeight * Zoom) + 26;
        Label(captions, frame, "ExportMask(plane) - anti-aliased coverage", PanelX[0], below, 14, Ink);
        Label(captions, frame, "each package's own rasterizer: soft edge values", PanelX[0], below + 20, 13, Dim);
        Label(captions, frame, "ExportMask(plane, MaskMeaning.HardEdged)", PanelX[1], below, 14, Ink);
        Label(captions, frame, "0 or 1 per pixel - identical in both packages", PanelX[1], below + 20, 13, Dim);
        Label(captions, frame, "where the two differ", PanelX[2], below, 14, Ink);
        Label(captions, frame, "edge pixels only; brighter = bigger difference", PanelX[2], below + 20, 13, Dim);
        Label(captions, frame, "The rule: a pixel belongs to a shape when its CENTRE is inside it, under the shape's fill rule.", 32, 600, 16, Ink);
        Label(captions, frame, "A centre exactly on an edge counts on a left or top edge and not on a right or bottom edge, so neighbours never share a pixel.", 32, 626, 14, Dim);
        Label(captions, frame, "Used for: masks as exact data, ownership maps, anything that must come out the same in both packages.", 32, 650, 14, Dim);

        drawing = CreateSession(DrawingWidth, DrawingHeight, Color.Transparent);
        shapes = drawing.AddLayer("Shapes", Color.White);
    }

    /// <inheritdoc />
    public override string Name => "hard-edges";

    /// <inheritdoc />
    public override string Title => "Hard-edged masks: one pixel rule";

    /// <inheritdoc />
    public override string Description => "MaskMeaning.HardEdged: coverage by pixel centres, computed from geometry in shared code - the same bits in both packages";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        BuildDrawing(seconds);
        drawing.ExportMask(soft);
        drawing.ExportMask(hard, MaskMeaning.HardEdged);

        ExportFrame(frame, rgba);
        PlaceImage(Zoomed(soft, (v, _) => new Vector4(v, v, v, 1f)), scratch, rgba, PanelX[0], PanelY);
        PlaceImage(Zoomed(hard, (v, _) => new Vector4(v, v, v, 1f)), scratch, rgba, PanelX[1], PanelY);
        PlaceImage(Zoomed(soft, (v, i) =>
        {
            float d = MathF.Abs(v - hard.Data[i]);
            return d > 0f ? new Vector4(0.25f + (0.75f * d), 0.2f + (0.5f * d), 0.1f, 1f) : new Vector4(0.08f, 0.09f, 0.13f, 1f);
        }), scratch, rgba, PanelX[2], PanelY);
    }

    // A mask blown up so that each pixel is a Zoom x Zoom block, coloured per value.
    private static DrawingPlaneImage Zoomed(DrawingPlane mask, Func<float, int, Vector4> color)
    {
        DrawingPlaneImage small = new DrawingPlaneImage(DrawingWidth, DrawingHeight);
        small.Fill((x, y) => color(mask[x, y], (y * DrawingWidth) + x));
        return small.Resized(DrawingWidth * Zoom, DrawingHeight * Zoom, DrawingPlaneResampling.Nearest);
    }

    // A turning star, a circle that breathes, a brushstroke that swings and a thin line - rebuilt for each frame.
    private void BuildDrawing(double seconds)
    {
        shapes.Clear();
        List<(float X, float Y)> star = new List<(float, float)>();
        for (int i = 0; i < 10; i++)
        {
            double angle = (seconds * 0.4) + (i * Math.PI / 5);
            double radius = i % 2 == 0 ? 21 : 9;
            star.Add(((float)(22 + (radius * Math.Cos(angle))), (float)(24 + (radius * Math.Sin(angle)))));
        }

        drawing.DrawPolyline(star, 1, Color.White, closed: true, filled: true);
        drawing.DrawCircle(60, 18, (float)(9 + (3 * PingPong(seconds, 4))), filled: true, color: Color.White);
        drawing.DrawBrushStroke(56, 44, 34, 11, (float)(-23 + (29 * PingPong(seconds, 6))), 0.08f, Color.White);
        drawing.DrawLine(4, 56, 76, (float)(50 - (6 * PingPong(seconds, 3))), 1.6f, Color.White);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        drawing.Dispose();
    }
}
