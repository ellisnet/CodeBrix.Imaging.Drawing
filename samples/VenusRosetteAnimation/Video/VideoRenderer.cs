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
using VenusRosetteAnimation.Animation;

namespace VenusRosetteAnimation.Video;

/// <summary>
/// Makes a video: every frame (frame i shows animation time i / 30 s) is written by the drawing session as raw
/// RGBA bytes and piped straight into ffmpeg by CodeBrix.VideoProcessing - no image files in between - which
/// encodes a silent H.264 MP4 (CRF 16, yuv420p, 30 fps, fast start). Videos never carry the safe-area overlay.
/// </summary>
internal static class VideoRenderer
{
    /// <summary>Renders all <see cref="Timeline.FrameCount" /> frames of a scene to an MP4.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="output">The MP4 to write (overwritten).</param>
    public static void Render(RosetteScene scene, string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        RawVideoPipeSource source = new RawVideoPipeSource(Frames(scene)) { FrameRate = Timeline.Fps };
        bool ok = FFMpegArguments
            .FromPipeInput(source)
            .OutputToFile(output, true, options => options
                .WithVideoCodec(VideoCodec.LibX264)
                .WithConstantRateFactor(16)
                .WithSpeedPreset(Speed.Slow)
                .ForcePixelFormat("yuv420p")
                .WithFramerate(Timeline.Fps)
                .WithFastStart())
            .ProcessSynchronously();
        if (!ok) throw new InvalidOperationException("ffmpeg did not finish the video.");
    }

    private static IEnumerable<IVideoFrame> Frames(RosetteScene scene)
    {
        // One buffer serves every frame: each is written down the pipe before the next is drawn.
        RawFrame frame = new RawFrame(scene.Layout.Width, scene.Layout.Height);
        Stopwatch clock = Stopwatch.StartNew();
        for (int i = 0; i < Timeline.FrameCount; i++)
        {
            scene.Show(Timeline.Clock.TimeOfFrame(i));
            scene.WriteFrame(frame.Pixels);
            if ((i + 1) % 30 == 0 || i == Timeline.FrameCount - 1)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  frame {0}/{1}  ({2:0.0} s)", i + 1, Timeline.FrameCount, clock.Elapsed.TotalSeconds));
            }

            yield return frame;
        }
    }

    private sealed class RawFrame : IVideoFrame
    {
        public RawFrame(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];
        }

        public byte[] Pixels { get; }

        public int Width { get; }

        public int Height { get; }

        public string Format => "rgba";

        public void Serialize(Stream pipe) => pipe.Write(Pixels, 0, Pixels.Length);

        public Task SerializeAsync(Stream pipe, CancellationToken token) => pipe.WriteAsync(Pixels, 0, Pixels.Length, token);
    }
}
