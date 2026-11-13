using System;
using System.Collections.Generic;
using System.Text;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Text;

public class TextLayoutEngineTests
{
    private const string Paragraph = "The quick brown fox jumps over the lazy dog while the patient owl watches from the old oak tree.";

    private static FontRegistry CreateFonts()
    {
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.RobotoPath);
        fonts.RegisterFont(TestAssets.OpenSansPath);
        return fonts;
    }

    [Fact]
    public void Layout_wraps_to_the_width_without_starting_a_line_with_a_space()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, Paragraph, 32, options: new TextLayoutOptions { MaxWidth = 420 });

        //Assert
        layout.LineCount.Should().BeGreaterThan(2);
        layout.Width.Should().Be(420f);
        foreach (TextLineMetrics line in layout.Lines)
        {
            line.Width.Should().BeLessThanOrEqualTo(420f);
            layout.Text[line.TextIndex].Should().NotBe(' ');
        }
        layout.Lines[^1].EndsParagraph.Should().BeTrue();
        layout.Lines[0].EndsParagraph.Should().BeFalse();
    }

    [Fact]
    public void Lines_cover_the_whole_text_in_order()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, Paragraph, 30, options: new TextLayoutOptions { MaxWidth = 300 });

        //Assert
        int expected = 0;
        foreach (TextLineMetrics line in layout.Lines)
        {
            line.TextIndex.Should().Be(expected);
            expected += line.TextLength;
        }
        expected.Should().Be(Paragraph.Length);
        var all = new StringBuilder();
        foreach (TextCluster cluster in layout.Clusters) { all.Append(cluster.Text); }
        all.ToString().Should().Be(Paragraph);
    }

    [Fact]
    public void Explicit_line_breaks_start_new_lines_and_a_trailing_break_leaves_an_empty_line()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "one\ntwo\r\nthree\n", 20);

        //Assert
        layout.LineCount.Should().Be(4);
        layout.Lines[0].TextIndex.Should().Be(0);
        layout.Lines[1].TextIndex.Should().Be(4);
        layout.Lines[2].TextIndex.Should().Be(9);
        layout.Lines[3].ClusterCount.Should().Be(0);
        layout.ClusterCount.Should().Be(11);
    }

    [Fact]
    public void Line_spacing_and_explicit_line_height_set_the_line_pitch()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult natural = TextLayoutEngine.Layout(fonts, "a\nb", 100);
        TextLayoutResult spaced = TextLayoutEngine.Layout(fonts, "a\nb", 100, options: new TextLayoutOptions { LineSpacing = 1.5f });
        TextLayoutResult fixedHeight = TextLayoutEngine.Layout(fonts, "a\nb", 100, options: new TextLayoutOptions { LineHeight = 90 });

        //Assert - Roboto: ascender 1900, descender -500, no line gap, 2048 units per em
        natural.Lines[0].Height.Should().BeApproximately(2400f / 2048f * 100f, 0.001f);
        natural.Lines[0].BaselineOffset.Should().BeApproximately(1900f / 2048f * 100f, 0.001f);
        spaced.Lines[1].Top.Should().BeApproximately(natural.Lines[0].Height * 1.5f, 0.001f);
        fixedHeight.Lines[1].Top.Should().Be(90f);
        fixedHeight.Height.Should().Be(180f);
    }

    [Theory]
    [InlineData(TextAlign.Left)]
    [InlineData(TextAlign.Center)]
    [InlineData(TextAlign.Right)]
    public void Alignment_places_each_line_within_the_width(TextAlign align)
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, Paragraph, 28, options: new TextLayoutOptions { MaxWidth = 500, Align = align });

        //Assert
        foreach (TextLineMetrics line in layout.Lines)
        {
            float expected = align switch
            {
                TextAlign.Center => (500f - line.Width) / 2f,
                TextAlign.Right => 500f - line.Width,
                _ => 0f,
            };
            line.Left.Should().BeApproximately(expected, 0.0001f);
            layout.Clusters[line.FirstCluster].Origin.X.Should().BeApproximately(expected, 0.0001f);
        }
    }

    [Fact]
    public void Justify_stretches_every_line_but_the_paragraph_end()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, Paragraph, 28, options: new TextLayoutOptions { MaxWidth = 500, Align = TextAlign.Justify });

        //Assert
        for (int i = 0; i < layout.LineCount - 1; i++)
        {
            layout.Lines[i].Left.Should().Be(0f);
            layout.Lines[i].Width.Should().BeApproximately(500f, 0.01f);
        }
        layout.Lines[^1].Width.Should().BeLessThan(500f);
    }

    [Fact]
    public void Without_a_width_lines_align_to_the_widest()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "a long first line\nshort", 40, options: new TextLayoutOptions { Align = TextAlign.Center });

        //Assert
        layout.Width.Should().Be(layout.Lines[0].Width);
        layout.Lines[0].Left.Should().Be(0f);
        layout.Lines[1].Left.Should().BeApproximately((layout.Lines[0].Width - layout.Lines[1].Width) / 2f, 0.0001f);
    }

    [Theory]
    [InlineData(TextVerticalAlign.Top)]
    [InlineData(TextVerticalAlign.Center)]
    [InlineData(TextVerticalAlign.Bottom)]
    public void Vertical_alignment_places_the_lines_in_the_box(TextVerticalAlign align)
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "one\ntwo", 50, options: new TextLayoutOptions { MaxHeight = 400, VerticalAlign = align });

        //Assert
        float content = layout.ContentBounds.Height;
        float expectedTop = align switch
        {
            TextVerticalAlign.Center => (400f - content) / 2f,
            TextVerticalAlign.Bottom => 400f - content,
            _ => 0f,
        };
        layout.Height.Should().Be(400f);
        layout.Lines[0].Top.Should().BeApproximately(expectedTop, 0.0001f);
    }

    [Fact]
    public void Character_ellipsis_cuts_the_last_kept_line_to_fit()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, Paragraph, 30,
            options: new TextLayoutOptions { MaxWidth = 300, MaxLines = 2, Trimming = TextTrimming.CharacterEllipsis });

        //Assert
        layout.LineCount.Should().Be(2);
        layout.IsTruncated.Should().BeTrue();
        layout.HasEllipsis.Should().BeTrue();
        layout.Clusters[^1].Text.Should().Be("…");
        layout.Lines[1].Width.Should().BeLessThanOrEqualTo(300f);
        layout.ShownTextLength.Should().BeLessThan(Paragraph.Length);
        layout.Clusters[^1].TextIndex.Should().Be(layout.ShownTextLength);
    }

    [Fact]
    public void Word_ellipsis_cuts_after_a_whole_word()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, Paragraph, 30,
            options: new TextLayoutOptions { MaxWidth = 300, MaxLines = 2, Trimming = TextTrimming.WordEllipsis, Ellipsis = "..." });

        //Assert
        layout.HasEllipsis.Should().BeTrue();
        layout.Clusters[^1].Text.Should().Be("...");
        Paragraph[layout.ShownTextLength].Should().Be(' ');
        layout.Lines[1].Width.Should().BeLessThanOrEqualTo(300f);
    }

    [Fact]
    public void Without_trimming_extra_lines_are_dropped_silently_and_max_height_limits_lines()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();
        float lineHeight = 2400f / 2048f * 30f;

        //Act
        TextLayoutResult byLines = TextLayoutEngine.Layout(fonts, Paragraph, 30, options: new TextLayoutOptions { MaxWidth = 300, MaxLines = 1 });
        TextLayoutResult byHeight = TextLayoutEngine.Layout(fonts, Paragraph, 30, options: new TextLayoutOptions { MaxWidth = 300, MaxHeight = (lineHeight * 2.5f) });

        //Assert
        byLines.LineCount.Should().Be(1);
        byLines.IsTruncated.Should().BeTrue();
        byLines.HasEllipsis.Should().BeFalse();
        byHeight.LineCount.Should().Be(2);
        byHeight.IsTruncated.Should().BeTrue();
    }

    [Fact]
    public void Words_and_lines_are_reported_as_pieces()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "Hello brave new\nworld", 40);
        IReadOnlyList<TextPiece> words = layout.GetPieces(TextUnit.Word);
        IReadOnlyList<TextPiece> lines = layout.GetPieces(TextUnit.Line);
        IReadOnlyList<TextPiece> characters = layout.GetPieces(TextUnit.Character);

        //Assert
        layout.WordCount.Should().Be(4);
        words.Count.Should().Be(4);
        words[1].Text.Should().Be("brave");
        words[1].TextIndex.Should().Be(6);
        words[3].LineIndex.Should().Be(1);
        lines.Count.Should().Be(2);
        lines[1].Text.Should().Be("world");
        characters.Count.Should().Be(layout.ClusterCount);
        characters[5].IsWhiteSpace.Should().BeTrue();
        words[0].GetOutline().IsEmpty.Should().BeFalse();
        words[0].Center.X.Should().BeApproximately(words[0].Bounds.X + (words[0].Bounds.Width / 2f), 0.0001f);
    }

    [Fact]
    public void Clusters_carry_outlines_boxes_and_lookups()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "Ab c", 60);

        //Assert
        layout.Clusters[0].HasInk.Should().BeTrue();
        layout.Clusters[2].IsWhiteSpace.Should().BeTrue();
        layout.Clusters[2].HasInk.Should().BeFalse();
        layout.Clusters[2].WordIndex.Should().Be(-1);
        layout.Clusters[3].WordIndex.Should().Be(1);
        layout.Clusters[1].Bounds.X.Should().Be(layout.Clusters[1].Origin.X);
        layout.Clusters[1].Bounds.Height.Should().Be(layout.Lines[0].Height);
        layout.GetRectForIndex(1).Should().Be(layout.Clusters[1].Bounds);
        layout.GetRectForIndex(4).Width.Should().Be(0f);
        layout.GetLineIndexAt(3).Should().Be(0);
        RectangleF outline = layout.GetOutline().GetBounds();
        RectangleF ink = layout.InkBounds.Value;
        outline.Left.Should().BeGreaterThanOrEqualTo(ink.Left - 0.001f);
        outline.Right.Should().BeLessThanOrEqualTo(ink.Right + 0.001f);
    }

    [Fact]
    public void Runs_mix_sizes_and_families_on_one_line()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();
        var runs = new[]
        {
            new TextRunDescriptor("Big ", 80, "Roboto"),
            new TextRunDescriptor("small", 20, "Open Sans") { Color = Color.Red },
        };

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, runs);

        //Assert
        layout.Text.Should().Be("Big small");
        layout.LineCount.Should().Be(1);
        layout.Lines[0].Ascent.Should().BeApproximately(1900f / 2048f * 80f, 0.001f);
        layout.Clusters[4].RunIndex.Should().Be(1);
        layout.Clusters[0].Origin.Y.Should().Be(layout.Clusters[4].Origin.Y);
        layout.Runs[1].Color.Should().Be(Color.Red);
    }

    [Fact]
    public void Empty_text_lays_out_one_empty_line()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, null, 30);

        //Assert
        layout.LineCount.Should().Be(1);
        layout.ClusterCount.Should().Be(0);
        layout.InkBounds.Should().BeNull();
        layout.GetOutline().IsEmpty.Should().BeTrue();
        layout.Height.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Layout_is_deterministic()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();
        var options = new TextLayoutOptions { MaxWidth = 333, Align = TextAlign.Justify, LineSpacing = 1.2f };

        //Act
        string first = TextLayoutEngine.Layout(fonts, Paragraph, 31, null, options).GetOutline().ToSvgPathData();
        string second = TextLayoutEngine.Layout(fonts, Paragraph, 31, null, options).GetOutline().ToSvgPathData();

        //Assert
        first.Should().Be(second);
    }

    [Fact]
    public void Text_along_a_straight_path_lands_on_its_baseline()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "Path", 40);
        float baseline = layout.Lines[0].Baseline;

        //Act
        PathGeometry along = layout.GetOutlineAlongPath(PathGeometry.Line(new PointF(100, 300), new PointF(700, 300)), 20f);
        PathGeometry off = layout.GetOutlineAlongPath(PathGeometry.Line(new PointF(100, 300), new PointF(110, 300)), 20f);

        //Assert - a horizontal path only moves the glyphs: by (100 + 20, 300 - baseline)
        RectangleF moved = along.GetBounds();
        RectangleF original = layout.GetOutline().GetBounds();
        moved.Left.Should().BeApproximately(original.Left + 120f, 0.01f);
        moved.Top.Should().BeApproximately(original.Top + 300f - baseline, 0.01f);
        off.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Text_along_a_vertical_path_turns_with_it()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "IIII", 40);

        //Act - running downwards, the glyphs' "up" points to +x
        IReadOnlyList<PathGeometry> glyphs = layout.GetClusterOutlinesAlongPath(PathGeometry.Line(new PointF(200, 0), new PointF(200, 800)));

        //Assert
        RectangleF first = glyphs[0].GetBounds();
        first.Left.Should().BeGreaterThanOrEqualTo(199.9f);
        first.Height.Should().BeLessThan(first.Width);
        glyphs[1].GetBounds().Top.Should().BeGreaterThan(first.Top);
    }

    [Fact]
    public void Invalid_arguments_throw()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        Action noFonts = () => TextLayoutEngine.Layout(null, "a", 10);
        Action noRuns = () => TextLayoutEngine.Layout(fonts, Array.Empty<TextRunDescriptor>());
        Action badWidth = () => TextLayoutEngine.Layout(fonts, "a", 10, options: new TextLayoutOptions { MaxWidth = 0 });
        Action badSpacing = () => TextLayoutEngine.Layout(fonts, "a", 10, options: new TextLayoutOptions { LineSpacing = Single.NaN });
        Action unknown = () => TextLayoutEngine.Layout(fonts, "a", 10, "Nope");
        Action badSize = () => _ = new TextRunDescriptor("a", 0, null);
        Action emptyRegistry = () => TextLayoutEngine.Layout(new FontRegistry(), "a", 10);

        //Assert
        noFonts.Should().Throw<ArgumentNullException>();
        noRuns.Should().Throw<ArgumentException>();
        badWidth.Should().Throw<ArgumentOutOfRangeException>();
        badSpacing.Should().Throw<ArgumentOutOfRangeException>();
        unknown.Should().Throw<ArgumentException>();
        badSize.Should().Throw<ArgumentOutOfRangeException>();
        emptyRegistry.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_word_wider_than_the_line_is_broken_between_characters()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "Supercalifragilistic", 40, options: new TextLayoutOptions { MaxWidth = 150 });

        //Assert
        layout.LineCount.Should().BeGreaterThan(1);
        foreach (TextLineMetrics line in layout.Lines) { line.Width.Should().BeLessThanOrEqualTo(150f); }
    }
}
