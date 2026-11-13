using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Keyframe tracks bound to properties: the scene is built ONCE and every frame is just <c>session.Time = t</c>.
/// Five lanes of the same four position keys with different segment easing, a hold (step) lane, a looping lane and
/// a ping-pong lane; a badge whose rotation, scale and opacity come from float tracks; and a group whose content
/// color comes from a color track.
/// </summary>
internal sealed class KeyframeScene : DemoScene
{
    private const float LaneLeft = 220;
    private const float LaneRight = 860;
    private const float LaneTop = 130;
    private const float LaneGap = 62;

    private readonly DrawingSession session;

    /// <summary>Builds the lanes, the tracks and their bindings.</summary>
    public KeyframeScene()
    {
        session = CreateFrameSession();
        DrawingLayer guides = session.AddLayer("Guides", Dim);
        DrawingLayer moving = session.AddLayer("Moving", Accent);
        session.ActiveLayer = moving;

        List<(string Label, PointTrack Track)> lanes = new List<(string, PointTrack)>
        {
            ("linear", Lane(0, EasingKind.Linear)),
            ("CubicInOut", Lane(1, EasingKind.CubicInOut)),
            ("BackOut", Lane(2, EasingKind.BackOut)),
            ("BounceOut", Lane(3, EasingKind.BounceOut)),
            ("ElasticOut", Lane(4, EasingKind.ElasticOut)),
        };

        PointTrack hold = new PointTrack();
        hold.Add(0, new PointF(LaneLeft, LaneY(5)));
        for (int i = 1; i <= 4; i++) hold.AddHold(i * 1.6, new PointF(Easing.Lerp(LaneLeft, LaneRight, i / 4.0), LaneY(5)));
        lanes.Add(("hold keys", hold));

        PointTrack loop = new PointTrack { { 0, new PointF(LaneLeft, LaneY(6)) }, { 1.5, new PointF(LaneRight, LaneY(6)), EasingKind.QuadInOut } };
        loop.LoopMode = TrackLoopMode.Loop;
        lanes.Add(("loop", loop));

        PointTrack pingPong = new PointTrack { { 0, new PointF(LaneLeft, LaneY(7)) }, { 1.5, new PointF(LaneRight, LaneY(7)), EasingKind.SineInOut } };
        pingPong.LoopMode = TrackLoopMode.PingPong;
        lanes.Add(("ping-pong", pingPong));

        for (int i = 0; i < lanes.Count; i++)
        {
            float y = LaneY(i);
            guides.AddShape(new LineShape(LaneLeft, y, LaneRight, y, 2, Color.FromRgba(132, 138, 164, 70)));
            guides.AddShape(new TextShape(lanes[i].Label, LaneLeft - 24, y + 5, 15, session.Fonts, Ink, TextAlign.Right));
            DrawingShape ball = session.DrawCircle(0, 0, 11, filled: true, color: i < 5 ? Accent : Color.FromRgb(120, 200, 255));
            session.Animations.Bind(ball, AnimatedProperty.Position, lanes[i].Track);
        }

        // A badge: rotation, scale and opacity from float tracks (rotation and scale about the badge's centre).
        var badge = new DrawingGroup(Color.FromRgb(255, 210, 120));
        badge.Add(new PolylineShape(Star(1040, 300, 70, 32), 1, null, isClosed: true, isFilled: true));
        badge.Add(new CircleShape(1040, 300, 18, 1, Backdrop, isFilled: true));
        session.DrawShape(badge);
        session.Animations.Bind(badge, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(1040, 300) } });
        session.Animations.Bind(badge, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 4, 360f, EasingKind.CubicInOut }, { 8, 0f, EasingKind.CubicInOut } });
        session.Animations.Bind(badge, AnimatedProperty.Scale, new FloatTrack { { 0, 0.4f }, { 2, 1.2f, EasingKind.ElasticOut }, { 6, 1.2f }, { 8, 0.4f, EasingKind.BackIn } });
        session.Animations.Bind(badge, AnimatedProperty.Opacity, new FloatTrack { { 0, 0f }, { 0.8, 1f }, { 7.2, 1f }, { 8, 0f } });
        guides.AddShape(new TextShape("rotation, scale, opacity tracks", 1040, 420, 13, session.Fonts, Dim, TextAlign.Center));

        // A group whose children take the color track's color.
        var swatches = new DrawingGroup();
        for (int i = 0; i < 4; i++) swatches.Add(new RectangleShape(960 + (i * 42), 520, 34, 80, 1, null, isFilled: true, cornerRadius: 5));
        session.DrawShape(swatches);
        session.Animations.Bind(swatches, AnimatedProperty.Color, new ColorTrack
        {
            { 0, Color.FromRgb(255, 90, 120) },
            { 2.5, Color.FromRgb(255, 200, 80), EasingKind.SineInOut },
            { 5, Color.FromRgb(90, 200, 255), EasingKind.SineInOut },
            { 8, Color.FromRgb(255, 90, 120), EasingKind.SineInOut },
        });
        guides.AddShape(new TextShape("color track -> group content color", 1040, 630, 13, session.Fonts, Dim, TextAlign.Center));
        guides.AddShape(new TextShape("Every frame is only session.Time = t: the bindings set positions, transforms, opacity and color.", 32, 690, 15, session.Fonts, Ink));
    }

    /// <inheritdoc />
    public override string Name => "keyframes";

    /// <inheritdoc />
    public override string Title => "Keyframe tracks";

    /// <inheritdoc />
    public override string Description => "PointTrack, FloatTrack, ColorTrack with per-segment easing, hold keys, Loop and PingPong, bound through session.Animations";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private static float LaneY(int lane) => LaneTop + (lane * LaneGap);

    //Four keys along the lane, each segment arriving with the lane's easing
    private static PointTrack Lane(int lane, EasingKind easing)
    {
        PointTrack track = new PointTrack();
        track.Add(0, new PointF(LaneLeft, LaneY(lane)));
        track.Add(2.5, new PointF(LaneRight, LaneY(lane)), easing);
        track.Add(5, new PointF((LaneLeft + LaneRight) / 2, LaneY(lane)), easing);
        track.Add(7.5, new PointF(LaneLeft, LaneY(lane)), easing);
        return track;
    }

    private static PointF[] Star(float cx, float cy, float outer, float inner)
    {
        PointF[] points = new PointF[10];
        for (int i = 0; i < points.Length; i++)
        {
            double angle = (Math.PI * i / 5) - (Math.PI / 2);
            float r = i % 2 == 0 ? outer : inner;
            points[i] = new PointF(cx + (r * (float)Math.Cos(angle)), cy + (r * (float)Math.Sin(angle)));
        }

        return points;
    }
}
