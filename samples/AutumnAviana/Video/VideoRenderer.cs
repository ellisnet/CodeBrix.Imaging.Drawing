using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutumnAviana.Animation;
using AutumnAviana.Scene;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Pipes;

namespace AutumnAviana.Video;

/// <summary>The three videos the sample makes.</summary>
internal enum VideoKind
{
    /// <summary>On the white background, as the animation is shown: H.264 MP4 (CRF 17, yuv420p, fast start).</summary>
    WhiteMp4,

    /// <summary>On a transparent background: ProRes 4444 with alpha in a QuickTime .mov (yuva444p10le).</summary>
    AlphaProRes,

    /// <summary>On a transparent background: VP9 with alpha in a WebM (yuva420p, CRF 20).</summary>
    AlphaWebM,
}

/// <summary>
/// Makes a video of the whole timeline at 1920x1080 and 30 fps: every frame (frame i shows animation time i / 30 s)
/// is exported by a drawing session as raw RGBA bytes - straight alpha - and piped straight into ffmpeg by
/// CodeBrix.VideoProcessing, with no image files in between. The frames are drawn in parallel by the library's
/// <see cref="FrameSequenceRenderer" /> (several scenes, one per worker) and handed over in frame order.
/// <para>
/// The transparent videos export the scene WITHOUT its background (the white is only the session's background
/// fill), so everything else keeps its own alpha: the leaves and letters are opaque, the soft ground shadows under
/// the piles, the letters and the raccoons stay semi-transparent dark. ffmpeg is told the input is "rgba" (straight
/// alpha) and nothing is premultiplied.
/// </para>
/// </summary>
internal static class VideoRenderer
{
    /// <summary>How many frames may wait, drawn, for ffmpeg (each 1920 x 1080 x 4 bytes).</summary>
    private const int Queue = 8;

    /// <summary>The file name ending for a kind of video (after the name, the stamp and the size).</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example "-white.mp4".</returns>
    public static string Suffix(VideoKind kind) => kind switch
    {
        VideoKind.WhiteMp4 => "-white.mp4",
        VideoKind.AlphaProRes => "-alpha.mov",
        _ => "-alpha.webm",
    };

    /// <summary>Renders every frame of the animation into a video file (overwritten).</summary>
    /// <param name="kind">Which video.</param>
    /// <param name="output">The file to write.</param>
    /// <param name="workers">How many scenes draw frames at once.</param>
    public static void Render(VideoKind kind, string output, int workers)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        bool transparent = kind != VideoKind.WhiteMp4;
        RawVideoPipeSource source = new RawVideoPipeSource(Frames(transparent, workers)) { FrameRate = Timeline.Fps };
        bool ok = FFMpegArguments
            .FromPipeInput(source)
            .OutputToFile(output, true, options =>
            {
                switch (kind)
                {
                    case VideoKind.WhiteMp4:
                        options.WithVideoCodec(VideoCodec.LibX264)
                            .WithConstantRateFactor(17)
                            .WithSpeedPreset(Speed.Slow)
                            .ForcePixelFormat("yuv420p")
                            .WithFastStart();
                        break;
                    case VideoKind.AlphaProRes:
                        // ProRes 4444 (profile 4) keeps a full alpha plane next to 10-bit 4:4:4 colour.
                        options.WithVideoCodec("prores_ks")
                            .WithCustomArgument("-profile:v 4 -vendor apl0")
                            .ForcePixelFormat("yuva444p10le");
                        break;
                    default:
                        // VP9 carries alpha in WebM (as a second, alpha-only stream inside the frames); constant
                        // quality, no bitrate cap.
                        options.WithVideoCodec("libvpx-vp9")
                            .WithConstantRateFactor(20)
                            .WithCustomArgument("-b:v 0 -row-mt 1 -cpu-used 2")
                            .ForcePixelFormat("yuva420p");
                        break;
                }

                options.WithFramerate(Timeline.Fps);
            })
            .ProcessSynchronously();
        if (!ok) throw new InvalidOperationException("ffmpeg did not finish the video " + output + ".");
    }

    /// <summary>
    /// The frames in order. The library's frame-sequence renderer draws them on worker scenes and hands each over,
    /// in frame order, on a producer thread; they wait (a few at a time) to be written down ffmpeg's pipe.
    /// </summary>
    private static IEnumerable<IVideoFrame> Frames(bool transparent, int workers)
    {
        Size size = Stage.FrameSize;
        FrameSequenceRenderer renderer = new FrameSequenceRenderer(Timeline.Clock, () => new AutumnScene().Session, size)
        {
            MaxDegreeOfParallelism = workers,
            IncludeBackground = !transparent,
        };

        int bytes = renderer.FrameBytes;
        using BlockingCollection<byte[]> ready = new BlockingCollection<byte[]>(Queue);
        ConcurrentBag<byte[]> spare = new ConcurrentBag<byte[]>();
        using CancellationTokenSource stop = new CancellationTokenSource();
        Task producer = Task.Run(() =>
        {
            try
            {
                renderer.Render(0, Timeline.FrameCount - 1, (frame, seconds, rgba) =>
                {
                    if (!spare.TryTake(out byte[] buffer)) buffer = new byte[bytes];
                    rgba.CopyTo(buffer);
                    ready.Add(buffer, stop.Token);
                }, stop.Token);
            }
            finally
            {
                ready.CompleteAdding();
            }
        });

        RawFrame video = new RawFrame(size.Width, size.Height);
        Stopwatch clock = Stopwatch.StartNew();
        int written = 0;
        try
        {
            foreach (byte[] pixels in ready.GetConsumingEnumerable())
            {
                video.Pixels = pixels;
                yield return video;
                spare.Add(pixels);
                written++;
                if (written % 150 == 0 || written == Timeline.FrameCount)
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  frame {0}/{1}  ({2:0.0} s)", written, Timeline.FrameCount, clock.Elapsed.TotalSeconds));
                }
            }
        }
        finally
        {
            // If ffmpeg stops reading early, stop drawing too; any failure of the drawing itself surfaces here.
            stop.Cancel();
            try
            {
                producer.Wait();
            }
            catch (AggregateException e) when (stop.IsCancellationRequested && e.InnerException is OperationCanceledException)
            {
            }
        }
    }

    /// <summary>One raw frame for ffmpeg: R, G, B, A bytes (straight alpha), rows top to bottom.</summary>
    private sealed class RawFrame : IVideoFrame
    {
        public RawFrame(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public byte[] Pixels { get; set; }

        public int Width { get; }

        public int Height { get; }

        public string Format => "rgba";

        public void Serialize(Stream pipe) => pipe.Write(Pixels, 0, Pixels.Length);

        public Task SerializeAsync(Stream pipe, CancellationToken token) => pipe.WriteAsync(Pixels, 0, Pixels.Length, token);
    }
}
