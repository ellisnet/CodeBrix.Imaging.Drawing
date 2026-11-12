using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Imaging;
using CodeBrix.VideoProcessing.Pipes;
using VenusRosetteAnimation.Animation;

namespace VenusRosetteAnimation.Video;

/// <summary>
/// Makes a video in two steps: every frame is exported by the drawing session as a full-size PNG (frame i shows
/// animation time i / 30 s), then CodeBrix.VideoProcessing pipes the PNGs into ffmpeg and encodes a silent H.264
/// MP4 (CRF 16, yuv420p, 30 fps, fast start).
/// </summary>
internal static class VideoRenderer
{
    /// <summary>Writes all <see cref="Timeline.FrameCount" /> frames as PNGs into a folder (emptied of old frames first).</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="folder">The frames folder.</param>
    /// <returns>The frame files, in order.</returns>
    public static IReadOnlyList<string> WriteFrames(RosetteScene scene, string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (string old in Directory.GetFiles(folder, "frame-*.png")) File.Delete(old);

        Stopwatch clock = Stopwatch.StartNew();
        List<string> files = new List<string>();
        for (int i = 0; i < Timeline.FrameCount; i++)
        {
            scene.Show(i / (double)Timeline.Fps);
            string path = Path.Combine(folder, "frame-" + i.ToString("0000", CultureInfo.InvariantCulture) + ".png");
            scene.SavePng(path);
            files.Add(path);
            if ((i + 1) % 30 == 0 || i == Timeline.FrameCount - 1)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  frame {0}/{1}  ({2:0.0} s)", i + 1, Timeline.FrameCount, clock.Elapsed.TotalSeconds));
            }
        }

        return files;
    }

    /// <summary>Encodes the frames into an MP4.</summary>
    /// <param name="frames">The PNG frames, in order (all the same size).</param>
    /// <param name="output">The MP4 to write (overwritten).</param>
    public static void Assemble(IReadOnlyList<string> frames, string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        RawVideoPipeSource source = new RawVideoPipeSource(Load(frames)) { FrameRate = Timeline.Fps };
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

    private static IEnumerable<IVideoFrame> Load(IReadOnlyList<string> frames)
    {
        foreach (string path in frames)
        {
            // The wrapper disposes the image once its frame has gone down the pipe.
            using ImageVideoFrameWrapper frame = new ImageVideoFrameWrapper(Image.Load<Rgba32>(path));
            yield return frame;
        }
    }
}
