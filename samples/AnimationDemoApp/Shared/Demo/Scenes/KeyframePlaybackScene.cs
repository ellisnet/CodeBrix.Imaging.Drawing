using System;
using System.Globalization;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Keyframe playback: a painting too costly for every frame is made once a second - a moving sky turned into
/// brushstrokes, on three stroke layouts in turn - and the frames between dissolve from one painting to the next.
/// A <see cref="KeyframePlayer" /> paints keyframes on a background worker (the next one ahead of time), keeps a
/// small cache, and gives exact frames after any seek. Shown: the played frame, the two keyframes it is made from,
/// a strip of the second's keyframes with the play head, and the player's counters.
/// </summary>
internal sealed class KeyframePlaybackScene : DemoScene
{
    private const int W = 480;
    private const int H = 360;
    private const int ThumbW = 240;
    private const int ThumbH = 180;
    private const int PanelY = 150;
    private const int Left = 48;

    private readonly DrawingSession frame;
    private readonly DrawingLayer readout;
    private readonly DrawingOwnershipMap[] maps = new DrawingOwnershipMap[3];
    private readonly KeyframePlayer player;
    private readonly byte[] played = new byte[W * H * 4];
    private readonly byte[] keyframe = new byte[W * H * 4];
    private readonly byte[] thumb = new byte[ThumbW * ThumbH * 4];

    /// <summary>Builds the frame, three stroke layouts and the player.</summary>
    public KeyframePlaybackScene()
    {
        frame = CreateFrameSession();
        DrawingLayer captions = frame.AddLayer("Captions", Dim);
        frame.ActiveLayer = captions;
        Label(captions, frame, "KeyframePlayer.RenderFrame(t): dissolve from keyframe k to k + 1", Left, PanelY - 14, 14, Ink);
        Label(captions, frame, "keyframe k", 600, PanelY - 14, 14, Ink);
        Label(captions, frame, "keyframe k + 1", 870, PanelY - 14, 14, Ink);
        Label(captions, frame, "One painting per second on three stroke layouts in turn; k = floor(t / interval), u = t / interval - k.", Left, 640, 14, Dim);
        Label(captions, frame, "The paint function runs on one background worker, never twice at once; seek with Left/Right in the live window - frames stay exact.", Left, 662, 14, Dim);
        readout = frame.AddLayer("Readout", Dim);

        for (int i = 0; i < maps.Length; i++)
        {
            using DrawingSession strokes = CreateSession(W, H, Color.Transparent);
            DrawingLayer layer = strokes.AddLayer("Strokes", Color.White);
            foreach (BrushStrokeShape stroke in BrushworkKit.Layout(W, H, 40f + (6 * i), 100 + i)) layer.AddShape(stroke);
            maps[i] = strokes.ExportOwnershipMap(layer, new Size(W, H)).FillUnowned();
        }

        player = new KeyframePlayer(W, H, 1.0, Paint, null, 6);
    }

    /// <inheritdoc />
    public override string Name => "keyframe-playback";

    /// <inheritdoc />
    public override string Title => "Keyframe playback: paint every second, dissolve between";

    /// <inheritdoc />
    public override string Description => "KeyframePlayer: background painting ahead, a bounded cache, exact frames after seeks, any DrawingTransition";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        player.RenderFrame(seconds, played);
        int k = player.KeyframeAt(seconds);
        double u = (seconds / player.Interval) - k;

        readout.Clear();
        Label(readout, frame, string.Format(CultureInfo.InvariantCulture, "t = {0:0.00} s    keyframe {1} -> {2}    u = {3:0.00}", seconds, k, k + 1, u),
            Left, PanelY + H + 32, 17, Ink);
        Label(readout, frame, string.Format(CultureInfo.InvariantCulture, "paint calls so far: {0}    keyframes cached: {1} (of {2})",
            player.PaintCount, player.CachedKeyframeCount, player.CacheCapacity), Left, PanelY + H + 58, 14, Dim);

        ExportFrame(frame, rgba);
        FrameCompositor.Copy(played, W, H, rgba, Left, PanelY);
        Thumbnail(k * player.Interval, rgba, 600, PanelY);
        Thumbnail((k + 1) * player.Interval, rgba, 870, PanelY);
        DrawStrip(rgba, seconds);
    }

    // A keyframe, read through the player (it is cached, so this is a copy) and shrunk.
    private void Thumbnail(double seconds, Span<byte> rgba, int x, int y)
    {
        player.RenderFrame(seconds, keyframe);
        DrawingPlaneImage.FromRgba(keyframe, W, H).Resized(ThumbW, ThumbH).ToRgba(thumb);
        FrameCompositor.Copy(thumb, ThumbW, ThumbH, rgba, x, y);
    }

    // A strip under the thumbnails: one tick per keyframe, the play head in the accent colour.
    private static void DrawStrip(Span<byte> rgba, double seconds)
    {
        const int StripLeft = 600, StripTop = 380, StripWidth = 510, StripHeight = 24;
        for (int y = StripTop; y < StripTop + StripHeight; y++)
        {
            for (int x = StripLeft; x < StripLeft + StripWidth; x++)
            {
                double at = (x - StripLeft) * 8.0 / StripWidth;
                bool tick = Math.Abs(at - Math.Round(at)) < 0.02;
                bool head = Math.Abs(at - seconds) < 0.025;
                int o = ((y * DemoFrame.Width) + x) * 4;
                (byte r, byte g, byte b) = head ? ((byte)255, (byte)176, (byte)72) : tick ? ((byte)200, (byte)204, (byte)220) : ((byte)44, (byte)48, (byte)66);
                rgba[o] = r;
                rgba[o + 1] = g;
                rgba[o + 2] = b;
                rgba[o + 3] = 255;
            }
        }
    }

    // One keyframe: a sky that changes with time, picked up by the keyframe's stroke layout.
    private void Paint(int keyframe, double seconds, Span<byte> rgba)
    {
        DrawingOwnershipMap map = maps[((keyframe % maps.Length) + maps.Length) % maps.Length];
        DrawingPlaneImage sky = new DrawingPlaneImage(W / 2, H / 2);
        double sunX = 40 + (25 * seconds), sunY = 50 + (8 * Math.Sin(seconds));
        sky.FillParallel((x, y) =>
        {
            double v = y / (double)(H / 2);
            double swirl = Math.Sin((x / 17.0) + (y / 23.0) + (seconds * 0.7));
            double d = Math.Sqrt(((x - sunX) * (x - sunX)) + ((y - sunY) * (y - sunY)));
            double sun = Math.Exp(-d / 22.0);
            return new Vector4((float)(0.1 + (0.25 * v) + (0.9 * sun)), (float)(0.18 + (0.12 * swirl) + (0.6 * sun)), (float)(0.55 - (0.25 * v) + (0.1 * swirl)), 1f);
        });
        DrawingPlaneImage painting = new DrawingPlaneImage(W, H);
        map.ApplyColors(painting, map.GetMeanColors(sky));
        painting.BlendColor(Color.FromRgb(12, 14, 34), map.CreateBoundaryPlane(0.7f).Multiply(0.9f).Clamp(0f, 1f));
        painting.ToRgba(rgba);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        player.Dispose();
        frame.Dispose();
    }
}
