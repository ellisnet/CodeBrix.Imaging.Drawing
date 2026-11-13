using System;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// Something that can produce any frame of the animation. The sample has three: the C# painting drawn with
/// CodeBrix.Imaging.Drawing (SkiaSharp), the same painting drawn with CodeBrix.Imaging.Drawing.NoSkia, and the
/// original Python script.
/// </summary>
internal interface IFramePainter : IDisposable
{
    /// <summary>A short name for the painter, for window titles and file names.</summary>
    string Name { get; }

    /// <summary>
    /// Says that <see cref="RenderFrame" /> is about to be asked for a time, so a painter that works elsewhere (in
    /// another process, say) can start on it while the other painters produce theirs. Optional.
    /// </summary>
    /// <param name="seconds">The animation time.</param>
    void Prepare(double seconds)
    {
    }

    /// <summary>Produces the video frame at a time.</summary>
    /// <param name="seconds">The animation time.</param>
    /// <param name="rgba">Receives the frame: <see cref="Timeline.Width" /> x <see cref="Timeline.Height" /> pixels, four bytes (R, G, B, A) each.</param>
    void RenderFrame(double seconds, Span<byte> rgba);
}
