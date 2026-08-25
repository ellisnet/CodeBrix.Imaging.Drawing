using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;
using CodeBrix.Imaging.Drawing.TestSupport;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Covers filter effects across output scales. A filter's parameters are SVG user units,
/// and the scale that converts them to device pixels is taken from the canvas transform
/// when the filtered layer opens - so the same document must produce the same picture at
/// any raster scale, not just the one the reference images were generated at.
/// </summary>
public class SvgFilterScaleTests
{
    private const string BlurSample = "filters-blur";

    /// <summary>Collects the image-filter descriptions of every layer a picture opens.</summary>
    private sealed class FilterDescriptionCollector : IDrawingCommandVisitor
    {
        public List<string> Descriptions { get; } = new List<string>();

        public void Visit(SaveLayerCommand command)
        {
            if (command.Paint?.ImageFilter is { } filter) { Descriptions.Add(filter.Description); }
        }

        public void Visit(DrawPictureCommand command) => command.Picture?.Accept(this);
    }

    private static byte[] Encode(DrawingBitmap bitmap)
    {
        using DrawingImage image = DrawingImage.FromBitmap(bitmap);
        using DrawingData data = image.Encode(DrawingEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void The_blur_fixture_renders_the_same_at_scale_one_and_scale_two()
    {
        //Arrange
        string referencePath = SvgTestAssets.GetReferencePngPath(BlurSample);
        Assert.SkipUnless(File.Exists(referencePath),
            $"No reference image for '{BlurSample}' - run the CodeBrix.Imaging.Drawing.ParityTests suite once to generate it.");
        byte[] referencePng = File.ReadAllBytes(referencePath);

        using var svg = new DrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        svg.Load(SvgTestAssets.GetSvgPath(BlurSample)).Should().BeTrue();

        //Act - the reference scale, and half of it brought back up to the same size
        using DrawingBitmap atReferenceScale = svg.RasterizeToBitmap(SvgTestAssets.RasterScale);
        using DrawingBitmap atUnitScale = svg.RasterizeToBitmap(1f);
        using var unitScaleEnlarged = new DrawingBitmap(atReferenceScale.Info);
        atUnitScale.ScalePixels(unitScaleEnlarged, new DrawingSamplingOptions(DrawingFilterMode.Linear))
            .Should().BeTrue();

        //Assert - both scales agree with the Skia reference; the enlarged half-scale render
        //  carries the softness of its own resample, so it gets the wider margin
        (double maxMean, double maxFraction) = SvgTestAssets.GetTolerance(BlurSample);
        ImageComparison.AssertSimilar(referencePng, Encode(atReferenceScale),
            $"SVG sample '{BlurSample}' at scale {SvgTestAssets.RasterScale}", maxMean, maxFraction);
        ImageComparison.AssertSimilar(referencePng, Encode(unitScaleEnlarged),
            $"SVG sample '{BlurSample}' at scale 1, enlarged to the reference size",
            maxMean * 2.0, maxFraction * 2.0);
    }

    [Fact]
    public void An_image_filter_describes_the_primitive_chain_it_was_built_from()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.Load(SvgTestAssets.GetSvgPath(BlurSample)).Should().BeTrue();
        var collector = new FilterDescriptionCollector();

        //Act
        svg.Picture.Accept(collector);

        //Assert - the fixture's two filters; the compiler wraps each filter's source in a
        //  picture and a color filter, so those bracket every chain
        collector.Descriptions.Should().Contain("colorFilter <- blur(6, 6) <- colorFilter <- picture");
        collector.Descriptions.Should().Contain(
            "colorFilter <- merge(offset(8, 8) <- blur(4, 4) <- colorFilter <- colorFilter <- picture, "
            + "colorFilter <- picture)");
    }

    [Fact]
    public void A_null_filter_graph_builds_no_filter()
    {
        //Arrange
        var factory = new NoSkiaImageFilterFactory();

        //Act
        DrawingImageFilter filter = factory.Create(null);

        //Assert
        filter.Should().BeNull();
    }
}
