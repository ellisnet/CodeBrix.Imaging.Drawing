using System;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class BrushStrokeShapeTests
{
    [Fact]
    public void Constructor_builds_a_polygon_from_tip_to_tip_along_both_sides()
    {
        //Arrange - a straight stroke along +x: tips at x 40 and 60, half width 5 at the middle
        var stroke = new BrushStrokeShape(50, 20, 20, 10, 0f);

        //Act
        PointF[] points = stroke.GetPoints();

        //Assert
        points.Length.Should().Be((2 * BrushStrokeShape.PointsPerSide) - 2);
        points[0].Should().Be(new PointF(40, 20));
        points[BrushStrokeShape.PointsPerSide - 1].Should().Be(new PointF(60, 20));
        points[8].Should().Be(new PointF(50, 25));
        points[(2 * BrushStrokeShape.PointsPerSide) - 2 - 8].Should().Be(new PointF(50, 15));
        stroke.GetBounds().Should().Be(new RectangleF(40, 15, 20, 10));
    }

    [Fact]
    public void Bend_moves_the_middle_of_the_centre_line_across_by_bend_times_length()
    {
        //Arrange
        var straight = new BrushStrokeShape(0, 0, 100, 10, 0f);
        var bent = new BrushStrokeShape(0, 0, 100, 10, 0f, 0.1f);

        //Act
        PointF middle = bent.GetPoints()[8];
        PointF tip = bent.GetPoints()[0];

        //Assert - Bend x Length x (4 s^2 - 1) at s = 0 is -10; the tips stay on the axis
        middle.Y.Should().Be(straight.GetPoints()[8].Y - 10f);
        tip.Should().Be(new PointF(-50, 0));
    }

    [Fact]
    public void Profile_sets_the_width_profile_exponent()
    {
        //Arrange - point 4 of 17 is at s = -0.25, where |2s| = 0.5
        var blunt = new BrushStrokeShape(0, 0, 32, 20, 0f);
        var pointed = new BrushStrokeShape(0, 0, 32, 20, 0f, 0f, 2f);

        //Act
        float bluntHalf = blunt.GetPoints()[4].Y;
        float pointedHalf = pointed.GetPoints()[4].Y;

        //Assert - 10 x sqrt(1 - 0.5^p)
        bluntHalf.Should().Be((float)(10 * Math.Sqrt(1 - 0.0625)));
        pointedHalf.Should().Be((float)(10 * Math.Sqrt(1 - 0.25)));
        pointed.Profile.Should().Be(2f);
        blunt.Profile.Should().Be(BrushStrokeShape.DefaultProfile);
    }

    [Fact]
    public void Angle_turns_the_stroke_about_its_centre()
    {
        //Arrange - pointing straight down the screen
        var stroke = new BrushStrokeShape(10, 10, 20, 4, 90f);

        //Act
        PointF[] points = stroke.GetPoints();

        //Assert
        points[0].X.Should().BeApproximately(10f, 1e-5f);
        points[0].Y.Should().BeApproximately(0f, 1e-5f);
        points[BrushStrokeShape.PointsPerSide - 1].Y.Should().BeApproximately(20f, 1e-5f);
        points[8].X.Should().BeApproximately(8f, 1e-5f);
    }

    [Fact]
    public void A_path_angle_passes_straight_to_a_stroke_and_matches_a_lines_axes()
    {
        //Arrange - a path heading straight down the screen, and a line along it
        PathPoint point = PathGeometry.Line(new PointF(10, 0), new PointF(10, 20)).GetPointAt(0.5f);
        var line = new LineShape(10, 0, 10, 20, 2f);

        //Act - all three speak degrees, clockwise on screen from +x
        var stroke = new BrushStrokeShape(point.Position.X, point.Position.Y, 20, 4, point.Angle);
        ElementAxes lineAxes = line.GetAxes().Value;
        PointF[] points = stroke.GetPoints();

        //Assert
        point.Angle.Should().Be(90f);
        lineAxes.Angle.Should().Be(90f);
        stroke.GetAxes().Value.Angle.Should().Be(lineAxes.Angle);
        points[0].X.Should().BeApproximately(10f, 1e-5f);
        points[0].Y.Should().BeApproximately(0f, 1e-5f);
        points[BrushStrokeShape.PointsPerSide - 1].Y.Should().BeApproximately(20f, 1e-5f);
    }

    [Fact]
    public void GetOutline_GetCoverageAreas_and_GetAxes_describe_the_stroke()
    {
        //Arrange
        var stroke = new BrushStrokeShape(5, 6, 30, 8, 23f, 0.05f, 3f, Color.Gold);

        //Act
        PathGeometry outline = stroke.GetOutline();
        var areas = stroke.GetCoverageAreas(0.1f);
        ElementAxes? axes = stroke.GetAxes();

        //Assert
        outline.Contours.Count.Should().Be(1);
        outline.Contours[0].IsClosed.Should().BeTrue();
        outline.FillRule.Should().Be(PathFillRule.NonZero);
        areas.Count.Should().Be(1);
        areas[0].ToSvgPathData().Should().Be(outline.ToSvgPathData());
        axes.Value.Should().Be(new ElementAxes(new PointF(5, 6), 23f, 30, 8));
        stroke.Color.Should().Be(Color.Gold);
    }

    [Fact]
    public void Constructor_rejects_unusable_values()
    {
        //Act
        Action zeroLength = () => new BrushStrokeShape(0, 0, 0, 5, 0f);
        Action negativeWidth = () => new BrushStrokeShape(0, 0, 5, -1, 0f);
        Action badAngle = () => new BrushStrokeShape(0, 0, 5, 5, float.NaN);
        Action badProfile = () => new BrushStrokeShape(0, 0, 5, 5, 0f, 0f, 0f);
        Action noFill = () => new BrushStrokeShape(0, 0, 5, 5, 0f, 0f, 4f, (ShapeFill)null);

        //Assert
        zeroLength.Should().Throw<ArgumentOutOfRangeException>();
        negativeWidth.Should().Throw<ArgumentOutOfRangeException>();
        badAngle.Should().Throw<ArgumentOutOfRangeException>();
        badProfile.Should().Throw<ArgumentOutOfRangeException>();
        noFill.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Draw_paints_an_anti_aliased_filled_stroke_over_transparency()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(60, 30),
            BackgroundFillColor = Color.Transparent,
            LayerOpacity = 255,
        });
        session.AddLayer("Paint", Color.FromRgb(20, 60, 200));
        session.DrawBrushStroke(30, 15, 40, 12, 11f, 0.05f);

        //Act
        byte[] rgba = session.ExportPixels(new Size(60, 30), includeBackground: false);

        //Assert - opaque paint at the centre, nothing in the corner, soft (partial) edge pixels exist
        int centre = ((15 * 60) + 30) * 4;
        rgba[centre].Should().Be(20);
        rgba[centre + 2].Should().Be(200);
        rgba[centre + 3].Should().Be(255);
        rgba[3].Should().Be(0);
        bool partial = false;
        for (int i = 3; i < rgba.Length; i += 4) { partial |= rgba[i] > 0 && rgba[i] < 255; }
        partial.Should().BeTrue();
    }

    [Fact]
    public void DrawBrushStroke_adds_the_shape_to_the_active_layer()
    {
        //Arrange
        using var session = new DrawingSession();
        DrawingLayer layer = session.AddLayer("Paint", Color.Red);

        //Act
        BrushStrokeShape stroke = session.DrawBrushStroke(100, 100, 80, 20, 57f, 0.1f, Color.Blue);

        //Assert
        layer.GetElements().Should().Equal(stroke);
        stroke.Bend.Should().Be(0.1f);
        stroke.Angle.Should().Be(57f);
        stroke.Color.Should().Be(Color.Blue);
    }
}
