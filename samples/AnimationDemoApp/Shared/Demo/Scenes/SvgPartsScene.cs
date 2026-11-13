using System;
using System.Globalization;
using System.IO;
using System.Text;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;
#if DEMO_NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
#endif

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// SVG as a session element (NoSkia package only). Two documents written in this file - a gear train (gear outlines
/// generated as SVG path data) and a clock - are loaded with DrawingSvg and drawn with DrawingSvgShape. Per-part
/// animation by element id: the gears turn at their gear ratios about their own centres, the clock's hands turn about
/// the clock's centre, the clock's tick marks draw on by trimming their strokes, and the gear train's arrow fades in.
/// Whole-element animation: the gear train fades in and the clock slides and grows into place, as any element would.
/// The Skia painter shows a placeholder for this scene: SVG exists only in the NoSkia package.
/// </summary>
internal sealed class SvgPartsScene : DemoScene
{
#if DEMO_NOSKIA
    private readonly DrawingSession session;
    private readonly DrawingSvg gears;
    private readonly DrawingSvg clock;

    /// <summary>Loads both documents and binds every part.</summary>
    public SvgPartsScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "parts by id: gear1 / gear2 / gear3 turn at their ratios (pivot = gear centre); arrow fades in", 40, 128, 13, Dim);
        Label(notes, session, "whole element: the gear train fades in", 40, 680, 13, Dim);
        Label(notes, session, "parts: hands turn about the centre, ticks draw on (TrimEnd)", 820, 128, 13, Dim);
        Label(notes, session, "whole element: the clock slides in and grows", 820, 680, 13, Dim);

        DrawingLayer art = session.AddLayer("Art", Ink);
        session.ActiveLayer = art;

        gears = new DrawingSvg();
        if (!gears.FromSvg(GearTrain())) throw new InvalidOperationException("The gear train did not load.");
        DrawingSvgShape train = session.DrawSvg(gears, new RectangleF(40, 140, 720, 520));
        session.Animations.Bind(train, AnimatedProperty.Opacity, new FloatTrack { { 0, 0f }, { 0.8, 1f, EasingKind.CubicOut } });
        BindGear(train, "gear1", new PointF(180, 200), 45f);
        BindGear(train, "gear2", new PointF(340, 200), -45f * 100f / 60f);
        BindGear(train, "gear3", new PointF(410.71f, 270.71f), 45f * 100f / 40f);
        train.BindPart(session.Animations, "arrow", AnimatedProperty.Opacity, new FloatTrack { { 1.0, 0f }, { 2.0, 1f } });
        train.BindPart(session.Animations, "arrow", AnimatedProperty.TrimEnd, new FloatTrack { { 1.0, 0f }, { 2.5, 1f, EasingKind.SineInOut } });

        clock = new DrawingSvg();
        clock.Fonts.RegisterFont(Path.Combine(AppContext.BaseDirectory, "assets", "Fonts", "Roboto-Regular.ttf"));
        if (!clock.FromSvg(Clock())) throw new InvalidOperationException("The clock did not load.");
        DrawingSvgShape face = session.DrawSvg(clock, new RectangleF(840, 170, 380, 380));
        face.Transform = DrawingTransform.Identity.WithAnchor(new PointF(1030, 360));
        session.Animations.Bind(face, AnimatedProperty.Position, new PointTrack { { 0, new PointF(420, 0) }, { 1.4, new PointF(0, 0), EasingKind.CubicOut } });
        session.Animations.Bind(face, AnimatedProperty.Scale, new FloatTrack { { 0, 0.5f }, { 1.6, 1f, EasingKind.BackOut } });
        var pivot = new PointF(150, 150);
        foreach (string hand in new[] { "hour", "minute", "second" }) face.GetPart(hand).Pivot = pivot;
        face.BindPart(session.Animations, "second", AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 8, 720f } });
        face.BindPart(session.Animations, "minute", AnimatedProperty.Rotation, new FloatTrack { { 0, 60f }, { 8, 120f } });
        face.BindPart(session.Animations, "hour", AnimatedProperty.Rotation, new FloatTrack { { 0, 300f }, { 8, 305f } });
        face.BindPart(session.Animations, "ticks", AnimatedProperty.TrimEnd, new FloatTrack { { 0.8, 0f }, { 2.6, 1f, EasingKind.SineInOut } });
    }

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        session.Dispose();
        gears.Dispose();
        clock.Dispose();
    }

    private void BindGear(DrawingSvgShape train, string id, PointF centre, float degreesPerSecond)
    {
        train.GetPart(id).Pivot = centre;
        train.BindPart(session.Animations, id, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 8, degreesPerSecond * 8f } });
    }
#else
    /// <summary>Never drawn by the Skia painter (it shows a placeholder for NoSkia-only scenes).</summary>
    /// <param name="seconds">The time within the scene.</param>
    /// <param name="rgba">The frame buffer, left as it is.</param>
    public override void Render(double seconds, Span<byte> rgba)
    {
    }
#endif

    /// <inheritdoc />
    public override string Name => "svg-parts";

    /// <inheritdoc />
    public override string Title => "SVG elements and parts (NoSkia)";

    /// <inheritdoc />
    public override string Description => "DrawingSvgShape: a loaded SVG as an element; GetPart(id) / BindPart turn, fade and trim parts at draw time";

    /// <inheritdoc />
    public override SceneAvailability Availability => SceneAvailability.NoSkiaOnly;

    //A 600 x 400 gear train: three meshing gears (20, 12 and 8 teeth on one tooth pitch), each turned at load so a
    //  tooth of one sits in a gap of the next, and a stroked arrow showing the first gear's direction
    private static string GearTrain()
    {
        var svg = new StringBuilder();
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"400\" viewBox=\"0 0 600 400\">");
        svg.Append("<rect x=\"0\" y=\"0\" width=\"600\" height=\"400\" rx=\"18\" fill=\"#1b1f30\"/>");
        svg.Append(Gear("gear1", 180, 200, 100, 20, -7.65, "#f0a848"));
        svg.Append(Gear("gear2", 340, 200, 60, 12, 152.25, "#78b4ff"));
        svg.Append(Gear("gear3", 410.71, 270.71, 40, 8, 183.375, "#8ce6a0"));
        svg.Append("<path id=\"arrow\" d=\"M 66 120 A 140 140 0 0 1 180 60\" fill=\"none\" stroke=\"#ecedf4\" stroke-width=\"5\" stroke-linecap=\"round\"/>");
        svg.Append("<path d=\"M 168 48 L 186 60 L 168 72\" fill=\"none\" stroke=\"#ecedf4\" stroke-width=\"5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string Gear(string id, double cx, double cy, double pitchRadius, int teeth, double turn, string color)
    {
        double step = 2 * Math.PI / teeth;
        double outer = pitchRadius + 9, root = pitchRadius - 9, hole = pitchRadius * 0.28;
        var d = new StringBuilder();
        for (int k = 0; k < teeth; k++)
        {
            double a = k * step;
            AppendPoint(d, k == 0 ? "M" : "L", cx, cy, root, a);
            AppendPoint(d, "L", cx, cy, outer, a + (0.2 * step));
            AppendPoint(d, "L", cx, cy, outer, a + (0.45 * step));
            AppendPoint(d, "L", cx, cy, root, a + (0.65 * step));
        }
        d.Append(" Z ");
        d.Append(string.Format(CultureInfo.InvariantCulture, "M {0} {1} A {2} {2} 0 1 0 {3} {1} A {2} {2} 0 1 0 {0} {1} Z",
            cx + hole, cy, hole, cx - hole));
        return string.Format(CultureInfo.InvariantCulture,
            "<g id=\"{0}\" transform=\"rotate({1} {2} {3})\"><path d=\"{4}\" fill=\"{5}\" fill-rule=\"evenodd\" stroke=\"#0c0e18\" stroke-width=\"2\"/>"
            + "<circle cx=\"{2}\" cy=\"{3}\" r=\"{6}\" fill=\"none\" stroke=\"#0c0e18\" stroke-width=\"3\"/></g>",
            id, turn, cx, cy, d, color, pitchRadius * 0.55);
    }

    private static void AppendPoint(StringBuilder d, string verb, double cx, double cy, double radius, double angle)
        => d.Append(string.Format(CultureInfo.InvariantCulture, "{0} {1:0.###} {2:0.###} ", verb, cx + (radius * Math.Cos(angle)), cy + (radius * Math.Sin(angle))));

    //A 300 x 300 clock: a face, twelve tick marks in one group (trimmed together), three hands pointing at 12 (turned
    //  by the scene) and a label in the registered font
    private static string Clock()
    {
        var svg = new StringBuilder();
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"300\" height=\"300\" viewBox=\"0 0 300 300\">");
        svg.Append("<circle id=\"face\" cx=\"150\" cy=\"150\" r=\"140\" fill=\"#ecedf4\" stroke=\"#f0a848\" stroke-width=\"8\"/>");
        svg.Append("<g id=\"ticks\" stroke=\"#1b1f30\" stroke-linecap=\"round\">");
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            double inner = i % 3 == 0 ? 100 : 112;
            svg.Append(string.Format(CultureInfo.InvariantCulture, "<line x1=\"{0:0.###}\" y1=\"{1:0.###}\" x2=\"{2:0.###}\" y2=\"{3:0.###}\" stroke-width=\"{4}\"/>",
                150 + (inner * Math.Sin(a)), 150 - (inner * Math.Cos(a)), 150 + (124 * Math.Sin(a)), 150 - (124 * Math.Cos(a)), i % 3 == 0 ? 7 : 4));
        }
        svg.Append("</g>");
        svg.Append("<text x=\"150\" y=\"205\" font-family=\"Roboto\" font-size=\"18\" text-anchor=\"middle\" fill=\"#848aa4\">PER-PART</text>");
        svg.Append("<line id=\"hour\" x1=\"150\" y1=\"150\" x2=\"150\" y2=\"88\" stroke=\"#1b1f30\" stroke-width=\"10\" stroke-linecap=\"round\"/>");
        svg.Append("<line id=\"minute\" x1=\"150\" y1=\"150\" x2=\"150\" y2=\"50\" stroke=\"#1b1f30\" stroke-width=\"6\" stroke-linecap=\"round\"/>");
        svg.Append("<line id=\"second\" x1=\"150\" y1=\"170\" x2=\"150\" y2=\"40\" stroke=\"#e0506e\" stroke-width=\"3\" stroke-linecap=\"round\"/>");
        svg.Append("<circle cx=\"150\" cy=\"150\" r=\"8\" fill=\"#e0506e\"/>");
        svg.Append("</svg>");
        return svg.ToString();
    }
}
