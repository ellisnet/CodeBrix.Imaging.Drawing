using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Pipes;
using VanGoghPaintingDemo.Painting;

namespace VanGoghPaintingDemo.Video;

/// <summary>
/// Makes a video: every frame is produced by a painter and piped, as raw pixels, straight into ffmpeg by
/// CodeBrix.VideoProcessing, which encodes a silent H.264 MP4 (CRF 18, yuv420p, 30 fps, fast start).
/// </summary>
internal static class VideoRenderer
{
    /// <summary>Renders a stretch of the animation to an MP4.</summary>
    /// <param name="painter">The painter that produces the frames.</param>
    /// <param name="fromSeconds">Where to start.</param>
    /// <param name="toSeconds">Where to stop.</param>
    /// <param name="output">The MP4 to write (overwritten).</param>
    public static void Render(IFramePainter painter, double fromSeconds, double toSeconds, string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        int first = (int)Math.Round(fromSeconds * Timeline.Fps);
        int last = (int)Math.Round(toSeconds * Timeline.Fps);
        RawVideoPipeSource source = new RawVideoPipeSource(Frames(painter, first, last)) { FrameRate = Timeline.Fps };
        bool ok = FFMpegArguments
            .FromPipeInput(source)
            .OutputToFile(output, true, options => options
                .WithVideoCodec(VideoCodec.LibX264)
                .WithConstantRateFactor(18)
                .WithSpeedPreset(Speed.Medium)
                .ForcePixelFormat("yuv420p")
                .WithFramerate(Timeline.Fps)
                .WithFastStart())
            .ProcessSynchronously();
        if (!ok) throw new InvalidOperationException("ffmpeg did not finish the video.");
    }

    private static IEnumerable<IVideoFrame> Frames(IFramePainter painter, int first, int last)
    {
        // One buffer serves every frame: each is written down the pipe before the next is painted.
        RawFrame frame = new RawFrame();
        Stopwatch clock = Stopwatch.StartNew();
        for (int i = first; i < last; i++)
        {
            painter.RenderFrame(Timeline.Clock.TimeOfFrame(i), frame.Pixels);
            int done = i - first + 1;
            if (done % 150 == 0 || i == last - 1)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  frame {0}/{1}  ({2:0.0} s)", done, last - first, clock.Elapsed.TotalSeconds));
            }

            yield return frame;
        }
    }

    private sealed class RawFrame : IVideoFrame
    {
        public byte[] Pixels { get; } = new byte[Timeline.Width * Timeline.Height * 4];

        public int Width => Timeline.Width;

        public int Height => Timeline.Height;

        public string Format => "rgba";

        public void Serialize(Stream pipe) => pipe.Write(Pixels, 0, Pixels.Length);

        public Task SerializeAsync(Stream pipe, CancellationToken token) => pipe.WriteAsync(Pixels, 0, Pixels.Length, token);
    }
}
