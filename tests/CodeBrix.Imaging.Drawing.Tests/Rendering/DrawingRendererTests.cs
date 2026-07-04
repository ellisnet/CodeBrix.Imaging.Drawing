using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Rendering;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Rendering;

public class DrawingRendererTests
{
    private static readonly SKSizeI Square1000 = new SKSizeI(1000, 1000);
    private static readonly SKImageInfo CanvasInfo = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);

    private static Stroke CenterLineStroke()
    {
        //A horizontal line through the center of the calibrated space
        var stroke = new Stroke(100f);
        stroke.AddPoint(200, 500);
        stroke.AddPoint(800, 500);
        return stroke;
    }

    private static SKBitmap RenderToBitmap(DrawingRenderer renderer, IReadOnlyList<DrawingLayer> layers,
        Stroke activeStroke = null, SKColor? activeStrokeColor = null)
    {
        var bitmap = new SKBitmap(CanvasInfo);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, CanvasInfo, layers, activeStroke, activeStrokeColor);
        return bitmap;
    }

    [Fact]
    public void Constructor_rejects_degenerate_calibration_size()
    {
        //Arrange
        Action act = () => _ = new DrawingRenderer(new SKSizeI(0, 100));

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Render_clears_canvas_with_surface_clear_color()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000) { SurfaceClearColor = SKColors.Green };

        //Act
        using SKBitmap bitmap = RenderToBitmap(renderer, new List<DrawingLayer>());

        //Assert
        bitmap.GetPixel(1, 1).Should().Be(SKColors.Green);
    }

    [Fact]
    public void Render_fills_drawing_rect_with_background_fill_color()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };

        //Act
        using SKBitmap bitmap = RenderToBitmap(renderer, new List<DrawingLayer>());

        //Assert
        bitmap.GetPixel(100, 100).Should().Be(SKColors.White);
    }

    [Fact]
    public void Render_updates_last_drawing_rect_and_canvas_size()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000);

        //Act
        using SKBitmap bitmap = RenderToBitmap(renderer, new List<DrawingLayer>());

        //Assert
        renderer.LastCanvasSize.Should().Be(new SKSizeI(200, 200));
        renderer.LastDrawingRect.Width.Should().Be(200f);
        renderer.LastDrawingRect.Height.Should().Be(200f);
    }

    [Fact]
    public void Render_draws_layer_stroke_translucently_over_background()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };
        var layer = new DrawingLayer("Pain", SKColors.Red);
        layer.AddStroke(CenterLineStroke());

        //Act
        using SKBitmap bitmap = RenderToBitmap(renderer, new[] { layer });

        //Assert - the stroke center is tinted (not white, not fully opaque red)
        SKColor pixel = bitmap.GetPixel(100, 100);
        pixel.Should().NotBe(SKColors.White);
        pixel.Should().NotBe(SKColors.Red);
        pixel.Red.Should().BeGreaterThan(pixel.Blue);
    }

    [Fact]
    public void Overlapping_strokes_in_one_layer_do_not_double_darken()
    {
        //Arrange - the defining "highlighter" behavior of the renderer
        using var singleRenderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };
        var singleLayer = new DrawingLayer("Pain", SKColors.Red);
        singleLayer.AddStroke(CenterLineStroke());

        using var doubleRenderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };
        var doubleLayer = new DrawingLayer("Pain", SKColors.Red);
        doubleLayer.AddStroke(CenterLineStroke());
        doubleLayer.AddStroke(CenterLineStroke());

        //Act
        using SKBitmap singleBitmap = RenderToBitmap(singleRenderer, new[] { singleLayer });
        using SKBitmap doubleBitmap = RenderToBitmap(doubleRenderer, new[] { doubleLayer });

        //Assert - the overlapping pixel is identical with one stroke or two
        doubleBitmap.GetPixel(100, 100).Should().Be(singleBitmap.GetPixel(100, 100));
    }

    [Fact]
    public void Overlapping_strokes_on_different_layers_do_compound()
    {
        //Arrange
        using var singleRenderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };
        var painOnly = new DrawingLayer("Pain", SKColors.Red);
        painOnly.AddStroke(CenterLineStroke());

        using var doubleRenderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };
        var pain = new DrawingLayer("Pain", SKColors.Red);
        pain.AddStroke(CenterLineStroke());
        var numbness = new DrawingLayer("Numbness", SKColors.Blue);
        numbness.AddStroke(CenterLineStroke());

        //Act
        using SKBitmap singleBitmap = RenderToBitmap(singleRenderer, new[] { painOnly });
        using SKBitmap doubleBitmap = RenderToBitmap(doubleRenderer, new[] { pain, numbness });

        //Assert - a second layer over the same spot changes the composite
        doubleBitmap.GetPixel(100, 100).Should().NotBe(singleBitmap.GetPixel(100, 100));
    }

    [Fact]
    public void Render_draws_active_stroke_when_supplied()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };

        //Act
        using SKBitmap bitmap = RenderToBitmap(renderer, new List<DrawingLayer>(),
            CenterLineStroke(), SKColors.Red);

        //Assert
        bitmap.GetPixel(100, 100).Should().NotBe(SKColors.White);
    }

    [Fact]
    public void Render_reflects_strokes_added_after_a_previous_render()
    {
        //Arrange - exercises the incremental layer-cache path
        using var renderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };
        var layer = new DrawingLayer("Pain", SKColors.Red);
        var layers = new[] { layer };
        using (SKBitmap first = RenderToBitmap(renderer, layers))
        {
            first.GetPixel(100, 100).Should().Be(SKColors.White);
        }

        //Act
        layer.AddStroke(CenterLineStroke());
        using SKBitmap second = RenderToBitmap(renderer, layers);

        //Assert
        second.GetPixel(100, 100).Should().NotBe(SKColors.White);
    }

    [Fact]
    public void Render_reflects_layer_clear_after_a_previous_render()
    {
        //Arrange - exercises the ResetVersion cache-invalidation path
        using var renderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };
        var layer = new DrawingLayer("Pain", SKColors.Red);
        layer.AddStroke(CenterLineStroke());
        var layers = new[] { layer };
        using (SKBitmap first = RenderToBitmap(renderer, layers))
        {
            first.GetPixel(100, 100).Should().NotBe(SKColors.White);
        }

        //Act
        layer.Clear();
        using SKBitmap second = RenderToBitmap(renderer, layers);

        //Assert
        second.GetPixel(100, 100).Should().Be(SKColors.White);
    }

    [Fact]
    public void Render_draws_background_image_into_drawing_rect()
    {
        //Arrange
        using var backgroundImage = new SKBitmap(new SKImageInfo(10, 10));
        backgroundImage.Erase(SKColors.Yellow);
        using var renderer = new DrawingRenderer(Square1000) { BackgroundImage = backgroundImage };

        //Act
        using SKBitmap bitmap = RenderToBitmap(renderer, new List<DrawingLayer>());

        //Assert
        bitmap.GetPixel(100, 100).Should().Be(SKColors.Yellow);
    }

    [Fact]
    public void RenderToImage_produces_image_of_requested_size()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000);

        //Act
        using SKImage image = renderer.RenderToImage(new SKSizeI(320, 240), new List<DrawingLayer>());

        //Assert
        image.Width.Should().Be(320);
        image.Height.Should().Be(240);
    }

    [Fact]
    public void RenderToImage_can_exclude_background()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000) { BackgroundFillColor = SKColors.White };

        //Act
        using SKImage image = renderer.RenderToImage(new SKSizeI(100, 100), new List<DrawingLayer>(), includeBackground: false);
        using SKBitmap bitmap = SKBitmap.FromImage(image);

        //Assert - fully transparent without the background fill
        bitmap.GetPixel(50, 50).Alpha.Should().Be((byte)0);
    }

    [Fact]
    public void Render_without_clearing_overlays_existing_canvas_content()
    {
        //Arrange - a fully transparent overlay session (no background, transparent colors)
        //  over a canvas that already holds externally drawn content (e.g. a video frame)
        using var renderer = new DrawingRenderer(Square1000);
        var layer = new DrawingLayer("Marks", SKColors.Red);
        layer.AddStroke(CenterLineStroke());

        var bitmap = new SKBitmap(CanvasInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Green); //stands in for the video frame

        //Act
        renderer.Render(canvas, CanvasInfo, new[] { layer }, clearCanvas: false);

        //Assert - the "video" shows through where nothing was drawn, and the stroke
        //  composites over it where it was
        bitmap.GetPixel(10, 10).Should().Be(SKColors.Green);
        SKColor strokePixel = bitmap.GetPixel(100, 100);
        strokePixel.Should().NotBe(SKColors.Green);
        strokePixel.Red.Should().BeGreaterThan((byte)0);
        bitmap.Dispose();
    }

    [Fact]
    public void Render_with_default_clearing_replaces_existing_canvas_content()
    {
        //Arrange
        using var renderer = new DrawingRenderer(Square1000);

        var bitmap = new SKBitmap(CanvasInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Green);

        //Act - default render clears to SurfaceClearColor (transparent)
        renderer.Render(canvas, CanvasInfo, new List<DrawingLayer>());

        //Assert
        bitmap.GetPixel(10, 10).Alpha.Should().Be((byte)0);
        bitmap.Dispose();
    }

    [Fact]
    public void Render_throws_after_dispose()
    {
        //Arrange
        var renderer = new DrawingRenderer(Square1000);
        renderer.Dispose();
        Action act = () =>
        {
            using SKBitmap bitmap = RenderToBitmap(renderer, new List<DrawingLayer>());
        };

        //Act + Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}
