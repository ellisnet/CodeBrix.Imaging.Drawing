using System;
using System.Globalization;
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
/// Motion along a path. Left: a dart flies a figure-eight, turned to the path's direction
/// (<see cref="DrawingAnimations.BindAlongPath" /> with orientToPath), with a ball following on the same path
/// without turning; a read-out shows the <see cref="PathPoint" /> at the dart (distance and heading) and a tangent
/// arrow. Right: a car (a <see cref="DrawingGroup" />, drawn facing right around its own origin) drives a rounded
/// track at an eased pace.
/// </summary>
internal sealed class MotionPathScene : DemoScene
{
    private readonly DrawingSession session;
    private readonly PathGeometry eight;
    private readonly FloatTrack dartProgress;
    private readonly DrawingLayer now;

    /// <summary>Builds the paths, the riders and their bindings.</summary>
    public MotionPathScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "orientToPath: the dart turns with the path; the ball does not", 60, 140, 14, Dim);
        Label(notes, session, "a group driving a rounded track, eased", 760, 140, 14, Dim);

        // A figure eight from two loops of cubic curves.
        eight = new PathGeometryBuilder()
            .MoveTo(360, 410)
            .CubicTo(new PointF(460, 280), new PointF(640, 300), new PointF(640, 410))
            .CubicTo(new PointF(640, 520), new PointF(460, 540), new PointF(360, 410))
            .CubicTo(new PointF(260, 280), new PointF(80, 300), new PointF(80, 410))
            .CubicTo(new PointF(80, 520), new PointF(260, 540), new PointF(360, 410))
            .Close()
            .Build();
        PathGeometry track = PathGeometry.RoundedRectangle(new RectangleF(780, 200, 420, 380), 120);

        DrawingLayer paths = session.AddLayer("Paths", Color.FromRgba(132, 138, 164, 110));
        session.ActiveLayer = paths;
        session.DrawShape(new PathShape(eight, new PathStrokeStyle(2f, dashPattern: new[] { 8f, 8f })));
        session.DrawShape(new PathShape(track, new PathStrokeStyle(34f), Color.FromRgb(46, 50, 66)));
        session.DrawShape(new PathShape(track, new PathStrokeStyle(2f, dashPattern: new[] { 18f, 14f }), Color.FromRgba(236, 236, 244, 120)));

        DrawingLayer riders = session.AddLayer("Riders", Accent);
        session.ActiveLayer = riders;
        dartProgress = new FloatTrack { { 0, 0f }, { 4, 1f } };
        dartProgress.LoopMode = TrackLoopMode.Loop;

        DrawingShape ball = session.DrawCircle(0, 0, 11, filled: true, color: Color.FromRgb(120, 180, 255));
        session.Animations.BindAlongPath(ball, eight, new FloatTrack { { 0, 0.5f }, { 2, 1f }, { 2, 0f }, { 4, 0.5f } }.Loop());

        DrawingShape dart = session.DrawShape(new PolylineShape(new[] { new PointF(24, 0), new PointF(-14, -13), new PointF(-6, 0), new PointF(-14, 13) }, 1, Accent, isFilled: true));
        dart.Effects = DrawingEffects.DropShadow(new PointF(6, 8), 6, Color.FromRgba(0, 0, 0, 150));
        session.Animations.BindAlongPath(dart, eight, dartProgress, orientToPath: true);

        var car = new DrawingGroup();
        car.Add(new RectangleShape(-30, -15, 60, 30, 1, Color.FromRgb(230, 80, 90), isFilled: true, cornerRadius: 8));
        car.Add(new RectangleShape(4, -11, 16, 22, 1, Color.FromRgb(160, 210, 255), isFilled: true, cornerRadius: 4));
        car.Add(new CircleShape(26, -9, 3, 1, Color.FromRgb(255, 240, 160), isFilled: true));
        car.Add(new CircleShape(26, 9, 3, 1, Color.FromRgb(255, 240, 160), isFilled: true));
        session.DrawShape(car);
        var lap = new FloatTrack { { 0, 0f }, { 8, 1f, EasingKind.SineInOut } };
        session.Animations.BindAlongPath(car, track, lap, orientToPath: true);

        now = session.AddLayer("Now", Ink);
    }

    /// <inheritdoc />
    public override string Name => "motion-path";

    /// <inheritdoc />
    public override string Title => "Motion along a path";

    /// <inheritdoc />
    public override string Description => "DrawingAnimations.BindAlongPath (position + optional tangent rotation), PathGeometry.GetPointAt, PathPoint";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;

        // The tangent at the dart, read from the same path and progress the binding uses.
        now.Clear();
        PathPoint point = eight.GetPointAt(dartProgress.Evaluate(seconds));
        PointF p = point.Position;
        now.AddShape(new ArrowShape(p.X, p.Y, p.X + (point.Tangent.X * 70), p.Y + (point.Tangent.Y * 70), 2, Color.FromRgba(236, 236, 244, 170), 12));
        string readout = string.Format(CultureInfo.InvariantCulture, "distance {0,6:0.0} of {1:0.0}    heading {2,6:0.0} deg",
            point.Distance, eight.GetLength(), point.Angle);
        now.AddShape(new TextShape(readout, 60, 640, 16, session.Fonts, Ink));

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
