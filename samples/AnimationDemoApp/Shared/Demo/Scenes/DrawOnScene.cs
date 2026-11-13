using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Draw-on with path trimming. Top: a signature-like flourish (SVG path data) written out by animating its
/// <see cref="PathShape.TrimEnd" /> from 0 to 1, a glowing pen nib riding exactly on the growing tip (the same
/// progress bound with <see cref="DrawingAnimations.BindAlongPath" />), then an underline. Bottom: a loading ring
/// (TrimStart and TrimEnd chasing each other while TrimOffset turns), a fixed-length piece running round a star
/// (TrimOffset wrapping past the start), and "marching ants" (a dashed outline whose dash offset moves, rebuilt by an
/// <see cref="AnimatedShape" />).
/// </summary>
internal sealed class DrawOnScene : DemoScene
{
    private const string Flourish =
        "M 150 330 C 190 200, 280 190, 260 280 C 245 350, 170 380, 190 300 C 210 230, 330 220, 320 300 " +
        "C 315 345, 360 350, 385 300 S 440 220, 455 290 C 465 335, 510 345, 530 300 C 545 265, 590 262, 592 300 " +
        "C 594 340, 640 348, 670 300 Q 720 220 770 300 T 870 300 C 895 260, 935 250, 960 250 " +
        "C 1000 250, 990 330, 1040 320 C 1080 312, 1100 270, 1130 240";

    private readonly DrawingSession session;

    /// <summary>Builds the paths and their bindings.</summary>
    public DrawOnScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "TrimEnd 0 -> 1 writes the path; the nib follows GetPointAt(progress)", 40, 140, 14, Dim);
        Label(notes, session, "loading ring: TrimStart / TrimEnd / TrimOffset", 180, 470, 14, Dim, TextAlign.Center);
        Label(notes, session, "a 30% piece running round (wraps the start)", 640, 470, 14, Dim, TextAlign.Center);
        Label(notes, session, "marching ants: a moving dash offset", 1110, 470, 14, Dim, TextAlign.Center);

        DrawingLayer ink = session.AddLayer("Ink", Ink);
        session.ActiveLayer = ink;
        PathGeometry flourish = PathGeometry.Parse(Flourish);
        session.DrawPath(flourish, 1.5f, Color.FromRgba(132, 138, 164, 60));
        var signature = new PathShape(flourish, new PathStrokeStyle(7f), Color.FromRgb(120, 180, 255)) { TrimEnd = 0f };
        session.DrawShape(signature);
        var writing = new FloatTrack { { 0.3, 0f }, { 5.0, 1f, EasingKind.SineInOut } };
        session.Animations.Bind(signature, AnimatedProperty.TrimEnd, writing);

        var underline = new PathShape(PathGeometry.Parse("M 170 400 C 450 380, 800 390, 1110 370"), new PathStrokeStyle(4f), Accent) { TrimEnd = 0f };
        session.DrawShape(underline);
        session.Animations.Bind(underline, AnimatedProperty.TrimEnd, new FloatTrack { { 5.0, 0f }, { 6.0, 1f, EasingKind.CubicOut } });

        DrawingShape nib = session.DrawCircle(0, 0, 7, filled: true, color: Color.White);
        nib.Effects = DrawingEffects.Glow(18, Color.FromRgba(120, 180, 255, 255), 1.6f);
        session.Animations.BindAlongPath(nib, flourish, writing);
        session.Animations.Bind(nib, AnimatedProperty.Opacity, new FloatTrack { { 0.2, 0f }, { 0.4, 1f }, { 4.9, 1f }, { 5.3, 0f } });

        // The loading ring: the ends chase each other while the whole piece turns.
        var ring = new PathShape(PathGeometry.Circle(new PointF(180, 590), 60), new PathStrokeStyle(10f), Accent);
        session.DrawShape(ring);
        session.DrawCircle(180, 590, 60, thickness: 10, color: Color.FromRgba(255, 176, 72, 40));
        FloatTrack head = Looping(new FloatTrack { { 0, 0.05f }, { 0.9, 0.75f, EasingKind.CubicInOut }, { 1.8, 0.8f } });
        FloatTrack tail = Looping(new FloatTrack { { 0, 0f }, { 0.9, 0.05f }, { 1.8, 0.75f, EasingKind.CubicInOut } });
        session.Animations.Bind(ring, AnimatedProperty.TrimEnd, head);
        session.Animations.Bind(ring, AnimatedProperty.TrimStart, tail);
        session.Animations.Bind(ring, AnimatedProperty.TrimOffset, new FloatTrack { { 0, 0f }, { 8, 3f } });

        // A fixed piece running round a star: start 0, end 0.3, the offset turning.
        PathGeometry star = PathGeometry.Star(new PointF(640, 600), 95, 42, 5);
        session.DrawPath(star, 2f, Color.FromRgba(132, 138, 164, 90));
        var runner = new PathShape(star, new PathStrokeStyle(8f, PathStrokeCap.Round, PathStrokeJoin.Round), Color.FromRgb(140, 230, 160)) { TrimEnd = 0.3f };
        session.DrawShape(runner);
        session.Animations.Bind(runner, AnimatedProperty.TrimOffset, new FloatTrack { { 0, 0f }, { 8, 2f } });

        // Marching ants: a dashed rounded rectangle rebuilt for each moment with a moving dash offset.
        PathGeometry box = PathGeometry.RoundedRectangle(new RectangleF(1010, 520, 200, 140), 18);
        var dashes = new PathStrokeStyle(4f, PathStrokeCap.Butt, PathStrokeJoin.Round, 4f, new[] { 14f, 9f });
        session.DrawShape(new AnimatedShape(t => new PathShape(box, dashes.WithDashOffset((float)(-t * 46.0) % 23f), Ink)));
    }

    /// <inheritdoc />
    public override string Name => "draw-on";

    /// <inheritdoc />
    public override string Title => "Draw-on: trimming a path";

    /// <inheritdoc />
    public override string Description => "PathGeometry.Parse (SVG path data), PathShape.TrimStart/TrimEnd/TrimOffset bound to tracks, BindAlongPath, dashes";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private static FloatTrack Looping(FloatTrack track)
    {
        track.LoopMode = TrackLoopMode.Loop;
        return track;
    }
}
