using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AnimationDemoApp.Demo;

namespace AnimationDemoApp;

/// <summary>
/// The demo scenes drawn without SkiaSharp: the same scene code as the main application, built against
/// CodeBrix.Imaging.Drawing.NoSkia. It is a console program with no window of its own.
/// <code>
/// dotnet run -- 2,10,20                      # the frames at those times -> Output/stills/frame-noskia-*.png
/// dotnet run -- 1,3 --scene text,fade        # only those scenes, on their own timeline
/// dotnet run -- --serve [--scene text]       # draw frames on request (this is how the main application uses it)
/// </code>
/// SERVING FRAMES: one application cannot reference both drawing packages, so the main application starts this
/// program with <c>--serve</c> (and the same <c>--scene</c> choice it was given) and talks to it over its
/// standard input and output. Once ready, the program writes the four bytes "RDY" + newline. Each request is one
/// line of text on standard input - the demo time in seconds - and the reply is that frame on standard output as
/// raw pixels: <see cref="DemoFrame.Width" /> x <see cref="DemoFrame.Height" />, four bytes (R, G, B, A) each.
/// The program ends when its standard input is closed. Messages go to standard error.
/// </summary>
internal static class Program
{
    /// <summary>The painter's short name, used in file names and window titles.</summary>
    private const string Name = "noskia";

    private static int Main(string[] args)
    {
        try
        {
            bool serve = false;
            string times = "2,10,20";
            string scenes = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--serve") serve = true;
                else if (args[i] == "--scene" && i + 1 < args.Length) scenes = args[++i];
                else if (!args[i].StartsWith("--", StringComparison.Ordinal)) times = args[i];
                else throw new ArgumentException("Unexpected argument '" + args[i] + "'.");
            }

            if (serve) return Serve(scenes);

            string folder = Path.Combine(OutputFolder(), "stills");
            Stopwatch clock = Stopwatch.StartNew();
            using DemoFramePainter painter = new DemoFramePainter(Name, scenes);
            byte[] frame = new byte[DemoFrame.Bytes];
            foreach (string item in times.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                double seconds = double.Parse(item, CultureInfo.InvariantCulture);
                clock.Restart();
                painter.RenderFrame(seconds, frame);
                string path = Path.Combine(folder, "frame-" + Name + "-" + seconds.ToString("000.000", CultureInfo.InvariantCulture) + "s.png");
                FrameFiles.SaveRgba(frame, DemoFrame.Width, DemoFrame.Height, path);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}  ({1:0.00} s)", path, clock.Elapsed.TotalSeconds));
            }

            return 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }
    }

    private static int Serve(string scenes)
    {
        using Stream replies = Console.OpenStandardOutput();
        using DemoFramePainter painter = new DemoFramePainter(Name, scenes);
        replies.Write("RDY\n"u8);
        replies.Flush();
        byte[] frame = new byte[DemoFrame.Bytes];
        string request;
        while ((request = Console.In.ReadLine()) != null)
        {
            painter.RenderFrame(double.Parse(request, CultureInfo.InvariantCulture), frame);
            replies.Write(frame, 0, frame.Length);
            replies.Flush();
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
}
