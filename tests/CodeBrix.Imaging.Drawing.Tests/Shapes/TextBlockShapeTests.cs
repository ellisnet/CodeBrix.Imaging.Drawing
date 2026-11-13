using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class TextBlockShapeTests
{
    private static readonly Size Frame = new Size(400, 240);

    private static DrawingSession CreateSession(bool background = true)
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = Frame,
            LayerOpacity = 255,
            BackgroundFillColor = background ? Color.White : Color.Transparent,
        });
        session.Fonts.RegisterFont(TestAssets.RobotoPath);
        session.AddLayer("Text", Color.Black);
        return session;
    }

    private static TextLayoutResult Lay(DrawingSession session, string text = "Typewriter text\nsecond line", float size = 36)
        => session.LayoutText(text, size, options: new TextLayoutOptions { MaxWidth = 360 });

    [Fact]
    public void DrawTextBlock_draws_inside_its_box_over_transparency()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        TextLayoutResult layout = Lay(session);

        //Act
        TextBlockShape shape = session.DrawTextBlock(layout, 20, 30, Color.FromRgb(200, 30, 40));
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        RectangleF bounds = shape.GetBounds().Value;
        bounds.Left.Should().BeGreaterThanOrEqualTo(20f);
        bounds.Top.Should().BeGreaterThanOrEqualTo(30f);
        shape.BoxBounds.Should().Be(new RectangleF(20, 30, 360, layout.Height));
        TestAssets.CountInked(pixels).Should().BeGreaterThan(500);
        TestAssets.PixelAt(pixels, Frame.Width, 5, 5).A.Should().Be(0);
        TestAssets.PixelAt(pixels, Frame.Width, 395, 235).A.Should().Be(0);
    }

    [Fact]
    public void Reveal_fraction_zero_draws_nothing_and_one_draws_everything()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        TextLayoutResult layout = Lay(session);
        var shape = new TextBlockShape(layout, 10, 10);
        session.DrawShape(shape);
        int all = TestAssets.CountInked(session.ExportPixels(Frame, false));

        //Act
        shape.RevealFraction = 0f;
        int none = TestAssets.CountInked(session.ExportPixels(Frame, false));
        shape.RevealFraction = 1f;
        int again = TestAssets.CountInked(session.ExportPixels(Frame, false));

        //Assert
        none.Should().Be(0);
        again.Should().Be(all);
        shape.IsFullyRevealed.Should().BeTrue();
        shape.GetBounds().Should().Be(new TextBlockShape(layout, 10, 10).GetBounds());
    }

    [Fact]
    public void Reveal_count_shows_the_first_units_of_the_chosen_kind()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        TextLayoutResult layout = Lay(session);
        var shape = new TextBlockShape(layout, 0, 0);

        //Act
        shape.RevealCount = 3;
        RectangleF? characters = shape.GetBounds();
        shape.RevealUnit = TextUnit.Word;
        shape.RevealCount = 1;
        RectangleF? firstWord = shape.GetBounds();
        shape.RevealUnit = TextUnit.Line;
        int lineCount = shape.GetUnitCount();
        shape.RevealFraction = 0.5f;
        RectangleF? firstLine = shape.GetBounds();

        //Assert
        float thirdRight = layout.Clusters[2].InkBounds.Value.Right;
        characters.Value.Right.Should().BeApproximately(thirdRight, 0.001f);
        firstWord.Value.Right.Should().BeApproximately(layout.GetPieces(TextUnit.Word)[0].InkBounds.Value.Right, 0.001f);
        lineCount.Should().Be(2);
        firstLine.Value.Bottom.Should().BeLessThan(layout.Lines[1].Top + layout.Lines[1].BaselineOffset);
        shape.GetUnitCount(TextUnit.Character).Should().Be(layout.ClusterCount);
    }

    [Fact]
    public void Soft_reveal_fades_the_leading_unit_in()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        TextLayoutResult layout = session.LayoutText("I", 120);
        var shape = new TextBlockShape(layout, 100, 40, Color.Black) { RevealSoftness = 1f };
        session.DrawShape(shape);
        RectangleF ink = shape.GetBounds().Value;
        int x = (int)(ink.Left + (ink.Width / 2f));
        int y = (int)(ink.Top + (ink.Height / 2f));

        //Act
        shape.RevealCount = 0.5f;
        byte half = TestAssets.PixelAt(session.ExportPixels(Frame, false), Frame.Width, x, y).A;
        shape.RevealCount = 1f;
        byte full = TestAssets.PixelAt(session.ExportPixels(Frame, false), Frame.Width, x, y).A;

        //Assert
        full.Should().Be(255);
        ((int)half).Should().BeInRange(115, 140);
    }

    [Fact]
    public void Pieces_together_have_exactly_the_blocks_outline()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var shape = new TextBlockShape(Lay(session), 15, 25, Color.Black);

        //Act
        IReadOnlyList<TextBlockShape> words = shape.CreatePieces(TextUnit.Word);
        IReadOnlyList<TextBlockShape> characters = shape.CreatePieces(TextUnit.Character);
        DrawingGroup lines = shape.CreatePieceGroup(TextUnit.Line);

        //Assert
        words.Count.Should().Be(4);
        characters.Count.Should().Be(shape.Layout.ClusterCount);
        lines.Count.Should().Be(2);
        int contours = 0;
        foreach (TextBlockShape word in words)
        {
            word.X.Should().Be(15f);
            contours += word.GetOutline().Contours.Count;
        }
        contours.Should().Be(shape.GetOutline().Contours.Count);
        words[1].GetOutline().ToSvgPathData().Should().Be(shape.Layout.GetPieces(TextUnit.Word)[1].GetOutline().Transformed(
            System.Numerics.Matrix3x2.CreateTranslation(15, 25)).ToSvgPathData());
    }

    [Fact]
    public void Outlined_text_is_stroked_and_its_bounds_grow_by_the_reach()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        TextLayoutResult layout = Lay(session);

        //Act
        var filled = new TextBlockShape(layout, 10, 10);
        var outlined = new TextBlockShape(layout, 10, 10, new PathStrokeStyle(4f), Color.Blue);
        session.DrawShape(outlined);
        byte[] pixels = session.ExportPixels(Frame, false);

        //Assert
        outlined.IsStroked.Should().BeTrue();
        outlined.IsFilled.Should().BeFalse();
        outlined.GetBounds().Value.Left.Should().BeApproximately(filled.GetBounds().Value.Left - 2f, 0.001f);
        TestAssets.CountInked(pixels).Should().BeGreaterThan(300);
    }

    [Fact]
    public void An_outside_stroke_draws_only_beyond_the_glyphs()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        TextLayoutResult layout = session.LayoutText("T", 160);
        var shape = new TextBlockShape(layout, 100, 20, new PathStrokeStyle(8f), Color.Black) { StrokePlacement = TextStrokePlacement.Outside };
        session.DrawShape(shape);
        RectangleF ink = new TextBlockShape(layout, 100, 20).GetBounds().Value;
        int stemX = (int)(ink.Left + (ink.Width / 2f));
        int barY = (int)(ink.Top + 6);

        //Act
        byte[] outside = session.ExportPixels(Frame, false);
        shape.StrokePlacement = TextStrokePlacement.Center;
        byte[] centred = session.ExportPixels(Frame, false);

        //Assert - inside the glyph (where the stem's contour overlaps the bar's) only the centred stroke draws
        TestAssets.PixelAt(outside, Frame.Width, stemX, barY + 30).A.Should().Be(0);
        TestAssets.PixelAt(outside, Frame.Width, stemX, (int)ink.Top - 2).A.Should().BeGreaterThan(100);
        TestAssets.CountInked(centred).Should().BeGreaterThan(TestAssets.CountInked(outside));
    }

    [Fact]
    public void Gradient_and_run_colors_paint_the_glyphs()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        var runs = new[]
        {
            new TextRunDescriptor("RED ", 60, null) { Color = Color.FromRgb(255, 0, 0) },
            new TextRunDescriptor("BLUE", 60, null) { Color = Color.FromRgb(0, 0, 255) },
        };
        TextLayoutResult layout = session.LayoutText(runs);
        var fill = new LinearGradientFill(new PointF(0, 0), new PointF(400, 0), Color.Black, Color.Black);

        //Act
        session.DrawShape(new TextBlockShape(layout, 10, 20, fill));
        byte[] pixels = session.ExportPixels(Frame, false);

        //Assert
        bool red = false, blue = false;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 255 && pixels[i] == 255 && pixels[i + 2] == 0) { red = true; }
            if (pixels[i + 3] == 255 && pixels[i] == 0 && pixels[i + 2] == 255) { blue = true; }
        }
        red.Should().BeTrue();
        blue.Should().BeTrue();
    }

    [Fact]
    public void Reveal_bound_to_a_track_follows_the_time_and_the_live_render_matches_a_fresh_one()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        TextLayoutResult layout = Lay(session);
        var shape = new TextBlockShape(layout, 10, 10);
        session.DrawShape(shape);
        session.Animations.Bind(shape, AnimatedProperty.RevealFraction, new FloatTrack { { 0, 0f }, { 2, 1f } });
        var info = new SKImageInfo(Frame.Width, Frame.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        //Act
        session.Time = 0.5;
        session.Render(canvas, info);
        session.Time = 1.0;
        session.Render(canvas, info);
        byte[] live = bitmap.Bytes;

        using DrawingSession fresh = CreateSession();
        var copy = new TextBlockShape(Lay(fresh), 10, 10) { RevealFraction = 0.5f };
        fresh.DrawShape(copy);
        using var freshBitmap = new SKBitmap(info);
        using var freshCanvas = new SKCanvas(freshBitmap);
        fresh.Render(freshCanvas, info);

        //Assert
        shape.RevealFraction.Should().BeApproximately(0.5f, 0.0001f);
        live.Should().Equal(freshBitmap.Bytes);
    }

    [Fact]
    public void Reveal_count_binding_accepts_counts_and_rejects_other_shapes()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var shape = new TextBlockShape(Lay(session), 0, 0);
        var line = new LineShape(0, 0, 10, 10);

        //Act
        session.Animations.Bind(shape, AnimatedProperty.RevealCount, new FloatTrack { { 0, -4f }, { 1, 5f } });
        session.Time = 1;
        Action wrong = () => session.Animations.Bind(line, AnimatedProperty.RevealFraction, new FloatTrack { { 0, 0f } });

        //Assert
        shape.RevealCount.Should().Be(5f);
        session.Time = 0;
        shape.RevealCount.Should().Be(0f);
        wrong.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Invalid_values_throw()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        TextLayoutResult layout = Lay(session);
        var shape = new TextBlockShape(layout, 0, 0);

        //Act
        Action noLayout = () => _ = new TextBlockShape(null, 0, 0);
        Action nothing = () => _ = new TextBlockShape(layout, 0, 0, (PathStrokeStyle)null);
        Action badFraction = () => shape.RevealFraction = 1.5f;
        Action badCount = () => shape.RevealCount = -1f;
        Action badSoftness = () => shape.RevealSoftness = Single.NaN;

        //Assert
        noLayout.Should().Throw<ArgumentNullException>();
        nothing.Should().Throw<ArgumentException>();
        badFraction.Should().Throw<ArgumentOutOfRangeException>();
        badCount.Should().Throw<ArgumentOutOfRangeException>();
        badSoftness.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Glyph_outlines_feed_a_draw_on_path_shape()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        TextLayoutResult layout = Lay(session, "Draw on", 60);

        //Act
        var path = new PathShape(layout.GetOutline(20, 40), new PathStrokeStyle(2f), Color.Black) { TrimEnd = 0f };
        session.DrawShape(path);
        int empty = TestAssets.CountInked(session.ExportPixels(Frame, false));
        path.TrimEnd = 0.5f;
        int half = TestAssets.CountInked(session.ExportPixels(Frame, false));
        path.TrimEnd = 1f;
        int full = TestAssets.CountInked(session.ExportPixels(Frame, false));

        //Assert
        empty.Should().Be(0);
        half.Should().BeGreaterThan(0);
        full.Should().BeGreaterThan(half);
    }

    [Fact]
    public void Pieces_animate_one_by_one_with_a_stagger()
    {
        //Arrange
        using DrawingSession session = CreateSession(background: false);
        var shape = new TextBlockShape(Lay(session), 10, 10, Color.Black);
        DrawingGroup group = shape.CreatePieceGroup(TextUnit.Word);
        session.DrawShape(group);
        session.Animations.BindStagger(group.GetChildren(), new StaggerOptions { Interval = 1, Duration = 1, FromOffset = new PointF(0, 20) });

        //Act
        session.Time = 0;
        int none = TestAssets.CountInked(session.ExportPixels(Frame, false));
        session.Time = 1.0;
        int firstWord = TestAssets.CountInked(session.ExportPixels(Frame, false));
        session.Time = 10;
        int all = TestAssets.CountInked(session.ExportPixels(Frame, false));

        //Assert
        none.Should().Be(0);
        firstWord.Should().BeGreaterThan(0);
        all.Should().BeGreaterThan(firstWord);
        group.GetChildren()[0].Transform.Translation.Should().Be(new PointF(0, 0));
    }

    [Fact]
    public void MergeOverlaps_joins_each_glyphs_overlapping_contours()
    {
        //Arrange - Roboto builds H and T from overlapping strokes
        using DrawingSession session = CreateSession();
        TextLayoutResult layout = session.LayoutText("HT", 120);
        var shape = new TextBlockShape(layout, 20, 20, new PathStrokeStyle(4f), Color.Black);

        //Act
        int plainContours = shape.GetOutline().Contours.Count;
        shape.MergeOverlaps = true;
        PathGeometry merged = shape.GetOutline();
        IReadOnlyList<TextBlockShape> pieces = shape.CreatePieces(TextUnit.Character);

        //Assert
        TextBlockShape.MergeTolerance.Should().Be(0.1f);
        new TextBlockShape(layout, 0, 0).MergeOverlaps.Should().BeFalse();
        plainContours.Should().Be(5);
        merged.Contours.Count.Should().Be(2);
        merged.ToSvgPathData().Should().Be(shape.GetOutline().ToSvgPathData());
        shape.IsDynamic.Should().BeTrue();
        pieces[0].MergeOverlaps.Should().BeTrue();
        pieces[0].GetOutline().Contours.Count.Should().Be(1);
    }

    [Fact]
    public void A_merged_centred_outline_draws_no_lines_inside_the_glyphs_over_transparency()
    {
        //Arrange
        using DrawingSession plain = CreateSession(background: false);
        using DrawingSession merged = CreateSession(background: false);
        TextLayoutResult layout = plain.LayoutText("HTE", 120);
        plain.DrawShape(new TextBlockShape(layout, 20, 20, new PathStrokeStyle(3f), Color.FromRgb(200, 0, 0)));
        merged.DrawShape(new TextBlockShape(layout, 20, 20, new PathStrokeStyle(3f), Color.FromRgb(200, 0, 0)) { MergeOverlaps = true });

        //Act
        byte[] plainPixels = plain.ExportPixels(Frame, includeBackground: false);
        byte[] mergedPixels = merged.ExportPixels(Frame, includeBackground: false);

        //Assert - the overlap lines are gone, so the outline inks fewer pixels; nothing else changes
        int plainInk = TestAssets.CountInked(plainPixels);
        int mergedInk = TestAssets.CountInked(mergedPixels);
        mergedInk.Should().BeLessThan(plainInk - 100);
        mergedInk.Should().BeGreaterThan(plainInk / 2);
        TestAssets.PixelAt(mergedPixels, 400, 2, 2).A.Should().Be((byte)0);
    }
}
