using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AnimationDemoApp.Demo;

namespace AnimationDemoApp.NoSkia;

/// <summary>
/// The scenes drawn through CodeBrix.Imaging.Drawing.NoSkia. One application cannot reference both drawing
/// packages, so the NoSkia drawing lives in its own console program (the AnimationDemoApp.NoSkia project, copied
/// into the "noskia" folder beside this application when it is built). This class starts that program once, with
/// <c>--serve</c>, and keeps it running: each frame is asked for with one line on its standard input (the demo
/// time) and read back from its standard output as raw pixels.
/// </summary>
internal sealed class NoSkiaPainter : IFramePainter
{
    private readonly Process process;
    private readonly Stream replies;
    private double? asked;
    private bool disposed;

    /// <summary>Starts the NoSkia program and waits until it is ready to draw.</summary>
    /// <param name="scenes">The scenes to play (the same choice the main application was given); or null for all.</param>
    /// <exception cref="InvalidOperationException">The program is missing, or did not start.</exception>
    public NoSkiaPainter(string scenes)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "noskia");
        string program = Path.Combine(folder, "AnimationDemoApp.NoSkia.dll");
        if (!File.Exists(program)) throw new InvalidOperationException("The NoSkia painter is missing from " + folder + ". Build AnimationDemoApp.slnx.");

        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        start.ArgumentList.Add(program);
        start.ArgumentList.Add("--serve");
        if (!string.IsNullOrWhiteSpace(scenes))
        {
            start.ArgumentList.Add("--scene");
            start.ArgumentList.Add(scenes);
        }

        process = Process.Start(start) ?? throw new InvalidOperationException("The NoSkia painter did not start.");
        replies = process.StandardOutput.BaseStream;

        // The program says "RDY" once its scenes are built.
        Span<byte> ready = stackalloc byte[4];
        Read(ready);
        if (!ready.SequenceEqual("RDY\n"u8)) throw new InvalidOperationException("The NoSkia painter did not start properly.");
    }

    /// <inheritdoc />
    public string Name => "noskia";

    /// <summary>Asks the NoSkia program for a frame now, so it draws while this process draws its own.</summary>
    /// <param name="seconds">The demo time.</param>
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
        if (rgba.Length < DemoFrame.Bytes) throw new ArgumentException("The frame buffer is too small.", nameof(rgba));

        // A frame asked for earlier must be read before another can be asked for; one for another time is dropped.
        if (asked != null && asked != seconds) Read(rgba.Slice(0, DemoFrame.Bytes));
        if (asked != seconds)
        {
            asked = null;
            Prepare(seconds);
        }

        Read(rgba.Slice(0, DemoFrame.Bytes));
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
