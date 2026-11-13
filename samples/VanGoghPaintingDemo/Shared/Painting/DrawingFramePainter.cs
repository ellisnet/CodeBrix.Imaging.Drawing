using System;
using CodeBrix.Imaging.Drawing.Painting;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// The C# port of the animation, painted with CodeBrix.Imaging.Drawing. One painting is made every
/// <see cref="Timeline.KeyframeSeconds" /> seconds (on the brushstroke layouts in turn), and each video frame is
/// the dissolve between the two nearest ones - the library's <see cref="KeyframePlayer" />, which also paints the
/// next painting in the background while two are on screen, so playback only waits after a jump to a new time.
/// </summary>
internal sealed class DrawingFramePainter : IFramePainter
{
    private readonly Painter painter;
    private readonly KeyframePlayer player;

    /// <summary>Creates the painter (this lays out the brushstrokes, which takes a few seconds).</summary>
    /// <param name="name">The painter's short name: which drawing package this build of the code uses.</param>
    /// <param name="log">Receives progress messages; may be null.</param>
    public DrawingFramePainter(string name, Action<string> log = null)
    {
        Name = name;
        painter = new Painter(log);
        player = new KeyframePlayer(Timeline.Width, Timeline.Height, Timeline.KeyframeSeconds,
            (keyframe, seconds, rgba) => painter.Paint(seconds, keyframe, rgba));
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public void Prepare(double seconds) => player.Prefetch(seconds);

    /// <inheritdoc />
    public void RenderFrame(double seconds, Span<byte> rgba) => player.RenderFrame(seconds, rgba);

    /// <summary>Waits for any painting still in progress, then releases the painter.</summary>
    public void Dispose()
    {
        player.Dispose();
        painter.Dispose();
    }
}
