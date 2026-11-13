using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;
using CodeBrix.Imaging.PixelFormats;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Multi-stop color ramps: each ramp is drawn twice - once sampled with <see cref="ColorRamp.At(double)" /> into
/// thin bars, once as a <see cref="LinearGradientFill" /> built from the ramp's own stops - to show that both
/// interpolate in the same space (straight sRGB). A marker sweeps each ramp, with the color it reads shown beside it.
/// </summary>
internal sealed class ColorRampScene : DemoScene
{
    private const float StripLeft = 230;
    private const float StripWidth = 640;
    private const float StripHeight = 34;
    private const float RowTop = ContentTop + 28;
    private const float RowHeight = 148;
    private const int Samples = 128;

    private readonly (string Label, ColorRamp Ramp)[] ramps =
    {
        ("Venus thread: rose, copper, violet", new ColorRamp(Color.FromRgb(246, 112, 168), Color.FromRgb(232, 146, 92), Color.FromRgb(156, 122, 248))),
        ("Sunset: five evenly spaced stops", new ColorRamp(Color.FromRgb(20, 24, 82), Color.FromRgb(120, 40, 140), Color.FromRgb(232, 72, 96), Color.FromRgb(255, 160, 64), Color.FromRgb(255, 236, 160))),
        ("Alpha: opaque teal to transparent", new ColorRamp(Color.FromRgba(40, 200, 190, 255), Color.FromRgba(40, 200, 190, 0))),
        ("Hard edges: stops sharing a position", new ColorRamp(new[]
        {
            new ColorStop(0f, Color.FromRgb(230, 60, 60)), new ColorStop(0.33f, Color.FromRgb(230, 60, 60)),
            new ColorStop(0.33f, Color.FromRgb(240, 200, 40)), new ColorStop(0.66f, Color.FromRgb(240, 200, 40)),
            new ColorStop(0.66f, Color.FromRgb(60, 120, 230)), new ColorStop(1f, Color.FromRgb(60, 120, 230)),
        })),
    };

    private readonly DrawingSession session;
    private readonly DrawingLayer moving;

    /// <summary>Builds the strips (drawn once; only the markers move).</summary>
    public ColorRampScene()
    {
        session = CreateFrameSession();
        DrawingLayer strips = session.AddLayer("Strips", Ink);
        for (int r = 0; r < ramps.Length; r++)
        {
            float top = RowTop + (r * RowHeight);
            Label(strips, session, ramps[r].Label, StripLeft, top + 14, 15, Ink);
            Label(strips, session, "At(t), sampled", StripLeft - 14, top + 46, 12, Dim, TextAlign.Right);
            Label(strips, session, "gradient fill", StripLeft - 14, top + 46 + StripHeight + 6, 12, Dim, TextAlign.Right);
            Checkerboard(strips, StripLeft, top + 24, StripWidth, (StripHeight * 2) + 6);

            float cell = StripWidth / Samples;
            for (int i = 0; i < Samples; i++)
            {
                double t = (i + 0.5) / Samples;
                strips.AddShape(new RectangleShape(StripLeft + (i * cell), top + 24, cell + 0.5f, StripHeight, 1, ramps[r].Ramp.At(t), isFilled: true));
            }

            strips.AddShape(new RectangleShape(StripLeft, top + 30 + StripHeight, StripWidth, StripHeight,
                new LinearGradientFill(new PointF(StripLeft, 0), new PointF(StripLeft + StripWidth, 0), ramps[r].Ramp.GetStops())));
        }

        moving = session.AddLayer("Markers", Ink);
    }

    /// <inheritdoc />
    public override string Name => "ramps";

    /// <inheritdoc />
    public override string Title => "Color ramps";

    /// <inheritdoc />
    public override string Description => "ColorRamp.At(t) sampled into bars above a LinearGradientFill made from the same ColorStops - the same straight-sRGB interpolation";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        double t = Easing.SineInOut(0.5 - (0.5 * Math.Cos(2 * Math.PI * seconds / 4.0)));
        moving.Clear();
        for (int r = 0; r < ramps.Length; r++)
        {
            float top = RowTop + (r * RowHeight);
            float x = StripLeft + (float)(t * StripWidth);
            moving.AddShape(new LineShape(x, top + 20, x, top + 34 + (StripHeight * 2), 3, Ink));

            Color color = ramps[r].Ramp.At(t);
            Rgba32 pixel = color.ToPixel<Rgba32>();
            Checkerboard(moving, 920, top + 24, 74, 74);
            moving.AddShape(new RectangleShape(920, top + 24, 74, 74, 1, color, isFilled: true, cornerRadius: 0));
            Label(moving, session, string.Format(CultureInfo.InvariantCulture, "At({0:0.00})", t), 1012, top + 50, 15, Ink);
            Label(moving, session, string.Format(CultureInfo.InvariantCulture, "R {0}  G {1}  B {2}  A {3}", pixel.R, pixel.G, pixel.B, pixel.A), 1012, top + 76, 13, Dim);
        }

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private static void Checkerboard(DrawingLayer layer, float x, float y, float width, float height)
    {
        const float square = 10;
        layer.AddShape(new RectangleShape(x, y, width, height, 1, Color.FromRgb(70, 72, 84), isFilled: true));
        for (float row = 0; row < height; row += square)
        {
            for (float col = ((int)(row / square) % 2) * square; col < width; col += square * 2)
            {
                layer.AddShape(new RectangleShape(x + col, y + row, Math.Min(square, width - col), Math.Min(square, height - row), 1,
                    Color.FromRgb(110, 112, 124), isFilled: true));
            }
        }
    }
}
