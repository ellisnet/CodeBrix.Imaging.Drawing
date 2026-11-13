using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Rolling along paths. Top: the letters of a word roll in along a swooping route as a train
/// (<see cref="DrawingAnimations.BindTextAlongPath" />): the last letter leads (<see cref="StaggerOrder.Reverse" />),
/// each rolls on its own baseline (<see cref="PathTrainOptions.RollOnBaseline" />), the gaps close as they arrive
/// (<see cref="PathTrainOptions.SpacingScale" />), and the progress track runs past 1 (to 1.025) and eases back
/// (<see cref="PathTrainOptions.ExtendBeyondEnds" />) - an overshoot that settles every letter upright, exactly where
/// the text block draws it (its ghost is shown). Bottom: a wheel rolls over hills
/// (<see cref="DrawingAnimations.BindAlongPath(DrawingElement, PathGeometry, KeyframeTrack{float}, PathTravelOptions)" />
/// with <see cref="PathTravelOptions.OrientToPath" /> and <see cref="PathTravelOptions.RollRadius" />): it tips with
/// the slope and turns with the distance, the two added; a read-out shows both parts.
/// </summary>
internal sealed class RollingTextScene : DemoScene
{
    private const string Word = "ROLLING";
    private const float WheelRadius = 30;
    private const float Upright = 1f;

    private readonly DrawingSession session;
    private readonly PathGeometry hills;
    private readonly FloatTrack wheelProgress = new FloatTrack { { 0.4, 0f }, { 7.4, 1f, EasingKind.SineInOut } };
    private readonly DrawingGroup wheel;
    private readonly DrawingLayer now;

    /// <summary>Builds the word's train, the hills and the wheel.</summary>
    public RollingTextScene()
    {
        session = CreateFrameSession();
        DrawingLayer stage = session.AddLayer("Stage", Dim);
        session.ActiveLayer = stage;

        // The word, laid out where it should end up; a ghost of it marks the resting places.
        TextLayoutResult layout = session.LayoutText(Word, 120);
        float left = (DemoFrame.Width - layout.Width) / 2f, top = 130;
        session.DrawShape(new TextBlockShape(layout, left, top, Color.FromRgba(132, 138, 164, 40)));
        float cx = left + (layout.Width / 2f), cy = top + (layout.Height / 2f);

        // The route: from off the left edge, a swoop down and up, ending heading right (so the letters arrive upright).
        PathGeometry route = new PathGeometryBuilder()
            .MoveTo(cx - 1150, cy + 40)
            .CubicTo(new PointF(cx - 900, cy + 190), new PointF(cx - 700, cy - 120), new PointF(cx - 460, cy + 20))
            .CubicTo(new PointF(cx - 280, cy + 120), new PointF(cx - 160, cy), new PointF(cx, cy))
            .Build();
        session.DrawShape(new PathShape(route, new PathStrokeStyle(1.5f, dashPattern: new[] { 7f, 7f }), Color.FromRgba(132, 138, 164, 110)));
        Label(stage, session, "the route (each letter travels a copy of it ending at its own place)", 40, 372, 13, Dim);
        Label(stage, session, "BindTextAlongPath: Reverse order, Stagger 0.07 s, Spacing 80 x SpacingScale 1 -> 0, RollOnBaseline, progress 0 -> 1.025 -> 1",
            40, 394, 13, Dim);

        var progress = new FloatTrack { { 0.3, 0f }, { 3.6, 1.025f, EasingKind.SineOut }, { 4.6, 1f, EasingKind.SineInOut } };
        var block = new TextBlockShape(layout, left, top, Accent);
        DrawingLayer letters = session.AddLayer("Letters", Accent);
        PathTrain train = session.Animations.BindTextAlongPath(block, TextUnit.Character, route, progress, new PathTrainOptions
        {
            Order = StaggerOrder.Reverse,
            Stagger = 0.07,
            Spacing = 80,
            SpacingScale = new FloatTrack { { 0, 1f }, { 2.6, 1f }, { 4.8, 0f, EasingKind.SineInOut } },
            RollOnBaseline = true,
            ExtendBeyondEnds = true,
        });
        train.AddTo(letters);

        // The hills: the path of the wheel's hub; the ground lies one radius below it, square to the path.
        hills = new PathGeometryBuilder()
            .MoveTo(40, 560)
            .CubicTo(new PointF(190, 560), new PointF(250, 470), new PointF(380, 480))
            .CubicTo(new PointF(500, 490), new PointF(540, 610), new PointF(680, 610))
            .CubicTo(new PointF(820, 610), new PointF(860, 500), new PointF(980, 505))
            .CubicTo(new PointF(1090, 510), new PointF(1140, 580), new PointF(1240, 580))
            .Build();
        var ground = new List<PointF>();
        for (int i = 0; i <= 300; i++)
        {
            PathPoint p = hills.GetPointAt(i / 300f);
            ground.Add(new PointF(p.Position.X - (p.Tangent.Y * WheelRadius), p.Position.Y + (p.Tangent.X * WheelRadius)));
        }
        ground.Add(new PointF(ground[^1].X, DemoFrame.Height));
        ground.Add(new PointF(ground[0].X, DemoFrame.Height));
        session.DrawShape(new PolylineShape(ground, 1, Color.FromRgb(44, 52, 46), isClosed: true, isFilled: true));
        session.DrawShape(new PolylineShape(ground.GetRange(0, 301), 3, Color.FromRgb(96, 140, 96)));
        session.DrawShape(new PathShape(hills, new PathStrokeStyle(1f, dashPattern: new[] { 4f, 6f }), Color.FromRgba(132, 138, 164, 100)));

        // The wheel, drawn round its hub at (0, 0): a rim, spokes and one red spoke to follow.
        DrawingLayer riders = session.AddLayer("Riders", Ink);
        wheel = new DrawingGroup();
        wheel.Add(new CircleShape(0, 0, WheelRadius - 2, 4, Ink));
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3;
            wheel.Add(new LineShape(0, 0, (float)(WheelRadius * Math.Cos(a)), (float)(WheelRadius * Math.Sin(a)), 2, Dim));
        }
        wheel.Add(new LineShape(0, 0, 0, -(WheelRadius - 3), 4, Color.FromRgb(240, 84, 110)));
        wheel.Add(new CircleShape(0, 0, 5, 1, Accent, isFilled: true));
        riders.AddShape(wheel);
        session.Animations.BindAlongPath(wheel, hills, wheelProgress, new PathTravelOptions
        {
            OrientToPath = true, RollRadius = WheelRadius, UprightAtProgress = Upright,
        });

        now = session.AddLayer("Now", Ink);
    }

    /// <inheritdoc />
    public override string Name => "rolling-text";

    /// <inheritdoc />
    public override string Title => "Rolling along a path: letters and a wheel";

    /// <inheritdoc />
    public override string Description => "BindTextAlongPath (a train of letters: stagger, spacing scale, roll on baseline, overshoot), PathTravelOptions (facing + roll add)";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;

        // The wheel's rotation is its heading plus its roll (distance from where it is upright / radius).
        now.Clear();
        PathPoint point = hills.GetPointAt(wheelProgress.Evaluate(seconds));
        double roll = (point.Distance - (Upright * hills.GetLength())) / WheelRadius * 180.0 / Math.PI;
        string readout = string.Format(CultureInfo.InvariantCulture,
            "wheel rotation {0,7:0.0} deg  =  heading {1,6:0.0} deg  +  roll {2,7:0.0} deg  (distance / radius)",
            wheel.Transform.Rotation, point.Angle, roll);
        now.AddShape(new TextShape(readout, 40, 700, 15, session.Fonts, Ink));

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
