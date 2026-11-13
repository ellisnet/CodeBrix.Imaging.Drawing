using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Paths;

public class PathGeometryCombiningTests
{
    private static PathGeometry Square(float x, float y, float size) => PathGeometry.Rectangle(new RectangleF(x, y, size, size));

    private static readonly PathGeometry A = Square(0, 0, 100);
    private static readonly PathGeometry B = Square(50, 50, 100);

    //The filled area of polygon geometry by the non-zero rule's bookkeeping: the signed areas of
    //  its contours summed (holes are wound against their outlines, so they subtract)
    private static double NetArea(PathGeometry geometry)
    {
        double total = 0.0;
        foreach (PathContour contour in geometry.Contours) { total += SignedArea(contour); }
        return Math.Abs(total);
    }

    private static double SignedArea(PathContour contour)
    {
        var points = new List<PointF> { contour.StartPoint };
        foreach (PathSegment segment in contour.GetSegments()) { points.Add(segment.End); }
        double sum = 0.0;
        for (int i = 0; i < points.Count; i++)
        {
            PointF a = points[i], b = points[(i + 1) % points.Count];
            sum += ((double)a.X * b.Y) - ((double)b.X * a.Y);
        }
        return sum / 2.0;
    }

    private static void ShouldBePolygons(PathGeometry geometry)
    {
        geometry.FillRule.Should().Be(PathFillRule.NonZero);
        foreach (PathContour contour in geometry.Contours)
        {
            contour.IsClosed.Should().BeTrue();
            foreach (PathSegment segment in contour.GetSegments())
            {
                segment.Kind.Should().Be(PathSegmentKind.Line);
                (segment.End.X * 1024f % 1f).Should().Be(0f);
                (segment.End.Y * 1024f % 1f).Should().Be(0f);
            }
        }
    }

    [Fact]
    public void Union_intersection_difference_and_exclusive_or_of_two_squares_have_the_expected_areas()
    {
        //Act
        PathGeometry union = A.Union(B);
        PathGeometry intersection = A.Intersect(B);
        PathGeometry difference = A.Subtract(B);
        PathGeometry exclusive = A.ExclusiveOr(B);

        //Assert
        NetArea(union).Should().Be(17500.0);
        union.Contours.Count.Should().Be(1);
        NetArea(intersection).Should().Be(2500.0);
        intersection.GetBounds().Should().Be(new RectangleF(50, 50, 50, 50));
        NetArea(difference).Should().Be(7500.0);
        NetArea(exclusive).Should().Be(15000.0);
        ShouldBePolygons(union);
        ShouldBePolygons(exclusive);
        A.CombineWith(B, PathBooleanOperation.Intersection).ToSvgPathData().Should().Be(intersection.ToSvgPathData());
    }

    [Fact]
    public void Each_geometry_fills_by_its_own_fill_rule()
    {
        //Arrange - an outer and an inner square wound the same way
        PathGeometry nested = PathGeometry.Combine(Square(0, 0, 100), Square(25, 25, 50));

        //Act
        double nonZero = NetArea(nested.Simplify());
        double evenOdd = NetArea(nested.WithFillRule(PathFillRule.EvenOdd).Simplify());
        double cut = NetArea(nested.WithFillRule(PathFillRule.EvenOdd).Intersect(Square(0, 0, 50)));

        //Assert
        nonZero.Should().Be(10000.0);
        evenOdd.Should().Be(7500.0);
        cut.Should().Be(2500.0 - 625.0);
    }

    [Fact]
    public void CombineAll_handles_many_geometries()
    {
        //Arrange - three squares in a row, each overlapping the next by half
        PathGeometry[] squares = { Square(0, 0, 100), Square(50, 0, 100), Square(100, 0, 100) };

        //Act
        double union = NetArea(PathGeometry.CombineAll(squares, PathBooleanOperation.Union));
        double intersection = NetArea(PathGeometry.CombineAll(squares, PathBooleanOperation.Intersection));
        double difference = NetArea(PathGeometry.CombineAll(squares, PathBooleanOperation.Difference));
        double exclusive = NetArea(PathGeometry.CombineAll(squares, PathBooleanOperation.ExclusiveOr));
        PathGeometry none = PathGeometry.CombineAll(Array.Empty<PathGeometry>(), PathBooleanOperation.Union);
        double one = NetArea(PathGeometry.CombineAll(new[] { squares[0] }, PathBooleanOperation.Difference));

        //Assert - covered once: 0-50, 150-200 and the middle 100-... ; the pieces covered three times: none
        union.Should().Be(20000.0);
        intersection.Should().Be(0.0);
        difference.Should().Be(5000.0);
        exclusive.Should().Be(10000.0);
        none.IsEmpty.Should().BeTrue();
        one.Should().Be(10000.0);
    }

    [Fact]
    public void Curves_are_flattened_within_the_tolerance_onto_the_grid()
    {
        //Arrange
        PathGeometry circle = PathGeometry.Circle(new PointF(200, 200), 50);

        //Act
        PathGeometry fine = circle.Simplify(0.05f);
        PathGeometry coarse = circle.Simplify(2f);

        //Assert
        ShouldBePolygons(fine);
        NetArea(fine).Should().BeInRange(Math.PI * 2500 * 0.995, Math.PI * 2500 * 1.001);
        coarse.Contours[0].SegmentCount.Should().BeLessThan(fine.Contours[0].SegmentCount);
        foreach (PathSegment segment in fine.Contours[0].GetSegments())
        {
            double r = Math.Sqrt(((segment.End.X - 200.0) * (segment.End.X - 200.0)) + ((segment.End.Y - 200.0) * (segment.End.Y - 200.0)));
            r.Should().BeInRange(49.9, 50.1);
        }
    }

    [Fact]
    public void An_open_contour_fills_as_if_closed()
    {
        //Arrange
        PathGeometry open = PathGeometry.FromPoints(new[] { new PointF(0, 0), new PointF(100, 0), new PointF(0, 100) }, false);

        //Act
        double area = NetArea(open.Simplify());

        //Assert
        area.Should().Be(5000.0);
    }

    [Fact]
    public void Combining_rejects_bad_input()
    {
        //Arrange
        PathGeometry far = Square(2_000_000, 0, 10);

        //Act
        Action nullOther = () => A.Union(null);
        Action badTolerance = () => A.Union(B, 0f);
        Action badOperation = () => A.CombineWith(B, (PathBooleanOperation)9);
        Action tooFar = () => A.Union(far);
        Action nullList = () => PathGeometry.CombineAll(null, PathBooleanOperation.Union);
        Action nullEntry = () => PathGeometry.CombineAll(new[] { A, null }, PathBooleanOperation.Union);
        Action badMiter = () => A.Inflate(5, PathStrokeJoin.Miter, 0.5f);
        Action nullStyle = () => A.ToStrokeOutline(null);

        //Assert
        nullOther.Should().Throw<ArgumentNullException>();
        badTolerance.Should().Throw<ArgumentOutOfRangeException>();
        badOperation.Should().Throw<ArgumentOutOfRangeException>();
        tooFar.Should().Throw<ArgumentException>();
        nullList.Should().Throw<ArgumentNullException>();
        nullEntry.Should().Throw<ArgumentNullException>();
        badMiter.Should().Throw<ArgumentOutOfRangeException>();
        nullStyle.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Empty_results_are_the_empty_geometry()
    {
        //Act
        PathGeometry apart = Square(0, 0, 10).Intersect(Square(50, 50, 10));
        PathGeometry gone = Square(0, 0, 10).Subtract(Square(-5, -5, 30));

        //Assert
        apart.IsEmpty.Should().BeTrue();
        gone.IsEmpty.Should().BeTrue();
        PathGeometry.Empty.Union(PathGeometry.Empty).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Stroke_outline_honours_the_caps()
    {
        //Arrange
        PathGeometry line = PathGeometry.Line(new PointF(0, 0), new PointF(100, 0));

        //Act
        PathGeometry butt = line.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Butt));
        PathGeometry square = line.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Square));
        PathGeometry round = line.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Round), 0.02f);

        //Assert
        NetArea(butt).Should().Be(1000.0);
        butt.GetBounds().Should().Be(new RectangleF(0, -5, 100, 10));
        NetArea(square).Should().Be(1100.0);
        square.GetBounds().Should().Be(new RectangleF(-5, -5, 110, 10));
        NetArea(round).Should().BeInRange(1000.0 + (Math.PI * 25 * 0.99), 1000.0 + (Math.PI * 25));
        ShouldBePolygons(round);
    }

    [Fact]
    public void Stroke_outline_honours_the_joins_and_the_miter_limit()
    {
        //Arrange - a right-angle corner: right, then down
        PathGeometry corner = PathGeometry.FromPoints(new[] { new PointF(0, 0), new PointF(100, 0), new PointF(100, 100) }, false);

        //Act
        PathGeometry miter = corner.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Butt, PathStrokeJoin.Miter));
        PathGeometry limited = corner.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Butt, PathStrokeJoin.Miter, miterLimit: 1.2f));
        PathGeometry bevel = corner.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Butt, PathStrokeJoin.Bevel));
        PathGeometry round = corner.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Butt, PathStrokeJoin.Round), 0.02f);

        //Assert - two 10 x 100 arms sharing a 5 x 5 square, plus the corner piece
        NetArea(miter).Should().Be(2000.0);
        miter.GetBounds().Should().Be(new RectangleF(0, -5, 105, 105));
        NetArea(limited).Should().Be(1987.5);
        NetArea(bevel).Should().Be(1987.5);
        NetArea(round).Should().BeInRange(1975.0 + (Math.PI * 25 / 4 * 0.99), 1975.0 + (Math.PI * 25 / 4));
    }

    [Fact]
    public void Stroke_outline_of_a_closed_contour_has_its_hole_and_dashes_are_cut_first()
    {
        //Arrange
        PathGeometry box = Square(0, 0, 100);
        PathGeometry line = PathGeometry.Line(new PointF(0, 0), new PointF(100, 0));

        //Act
        PathGeometry ring = box.ToStrokeOutline(new PathStrokeStyle(10, PathStrokeCap.Butt, PathStrokeJoin.Miter));
        PathGeometry dashes = line.ToStrokeOutline(new PathStrokeStyle(2, PathStrokeCap.Butt, dashPattern: new[] { 10f, 10f }));

        //Assert
        NetArea(ring).Should().Be(110.0 * 110.0 - (90.0 * 90.0));
        ring.Contours.Count.Should().Be(2);
        dashes.Contours.Count.Should().Be(5);
        NetArea(dashes).Should().Be(100.0);
    }

    [Fact]
    public void A_zero_length_contour_strokes_to_a_dot_for_round_and_square_caps_only()
    {
        //Arrange - dashes of zero length are single points
        PathGeometry dots = PathGeometry.Line(new PointF(0, 0), new PointF(40, 0)).Dash(new[] { 0f, 20f });

        //Act
        PathGeometry round = dots.ToStrokeOutline(new PathStrokeStyle(6, PathStrokeCap.Round), 0.02f);
        PathGeometry square = dots.ToStrokeOutline(new PathStrokeStyle(6, PathStrokeCap.Square));
        PathGeometry butt = dots.ToStrokeOutline(new PathStrokeStyle(6, PathStrokeCap.Butt));

        //Assert - dots at 0 and 20 (a dash exactly at the contour's end is not cut)
        round.Contours.Count.Should().Be(2);
        NetArea(round).Should().BeInRange(2 * Math.PI * 9 * 0.99, 2 * Math.PI * 9);
        NetArea(square).Should().Be(2 * 36.0);
        butt.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void A_curved_stroke_outline_is_one_clean_contour()
    {
        //Arrange - many divided-curve corners, each joined round; neighbouring pieces must meet without hairline gaps
        PathGeometry wave = PathGeometry.Parse("M 0 100 C 50 0, 150 0, 200 100 S 350 200, 400 100 Q 450 40 500 100");

        //Act
        PathGeometry miter = wave.ToStrokeOutline(new PathStrokeStyle(26, PathStrokeCap.Round, PathStrokeJoin.Miter));
        PathGeometry bevel = wave.ToStrokeOutline(new PathStrokeStyle(9, PathStrokeCap.Butt, PathStrokeJoin.Bevel), 0.05f);

        //Assert
        miter.Contours.Count.Should().Be(1);
        bevel.Contours.Count.Should().Be(1);
        ShouldBePolygons(miter);
    }

    [Fact]
    public void Inflate_grows_and_deflate_shrinks_with_the_join_at_the_corners()
    {
        //Act
        PathGeometry miter = A.Inflate(10, PathStrokeJoin.Miter);
        PathGeometry round = A.Inflate(10, PathStrokeJoin.Round, tolerance: 0.02f);
        PathGeometry bevel = A.Inflate(10, PathStrokeJoin.Bevel);
        PathGeometry shrunk = A.Deflate(10);
        PathGeometry gone = A.Deflate(60);
        PathGeometry same = A.Inflate(0);

        //Assert
        NetArea(miter).Should().Be(120.0 * 120.0);
        miter.GetBounds().Should().Be(new RectangleF(-10, -10, 120, 120));
        NetArea(round).Should().BeInRange(14000 + (Math.PI * 100 * 0.99), 14000 + (Math.PI * 100));
        NetArea(bevel).Should().Be(14400.0 - (4 * 50.0));
        NetArea(shrunk).Should().Be(6400.0);
        shrunk.GetBounds().Should().Be(new RectangleF(10, 10, 80, 80));
        gone.IsEmpty.Should().BeTrue();
        NetArea(same).Should().Be(10000.0);
        A.Inflate(-10).ToSvgPathData().Should().Be(shrunk.ToSvgPathData());
    }

    [Fact]
    public void Deflate_rounds_the_inside_corners_of_a_concave_shape()
    {
        //Arrange - an L made of two 100 x 40 arms
        PathGeometry l = PathGeometry.FromPoints(new[]
        {
            new PointF(0, 0), new PointF(40, 0), new PointF(40, 60), new PointF(100, 60), new PointF(100, 100), new PointF(0, 100),
        }, true);

        //Act
        PathGeometry miter = l.Deflate(5, PathStrokeJoin.Miter);
        PathGeometry round = l.Deflate(5, PathStrokeJoin.Round, tolerance: 0.02f);

        //Assert - the concave corner keeps a sharp miter or takes a quarter-circle fillet of radius 5
        NetArea(miter).Should().Be((30.0 * 90.0) + (60.0 * 30.0));
        double fillet = 25.0 - (Math.PI * 25.0 / 4.0);
        NetArea(round).Should().BeInRange(NetArea(miter) + (fillet * 0.98), NetArea(miter) + (fillet * 1.02));
    }

    [Fact]
    public void A_path_shape_strokes_its_trimmed_path_to_an_outline()
    {
        //Arrange
        var shape = new PathShape(PathGeometry.Line(new PointF(0, 0), new PointF(100, 0)), new PathStrokeStyle(10, PathStrokeCap.Butt));
        var filled = new PathShape(A, color: Color.Red, isFilled: true);

        //Act
        double whole = NetArea(shape.GetStrokeOutline());
        shape.TrimEnd = 0.5f;
        double half = NetArea(shape.GetStrokeOutline());

        //Assert
        whole.Should().Be(1000.0);
        half.Should().Be(500.0);
        filled.GetStrokeOutline().Should().BeNull();
    }

    [Fact]
    public void A_combined_shape_draws_with_straight_alpha_over_transparency()
    {
        //Arrange - a ring cut by exclusive-or: filled outside the overlap, a hole inside it
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(200, 200), LayerOpacity = 255 });
        session.AddLayer("Paths", Color.FromRgb(0, 128, 255));
        session.DrawPath(Square(20, 20, 100).ExclusiveOr(Square(70, 70, 100)), filled: true);

        //Act
        byte[] pixels = session.ExportPixels(new Size(200, 200), includeBackground: false);

        //Assert
        TestAssets.PixelAt(pixels, 200, 40, 40).Should().Be(new Rgba32(0, 128, 255, 255));
        TestAssets.PixelAt(pixels, 200, 95, 95).A.Should().Be(0);
        TestAssets.PixelAt(pixels, 200, 150, 150).A.Should().Be(255);
        TestAssets.PixelAt(pixels, 200, 190, 10).A.Should().Be(0);
    }
}
