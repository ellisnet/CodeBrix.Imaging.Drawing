using System;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Effects;

public class DrawingMaskTests
{
    private static readonly Size Frame = new Size(200, 200);
    private static readonly PathGeometry LeftHalf = PathGeometry.Rectangle(new RectangleF(0, 0, 100, 200));

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255 });
        session.AddLayer("Content", Color.FromRgb(200, 30, 30));
        return session;
    }

    [Fact]
    public void Factories_describe_the_mask()
    {
        //Arrange
        var layer = new DrawingLayer("Matte", Color.White);
        var plane = new DrawingPlane(4, 3, 0.5f);

        //Act
        DrawingMask inside = DrawingMask.Inside(LeftHalf);
        DrawingMask outside = DrawingMask.Outside(LeftHalf, antialias: false);
        DrawingMask matte = DrawingMask.FromLayer(layer, invert: true);
        DrawingMask alpha = DrawingMask.FromPlane(plane);

        //Assert
        inside.Kind.Should().Be(DrawingMaskKind.Inside);
        inside.IsInverted.Should().BeFalse();
        inside.IsAntialiased.Should().BeTrue();
        outside.IsInverted.Should().BeTrue();
        outside.IsAntialiased.Should().BeFalse();
        matte.Layer.Should().BeSameAs(layer);
        matte.IsInverted.Should().BeTrue();
        alpha.Destination.Should().Be(new RectangleF(0, 0, 4, 3));
        alpha.PlaneWidth.Should().Be(4);
        alpha.PlaneHeight.Should().Be(3);
    }

    [Fact]
    public void Factories_reject_bad_arguments()
    {
        //Act
        Action path = () => DrawingMask.Inside(null);
        Action layer = () => DrawingMask.FromLayer(null);
        Action empty = () => DrawingMask.FromPlane(new DrawingPlane(2, 2), RectangleF.Empty);

        //Assert
        path.Should().Throw<ArgumentNullException>();
        layer.Should().Throw<ArgumentNullException>();
        empty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Layer_mask_inside_a_path_shows_content_only_there_over_transparency()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(20, 20, 160, 160, filled: true);
        session.Layers[0].Mask = DrawingMask.Inside(LeftHalf);

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.PixelAt(pixels, 200, 50, 100).Should().Be(new Rgba32(200, 30, 30, 255));
        TestAssets.PixelAt(pixels, 200, 150, 100).A.Should().Be(0);
    }

    [Fact]
    public void Group_mask_outside_a_path_cuts_a_hole_that_moves_with_the_group()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var group = new DrawingGroup();
        group.Add(new RectangleShape(0, 0, 100, 100, 1, null, true));
        group.Mask = DrawingMask.Outside(PathGeometry.Circle(new PointF(50, 50), 25));
        group.Transform = DrawingTransform.Translate(50, 50);
        session.DrawShape(group);

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().Be(0);
        TestAssets.PixelAt(pixels, 200, 60, 60).A.Should().Be(255);
        TestAssets.PixelAt(pixels, 200, 20, 20).A.Should().Be(0);
    }

    [Fact]
    public void Layer_mask_from_another_layer_uses_its_coverage_and_follows_its_changes()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(0, 0, 200, 200, filled: true);
        var matte = new DrawingLayer("Matte", Color.White);
        matte.AddShape(new CircleShape(60, 100, 30, 1, null, true));
        session.Layers[0].Mask = DrawingMask.FromLayer(matte);
        var info = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        //Act
        session.Render(canvas, info);
        SKColor beforeOnRight = bitmap.GetPixel(140, 100);
        matte.AddShape(new CircleShape(140, 100, 30, 1, null, true));
        session.Render(canvas, info);
        SKColor afterOnRight = bitmap.GetPixel(140, 100);
        byte[] exported = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        beforeOnRight.Alpha.Should().Be(0);
        afterOnRight.Alpha.Should().Be(255);
        TestAssets.PixelAt(exported, 200, 60, 100).A.Should().Be(255);
        TestAssets.PixelAt(exported, 200, 100, 100).A.Should().Be(0);
    }

    [Fact]
    public void Inverted_layer_matte_keeps_content_where_the_matte_is_not()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(0, 0, 200, 200, filled: true);
        var matte = new DrawingLayer("Matte", Color.White);
        matte.AddShape(new CircleShape(100, 100, 40, 1, null, true));
        session.Layers[0].Mask = DrawingMask.FromLayer(matte, invert: true);

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().Be(0);
        TestAssets.PixelAt(pixels, 200, 10, 10).A.Should().Be(255);
    }

    [Fact]
    public void Plane_mask_scales_coverage_by_the_plane_values()
    {
        //Arrange - a horizontal ramp 0 .. 1 across the frame
        using DrawingSession session = CreateSession();
        session.DrawRectangle(0, 0, 200, 200, filled: true);
        DrawingPlane ramp = new DrawingPlane(200, 200).Fill((x, y) => x / 199f);
        session.Layers[0].Mask = DrawingMask.FromPlane(ramp);

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.PixelAt(pixels, 200, 0, 100).A.Should().BeLessThan(3);
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().BeInRange((byte)120, (byte)135);
        TestAssets.PixelAt(pixels, 200, 199, 100).A.Should().BeGreaterThan(250);
    }

    [Fact]
    public void Disposing_a_plane_mask_releases_it_and_it_then_covers_nothing()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(0, 0, 200, 200, filled: true);
        var mask = DrawingMask.FromPlane(new DrawingPlane(200, 200, 1f));
        session.Layers[0].Mask = mask;
        var info = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, info);
        byte before = session.ExportPixels(Frame, includeBackground: false)[(100 * 200 + 100) * 4 + 3];

        //Act
        mask.Dispose();
        mask.Dispose();
        session.Render(canvas, info);
        byte after = session.ExportPixels(Frame, includeBackground: false)[(100 * 200 + 100) * 4 + 3];
        var inverted = DrawingMask.FromPlane(new DrawingPlane(200, 200, 1f), true);
        inverted.Dispose();
        session.Layers[0].Mask = inverted;
        byte invertedAfter = session.ExportPixels(Frame, includeBackground: false)[(100 * 200 + 100) * 4 + 3];
        var path = DrawingMask.Inside(LeftHalf);
        path.Dispose();

        //Assert - the live cache notices the disposal; size and kind stay readable
        before.Should().Be(255);
        mask.IsDisposed.Should().BeTrue();
        after.Should().Be(0);
        bitmap.GetPixel(100, 100).Alpha.Should().Be(0);
        mask.PlaneWidth.Should().Be(200);
        invertedAfter.Should().Be(255);
        path.IsDisposed.Should().BeTrue();
        path.Geometry.Should().BeSameAs(LeftHalf);
    }

    [Fact]
    public void Masked_layer_live_render_matches_a_fresh_render_after_the_mask_changes()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawCircle(100, 100, 80, filled: true);
        var info = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, info);

        //Act
        session.Layers[0].Mask = DrawingMask.Inside(LeftHalf);
        session.Render(canvas, info);
        using DrawingSession fresh = CreateSession();
        fresh.DrawCircle(100, 100, 80, filled: true);
        fresh.Layers[0].Mask = DrawingMask.Inside(LeftHalf);
        using var freshBitmap = new SKBitmap(info);
        using var freshCanvas = new SKCanvas(freshBitmap);
        fresh.Render(freshCanvas, info);

        //Assert
        bitmap.Bytes.Should().Equal(freshBitmap.Bytes);
        bitmap.GetPixel(150, 100).Alpha.Should().Be(0);
    }

    [Fact]
    public void Masks_count_in_the_layer_coverage_export()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(0, 0, 200, 200, filled: true);
        session.Layers[0].Mask = DrawingMask.Inside(LeftHalf, antialias: false);

        //Act
        DrawingPlane coverage = session.ExportMask(Frame);

        //Assert
        coverage[50, 100].Should().Be(1f);
        coverage[150, 100].Should().Be(0f);
    }
}
