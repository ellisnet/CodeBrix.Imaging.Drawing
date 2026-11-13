using System;
using System.Diagnostics;
using System.Globalization;
using AutumnAviana.Animation;
using AutumnAviana.Scene;
using CodeBrix.Imaging.Drawing.NoSkia;

namespace AutumnAviana.Rendering;

/// <summary>
/// Measures the live path: the session rendering consecutive frames into a window-sized canvas, with its layer
/// caches (exactly what the live window does each frame).
/// </summary>
internal static class LiveFrameTimer
{
    private const int Width = Hosting.LiveWindow.Width;
    private const int Height = Hosting.LiveWindow.Height;

    /// <summary>Renders every frame from one time to another and prints the mean and worst times.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="start">The first time.</param>
    /// <param name="end">The last time.</param>
    public static void Measure(AutumnScene scene, double start, double end)
    {
        DrawingImageInfo info = new DrawingImageInfo(Width, Height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);
        using DrawingBitmap bitmap = new DrawingBitmap(info);
        using DrawingCanvas canvas = new DrawingCanvas(bitmap);
        scene.Show(start - (1.0 / Timeline.Fps));
        scene.Session.Render(canvas, info, true);
        double total = 0;
        double worst = 0;
        int frames = 0;
        Stopwatch clock = new Stopwatch();
        for (int f = Timeline.Clock.FrameAt(start); f <= Timeline.Clock.FrameAt(end); f++)
        {
            clock.Restart();
            scene.Show(Timeline.Clock.TimeOfFrame(f));
            scene.Session.Render(canvas, info, true);
            double ms = clock.Elapsed.TotalMilliseconds;
            total += ms;
            worst = Math.Max(worst, ms);
            frames++;
        }

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Live {0}x{1}, {2:0.0}-{3:0.0} s: mean {4:0.0} ms, worst {5:0.0} ms per frame ({6} frames)",
            Width, Height, start, end, total / frames, worst, frames));
    }

    /// <summary>
    /// Plays the animation from the start to the end of the raccoons in real time without a window, exactly as the
    /// live window does: the play head advances one frame every 1/30 s; each frame is taken
    /// from the frame cache when it is ready there, and otherwise drawn live by the scene's own session (on this
    /// thread, competing with the cache's workers). Prints how many pan frames came from the cache, and how long the
    /// frames took to get ready, before, during and after the pan.
    /// </summary>
    /// <param name="scene">The scene the live frames are drawn from.</param>
    /// <param name="workers">The cache's worker count.</param>
    public static void MeasurePlayback(AutumnScene scene, int workers)
    {
        System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
        int startFrame = 0;
        int endFrame = Timeline.Clock.FrameAt(Timeline.Hold.StartTime + 1);
        DrawingImageInfo info = new DrawingImageInfo(Width, Height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);
        using DrawingBitmap bitmap = new DrawingBitmap(info);
        using DrawingCanvas canvas = new DrawingCanvas(bitmap);
        using Hosting.FrameSource source = new Hosting.FrameSource(scene, canvas, info, workers);
        byte[] frame = new byte[Width * Height * 4];
        int[] late = new int[3];
        int[] count = new int[3];
        double[] sum = new double[3];
        int cachedMisses = 0;
        int cachedDue = 0;
        double[] worstLive = new double[3];
        System.Collections.Generic.List<string> lateTimes = new System.Collections.Generic.List<string>();
        Stopwatch clock = Stopwatch.StartNew();
        Stopwatch one = new Stopwatch();
        for (int f = startFrame; f <= endFrame; f++)
        {
            double due = (f - startFrame) / (double)Timeline.Fps;
            while (clock.Elapsed.TotalSeconds < due) System.Threading.Thread.Sleep(1);
            double t = Timeline.Clock.TimeOfFrame(f);
            int part = t < Timeline.Pan.StartTime ? 0 : t <= Timeline.Pan.EndTime ? 1 : 2;
            bool wanted = Hosting.FrameSource.IsCached(f);
            if (wanted) cachedDue++;
            one.Restart();
            bool cached = source.Draw(f, t, frame);
            if (wanted && !cached) cachedMisses++;
            double ms = one.Elapsed.TotalMilliseconds;
            worstLive[part] = Math.Max(worstLive[part], ms);
            sum[part] += ms;
            count[part]++;
            if (ms > 1000.0 / Timeline.Fps)
            {
                late[part]++;
                if (lateTimes.Count < 16) lateTimes.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.00} s ({1:0} ms)", t, ms));
            }
        }

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Playback {0:0.0}-{1:0.0} s at {2} fps ({3} cache workers): {4} of {5} pan frames came from the cache.",
            Timeline.Clock.TimeOfFrame(startFrame), Timeline.Clock.TimeOfFrame(endFrame), Timeline.Fps, workers, cachedDue - cachedMisses, cachedDue));
        string[] names = { "before the pan", "the pan", "after the pan" };
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0}: mean {1:0.0} ms, worst {2:0.0} ms per frame; {3} of {4} frames over the {5:0.0} ms budget",
                names[i], sum[i] / Math.Max(1, count[i]), worstLive[i], late[i], count[i], 1000.0 / Timeline.Fps));
        }

        if (lateTimes.Count > 0) Console.WriteLine("  first late frames at: " + string.Join(", ", lateTimes));
    }
}
