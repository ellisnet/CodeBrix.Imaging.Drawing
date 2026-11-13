using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AutumnAviana.Animation;
using AutumnAviana.Rendering;
using AutumnAviana.Scene;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;

namespace AutumnAviana;

/// <summary>
/// Autumn Aviana: fall leaves drift down onto a white screen and heap up into piles that turn out to spell AVIANA;
/// the camera pans down onto three raccoons already rolling the letters of "@ Øasys" in from the left; two walk off
/// to the right and the last runs off in a bounding lope. A 30 fps, 1920x1080 animation (its length is <see cref="Timeline.TotalSeconds" />) drawn entirely with the
/// completely managed CodeBrix.Imaging.Drawing.NoSkia package (no SkiaSharp, no native drawing library). The live
/// window plays it at 1280x720; stills are full size.
/// <code>
/// dotnet run                                   # live, in a window (SDL3), real time
/// dotnet run -- --stills 5,20,40,50,58         # full-size PNG frames at those times -> Output/stills
/// dotnet run -- --stills 50 --crop 0,500,960,540  # just part of each frame (x,y,width,height)
/// dotnet run -- --stills 50 --transparent      # on transparency (as the alpha videos), not on white
/// dotnet run -- --sheet 2,8,14,20,26,32        # one contact sheet of those times -> Output/sheets
///                                              #   (--crop too; --big for larger thumbnails;
///                                              #   --follow 3: the crop moves with raccoon 3)
/// dotnet run -- --timing                       # how long frames take to draw (and a real-time playback test)
/// dotnet run -- --verify                       # check that every frame is a pure function of its time
/// dotnet run -- --render ~/Temp                # the three videos (ffmpeg on the PATH), raw frames piped in:
///                                              #   white H.264 MP4, ProRes 4444 .mov and VP9 .webm with alpha
///                                              #   (no folder: Output/videos)
/// dotnet run -- --lineup                       # the three raccoons side by side -> Output/stills
/// dotnet run -- --sizes                        # paw reach check; the raccoons' tail and body extents
/// </code>
/// In the window: Space pause/play (at the end: play again), Left/Right seek 1 s (Shift: 5 s), Home restart,
/// D show/hide the debug overlay (timecode, frame, active cues), Esc quit. All timing lives in
/// <see cref="Timeline" />, as cues of the library timeline.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Dictionary<string, string> options = Parse(args);
            if (options.TryGetValue("stills", out string times)) return Stills(times, CropArea(options), !options.ContainsKey("transparent"));
            if (options.TryGetValue("sheet", out string sheetTimes)) return Sheet(sheetTimes, options.ContainsKey("big"), CropArea(options), Follow(options));
            if (options.ContainsKey("timing")) return Timing();
            if (options.ContainsKey("verify")) return Verify();
            if (options.ContainsKey("sizes")) return Sizes();
            if (options.ContainsKey("lineup")) return Lineup();
            if (options.TryGetValue("render", out string folder)) return Render(folder);
            return Hosting.LiveWindow.Run();
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }
    }

    private static List<double> ParseTimes(string times)
    {
        List<double> list = new List<double>();
        foreach (string item in times.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            list.Add(double.Parse(item, CultureInfo.InvariantCulture));
        }

        if (list.Count == 0) throw new ArgumentException("Give one or more times in seconds, separated by commas.");
        return list;
    }

    /// <summary>The --crop option: x,y,width,height in frame pixels, or <c>null</c> for the whole frame.</summary>
    private static Rectangle? CropArea(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("crop", out string crop) || string.IsNullOrEmpty(crop)) return null;
        string[] parts = crop.Split(',');
        if (parts.Length != 4) throw new ArgumentException("--crop takes x,y,width,height (frame pixels).");
        int[] v = Array.ConvertAll(parts, p => int.Parse(p, CultureInfo.InvariantCulture));
        Rectangle area = new Rectangle(v[0], v[1], v[2], v[3]);
        if (area.X < 0 || area.Y < 0 || area.Width <= 0 || area.Height <= 0 || area.Right > Stage.Width || area.Bottom > Stage.Height)
        {
            throw new ArgumentException("--crop must lie inside the 1920x1080 frame.");
        }

        return area;
    }

    private static int Stills(string times, Rectangle? area, bool includeBackground)
    {
        string folder = Path.Combine(OutputFolder(), "stills");
        using AutumnScene scene = new AutumnScene();
        byte[] frame = new byte[Stage.Width * Stage.Height * 4];
        Stopwatch clock = new Stopwatch();
        foreach (double seconds in ParseTimes(times))
        {
            clock.Restart();
            scene.Show(seconds);
            scene.WriteFrame(frame, Stage.FrameSize, includeBackground);
            string path = Path.Combine(folder, "still-" + seconds.ToString("00.000", CultureInfo.InvariantCulture) + "s" + (area == null ? string.Empty : "-crop")
                + (includeBackground ? string.Empty : "-alpha") + ".png");
            if (area == null) FrameFiles.SaveRgba(frame, Stage.Width, Stage.Height, path);
            else FrameFiles.SaveRgbaCrop(frame, Stage.Width, Stage.Height, area.Value, path);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}  ({1:0.00} s)", path, clock.Elapsed.TotalSeconds));
        }

        return 0;
    }

    /// <summary>The --follow option: which raccoon (1-3) the crop moves with, or <c>null</c>.</summary>
    private static int? Follow(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("follow", out string which)) return null;
        int n = int.Parse(which, CultureInfo.InvariantCulture);
        if (n < 1 || n > 3) throw new ArgumentException("--follow takes a raccoon number, 1 to 3.");
        return n - 1;
    }

    private static int Sheet(string times, bool big, Rectangle? area, int? follow)
    {
        List<double> list = ParseTimes(times);
        using AutumnScene scene = new AutumnScene();
        byte[][] frames = new byte[list.Count][];
        byte[] frame = new byte[Stage.Width * Stage.Height * 4];
        Rectangle part = area ?? new Rectangle(0, 0, Stage.Width, Stage.Height);
        for (int i = 0; i < list.Count; i++)
        {
            scene.Show(list[i]);
            scene.WriteFrame(frame, Stage.FrameSize);
            if (follow != null)
            {
                // Centre the crop on the raccoon (a little behind the middle of its body, for the tail), in the frame.
                Raccoons.RaccoonRig rig = scene.Raccoons[follow.Value].Rig;
                float middle = rig.Root.Transform.Translation.X - (60 * rig.Look.Scale);
                int x = (int)Math.Clamp(middle - (part.Width / 2f), 0, Stage.Width - part.Width);
                part = new Rectangle(x, part.Y, part.Width, part.Height);
            }

            frames[i] = FrameFiles.Crop(frame, Stage.Width, part);
        }

        string name = "sheet-" + list[0].ToString("00.0", CultureInfo.InvariantCulture) + "-" + list[^1].ToString("00.0", CultureInfo.InvariantCulture) + ".png";
        string path = Path.Combine(OutputFolder(), "sheets", name);
        int columns = list.Count <= 4 ? list.Count : list.Count <= 9 ? 3 : 4;
        FrameFiles.SaveSheet(frames, part.Width, part.Height, columns, big ? 960 : 480, path);
        Console.WriteLine(path);
        return 0;
    }

    private static int Timing()
    {
        Stopwatch clock = Stopwatch.StartNew();
        using AutumnScene scene = new AutumnScene();
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Scene built in {0:0} ms: {1} settled leaves, {2} of them falling visibly; most in the air at once: {3}.",
            clock.Elapsed.TotalMilliseconds, Piles.PilePlanner.Leaves.Count, scene.FallingCount, scene.MostAirborne()));
        foreach (Piles.PileLetter letter in Piles.PileLetters.Letters)
        {
            Console.Write(string.Format(CultureInfo.InvariantCulture, "{0} {1:0}..{2:0}  ", letter.Character, letter.Region.Bounds.Left, letter.Region.Bounds.Right));
        }

        Console.WriteLine();
        System.Text.StringBuilder profile = new System.Text.StringBuilder("Leaves in the air:");
        for (int s = 0; s <= 38; s += 2) profile.Append(string.Format(CultureInfo.InvariantCulture, " {0}s:{1}", s, scene.AirborneAt(s)));
        Console.WriteLine(profile.ToString());
        byte[] frame = new byte[Stage.Width * Stage.Height * 4];
        foreach (double seconds in new[] { 10.0, 20.0, 40.0, 52.0, 64.0 })
        {
            clock.Restart();
            scene.Show(seconds);
            scene.WriteFrame(frame, Stage.FrameSize);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Export at {0,5:0.0} s (1920x1080, from scratch): {1,6:0.0} ms  ({2} leaves in the air)",
                seconds, clock.Elapsed.TotalMilliseconds, scene.AirborneAt(seconds)));
        }

        Console.WriteLine();
        // The leaf fall, the still moment, the raccoons (pushing; walking off and pushing; the run off) and the hold.
        double hold = Timeline.Hold.StartTime;
        foreach ((double start, double end) in new[] { (5.0, 7.0), (14.0, 16.0), (24.0, 26.0), (30.0, 32.0), (34.0, 36.0), (37.2, 38.2), (44.0, 46.0), (51.0, 53.0), (58.0, 60.0), (hold + 0.5, hold + 2.5) })
        {
            LiveFrameTimer.Measure(scene, start, end);
        }

        // Every frame once, then how far any paw target lay beyond its leg's reach (0 = no footfall ever slips).
        for (int f = 0; f < Timeline.FrameCount; f++) scene.Show(Timeline.Clock.TimeOfFrame(f));
        foreach (Raccoons.RaccoonActor actor in scene.Raccoons)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Raccoon {0}: furthest paw target beyond reach {1:0.00} units.", actor.Index + 1, actor.Rig.Overreach));
        }

        Console.WriteLine();
        LiveFrameTimer.MeasurePlayback(scene, Hosting.LiveWindow.CacheWorkers);

        return 0;
    }

    /// <summary>
    /// Renders the three videos of the whole animation (1920x1080, 30 fps) into a folder, named with a time stamp:
    /// on white as an H.264 MP4, and on transparency as ProRes 4444 (.mov) and VP9 (.webm), both with alpha.
    /// </summary>
    private static int Render(string folder)
    {
        string output = string.IsNullOrEmpty(folder) ? Path.Combine(OutputFolder(), "videos")
            : folder.StartsWith('~') ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + folder.Substring(1) : folder;
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        int workers = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        foreach (Video.VideoKind kind in new[] { Video.VideoKind.WhiteMp4, Video.VideoKind.AlphaProRes, Video.VideoKind.AlphaWebM })
        {
            string video = Path.Combine(output, "AutumnAviana-" + stamp + "-" + Stage.Width + "x" + Stage.Height + Video.VideoRenderer.Suffix(kind));
            Console.WriteLine("Rendering " + Timeline.FrameCount + " frames into " + video);
            Stopwatch clock = Stopwatch.StartNew();
            Video.VideoRenderer.Render(kind, video, workers);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Wrote {0} ({1:0.0} MB) in {2:0.0} s", video, new FileInfo(video).Length / 1048576.0, clock.Elapsed.TotalSeconds));
        }

        return 0;
    }

    /// <summary>
    /// Draws the three raccoons side by side, standing, each at its own size and in its own coat (a picture for
    /// comparing them; not a frame of the animation).
    /// </summary>
    private static int Lineup()
    {
        Size size = new Size(Stage.Width, 540);
        using AutumnScene scene = new AutumnScene();
        using DrawingSession session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = size,
            LayerOpacity = 255,
            ActiveStrokeOpacity = 255,
            BackgroundFillColor = Color.White,
            SurfaceClearColor = Color.White,
        });
        CodeBrix.Imaging.Drawing.Models.DrawingLayer layer = session.AddLayer("Raccoons", Color.Black);
        float[] places = { 470, 1010, 1560 };
        float[] tail = { -4f, -9f, 11f };
        for (int i = 0; i < scene.Raccoons.Count; i++)
        {
            Raccoons.RaccoonRig rig = new Raccoons.RaccoonRig(scene.Raccoons[i].Rig.Look);
            for (int k = 0; k < rig.TailSegments.Count; k++) rig.TailSegments[k].Transform = rig.TailSegments[k].Transform.WithRotation(tail[k]);
            rig.Pose(places[i], 380, 0, 0, 0,
                new Raccoons.LimbPose(new PointF(-12, -9), 0), new Raccoons.LimbPose(new PointF(-66, -9), 0),
                new Raccoons.LimbPose(new PointF(60, -7), 0), new Raccoons.LimbPose(new PointF(30, -7), 0), 0);
            layer.AddShape(rig.Root);
        }

        byte[] frame = new byte[size.Width * size.Height * 4];
        session.ExportPixels(frame, size);
        string path = Path.Combine(OutputFolder(), "stills", "lineup.png");
        FrameFiles.SaveRgba(frame, size.Width, size.Height, path);
        Console.WriteLine(path);
        return 0;
    }

    /// <summary>
    /// Prints how far any raccoon's paw target ever lay beyond its leg's reach (posing every frame, drawing none),
    /// and each raccoon's tail and body extents as built (unposed, raccoon units, before its scale): the fur is
    /// worked into the edges, so these must not grow.
    /// </summary>
    private static int Sizes()
    {
        using AutumnScene scene = new AutumnScene();

        // Every frame posed once (nothing drawn), for how far any paw target lay beyond its leg's reach.
        for (int f = Timeline.Clock.FrameAt(Timeline.Raccoons[0].Push.StartTime); f < Timeline.FrameCount; f++) scene.Show(Timeline.Clock.TimeOfFrame(f));
        foreach (Raccoons.RaccoonActor actor in scene.Raccoons)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Raccoon {0}: furthest paw target beyond reach {1:0.00} units.", actor.Index + 1, actor.Rig.Overreach));
        }

        foreach (Raccoons.RaccoonActor actor in scene.Raccoons)
        {
            Raccoons.RaccoonRig rig = new Raccoons.RaccoonRig(actor.Rig.Look);
            RectangleF tail = rig.Tail.GetBounds() ?? RectangleF.Empty;
            RectangleF body = rig.Body.GetBounds() ?? RectangleF.Empty;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Raccoon {0} (scale {1:0.00}): tail x {2:0.0}..{3:0.0}, y {4:0.0}..{5:0.0} ({6:0.0} x {7:0.0}); body with tail and head {8:0.0} x {9:0.0} units.",
                actor.Index + 1, rig.Look.Scale, tail.Left, tail.Right, tail.Top, tail.Bottom, tail.Width, tail.Height, body.Width, body.Height));
        }

        return 0;
    }

    /// <summary>
    /// Checks that the picture is a pure function of the time: a frame exported twice is the same, byte for byte; a
    /// frame exported after jumping forwards and backwards through the animation equals the same frame from a
    /// freshly built scene; and frames made in parallel by the library's frame-sequence renderer (several sessions)
    /// equal frames exported one by one.
    /// </summary>
    private static int Verify()
    {
        double[] times = { 0.0, 6.5, 17.25, 30.0, 37.5, 40.1, 44.0, 49.7, 53.5, 58.3, 62.0, Timeline.TotalSeconds - 0.5 };
        Size size = new Size(Stage.Width, Stage.Height);
        int bytes = Stage.Width * Stage.Height * 4;
        bool ok = true;
        using AutumnScene wandering = new AutumnScene();
        byte[] a = new byte[bytes];
        byte[] b = new byte[bytes];
        byte[] c = new byte[bytes];
        foreach (double t in times)
        {
            using AutumnScene fresh = new AutumnScene();
            fresh.Show(t);
            fresh.WriteFrame(a, size);
            fresh.WriteFrame(b, size);
            wandering.Show(Timeline.TotalSeconds - t);
            wandering.Show(t * 0.37);
            wandering.Show(t);
            wandering.WriteFrame(c, size);
            bool twice = a.AsSpan().SequenceEqual(b);
            bool seek = a.AsSpan().SequenceEqual(c);
            ok &= twice && seek;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,6:0.00} s: exported twice {1}; after seeking {2}",
                t, twice ? "identical" : "DIFFERENT", seek ? "identical" : "DIFFERENT"));
        }

        // Parallel rendering (four sessions) against one-by-one exports, across the camera pan and the raccoons.
        int first = Timeline.Clock.FrameAt(Timeline.Pan.StartTime + 1);
        int last = first + 11;
        byte[][] parallel = new byte[last - first + 1][];
        FrameSequenceRenderer renderer = new FrameSequenceRenderer(Timeline.Clock, () => new AutumnScene().Session, size) { MaxDegreeOfParallelism = 4 };
        renderer.Render(first, last, (frame, seconds, rgba) => parallel[frame - first] = rgba.ToArray());
        int same = 0;
        for (int f = first; f <= last; f++)
        {
            wandering.Show(Timeline.Clock.TimeOfFrame(f));
            wandering.WriteFrame(c, size);
            if (parallel[f - first].AsSpan().SequenceEqual(c)) same++;
        }

        ok &= same == parallel.Length;
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Frames {0}-{1} rendered in parallel: {2} of {3} identical to one-by-one exports.",
            first, last, same, parallel.Length));
        Console.WriteLine(ok ? "Every frame is a pure function of its time." : "MISMATCH: some frames depend on more than their time.");
        return ok ? 0 : 1;
    }

    /// <summary>The sample's Output folder (git-ignored), next to AutumnAviana.csproj.</summary>
    public static string OutputFolder()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo folder = new DirectoryInfo(start);
            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder.FullName, "AutumnAviana.csproj"))) return Path.Combine(folder.FullName, "Output");
                folder = folder.Parent;
            }
        }

        return Path.Combine(Environment.CurrentDirectory, "Output");
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Unexpected argument '" + args[i] + "'.");
            string name = args[i].Substring(2);
            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : string.Empty;
            options[name] = value;
        }

        return options;
    }
}
