using System;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class TextShapeTests
{
    private static FontRegistry CreateFonts()
    {
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.OpenSansPath);
        return fonts;
    }

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(400, 200),
            LayerOpacity = 255,
            BackgroundFillColor = Color.White,
        });
        session.Fonts.RegisterFont(TestAssets.OpenSansPath);
        session.AddLayer("Text", Color.Black);
        return session;
    }

    [Fact]
    public void Width_matches_the_registry_measurement()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        var shape = new TextShape("Earth years", 10, 50, 30, fonts, tracking: 0.05f);

        //Assert
        shape.Width.Should().Be(fonts.MeasureText("Earth years", 30, tracking: 0.05f));
        shape.FamilyName.Should().Be("Open Sans");
        shape.Text.Should().Be("Earth years");
    }

    [Theory]
    [InlineData(TextAlign.Left)]
    [InlineData(TextAlign.Center)]
    [InlineData(TextAlign.Right)]
    public void Alignment_positions_the_line_about_its_anchor(TextAlign align)
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        var shape = new TextShape("Threads", 200, 100, 40, fonts, align: align);

        //Assert
        float expectedLeft = align switch
        {
            TextAlign.Center => 200 - (shape.Width / 2),
            TextAlign.Right => 200 - shape.Width,
            _ => 200,
        };
        shape.Left.Should().BeApproximately(expectedLeft, 1e-4f);
        RectangleF bounds = shape.GetBounds().Value;
        bounds.Left.Should().BeGreaterThanOrEqualTo(expectedLeft - 4);
        bounds.Right.Should().BeLessThanOrEqualTo(expectedLeft + shape.Width + 4);
    }

    [Fact]
    public void Bounds_sit_on_the_baseline()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act - capitals rise above the baseline and do not descend below it
        RectangleF bounds = new TextShape("HELLO", 0, 100, 50, fonts).GetBounds().Value;

        //Assert - Open Sans cap height is about 0.71 em
        bounds.Bottom.Should().BeApproximately(100, 1.5f);
        bounds.Top.Should().BeApproximately(100 - (0.714f * 50), 2f);
    }

    [Fact]
    public void Tracking_widens_the_line_by_the_gaps_between_characters()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();

        //Act
        var plain = new TextShape("ABCD", 0, 50, 20, fonts);
        var tracked = new TextShape("ABCD", 0, 50, 20, fonts, tracking: 0.5f);

        //Assert - three gaps of half an em (10 units)
        tracked.Width.Should().BeApproximately(plain.Width + 30, 0.01f);
        tracked.GetBounds().Value.Width.Should().BeGreaterThan(plain.GetBounds().Value.Width + 25);
    }

    [Fact]
    public void Whitespace_only_text_has_an_empty_bounds_and_draws_nothing()
    {
        //Arrange
        using DrawingSession session = CreateSession();

        //Act
        TextShape shape = session.DrawText("   ", 50, 100, 30);
        byte[] pixels = session.ExportPixels(new Size(400, 200), includeBackground: false);

        //Assert
        shape.GetBounds().Value.Width.Should().Be(0);
        TestAssets.CountInked(pixels).Should().Be(0);
    }

    [Fact]
    public void DrawText_renders_glyph_ink_inside_its_bounds()
    {
        //Arrange
        using DrawingSession session = CreateSession();

        //Act
        TextShape shape = session.DrawText("Venus", 20, 120, 80, Color.Red);
        byte[] pixels = session.ExportPixels(new Size(400, 200), includeBackground: false);

        //Assert - plenty of ink, all of it red, none outside the reported bounds
        RectangleF bounds = shape.GetBounds().Value;
        int inked = 0;
        for (int y = 0; y < 200; y++)
        {
            for (int x = 0; x < 400; x++)
            {
                Rgba32 pixel = TestAssets.PixelAt(pixels, 400, x, y);
                if (pixel.A == 0) { continue; }
                inked++;
                (x + 1).Should().BeGreaterThan((int)Math.Floor(bounds.Left) - 1);
                x.Should().BeLessThan((int)Math.Ceiling(bounds.Right) + 1);
                (y + 1).Should().BeGreaterThan((int)Math.Floor(bounds.Top) - 1);
                y.Should().BeLessThan((int)Math.Ceiling(bounds.Bottom) + 1);
                if (pixel.A == 255) { pixel.Should().Be(new Rgba32(255, 0, 0, 255)); }
            }
        }
        inked.Should().BeGreaterThan(1500);
    }

    [Fact]
    public void Text_takes_a_fill()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var fill = new LinearGradientFill(new PointF(0, 0), new PointF(400, 0), Color.Blue, Color.Blue);

        //Act
        session.DrawShape(new TextShape("IIII", 20, 150, 140, session.Fonts, fill));
        byte[] pixels = session.ExportPixels(new Size(400, 200), includeBackground: false);

        //Assert - some solid pixel inside the first I's stem is blue
        bool foundBlue = false;
        for (int x = 0; x < 120 && !foundBlue; x++)
        {
            foundBlue = TestAssets.PixelAt(pixels, 400, x, 100).Equals(new Rgba32(0, 0, 255, 255));
        }
        foundBlue.Should().BeTrue();
    }

    [Fact]
    public void Text_uses_the_layer_color_when_it_has_none()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.ActiveLayer.Color = Color.Lime;

        //Act
        session.DrawText("IIII", 20, 150, 140);
        byte[] pixels = session.ExportPixels(new Size(400, 200), includeBackground: false);

        //Assert
        bool foundLime = false;
        for (int x = 0; x < 120 && !foundLime; x++)
        {
            foundLime = TestAssets.PixelAt(pixels, 400, x, 100).Equals(new Rgba32(0, 255, 0, 255));
        }
        foundLime.Should().BeTrue();
    }

    [Fact]
    public void DrawText_without_a_registered_font_throws()
    {
        //Arrange
        using var session = new DrawingSession();
        session.AddLayer("Text", Color.Black);
        Action act = () => session.DrawText("Hello", 10, 10, 20);

        //Act + Assert
        act.Should().Throw<InvalidOperationException>();
        session.StrokeCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_validates_its_arguments()
    {
        //Arrange
        FontRegistry fonts = CreateFonts();
        Action noFonts = () => _ = new TextShape("A", 0, 0, 10, null);
        Action badSize = () => _ = new TextShape("A", 0, 0, 0, fonts);
        Action badAlign = () => _ = new TextShape("A", 0, 0, 10, fonts, align: (TextAlign)7);
        Action badTracking = () => _ = new TextShape("A", 0, 0, 10, fonts, tracking: Single.NaN);
        Action unknownFamily = () => _ = new TextShape("A", 0, 0, 10, fonts, familyName: "Arial");

        //Act + Assert
        noFonts.Should().Throw<ArgumentNullException>();
        badSize.Should().Throw<ArgumentOutOfRangeException>();
        badAlign.Should().Throw<ArgumentOutOfRangeException>();
        badTracking.Should().Throw<ArgumentOutOfRangeException>();
        unknownFamily.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Sessions_can_share_one_registry()
    {
        //Arrange
        FontRegistry shared = CreateFonts();

        //Act
        using var first = new DrawingSession(new DrawingSessionOptions { Fonts = shared });
        using DrawingSession second = DrawingSession.CreateForImage(
            new byte[] { 0, 0, 0, 255 }, 1, 1, new Size(10, 10), new DrawingSessionOptions { Fonts = shared });

        //Assert
        using var own = new DrawingSession();
        first.Fonts.Should().BeSameAs(shared);
        second.Fonts.Should().BeSameAs(shared);
        own.Fonts.Count.Should().Be(0);
    }
}
