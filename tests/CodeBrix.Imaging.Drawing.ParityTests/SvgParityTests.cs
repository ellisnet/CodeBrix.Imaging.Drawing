extern alias noskia;

using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.SkiaSvg;
using CodeBrix.SkiaSvg.TypefaceProviders;
using SkiaSharp;
using Xunit;
using NoSkiaDrawingSvg = CodeBrix.Imaging.Drawing.NoSkia.Svg.DrawingSvg;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Renders every sample SVG through BOTH stacks - the SkiaSharp-based CodeBrix.SkiaSvg
/// renderer and the fully managed NoSkia renderer - and asserts near-identical output.
/// This suite also GENERATES the reference images (tests/SvgAssets/References/*.png) from
/// the SkiaSharp stack whenever one is missing (or REGENERATE_SVG_REFERENCES=1), so the
/// references can be committed and the NoSkia-only test suite can verify against them on
/// machines without any Skia native library.
/// </summary>
public class SvgParityTests
{
    /// <summary>
    /// A typeface provider that serves the repository's deterministic test font (Open Sans
    /// Regular) for every requested family, so reference rendering never consults system
    /// fonts.
    /// </summary>
    private sealed class TestFontTypefaceProvider : ITypefaceProvider
    {
        private static readonly SKTypeface Typeface = SKTypeface.FromFile(SvgTestAssets.TestFontPath);

        public SKTypeface FromFamilyName(string fontFamily,
            SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontSlant)
            => Typeface;
    }

    /// <summary>The sample names, as xUnit theory data.</summary>
    public static IEnumerable<object[]> SampleNames()
    {
        foreach (string name in SvgTestAssets.GetSampleNames())
        {
            yield return new object[] { name };
        }
    }

    private static byte[] RasterizeWithSkia(string svgPath)
    {
        //The same rasterization recipe CodeBrix.PdfDocuments' SvgImageRasterizer uses
        using var svg = new SKSvg();
        svg.Settings.TypefaceProviders.Clear();
        svg.Settings.TypefaceProviders.Add(new TestFontTypefaceProvider());

        using FileStream stream = File.OpenRead(svgPath);
        svg.Load(stream);
        SKPicture picture = svg.Picture;
        Assert.True(picture != null, $"The SkiaSharp stack could not load {Path.GetFileName(svgPath)}");

        SKRect bounds = picture.CullRect;
        int width = Math.Max(1, (int)Math.Ceiling(bounds.Width * SvgTestAssets.RasterScale));
        int height = Math.Max(1, (int)Math.Ceiling(bounds.Height * SvgTestAssets.RasterScale));

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(info);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(SvgTestAssets.RasterScale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] RasterizeWithNoSkia(string svgPath)
    {
        using var svg = new NoSkiaDrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        Assert.True(svg.Load(svgPath),
            $"The NoSkia stack could not load {Path.GetFileName(svgPath)}");
        return svg.RasterizeToPng(SvgTestAssets.RasterScale);
    }

    [Fact]
    public void Reference_images_exist_for_every_sample()
    {
        //Arrange - regeneration is explicit, so committed references stay stable
        bool regenerate = Environment.GetEnvironmentVariable("REGENERATE_SVG_REFERENCES") == "1";
        Directory.CreateDirectory(SvgTestAssets.ReferencesDirectory);

        //Act - render any missing reference through the SkiaSharp stack and store it in
        //the repository, ready to be committed
        var generated = new List<string>();
        foreach (string sampleName in SvgTestAssets.GetSampleNames())
        {
            string referencePath = SvgTestAssets.GetReferencePngPath(sampleName);
            if (regenerate || !File.Exists(referencePath))
            {
                byte[] png = RasterizeWithSkia(SvgTestAssets.GetSvgPath(sampleName));
                File.WriteAllBytes(referencePath, png);
                generated.Add(sampleName);
            }
        }

        //Assert
        foreach (string sampleName in SvgTestAssets.GetSampleNames())
        {
            Assert.True(File.Exists(SvgTestAssets.GetReferencePngPath(sampleName)),
                $"No reference image for {sampleName}");
        }
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void NoSkia_output_matches_live_skia_output(string sampleName)
    {
        //Arrange
        string svgPath = SvgTestAssets.GetSvgPath(sampleName);

        //Act
        byte[] skiaPng = RasterizeWithSkia(svgPath);
        byte[] noSkiaPng = RasterizeWithNoSkia(svgPath);

        //Assert
        (double maxMean, double maxFraction) = SvgTestAssets.GetTolerance(sampleName);
        ImageComparison.AssertSimilar(skiaPng, noSkiaPng, $"SVG sample '{sampleName}' (live A/B)",
            maxMean, maxFraction);
    }
}
