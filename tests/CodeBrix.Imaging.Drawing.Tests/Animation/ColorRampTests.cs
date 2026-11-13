using System;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Animation;

public class ColorRampTests
{
    private static Rgba32 Pixel(Color color) => color.ToPixel<Rgba32>();

    [Fact]
    public void At_returns_the_stop_colors_at_the_stops()
    {
        //Arrange
        var ramp = new ColorRamp(Color.FromRgb(246, 112, 168), Color.FromRgb(232, 146, 92), Color.FromRgb(156, 122, 248));

        //Act + Assert
        Pixel(ramp.At(0)).Should().Be(new Rgba32(246, 112, 168, 255));
        Pixel(ramp.At(0.5)).Should().Be(new Rgba32(232, 146, 92, 255));
        Pixel(ramp.At(1)).Should().Be(new Rgba32(156, 122, 248, 255));
    }

    [Fact]
    public void At_interpolates_straight_srgb_channels_with_rounding()
    {
        //Arrange
        var ramp = new ColorRamp(Color.FromRgba(0, 100, 200, 0), Color.FromRgba(255, 0, 100, 255));

        //Act
        Rgba32 quarter = Pixel(ramp.At(0.25));

        //Assert - 63.75 -> 64, 75, 175, 63.75 -> 64
        quarter.Should().Be(new Rgba32(64, 75, 175, 64));
    }

    [Fact]
    public void At_with_alpha_replaces_the_alpha()
    {
        //Arrange
        var ramp = new ColorRamp(Color.Red, Color.Blue);

        //Act
        Rgba32 pixel = Pixel(ramp.At(0, 40));

        //Assert
        pixel.Should().Be(new Rgba32(255, 0, 0, 40));
    }

    [Fact]
    public void At_holds_the_end_colors_outside_the_stops_and_clamps()
    {
        //Arrange
        var ramp = new ColorRamp(new[] { new ColorStop(0.2f, Color.Red), new ColorStop(0.8f, Color.Blue) });

        //Act + Assert
        Pixel(ramp.At(0.1)).Should().Be(Pixel(Color.Red));
        Pixel(ramp.At(0.9)).Should().Be(Pixel(Color.Blue));
        Pixel(ramp.At(-5)).Should().Be(Pixel(Color.Red));
        Pixel(ramp.At(Double.NaN)).Should().Be(Pixel(Color.Red));
        Pixel(ramp.At(5)).Should().Be(Pixel(Color.Blue));
    }

    [Fact]
    public void At_a_shared_position_takes_the_later_stop()
    {
        //Arrange - a hard edge at 0.5
        var ramp = new ColorRamp(new[]
        {
            new ColorStop(0f, Color.Red), new ColorStop(0.5f, Color.Red),
            new ColorStop(0.5f, Color.Lime), new ColorStop(1f, Color.Lime),
        });

        //Act + Assert
        Pixel(ramp.At(0.4999)).Should().Be(Pixel(Color.Red));
        Pixel(ramp.At(0.5)).Should().Be(Pixel(Color.Lime));
    }

    [Fact]
    public void Constructor_rejects_too_few_or_unordered_stops()
    {
        //Arrange
        Action one = () => _ = new ColorRamp(Color.Red);
        Action unordered = () => _ = new ColorRamp(new[] { new ColorStop(0.6f, Color.Red), new ColorStop(0.2f, Color.Blue) });
        Action none = () => _ = new ColorRamp((ColorStop[])null);

        //Act + Assert
        one.Should().Throw<ArgumentException>();
        unordered.Should().Throw<ArgumentException>();
        none.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetStops_returns_evenly_spaced_copies()
    {
        //Arrange
        var ramp = new ColorRamp(Color.Red, Color.Green, Color.Blue);

        //Act
        ColorStop[] stops = ramp.GetStops();
        stops[0] = new ColorStop(0.3f, Color.Black);

        //Assert
        ramp.StopCount.Should().Be(3);
        ramp.GetStops()[0].Position.Should().Be(0f);
        ramp.GetStops()[1].Position.Should().Be(0.5f);
        ramp.GetStops()[2].Position.Should().Be(1f);
    }

    [Fact]
    public void ColorStop_rejects_positions_outside_zero_to_one()
    {
        //Arrange
        Action below = () => _ = new ColorStop(-0.1f, Color.Red);
        Action nan = () => _ = new ColorStop(Single.NaN, Color.Red);

        //Act + Assert
        below.Should().Throw<ArgumentOutOfRangeException>();
        nan.Should().Throw<ArgumentOutOfRangeException>();
    }
}
