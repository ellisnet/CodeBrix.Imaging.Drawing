using System;
using CodeBrix.Imaging.Drawing.Models;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionNormalizedInputTests
{
    private static readonly SKImageInfo CanvasInfo = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
    private static readonly SKSize ViewSize = new SKSize(200, 200);

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession();
        session.AddLayer("Ink", SKColors.Red);
        return session;
    }

    private static void RenderOnce(DrawingSession session)
    {
        using var bitmap = new SKBitmap(CanvasInfo);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, CanvasInfo);
    }

    [Fact]
    public void Normalized_press_works_without_any_prior_render()
    {
        //Arrange - deliberately NO Render call: normalized input needs no canvas size
        using DrawingSession session = CreateSession();

        //Act
        bool started = session.PointerPressedNormalized(0.5f, 0.5f);
        session.PointerMovedNormalized(0.75f, 0.25f);
        bool committed = session.PointerReleased();

        //Assert
        started.Should().BeTrue();
        committed.Should().BeTrue();
        session.StrokeCount.Should().Be(1);
    }

    [Fact]
    public void Normalized_points_land_at_the_expected_calibrated_coordinates()
    {
        //Arrange - default calibration space is 1000 x 1000
        using DrawingSession session = CreateSession();

        //Act
        session.PointerPressedNormalized(0.25f, 0.5f);
        session.PointerMovedNormalized(1f, 0f);
        session.PointerReleased();

        //Assert
        StrokePoint[] points = session.ActiveLayer.GetStrokes()[0].GetPoints();
        points[0].X.Should().Be(250);
        points[0].Y.Should().Be(500);
        points[1].X.Should().Be(1000);
        points[1].Y.Should().Be(0);
    }

    [Fact]
    public void Normalized_input_matches_view_input_for_the_same_position()
    {
        //Arrange - a square view over the square default calibration space: the view point
        //  (50, 150) on a 200x200 view is the normalized position (0.25, 0.75)
        using DrawingSession viewSession = CreateSession();
        RenderOnce(viewSession);
        using DrawingSession normalizedSession = CreateSession();

        //Act
        viewSession.PointerPressed(new SKPoint(50, 150), ViewSize);
        viewSession.PointerReleased();
        normalizedSession.PointerPressedNormalized(0.25f, 0.75f);
        normalizedSession.PointerReleased();

        //Assert
        StrokePoint viewPoint = viewSession.ActiveLayer.GetStrokes()[0].GetPoints()[0];
        StrokePoint normalizedPoint = normalizedSession.ActiveLayer.GetStrokes()[0].GetPoints()[0];
        normalizedPoint.X.Should().Be(viewPoint.X);
        normalizedPoint.Y.Should().Be(viewPoint.Y);
    }

    [Fact]
    public void Normalized_press_outside_the_unit_square_is_ignored()
    {
        //Arrange
        using DrawingSession session = CreateSession();

        //Act + Assert - mirrors how a view press outside the drawing area is ignored
        session.PointerPressedNormalized(-0.1f, 0.5f).Should().BeFalse();
        session.PointerPressedNormalized(0.5f, 1.1f).Should().BeFalse();
        session.IsPointerActive.Should().BeFalse();
    }

    [Fact]
    public void Normalized_moves_clamp_to_the_drawing_area()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.PointerPressedNormalized(0.5f, 0.5f);

        //Act - mirrors how view moves are clamped while a stroke is in progress
        session.PointerMovedNormalized(2f, -1f);
        session.PointerReleased();

        //Assert
        StrokePoint clamped = session.ActiveLayer.GetStrokes()[0].GetPoints()[1];
        clamped.X.Should().Be(1000);
        clamped.Y.Should().Be(0);
    }

    [Fact]
    public void Normalized_move_without_press_is_ignored()
    {
        //Arrange
        using DrawingSession session = CreateSession();

        //Act + Assert
        session.PointerMovedNormalized(0.5f, 0.5f).Should().BeFalse();
    }

    [Fact]
    public void Normalized_press_without_layers_is_ignored()
    {
        //Arrange
        using var session = new DrawingSession();

        //Act + Assert
        session.PointerPressedNormalized(0.5f, 0.5f).Should().BeFalse();
    }

    [Fact]
    public void Normalized_input_rejects_nan_positions()
    {
        //Arrange
        using DrawingSession session = CreateSession();

        //Act + Assert
        session.PointerPressedNormalized(Single.NaN, 0.5f).Should().BeFalse();
        session.PointerPressedNormalized(0.5f, 0.5f).Should().BeTrue();
        session.PointerMovedNormalized(Single.NaN, Single.NaN).Should().BeFalse();
        session.PointerReleased().Should().BeTrue();
    }

    [Fact]
    public void Normalized_and_view_input_may_be_mixed_within_one_stroke()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        RenderOnce(session);

        //Act - press from vision-style input, extend from the mouse, and vice versa
        session.PointerPressedNormalized(0.1f, 0.1f).Should().BeTrue();
        session.PointerMoved(new SKPoint(100, 100), ViewSize).Should().BeTrue();
        session.PointerMovedNormalized(0.9f, 0.9f).Should().BeTrue();
        bool committed = session.PointerReleased();

        //Assert
        committed.Should().BeTrue();
        session.ActiveLayer.GetStrokes()[0].PointCount.Should().Be(3);
    }

    [Fact]
    public void Normalized_input_respects_non_square_calibration_spaces()
    {
        //Arrange - a 4:3 drawing space
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(800, 600),
        });
        session.AddLayer("Ink", SKColors.Red);

        //Act
        session.PointerPressedNormalized(0.5f, 0.5f);
        session.PointerReleased();

        //Assert
        StrokePoint center = session.ActiveLayer.GetStrokes()[0].GetPoints()[0];
        center.X.Should().Be(400);
        center.Y.Should().Be(300);
        session.Dispose();
    }
}
