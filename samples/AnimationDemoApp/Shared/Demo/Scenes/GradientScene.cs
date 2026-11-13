using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Gradient fills on filled shapes: a <see cref="LinearGradientFill" /> whose direction turns, the three
/// <see cref="GradientSpread" /> modes, <see cref="RadialGradientFill" /> glows on a circle and an ellipse, a
/// star polygon and a line of text painted by gradients. Fills are immutable, so every moving gradient is a new
/// fill on a new shape each frame - shapes stay simple values.
/// </summary>
internal sealed class GradientScene : DemoScene
{
    private static readonly ColorStop[] Aurora =
    {
        new ColorStop(0f, Color.FromRgb(40, 220, 170)),
        new ColorStop(0.5f, Color.FromRgb(70, 110, 255)),
        new ColorStop(1f, Color.FromRgb(230, 80, 200)),
    };

    private readonly DrawingSession session;
    private readonly DrawingLayer moving;

    /// <summary>Builds the labels (drawn once; the filled shapes are rebuilt every frame).</summary>
    public GradientScene()
    {
        session = CreateFrameSession();
        DrawingLayer labels = session.AddLayer("Labels", Dim);
        Label(labels, session, "LinearGradientFill, direction turning", 50, 128, 14, Dim);
        Label(labels, session, "GradientSpread.Pad", 50, 420, 13, Dim);
        Label(labels, session, "GradientSpread.Repeat", 50, 500, 13, Dim);
        Label(labels, session, "GradientSpread.Reflect", 50, 580, 13, Dim);
        Label(labels, session, "RadialGradientFill: a glow with a moving highlight", 470, 128, 14, Dim);
        Label(labels, session, "radial fill on an ellipse", 470, 540, 13, Dim);
        Label(labels, session, "polygon and text with linear fills", 880, 128, 14, Dim);
        moving = session.AddLayer("Shapes", Ink);
    }

    /// <inheritdoc />
    public override string Name => "gradients";

    /// <inheritdoc />
    public override string Title => "Gradient fills";

    /// <inheritdoc />
    public override string Description => "Linear and radial gradients with color stops on rectangles, circles, ellipses, polygons and text";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        moving.Clear();

        // A rounded rectangle whose gradient direction turns.
        double angle = seconds * 0.9;
        PointF center = new PointF(230, 265);
        PointF direction = new PointF((float)Math.Cos(angle) * 170, (float)Math.Sin(angle) * 170);
        moving.AddShape(new RectangleShape(50, 145, 360, 240,
            new LinearGradientFill(new PointF(center.X - direction.X, center.Y - direction.Y), new PointF(center.X + direction.X, center.Y + direction.Y), Aurora),
            cornerRadius: 24));

        // The three spread modes, with a short gradient sliding along each bar.
        float offset = (float)(Easing.SineInOut((seconds % 4) / 4) * 120);
        GradientSpread[] spreads = { GradientSpread.Pad, GradientSpread.Repeat, GradientSpread.Reflect };
        for (int i = 0; i < spreads.Length; i++)
        {
            float y = 430 + (i * 80);
            moving.AddShape(new RectangleShape(50, y, 360, 44,
                new LinearGradientFill(new PointF(150 + offset, 0), new PointF(210 + offset, 0), new[]
                {
                    new ColorStop(0f, Color.FromRgb(255, 190, 70)), new ColorStop(1f, Color.FromRgb(120, 40, 160)),
                }, spreads[i]), cornerRadius: 6));
        }

        // A glowing orb: a radial gradient whose highlight circles the center, inside a pulsing halo.
        float pulse = (float)Easing.SineInOut(0.5 + (0.5 * Math.Sin(seconds * 2.2)));
        PointF orb = new PointF(645, 320);
        float halo = 150 + (40 * pulse);
        moving.AddShape(new CircleShape(orb.X, orb.Y, halo, new RadialGradientFill(orb, halo, new[]
        {
            new ColorStop(0.55f, Color.FromRgba(255, 200, 120, 150)), new ColorStop(1f, Color.FromRgba(255, 200, 120, 0)),
        })));
        PointF highlight = new PointF(orb.X + ((float)Math.Cos(seconds * 1.4) * 45), orb.Y + ((float)Math.Sin(seconds * 1.4) * 45));
        moving.AddShape(new CircleShape(orb.X, orb.Y, 90, new RadialGradientFill(highlight, 150, new[]
        {
            new ColorStop(0f, Color.FromRgb(255, 250, 230)), new ColorStop(0.35f, Color.FromRgb(255, 170, 70)), new ColorStop(1f, Color.FromRgb(150, 40, 30)),
        })));

        // An ellipse lit by a radial "spotlight" that sweeps along it.
        PointF spot = new PointF(645 + ((float)Math.Sin(seconds * 1.1) * 130), 610);
        moving.AddShape(new EllipseShape(645, 610, 165, 55, new RadialGradientFill(spot, 120, new[]
        {
            new ColorStop(0f, Color.FromRgb(230, 240, 255)), new ColorStop(1f, Color.FromRgb(30, 50, 110)),
        })));

        // A turning star with a linear fill, and text painted by a gradient that slides across it.
        PointF[] star = new PointF[10];
        for (int i = 0; i < star.Length; i++)
        {
            double a = (seconds * 0.6) + (i * Math.PI / 5) - (Math.PI / 2);
            float radius = i % 2 == 0 ? 140 : 60;
            star[i] = new PointF(1060 + (radius * (float)Math.Cos(a)), 310 + (radius * (float)Math.Sin(a)));
        }

        moving.AddShape(new PolylineShape(star, new LinearGradientFill(new PointF(920, 170), new PointF(1200, 450), Aurora)));
        float sweep = (float)((seconds % 3) / 3 * 600) - 100;
        moving.AddShape(new TextShape("GRADIENT", 1060, 600, 78, session.Fonts,
            new LinearGradientFill(new PointF(860 + sweep, 0), new PointF(1010 + sweep, 0), new[]
            {
                new ColorStop(0f, Color.FromRgb(120, 170, 255)), new ColorStop(0.5f, Color.FromRgb(255, 255, 255)), new ColorStop(1f, Color.FromRgb(120, 170, 255)),
            }, GradientSpread.Pad), TextAlign.Center, 0.02f));

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
