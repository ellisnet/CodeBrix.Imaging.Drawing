using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Rendering ahead of the play head. A small scene (its own 8-second timeline at 30 frames a second) is rendered by
/// a <see cref="FrameCache" /> on two background workers: the play head follows this scene's time, the cache holds
/// the next <see cref="Capacity" /> frames that <see cref="FrameCache.ShouldRender" /> accepts (here the "heavy"
/// first 1.2 s of every 2 s) and keeps <see cref="KeepBehind" /> frames behind the head. The strip shows the frames
/// around the play head: cached ahead, kept behind, not wanted. Above it, the cached frame at the play head
/// (<see cref="FrameCache.TryGetFrame" />) beside the same frame drawn live, with a byte-by-byte comparison.
/// So that every frame of this demo is a function of its time alone, the scene waits for the workers to catch up
/// (first at the head minus KeepBehind, then at the head) before it draws the strip; a live view would not wait - it
/// would draw live whenever TryGetFrame says no.
/// </summary>
internal sealed class FrameCacheScene : DemoScene
{
    private const int SmallW = 240, SmallH = 135;
    private const int Capacity = 20;
    private const int KeepBehind = 5;
    private const int StripFrames = 50, StripBefore = 10;
    private const float StripLeft = 40, StripTop = 520, CellWidth = 24, CellHeight = 40;
    private static readonly Color Ahead = Color.FromRgb(255, 176, 72);
    private static readonly Color Behind = Color.FromRgb(120, 160, 255);
    private static readonly Color Missing = Color.FromRgb(92, 96, 124);
    private static readonly Color Unwanted = Color.FromRgb(36, 38, 52);

    private readonly DrawingSession frame;
    private readonly DrawingSession live;
    private readonly AnimationTimeline timeline = new AnimationTimeline(8, 30);
    private readonly FrameCache cache;
    private readonly DrawingLayer now;
    private readonly byte[] cached = new byte[SmallW * SmallH * 4];
    private readonly byte[] drawn = new byte[SmallW * SmallH * 4];

    /// <summary>Builds the frame, the live copy of the small scene and the cache.</summary>
    public FrameCacheScene()
    {
        frame = CreateFrameSession();
        DrawingLayer notes = frame.AddLayer("Notes", Dim);
        Label(notes, frame, "FrameCache.TryGetFrame(head)", 40, 128, 14, Ink);
        Label(notes, frame, "the same frame drawn live", 560, 128, 14, Ink);
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture,
            "new FrameCache(timeline, factory, {0}x{1}, capacity {2}, workers 2) {{ KeepBehind = {3}, ShouldRender = f => f % 60 < 36 }}",
            SmallW, SmallH, Capacity, KeepBehind), 40, 470, 13, Dim);
        Legend(notes, 40, "cached ahead", Ahead);
        Legend(notes, 220, "kept behind", Behind);
        Legend(notes, 400, "wanted, not held", Missing);
        Legend(notes, 600, "not wanted (ShouldRender false)", Unwanted);
        Label(notes, frame, "This demo waits for the workers so each of its frames depends on the time alone; a live view would draw live when TryGetFrame says no.",
            40, 700, 12, Dim);
        now = frame.AddLayer("Now", Ink);

        live = CreateSmallScene();
        cache = new FrameCache(timeline, CreateSmallScene, new Size(SmallW, SmallH), Capacity, 2)
        {
            KeepBehind = KeepBehind,
            ShouldRender = Wanted,
        };
    }

    /// <inheritdoc />
    public override string Name => "frame-cache";

    /// <inheritdoc />
    public override string Title => "Frame cache: rendering ahead of the play head";

    /// <inheritdoc />
    public override string Description => "FrameCache: background workers, one session each, a window ahead of the play head, ShouldRender, KeepBehind, TryGetFrame";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        int head = Math.Clamp(timeline.FrameAt(seconds), 0, timeline.FrameCount - 1);
        cache.SetPlayHead(Math.Max(0, head - KeepBehind));
        bool ready = WaitForWindow(Math.Max(0, head - KeepBehind));
        cache.SetPlayHead(head);
        ready &= WaitForWindow(head);

        bool hit = cache.TryGetFrame(head, cached);
        live.Time = timeline.TimeOfFrame(head);
        live.ExportPixels(drawn, new Size(SmallW, SmallH));
        int differing = 0;
        for (int i = 0; i < drawn.Length; i++) if (hit && cached[i] != drawn[i]) differing++;

        now.Clear();
        frame.Time = seconds;
        for (int k = 0; k < StripFrames; k++)
        {
            int f = head - StripBefore + k;
            if (f < 0 || f >= timeline.FrameCount) continue;
            bool has = cache.Contains(f);
            Color color = !Wanted(f) ? Unwanted : has ? (f < head ? Behind : Ahead) : Missing;
            float x = StripLeft + (k * CellWidth);
            now.AddShape(new RectangleShape(x + 1, StripTop, CellWidth - 2, CellHeight, 1, color, isFilled: true, cornerRadius: 3));
            if (f % 30 == 0)
            {
                now.AddShape(new TextShape((f / 30).ToString(CultureInfo.InvariantCulture) + " s", x, StripTop + CellHeight + 18, 12, frame.Fonts, Dim));
            }
        }
        float headX = StripLeft + (StripBefore * CellWidth) + (CellWidth / 2f);
        now.AddShape(new PolylineShape(new[] { new PointF(headX - 8, StripTop - 14), new PointF(headX + 8, StripTop - 14), new PointF(headX, StripTop - 2) },
            1, Ink, isClosed: true, isFilled: true));
        now.AddShape(new TextShape(string.Format(CultureInfo.InvariantCulture,
            "play head: frame {0}    frames held: {1}    {2}", head, cache.Count,
            !Wanted(head) ? "the head's frame is not wanted: draw it live"
                : !hit ? "not ready"
                : differing == 0 ? "cached frame == live frame (0 bytes differ)" : differing.ToString(CultureInfo.InvariantCulture) + " bytes differ"),
            40, 438, 16, frame.Fonts, Accent));
        if (!ready || cache.Failure != null)
        {
            now.AddShape(new TextShape(cache.Failure != null ? "worker failed: " + cache.Failure.Message : "the workers did not catch up in time",
                1240, 438, 14, frame.Fonts, Color.FromRgb(240, 84, 110), TextAlign.Right));
        }

        ExportFrame(frame, rgba);
        if (hit) Enlarge(cached, rgba, 40, 150);
        else DrawEmpty(rgba, 40, 150);
        Enlarge(drawn, rgba, 560, 150);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        cache.Dispose();
        live.Dispose();
        frame.Dispose();
    }

    //The "heavy" stretches: the first 1.2 s of every 2 s
    private static bool Wanted(int f) => f % 60 < 36;

    //Waits until every wanted frame of the window from a head is ready (or a worker fails, or 10 s pass)
    private bool WaitForWindow(int head)
    {
        int wanted = 0;
        var clock = Stopwatch.StartNew();
        for (int f = head; f < timeline.FrameCount && wanted < Capacity; f++)
        {
            if (!Wanted(f)) continue;
            wanted++;
            while (!cache.Contains(f))
            {
                if (cache.Failure != null || clock.Elapsed.TotalSeconds > 10) return false;
                Thread.Sleep(1);
            }
        }
        return true;
    }

    //The small scene: a ball bouncing over a turning star, every frame a function of its time
    private static DrawingSession CreateSmallScene()
    {
        DrawingSession small = CreateSession(SmallW, SmallH, Color.FromRgb(30, 34, 52));
        DrawingLayer layer = small.AddLayer("Small", Color.White);
        layer.AddShape(new RectangleShape(0, 112, SmallW, 23, 1, Color.FromRgb(46, 52, 74), isFilled: true));
        var star = new PathShape(PathGeometry.Star(new PointF(170, 62), 38, 16, 5), Color.FromRgb(255, 176, 72))
        {
            Transform = new DrawingTransform(new PointF(0, 0), anchor: new PointF(170, 62)),
        };
        layer.AddShape(star);
        small.Animations.Bind(star, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 8, 360f } });
        var ball = new CircleShape(0, 0, 13, 1, Color.FromRgb(120, 180, 255), isFilled: true);
        layer.AddShape(ball);
        small.Animations.Bind(ball, AnimatedProperty.Position,
            t => new PointF(30 + (float)(180 * ((t / 4) % 1)), 99 - (float)(80 * Math.Abs(Math.Sin(Math.PI * t / 0.8)))));
        return small;
    }

    private void Legend(DrawingLayer layer, float x, string text, Color color)
    {
        layer.AddShape(new RectangleShape(x, StripTop + 76, 16, 16, 1, color, isFilled: true, cornerRadius: 3));
        Label(layer, frame, text, x + 24, StripTop + 89, 13, Dim);
    }

    //Copies a small frame into the frame at twice its size
    private static void Enlarge(byte[] small, Span<byte> rgba, int left, int top)
    {
        for (int y = 0; y < SmallH * 2; y++)
        {
            for (int x = 0; x < SmallW * 2; x++)
            {
                int from = (((y / 2) * SmallW) + (x / 2)) * 4;
                int to = (((top + y) * DemoFrame.Width) + left + x) * 4;
                rgba[to] = small[from];
                rgba[to + 1] = small[from + 1];
                rgba[to + 2] = small[from + 2];
                rgba[to + 3] = 255;
            }
        }
    }

    //A hatched panel where no cached frame is
    private static void DrawEmpty(Span<byte> rgba, int left, int top)
    {
        for (int y = 0; y < SmallH * 2; y++)
        {
            for (int x = 0; x < SmallW * 2; x++)
            {
                int to = (((top + y) * DemoFrame.Width) + left + x) * 4;
                byte v = ((x + y) / 12) % 2 == 0 ? (byte)40 : (byte)30;
                rgba[to] = v;
                rgba[to + 1] = v;
                rgba[to + 2] = (byte)(v + 10);
                rgba[to + 3] = 255;
            }
        }
    }
}
