using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// The frame-sequence renderer: a short clip (a ball bouncing across, leaving a time-keyed trail) is rendered
/// ONCE, when the scene is built, by a <see cref="FrameSequenceRenderer" /> on four threads - each thread with its
/// own session from the clip's factory, the frames handed back in order - and again on one thread to show the
/// results are identical. The scene then plays the rendered frames back, shows them as a film strip, and shows the
/// same clip rendered without its background (over a checkerboard: the alpha carries it).
/// </summary>
internal sealed class SequenceScene : DemoScene
{
    private const int ClipWidth = 384;
    private const int ClipHeight = 216;
    private const int ThumbWidth = 128;
    private const int ThumbHeight = 72;
    private const double ClipSeconds = 4;
    private const double ClipFps = 12;

    private readonly AnimationTimeline clip = new AnimationTimeline(ClipSeconds, ClipFps);
    private readonly List<byte[]> frames = new List<byte[]>();
    private readonly List<byte[]> clearFrames = new List<byte[]>();
    private readonly List<byte[]> thumbs = new List<byte[]>();
    private readonly byte[] board;
    private readonly DrawingSession frame;
    private readonly DrawingLayer notes;
    private readonly bool identical;
    private readonly double parallelMilliseconds;
    private readonly double singleMilliseconds;

    /// <summary>Renders the clip (on four threads and on one) and builds the frame around it.</summary>
    public SequenceScene()
    {
        var renderer = new FrameSequenceRenderer(clip, () => CreateClip(), new Size(ClipWidth, ClipHeight)) { MaxDegreeOfParallelism = 4 };
        Stopwatch clock = Stopwatch.StartNew();
        renderer.Render(0, clip.FrameCount - 1, (index, seconds, rgba) => frames.Add(rgba.ToArray()));
        parallelMilliseconds = clock.Elapsed.TotalMilliseconds;

        var single = new FrameSequenceRenderer(clip, () => CreateClip(), new Size(ClipWidth, ClipHeight));
        bool same = true;
        clock.Restart();
        single.Render(0, clip.FrameCount - 1, (index, seconds, rgba) => same &= rgba.SequenceEqual(new ReadOnlySpan<byte>(frames[index])));
        singleMilliseconds = clock.Elapsed.TotalMilliseconds;
        identical = same;

        var clear = new FrameSequenceRenderer(clip, () => CreateClip(), new Size(ClipWidth, ClipHeight)) { MaxDegreeOfParallelism = 2, IncludeBackground = false };
        clear.Render(0, clip.FrameCount - 1, (index, seconds, rgba) => clearFrames.Add(rgba.ToArray()));

        for (int i = 0; i < 12; i++)
        {
            DrawingPlaneImage thumb = DrawingPlaneImage.FromRgba(frames[i * 4], ClipWidth, ClipHeight).Downsampled(3);
            thumbs.Add(thumb.ToRgba());
        }

        board = CreateCheckerboard(ClipWidth, ClipHeight).ToRgba();
        frame = CreateFrameSession();
        DrawingLayer labels = frame.AddLayer("Labels", Dim);
        Label(labels, frame, "playing the rendered frames", 80, 132, 13, Dim);
        Label(labels, frame, "the same clip with includeBackground: false", 520, 132, 13, Dim);
        Label(labels, frame, "every fourth frame", 80, 410, 13, Dim);
        notes = frame.AddLayer("Notes", Ink);
    }

    /// <inheritdoc />
    public override string Name => "sequence";

    /// <inheritdoc />
    public override string Title => "Frame-sequence renderer";

    /// <inheritdoc />
    public override string Description => "FrameSequenceRenderer: frames N..M of a timeline-driven scene, in order, on several threads (a session per thread) - identical to one";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        int index = clip.FrameAt(seconds % ClipSeconds);
        notes.Clear();
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture,
            "{0} frames of {1}x{2} at {3} fps: 4 threads {4:0} ms, 1 thread {5:0} ms - identical: {6}.   Showing frame {7}.",
            clip.FrameCount, ClipWidth, ClipHeight, ClipFps, parallelMilliseconds, singleMilliseconds, identical ? "yes" : "NO", index), 80, 680, 15, Ink);
        notes.AddShape(new RectangleShape(78 + ((index / 4 % 6) * (ThumbWidth + 8)), 418 + ((index / 4 / 6) * (ThumbHeight + 8)), ThumbWidth + 4, ThumbHeight + 4, 3, Accent));
        ExportFrame(frame, rgba);

        FrameCompositor.Copy(frames[index], ClipWidth, ClipHeight, rgba, 80, 145);
        FrameCompositor.Copy(board, ClipWidth, ClipHeight, rgba, 520, 145);
        FrameCompositor.Over(clearFrames[index], ClipWidth, ClipHeight, rgba, 520, 145);
        for (int i = 0; i < thumbs.Count; i++)
        {
            FrameCompositor.Copy(thumbs[i], ThumbWidth, ThumbHeight, rgba, 80 + ((i % 6) * (ThumbWidth + 8)), 420 + ((i / 6) * (ThumbHeight + 8)));
        }
    }

    /// <inheritdoc />
    public override void Dispose() => frame.Dispose();

    //The clip: one complete, time-pure scene per call (the renderer calls this once per thread)
    private static DrawingSession CreateClip()
    {
        DrawingSession session = CreateSession(ClipWidth, ClipHeight, Color.FromRgb(28, 40, 70));
        session.AddLayer("Ground", Color.FromRgb(60, 90, 120));
        session.DrawRectangle(0, 190, ClipWidth, 26, filled: true);

        var path = new PointTrack();
        path.Add(0, new PointF(30, 40));
        path.Add(ClipSeconds, new PointF(ClipWidth - 30, 40));
        var height = new FloatTrack { { 0, 0f }, { 1.3, 136f, EasingKind.BounceOut }, { 2.2, 90f, EasingKind.QuadOut }, { 3.2, 136f, EasingKind.BounceOut } };

        DrawingLayer trail = session.AddLayer("Trail", Color.FromRgb(255, 176, 72));
        for (int i = 0; i < 40; i++)
        {
            double t = i * ClipSeconds / 40;
            PointF at = path.Evaluate(t);
            trail.AddShape(new CircleShape(at.X, at.Y + height.Evaluate(t), 3, 1, null, isFilled: true) { StartTime = t, EndTime = t + 1.2 });
        }

        session.ActiveLayer = session.AddLayer("Ball", Color.FromRgb(255, 240, 200));
        session.DrawShape(new AnimatedShape(t =>
        {
            PointF at = path.Evaluate(t);
            return new CircleShape(at.X, at.Y + height.Evaluate(t), 14, 1, null, isFilled: true);
        }));
        return session;
    }
}
