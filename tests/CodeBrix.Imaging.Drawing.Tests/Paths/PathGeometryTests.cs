using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Paths;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Paths;

public class PathGeometryTests
{
    private static PathGeometry Square() => PathGeometry.Rectangle(new RectangleF(0, 0, 100, 100));

    [Fact]
    public void Builder_records_contours_segments_and_closing()
    {
        //Arrange
        var builder = new PathGeometryBuilder { FillRule = PathFillRule.EvenOdd };

        //Act
        PathGeometry geometry = builder.MoveTo(10, 10).LineTo(50, 10).QuadraticTo(new PointF(70, 30), new PointF(50, 50))
            .CubicTo(new PointF(40, 60), new PointF(20, 60), new PointF(10, 50)).Close()
            .MoveTo(100, 100).LineTo(150, 100).Build();

        //Assert
        geometry.Contours.Count.Should().Be(2);
        geometry.FillRule.Should().Be(PathFillRule.EvenOdd);
        geometry.Contours[0].IsClosed.Should().BeTrue();
        geometry.Contours[0].SegmentCount.Should().Be(3);
        geometry.Contours[0][1].Kind.Should().Be(PathSegmentKind.Quadratic);
        geometry.Contours[0][2].Kind.Should().Be(PathSegmentKind.Cubic);
        geometry.Contours[1].IsClosed.Should().BeFalse();
        geometry.Contours[1].EndPoint.Should().Be(new PointF(150, 100));
    }

    [Fact]
    public void Contour_constructor_rejects_segments_that_do_not_chain()
    {
        //Arrange
        var segments = new[] { PathSegment.Line(new PointF(0, 0), new PointF(10, 0)), PathSegment.Line(new PointF(11, 0), new PointF(20, 0)) };

        //Act
        Action act = () => _ = new PathContour(new PointF(0, 0), segments, false);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetLength_of_lines_is_exact_and_closed_contours_include_the_closing_line()
    {
        //Arrange
        PathGeometry open = PathGeometry.FromPoints(new[] { new PointF(0, 0), new PointF(30, 40), new PointF(30, 100) }, false);

        //Act
        float openLength = open.GetLength();
        float squareLength = Square().GetLength();

        //Assert
        openLength.Should().Be(110f);
        squareLength.Should().Be(400f);
    }

    [Fact]
    public void GetLength_of_a_circle_is_within_the_measuring_tolerance_of_two_pi_r()
    {
        //Arrange
        PathGeometry circle = PathGeometry.Circle(new PointF(0, 0), 100);

        //Act
        float length = circle.GetLength();

        //Assert - the cubic approximation of a circle is about 0.03% long; the chords a little short
        Math.Abs(length - (2 * Math.PI * 100)).Should().BeLessThan(0.2);
    }

    [Fact]
    public void GetPointAt_walks_the_path_by_length_with_unit_tangents()
    {
        //Arrange
        PathGeometry square = Square();

        //Act
        PathPoint start = square.GetPointAt(0f);
        PathPoint corner = square.GetPointAt(0.25f);
        PathPoint middleOfLeft = square.GetPointAt(0.875f);
        PathPoint end = square.GetPointAt(1f);
        PathPoint clamped = square.GetPointAt(7f);

        //Assert
        start.Position.Should().Be(new PointF(0, 0));
        start.Tangent.Should().Be(new PointF(1, 0));
        start.Angle.Should().Be(0f);
        corner.Position.Should().Be(new PointF(100, 0));
        corner.Tangent.Should().Be(new PointF(0, 1));
        corner.Angle.Should().Be(90f);
        middleOfLeft.Position.Should().Be(new PointF(0, 50));
        middleOfLeft.Angle.Should().Be(-90f);
        middleOfLeft.Distance.Should().Be(350f);
        end.Position.Should().Be(new PointF(0, 0));
        clamped.Should().Be(end);
    }

    [Fact]
    public void GetPointAtDistance_lies_on_the_curve_and_continues_on_the_next_contour()
    {
        //Arrange
        PathGeometry geometry = PathGeometry.Combine(PathGeometry.Circle(new PointF(0, 0), 50), PathGeometry.Line(new PointF(200, 0), new PointF(300, 0)));
        float circleLength = geometry.Contours[0].Length;

        //Act
        PathPoint onCircle = geometry.GetPointAtDistance(circleLength * 0.3f);
        PathPoint onLine = geometry.GetPointAtDistance(circleLength + 10f);

        //Assert
        float radius = MathF.Sqrt((onCircle.Position.X * onCircle.Position.X) + (onCircle.Position.Y * onCircle.Position.Y));
        Math.Abs(radius - 50f).Should().BeLessThan(0.02f);
        onCircle.ContourIndex.Should().Be(0);
        onLine.ContourIndex.Should().Be(1);
        onLine.Position.X.Should().BeApproximately(210f, 1e-3f);
        onLine.Position.Y.Should().Be(0f);
    }

    [Fact]
    public void GetBounds_is_the_exact_extent_of_curves_not_their_control_points()
    {
        //Arrange
        PathGeometry circle = PathGeometry.Circle(new PointF(50, 60), 40);
        PathGeometry curve = new PathGeometryBuilder().MoveTo(0, 0).CubicTo(new PointF(0, 100), new PointF(100, 100), new PointF(100, 0)).Build();

        //Act
        RectangleF circleBounds = circle.GetBounds();
        RectangleF curveBounds = curve.GetBounds();

        //Assert
        circleBounds.X.Should().BeApproximately(10f, 1e-4f);
        circleBounds.Y.Should().BeApproximately(20f, 1e-4f);
        circleBounds.Width.Should().BeApproximately(80f, 1e-4f);
        circleBounds.Height.Should().BeApproximately(80f, 1e-4f);
        curveBounds.Height.Should().BeApproximately(75f, 1e-4f);
        PathGeometry.Empty.GetBounds().Should().Be(RectangleF.Empty);
    }

    [Fact]
    public void Parse_reads_absolute_relative_and_shorthand_commands()
    {
        //Arrange
        const string data = "M10 10 h 20 v 20 H 10 Z m 50 0 l10-10 l 10,10 c 0 10 10 10 10 0 s 10 -10 10 0 q 5 5 10 0 t 10 0";

        //Act
        PathGeometry geometry = PathGeometry.Parse(data);

        //Assert
        geometry.Contours.Count.Should().Be(2);
        PathContour box = geometry.Contours[0];
        box.IsClosed.Should().BeTrue();
        box[0].End.Should().Be(new PointF(30, 10));
        box[1].End.Should().Be(new PointF(30, 30));
        box[2].End.Should().Be(new PointF(10, 30));
        PathContour wave = geometry.Contours[1];
        wave.StartPoint.Should().Be(new PointF(60, 10));
        wave[1].End.Should().Be(new PointF(80, 10));
        wave[2].Kind.Should().Be(PathSegmentKind.Cubic);
        //S reflects the previous second control point (90, 20) about (90, 10)
        wave[3].Control1.Should().Be(new PointF(90, 0));
        //T reflects the previous quadratic control point (105, 15) about (110, 10)
        wave[5].Control1.Should().Be(new PointF(115, 5));
        wave.EndPoint.Should().Be(new PointF(120, 10));
    }

    [Fact]
    public void Parse_reads_compact_numbers_and_arc_flags()
    {
        //Act
        PathGeometry geometry = PathGeometry.Parse("M.5.5L-1e1-2.5e0A10 10 0 0110 0");

        //Assert
        geometry.Contours[0].StartPoint.Should().Be(new PointF(0.5f, 0.5f));
        geometry.Contours[0][0].End.Should().Be(new PointF(-10f, -2.5f));
        geometry.Contours[0].EndPoint.Should().Be(new PointF(10f, 0f));
    }

    [Fact]
    public void Arc_ends_exactly_at_its_end_point_and_follows_the_circle()
    {
        //Act - a half circle of radius 50 from (0, 0) to (100, 0), clockwise on screen (through y = -50)
        PathGeometry arc = PathGeometry.Parse("M0 0 A50 50 0 0 1 100 0");

        //Assert
        arc.Contours[0].EndPoint.Should().Be(new PointF(100, 0));
        PathPoint top = arc.GetPointAt(0.5f);
        top.Position.X.Should().BeApproximately(50f, 0.05f);
        top.Position.Y.Should().BeApproximately(-50f, 0.05f);
        Math.Abs(arc.GetLength() - (Math.PI * 50)).Should().BeLessThan(0.1);
    }

    [Theory]
    [InlineData("L 10 10")]
    [InlineData("M 10")]
    [InlineData("M 0 0 A 10 10 0 2 1 5 5")]
    [InlineData("M 0 0 Z 5")]
    public void Parse_rejects_invalid_data_with_a_format_exception(string data)
    {
        //Act
        Action act = () => PathGeometry.Parse(data);
        bool parsed = PathGeometry.TryParse(data, out PathGeometry geometry);

        //Assert
        act.Should().Throw<FormatException>();
        parsed.Should().BeFalse();
        geometry.Should().BeNull();
    }

    [Fact]
    public void ToSvgPathData_round_trips_every_point()
    {
        //Arrange
        PathGeometry original = PathGeometry.Combine(PathGeometry.Star(new PointF(100, 100), 80, 30.3f, 5, 7f),
            PathGeometry.Parse("M 1 2 Q 3.25 4 5 6 C 7 8 9 10 11.125 12"));

        //Act
        PathGeometry copy = PathGeometry.Parse(original.ToSvgPathData());

        //Assert
        copy.ToSvgPathData().Should().Be(original.ToSvgPathData());
        copy.Contours[0].GetSegments().Should().Equal(original.Contours[0].GetSegments());
    }

    [Fact]
    public void Trim_of_the_whole_path_returns_the_same_geometry()
    {
        //Arrange
        PathGeometry square = Square();

        //Act
        PathGeometry trimmed = square.Trim(0f, 1f, 0.3f);

        //Assert
        trimmed.Should().BeSameAs(square);
    }

    [Fact]
    public void Trim_keeps_the_part_between_the_fractions_as_open_contours()
    {
        //Arrange
        PathGeometry square = Square();

        //Act
        PathGeometry half = square.Trim(0.125f, 0.625f);

        //Assert
        half.Contours.Count.Should().Be(1);
        half.Contours[0].IsClosed.Should().BeFalse();
        half.Contours[0].StartPoint.Should().Be(new PointF(50, 0));
        half.Contours[0].EndPoint.Should().Be(new PointF(50, 100));
        half.GetLength().Should().Be(200f);
    }

    [Fact]
    public void Trim_wraps_round_one_closed_contour_as_one_piece()
    {
        //Arrange
        PathGeometry square = Square();

        //Act - from 0.875 round past the start to 0.125
        PathGeometry wrapped = square.Trim(0f, 0.25f, 0.875f);

        //Assert
        wrapped.Contours.Count.Should().Be(1);
        wrapped.Contours[0].StartPoint.Should().Be(new PointF(0, 50));
        wrapped.Contours[0].EndPoint.Should().Be(new PointF(50, 0));
        wrapped.GetLength().Should().Be(100f);
    }

    [Fact]
    public void Trim_with_no_length_is_empty_and_swaps_reversed_fractions()
    {
        //Arrange
        PathGeometry square = Square();

        //Act
        PathGeometry nothing = square.Trim(0.4f, 0.4f);
        PathGeometry swapped = square.Trim(0.5f, 0.25f);

        //Assert
        nothing.IsEmpty.Should().BeTrue();
        swapped.GetLength().Should().Be(100f);
        swapped.Contours[0].StartPoint.Should().Be(new PointF(100, 0));
    }

    [Fact]
    public void Dash_splits_each_contour_by_the_pattern_and_keeps_zero_length_dots()
    {
        //Arrange
        PathGeometry line = PathGeometry.Line(new PointF(0, 0), new PointF(100, 0));

        //Act
        PathGeometry dashes = line.Dash(new[] { 20f, 10f });
        PathGeometry offset = line.Dash(new[] { 20f, 10f }, 25f);
        PathGeometry dots = line.Dash(new[] { 0f, 25f });
        PathGeometry odd = line.Dash(new[] { 10f });

        //Assert
        dashes.Contours.Count.Should().Be(4);
        dashes.Contours[1].StartPoint.Should().Be(new PointF(30, 0));
        dashes.Contours[3].EndPoint.Should().Be(new PointF(100, 0));
        dashes.Contours[3].Length.Should().Be(10f);
        offset.Contours[0].StartPoint.Should().Be(new PointF(5, 0));
        dots.Contours.Count.Should().Be(4);
        dots.Contours[2].StartPoint.Should().Be(new PointF(50, 0));
        dots.Contours[2].Length.Should().Be(0f);
        odd.Contours.Count.Should().Be(5);
    }

    [Theory]
    [InlineData(new float[0])]
    [InlineData(new[] { 0f, 0f })]
    [InlineData(new[] { 5f, -1f })]
    public void Dash_rejects_patterns_without_length_or_with_negative_entries(float[] pattern)
    {
        //Act
        Action act = () => Square().Dash(pattern);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Flatten_keeps_every_point_within_the_tolerance_of_the_curve()
    {
        //Arrange
        PathGeometry circle = PathGeometry.Circle(new PointF(0, 0), 200);

        //Act
        IReadOnlyList<PathPolyline> fine = circle.Flatten(0.1f);
        IReadOnlyList<PathPolyline> coarse = circle.Flatten(5f);

        //Assert
        fine.Count.Should().Be(1);
        fine[0].IsClosed.Should().BeTrue();
        fine[0][0].Should().Be(new PointF(200, 0));
        fine[0][fine[0].Count - 1].Should().NotBe(fine[0][0]);
        coarse[0].Count.Should().BeLessThan(fine[0].Count);
        for (int i = 0; i < fine[0].Count; i++)
        {
            PointF a = fine[0][i], b = fine[0][(i + 1) % fine[0].Count];
            //The chord's midpoint must be within the tolerance of the circle (plus the cubic's own 0.03% error)
            float mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
            float r = MathF.Sqrt((mx * mx) + (my * my));
            (200f - r).Should().BeLessThan(0.1f + 0.06f);
        }
    }

    [Fact]
    public void Flatten_keeps_lines_as_their_two_points_and_rejects_a_bad_tolerance()
    {
        //Arrange
        PathGeometry open = PathGeometry.FromPoints(new[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10) }, false);

        //Act
        IReadOnlyList<PathPolyline> lines = open.Flatten();
        Action zero = () => open.Flatten(0f);

        //Assert
        lines[0].GetPoints().Should().Equal(new PointF(0, 0), new PointF(10, 0), new PointF(10, 10));
        lines[0].IsClosed.Should().BeFalse();
        zero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Transformed_maps_every_point_and_keeps_the_length_under_rotation()
    {
        //Arrange
        PathGeometry square = Square();
        Matrix3x2 rotation = Matrix3x2.CreateRotation(MathF.PI / 2) * Matrix3x2.CreateTranslation(200, 0);

        //Act
        PathGeometry moved = square.Transformed(Matrix3x2.CreateTranslation(5, 7));
        PathGeometry turned = square.Transformed(rotation);

        //Assert
        moved.Contours[0].StartPoint.Should().Be(new PointF(5, 7));
        moved.Contours[0][1].End.Should().Be(new PointF(105, 107));
        turned.GetLength().Should().BeApproximately(400f, 1e-3f);
        square.Transformed(Matrix3x2.Identity).Should().BeSameAs(square);
    }

    [Fact]
    public void Reversed_travels_the_other_way_with_the_same_length()
    {
        //Arrange
        PathGeometry open = PathGeometry.Parse("M 0 0 L 10 0 Q 20 0 20 10");

        //Act
        PathGeometry reversed = open.Reversed();

        //Assert
        reversed.Contours[0].StartPoint.Should().Be(new PointF(20, 10));
        reversed.Contours[0][0].Kind.Should().Be(PathSegmentKind.Quadratic);
        reversed.Contours[0][0].Control1.Should().Be(new PointF(20, 0));
        reversed.Contours[0].EndPoint.Should().Be(new PointF(0, 0));
        reversed.GetLength().Should().BeApproximately(open.GetLength(), 1e-4f);
    }

    [Fact]
    public void Shape_factories_make_the_expected_corners()
    {
        //Act
        PathGeometry hexagon = PathGeometry.RegularPolygon(new PointF(0, 0), 10, 6);
        PathGeometry star = PathGeometry.Star(new PointF(0, 0), 10, 4, 5);
        PathGeometry rounded = PathGeometry.RoundedPolygon(new[] { new PointF(0, 0), new PointF(100, 0), new PointF(100, 100), new PointF(0, 100) }, 10);
        PathGeometry roundedRectangle = PathGeometry.RoundedRectangle(new RectangleF(0, 0, 100, 100), 10);

        //Assert
        //Closed polygons keep their last side as the implicit closing line
        hexagon.Contours[0].SegmentCount.Should().Be(5);
        hexagon.Contours[0].IsClosed.Should().BeTrue();
        hexagon.Contours[0].StartPoint.X.Should().BeApproximately(0f, 1e-5f);
        hexagon.Contours[0].StartPoint.Y.Should().BeApproximately(-10f, 1e-5f);
        star.Contours[0].SegmentCount.Should().Be(9);
        rounded.Contours[0].IsClosed.Should().BeTrue();
        rounded.GetBounds().Width.Should().BeApproximately(100f, 1e-3f);
        rounded.GetLength().Should().BeApproximately(roundedRectangle.GetLength(), 0.05f);
    }

    [Fact]
    public void Factories_reject_bad_arguments()
    {
        //Act
        Action polygon = () => PathGeometry.RegularPolygon(new PointF(0, 0), 10, 2);
        Action star = () => PathGeometry.Star(new PointF(0, 0), 10, 5, 1);
        Action rounded = () => PathGeometry.RoundedPolygon(new[] { new PointF(0, 0), new PointF(1, 0) }, 2);
        Action negative = () => PathGeometry.RoundedRectangle(new RectangleF(0, 0, 10, 10), -1);
        Action nan = () => new PathGeometryBuilder().LineTo(Single.NaN, 0);

        //Assert
        polygon.Should().Throw<ArgumentOutOfRangeException>();
        star.Should().Throw<ArgumentOutOfRangeException>();
        rounded.Should().Throw<ArgumentException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
        nan.Should().Throw<ArgumentException>();
    }
}
