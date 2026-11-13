using System;
using System.Globalization;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// An ownership map, visualised: forty drifting brushstrokes and three circles on one layer; the map says which
/// element owns each pixel (the last one painted over the pixel's centre). Left: every element in its own colour,
/// unowned pixels dark. Right: the same map after FillUnowned - every gap given to the nearest owner - with the
/// seams between owners (CreateBoundaryPlane) drawn dark. Read-outs show the element and pixel counts.
/// </summary>
internal sealed class OwnershipScene : DemoScene
{
    private const int MapWidth = 560;
    private const int MapHeight = 420;
    private const int PanelY = 150;
    private const int StrokeCount = 40;
    private static readonly int[] PanelX = { 48, 672 };

    private readonly DrawingSession frame;
    private readonly DrawingLayer readout;
    private readonly DrawingSession drawing;
    private readonly DrawingLayer paint;
    private readonly (double X, double Y, double Length, double Angle, double Speed, double Phase)[] strokes;
    private readonly Vector4[] colors = BrushworkKit.DistinctColors(StrokeCount + 3);
    private readonly byte[] scratch = new byte[MapWidth * MapHeight * 4];

    /// <summary>Builds the frame and the drifting layout.</summary>
    public OwnershipScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "ExportOwnershipMap(layer, size) - one colour per element, unowned dark", PanelX[0], PanelY - 14, 14, Ink);
        Label(captions, frame, "FillUnowned() + CreateBoundaryPlane() seams", PanelX[1], PanelY - 14, 14, Ink);
        readout = frame.AddLayer("Readout", Dim);

        drawing = CreateSession(MapWidth, MapHeight, Color.Transparent);
        paint = drawing.AddLayer("Paint", Color.White);
        DrawingRandom random = new DrawingRandom(2024);
        strokes = new (double, double, double, double, double, double)[StrokeCount];
        for (int i = 0; i < StrokeCount; i++)
        {
            strokes[i] = (random.Uniform(0, MapWidth), random.Uniform(0, MapHeight), random.Uniform(60, 140), random.Uniform(0, 180),
                random.Uniform(-23, 23), random.Uniform(0, 2 * Math.PI));
        }
    }

    /// <inheritdoc />
    public override string Name => "ownership";

    /// <inheritdoc />
    public override string Title => "Ownership map: which element owns each pixel";

    /// <inheritdoc />
    public override string Description => "Hard-edged, last painted wins; Nobody where nothing covers a pixel; FillUnowned gives gaps to the nearest owner (exact Euclidean)";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        paint.Clear();
        for (int i = 0; i < StrokeCount; i++)
        {
            (double x, double y, double length, double angle, double speed, double phase) = strokes[i];
            double drift = Math.Sin((seconds * 0.6) + phase);
            paint.AddShape(new BrushStrokeShape((float)(x + (30 * drift)), (float)(y + (14 * Math.Cos((seconds * 0.5) + phase))),
                (float)length, (float)(length * 0.32), (float)(angle + (speed * seconds)), (float)(0.12 * drift)));
        }

        for (int c = 0; c < 3; c++)
        {
            double a = (seconds * 0.5) + (c * 2.1);
            paint.AddShape(new CircleShape((float)(280 + (170 * Math.Cos(a))), (float)(210 + (110 * Math.Sin(a))), 34, 1, null, true));
        }

        DrawingOwnershipMap map = drawing.ExportOwnershipMap(paint, new Size(MapWidth, MapHeight));
        DrawingOwnershipMap filled = map.FillUnowned();

        DrawingPlaneImage left = BrushworkKit.Flat(MapWidth, MapHeight, Color.FromRgb(26, 28, 40));
        map.ApplyColors(left, colors);
        DrawingPlaneImage right = BrushworkKit.Flat(MapWidth, MapHeight, Color.Black);
        filled.ApplyColors(right, colors);
        right.BlendColor(Color.FromRgb(14, 16, 26), filled.CreateBoundaryPlane(0.8f).Multiply(1.8f).Clamp(0f, 1f));

        readout.Clear();
        float under = PanelY + MapHeight + 30;
        Label(readout, frame, string.Format(CultureInfo.InvariantCulture, "{0} elements; {1:N0} of {2:N0} pixels unowned ({3:0.0} %)",
            map.ElementCount, map.UnownedCount, MapWidth * MapHeight, 100.0 * map.UnownedCount / (MapWidth * MapHeight)), PanelX[0], under, 15, Ink);
        int top = map[MapWidth / 2, MapHeight / 2];
        Label(readout, frame, top == DrawingOwnershipMap.Nobody ? "centre pixel: Nobody" : string.Format(CultureInfo.InvariantCulture,
            "centre pixel: element {0} ({1:N0} pixels, bounds {2})", top, map.GetPixelCount(top), map.GetBounds(top)), PanelX[0], under + 24, 14, Dim);
        Label(readout, frame, string.Format(CultureInfo.InvariantCulture, "after FillUnowned: {0:N0} unowned; ties go to the smaller x, then the smaller y",
            filled.UnownedCount), PanelX[1], under, 15, Ink);
        Label(readout, frame, "seams: owner differs from the left or upper neighbour, blurred (sigma 0.8)", PanelX[1], under + 24, 14, Dim);

        ExportFrame(frame, rgba);
        PlaceImage(left, scratch, rgba, PanelX[0], PanelY);
        PlaceImage(right, scratch, rgba, PanelX[1], PanelY);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        drawing.Dispose();
    }
}
