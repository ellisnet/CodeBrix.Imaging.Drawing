using System;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Brush texture, built up in four steps on eight large brushstrokes that slowly bend and turn: flat colour through
/// the ownership map; bristle ridges from each pixel's position ACROSS its own stroke (FillByElement, the stroke's
/// own axes); a light-to-dark ramp ALONG each stroke; and dark seams where strokes meet (CreateBoundaryPlane).
/// </summary>
internal sealed class BrushTextureScene : DemoScene
{
    private const int PanelWidth = 290;
    private const int PanelHeight = 340;
    private const int PanelY = 160;
    private static readonly int[] PanelX = { 22, 336, 650, 964 };
    private static readonly Color Paper = Color.FromRgb(236, 228, 210);

    private static readonly Vector4[] Colors =
    {
        new Vector4(0.16f, 0.27f, 0.62f, 1f), new Vector4(0.91f, 0.62f, 0.15f, 1f), new Vector4(0.2f, 0.52f, 0.47f, 1f),
        new Vector4(0.78f, 0.25f, 0.18f, 1f), new Vector4(0.33f, 0.4f, 0.76f, 1f), new Vector4(0.95f, 0.82f, 0.36f, 1f),
        new Vector4(0.12f, 0.36f, 0.3f, 1f), new Vector4(0.62f, 0.3f, 0.52f, 1f),
    };

    private readonly DrawingSession frame;
    private readonly DrawingSession drawing;
    private readonly DrawingLayer paint;
    private readonly float[] phases = new float[Colors.Length];
    private readonly byte[] scratch = new byte[PanelWidth * PanelHeight * 4];

    /// <summary>Builds the frame and the strokes' session.</summary>
    public BrushTextureScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        string[] titles = { "1  ApplyColors: flat colour", "2  x bristle ridges", "3  x ramp along the stroke", "4  + dark seams" };
        string[] notes =
        {
            "the ownership map, one colour per stroke",
            "FillByElement: 1 + 0.2 sin(Across x 1.1 + phase)",
            "FillByElement: 0.8 + 0.25 x NormalizedAlong",
            "CreateBoundaryPlane(1.1) through BlendColor",
        };
        for (int i = 0; i < 4; i++)
        {
            Label(captions, frame, titles[i], PanelX[i], PanelY + PanelHeight + 28, 15, Ink);
            Label(captions, frame, notes[i], PanelX[i], PanelY + PanelHeight + 50, 13, Dim);
        }

        Label(captions, frame, "Across and Along are measured in each stroke's own axes (centre, angle, length, width from BrushStrokeShape.GetAxes),", 22, 640, 14, Dim);
        Label(captions, frame, "so the ridges turn and bend with the strokes. Paper (unowned pixels) is left untouched: unownedValue = 1.", 22, 662, 14, Dim);

        drawing = CreateSession(PanelWidth, PanelHeight, Color.Transparent);
        paint = drawing.AddLayer("Paint", Color.White);
        DrawingRandom random = new DrawingRandom(77);
        for (int i = 0; i < phases.Length; i++) phases[i] = (float)random.Uniform(0, 2 * Math.PI);
    }

    /// <inheritdoc />
    public override string Name => "brush-texture";

    /// <inheritdoc />
    public override string Title => "Brush texture: measuring pixels in a stroke's own axes";

    /// <inheritdoc />
    public override string Description => "Ownership map + FillByElement (along / across each stroke) + CreateBoundaryPlane - bristles, ramps and seams";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        paint.Clear();
        for (int i = 0; i < Colors.Length; i++)
        {
            double row = i / 2, column = i % 2;
            float bend = (float)(0.14 * Math.Sin((seconds * 0.9) + (i * 1.3)));
            float angle = (float)((column == 0 ? 29 : -23) + (14 * Math.Sin((seconds * 0.5) + i))); // degrees
            paint.AddShape(new BrushStrokeShape((float)(80 + (130 * column)), (float)(52 + (78 * row)), 230, 66, angle, bend));
        }

        DrawingOwnershipMap map = drawing.ExportOwnershipMap(paint, new Size(PanelWidth, PanelHeight));
        float shift = (float)seconds;
        DrawingPlane ridges = new DrawingPlane(PanelWidth, PanelHeight);
        map.FillByElement(ridges, p => (float)(1 + (0.2 * Math.Sin((p.Across * 1.1) + phases[p.ElementIndex] + shift))), 1f);
        DrawingPlane ramp = new DrawingPlane(PanelWidth, PanelHeight);
        map.FillByElement(ramp, p => (float)(0.8 + (0.25 * p.NormalizedAlong)), 1f);
        DrawingPlane seams = map.CreateBoundaryPlane(1.1f).Multiply(1.6f).Clamp(0f, 1f);

        DrawingPlaneImage flat = BrushworkKit.Flat(PanelWidth, PanelHeight, Paper);
        map.ApplyColors(flat, Colors);
        DrawingPlaneImage ridged = flat.Clone().Multiply(ridges);
        DrawingPlaneImage ramped = ridged.Clone().Multiply(ramp);
        DrawingPlaneImage finished = ramped.Clone().BlendColor(Color.FromRgb(24, 22, 40), seams.Multiply(0.8f));

        ExportFrame(frame, rgba);
        PlaceImage(flat, scratch, rgba, PanelX[0], PanelY);
        PlaceImage(ridged, scratch, rgba, PanelX[1], PanelY);
        PlaceImage(ramped, scratch, rgba, PanelX[2], PanelY);
        PlaceImage(finished, scratch, rgba, PanelX[3], PanelY);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        drawing.Dispose();
    }
}
