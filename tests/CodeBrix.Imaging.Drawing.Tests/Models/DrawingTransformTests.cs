using System;
using CodeBrix.Imaging.Drawing.Models;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class DrawingTransformTests
{
    private static void ShouldBeNear(PointF actual, float x, float y)
    {
        actual.X.Should().BeApproximately(x, 1e-3f);
        actual.Y.Should().BeApproximately(y, 1e-3f);
    }

    [Fact]
    public void Identity_changes_nothing()
    {
        //Arrange
        DrawingTransform identity = DrawingTransform.Identity;
        var bounds = new RectangleF(1.5f, 2.25f, 10, 20);

        //Act + Assert
        identity.IsIdentity.Should().BeTrue();
        identity.TransformPoint(new PointF(3, 4)).Should().Be(new PointF(3, 4));
        identity.TransformBounds(bounds).Should().Be(bounds);
        new DrawingTransform(new PointF(0, 0), anchor: new PointF(50, 50)).IsIdentity.Should().BeTrue();
    }

    [Fact]
    public void TransformPoint_scales_and_rotates_about_the_anchor_then_moves()
    {
        //Arrange - scale 2 about (10, 10), turn 90 degrees clockwise, then move (100, 0)
        var transform = new DrawingTransform(new PointF(100, 0), 90f, 2f, 2f, new PointF(10, 10));

        //Act
        PointF moved = transform.TransformPoint(new PointF(20, 10));
        PointF anchor = transform.TransformPoint(new PointF(10, 10));

        //Assert - (20,10) is 10 right of the anchor: scaled to 20 right, turned to 20 below
        ShouldBeNear(moved, 110, 30);
        ShouldBeNear(anchor, 110, 10);
    }

    [Fact]
    public void TransformBounds_covers_the_rotated_corners()
    {
        //Arrange
        DrawingTransform transform = DrawingTransform.Rotate(45f, new PointF(0, 0));

        //Act
        RectangleF bounds = transform.TransformBounds(new RectangleF(0, 0, 10, 10));

        //Assert
        bounds.Width.Should().BeApproximately(14.142f, 1e-3f);
        bounds.Height.Should().BeApproximately(14.142f, 1e-3f);
        bounds.X.Should().BeApproximately(-7.071f, 1e-3f);
    }

    [Fact]
    public void With_methods_change_one_part()
    {
        //Arrange
        DrawingTransform start = DrawingTransform.Translate(5, 6);

        //Act
        DrawingTransform changed = start.WithRotation(30).WithScale(2, 3).WithAnchor(new PointF(1, 1));

        //Assert
        changed.Translation.Should().Be(new PointF(5, 6));
        changed.Rotation.Should().Be(30f);
        changed.ScaleX.Should().Be(2f);
        changed.ScaleY.Should().Be(3f);
        changed.Anchor.Should().Be(new PointF(1, 1));
        start.Rotation.Should().Be(0f);
    }

    [Fact]
    public void Equality_is_by_value()
    {
        //Arrange
        DrawingTransform a = DrawingTransform.Scale(2f, new PointF(4, 4));
        DrawingTransform b = DrawingTransform.Scale(2f, new PointF(4, 4));

        //Act + Assert
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
        (a != DrawingTransform.Identity).Should().BeTrue();
    }

    [Fact]
    public void Constructor_rejects_values_that_are_not_finite()
    {
        //Arrange
        Action act = () => _ = new DrawingTransform(new PointF(float.NaN, 0));

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Element_bounds_follow_the_element_transform()
    {
        //Arrange
        var circle = new CodeBrix.Imaging.Drawing.Shapes.CircleShape(10, 10, 5, 1, null, isFilled: true);

        //Act
        RectangleF? plain = circle.GetTransformedBounds();
        circle.Transform = DrawingTransform.Translate(100, 50);
        RectangleF? moved = circle.GetTransformedBounds();

        //Assert
        plain.Should().Be(circle.GetBounds());
        moved.Should().Be(new RectangleF(105, 55, 10, 10));
    }
}
