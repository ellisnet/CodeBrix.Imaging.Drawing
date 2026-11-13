using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using VanGoghPaintingDemo.Painting;

namespace VanGoghPaintingDemo;

/// <summary>
/// The Van Gogh painting, made without SkiaSharp: the same painting code as the main application, built against
/// CodeBrix.Imaging.Drawing.NoSkia. It is a console program with no window of its own.
/// <code>
/// dotnet run -- 60,110,200,260     # the video frames at those times -> Output/stills/frame-noskia-*.png
/// dotnet run -- --serve            # paint frames on request (this is how the main application uses it)
/// </code>
/// SERVING FRAMES: one application cannot reference both drawing packages, so the main application starts this
/// program with <c>--serve</c> and talks to it over its standard input and output. Once ready, the program
/// writes the four bytes "RDY" + newline. Each request is one line of text on standard input - the animation
/// time in seconds - and the reply is that frame on standard output as raw pixels:
/// <see cref="Timeline.Width" /> x <see cref="Timeline.Height" />, four bytes (R, G, B, A) each. The program ends
/// when its standard input is closed. Progress messages go to standard error.
/// </summary>
internal static class Program
{
    /// <summary>The painter's short name, used in file names and window titles.</summary>
    private const string Name = "noskia";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--serve") return Serve();

            string times = args.Length > 0 ? args[0] : "60,110,200,260";
            string folder = Path.Combine(OutputFolder(), "stills");
            Stopwatch clock = Stopwatch.StartNew();
            using DrawingFramePainter painter = new DrawingFramePainter(Name, Console.WriteLine);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "ready in {0:0.0} s", clock.Elapsed.TotalSeconds));
            byte[] frame = new byte[Timeline.Width * Timeline.Height * 4];
            foreach (string item in times.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                double seconds = double.Parse(item, CultureInfo.InvariantCulture);
                clock.Restart();
                painter.RenderFrame(seconds, frame);
                string path = Path.Combine(folder, "frame-" + Name + "-" + seconds.ToString("000.000", CultureInfo.InvariantCulture) + "s.png");
                FrameFiles.SaveRgba(frame, Timeline.Width, Timeline.Height, path);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}  ({1:0.0} s)", path, clock.Elapsed.TotalSeconds));
            }

            return 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }
    }

    private static int Serve()
    {
        using Stream replies = Console.OpenStandardOutput();
        using DrawingFramePainter painter = new DrawingFramePainter(Name, Console.Error.WriteLine);
        replies.Write("RDY\n"u8);
        replies.Flush();
        byte[] frame = new byte[Timeline.Width * Timeline.Height * 4];
        string request;
        while ((request = Console.In.ReadLine()) != null)
        {
            painter.RenderFrame(double.Parse(request, CultureInfo.InvariantCulture), frame);
            replies.Write(frame, 0, frame.Length);
            replies.Flush();
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
}
