using System;
using System.IO;
using System.Text;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.SvgParse;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// Covers the renderer facade itself: what a load reports, what it exposes afterwards, that
/// replaying onto a caller's canvas honors the transform already on it, and the lifetime
/// guarantee - a picture handed out keeps working, text included, after the renderer that
/// produced it has been disposed.
/// </summary>
public class DrawingSvgTests
{
    private const string TextMarkup =
@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""60"" viewBox=""0 0 200 60"">
  <rect x=""0"" y=""0"" width=""200"" height=""60"" fill=""#fefbf3""/>
  <text x=""10"" y=""40"" font-family=""Open Sans"" font-size=""24"" fill=""#204060"">Hello</text>
</svg>";

    private static DrawingBitmap Render(DrawingRect bounds, Action<DrawingCanvas> draw)
    {
        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            (int)MathF.Ceiling(bounds.Width), (int)MathF.Ceiling(bounds.Height),
            DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        using var canvas = new DrawingCanvas(bitmap);
        canvas.Translate(-bounds.Left, -bounds.Top);
        draw(canvas);
        return bitmap;
    }

    [Fact]
    public void A_new_renderer_has_loaded_nothing()
    {
        //Arrange + Act
        using var svg = new DrawingSvg();

        //Assert
        svg.IsLoaded.Should().BeFalse();
        svg.Picture.Should().BeNull();
        svg.Scene.Should().BeNull();
        svg.Bounds.Should().Be(DrawingRect.Empty);
        svg.DeclaredWidth.Should().Be(SvgUnit.None);
        svg.DeclaredHeight.Should().Be(SvgUnit.None);
        svg.Warnings.Should().BeEmpty();
        svg.TextEmission.Should().Be(DrawingSvgTextEmission.Runs);
    }

    [Fact]
    public void Loading_from_a_stream_a_file_and_markup_all_report_the_same_document()
    {
        //Arrange
        string path = SvgTestAssets.GetSvgPath("basic-shapes");
        string markup = File.ReadAllText(path);

        //Act
        using var fromPath = new DrawingSvg();
        using var fromStream = new DrawingSvg();
        using var fromMarkup = new DrawingSvg();
        using FileStream stream = File.OpenRead(path);

        //Assert
        fromPath.Load(path).Should().BeTrue();
        fromStream.Load(stream).Should().BeTrue();
        fromMarkup.FromSvg(markup).Should().BeTrue();
        fromStream.Bounds.Should().Be(fromPath.Bounds);
        fromMarkup.Bounds.Should().Be(fromPath.Bounds);
        fromPath.Scene.Should().NotBeNull();
    }

    [Fact]
    public void A_load_that_fails_leaves_the_renderer_empty()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.Load(SvgTestAssets.GetSvgPath("basic-shapes")).Should().BeTrue();

        //Act
        svg.FromSvg("<not-svg/>").Should().BeFalse();

        //Assert - the previous document is gone rather than silently still in place
        svg.IsLoaded.Should().BeFalse();
        svg.Picture.Should().BeNull();
        svg.Scene.Should().BeNull();
        svg.Bounds.Should().Be(DrawingRect.Empty);
    }

    [Fact]
    public void A_missing_argument_is_refused_rather_than_treated_as_an_empty_document()
    {
        //Arrange
        using var svg = new DrawingSvg();

        //Act + Assert
        Assert.Throws<ArgumentNullException>(() => svg.Load((Stream)null));
        Assert.Throws<ArgumentException>(() => svg.Load((string)null));
        Assert.Throws<ArgumentException>(() => svg.FromSvg("   "));
        Assert.Throws<InvalidOperationException>(() => svg.Render(
            new DrawingCanvas(new DrawingBitmap(new DrawingImageInfo(
                4, 4, DrawingColorType.Rgba8888, DrawingAlphaType.Premul)))));
        Assert.Throws<InvalidOperationException>(() => svg.RasterizeToBitmap());
    }

    [Fact]
    public void Rendering_honors_the_transform_the_canvas_already_carries()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        svg.FromSvg(TextMarkup).Should().BeTrue();

        //Act - the same document at scale 2, once through the renderer's own rasterizer and
        //  once by replaying onto a canvas that was already scaled
        using DrawingBitmap rasterized = svg.RasterizeToBitmap(2f);
        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            rasterized.Width, rasterized.Height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        using (var canvas = new DrawingCanvas(bitmap))
        {
            canvas.Scale(2f);
            svg.Render(canvas);
        }

        //Assert
        using (bitmap)
        {
            bitmap.Bytes.Should().Equal(rasterized.Bytes);
        }
        Assert.Throws<ArgumentNullException>(() => svg.Render(null));
    }

    [Fact]
    public void A_picture_keeps_drawing_its_text_after_the_renderer_is_disposed()
    {
        //Arrange
        var svg = new DrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        svg.FromSvg(TextMarkup).Should().BeTrue();
        DrawingPicture picture = svg.Picture;
        DrawingSvgScene scene = svg.Scene;
        DrawingRect bounds = svg.Bounds;
        using DrawingBitmap before = Render(bounds, canvas => canvas.DrawPicture(picture));

        //Act
        svg.Dispose();
        using DrawingBitmap after = Render(bounds, canvas => canvas.DrawPicture(picture));

        //Assert - the outliner the text commands carry holds the font registry itself, so
        //  disposal takes nothing away from a picture the caller is already holding
        after.Bytes.Should().Equal(before.Bytes);
        scene.Root.Should().NotBeNull();
        scene.Traverse().Should().NotBeEmpty();
        svg.Picture.Should().BeSameAs(picture);
    }

    [Fact]
    public void A_reload_replaces_the_picture_and_the_scene()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.Load(SvgTestAssets.GetSvgPath("basic-shapes")).Should().BeTrue();
        DrawingPicture first = svg.Picture;
        DrawingSvgScene firstScene = svg.Scene;

        //Act
        svg.Load(SvgTestAssets.GetSvgPath("paths-curves")).Should().BeTrue();

        //Assert
        svg.Picture.Should().NotBeSameAs(first);
        svg.Scene.Should().NotBeSameAs(firstScene);
    }

    [Fact]
    public void Rasterizing_needs_a_positive_scale()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.Load(SvgTestAssets.GetSvgPath("basic-shapes")).Should().BeTrue();

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => svg.RasterizeToBitmap(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => svg.RasterizeToBitmap(-1f));
    }

    [Fact]
    public void A_background_color_is_painted_behind_the_document()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.FromSvg(@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""20"" height=""20"" viewBox=""0 0 20 20"">
  <rect x=""5"" y=""5"" width=""10"" height=""10"" fill=""#ff0000""/>
</svg>").Should().BeTrue();

        //Act
        using DrawingBitmap bitmap = svg.RasterizeToBitmap(1f, DrawingColors.White);

        //Assert
        bitmap.GetPixel(0, 0).Should().Be(DrawingColors.White);
        bitmap.GetPixel(10, 10).Should().Be(new DrawingColor(255, 0, 0, 255));
    }

    [Fact]
    public void Png_bytes_are_a_png()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.Load(SvgTestAssets.GetSvgPath("basic-shapes")).Should().BeTrue();

        //Act
        byte[] png = svg.RasterizeToPng(1f);

        //Assert
        png.Should().NotBeEmpty();
        Encoding.ASCII.GetString(png, 1, 3).Should().Be("PNG");
    }
}
