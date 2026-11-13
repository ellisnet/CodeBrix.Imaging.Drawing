using System;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionOpacityTests
{
    private static DrawingSession CreateTwoLayerSession(Color background)
    {
        //Two opaque layers covering the same square: what a frame fade must not let show through
        var session = new DrawingSession(new DrawingSessionOptions { BackgroundFillColor = background, LayerOpacity = 255 });
        session.AddLayer("Under", Color.Red);
        session.DrawRectangle(0, 0, 1000, 1000, filled: true);
        session.ActiveLayer = session.AddLayer("Over", Color.Blue);
        session.DrawRectangle(250, 250, 500, 500, filled: true);
        return session;
    }

    [Fact]
    public void Opacity_defaults_to_one_and_renders_as_before()
    {
        //Arrange
        using DrawingSession session = CreateTwoLayerSession(Color.Black);

        //Act
        byte[] pixels = session.ExportPixels(new Size(100, 100));

        //Assert
        session.Opacity.Should().Be(1f);
        session.ActiveLayer.Opacity.Should().Be(1f);
        TestAssets.PixelAt(pixels, 100, 50, 50).Should().Be(new Rgba32(0, 0, 255, 255));
        TestAssets.PixelAt(pixels, 100, 5, 5).Should().Be(new Rgba32(255, 0, 0, 255));
    }

    [Fact]
    public void Opacity_fades_all_layers_as_one_group_over_the_background()
    {
        //Arrange
        using DrawingSession session = CreateTwoLayerSession(Color.Black);

        //Act
        session.Opacity = 0.5f;
        byte[] pixels = session.ExportPixels(new Size(100, 100));

        //Assert - half blue over black (no red showing through the blue), still opaque
        Rgba32 center = TestAssets.PixelAt(pixels, 100, 50, 50);
        center.R.Should().BeLessThanOrEqualTo((byte)1);
        center.B.Should().BeInRange((byte)126, (byte)130);
        center.A.Should().Be((byte)255);
        TestAssets.PixelAt(pixels, 100, 5, 5).R.Should().BeInRange((byte)126, (byte)130);
    }

    [Fact]
    public void Opacity_zero_leaves_only_the_background_and_is_transparent_without_it()
    {
        //Arrange
        using DrawingSession session = CreateTwoLayerSession(Color.FromRgb(4, 3, 9));

        //Act
        session.Opacity = 0f;
        byte[] withBackground = session.ExportPixels(new Size(50, 50));
        byte[] withoutBackground = session.ExportPixels(new Size(50, 50), includeBackground: false);

        //Assert
        TestAssets.PixelAt(withBackground, 50, 25, 25).Should().Be(new Rgba32(4, 3, 9, 255));
        TestAssets.CountInked(withoutBackground).Should().Be(0);
    }

    [Fact]
    public void Opacity_over_transparency_scales_alpha_and_keeps_color()
    {
        //Arrange
        using DrawingSession session = CreateTwoLayerSession(Color.Transparent);

        //Act
        session.Opacity = 0.25f;
        byte[] pixels = session.ExportPixels(new Size(100, 100), includeBackground: false);

        //Assert - straight alpha: blue at a quarter opacity
        Rgba32 center = TestAssets.PixelAt(pixels, 100, 50, 50);
        center.A.Should().BeInRange((byte)62, (byte)66);
        center.B.Should().BeGreaterThan((byte)250);
        center.R.Should().BeLessThanOrEqualTo((byte)4);
    }

    [Fact]
    public void Layer_opacity_fades_one_layer_only()
    {
        //Arrange
        using DrawingSession session = CreateTwoLayerSession(Color.Black);

        //Act
        session.GetLayer("Over").Opacity = 0.5f;
        byte[] pixels = session.ExportPixels(new Size(100, 100));

        //Assert - half blue over the red layer
        Rgba32 center = TestAssets.PixelAt(pixels, 100, 50, 50);
        center.R.Should().BeInRange((byte)126, (byte)130);
        center.B.Should().BeInRange((byte)126, (byte)130);
    }

    [Fact]
    public void Live_render_follows_opacity_changes()
    {
        //Arrange
        using DrawingSession session = CreateTwoLayerSession(Color.Black);
        var info = new SKImageInfo(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        int redraws = 0;
        session.RedrawRequested += (_, _) => redraws++;

        //Act
        session.Render(canvas, info);
        SKColor before = bitmap.GetPixel(50, 50);
        session.Opacity = 0.5f;
        session.Render(canvas, info);
        SKColor after = bitmap.GetPixel(50, 50);
        session.GetLayer("Over").Opacity = 0f;
        session.Render(canvas, info);
        SKColor hidden = bitmap.GetPixel(50, 50);

        //Assert - the cached scene is rebuilt for both the session and the layer change
        before.Blue.Should().Be((byte)255);
        after.Blue.Should().BeInRange((byte)126, (byte)130);
        hidden.Blue.Should().Be((byte)0);
        hidden.Red.Should().BeInRange((byte)126, (byte)130);
        redraws.Should().Be(1);
    }

    [Fact]
    public void Opacity_rejects_values_outside_zero_to_one()
    {
        //Arrange
        using DrawingSession session = CreateTwoLayerSession(Color.Black);
        var layer = new DrawingLayer("L", Color.Red);
        Action sessionTooHigh = () => session.Opacity = 1.5f;
        Action sessionNan = () => session.Opacity = Single.NaN;
        Action layerNegative = () => layer.Opacity = -0.1f;

        //Act + Assert
        sessionTooHigh.Should().Throw<ArgumentOutOfRangeException>();
        sessionNan.Should().Throw<ArgumentOutOfRangeException>();
        layerNegative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LayerOpacity_keeps_its_meaning_inside_the_fade()
    {
        //Arrange - highlighter alpha 100 on a single layer, then a half fade
        using var session = new DrawingSession(new DrawingSessionOptions { BackgroundFillColor = Color.White });
        session.AddLayer("Ink", Color.Black);
        session.DrawRectangle(0, 0, 1000, 1000, filled: true);

        //Act
        byte[] full = session.ExportPixels(new Size(20, 20));
        session.Opacity = 0.5f;
        byte[] faded = session.ExportPixels(new Size(20, 20));

        //Assert - 100/255 black over white is about 155; at half that alpha about 205
        TestAssets.PixelAt(full, 20, 10, 10).R.Should().BeInRange((byte)153, (byte)157);
        TestAssets.PixelAt(faded, 20, 10, 10).R.Should().BeInRange((byte)203, (byte)207);
    }
}
