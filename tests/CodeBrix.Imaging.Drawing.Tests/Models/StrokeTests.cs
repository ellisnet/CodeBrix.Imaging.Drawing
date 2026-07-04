using System;
using CodeBrix.Imaging.Drawing.Models;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class StrokeTests
{
    [Fact]
    public void Constructor_uses_default_width()
        => new Stroke().Width.Should().Be(Stroke.DefaultWidth);

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void Constructor_rejects_non_positive_width(float width)
    {
        //Arrange
        Action act = () => _ = new Stroke(width);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_stores_start_timestamp()
    {
        //Arrange
        var startedAt = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

        //Act
        var stroke = new Stroke(10f, startedAt);

        //Assert
        stroke.StartedAtUtc.Should().Be(startedAt);
    }

    [Fact]
    public void AddPoint_adds_points_in_order()
    {
        //Arrange
        var stroke = new Stroke();

        //Act
        stroke.AddPoint(1, 1, 0);
        stroke.AddPoint(2, 2, 10);
        stroke.AddPoint(3, 3, 20);

        //Assert
        stroke.PointCount.Should().Be(3);
        StrokePoint[] points = stroke.GetPoints();
        points[0].X.Should().Be(1);
        points[1].X.Should().Be(2);
        points[2].X.Should().Be(3);
    }

    [Fact]
    public void AddPoint_ignores_consecutive_duplicate_position()
    {
        //Arrange
        var stroke = new Stroke();
        stroke.AddPoint(5, 5, 0);

        //Act
        bool added = stroke.AddPoint(5, 5, 50);

        //Assert
        added.Should().BeFalse();
        stroke.PointCount.Should().Be(1);
    }

    [Fact]
    public void AddPoint_allows_returning_to_earlier_position()
    {
        //Arrange
        var stroke = new Stroke();
        stroke.AddPoint(5, 5);
        stroke.AddPoint(6, 6);

        //Act
        bool added = stroke.AddPoint(5, 5);

        //Assert
        added.Should().BeTrue();
        stroke.PointCount.Should().Be(3);
    }

    [Fact]
    public void LastPoint_returns_most_recent_point()
    {
        //Arrange
        var stroke = new Stroke();
        stroke.AddPoint(1, 1);
        stroke.AddPoint(9, 8, 25);

        //Act
        StrokePoint? last = stroke.LastPoint;

        //Assert
        last.Should().NotBeNull();
        last.Value.X.Should().Be(9);
        last.Value.Y.Should().Be(8);
        last.Value.TimeOffsetMs.Should().Be(25);
    }

    [Fact]
    public void LastPoint_is_null_for_empty_stroke()
        => new Stroke().LastPoint.Should().BeNull();
}
