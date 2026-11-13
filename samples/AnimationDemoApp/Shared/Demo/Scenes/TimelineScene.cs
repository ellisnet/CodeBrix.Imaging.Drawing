using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// A cue sheet: an <see cref="AnimationTimeline" /> of 8 seconds at 30 fps with four cues (each with its own easing)
/// and four markers, drawn as bars on a ruler with a moving playhead. Below, each cue's linear and eased progress at
/// the current time as two bars, the cue-driven motion they produce, the markers lighting up in their frame, and the
/// timecode and frame number from the timeline.
/// </summary>
internal sealed class TimelineScene : DemoScene
{
    private const float RulerLeft = 120;
    private const float RulerRight = 1220;
    private const float RulerTop = 130;
    private const float RowHeight = 30;
    private const float BarsTop = 330;

    private static readonly Color[] CueColors =
    {
        Color.FromRgb(120, 170, 255), Color.FromRgb(255, 176, 72), Color.FromRgb(140, 230, 160), Color.FromRgb(240, 120, 190),
    };

    private readonly AnimationTimeline timeline = new AnimationTimeline(8, 30);
    private readonly List<TimelineCue> cues = new List<TimelineCue>();
    private readonly List<TimelineCue> markers = new List<TimelineCue>();
    private readonly DrawingSession session;
    private readonly DrawingLayer now;

    /// <summary>Builds the cue sheet (drawn once) and the layer that shows the current time.</summary>
    public TimelineScene()
    {
        cues.Add(timeline.AddCue("intro", 0, 1.5, EasingKind.SmoothStep));
        cues.Add(timeline.AddCue("travel", 1, 6, EasingKind.CubicInOut));
        cues.Add(timeline.AddCue("spin", 2, 5, EasingKind.BackOut));
        cues.Add(timeline.AddCue("outro", 6.5, 8, EasingKind.SineIn));
        for (int i = 1; i <= 4; i++) markers.Add(timeline.AddMarker("beat " + i, 1.5 + i));

        session = CreateFrameSession();
        DrawingLayer sheet = session.AddLayer("Sheet", Dim);
        session.ActiveLayer = sheet;
        for (int second = 0; second <= 8; second++)
        {
            float x = X(second);
            sheet.AddShape(new LineShape(x, RulerTop - 8, x, RulerTop + (cues.Count * RowHeight) + 30, 1, Color.FromRgba(132, 138, 164, 60)));
            Label(sheet, session, second + " s", x, RulerTop - 14, 11, Dim, TextAlign.Center);
        }

        for (int i = 0; i < cues.Count; i++)
        {
            TimelineCue cue = cues[i];
            float y = RulerTop + (i * RowHeight);
            sheet.AddShape(new RectangleShape(X(cue.StartTime), y, X(cue.EndTime) - X(cue.StartTime), RowHeight - 8, 1, CueColors[i], isFilled: true, cornerRadius: 4));
            Label(sheet, session, cue.Name + "  (" + cue.Easing + ")", X(cue.StartTime) + 6, y + 16, 12, Backdrop);
            Label(sheet, session, cue.Name, 32, BarsTop + (i * 52) + 4, 15, Ink);
            sheet.AddShape(new RectangleShape(RulerLeft, BarsTop + (i * 52) - 10, RulerRight - RulerLeft, 12, 1, Color.FromRgba(132, 138, 164, 50), isFilled: true));
            sheet.AddShape(new RectangleShape(RulerLeft, BarsTop + (i * 52) + 6, RulerRight - RulerLeft, 12, 1, Color.FromRgba(132, 138, 164, 50), isFilled: true));
        }

        float markerY = RulerTop + (cues.Count * RowHeight) + 14;
        foreach (TimelineCue marker in markers)
        {
            sheet.AddShape(new PolylineShape(new[] { new PointF(X(marker.StartTime) - 6, markerY - 8), new PointF(X(marker.StartTime) + 6, markerY - 8), new PointF(X(marker.StartTime), markerY) }, 1, Dim, isClosed: true, isFilled: true));
        }

        Label(sheet, session, "linear progress (upper bar) and eased progress (lower bar) of each cue at the playhead", RulerLeft, BarsTop - 26, 12, Dim);
        now = session.AddLayer("Now", Accent);
    }

    /// <inheritdoc />
    public override string Name => "timeline";

    /// <inheritdoc />
    public override string Title => "Timeline and cues";

    /// <inheritdoc />
    public override string Description => "AnimationTimeline: frames and timecode; TimelineCue: named stretches with eased progress; markers active in their frame";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        now.Clear();
        float playhead = X(seconds);
        now.AddShape(new LineShape(playhead, RulerTop - 10, playhead, RulerTop + (cues.Count * RowHeight) + 20, 2, Accent));

        for (int i = 0; i < cues.Count; i++)
        {
            float width = RulerRight - RulerLeft;
            float y = BarsTop + (i * 52);
            float linear = (float)cues[i].LinearProgress(seconds);
            float eased = (float)cues[i].Progress(seconds);
            if (linear > 0) now.AddShape(new RectangleShape(RulerLeft, y - 10, width * linear, 12, 1, CueColors[i], isFilled: true));
            // An overshooting curve (BackOut) passes 1; the bar stops at the end and the number shows the overshoot.
            if (eased > 0) now.AddShape(new RectangleShape(RulerLeft, y + 6, Math.Max(1, width * Math.Min(eased, 1f)), 12, 1, CueColors[i], isFilled: true));
            Label(now, session, eased.ToString("0.000", CultureInfo.InvariantCulture), 112, y + 4, 12, Ink, TextAlign.Right);
        }

        // What the cues drive: a puck that travels, spins (overshooting a little) and fades in and out.
        double travel = timeline.Progress("travel", seconds);
        double spin = timeline.Progress("spin", seconds);
        double fade = timeline.Progress("intro", seconds) * (1 - timeline.Progress("outro", seconds));
        var puck = new DrawingGroup(Color.FromRgb(255, 220, 140));
        puck.Add(new RectangleShape(-26, -26, 52, 52, 1, null, isFilled: true, cornerRadius: 8));
        puck.Add(new CircleShape(0, -14, 6, 1, Backdrop, isFilled: true));
        puck.Transform = new DrawingTransform(new PointF((float)Easing.Lerp(RulerLeft + 40, RulerRight - 40, travel), 600), (float)(spin * 360));
        puck.Opacity = (float)Math.Clamp(fade, 0, 1);
        now.AddShape(puck);

        float markerY = RulerTop + (cues.Count * RowHeight) + 14;
        foreach (TimelineCue cue in timeline.GetActiveCues(seconds))
        {
            if (cue.IsMarker) now.AddShape(new CircleShape(X(cue.StartTime), markerY - 4, 12, 3, Accent));
        }

        string time = string.Format(CultureInfo.InvariantCulture, "{0}   frame {1} / {2}   active: {3}",
            timeline.FormatTimecode(seconds), timeline.FrameAt(seconds), timeline.FrameCount, ActiveNames(seconds));
        Label(now, session, time, 32, 690, 16, Ink);
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private static float X(double seconds) => (float)Easing.Lerp(RulerLeft, RulerRight, seconds / 8.0);

    private string ActiveNames(double seconds)
    {
        List<string> names = new List<string>();
        foreach (TimelineCue cue in timeline.GetActiveCues(seconds)) names.Add(cue.Name);
        return names.Count == 0 ? "none" : string.Join(", ", names);
    }
}
