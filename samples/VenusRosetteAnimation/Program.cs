using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CodeBrix.Imaging.Drawing.SafeAreas;
using VenusRosetteAnimation.Animation;
using VenusRosetteAnimation.Hosting;
using VenusRosetteAnimation.Video;

namespace VenusRosetteAnimation;

/// <summary>
/// The Venus rosette: Earth and Venus circle the Sun, and every three days a thread joins them; over eight years
/// (thirteen Venus years) the threads weave a five-petalled rosette. A 21-second, 30 fps animation drawn with
/// CodeBrix.Imaging.Drawing, in two formats: landscape 1920x1080 and portrait 1080x1920.
/// <code>
/// dotnet run                                  # live, in a window (landscape; --portrait for portrait)
/// dotnet run -- --stills 0.5,10,21            # single landscape frames at those times -> Output/stills
/// dotnet run -- --stills 0.5,10,21 --portrait # the same, portrait
/// dotnet run -- --render                      # both videos, raw frames piped to ffmpeg:
///                                             #   Output/VenusRosette-*-1920x1080.mp4 and -1080x1920.mp4
/// dotnet run -- --render landscape            # just one of them (landscape or portrait)
/// dotnet run -- --check                       # the safe-area check of both layouts, then exit
/// </code>
/// Add --safe-area to show the layout's safe-area guide as a translucent overlay in the live window (S toggles it)
/// and in stills; videos never carry it. Every run that makes stills or videos also prints the layout's safe-area
/// check (advisory). The video is encoded by ffmpeg (through CodeBrix.VideoProcessing), which must be installed and
/// on the PATH.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Dictionary<string, string> options = Parse(args);
            bool showSafeArea = options.ContainsKey("safe-area");
            Layout chosen = options.ContainsKey("portrait") ? Layout.Portrait : Layout.Landscape;
            if (options.ContainsKey("check"))
            {
                foreach (Layout layout in new[] { Layout.Landscape, Layout.Portrait })
                {
                    using RosetteScene checkScene = new RosetteScene(layout);
                    ReportSafeArea(checkScene);
                }

                return 0;
            }

            if (options.TryGetValue("stills", out string times))
            {
                using RosetteScene stillScene = new RosetteScene(chosen);
                if (showSafeArea) stillScene.Overlay = new SafeAreaOverlay(chosen.SafeArea) { Duration = Timeline.TotalSeconds };
                ReportSafeArea(stillScene);
                return Stills(stillScene, times);
            }

            if (options.TryGetValue("render", out string which)) return Render(which);
            using RosetteScene scene = new RosetteScene(chosen);
            return LiveWindow.Run(scene, showSafeArea);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }
    }

    private static int Stills(RosetteScene scene, string times)
    {
        string folder = Path.Combine(OutputFolder(), "stills");
        Directory.CreateDirectory(folder);
        string suffix = scene.Overlay == null ? string.Empty : "-safe-area";
        foreach (string item in times.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            double seconds = double.Parse(item, CultureInfo.InvariantCulture);
            scene.Show(seconds);
            string path = Path.Combine(folder, "still-" + scene.Layout.Name + "-" + seconds.ToString("00.000", CultureInfo.InvariantCulture) + "s" + suffix + ".png");
            scene.SavePng(path);
            Console.WriteLine(path);
        }

        return 0;
    }

    private static int Render(string which)
    {
        List<Layout> layouts = which.ToLowerInvariant() switch
        {
            "" or "both" => new List<Layout> { Layout.Landscape, Layout.Portrait },
            "landscape" => new List<Layout> { Layout.Landscape },
            "portrait" => new List<Layout> { Layout.Portrait },
            _ => throw new ArgumentException("--render takes landscape, portrait or both (the default)."),
        };

        string output = OutputFolder();
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        foreach (Layout layout in layouts)
        {
            using RosetteScene scene = new RosetteScene(layout);
            ReportSafeArea(scene);
            string video = Path.Combine(output, "VenusRosette-" + stamp + "-" + layout.Width + "x" + layout.Height + ".mp4");
            Console.WriteLine("Rendering " + Timeline.FrameCount + " " + layout.Name + " frames into " + video);
            VideoRenderer.Render(scene, video);
            Console.WriteLine("Wrote " + video);
        }

        return 0;
    }

    /// <summary>Prints the layout's safe-area check (advisory: it never stops a run).</summary>
    private static void ReportSafeArea(RosetteScene scene)
    {
        IReadOnlyList<SafeAreaFinding> findings = scene.CheckSafeArea();
        string guide = scene.Layout.SafeArea.Name;
        if (findings.Count == 0)
        {
            Console.WriteLine("Safe area (" + scene.Layout.Name + ", " + guide + "): all text is clear.");
            return;
        }

        Console.WriteLine("Safe area (" + scene.Layout.Name + ", " + guide + "): " + findings.Count + " finding(s), advisory:");
        foreach (SafeAreaFinding finding in findings) Console.WriteLine("  " + finding);
    }

    /// <summary>The sample's Output folder (git-ignored), next to VenusRosetteAnimation.csproj.</summary>
    private static string OutputFolder()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo folder = new DirectoryInfo(start);
            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder.FullName, "VenusRosetteAnimation.csproj"))) return Path.Combine(folder.FullName, "Output");
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
