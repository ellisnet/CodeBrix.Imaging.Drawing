using CodeBrix.Imaging.Drawing.Rendering;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Rendering;

public class CanvasCalibrationTests
{
    private static readonly SKSizeI Square1000 = new SKSizeI(1000, 1000);

    [Fact]
    public void GetDrawingRect_centers_square_space_in_wide_canvas()
    {
        //Arrange + Act
        SKRect rect = CanvasCalibration.GetDrawingRect(new SKSizeI(400, 200), Square1000);

        //Assert
        rect.Left.Should().Be(100f);
        rect.Top.Should().Be(0f);
        rect.Width.Should().Be(200f);
        rect.Height.Should().Be(200f);
    }

    [Fact]
    public void GetDrawingRect_centers_square_space_in_tall_canvas()
    {
        //Arrange + Act
        SKRect rect = CanvasCalibration.GetDrawingRect(new SKSizeI(200, 400), Square1000);

        //Assert
        rect.Left.Should().Be(0f);
        rect.Top.Should().Be(100f);
        rect.Width.Should().Be(200f);
        rect.Height.Should().Be(200f);
    }

    [Fact]
    public void GetDrawingRect_honors_non_square_calibration_aspect()
    {
        //Arrange + Act - a 2:1 calibration space inside a square canvas
        SKRect rect = CanvasCalibration.GetDrawingRect(new SKSizeI(400, 400), new SKSizeI(2000, 1000));

        //Assert
        rect.Width.Should().Be(400f);
        rect.Height.Should().Be(200f);
        rect.Top.Should().Be(100f);
    }

    [Fact]
    public void GetDrawingRect_is_empty_for_degenerate_sizes()
        => CanvasCalibration.GetDrawingRect(new SKSizeI(0, 100), Square1000).IsEmpty.Should().BeTrue();

    [Fact]
    public void ViewPointToCalibrated_maps_center_to_center()
    {
        //Arrange - view and canvas are the same size, square canvas
        var viewSize = new SKSize(200, 200);
        var canvasSize = new SKSizeI(200, 200);

        //Act
        SKPointI? result = CanvasCalibration.ViewPointToCalibrated(
            new SKPoint(100, 100), viewSize, canvasSize, Square1000);

        //Assert
        result.Should().NotBeNull();
        result.Value.X.Should().Be(500);
        result.Value.Y.Should().Be(500);
    }

    [Fact]
    public void ViewPointToCalibrated_scales_for_high_dpi_canvas()
    {
        //Arrange - canvas pixels are 2x the view's logical units
        var viewSize = new SKSize(200, 200);
        var canvasSize = new SKSizeI(400, 400);

        //Act
        SKPointI? result = CanvasCalibration.ViewPointToCalibrated(
            new SKPoint(50, 100), viewSize, canvasSize, Square1000);

        //Assert
        result.Should().NotBeNull();
        result.Value.X.Should().Be(250);
        result.Value.Y.Should().Be(500);
    }

    [Fact]
    public void ViewPointToCalibrated_returns_null_outside_drawing_area()
    {
        //Arrange - wide canvas, so the left margin is outside the centered square
        var viewSize = new SKSize(400, 200);
        var canvasSize = new SKSizeI(400, 200);

        //Act
        SKPointI? result = CanvasCalibration.ViewPointToCalibrated(
            new SKPoint(10, 100), viewSize, canvasSize, Square1000);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ViewPointToCalibrated_clamps_outside_point_when_requested()
    {
        //Arrange
        var viewSize = new SKSize(400, 200);
        var canvasSize = new SKSizeI(400, 200);

        //Act
        SKPointI? result = CanvasCalibration.ViewPointToCalibrated(
            new SKPoint(10, 100), viewSize, canvasSize, Square1000, clampToDrawingArea: true);

        //Assert
        result.Should().NotBeNull();
        result.Value.X.Should().Be(0);
        result.Value.Y.Should().Be(500);
    }

    [Fact]
    public void ViewPointToCalibrated_returns_null_for_zero_view_size()
        => CanvasCalibration.ViewPointToCalibrated(
            new SKPoint(1, 1), new SKSize(0, 0), new SKSizeI(100, 100), Square1000).Should().BeNull();

    [Fact]
    public void CalibratedToCanvas_round_trips_view_point()
    {
        //Arrange
        var canvasSize = new SKSizeI(500, 500);
        SKRect drawingRect = CanvasCalibration.GetDrawingRect(canvasSize, Square1000);
        var original = new SKPoint(123, 234);

        //Act
        SKPointI? calibrated = CanvasCalibration.ViewPointToCalibrated(
            original, new SKSize(500, 500), canvasSize, Square1000);
        SKPoint roundTripped = CanvasCalibration.CalibratedToCanvas(calibrated.Value, Square1000, drawingRect);

        //Assert - within one pixel of the original position after the integer round trip
        ((double)roundTripped.X).Should().BeApproximately(original.X, 1.0);
        ((double)roundTripped.Y).Should().BeApproximately(original.Y, 1.0);
    }

    [Fact]
    public void ScaleStrokeWidth_scales_proportionally()
    {
        //Arrange - drawing rect is 1/5 the calibration width
        var drawingRect = new SKRect(0, 0, 200, 200);

        //Act
        float scaled = CanvasCalibration.ScaleStrokeWidth(15f, Square1000, drawingRect);

        //Assert
        scaled.Should().Be(3f);
    }

    [Fact]
    public void ScaleStrokeWidth_never_returns_less_than_one_pixel()
        => CanvasCalibration.ScaleStrokeWidth(2f, Square1000, new SKRect(0, 0, 10, 10)).Should().Be(1f);
}
