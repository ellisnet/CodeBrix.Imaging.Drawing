using System;
using CodeBrix.Imaging.Drawing.Models;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class DrawingLayerTests
{
    private static Stroke CreateStroke(int x = 1, int y = 1)
    {
        var stroke = new Stroke();
        stroke.AddPoint(x, y);
        return stroke;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_missing_name(string name)
    {
        //Arrange
        Action act = () => _ = new DrawingLayer(name, SKColors.Red);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_trims_name_and_stores_color()
    {
        //Arrange + Act
        var layer = new DrawingLayer("  Pain  ", SKColors.Magenta);

        //Assert
        layer.Name.Should().Be("Pain");
        layer.Color.Should().Be(SKColors.Magenta);
    }

    [Fact]
    public void AddStroke_adds_stroke_with_points()
    {
        //Arrange
        var layer = new DrawingLayer("Pain", SKColors.Red);

        //Act
        bool added = layer.AddStroke(CreateStroke());

        //Assert
        added.Should().BeTrue();
        layer.ElementCount.Should().Be(1);
    }

    [Fact]
    public void AddStroke_ignores_null_stroke()
        => new DrawingLayer("Pain", SKColors.Red).AddStroke(null).Should().BeFalse();

    [Fact]
    public void AddStroke_ignores_empty_stroke()
        => new DrawingLayer("Pain", SKColors.Red).AddStroke(new Stroke()).Should().BeFalse();

    [Fact]
    public void AddStroke_does_not_bump_reset_version()
    {
        //Arrange
        var layer = new DrawingLayer("Pain", SKColors.Red);
        int versionBefore = layer.ResetVersion;

        //Act
        layer.AddStroke(CreateStroke());

        //Assert - appends render incrementally, so no cache reset is needed
        layer.ResetVersion.Should().Be(versionBefore);
    }

    [Fact]
    public void RemoveLastElement_removes_and_bumps_reset_version()
    {
        //Arrange
        var layer = new DrawingLayer("Pain", SKColors.Red);
        layer.AddStroke(CreateStroke(1, 1));
        layer.AddStroke(CreateStroke(2, 2));
        int versionBefore = layer.ResetVersion;

        //Act
        bool removed = layer.RemoveLastElement();

        //Assert
        removed.Should().BeTrue();
        layer.ElementCount.Should().Be(1);
        layer.GetStrokes()[0].GetPoints()[0].X.Should().Be(1);
        layer.ResetVersion.Should().NotBe(versionBefore);
    }

    [Fact]
    public void RemoveLastElement_returns_false_when_empty()
        => new DrawingLayer("Pain", SKColors.Red).RemoveLastElement().Should().BeFalse();

    [Fact]
    public void Clear_removes_strokes_and_bumps_reset_version()
    {
        //Arrange
        var layer = new DrawingLayer("Pain", SKColors.Red);
        layer.AddStroke(CreateStroke());
        int versionBefore = layer.ResetVersion;

        //Act
        layer.Clear();

        //Assert
        layer.ElementCount.Should().Be(0);
        layer.ResetVersion.Should().NotBe(versionBefore);
    }

    [Fact]
    public void Clear_on_empty_layer_does_not_bump_reset_version()
    {
        //Arrange
        var layer = new DrawingLayer("Pain", SKColors.Red);
        int versionBefore = layer.ResetVersion;

        //Act
        layer.Clear();

        //Assert
        layer.ResetVersion.Should().Be(versionBefore);
    }

    [Fact]
    public void Color_change_bumps_reset_version()
    {
        //Arrange
        var layer = new DrawingLayer("Pain", SKColors.Red);
        int versionBefore = layer.ResetVersion;

        //Act
        layer.Color = SKColors.Blue;

        //Assert
        layer.ResetVersion.Should().NotBe(versionBefore);
    }

    [Fact]
    public void Color_set_to_same_value_does_not_bump_reset_version()
    {
        //Arrange
        var layer = new DrawingLayer("Pain", SKColors.Red);
        int versionBefore = layer.ResetVersion;

        //Act
        layer.Color = SKColors.Red;

        //Assert
        layer.ResetVersion.Should().Be(versionBefore);
    }
}
