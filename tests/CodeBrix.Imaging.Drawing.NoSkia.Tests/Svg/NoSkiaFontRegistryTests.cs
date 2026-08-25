using System.IO;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;
using CodeBrix.Imaging.Drawing.TestSupport;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// Covers the registry's second job. Its first is supplying typefaces for rendering; its
/// second is handing the font file itself back, so a consumer that re-emits a picture into a
/// container with its own font support - a PDF writer embedding the face behind a text run -
/// can embed the very bytes the outlines were measured from. Resolution follows exactly the
/// rules rendering follows, fallback included, or the embedded face would not be the face
/// that was drawn.
/// </summary>
public class NoSkiaFontRegistryTests
{
    private const string TestFontFamily = "Open Sans";

    private static byte[] TestFontBytes => File.ReadAllBytes(SvgTestAssets.TestFontPath);

    [Fact]
    public void A_font_registered_from_a_file_hands_that_file_back()
    {
        //Arrange
        var registry = new NoSkiaFontRegistry();
        registry.RegisterFont(SvgTestAssets.TestFontPath).Should().Be(TestFontFamily);

        //Act
        bool found = registry.TryGetFontData(TestFontFamily, out byte[] data, out string resolvedFamilyName);

        //Assert
        found.Should().BeTrue();
        data.Should().Equal(TestFontBytes);
        resolvedFamilyName.Should().Be(TestFontFamily);
    }

    [Fact]
    public void A_font_registered_from_bytes_or_a_stream_hands_the_same_bytes_back()
    {
        //Arrange
        byte[] expected = TestFontBytes;
        var fromBytes = new NoSkiaFontRegistry();
        var fromStream = new NoSkiaFontRegistry();
        fromBytes.RegisterFont(expected);
        using (FileStream stream = File.OpenRead(SvgTestAssets.TestFontPath))
        {
            fromStream.RegisterFont(stream);
        }

        //Act
        fromBytes.TryGetFontData(TestFontFamily, out byte[] bytesData, out _).Should().BeTrue();
        fromStream.TryGetFontData(TestFontFamily, out byte[] streamData, out _).Should().BeTrue();

        //Assert
        bytesData.Should().Equal(expected);
        streamData.Should().Equal(expected);
    }

    [Fact]
    public void An_unregistered_family_resolves_to_the_fallback_rendering_would_use()
    {
        //Arrange
        var registry = new NoSkiaFontRegistry();
        registry.RegisterFont(SvgTestAssets.TestFontPath);

        //Act - rendering falls back to the first registered font, and so does this
        bool found = registry.TryGetFontData("Nothing Like This", out byte[] data, out string resolvedFamilyName);

        //Assert
        found.Should().BeTrue();
        data.Should().Equal(TestFontBytes);
        resolvedFamilyName.Should().Be(TestFontFamily);
    }

    [Fact]
    public void A_css_family_list_resolves_candidate_by_candidate()
    {
        //Arrange
        var registry = new NoSkiaFontRegistry();
        registry.RegisterFont(SvgTestAssets.TestFontPath);

        //Act
        bool found = registry.TryGetFontData(
            "Nonexistent, 'Open Sans', sans-serif", out byte[] data, out string resolvedFamilyName);

        //Assert
        found.Should().BeTrue();
        data.Should().Equal(TestFontBytes);
        resolvedFamilyName.Should().Be(TestFontFamily);
    }

    [Fact]
    public void An_override_name_resolves_to_the_font_it_was_registered_for()
    {
        //Arrange
        var registry = new NoSkiaFontRegistry();
        registry.RegisterFont(SvgTestAssets.TestFontPath, "Document Body").Should().Be("Document Body");

        //Act
        bool found = registry.TryGetFontData("Document Body", out byte[] data, out string resolvedFamilyName);

        //Assert - the override is how the document asks for it; the embedded name is what
        //  the outlines were measured under, which is what a text command records
        found.Should().BeTrue();
        data.Should().Equal(TestFontBytes);
        resolvedFamilyName.Should().Be(TestFontFamily);
    }

    [Fact]
    public void An_empty_registry_resolves_nothing()
    {
        //Arrange
        var registry = new NoSkiaFontRegistry();

        //Act
        bool found = registry.TryGetFontData(TestFontFamily, out byte[] data, out string resolvedFamilyName);

        //Assert
        found.Should().BeFalse();
        data.Should().BeNull();
        resolvedFamilyName.Should().BeNull();
    }

    [Fact]
    public void The_bytes_handed_out_are_a_copy()
    {
        //Arrange
        var registry = new NoSkiaFontRegistry();
        registry.RegisterFont(SvgTestAssets.TestFontPath);

        //Act - a caller that writes into what it was given must not corrupt the registry
        registry.TryGetFontData(TestFontFamily, out byte[] first, out _).Should().BeTrue();
        first[0] = 0xFF;
        registry.TryGetFontData(TestFontFamily, out byte[] second, out _).Should().BeTrue();

        //Assert
        second.Should().Equal(TestFontBytes);
    }

    [Fact]
    public void A_registered_font_is_still_matchable_and_countable()
    {
        //Arrange
        var registry = new NoSkiaFontRegistry();

        //Act
        registry.RegisterFont(SvgTestAssets.TestFontPath, "Body");

        //Assert - registering through the retained buffer changed none of the old behavior
        registry.Count.Should().Be(1);
        registry.GetRegisteredFamilyNames().Should().Equal(new[] { "Body" });
        registry.TryFindFamily("Body", out CodeBrix.Imaging.Fonts.FontFamily byOverride).Should().BeTrue();
        byOverride.Name.Should().Be(TestFontFamily);
        registry.TryFindFamily(TestFontFamily, out CodeBrix.Imaging.Fonts.FontFamily byEmbedded).Should().BeTrue();
        byEmbedded.Name.Should().Be(TestFontFamily);
        registry.TryFindFamily("Nothing Like This", out _).Should().BeFalse();
    }
}
