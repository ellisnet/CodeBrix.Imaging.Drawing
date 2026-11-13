using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Glyphs as paths. Top: outlined text (a stroke only) and gradient-filled text with an outline. Middle: the glyph
/// outlines taken into a <see cref="PathShape" /> and drawn on by trimming, then filled in. Bottom: a line of text
/// set along a wave (<see cref="TextLayoutResult.GetOutlineAlongPath" />, each glyph turned to the path's
/// direction), sliding along it.
/// </summary>
internal sealed class OutlinedTextScene : DemoScene
{
    private const string Wave = "M 40 620 C 220 480, 420 480, 640 600 S 1060 720, 1240 560";

    private readonly DrawingSession session;

    /// <summary>Builds every shape and binding.</summary>
    public OutlinedTextScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "a stroke only, StrokePlacement.Outside (no overlap lines)", 40, 128, 13, Dim);
        Label(notes, session, "a LinearGradientFill inside a 4 px outside outline", 680, 128, 13, Dim);
        Label(notes, session, "GetOutline() into a PathShape: TrimEnd 0 -> 1 traces every contour, then the fill fades in", 40, 300, 13, Dim);
        Label(notes, session, "GetOutlineAlongPath(wave, distance): glyphs turned by the path's tangent", 40, 470, 13, Dim);

        DrawingLayer text = session.AddLayer("Text", Ink);
        session.ActiveLayer = text;

        TextLayoutResult outline = session.LayoutText("OUTLINE", 100);
        session.DrawShape(new TextBlockShape(outline, 40, 136, new PathStrokeStyle(5f), Ink) { StrokePlacement = TextStrokePlacement.Outside });

        TextLayoutResult gradient = session.LayoutText("Gradient", 100);
        var fill = new LinearGradientFill(new PointF(680, 150), new PointF(1120, 250), Color.FromRgb(255, 176, 72), Color.FromRgb(236, 90, 160));
        session.DrawShape(new TextBlockShape(gradient, 680, 136, new PathStrokeStyle(8f, PathStrokeCap.Round, PathStrokeJoin.Round), Color.FromRgb(250, 240, 255), null, fill)
        {
            StrokePlacement = TextStrokePlacement.Outside,
        });

        // Draw-on: the outlines are paths, so the phase-3a trim writes them out.
        TextLayoutResult drawOn = session.LayoutText("Draw on, then fill", 92);
        var strokes = new PathShape(drawOn.GetOutline(40, 304), new PathStrokeStyle(2f), Color.FromRgb(140, 200, 255)) { TrimEnd = 0f };
        session.DrawShape(strokes);
        session.Animations.Bind(strokes, AnimatedProperty.TrimEnd, new FloatTrack { { 0.3, 0f }, { 3.8, 1f, EasingKind.SineInOut } });
        var filled = new TextBlockShape(drawOn, 40, 304, Color.FromRgb(140, 200, 255)) { Opacity = 0f };
        session.DrawShape(filled);
        session.Animations.Bind(filled, AnimatedProperty.Opacity, new FloatTrack { { 3.6, 0f }, { 4.6, 1f, EasingKind.CubicOut } });

        // Text on a path, sliding along it.
        PathGeometry wave = PathGeometry.Parse(Wave);
        session.DrawPath(wave, 1.5f, Color.FromRgba(132, 138, 164, 90));
        TextLayoutResult along = session.LayoutText("letters ride the curve, each one turned to follow it", 30);
        float travel = wave.GetLength() - along.Width - 20;
        var alongFill = new LinearGradientFill(new PointF(40, 0), new PointF(1240, 0), Color.FromRgb(140, 230, 160), Color.FromRgb(255, 176, 72));
        session.DrawShape(new AnimatedShape(t => new PathShape(along.GetOutlineAlongPath(wave, 10 + (float)(PingPong(t, 8) * travel), -6f), alongFill)));
    }

    /// <inheritdoc />
    public override string Name => "outlined-text";

    /// <inheritdoc />
    public override string Title => "Outlined, gradient and drawn-on text";

    /// <inheritdoc />
    public override string Description => "Glyph outlines as PathGeometry: stroke and gradient fill, draw-on by trimming, text along a path";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
