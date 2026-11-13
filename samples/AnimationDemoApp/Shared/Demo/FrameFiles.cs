using System;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;

namespace AnimationDemoApp.Demo;

/// <summary>Saves raw frames as PNG files, through CodeBrix.Imaging.</summary>
internal static class FrameFiles
{
    /// <summary>Saves a four-bytes-per-pixel (R, G, B, A, straight alpha) frame.</summary>
    /// <param name="rgba">The pixels.</param>
    /// <param name="width">The frame's width.</param>
    /// <param name="height">The frame's height.</param>
    /// <param name="path">The file to write (its folder is created if needed).</param>
    public static void SaveRgba(ReadOnlySpan<byte> rgba, int width, int height, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgba, width, height, PngFormat.Instance);
        image.SaveAsPng(path);
    }
}
