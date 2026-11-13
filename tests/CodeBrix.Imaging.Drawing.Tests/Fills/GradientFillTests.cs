using System;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Fills;

public class GradientFillTests
{
    private static readonly Size Square1000 = new Size(1000, 1000);

    private static byte[] RenderShape(DrawingShape shape, bool includeBackground = true)
    {
        using var renderer = new DrawingRenderer(Square1000) { BackgroundFillColor = Color.White, LayerOpacity = 255 };
        var layer = new DrawingLayer("Fills", Color.Black);
        layer.AddShape(shape);
        var pixels = new byte[100 * 100 * 4];
        renderer.RenderToPixels(pixels, new Size(100, 100), new[] { layer }, includeBackground);
        return pixels;
    }

    private static void ShouldBeNear(Rgba32 actual, Rgba32 expected, int tolerance)
    {
        Math.Abs(actual.R - expected.R).Should().BeLessThanOrEqualTo(tolerance);
        Math.Abs(actual.G - expected.G).Should().BeLessThanOrEqualTo(tolerance);
        Math.Abs(actual.B - expected.B).Should().BeLessThanOrEqualTo(tolerance);
        Math.Abs(actual.A - expected.A).Should().BeLessThanOrEqualTo(tolerance);
    }

    [Fact]
    public void Linear_gradient_rectangle_runs_from_start_color_to_end_color()
    {
        //Arrange
        var fill = new LinearGradientFill(new PointF(0, 0), new PointF(1000, 0), Color.Red, Color.Blue);

        //Act
        byte[] pixels = RenderShape(new RectangleShape(0, 0, 1000, 1000, fill));

        //Assert
        ShouldBeNear(TestAssets.PixelAt(pixels, 100, 0, 50), new Rgba32(255, 0, 0, 255), 4);
        ShouldBeNear(TestAssets.PixelAt(pixels, 100, 50, 50), new Rgba32(128, 0, 127, 255), 4);
        ShouldBeNear(TestAssets.PixelAt(pixels, 100, 99, 50), new Rgba32(0, 0, 255, 255), 4);
    }

    [Fact]
    public void Radial_gradient_circle_runs_from_center_outward()
    {
        //Arrange
        var fill = new RadialGradientFill(new PointF(500, 500), 400, Color.White, Color.Black);

        //Act
        byte[] pixels = RenderShape(new CircleShape(500, 500, 400, fill));

        //Assert - white at the center, near black just inside the edge, the page outside
        ShouldBeNear(TestAssets.PixelAt(pixels, 100, 50, 50), new Rgba32(255, 255, 255, 255), 6);
        ShouldBeNear(TestAssets.PixelAt(pixels, 100, 50, 88), new Rgba32(13, 13, 13, 255), 16);
        ShouldBeNear(TestAssets.PixelAt(pixels, 100, 2, 2), new Rgba32(255, 255, 255, 255), 0);
    }

    [Fact]
    public void Fill_with_translucent_stops_keeps_alpha_over_transparency()
    {
        //Arrange - a gradient from opaque red to fully transparent red, no background
        var fill = new LinearGradientFill(new PointF(0, 0), new PointF(1000, 0),
            Color.FromRgba(255, 0, 0, 255), Color.FromRgba(255, 0, 0, 0));

        //Act
        byte[] pixels = RenderShape(new RectangleShape(0, 0, 1000, 1000, fill), includeBackground: false);

        //Assert - straight alpha: the color stays red while the alpha falls
        Rgba32 left = TestAssets.PixelAt(pixels, 100, 1, 50);
        Rgba32 middle = TestAssets.PixelAt(pixels, 100, 50, 50);
        Rgba32 right = TestAssets.PixelAt(pixels, 100, 99, 50);
        left.A.Should().BeGreaterThan((byte)240);
        Math.Abs(middle.A - 128).Should().BeLessThanOrEqualTo(4);
        middle.R.Should().BeGreaterThan((byte)245);
        right.A.Should().BeLessThan((byte)8);
    }

    [Theory]
    [InlineData(GradientSpread.Pad)]
    [InlineData(GradientSpread.Repeat)]
    [InlineData(GradientSpread.Reflect)]
    public void Spread_controls_the_area_beyond_the_end_stop(GradientSpread spread)
    {
        //Arrange - the gradient covers only the left fifth of the rectangle
        var fill = new LinearGradientFill(new PointF(0, 0), new PointF(200, 0),
            new[] { new ColorStop(0f, Color.Black), new ColorStop(1f, Color.White) }, spread);

        //Act
        byte[] pixels = RenderShape(new RectangleShape(0, 0, 1000, 1000, fill));
        Rgba32 beyond = TestAssets.PixelAt(pixels, 100, 25, 50); //calibrated x = 255, 27.5% into the second period

        //Assert
        switch (spread)
        {
            case GradientSpread.Pad:
                beyond.R.Should().BeGreaterThan((byte)250);
                break;
            case GradientSpread.Repeat:
                Math.Abs(beyond.R - 70).Should().BeLessThanOrEqualTo(14);
                break;
            default:
                Math.Abs(beyond.R - 185).Should().BeLessThanOrEqualTo(14);
                break;
        }
    }

    [Fact]
    public void Filled_shapes_without_a_fill_render_exactly_as_before()
    {
        //Arrange
        var plain = new CircleShape(500, 500, 300, 15, Color.Red, isFilled: true);

        //Act
        byte[] pixels = RenderShape(plain);

        //Assert
        plain.Fill.Should().BeNull();
        TestAssets.PixelAt(pixels, 100, 50, 50).Should().Be(new Rgba32(255, 0, 0, 255));
    }

    [Fact]
    public void Every_fillable_shape_accepts_a_fill()
    {
        //Arrange
        var fill = new LinearGradientFill(new PointF(0, 0), new PointF(0, 1000), Color.Lime, Color.Lime);
        DrawingShape[] shapes =
        {
            new CircleShape(500, 500, 300, fill),
            new EllipseShape(500, 500, 300, 200, fill),
            new RectangleShape(200, 200, 600, 600, fill, cornerRadius: 40),
            new PolylineShape(new[] { new PointF(200, 200), new PointF(800, 200), new PointF(500, 800) }, fill),
        };

        //Act + Assert
        foreach (DrawingShape shape in shapes)
        {
            shape.Fill.Should().BeSameAs(fill);
            TestAssets.PixelAt(RenderShape(shape), 100, 50, 40).Should().Be(new Rgba32(0, 255, 0, 255));
        }
    }

    [Fact]
    public void Constructors_validate_their_arguments()
    {
        //Arrange
        Action samePoints = () => _ = new LinearGradientFill(new PointF(5, 5), new PointF(5, 5), Color.Red, Color.Blue);
        Action zeroRadius = () => _ = new RadialGradientFill(new PointF(5, 5), 0, Color.Red, Color.Blue);
        Action oneStop = () => _ = new RadialGradientFill(new PointF(5, 5), 10, new[] { new ColorStop(0, Color.Red) });
        Action badSpread = () => _ = new LinearGradientFill(new PointF(0, 0), new PointF(1, 0),
            new[] { new ColorStop(0, Color.Red), new ColorStop(1, Color.Blue) }, (GradientSpread)9);
        Action nullFill = () => _ = new CircleShape(1, 1, 1, (ShapeFill)null);

        //Act + Assert
        samePoints.Should().Throw<ArgumentException>();
        zeroRadius.Should().Throw<ArgumentOutOfRangeException>();
        oneStop.Should().Throw<ArgumentException>();
        badSpread.Should().Throw<ArgumentOutOfRangeException>();
        nullFill.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetStops_returns_a_copy_in_order()
    {
        //Arrange
        var fill = new RadialGradientFill(new PointF(0, 0), 10,
            new[] { new ColorStop(0f, Color.Red), new ColorStop(0.4f, Color.Green), new ColorStop(1f, Color.Blue) });

        //Act
        ColorStop[] stops = fill.GetStops();

        //Assert
        fill.StopCount.Should().Be(3);
        stops[1].Position.Should().Be(0.4f);
        fill.Spread.Should().Be(GradientSpread.Pad);
    }
}
