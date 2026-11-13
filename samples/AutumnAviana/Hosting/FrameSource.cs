using System;
using System.Threading.Tasks;
using AutumnAviana.Animation;
using AutumnAviana.Scene;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.NoSkia;

namespace AutumnAviana.Hosting;

/// <summary>
/// Where the live view's frames come from: the library's <see cref="FrameCache" /> for the camera pan (rendered ahead
/// of the play head on background workers, each keeping one scene for the whole run), the scene's own session
/// (rendering live with its layer caches) for everything else. The pan is not still apart from the camera: the
/// first raccoon and its letters are already rolling in as it plays. That needs nothing special - the cache renders
/// every frame from its own scene at that frame's time, raccoon and all - but it is why the cached stretch is
/// exactly the pan: every frame in it moves every pile leaf.
/// <para>
/// One subtlety: while the pan plays from the cache, the live session sits idle with its layers drawn for the
/// camera as it was before the pan, so the first live frame after the pan would have to redraw every pile leaf (a
/// visible hitch). So as soon as the pan starts playing from the cache, the live session is rendered once, on a
/// background task, at the camera's final position - into a spare canvas - and the frames after the pan find their
/// layers ready. The main thread only touches the scene again after that task has finished.
/// </para>
/// </summary>
internal sealed class FrameSource : IDisposable
{
    private readonly AutumnScene scene;
    private readonly DrawingCanvas canvas;
    private readonly DrawingImageInfo info;
    private readonly FrameCache cache;
    private readonly DrawingBitmap spareBitmap;
    private readonly DrawingCanvas spareCanvas;
    private Task warming;
    private bool lastWasCached;

    /// <summary>Creates the frame source.</summary>
    /// <param name="scene">The scene the live frames are drawn from.</param>
    /// <param name="canvas">The canvas live frames are drawn on (over the bitmap the frames are read from).</param>
    /// <param name="info">The canvas' size and format.</param>
    /// <param name="workers">The frame cache's worker count.</param>
    public FrameSource(AutumnScene scene, DrawingCanvas canvas, DrawingImageInfo info, int workers)
    {
        this.scene = scene;
        this.canvas = canvas;
        this.info = info;
        // Every pan frame is wanted ahead, from as early as possible (only those frames are rendered and kept), so the
        // cache's capacity is the pan's frame count: the whole pan is ready long before it plays.
        int panFrames = 0;
        for (int f = 0; f < Timeline.FrameCount; f++)
        {
            if (IsCached(f)) panFrames++;
        }

        cache = new FrameCache(Timeline.Clock, () => new AutumnScene().Session, new Size(info.Width, info.Height), panFrames, workers)
        {
            ShouldRender = IsCached,
        };
        spareBitmap = new DrawingBitmap(info);
        spareCanvas = new DrawingCanvas(spareBitmap);
    }

    /// <summary>
    /// Whether a frame comes from the frame cache: the camera pan (every pile leaf moves, so the whole piles layer is
    /// redrawn each frame - several times too slow to draw live).
    /// </summary>
    /// <param name="frame">The frame number.</param>
    /// <returns><c>true</c> for the frames the cache renders ahead.</returns>
    public static bool IsCached(int frame)
    {
        double t = Timeline.Clock.TimeOfFrame(frame);
        return t >= Timeline.Pan.StartTime && t <= Timeline.Pan.EndTime + (1.0 / Timeline.Fps);
    }

    /// <summary>
    /// Puts a frame into the frame's pixels: copied from the cache when it is there, otherwise drawn live on the
    /// canvas.
    /// </summary>
    /// <param name="frame">The frame number.</param>
    /// <param name="seconds">The time to show (the play head's exact time when drawn live).</param>
    /// <param name="pixels">The pixels under the canvas (width x height x 4 bytes, R, G, B, A).</param>
    /// <returns><c>true</c> when the frame came from the cache.</returns>
    public bool Draw(int frame, double seconds, Span<byte> pixels)
    {
        cache.SetPlayHead(frame);
        if (cache.TryGetFrame(frame, pixels))
        {
            if (!lastWasCached && warming == null) warming = Task.Run(WarmAfterPan);
            lastWasCached = true;
            return true;
        }

        lastWasCached = false;
        if (warming != null)
        {
            warming.Wait();
            warming = null;
        }

        scene.Show(seconds);
        scene.Session.Render(canvas, info, true);
        return false;
    }

    /// <summary>Stops the cache and waits for any warming.</summary>
    public void Dispose()
    {
        warming?.Wait();
        cache.Dispose();
        spareCanvas.Dispose();
        spareBitmap.Dispose();
    }

    private void WarmAfterPan()
    {
        scene.Show(Timeline.Pan.EndTime + (2.0 / Timeline.Fps));
        scene.Session.Render(spareCanvas, info, true);
    }
}
