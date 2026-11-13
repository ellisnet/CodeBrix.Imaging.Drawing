using System;

namespace AnimationDemoApp.Demo;

/// <summary>
/// Something that can produce any frame of the demo. The sample has two: the scenes drawn with
/// CodeBrix.Imaging.Drawing (SkiaSharp) in this process, and the same scenes drawn with
/// CodeBrix.Imaging.Drawing.NoSkia in a second program.
/// </summary>
internal interface IFramePainter : IDisposable
{
    /// <summary>A short name for the painter ("skia" or "noskia"), for window titles and file names.</summary>
    string Name { get; }

    /// <summary>
    /// Says that <see cref="RenderFrame" /> is about to be asked for a time, so a painter that works elsewhere (in
    /// another process) can start on it while the other painters produce theirs. Optional.
    /// </summary>
    /// <param name="seconds">The demo time.</param>
    void Prepare(double seconds)
    {
    }

    /// <summary>Produces the frame at a time.</summary>
    /// <param name="seconds">The demo time (on the timeline of all the chosen scenes, one after another).</param>
    /// <param name="rgba">Receives the frame: <see cref="DemoFrame.Width" /> x <see cref="DemoFrame.Height" /> pixels, four bytes (R, G, B, A) each.</param>
    void RenderFrame(double seconds, Span<byte> rgba);
}
