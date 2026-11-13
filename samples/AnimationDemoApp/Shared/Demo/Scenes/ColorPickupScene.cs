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
/// Colour pickup: a moving picture (a sunset painted per pixel at HALF size - a sun sinking over hills and a winding
/// river) is turned into a brushstroke mosaic. The stroke layout's ownership map is made once; every frame each
/// stroke takes the mean colour of the picture beneath it (GetMeanColors, reading the half-size picture by the
/// nearest-pixel rule) and ApplyColors paints it back through the map, with dark seams between strokes.
/// </summary>
internal sealed class ColorPickupScene : DemoScene
{
    private const int MapWidth = 560;
    private const int MapHeight = 420;
    private const int PanelY = 150;
    private static readonly int[] PanelX = { 48, 672 };

    private readonly DrawingSession frame;
    private readonly DrawingOwnershipMap map;
    private readonly DrawingPlane seams;
    private readonly byte[] scratch = new byte[MapWidth * MapHeight * 4];

    /// <summary>Builds the frame, the stroke layout and its ownership map.</summary>
    public ColorPickupScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "the picture: DrawingPlaneImage, 280 x 210, painted per pixel (shown enlarged)", PanelX[0], PanelY - 14, 14, Ink);
        Label(captions, frame, "GetMeanColors(picture) -> ApplyColors: one colour per stroke", PanelX[1], PanelY - 14, 14, Ink);
        Label(captions, frame, "Each map pixel reads the picture pixel at floor((2x + 1) * 280 / 1120): half-size sources need no resizing.", PanelX[0], 600, 14, Dim);
        Label(captions, frame, "Means are alpha-weighted (transparent pixels lend no colour) and summed row by row on one thread - the same bits in both packages.", PanelX[0], 624, 14, Dim);

        using DrawingSession strokes = CreateSession(MapWidth, MapHeight, Color.Transparent);
        DrawingLayer layer = strokes.AddLayer("Strokes", Color.White);
        foreach (BrushStrokeShape stroke in BrushworkKit.Layout(MapWidth, MapHeight, 46f, 9)) layer.AddShape(stroke);
        map = strokes.ExportOwnershipMap(layer, new Size(MapWidth, MapHeight)).FillUnowned();
        seams = map.CreateBoundaryPlane(0.7f).Multiply(1.3f).Clamp(0f, 1f);
    }

    /// <inheritdoc />
    public override string Name => "colour-pickup";

    /// <inheritdoc />
    public override string Title => "Colour pickup: a picture as a brushstroke mosaic";

    /// <inheritdoc />
    public override string Description => "Each brushstroke of an ownership map takes the mean colour of the picture beneath it, every frame";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        DrawingPlaneImage picture = Picture(seconds);
        Vector4[] means = map.GetMeanColors(picture);
        DrawingPlaneImage mosaic = new DrawingPlaneImage(MapWidth, MapHeight);
        map.ApplyColors(mosaic, means);
        mosaic.BlendColor(Color.FromRgb(20, 16, 30), seams.Clone().Multiply(0.7f));

        ExportFrame(frame, rgba);
        PlaceImage(picture.Resized(MapWidth, MapHeight), scratch, rgba, PanelX[0], PanelY);
        PlaceImage(mosaic, scratch, rgba, PanelX[1], PanelY);
    }

    // A sunset at half size: sky gradient, a sinking sun with a glow, two rows of hills and a winding river.
    private static DrawingPlaneImage Picture(double seconds)
    {
        const int W = MapWidth / 2, H = MapHeight / 2;
        double sunX = 70 + (140 * (seconds / 8.0)), sunY = 60 + (50 * (seconds / 8.0));
        DrawingPlaneImage picture = new DrawingPlaneImage(W, H);
        picture.FillParallel((x, y) =>
        {
            double v = y / (double)H;
            double r = 0.95 - (0.55 * v), g = 0.55 - (0.35 * v), b = 0.35 + (0.25 * v);
            double dx = x - sunX, dy = y - sunY;
            double d = Math.Sqrt((dx * dx) + (dy * dy));
            double glow = Math.Exp(-d / 40.0);
            r += 0.5 * glow;
            g += 0.35 * glow;
            if (d < 18) (r, g, b) = (1.0, 0.92, 0.6);
            double far = 120 + (14 * Math.Sin((x / 37.0) + 1)) + (8 * Math.Sin(x / 11.0));
            double near = 160 + (18 * Math.Sin((x / 53.0) + 2)) + (6 * Math.Sin((x / 7.0) + seconds));
            if (y > far) (r, g, b) = (0.32, 0.22 + (0.1 * glow), 0.36);
            if (y > near) (r, g, b) = (0.12, 0.28, 0.18);
            double river = W / 2.0 + (40 * Math.Sin((y / 14.0) - (seconds * 0.8))) + ((y - near) * 0.6);
            if (y > near && Math.Abs(x - river) < 4 + ((y - near) * 0.35)) (r, g, b) = (0.55 + (0.3 * glow), 0.6, 0.85);
            return new Vector4((float)r, (float)g, (float)b, 1f);
        });
        return picture;
    }

    /// <inheritdoc />
    public override void Dispose() => frame.Dispose();
}
