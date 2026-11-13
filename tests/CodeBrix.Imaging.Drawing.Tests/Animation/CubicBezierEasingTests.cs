using System;
using CodeBrix.Imaging.Drawing.Animation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Animation;

public class CubicBezierEasingTests
{
    [Fact]
    public void Evaluate_on_the_diagonal_curve_is_linear()
    {
        //Arrange - control points on the diagonal make cubic-bezier the identity
        var curve = new CubicBezierEasing(1.0 / 3, 1.0 / 3, 2.0 / 3, 2.0 / 3);

        //Act + Assert
        foreach (double t in new[] { 0.1, 0.25, 0.5, 0.9 })
        {
            curve.Evaluate(t).Should().BeApproximately(t, 1e-6);
        }
    }

    [Fact]
    public void Evaluate_is_exact_at_the_ends_and_clamps()
    {
        //Arrange
        CubicBezierEasing curve = CubicBezierEasing.Ease;

        //Act + Assert
        curve.Evaluate(0).Should().Be(0);
        curve.Evaluate(1).Should().Be(1);
        curve.Evaluate(-1).Should().Be(0);
        curve.Evaluate(2).Should().Be(1);
        curve.Evaluate(Double.NaN).Should().Be(0);
    }

    [Fact]
    public void Ease_in_out_is_symmetric()
    {
        //Arrange
        CubicBezierEasing curve = CubicBezierEasing.EaseInOut;

        //Act + Assert
        curve.Evaluate(0.5).Should().BeApproximately(0.5, 1e-6);
        (curve.Evaluate(0.2) + curve.Evaluate(0.8)).Should().BeApproximately(1, 1e-6);
    }

    [Fact]
    public void Evaluate_matches_known_css_ease_values()
    {
        //Arrange - CSS "ease" at its midpoint is about 0.8024
        CubicBezierEasing curve = CubicBezierEasing.Ease;

        //Act
        double value = curve.Evaluate(0.5);

        //Assert
        value.Should().BeApproximately(0.8024033877399112, 1e-5);
    }

    [Fact]
    public void Y_outside_zero_to_one_overshoots()
    {
        //Arrange - a "back out" style curve
        var curve = new CubicBezierEasing(0.34, 1.56, 0.64, 1);

        //Act
        double peak = 0;
        for (int i = 0; i <= 100; i++) { peak = Math.Max(peak, curve.Evaluate(i / 100.0)); }

        //Assert
        peak.Should().BeGreaterThan(1);
    }

    [Theory]
    [InlineData(-0.1, 0, 0.5, 1)]
    [InlineData(0.5, 0, 1.1, 1)]
    [InlineData(Double.NaN, 0, 0.5, 1)]
    [InlineData(0.2, Double.PositiveInfinity, 0.5, 1)]
    public void Constructor_rejects_invalid_control_points(double x1, double y1, double x2, double y2)
    {
        //Arrange
        Action act = () => _ = new CubicBezierEasing(x1, y1, x2, y2);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Easing_CubicBezier_matches_an_instance()
    {
        //Act + Assert
        Easing.CubicBezier(0.3, 0.42, 0, 0.58, 1).Should().Be(CubicBezierEasing.EaseInOut.Evaluate(0.3));
    }
}
