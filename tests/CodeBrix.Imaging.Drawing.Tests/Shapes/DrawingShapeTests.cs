using System;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class DrawingShapeTests
{
    private static readonly SKSizeI Square1000 = new SKSizeI(1000, 1000);
    private static readonly SKImageInfo CanvasInfo = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);

    private static SKBitmap RenderShape(DrawingShape shape, SKColor layerColor)
    {
        using var renderer = new DrawingRenderer(Square1000)
        {
            BackgroundFillColor = Color.White,
            LayerOpacity = 255, //opaque, so pixel assertions are exact
        };
        var layer = new DrawingLayer("Shapes", layerColor);
        layer.AddShape(shape);

        var bitmap = new SKBitmap(CanvasInfo);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, CanvasInfo, new[] { layer });
        return bitmap;
    }

    [Fact]
    public void Line_renders_between_its_end_points()
    {
        //Arrange - a horizontal line through the vertical center (calibrated y=500 -> pixel y=100)
        var line = new LineShape(100, 500, 900, 500, strokeThickness: 50);

        //Act
        using SKBitmap bitmap = RenderShape(line, SKColors.Red);

        //Assert
        bitmap.GetPixel(100, 100).Red.Should().Be((byte)255);
        bitmap.GetPixel(100, 20).Should().Be(SKColors.White); //far from the line
    }

    [Fact]
    public void Circle_outline_renders_rim_but_not_center()
    {
        //Arrange - centered circle, radius 400 calibrated units (80 pixels)
        var circle = new CircleShape(500, 500, 400, strokeThickness: 40);

        //Act
        using SKBitmap bitmap = RenderShape(circle, SKColors.Red);

        //Assert - rim pixel is red, center stays white
        bitmap.GetPixel(180, 100).Red.Should().Be((byte)255);
        bitmap.GetPixel(100, 100).Should().Be(SKColors.White);
    }

    [Fact]
    public void Filled_circle_renders_center_solid()
    {
        //Arrange
        var circle = new CircleShape(500, 500, 400, isFilled: true);

        //Act
        using SKBitmap bitmap = RenderShape(circle, SKColors.Red);

        //Assert
        bitmap.GetPixel(100, 100).Red.Should().Be((byte)255);
    }

    [Fact]
    public void Shape_specific_color_overrides_layer_color()
    {
        //Arrange - a blue circle on a red layer
        var circle = new CircleShape(500, 500, 400, color: Color.Blue, isFilled: true);

        //Act
        using SKBitmap bitmap = RenderShape(circle, SKColors.Red);

        //Assert
        SKColor center = bitmap.GetPixel(100, 100);
        center.Blue.Should().Be((byte)255);
        center.Red.Should().Be((byte)0);
    }

    [Fact]
    public void Rectangle_outline_renders_edges()
    {
        //Arrange - rectangle covering the middle half of the space
        var rectangle = new RectangleShape(250, 250, 500, 500, strokeThickness: 40);

        //Act
        using SKBitmap bitmap = RenderShape(rectangle, SKColors.Red);

        //Assert - edge pixel red (calibrated x=250 -> pixel 50), center white
        bitmap.GetPixel(50, 100).Red.Should().Be((byte)255);
        bitmap.GetPixel(100, 100).Should().Be(SKColors.White);
    }

    [Fact]
    public void Ellipse_renders_wider_than_tall()
    {
        //Arrange
        var ellipse = new EllipseShape(500, 500, 450, 200, strokeThickness: 40, isFilled: true);

        //Act
        using SKBitmap bitmap = RenderShape(ellipse, SKColors.Red);

        //Assert - a point inside horizontally but outside vertically distinguishes the radii
        bitmap.GetPixel(30, 100).Red.Should().Be((byte)255);   //x=150 calibrated: inside rx=450
        bitmap.GetPixel(100, 30).Should().Be(SKColors.White);  //y=150 calibrated: outside ry=200
    }

    [Fact]
    public void Arrow_renders_shaft_and_head()
    {
        //Arrange - arrow pointing right, tip at (800, 500), generous head
        var arrow = new ArrowShape(200, 500, 800, 500, strokeThickness: 30, headLength: 200);

        //Act
        using SKBitmap bitmap = RenderShape(arrow, SKColors.Red);

        //Assert - shaft pixel, plus a head-barb pixel above the shaft near the tip
        bitmap.GetPixel(100, 100).Red.Should().Be((byte)255);
        //Head barb: 200 units back at 30 degrees -> approx (627, 400) calibrated -> (125, 80) pixels
        bitmap.GetPixel(125, 80).Red.Should().Be((byte)255);
    }

    [Fact]
    public void Polyline_closed_connects_last_point_to_first()
    {
        //Arrange - a triangle
        var triangle = new PolylineShape(
            new[] { new PointF(500, 200), new PointF(800, 800), new PointF(200, 800) },
            strokeThickness: 40, isClosed: true);

        //Act
        using SKBitmap bitmap = RenderShape(triangle, SKColors.Red);

        //Assert - the closing edge midpoint (500, 500 between the two base corners? no:
        //  between (200,800)->(500,200) passes through (350,500) -> pixel (70,100))
        bitmap.GetPixel(70, 100).Red.Should().Be((byte)255);
    }

    [Fact]
    public void Polyline_requires_at_least_two_points()
    {
        //Arrange
        Action act = () => _ = new PolylineShape(new[] { new PointF(1, 1) });

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-3f)]
    public void Shapes_reject_non_positive_thickness(float thickness)
    {
        //Arrange
        Action act = () => _ = new LineShape(0, 0, 10, 10, thickness);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Circle_rejects_non_positive_radius()
    {
        //Arrange
        Action act = () => _ = new CircleShape(10, 10, 0);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Shapes_and_strokes_share_a_layer_and_render_in_order()
    {
        //Arrange - a stroke first, then a shape on the same layer
        using var renderer = new DrawingRenderer(Square1000)
        {
            BackgroundFillColor = Color.White,
            LayerOpacity = 255,
        };
        var layer = new DrawingLayer("Mixed", SKColors.Red);
        var stroke = new Stroke(100f);
        stroke.AddPoint(200, 500);
        stroke.AddPoint(800, 500);
        layer.AddStroke(stroke);
        layer.AddShape(new CircleShape(500, 500, 300, strokeThickness: 40));

        //Act
        var bitmap = new SKBitmap(CanvasInfo);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, CanvasInfo, new[] { layer });

        //Assert - both are visible
        layer.ElementCount.Should().Be(2);
        bitmap.GetPixel(50, 100).Red.Should().Be((byte)255);  //stroke, outside the circle
        bitmap.GetPixel(100, 40).Red.Should().Be((byte)255);  //circle rim (y=200 calibrated)
        bitmap.Dispose();
    }

    [Fact]
    public void Shape_color_exposes_both_imaging_and_skia_forms()
    {
        //Arrange - constructed with a CodeBrix.Imaging color
        var circle = new CircleShape(500, 500, 400, color: Color.Blue);

        //Act + Assert - readable as either type
        circle.Color.Should().Be(Color.Blue);
        circle.GetColorAsSkia().Should().Be(SKColors.Blue);
    }

    [Fact]
    public void Shape_without_color_reports_null_in_both_forms()
    {
        //Arrange
        var circle = new CircleShape(500, 500, 400);

        //Act + Assert
        circle.Color.Should().BeNull();
        circle.GetColorAsSkia().Should().BeNull();
    }

    [Fact]
    public void Polyline_points_round_trip_through_both_getters()
    {
        //Arrange
        var polyline = new PolylineShape(new[] { new PointF(10, 20), new PointF(30, 40) });

        //Act
        PointF[] imaging = polyline.GetPoints();
        SKPoint[] skia = polyline.GetPointsAsSkia();

        //Assert
        imaging[0].Should().Be(new PointF(10, 20));
        imaging[1].Should().Be(new PointF(30, 40));
        skia[0].Should().Be(new SKPoint(10, 20));
        skia[1].Should().Be(new SKPoint(30, 40));
    }
}
