using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// The playback controller and the debug overlay. A small clip with its own timeline is driven by a
/// <see cref="PlaybackController" /> that replays a script of key presses - play, pause, double speed, seek back,
/// play backwards, step frames, loop, and a half-second stall that the controller's cap turns into a pause instead
/// of a jump - and the clip is exported WITH a <see cref="DebugOverlay" /> (timecode, frame number, active cues),
/// which only an export that asks for it carries. (In the live window, D shows the same overlay over any scene.)
/// </summary>
internal sealed class PlaybackScene : DemoScene
{
    private const int ClipWidth = 640;
    private const int ClipHeight = 360;
    private const double Step = 1 / 30.0;

    private static readonly (double At, string Label, Action<PlaybackController> Act)[] Script =
    {
        (0.0, "Play()", p => p.Play()),
        (1.4, "Pause()", p => p.Pause()),
        (2.0, "Speed = 2; Play()", p => { p.Speed = 2; p.Play(); }),
        (3.0, "SeekBy(-2)", p => p.SeekBy(-2)),
        (3.8, "Speed = -1 (backwards)", p => p.Speed = -1),
        (4.8, "Step(3) - pauses", p => p.Step(3)),
        (5.4, "IsLooping = true; Speed = 1.5; Play()", p => { p.IsLooping = true; p.Speed = 1.5; p.Play(); }),
        (6.6, "a 0.5 s stall -> Advance(0.5): capped at 0.1 s", null),
    };

    private readonly AnimationTimeline clipTimeline = new AnimationTimeline(5, 30);
    private readonly DrawingSession frame;
    private readonly DrawingLayer state;
    private readonly DrawingSession clip;
    private readonly DebugOverlay overlay;
    private readonly byte[] clipPixels = new byte[ClipWidth * ClipHeight * 4];

    /// <summary>Builds the clip, its overlay and the frame around them.</summary>
    public PlaybackScene()
    {
        clipTimeline.AddCue("rise", 0, 2, EasingKind.CubicOut);
        clipTimeline.AddCue("spin", 1.5, 4, EasingKind.SineInOut);
        clipTimeline.AddMarker("flash", 2.5);

        clip = CreateSession(ClipWidth, ClipHeight, Color.FromRgb(24, 34, 60));
        clip.AddLayer("Shapes", Color.FromRgb(255, 176, 72));
        clip.DrawShape(new AnimatedShape(t =>
        {
            float y = (float)Easing.Lerp(300, 180, clipTimeline.Progress("rise", t));
            var box = new DrawingGroup(Color.FromRgb(255, 176, 72));
            box.Add(new RectangleShape(-50, -50, 100, 100, 1, null, isFilled: true, cornerRadius: 12));
            box.Add(new CircleShape(0, -28, 10, 1, Color.FromRgb(24, 34, 60), isFilled: true));
            box.Transform = new DrawingTransform(new PointF(320, y), (float)(clipTimeline.Progress("spin", t) * 360));
            return box;
        }));
        clip.DrawShape(new AnimatedShape(t => clipTimeline.FrameAt(t) == clipTimeline.FrameAt(2.5)
            ? new CircleShape(320, 180, 150, 6, Color.FromRgb(255, 240, 200))
            : null));
        overlay = new DebugOverlay(clip.Fonts, clipTimeline) { ShowCues = true, Corner = DebugOverlayCorner.BottomLeft };

        frame = CreateFrameSession();
        DrawingLayer script = frame.AddLayer("Script", Dim);
        Label(script, frame, "the clip, exported with a DebugOverlay", 48, 130, 13, Dim);
        Label(script, frame, "scripted input (scene time: call)", 730, 130, 13, Dim);
        for (int i = 0; i < Script.Length; i++)
        {
            Label(script, frame, string.Format(CultureInfo.InvariantCulture, "{0:0.0} s  {1}", Script[i].At, Script[i].Label), 730, 160 + (i * 28), 14, Ink);
        }

        state = frame.AddLayer("State", Accent);
    }

    /// <inheritdoc />
    public override string Name => "playback";

    /// <inheritdoc />
    public override string Title => "Playback controller and debug overlay";

    /// <inheritdoc />
    public override string Description => "PlaybackController (play, pause, seek, step, speed, loop, capped advance) and DebugOverlay (timecode, frame, cues) in an export";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        // Replay the script up to now, one 30 fps tick at a time: the controller is a pure state machine.
        var playback = new PlaybackController(clipTimeline);
        int ticks = (int)Math.Floor((seconds / Step) + 1e-9);
        int next = 0;
        int current = -1;
        for (int k = 0; k <= ticks; k++)
        {
            double now = k * Step;
            double elapsed = Step;
            while (next < Script.Length && Script[next].At <= now + 1e-9)
            {
                if (Script[next].Act == null) elapsed = 0.5;
                else Script[next].Act(playback);
                current = next++;
            }

            if (k > 0) playback.Advance(elapsed);
        }

        clip.Time = playback.Time;
        clip.ExportPixels(clipPixels, new Size(ClipWidth, ClipHeight), true, null, overlay);

        state.Clear();
        if (current >= 0) state.AddShape(new RectangleShape(722, 140 + (current * 28), 530, 26, 2, Accent, cornerRadius: 4));
        string[] lines =
        {
            string.Format(CultureInfo.InvariantCulture, "Time {0:0.000} s   Frame {1}", playback.Time, playback.Frame),
            string.Format(CultureInfo.InvariantCulture, "IsPlaying {0}   Speed {1}   IsLooping {2}   IsAtEnd {3}", playback.IsPlaying, playback.Speed, playback.IsLooping, playback.IsAtEnd),
        };
        for (int i = 0; i < lines.Length; i++) Label(state, frame, lines[i], 48, 560 + (i * 30), 17, Ink);
        Label(state, frame, "Live window: Space, Left/Right, Home go through the same controller; D toggles the overlay.", 48, 690, 14, Dim);
        ExportFrame(frame, rgba);
        FrameCompositor.Copy(clipPixels, ClipWidth, ClipHeight, rgba, 48, 145);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        clip.Dispose();
    }
}
