using System;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionHardEdgedMaskTests
{
    //A session whose calibrated units are output pixels (calibration size = export size)
    private static DrawingSession CreateSession(int width, int height, Color? background = null)
        => new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(width, height),
            BackgroundFillColor = background ?? Color.Transparent,
            LayerOpacity = 255,
        });

    //The plane as rows of '#' (1) and '.' (0); anything else fails the test
    private static string[] Rows(DrawingPlane plane)
    {
        var rows = new string[plane.Height];
        for (int y = 0; y < plane.Height; y++)
        {
            var chars = new char[plane.Width];
            for (int x = 0; x < plane.Width; x++)
            {
                float v = plane[x, y];
                chars[x] = v == 1f ? '#' : v == 0f ? '.' : '?';
            }
            rows[y] = new string(chars);
        }
        return rows;
    }

    private sealed class UnknownAreaShape : DrawingShape
    {
        public UnknownAreaShape() : base(1f, (Color?)null) { }

        public override void Draw(SKCanvas canvas, SKColor color) { }
    }

    [Fact]
    public void ExportMask_hard_edged_covers_exactly_the_pixels_whose_centres_are_inside()
    {
        //Arrange - a rectangle from (1, 1) to (4, 3): centres 1.5, 2.5, 3.5 across, 1.5, 2.5 down
        using DrawingSession session = CreateSession(6, 5);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(1, 1, 3, 2, filled: true);

        //Act
        DrawingPlane mask = session.ExportMask(new Size(6, 5), MaskMeaning.HardEdged);

        //Assert
        Rows(mask).Should().Equal("......", ".###..", ".###..", "......", "......");
    }

    [Fact]
    public void ExportMask_hard_edged_keeps_centres_on_left_and_top_edges_and_drops_right_and_bottom()
    {
        //Arrange - edges exactly through pixel centres: x 1.5 .. 3.5, y 0.5 .. 2.5
        using DrawingSession session = CreateSession(5, 4);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(1.5f, 0.5f, 2f, 2f, filled: true);

        //Act
        DrawingPlane mask = session.ExportMask(new Size(5, 4), MaskMeaning.HardEdged);

        //Assert - columns 1, 2 (centres 1.5 on the left edge, 2.5) and rows 0, 1
        Rows(mask).Should().Equal(".##..", ".##..", ".....", ".....");
    }

    [Fact]
    public void ExportMask_hard_edged_honours_the_even_odd_fill_rule()
    {
        //Arrange - a 6 x 6 square with a 2 x 2 square inside it, filled even-odd
        using DrawingSession session = CreateSession(6, 6);
        session.AddLayer("Ink", Color.Red);
        PathGeometry ring = PathGeometry.Combine(
            PathGeometry.Rectangle(new RectangleF(0, 0, 6, 6)),
            PathGeometry.Rectangle(new RectangleF(2, 2, 2, 2))).WithFillRule(PathFillRule.EvenOdd);
        session.DrawPath(ring, filled: true);

        //Act
        DrawingPlane mask = session.ExportMask(new Size(6, 6), MaskMeaning.HardEdged);

        //Assert
        Rows(mask).Should().Equal("######", "######", "##..##", "##..##", "######", "######");
    }

    [Fact]
    public void ExportMask_hard_edged_covers_an_outline_band_and_not_the_inside()
    {
        //Arrange - a circle of radius 8 outlined 2 units thick: the band 7 .. 9 from the centre
        using DrawingSession session = CreateSession(20, 20);
        session.AddLayer("Ink", Color.Red);
        session.DrawCircle(10, 10, 8, thickness: 2);

        //Act
        DrawingPlane mask = session.ExportMask(new Size(20, 20), MaskMeaning.HardEdged);

        //Assert - centre and ring pixels by distance of their centres from (10, 10)
        mask[9, 9].Should().Be(0f);
        mask[10, 1].Should().Be(1f);  //centre (10.5, 1.5): 8.5 away
        mask[10, 3].Should().Be(0f);  //(10.5, 3.5): 6.5 away
        mask[0, 10].Should().Be(0f);  //(0.5, 10.5): 9.5 away
    }

    [Fact]
    public void ExportMask_hard_edged_counts_a_dot_stroke_by_its_round_width()
    {
        //Arrange - a one-point stroke of width 4 at (5, 5): a disc of radius 2
        using DrawingSession session = CreateSession(10, 10);
        DrawingLayer layer = session.AddLayer("Ink", Color.Red);
        var stroke = new Stroke(4f);
        stroke.AddPoint(5, 5);
        layer.AddStroke(stroke);

        //Act
        DrawingPlane mask = session.ExportMask(new Size(10, 10), MaskMeaning.HardEdged);

        //Assert - the 4 x 4 block of centres 3.5 .. 6.5 minus its corners (2.12 away)
        Rows(mask).Should().Equal("..........", "..........", "..........", "....##....", "...####...",
            "...####...", "....##....", "..........", "..........", "..........");
    }

    [Fact]
    public void ExportMask_hard_edged_ignores_paint_and_layer_opacity_but_not_element_opacity_or_time()
    {
        //Arrange
        using DrawingSession session = CreateSession(8, 2);
        DrawingLayer faded = session.AddLayer("Faded", Color.Red);
        faded.Opacity = 0f;
        session.DrawRectangle(0, 0, 2, 2, filled: true, color: Color.FromRgba(0, 0, 0, 0));
        session.DrawShape(new RectangleShape(2, 0, 2, 2, isFilled: true) { Opacity = 0f });
        session.DrawShape(new RectangleShape(4, 0, 2, 2, isFilled: true) { StartTime = 5.0 });
        session.DrawShape(new RectangleShape(6, 0, 2, 2, isFilled: true) { Opacity = 0.01f });

        //Act
        DrawingPlane mask = session.ExportMask(new Size(8, 2), MaskMeaning.HardEdged);
        session.Time = 6.0;
        DrawingPlane later = session.ExportMask(new Size(8, 2), MaskMeaning.HardEdged);

        //Assert - a transparent colour and a layer at opacity 0 still cover; opacity 0 and a
        //  time window that has not opened do not
        Rows(mask).Should().Equal("##....##", "##....##");
        Rows(later).Should().Equal("##..####", "##..####");
    }

    [Fact]
    public void ExportMask_hard_edged_follows_transforms_and_the_aspect_fit()
    {
        //Arrange - a 4 x 4 calibration exported 8 x 4: the drawing sits at x 2 .. 6, scale 1
        using DrawingSession session = CreateSession(4, 4);
        session.AddLayer("Ink", Color.Red);
        session.DrawShape(new RectangleShape(0, 0, 1, 1, isFilled: true) { Transform = DrawingTransform.Translate(1, 2) });

        //Act
        DrawingPlane mask = session.ExportMask(new Size(8, 4), MaskMeaning.HardEdged);

        //Assert
        Rows(mask).Should().Equal("........", "........", "...#....", "........");
    }

    [Fact]
    public void ExportMask_hard_edged_of_one_layer_and_of_the_union()
    {
        //Arrange
        using DrawingSession session = CreateSession(4, 1);
        DrawingLayer first = session.AddLayer("First", Color.Red);
        session.DrawRectangle(0, 0, 1, 1, filled: true);
        DrawingLayer second = session.AddLayer("Second", Color.Blue);
        session.ActiveLayer = second;
        session.DrawRectangle(2, 0, 1, 1, filled: true);
        var reused = new DrawingPlane(4, 1, 7f);

        //Act
        DrawingPlane one = session.ExportMask(first, new Size(4, 1), MaskMeaning.HardEdged);
        session.ExportMask(reused, MaskMeaning.HardEdged);

        //Assert
        Rows(one).Should().Equal("#...");
        Rows(reused).Should().Equal("#.#.");
    }

    [Fact]
    public void ExportMask_hard_edged_skips_shapes_whose_area_is_unknown_and_ignores_the_background()
    {
        //Arrange
        using DrawingSession session = CreateSession(3, 3, Color.White);
        session.AddLayer("Ink", Color.Red);
        session.DrawShape(new UnknownAreaShape());

        //Act
        DrawingPlane mask = session.ExportMask(new Size(3, 3), MaskMeaning.HardEdged);

        //Assert
        Rows(mask).Should().Equal("...", "...", "...");
    }

    [Fact]
    public void ExportMask_hard_edged_gives_the_same_mask_every_time_and_differs_from_the_anti_aliased_one()
    {
        //Arrange
        using DrawingSession session = CreateSession(64, 48);
        session.AddLayer("Ink", Color.Red);
        session.DrawCircle(30, 20, 13.3f, filled: true);
        session.DrawLine(3, 40, 60, 5, thickness: 3.5f);
        session.DrawShape(new BrushStrokeShape(40, 30, 30, 9, 34f, 0.1f));

        //Act
        DrawingPlane first = session.ExportMask(new Size(64, 48), MaskMeaning.HardEdged);
        DrawingPlane second = session.ExportMask(new Size(64, 48), MaskMeaning.HardEdged);
        DrawingPlane soft = session.ExportMask(new Size(64, 48));

        //Assert - only 0 and 1; anti-aliased coverage has edge values in between
        first.Data.Should().Equal(second.Data);
        foreach (float v in first.Data) { (v == 0f || v == 1f).Should().BeTrue(); }
        bool between = false;
        foreach (float v in soft.Data) { between |= v > 0f && v < 1f; }
        between.Should().BeTrue();
    }

    [Fact]
    public void ExportMask_hard_edged_of_a_scaled_export_scales_the_geometry()
    {
        //Arrange - a 2 x 2 unit square at (1, 1) in a 4 x 4 calibration, exported at 8 x 8
        using DrawingSession session = CreateSession(4, 4);
        session.AddLayer("Ink", Color.Red);
        session.DrawRectangle(1, 1, 2, 2, filled: true);

        //Act
        DrawingPlane mask = session.ExportMask(new Size(8, 8), MaskMeaning.HardEdged);

        //Assert
        Rows(mask).Should().Equal("........", "........", "..####..", "..####..", "..####..", "..####..", "........", "........");
    }

    [Fact]
    public void ExportMask_undefined_meaning_still_throws()
    {
        //Arrange
        using DrawingSession session = CreateSession(4, 4);

        //Act
        Action undefined = () => session.ExportMask(new Size(4, 4), (MaskMeaning)3);

        //Assert
        undefined.Should().Throw<ArgumentOutOfRangeException>();
    }
}
