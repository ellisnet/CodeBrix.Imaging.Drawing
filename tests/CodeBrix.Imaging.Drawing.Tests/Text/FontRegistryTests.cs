using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Text;

public class FontRegistryTests
{
    [Fact]
    public void RegisterFont_from_a_path_returns_the_declared_family_name()
    {
        //Arrange
        var fonts = new FontRegistry();

        //Act
        string name = fonts.RegisterFont(TestAssets.OpenSansPath);

        //Assert
        name.Should().Be("Open Sans");
        fonts.Count.Should().Be(1);
        fonts.Contains("open sans").Should().BeTrue();
        fonts.Contains(null).Should().BeTrue();
    }

    [Fact]
    public void RegisterFont_from_bytes_and_stream_honors_the_override_name()
    {
        //Arrange
        var fonts = new FontRegistry();
        byte[] data = File.ReadAllBytes(TestAssets.OpenSansPath);

        //Act
        string fromBytes = fonts.RegisterFont(data, "Body");
        using var stream = new MemoryStream(data);
        string fromStream = fonts.RegisterFont(stream, "Heading");

        //Assert
        fromBytes.Should().Be("Body");
        fromStream.Should().Be("Heading");
        fonts.GetRegisteredFamilyNames().Should().Equal("Body", "Heading");
        fonts.Contains("Heading").Should().BeTrue();
        fonts.Contains("Open Sans").Should().BeTrue();
    }

    [Fact]
    public void RegisterFont_rejects_data_that_is_not_a_font()
    {
        //Arrange
        var fonts = new FontRegistry();
        Action act = () => fonts.RegisterFont(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        //Act + Assert
        act.Should().Throw<ArgumentException>();
        fonts.Count.Should().Be(0);
    }

    [Fact]
    public void An_empty_registry_contains_nothing_and_refuses_to_measure()
    {
        //Arrange
        var fonts = new FontRegistry();
        Action act = () => fonts.MeasureText("Hello", 20);

        //Act + Assert
        fonts.Contains(null).Should().BeFalse();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MeasureText_rejects_an_unregistered_family()
    {
        //Arrange
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.OpenSansPath);
        Action act = () => fonts.MeasureText("Hello", 20, familyName: "Some System Font");

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeasureText_scales_with_size_and_grows_with_tracking()
    {
        //Arrange
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.OpenSansPath);

        //Act
        float small = fonts.MeasureText("Venus Rosette", 20);
        float large = fonts.MeasureText("Venus Rosette", 40);
        float tracked = fonts.MeasureText("Venus Rosette", 20, tracking: 0.1f);

        //Assert - twelve gaps between thirteen characters, each a tenth of an em (2 units)
        small.Should().BeGreaterThan(0);
        large.Should().BeApproximately(small * 2, 0.05f);
        tracked.Should().BeApproximately(small + (12 * 2f), 0.05f);
        fonts.MeasureText(String.Empty, 20).Should().Be(0);
        fonts.MeasureText(null, 20).Should().Be(0);
    }

    [Fact]
    public void RegisterFont_from_many_threads_while_others_measure_registers_every_family()
    {
        //Arrange
        var fonts = new FontRegistry();
        byte[] data = File.ReadAllBytes(TestAssets.OpenSansPath);
        fonts.RegisterFont(data, "Face base");
        float expected = fonts.MeasureText("Hello", 20, familyName: "Face base");
        int wrongWidths = 0;

        //Act
        Parallel.For(0, 64, i =>
        {
            fonts.RegisterFont(data, "Face " + i);
            if (fonts.MeasureText("Hello", 20, familyName: "Face base") != expected)
            {
                Interlocked.Increment(ref wrongWidths);
            }
        });

        //Assert
        wrongWidths.Should().Be(0);
        fonts.Count.Should().Be(65);
        for (int i = 0; i < 64; i++) { fonts.Contains("Face " + i).Should().BeTrue(); }
    }
}
