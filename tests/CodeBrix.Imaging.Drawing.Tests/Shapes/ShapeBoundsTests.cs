using System;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class ShapeBoundsTests
{
    private sealed class UnboundedShape : DrawingShape
    {
        public UnboundedShape() : base(10f, (Color?)null) { }

        public override void Draw(SKCanvas canvas, SKColor color) { }
    }

    private static void ShouldBe(RectangleF? actual, float x, float y, float width, float height)
    {
        actual.HasValue.Should().BeTrue();
        actual.Value.X.Should().BeApproximately(x, 1e-3f);
        actual.Value.Y.Should().BeApproximately(y, 1e-3f);
        actual.Value.Width.Should().BeApproximately(width, 1e-3f);
        actual.Value.Height.Should().BeApproximately(height, 1e-3f);
    }

    [Fact]
    public void Custom_shapes_report_unknown_bounds_by_default()
    {
        //Act + Assert
        new UnboundedShape().GetBounds().Should().BeNull();
    }

    [Fact]
    public void Line_bounds_include_half_the_thickness()
    {
        //Act + Assert
        ShouldBe(new LineShape(100, 200, 300, 200, 20).GetBounds(), 90, 190, 220, 20);
    }

    [Fact]
    public void Arrow_bounds_include_the_head()
    {
        //Arrange - pointing right, so the head's arms reach back and up/down from the tip
        var arrow = new ArrowShape(100, 500, 400, 500, 10, headLength: 60);

        //Act
        RectangleF bounds = arrow.GetBounds().Value;

        //Assert - arms at 30 degrees: 60 * sin(30) = 30 above and below the shaft
        bounds.Top.Should().BeApproximately(500 - 30 - 5, 1e-3f);
        bounds.Bottom.Should().BeApproximately(500 + 30 + 5, 1e-3f);
        bounds.Left.Should().BeApproximately(95, 1e-3f);
        bounds.Right.Should().BeApproximately(405, 1e-3f);
    }

    [Fact]
    public void Circle_and_ellipse_bounds_grow_by_half_the_outline_only_when_outlined()
    {
        //Act + Assert
        ShouldBe(new CircleShape(500, 400, 100, 20).GetBounds(), 390, 290, 220, 220);
        ShouldBe(new CircleShape(500, 400, 100, 20, isFilled: true).GetBounds(), 400, 300, 200, 200);
        ShouldBe(new EllipseShape(500, 400, 100, 50, 10).GetBounds(), 395, 345, 210, 110);
        ShouldBe(new EllipseShape(500, 400, 100, 50, 10, isFilled: true).GetBounds(), 400, 350, 200, 100);
    }

    [Fact]
    public void Rectangle_and_polyline_bounds_cover_their_geometry()
    {
        //Arrange
        var fill = new LinearGradientFill(new PointF(0, 0), new PointF(1, 0), Color.Red, Color.Blue);
        var points = new[] { new PointF(10, 50), new PointF(80, 20), new PointF(60, 90) };

        //Act + Assert
        ShouldBe(new RectangleShape(10, 20, 100, 50, 4).GetBounds(), 8, 18, 104, 54);
        ShouldBe(new RectangleShape(10, 20, 100, 50, fill).GetBounds(), 10, 20, 100, 50);
        ShouldBe(new PolylineShape(points, 6).GetBounds(), 7, 17, 76, 76);
        ShouldBe(new PolylineShape(points, fill).GetBounds(), 10, 20, 70, 70);
    }

    [Fact]
    public void Stroke_bounds_cover_its_points_and_width()
    {
        //Arrange
        var stroke = new Stroke(30f);
        stroke.AddPoint(100, 100);
        stroke.AddPoint(200, 150);
        var dot = new Stroke(10f);
        dot.AddPoint(50, 60);

        //Act + Assert
        ShouldBe(stroke.GetBounds(), 85, 85, 130, 80);
        ShouldBe(dot.GetBounds(), 45, 55, 10, 10);
        new Stroke().GetBounds().Should().BeNull();
    }

    [Fact]
    public void Safe_area_flags_default_off_and_are_settable()
    {
        //Arrange
        var line = new LineShape(0, 0, 1, 1);
        var layer = new DrawingLayer("L", Color.Red);

        //Act
        bool before = line.IsSafeAreaChecked || layer.IsSafeAreaChecked;
        line.IsSafeAreaChecked = true;
        layer.IsSafeAreaChecked = true;

        //Assert
        before.Should().BeFalse();
        line.IsSafeAreaChecked.Should().BeTrue();
        layer.IsSafeAreaChecked.Should().BeTrue();
    }
}
