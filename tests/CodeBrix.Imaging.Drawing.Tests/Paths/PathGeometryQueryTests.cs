using System;
using CodeBrix.Imaging.Drawing.Paths;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Paths;

public class PathGeometryQueryTests
{
    private static readonly PathGeometry Square = PathGeometry.Rectangle(new RectangleF(0, 0, 100, 100));

    //Two nested squares: a frame under even-odd, a solid square under non-zero (same direction)
    private static PathGeometry NestedSquares(PathFillRule rule)
        => new PathGeometryBuilder()
            .AddRectangle(new RectangleF(0, 0, 100, 100))
            .AddRectangle(new RectangleF(25, 25, 50, 50))
            .Build().WithFillRule(rule);

    [Fact]
    public void Contains_answers_inside_and_outside_a_square()
    {
        //Assert
        Square.Contains(new PointF(50, 50)).Should().BeTrue();
        Square.Contains(new PointF(1, 99)).Should().BeTrue();
        Square.Contains(new PointF(-1, 50)).Should().BeFalse();
        Square.Contains(new PointF(50, 101)).Should().BeFalse();
        PathGeometry.Empty.Contains(new PointF(0, 0)).Should().BeFalse();
    }

    [Fact]
    public void Contains_follows_the_geometry_fill_rule()
    {
        //Arrange
        PathGeometry frame = NestedSquares(PathFillRule.EvenOdd);
        PathGeometry solid = NestedSquares(PathFillRule.NonZero);

        //Assert - the centre is a hole only under even-odd
        frame.Contains(new PointF(50, 50)).Should().BeFalse();
        frame.Contains(new PointF(10, 10)).Should().BeTrue();
        solid.Contains(new PointF(50, 50)).Should().BeTrue();
    }

    [Fact]
    public void Contains_treats_an_open_contour_as_closed_like_a_fill()
    {
        //Arrange - a triangle without its closing line
        PathGeometry open = PathGeometry.FromPoints(new[] { new PointF(0, 0), new PointF(100, 0), new PointF(0, 100) }, closed: false);

        //Assert
        open.Contains(new PointF(20, 20)).Should().BeTrue();
        open.Contains(new PointF(80, 80)).Should().BeFalse();
    }

    [Fact]
    public void DistanceToEdge_measures_to_the_nearest_edge_inside_and_out()
    {
        //Assert
        Square.DistanceToEdge(new PointF(50, 50)).Should().Be(50f);
        Square.DistanceToEdge(new PointF(10, 40)).Should().Be(10f);
        Square.DistanceToEdge(new PointF(103, 104)).Should().Be(5f);
        Square.DistanceToEdge(new PointF(100, 30)).Should().BeApproximately(0f, 1e-5f);
        Single.IsPositiveInfinity(PathGeometry.Empty.DistanceToEdge(new PointF(0, 0))).Should().BeTrue();
    }

    [Fact]
    public void DistanceToEdge_of_a_circle_is_within_the_tolerance_of_the_true_distance()
    {
        //Arrange
        PathGeometry circle = PathGeometry.Circle(new PointF(0, 0), 100);

        //Act
        float coarse = circle.DistanceToEdge(new PointF(0, 0), 2f);
        float fine = circle.DistanceToEdge(new PointF(0, 0), 0.05f);

        //Assert - chords lie inside the circle by at most the tolerance
        coarse.Should().BeInRange(98f, 100f);
        fine.Should().BeInRange(99.95f, 100f);
    }

    [Fact]
    public void GetSignedDistance_is_negative_inside_and_positive_outside()
    {
        //Assert
        Square.GetSignedDistance(new PointF(50, 40)).Should().Be(-40f);
        Square.GetSignedDistance(new PointF(50, 130)).Should().Be(30f);
    }

    [Fact]
    public void GetNearestPoint_lies_on_the_drawn_path_and_leaves_open_contours_open()
    {
        //Arrange - the top and right sides only: no line back from (100, 100) to (0, 0)
        PathGeometry corner = PathGeometry.FromPoints(new[] { new PointF(0, 0), new PointF(100, 0), new PointF(100, 100) }, closed: false);

        //Act
        PointF nearTop = corner.GetNearestPoint(new PointF(40, -20));
        PointF nearMissingLine = corner.GetNearestPoint(new PointF(30, 60));

        //Assert
        nearTop.Should().Be(new PointF(40, 0));
        nearMissingLine.X.Should().BeApproximately(30f, 1e-4f);
        nearMissingLine.Y.Should().Be(0f);
        Square.GetNearestPoint(new PointF(-5, 30)).Should().Be(new PointF(0, 30));
    }

    [Fact]
    public void GetNearestPoint_of_an_empty_geometry_throws()
    {
        //Act
        Action act = () => PathGeometry.Empty.GetNearestPoint(new PointF(1, 1));

        //Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Distance_between_separate_areas_is_the_gap_between_their_nearest_points()
    {
        //Arrange
        PathGeometry right = PathGeometry.Rectangle(new RectangleF(130, 40, 50, 50));
        PathGeometry below = PathGeometry.Rectangle(new RectangleF(140, 140, 10, 10));

        //Assert
        Square.Distance(right).Should().Be(30f);
        right.Distance(Square).Should().Be(30f);
        Square.Distance(below).Should().Be(MathF.Sqrt((40 * 40) + (40 * 40)));
    }

    [Fact]
    public void Distance_is_zero_for_overlap_containment_and_crossing_edges()
    {
        //Arrange
        PathGeometry inside = PathGeometry.Rectangle(new RectangleF(40, 40, 10, 10));
        PathGeometry wide = PathGeometry.Rectangle(new RectangleF(-50, 40, 200, 10));
        PathGeometry tall = PathGeometry.Rectangle(new RectangleF(45, -50, 10, 200));

        //Assert - a plus sign: no corner of either lies inside the other, yet they cross
        Square.Distance(inside).Should().Be(0f);
        inside.Distance(Square).Should().Be(0f);
        wide.Distance(tall).Should().Be(0f);
        Single.IsPositiveInfinity(Square.Distance(PathGeometry.Empty)).Should().BeTrue();
    }

    [Fact]
    public void Distance_with_an_offset_equals_the_distance_to_the_moved_geometry()
    {
        //Arrange
        PathGeometry other = PathGeometry.Rectangle(new RectangleF(0, 0, 40, 40));

        //Act
        float shifted = Square.Distance(other, new PointF(125, 0));
        float moved = Square.Distance(other.Transformed(System.Numerics.Matrix3x2.CreateTranslation(125, 0)));

        //Assert
        shifted.Should().Be(25f);
        moved.Should().Be(shifted);
    }

    [Fact]
    public void Queries_reject_a_tolerance_that_is_not_positive()
    {
        //Act
        Action zero = () => Square.Contains(new PointF(1, 1), 0f);
        Action nan = () => Square.DistanceToEdge(new PointF(1, 1), Single.NaN);
        Action badOffset = () => Square.Distance(Square, new PointF(Single.PositiveInfinity, 0));

        //Assert
        zero.Should().Throw<ArgumentOutOfRangeException>();
        nan.Should().Throw<ArgumentOutOfRangeException>();
        badOffset.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetPointAt_extending_beyond_the_ends_continues_straight_along_the_end_directions()
    {
        //Arrange - 200 long, heading right
        PathGeometry line = PathGeometry.Line(new PointF(0, 10), new PointF(200, 10));

        //Act
        PathPoint before = line.GetPointAt(-0.25f, extendBeyondEnds: true);
        PathPoint after = line.GetPointAt(1.1f, extendBeyondEnds: true);
        PathPoint clamped = line.GetPointAt(1.1f, extendBeyondEnds: false);
        PathPoint byDistance = line.GetPointAtDistance(230f, extendBeyondEnds: true);

        //Assert
        before.Position.Should().Be(new PointF(-50, 10));
        before.Distance.Should().Be(-50f);
        after.Position.X.Should().BeApproximately(220f, 1e-3f);
        after.Distance.Should().BeApproximately(220f, 1e-3f);
        clamped.Position.Should().Be(new PointF(200, 10));
        byDistance.Position.Should().Be(new PointF(230, 10));
        line.GetPointAt(0.5f, extendBeyondEnds: true).Should().Be(line.GetPointAt(0.5f));
    }

    [Fact]
    public void GetPointAt_extending_a_curve_follows_its_last_tangent()
    {
        //Arrange - a quarter turn ending heading down
        PathGeometry turn = new PathGeometryBuilder().MoveTo(new PointF(0, 0)).QuadraticTo(new PointF(100, 0), new PointF(100, 100)).Build();
        float length = turn.GetLength();

        //Act
        PathPoint end = turn.GetPointAt(1f);
        PathPoint past = turn.GetPointAtDistance(length + 30f, extendBeyondEnds: true);

        //Assert
        past.Tangent.Should().Be(end.Tangent);
        past.Position.X.Should().BeApproximately(end.Position.X + (end.Tangent.X * 30f), 1e-3f);
        past.Position.Y.Should().BeApproximately(end.Position.Y + (end.Tangent.Y * 30f), 1e-3f);
    }
}
