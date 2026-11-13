using System;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.Imaging.Processing;

namespace AutumnAviana.Rendering;

/// <summary>Saves raw frames as PNG files, and lays several frames out as a contact sheet, through CodeBrix.Imaging.</summary>
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

    /// <summary>Copies part of a frame into a new, tightly packed RGBA buffer.</summary>
    /// <param name="rgba">The frame.</param>
    /// <param name="width">The frame's width.</param>
    /// <param name="area">The part to copy (inside the frame).</param>
    /// <returns>The part's pixels.</returns>
    public static byte[] Crop(ReadOnlySpan<byte> rgba, int width, Rectangle area)
    {
        byte[] part = new byte[area.Width * area.Height * 4];
        for (int y = 0; y < area.Height; y++)
        {
            rgba.Slice((((area.Y + y) * width) + area.X) * 4, area.Width * 4).CopyTo(part.AsSpan(y * area.Width * 4));
        }

        return part;
    }

    /// <summary>Saves part of a frame.</summary>
    /// <param name="rgba">The pixels (R, G, B, A, straight alpha).</param>
    /// <param name="width">The frame's width.</param>
    /// <param name="height">The frame's height.</param>
    /// <param name="area">The part to keep.</param>
    /// <param name="path">The file to write.</param>
    public static void SaveRgbaCrop(ReadOnlySpan<byte> rgba, int width, int height, Rectangle area, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgba, width, height, PngFormat.Instance);
        image.Mutate(c => c.Crop(area));
        image.SaveAsPng(path);
    }

    /// <summary>Lays frames out in a grid of thumbnails (left to right, top to bottom) and saves it.</summary>
    /// <param name="frames">The frames (each width x height x 4 bytes).</param>
    /// <param name="width">The frames' width.</param>
    /// <param name="height">The frames' height.</param>
    /// <param name="columns">Thumbnails per row.</param>
    /// <param name="thumbWidth">A thumbnail's width (its height keeps the frame's aspect).</param>
    /// <param name="path">The file to write.</param>
    public static void SaveSheet(byte[][] frames, int width, int height, int columns, int thumbWidth, string path)
    {
        const int gap = 6;
        int thumbHeight = (int)Math.Round(thumbWidth * height / (double)width);
        int rows = (frames.Length + columns - 1) / columns;
        using Image<Rgba32> sheet = new Image<Rgba32>((columns * (thumbWidth + gap)) + gap, (rows * (thumbHeight + gap)) + gap, new Rgba32(60, 60, 64, 255));
        for (int i = 0; i < frames.Length; i++)
        {
            using Image<Rgba32> frame = Image.LoadPixelData<Rgba32>(frames[i], width, height, PngFormat.Instance);
            frame.Mutate(c => c.Resize(thumbWidth, thumbHeight));
            int x = gap + ((i % columns) * (thumbWidth + gap));
            int y = gap + ((i / columns) * (thumbHeight + gap));
            sheet.Mutate(c => c.DrawImage(frame, new Point(x, y), 1f));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        sheet.SaveAsPng(path);
    }
}
