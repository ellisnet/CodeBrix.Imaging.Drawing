using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using VanGoghPaintingDemo.Hosting;
using VanGoghPaintingDemo.NoSkia;
using VanGoghPaintingDemo.Painting;
using VanGoghPaintingDemo.Python;
using VanGoghPaintingDemo.Video;

namespace VanGoghPaintingDemo;

/// <summary>
/// A Van Gogh-style painted animation - the Boat of Heaven crossing a torch-lit river at night, the Octaquetra
/// blossoming in the sky, the temple's light and the dawn - painted in C# with CodeBrix.Imaging.Drawing, and
/// shown side by side with the same painting made through CodeBrix.Imaging.Drawing.NoSkia.
/// 1080x1920 (portrait), 30 fps, a little over five minutes.
/// <code>
/// dotnet run                                   # live, in a window: Skia | NoSkia side by side (Tab switches view)
/// dotnet run -- --stills 60,110,200            # frames at those times -> Output/stills (one per painter, plus a
///                                              #   side-by-side "compare" image)
/// dotnet run -- --render                       # the whole video, once per painter -> Output/VanGogh-*.mp4
/// dotnet run -- --render --from 96 --to 120    # just a stretch of it
/// dotnet run -- --engine skia                  # any of the above with chosen painters: a comma-separated list of
///                                              #   skia, noskia and python (the default is skia,noskia)
/// </code>
/// Videos are encoded by ffmpeg (through CodeBrix.VideoProcessing), which must be installed and on the PATH.
/// <para>
/// THE THREE PAINTERS: <c>skia</c> is this application's own painting, drawn with CodeBrix.Imaging.Drawing.
/// <c>noskia</c> is the same painting code built against the completely managed package: one application
/// cannot reference both packages, so it runs as a second program (the VanGoghPaintingDemo.NoSkia project) that
/// this one starts and asks for frames - see <see cref="NoSkiaPainter" />. <c>python</c> is the Python script the
/// animation was ported from (assets/Python), run inside this process by CodeBrix.Python; it is off unless asked
/// for, and needs a CPython (with its shared library) that has numpy, scipy and Pillow.
/// </para>
/// <code>
/// dotnet run -- --engine python,skia           # the Python original beside its C# port
/// dotnet run -- --engine python,skia --python-dll /path/libpython3.13.so   # which Python to use (else PYTHONNET_PYDLL, else searched for)
/// </code>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        List<IFramePainter> painters = new List<IFramePainter>();
        try
        {
            Dictionary<string, string> options = Parse(args);
            string[] engines = (options.TryGetValue("engine", out string chosen) ? chosen.ToLowerInvariant() : "skia,noskia")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (engines.Length == 0 || engines.Distinct().Count() != engines.Length || engines.Any(e => e is not ("skia" or "noskia" or "python")))
            {
                throw new ArgumentException("--engine takes a comma-separated list of skia, noskia and python (the default is skia,noskia).");
            }

            options.TryGetValue("python-dll", out string pythonDll);
            string output = OutputFolder();
            foreach (string engine in engines)
            {
                Console.WriteLine("Starting the " + engine + " painter ...");
                painters.Add(engine switch
                {
                    "python" => new PythonPainter(pythonDll, Path.Combine(output, "python-cache")),
                    "noskia" => new NoSkiaPainter(),
                    _ => new DrawingFramePainter("skia", Console.WriteLine),
                });
            }

            if (options.TryGetValue("stills", out string times)) return Stills(painters, times, output);
            if (options.ContainsKey("render")) return Render(painters, options, output);
            return LiveWindow.Run(painters);
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

    private static int Stills(List<IFramePainter> painters, string times, string output)
    {
        const int W = Timeline.Width;
        const int H = Timeline.Height;
        string folder = Path.Combine(output, "stills");
        byte[] frame = new byte[W * H * 4];
        byte[] both = painters.Count > 1 ? new byte[painters.Count * W * H * 4] : null;
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

    private static int Render(List<IFramePainter> painters, Dictionary<string, string> options, string output)
    {
        double from = options.TryGetValue("from", out string a) ? double.Parse(a, CultureInfo.InvariantCulture) : 0;
        double to = options.TryGetValue("to", out string b) ? double.Parse(b, CultureInfo.InvariantCulture) : Timeline.TotalSeconds;
        if (from < 0 || to > Timeline.TotalSeconds + 0.001 || to <= from) throw new ArgumentException("--from and --to must lie within the animation, with --to after --from.");
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        foreach (IFramePainter painter in painters)
        {
            string video = Path.Combine(output, "VanGogh-" + painter.Name + "-" + stamp + "-" + Timeline.Width + "x" + Timeline.Height + ".mp4");
            Console.WriteLine("Rendering " + video);
            VideoRenderer.Render(painter, from, to, video);
            Console.WriteLine("Wrote " + video);
        }

        return 0;
    }

    /// <summary>The sample's Output folder (git-ignored), next to VanGoghPaintingDemo.slnx.</summary>
    private static string OutputFolder()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo folder = new DirectoryInfo(start);
            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder.FullName, "VanGoghPaintingDemo.slnx"))) return Path.Combine(folder.FullName, "Output");
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
