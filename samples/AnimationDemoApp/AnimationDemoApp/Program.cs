using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AnimationDemoApp.Demo;
using AnimationDemoApp.Hosting;
using AnimationDemoApp.NoSkia;

namespace AnimationDemoApp;

/// <summary>
/// A tour of the CodeBrix.Imaging.Drawing animation features, scene by scene, drawn with CodeBrix.Imaging.Drawing
/// and shown side by side with the same scenes drawn through CodeBrix.Imaging.Drawing.NoSkia. 1280x720.
/// <code>
/// dotnet run                                   # live, in a window: Skia | NoSkia side by side (Tab switches view)
/// dotnet run -- --stills 2,10,20               # frames at those times -> Output/stills (one per painter, plus a
///                                              #   side-by-side "compare" image)
/// dotnet run -- --scene text,fade              # only those scenes (any of the above), on their own timeline
/// dotnet run -- --list                         # the scenes, with their times
/// dotnet run -- --engine skia                  # any of the above with chosen painters: a comma-separated list of
///                                              #   skia and noskia (the default is skia,noskia)
/// </code>
/// THE TWO PAINTERS: <c>skia</c> is this application's own drawing, with CodeBrix.Imaging.Drawing. <c>noskia</c> is
/// the same scene code built against the completely managed package: one application cannot reference both
/// packages, so it runs as a second program (the AnimationDemoApp.NoSkia project) that this one starts and asks
/// for frames - see <see cref="NoSkiaPainter" />. The scenes are listed in Shared/Demo/SceneCatalog.cs.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        List<IFramePainter> painters = new List<IFramePainter>();
        try
        {
            Dictionary<string, string> options = Parse(args);
            options.TryGetValue("scene", out string scenes);
            using SceneCatalog catalog = new SceneCatalog(scenes);
            if (options.ContainsKey("list")) return List(catalog);

            string[] engines = (options.TryGetValue("engine", out string chosen) ? chosen.ToLowerInvariant() : "skia,noskia")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (engines.Length == 0 || engines.Distinct().Count() != engines.Length || engines.Any(e => e is not ("skia" or "noskia")))
            {
                throw new ArgumentException("--engine takes a comma-separated list of skia and noskia (the default is skia,noskia).");
            }

            foreach (string engine in engines)
            {
                Console.WriteLine("Starting the " + engine + " painter ...");
                painters.Add(engine == "noskia" ? new NoSkiaPainter(scenes) : new DemoFramePainter("skia", scenes));
            }

            if (options.TryGetValue("stills", out string times)) return Stills(painters, times, OutputFolder());
            return LiveWindow.Run(painters, catalog);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }
        finally
        {
            for (int i = painters.Count - 1; i >= 0; i--) painters[i].Dispose();
        }
    }

    private static int List(SceneCatalog catalog)
    {
        for (int i = 0; i < catalog.Scenes.Count; i++)
        {
            DemoScene scene = catalog.Scenes[i];
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,6:0.0} s  {1,-12} {2} ({3:0.0} s) - {4}",
                catalog.StartOf(i), scene.Name, scene.Title, scene.DurationSeconds, scene.Description));
        }

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,6:0.0} s  end", catalog.TotalSeconds));
        return 0;
    }

    private static int Stills(List<IFramePainter> painters, string times, string output)
    {
        const int W = DemoFrame.Width;
        const int H = DemoFrame.Height;
        string folder = Path.Combine(output, "stills");
        byte[] frame = new byte[DemoFrame.Bytes];
        byte[] both = painters.Count > 1 ? new byte[painters.Count * DemoFrame.Bytes] : null;
        foreach (string item in times.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            double seconds = double.Parse(item, CultureInfo.InvariantCulture);
            string stamp = seconds.ToString("000.000", CultureInfo.InvariantCulture) + "s.png";
            foreach (IFramePainter painter in painters) painter.Prepare(seconds);
            for (int p = 0; p < painters.Count; p++)
            {
                painters[p].RenderFrame(seconds, frame);
                string path = Path.Combine(folder, "frame-" + painters[p].Name + "-" + stamp);
                FrameFiles.SaveRgba(frame, W, H, path);
                Console.WriteLine(path);
                if (both == null) continue;
                for (int y = 0; y < H; y++)
                {
                    frame.AsSpan(y * W * 4, W * 4).CopyTo(both.AsSpan(((y * painters.Count) + p) * W * 4, W * 4));
                }
            }

            if (both == null) continue;
            string compare = Path.Combine(folder, "compare-" + stamp);
            FrameFiles.SaveRgba(both, painters.Count * W, H, compare);
            Console.WriteLine(compare);
        }

        return 0;
    }

    /// <summary>The sample's Output folder (git-ignored), next to AnimationDemoApp.slnx.</summary>
    private static string OutputFolder()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo folder = new DirectoryInfo(start);
            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder.FullName, "AnimationDemoApp.slnx"))) return Path.Combine(folder.FullName, "Output");
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
