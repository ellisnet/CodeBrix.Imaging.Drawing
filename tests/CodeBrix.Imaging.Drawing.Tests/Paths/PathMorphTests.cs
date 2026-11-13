using System;
using CodeBrix.Imaging.Drawing.Paths;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Paths;

public class PathMorphTests
{
    [Fact]
    public void GetPath_at_the_ends_returns_the_original_geometries()
    {
        //Arrange
        PathGeometry from = PathGeometry.Circle(new PointF(0, 0), 50);
        PathGeometry to = PathGeometry.RegularPolygon(new PointF(0, 0), 60, 5);
        var morph = new PathMorph(from, to);

        //Act
        PathGeometry start = morph.GetPath(0f);
        PathGeometry end = morph.GetPath(1f);
        PathGeometry before = morph.GetPath(-3f);
        PathGeometry nan = morph.GetPath(Single.NaN);

        //Assert
        start.Should().BeSameAs(from);
        end.Should().BeSameAs(to);
        before.Should().BeSameAs(from);
        nan.Should().BeSameAs(from);
    }

    [Fact]
    public void Halfway_between_two_copies_of_a_shape_is_the_shape_halfway_between_them()
    {
        //Arrange
        PathGeometry from = PathGeometry.Rectangle(new RectangleF(0, 0, 100, 100));
        PathGeometry to = PathGeometry.Rectangle(new RectangleF(200, 40, 100, 100));
        var morph = new PathMorph(from, to, 40);

        //Act
        PathGeometry middle = morph.GetPath(0.5f);

        //Assert
        middle.Contours.Count.Should().Be(1);
        middle.Contours[0].IsClosed.Should().BeTrue();
        middle.Contours[0].SegmentCount.Should().Be(40);
        middle.Contours[0].StartPoint.Should().Be(new PointF(100, 20));
        middle.GetBounds().Should().Be(new RectangleF(100, 20, 100, 100));
    }

    [Fact]
    public void Closed_contours_drawn_opposite_ways_round_are_matched_without_a_twist()
    {
        //Arrange - the same square, once clockwise and once counter-clockwise from another corner
        PathGeometry clockwise = PathGeometry.Rectangle(new RectangleF(0, 0, 100, 100));
        PathGeometry counter = PathGeometry.FromPoints(new[] { new PointF(100, 100), new PointF(100, 0), new PointF(0, 0), new PointF(0, 100) }, true);
        var morph = new PathMorph(clockwise, counter, 40);

        //Act
        PathGeometry middle = morph.GetPath(0.5f);

        //Assert - every in-between point stays on the square
        middle.GetLength().Should().BeApproximately(400f, 1e-3f);
    }

    [Fact]
    public void An_extra_contour_grows_out_of_its_own_centre()
    {
        //Arrange
        PathGeometry one = PathGeometry.Circle(new PointF(0, 0), 10);
        PathGeometry two = PathGeometry.Combine(PathGeometry.Circle(new PointF(0, 0), 10), PathGeometry.Circle(new PointF(100, 0), 20));
        var morph = new PathMorph(one, two, 32);

        //Act
        PathGeometry middle = morph.GetPath(0.5f);

        //Assert
        morph.PairCount.Should().Be(2);
        middle.Contours.Count.Should().Be(2);
        RectangleF grown = middle.Contours.Count > 1 ? new PathGeometry(new[] { middle.Contours[1] }).GetBounds() : RectangleF.Empty;
        (grown.X + (grown.Width / 2)).Should().BeApproximately(100f, 0.5f);
        grown.Width.Should().BeApproximately(20f, 0.5f);
    }

    [Fact]
    public void Open_contours_are_matched_end_to_end_choosing_the_closer_direction()
    {
        //Arrange
        PathGeometry from = PathGeometry.Line(new PointF(0, 0), new PointF(100, 0));
        PathGeometry to = PathGeometry.Line(new PointF(100, 10), new PointF(0, 10));
        var morph = new PathMorph(from, to, 11);

        //Act
        PathGeometry middle = morph.GetPath(0.5f);

        //Assert - (0,0) pairs with (0,10), not with (100,10)
        middle.Contours[0].IsClosed.Should().BeFalse();
        middle.Contours[0].StartPoint.Should().Be(new PointF(0, 5));
        middle.Contours[0].EndPoint.Should().Be(new PointF(100, 5));
    }

    [Fact]
    public void An_open_contour_morphs_with_a_closed_one_into_an_open_in_between()
    {
        //Arrange
        PathGeometry line = PathGeometry.Line(new PointF(-50, 0), new PointF(50, 0));
        PathGeometry circle = PathGeometry.Circle(new PointF(0, 0), 50);
        var morph = new PathMorph(line, circle, 64);

        //Act
        PathGeometry middle = morph.GetPath(0.5f);

        //Assert
        middle.Contours[0].IsClosed.Should().BeFalse();
        middle.Contours[0].SegmentCount.Should().Be(64);
    }

    [Fact]
    public void Constructor_rejects_bad_arguments()
    {
        //Act
        Action nullFrom = () => _ = new PathMorph(null, PathGeometry.Empty);
        Action one = () => _ = new PathMorph(PathGeometry.Empty, PathGeometry.Empty, 1);

        //Assert
        nullFrom.Should().Throw<ArgumentNullException>();
        one.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Stroke_style_validates_and_copies_its_dash_pattern()
    {
        //Arrange
        var pattern = new[] { 4f, 2f };

        //Act
        var style = new PathStrokeStyle(3f, PathStrokeCap.Butt, PathStrokeJoin.Miter, 6f, pattern, 1f);
        pattern[0] = 99f;
        Action zeroWidth = () => _ = new PathStrokeStyle(0f);
        Action lowMiter = () => _ = new PathStrokeStyle(1f, miterLimit: 0.5f);
        Action badDash = () => _ = new PathStrokeStyle(1f, dashPattern: new[] { -1f, 2f });

        //Assert
        style.IsDashed.Should().BeTrue();
        style.GetDashPattern().Should().Equal(4f, 2f);
        style.WithWidth(5f).Width.Should().Be(5f);
        style.WithDashOffset(3f).DashOffset.Should().Be(3f);
        new PathStrokeStyle(2f).GetDashPattern().Should().BeEmpty();
        zeroWidth.Should().Throw<ArgumentOutOfRangeException>();
        lowMiter.Should().Throw<ArgumentOutOfRangeException>();
        badDash.Should().Throw<ArgumentException>();
    }
}
