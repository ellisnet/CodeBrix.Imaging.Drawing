using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Text;

//Roboto Regular carries GPOS kerning (Open Sans does not), so it is the kerning test font
public class KerningTests
{
    private static FontRegistry CreateRoboto()
    {
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.RobotoPath);
        return fonts;
    }

    private static FontRegistry CreateOpenSans()
    {
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.OpenSansPath);
        return fonts;
    }

    [Theory]
    [InlineData("To")]
    [InlineData("AV")]
    [InlineData("WAVE")]
    public void Untracked_text_is_kerned_by_default_when_the_font_has_kerning_data(string text)
    {
        //Arrange
        FontRegistry fonts = CreateRoboto();

        //Act
        var kerned = new TextShape(text, 0, 100, 100, fonts);
        var plain = new TextShape(text, 0, 100, 100, fonts, (Color?)null, TextAlign.Left, 0f, null, kerning: false);

        //Assert
        kerned.Kerning.Should().BeTrue();
        plain.Kerning.Should().BeFalse();
        kerned.Width.Should().BeLessThan(plain.Width - 1f);
    }

    [Fact]
    public void The_measured_kerning_of_To_matches_the_font_stack()
    {
        //Arrange
        FontRegistry fonts = CreateRoboto();

        //Act
        float kerned = fonts.MeasureText("To", 100);
        float plain = fonts.MeasureText("To", 100, 0f, null, kerning: false);

        //Assert - 111.87 kerned vs 116.70 unkerned at size 100
        kerned.Should().BeApproximately(111.865f, 0.01f);
        plain.Should().BeApproximately(116.699f, 0.01f);
    }

    [Fact]
    public void Tracked_text_keeps_its_kerning()
    {
        //Arrange
        FontRegistry fonts = CreateRoboto();
        const float size = 80f;
        const float tracking = 0.1f;

        //Act
        float untracked = new TextShape("WAVE To", 0, 100, size, fonts).Width;
        float tracked = new TextShape("WAVE To", 0, 100, size, fonts, tracking: tracking).Width;
        float trackedPlain = new TextShape("WAVE To", 0, 100, size, fonts, (Color?)null, TextAlign.Left, tracking, null, kerning: false).Width;

        //Assert - seven characters, six tracked gaps, and the kerned advances underneath
        tracked.Should().BeApproximately(untracked + (6 * tracking * size), 0.01f);
        tracked.Should().BeLessThan(trackedPlain - 1f);
    }

    [Fact]
    public void Tracked_glyphs_sit_at_the_kerned_positions_plus_the_tracking()
    {
        //Arrange
        FontRegistry fonts = CreateRoboto();
        float kernedTo = fonts.MeasureText("To", 100);
        float plainT = fonts.MeasureText("T", 100);
        float o = fonts.MeasureText("o", 100);

        //Act - the o's ink starts where the kerned pen puts it, plus one tracking gap
        var tracked = new TextShape("To", 0, 100, 100, fonts, tracking: 0.2f);
        var untracked = new TextShape("To", 0, 100, 100, fonts);

        //Assert
        tracked.Width.Should().BeApproximately(kernedTo + 20f, 0.001f);
        (kernedTo - o).Should().BeLessThan(plainT - 1f);
        tracked.GetBounds().Value.Right.Should().BeApproximately(untracked.GetBounds().Value.Right + 20f, 0.01f);
    }

    [Fact]
    public void A_font_without_kerning_data_lays_out_the_same_either_way()
    {
        //Arrange
        FontRegistry fonts = CreateOpenSans();

        //Act
        float kerned = fonts.MeasureText("AV To", 60);
        float plain = fonts.MeasureText("AV To", 60, 0f, null, kerning: false);

        //Assert
        kerned.Should().Be(plain);
    }

    [Fact]
    public void The_layout_engine_kerns_unless_told_not_to()
    {
        //Arrange
        FontRegistry fonts = CreateRoboto();

        //Act
        TextLayoutResult kerned = TextLayoutEngine.Layout(fonts, "To", 100);
        TextLayoutResult plain = TextLayoutEngine.Layout(fonts, "To", 100, options: new TextLayoutOptions { Kerning = false });

        //Assert
        kerned.Clusters[1].Origin.X.Should().BeApproximately(fonts.MeasureText("To", 100) - fonts.MeasureText("o", 100), 0.001f);
        plain.Clusters[1].Origin.X.Should().BeApproximately(fonts.MeasureText("T", 100), 0.001f);
        kerned.Width.Should().BeLessThan(plain.Width - 1f);
    }

    [Fact]
    public void Kerning_survives_wrapping_and_run_tracking()
    {
        //Arrange
        FontRegistry fonts = CreateRoboto();
        var options = new TextLayoutOptions { MaxWidth = 400 };

        //Act
        TextLayoutResult kerned = TextLayoutEngine.Layout(fonts, new[] { new TextRunDescriptor("To To To To To", 60, null) { Tracking = 0.05f } }, options);
        options.Kerning = false;
        TextLayoutResult plain = TextLayoutEngine.Layout(fonts, new[] { new TextRunDescriptor("To To To To To", 60, null) { Tracking = 0.05f } }, options);

        //Assert
        kerned.Lines[0].Width.Should().BeLessThan(plain.Lines[0].Width);
        float gap = kerned.Clusters[1].Origin.X - kerned.Clusters[0].Origin.X;
        gap.Should().BeApproximately(kerned.Clusters[0].Advance + 3f, 0.001f);
    }

    [Fact]
    public void A_ligature_is_one_cluster_covering_its_characters()
    {
        //Arrange
        FontRegistry fonts = CreateRoboto();

        //Act
        TextLayoutResult layout = TextLayoutEngine.Layout(fonts, "office", 50);

        //Assert - Roboto draws "ffi" as one glyph
        layout.ClusterCount.Should().Be(4);
        layout.Clusters[1].Text.Should().Be("ffi");
        layout.Clusters[1].TextIndex.Should().Be(1);
        layout.Clusters[1].TextLength.Should().Be(3);
        layout.GetClusterIndexAt(2).Should().Be(1);
    }

    [Fact]
    public void Kerning_option_is_on_by_default()
    {
        //Arrange
        var options = new TextLayoutOptions();

        //Act
        bool kerning = options.Kerning;

        //Assert
        kerning.Should().BeTrue();
    }
}
