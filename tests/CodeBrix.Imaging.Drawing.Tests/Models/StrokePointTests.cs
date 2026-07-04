using CodeBrix.Imaging.Drawing.Models;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class StrokePointTests
{
    [Fact]
    public void Constructor_stores_values()
    {
        //Arrange + Act
        var point = new StrokePoint(12, 34, 56);

        //Assert
        point.X.Should().Be(12);
        point.Y.Should().Be(34);
        point.TimeOffsetMs.Should().Be(56);
    }

    [Fact]
    public void TimeOffsetMs_defaults_to_zero()
        => new StrokePoint(1, 2).TimeOffsetMs.Should().Be(0);

    [Fact]
    public void IsSamePositionAs_ignores_time_offset()
        => new StrokePoint(5, 6, 100).IsSamePositionAs(new StrokePoint(5, 6, 999)).Should().BeTrue();

    [Fact]
    public void IsSamePositionAs_detects_different_positions()
        => new StrokePoint(5, 6).IsSamePositionAs(new StrokePoint(5, 7)).Should().BeFalse();

    [Fact]
    public void Equals_considers_position_and_time()
    {
        //Arrange
        var point = new StrokePoint(1, 2, 3);

        //Act + Assert
        (point == new StrokePoint(1, 2, 3)).Should().BeTrue();
        (point != new StrokePoint(1, 2, 4)).Should().BeTrue();
    }
}
