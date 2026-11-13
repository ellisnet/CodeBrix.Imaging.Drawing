using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class ShapeOutlineTests
{
    private sealed class OpaqueShape : DrawingShape
    {
        public OpaqueShape() : base(1f, (Color?)null) { }

        public override void Draw(SKCanvas canvas, SKColor color) { }
    }

    [Fact]
    public void Simple_shapes_report_their_geometry()
    {
        //Act
        PathGeometry line = new LineShape(1, 2, 31, 42).GetOutline();
        PathGeometry circle = new CircleShape(50, 50, 10).GetOutline();
        PathGeometry ellipse = new EllipseShape(50, 50, 20, 10).GetOutline();
        PathGeometry rectangle = new RectangleShape(10, 20, 30, 40).GetOutline();
        PathGeometry rounded = new RectangleShape(10, 20, 30, 40, cornerRadius: 5).GetOutline();
        PathGeometry polyline = new PolylineShape(new[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10) }, isClosed: true).GetOutline();

        //Assert
        line.GetLength().Should().Be(50f);
        circle.GetBounds().Width.Should().BeApproximately(20f, 1e-4f);
        ellipse.GetBounds().Height.Should().BeApproximately(20f, 1e-4f);
        rectangle.GetBounds().Should().Be(new RectangleF(10, 20, 30, 40));
        rectangle.Contours[0].IsClosed.Should().BeTrue();
        rounded.Contours[0].SegmentCount.Should().Be(8);
        polyline.Contours[0].IsClosed.Should().BeTrue();
        polyline.GetLength().Should().BeApproximately(34.142136f, 1e-4f);
    }

    [Fact]
    public void Arrow_outline_is_the_shaft_and_the_head()
    {
        //Act
        PathGeometry arrow = new ArrowShape(0, 0, 100, 0, headLength: 30).GetOutline();

        //Assert
        arrow.Contours.Count.Should().Be(2);
        arrow.Contours[0].EndPoint.Should().Be(new PointF(100, 0));
        arrow.Contours[1][0].End.Should().Be(new PointF(100, 0));
        arrow.Contours[1].GetSegments()[1].End.X.Should().BeLessThan(100f);
    }

    [Fact]
    public void Text_outline_is_its_glyphs_inside_its_bounds()
    {
        //Arrange
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.OpenSansPath);
        var text = new TextShape("Ab", 100, 200, 50, fonts);

        //Act
        PathGeometry outline = text.GetOutline();

        //Assert
        outline.Contours.Count.Should().BeGreaterThan(1);
        RectangleF bounds = outline.GetBounds();
        RectangleF reported = text.GetBounds().Value;
        bounds.Left.Should().BeGreaterThanOrEqualTo(reported.Left - 0.001f);
        bounds.Right.Should().BeLessThanOrEqualTo(reported.Right + 0.001f);
        bounds.Bottom.Should().BeLessThanOrEqualTo(reported.Bottom + 0.001f);
    }

    [Fact]
    public void Group_and_animated_shape_outlines_pass_through_child_transforms()
    {
        //Arrange
        var group = new DrawingGroup();
        group.Add(new RectangleShape(0, 0, 10, 10)).Transform = DrawingTransform.Translate(100, 0);
        group.Add(new LineShape(0, 0, 0, 5));
        var animated = new AnimatedShape(t => new CircleShape(0, 0, 5) { Transform = DrawingTransform.Translate(50, 50) });

        //Act
        PathGeometry groupOutline = group.GetOutline();
        PathGeometry animatedOutline = animated.GetOutline();
        group.Add(new OpaqueShape());

        //Assert
        groupOutline.Contours.Count.Should().Be(2);
        groupOutline.Contours[0].StartPoint.Should().Be(new PointF(100, 0));
        animatedOutline.GetBounds().X.Should().BeApproximately(45f, 1e-4f);
        group.GetOutline().Should().BeNull();
        new DrawingGroup().GetOutline().IsEmpty.Should().BeTrue();
        new OpaqueShape().GetOutline().Should().BeNull();
    }

    [Fact]
    public void Element_transformed_outline_and_stroke_outline()
    {
        //Arrange
        var circle = new CircleShape(0, 0, 10) { Transform = new DrawingTransform(new PointF(100, 0), 0, 2, 2) };
        var stroke = new Stroke(8f);
        stroke.AddPoint(0, 0);
        var dot = stroke.GetOutline();
        stroke.AddPoint(30, 40);

        //Act
        PathGeometry transformed = circle.GetTransformedOutline();
        PathGeometry line = stroke.GetOutline();

        //Assert
        transformed.GetBounds().Width.Should().BeApproximately(40f, 1e-3f);
        transformed.GetBounds().X.Should().BeApproximately(80f, 1e-3f);
        dot.GetBounds().Width.Should().BeApproximately(8f, 1e-4f);
        line.GetLength().Should().Be(50f);
        new Stroke().GetOutline().Should().BeNull();
    }

    [Fact]
    public void Particles_outline_is_a_circle_per_live_particle()
    {
        //Arrange
        var system = new DrawingParticleSystem(1);
        system.AddEmitter(new DrawingParticleEmitter { X = 10, Y = 10, Burst = 3, Lifetime = 5, Speed = 0, StartSize = 4, EndSize = 4 });

        //Act
        PathGeometry outline = new ParticlesShape(system, 1).GetOutline();

        //Assert
        outline.Contours.Count.Should().Be(3);
    }

    [Fact]
    public void Outlines_flatten_to_polygons_for_polygon_work()
    {
        //Arrange
        PathGeometry outline = new CircleShape(0, 0, 100).GetOutline();

        //Act
        IReadOnlyList<PathPolyline> polygons = outline.Flatten(0.5f);

        //Assert
        polygons.Count.Should().Be(1);
        polygons[0].IsClosed.Should().BeTrue();
        polygons[0].Count.Should().BeGreaterThan(16);
    }
}
