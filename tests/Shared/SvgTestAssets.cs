using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodeBrix.Imaging.Drawing.TestSupport;

/// <summary>
/// Locates the repository's shared SVG test assets (tests/SvgAssets): the sample SVG
/// documents, the deterministic test font, and the checked-in reference images that the
/// SkiaSharp-based stack generated. Paths resolve from this source file's location, so
/// reference images written by the generator land in the repository (ready to commit)
/// rather than in a bin folder.
/// </summary>
internal static class SvgTestAssets
{
    /// <summary>The absolute path of the tests/SvgAssets folder.</summary>
    public static string AssetsDirectory { get; } = Path.Combine(GetTestsDirectory(), "SvgAssets");

    /// <summary>The absolute path of the reference-image folder (tests/SvgAssets/References).</summary>
    public static string ReferencesDirectory { get; } = Path.Combine(AssetsDirectory, "References");

    /// <summary>The absolute path of the deterministic test font (Open Sans Regular).</summary>
    public static string TestFontPath { get; } = Path.Combine(AssetsDirectory, "Fonts", "OpenSans-Regular.ttf");

    /// <summary>
    /// Enumerates the sample SVG file names (without extension), sorted for stable test ordering.
    /// </summary>
    /// <returns>The sample names.</returns>
    public static IReadOnlyList<string> GetSampleNames()
    {
        var names = new List<string>();
        foreach (string file in Directory.GetFiles(AssetsDirectory, "*.svg"))
        {
            names.Add(Path.GetFileNameWithoutExtension(file));
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Gets the absolute path of a sample SVG file.
    /// </summary>
    /// <param name="sampleName">The sample name (file name without extension).</param>
    /// <returns>The SVG file path.</returns>
    public static string GetSvgPath(string sampleName) => Path.Combine(AssetsDirectory, sampleName + ".svg");

    /// <summary>
    /// Gets the absolute path of a sample's reference PNG (which may not exist yet).
    /// </summary>
    /// <param name="sampleName">The sample name (file name without extension).</param>
    /// <returns>The reference PNG path.</returns>
    public static string GetReferencePngPath(string sampleName)
        => Path.Combine(ReferencesDirectory, sampleName + ".png");

    /// <summary>
    /// The raster scale (device pixels per SVG user unit) used for every reference image
    /// and comparison - matching the Html2Pdf default SvgRasterScale of 2.0.
    /// </summary>
    public const float RasterScale = 2.0f;

    /// <summary>
    /// The similarity tolerances for one sample: two independent rasterizers differ most
    /// on anti-aliased edges, resampled gradients, blur kernels, and glyph outlines, so
    /// samples heavy in those features allow a wider (but still small) margin.
    /// </summary>
    /// <param name="sampleName">The sample name.</param>
    /// <returns>The maximum mean channel delta and maximum fraction of noticeably different pixels.</returns>
    public static (double MaxMeanDelta, double MaxFractionNoticeable) GetTolerance(string sampleName)
        => sampleName switch
        {
            "text-basic" => (4.0, 0.08),
            "filters-blur" => (5.0, 0.10),
            "opacity-masking" => (3.0, 0.05),
            "gradients" => (2.5, 0.05),
            _ => (2.0, 0.03),
        };

    private static string GetTestsDirectory([CallerFilePath] string thisFilePath = null)
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFilePath));
}
