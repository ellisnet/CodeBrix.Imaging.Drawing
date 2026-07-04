using System;
using CodeBrix.Imaging.Drawing.Models;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionTests
{
    private static readonly SKImageInfo CanvasInfo = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
    private static readonly SKSize ViewSize = new SKSize(200, 200);

    private static DrawingSession CreateRenderedSession()
    {
        //A session that has rendered once, so pointer input can be calibrated
        var session = new DrawingSession();
        session.AddLayer("Pain", SKColors.Red);
        RenderOnce(session);
        return session;
    }

    private static void RenderOnce(DrawingSession session)
    {
        using var bitmap = new SKBitmap(CanvasInfo);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, CanvasInfo);
    }

    [Fact]
    public void AddLayer_makes_first_layer_active()
    {
        //Arrange
        using var session = new DrawingSession();

        //Act
        DrawingLayer pain = session.AddLayer("Pain", SKColors.Red);
        session.AddLayer("Numbness", SKColors.Blue);

        //Assert
        session.ActiveLayer.Should().Be(pain);
        session.Layers.Should().HaveCount(2);
    }

    [Fact]
    public void AddLayer_rejects_duplicate_name()
    {
        //Arrange
        using var session = new DrawingSession();
        session.AddLayer("Pain", SKColors.Red);
        Action act = () => session.AddLayer("Pain", SKColors.Blue);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetLayer_finds_layer_by_name()
    {
        //Arrange
        using var session = new DrawingSession();
        DrawingLayer numbness = session.AddLayer("Numbness", SKColors.Blue);

        //Act + Assert
        session.GetLayer("Numbness").Should().Be(numbness);
        session.GetLayer("Missing").Should().BeNull();
    }

    [Fact]
    public void RemoveLayer_reassigns_active_layer()
    {
        //Arrange
        using var session = new DrawingSession();
        DrawingLayer pain = session.AddLayer("Pain", SKColors.Red);
        DrawingLayer numbness = session.AddLayer("Numbness", SKColors.Blue);

        //Act
        bool removed = session.RemoveLayer(pain);

        //Assert
        removed.Should().BeTrue();
        session.ActiveLayer.Should().Be(numbness);
        session.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void PointerPressed_before_first_render_is_ignored()
    {
        //Arrange
        using var session = new DrawingSession();
        session.AddLayer("Pain", SKColors.Red);

        //Act
        bool started = session.PointerPressed(new SKPoint(100, 100), ViewSize);

        //Assert
        started.Should().BeFalse();
        session.IsPointerActive.Should().BeFalse();
    }

    [Fact]
    public void PointerPressed_without_layers_is_ignored()
    {
        //Arrange
        using var session = new DrawingSession();
        RenderOnce(session);

        //Act + Assert
        session.PointerPressed(new SKPoint(100, 100), ViewSize).Should().BeFalse();
    }

    [Fact]
    public void Pointer_press_move_release_commits_stroke_to_active_layer()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();

        //Act
        session.PointerPressed(new SKPoint(50, 50), ViewSize);
        session.PointerMoved(new SKPoint(100, 100), ViewSize);
        session.PointerMoved(new SKPoint(150, 150), ViewSize);
        bool committed = session.PointerReleased();

        //Assert
        committed.Should().BeTrue();
        session.StrokeCount.Should().Be(1);
        session.HasStrokes.Should().BeTrue();
        session.ActiveLayer.ElementCount.Should().Be(1);
        session.ActiveLayer.GetStrokes()[0].PointCount.Should().Be(3);
    }

    [Fact]
    public void Pointer_press_and_release_without_movement_commits_a_dot()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();

        //Act
        session.PointerPressed(new SKPoint(100, 100), ViewSize);
        session.PointerReleased();

        //Assert
        session.ActiveLayer.GetStrokes()[0].PointCount.Should().Be(1);
    }

    [Fact]
    public void PointerMoved_without_press_is_ignored()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();

        //Act + Assert
        session.PointerMoved(new SKPoint(100, 100), ViewSize).Should().BeFalse();
        session.StrokeCount.Should().Be(0);
    }

    [Fact]
    public void PointerCanceled_discards_in_progress_stroke()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        session.PointerPressed(new SKPoint(100, 100), ViewSize);

        //Act
        session.PointerCanceled();

        //Assert
        session.IsPointerActive.Should().BeFalse();
        session.PointerReleased().Should().BeFalse();
        session.StrokeCount.Should().Be(0);
    }

    [Fact]
    public void Stroke_commits_use_the_layer_that_was_active_at_press_time()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        DrawingLayer pain = session.ActiveLayer;
        DrawingLayer numbness = session.AddLayer("Numbness", SKColors.Blue);
        session.PointerPressed(new SKPoint(100, 100), ViewSize);

        //Act - switching layers mid-stroke must not re-target the stroke
        session.ActiveLayer = numbness;
        session.PointerReleased();

        //Assert
        pain.ElementCount.Should().Be(1);
        numbness.ElementCount.Should().Be(0);
    }

    [Fact]
    public void DrawingChanged_is_raised_when_a_stroke_commits()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        var changedCount = 0;
        session.DrawingChanged += (s, e) => changedCount++;

        //Act
        session.PointerPressed(new SKPoint(100, 100), ViewSize);
        session.PointerReleased();

        //Assert
        changedCount.Should().Be(1);
    }

    [Fact]
    public void RedrawRequested_is_raised_by_pointer_input()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        var redrawCount = 0;
        session.RedrawRequested += (s, e) => redrawCount++;

        //Act
        session.PointerPressed(new SKPoint(100, 100), ViewSize);
        session.PointerMoved(new SKPoint(120, 120), ViewSize);
        session.PointerReleased();

        //Assert
        redrawCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public void Clear_removes_all_strokes_but_keeps_layers()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        session.PointerPressed(new SKPoint(100, 100), ViewSize);
        session.PointerReleased();

        //Act
        session.Clear();

        //Assert
        session.HasStrokes.Should().BeFalse();
        session.Layers.Should().HaveCount(1);
        session.ActiveLayer.Should().NotBeNull();
    }

    [Fact]
    public void UndoLastStroke_removes_most_recent_stroke_across_layers()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        DrawingLayer pain = session.ActiveLayer;
        DrawingLayer numbness = session.AddLayer("Numbness", SKColors.Blue);

        session.PointerPressed(new SKPoint(50, 50), ViewSize);
        session.PointerReleased();
        session.ActiveLayer = numbness;
        session.PointerPressed(new SKPoint(150, 150), ViewSize);
        session.PointerReleased();

        //Act
        bool undone = session.UndoLastStroke();

        //Assert - the numbness stroke was the most recent
        undone.Should().BeTrue();
        pain.ElementCount.Should().Be(1);
        numbness.ElementCount.Should().Be(0);
    }

    [Fact]
    public void UndoLastStroke_returns_false_when_nothing_to_undo()
        => CreateRenderedSession().UndoLastStroke().Should().BeFalse();

    [Fact]
    public void ExportPng_produces_decodable_image_of_requested_size()
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        session.BackgroundFillColor = SKColors.White;
        session.PointerPressed(new SKPoint(100, 100), ViewSize);
        session.PointerReleased();

        //Act
        byte[] png = session.ExportPng(new SKSizeI(400, 400));

        //Assert
        png.Should().NotBeNull();
        using SKBitmap decoded = SKBitmap.Decode(png);
        decoded.Should().NotBeNull();
        decoded.Width.Should().Be(400);
        decoded.Height.Should().Be(400);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ExportJpeg_rejects_out_of_range_quality(int quality)
    {
        //Arrange
        using DrawingSession session = CreateRenderedSession();
        Action act = () => session.ExportJpeg(new SKSizeI(100, 100), quality);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetBackgroundImage_rejects_undecodable_bytes()
    {
        //Arrange
        using var session = new DrawingSession();
        Action act = () => session.SetBackgroundImage(new byte[] { 1, 2, 3, 4 });

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetBackgroundImage_decodes_and_applies_image()
    {
        //Arrange
        using var session = new DrawingSession();
        using var source = new SKBitmap(new SKImageInfo(10, 10));
        source.Erase(SKColors.Yellow);
        using SKImage sourceImage = SKImage.FromBitmap(source);
        using SKData encoded = sourceImage.Encode(SKEncodedImageFormat.Png, 100);

        //Act
        session.SetBackgroundImage(encoded.ToArray());

        //Assert
        session.BackgroundImage.Should().NotBeNull();
        session.BackgroundImage.Width.Should().Be(10);
    }

    [Fact]
    public void Options_are_applied_at_construction()
    {
        //Arrange + Act
        using var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new SKSizeI(2000, 1000),
            LayerOpacity = 128,
            ActiveStrokeOpacity = 255,
            BackgroundFillColor = SKColors.White,
            SurfaceClearColor = SKColors.Black,
            StrokeWidth = 42f,
        });

        //Assert
        session.CalibrationSize.Should().Be(new SKSizeI(2000, 1000));
        session.LayerOpacity.Should().Be((byte)128);
        session.ActiveStrokeOpacity.Should().Be((byte)255);
        session.BackgroundFillColor.Should().Be(SKColors.White);
        session.SurfaceClearColor.Should().Be(SKColors.Black);
        session.StrokeWidth.Should().Be(42f);
    }

    [Fact]
    public void Disposed_session_throws_on_use()
    {
        //Arrange
        var session = new DrawingSession();
        session.Dispose();
        Action act = () => session.AddLayer("Pain", SKColors.Red);

        //Act + Assert
        session.IsDisposed.Should().BeTrue();
        act.Should().Throw<ObjectDisposedException>();
    }
}
