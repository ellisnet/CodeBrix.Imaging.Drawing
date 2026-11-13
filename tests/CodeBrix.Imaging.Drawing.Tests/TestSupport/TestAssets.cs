using System;
using System.IO;
using System.Runtime.CompilerServices;
using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.Tests.TestSupport;

/// <summary>
/// Locates the repository's committed test assets (the OFL Open Sans and Roboto fonts under
/// tests/SvgAssets/Fonts) from this source file's own location, so the same code works in
/// the Skia test project and in the NoSkia test project that links it - and reads pixels
/// out of raw RGBA frames.
/// </summary>
internal static class TestAssets
{
    /// <summary>The committed Open Sans Regular font (SIL OFL 1.1).</summary>
    public static string OpenSansPath { get; } = Path.Combine(GetTestsDirectory(), "SvgAssets", "Fonts", "OpenSans-Regular.ttf");

    /// <summary>
    /// The committed Roboto Regular font (SIL OFL 1.1) - the kerning test font: unlike Open Sans it
    /// carries kerning data (GPOS kern pairs such as "To" and "AV") and fi/ffi ligatures.
    /// </summary>
    public static string RobotoPath { get; } = Path.Combine(GetTestsDirectory(), "SvgAssets", "Fonts", "Roboto-Regular.ttf");

    /// <summary>Reads one pixel of a tightly packed RGBA frame.</summary>
    /// <param name="rgba">The frame's bytes.</param>
    /// <param name="width">The frame's width.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row.</param>
    /// <returns>The pixel.</returns>
    public static Rgba32 PixelAt(byte[] rgba, int width, int x, int y)
    {
        int i = ((y * width) + x) * 4;
        return new Rgba32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
    }

    /// <summary>Counts the pixels of a frame whose alpha is above zero.</summary>
    /// <param name="rgba">The frame's bytes.</param>
    /// <returns>The count.</returns>
    public static int CountInked(byte[] rgba)
    {
        int count = 0;
        for (int i = 3; i < rgba.Length; i += 4)
        {
            if (rgba[i] > 0) { count++; }
        }
        return count;
    }

    private static string GetTestsDirectory([CallerFilePath] string thisFilePath = null)
    {
        //This file sits in tests/CodeBrix.Imaging.Drawing.Tests/TestSupport
        DirectoryInfo folder = new FileInfo(thisFilePath ?? String.Empty).Directory;
        while (folder != null && !Directory.Exists(Path.Combine(folder.FullName, "SvgAssets")))
        {
            folder = folder.Parent;
        }
        return folder?.FullName ?? String.Empty;
    }
}
