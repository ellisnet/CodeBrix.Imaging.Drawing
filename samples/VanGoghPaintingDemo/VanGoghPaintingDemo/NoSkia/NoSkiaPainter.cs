using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using VanGoghPaintingDemo.Painting;

namespace VanGoghPaintingDemo.NoSkia;

/// <summary>
/// The painting made through CodeBrix.Imaging.Drawing.NoSkia. One application cannot reference both drawing
/// packages, so the NoSkia painting lives in its own console program (the VanGoghPaintingDemo.NoSkia project,
/// copied into the "noskia" folder beside this application when it is built). This class starts that program once,
/// with <c>--serve</c>, and keeps it running: each frame is asked for with one line on its standard input (the
/// animation time) and read back from its standard output as raw pixels.
/// </summary>
internal sealed class NoSkiaPainter : IFramePainter
{
    private const int FrameBytes = Timeline.Width * Timeline.Height * 4;

    private readonly Process process;
    private readonly Stream replies;
    private double? asked;
    private bool disposed;

    /// <summary>Starts the NoSkia program and waits until it is ready to paint.</summary>
    /// <exception cref="InvalidOperationException">The program is missing, or did not start.</exception>
    public NoSkiaPainter()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "noskia");
        string program = Path.Combine(folder, "VanGoghPaintingDemo.NoSkia.dll");
        if (!File.Exists(program)) throw new InvalidOperationException("The NoSkia painter is missing from " + folder + ". Build VanGoghPaintingDemo.slnx.");

        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        start.ArgumentList.Add(program);
        start.ArgumentList.Add("--serve");
        process = Process.Start(start) ?? throw new InvalidOperationException("The NoSkia painter did not start.");
        replies = process.StandardOutput.BaseStream;

        // The program says "RDY" once its brushstroke layouts are built.
        Span<byte> ready = stackalloc byte[4];
        Read(ready);
        if (!ready.SequenceEqual("RDY\n"u8)) throw new InvalidOperationException("The NoSkia painter did not start properly.");
    }

    /// <inheritdoc />
    public string Name => "noskia";

    /// <summary>Asks the NoSkia program for a frame now, so it paints while this process paints its own.</summary>
    /// <param name="seconds">The animation time.</param>
    public void Prepare(double seconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (asked != null) return;
        process.StandardInput.WriteLine(seconds.ToString("R", CultureInfo.InvariantCulture));
        process.StandardInput.Flush();
        asked = seconds;
    }

    /// <inheritdoc />
    public void RenderFrame(double seconds, Span<byte> rgba)
    {
        if (rgba.Length < FrameBytes) throw new ArgumentException("The frame buffer is too small.", nameof(rgba));

        // A frame asked for earlier must be read before another can be asked for; one for another time is dropped.
        if (asked != null && asked != seconds) Read(rgba.Slice(0, FrameBytes));
        if (asked != seconds)
        {
            asked = null;
            Prepare(seconds);
        }

        Read(rgba.Slice(0, FrameBytes));
        asked = null;
    }

    /// <summary>Ends the NoSkia program.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            process.StandardInput.Close(); // the program ends when its input does
            if (!process.WaitForExit(5000)) process.Kill();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            // The program had already gone.
        }

        process.Dispose();
    }

    private void Read(Span<byte> buffer)
    {
        try
        {
            replies.ReadExactly(buffer);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidOperationException("The NoSkia painter stopped unexpectedly.", e);
        }
    }
}
