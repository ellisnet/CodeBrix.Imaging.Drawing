using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using CodeBrix.Imaging.Drawing.TestSupport;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Renders every sample SVG through the fully managed NoSkia renderer and compares the
/// output against the checked-in reference images that the SkiaSharp-based stack
/// generated (tests/SvgAssets/References). These tests need no Skia native library, so
/// they verify - on any machine - that the NoSkia renderer still produces what the Skia
/// stack produced. A missing reference image skips its test (run the ParityTests suite
/// once to generate references).
/// </summary>
public class SvgReferenceTests
{
    /// <summary>The sample names, as xUnit theory data.</summary>
    public static IEnumerable<object[]> SampleNames()
    {
        foreach (string name in SvgTestAssets.GetSampleNames())
        {
            yield return new object[] { name };
        }
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void NoSkia_output_matches_reference_image(string sampleName)
    {
        //Arrange
        string referencePath = SvgTestAssets.GetReferencePngPath(sampleName);
        Assert.SkipUnless(File.Exists(referencePath),
            $"No reference image for '{sampleName}' - run the CodeBrix.Imaging.Drawing.ParityTests suite once to generate it.");
        byte[] referencePng = File.ReadAllBytes(referencePath);

        //Act
        using var svg = new DrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        svg.Load(SvgTestAssets.GetSvgPath(sampleName)).Should().BeTrue();
        byte[] noSkiaPng = svg.RasterizeToPng(SvgTestAssets.RasterScale);

        //Assert
        (double maxMean, double maxFraction) = SvgTestAssets.GetTolerance(sampleName);
        ImageComparison.AssertSimilar(referencePng, noSkiaPng, $"SVG sample '{sampleName}' (vs reference)",
            maxMean, maxFraction);
    }

    [Fact]
    public void Svg_bounds_and_size_are_exposed()
    {
        //Arrange
        using var svg = new DrawingSvg();

        //Act
        svg.Load(SvgTestAssets.GetSvgPath("basic-shapes"));

        //Assert
        svg.IsLoaded.Should().BeTrue();
        svg.Bounds.Width.Should().Be(400f);
        svg.Bounds.Height.Should().Be(300f);
    }

    [Fact]
    public void Unloadable_markup_does_not_load()
    {
        //Arrange
        using var svg = new DrawingSvg();

        //Act + Assert - not-actually-SVG markup compiles to no picture
        svg.FromSvg("<not-svg/>").Should().BeFalse();
        svg.IsLoaded.Should().BeFalse();
        svg.Picture.Should().BeNull();
    }
}
