using System;
using CodeBrix.Imaging.Drawing.Animation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Animation;

public class EasingTests
{
    public static TheoryData<EasingKind> AllKinds()
    {
        var data = new TheoryData<EasingKind>();
        foreach (EasingKind kind in Enum.GetValues<EasingKind>()) { data.Add(kind); }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Evaluate_starts_at_zero_and_ends_at_one(EasingKind kind)
    {
        //Act
        double start = Easing.Evaluate(kind, 0);
        double end = Easing.Evaluate(kind, 1);

        //Assert
        start.Should().BeApproximately(0, 1e-9);
        end.Should().BeApproximately(1, 1e-9);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Evaluate_clamps_out_of_range_and_nan_input(EasingKind kind)
    {
        //Act + Assert
        Easing.Evaluate(kind, -3).Should().Be(Easing.Evaluate(kind, 0));
        Easing.Evaluate(kind, 7).Should().Be(Easing.Evaluate(kind, 1));
        Easing.Evaluate(kind, Double.NaN).Should().Be(Easing.Evaluate(kind, 0));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void GetFunction_matches_evaluate_and_is_cached(EasingKind kind)
    {
        //Act
        Func<double, double> first = Easing.GetFunction(kind);
        Func<double, double> second = Easing.GetFunction(kind);

        //Assert
        first.Should().BeSameAs(second);
        first(0.37).Should().Be(Easing.Evaluate(kind, 0.37));
    }

    [Fact]
    public void GetFunction_rejects_undefined_kind()
    {
        //Arrange
        Action act = () => Easing.GetFunction((EasingKind)12345);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(EasingKind.Linear, 0.25, 0.25)]
    [InlineData(EasingKind.SmoothStep, 0.25, 0.15625)]
    [InlineData(EasingKind.SmootherStep, 0.5, 0.5)]
    [InlineData(EasingKind.QuadIn, 0.5, 0.25)]
    [InlineData(EasingKind.QuadOut, 0.5, 0.75)]
    [InlineData(EasingKind.QuadInOut, 0.25, 0.125)]
    [InlineData(EasingKind.CubicIn, 0.5, 0.125)]
    [InlineData(EasingKind.CubicOut, 0.5, 0.875)]
    [InlineData(EasingKind.CubicInOut, 0.75, 0.9375)]
    [InlineData(EasingKind.QuartIn, 0.5, 0.0625)]
    [InlineData(EasingKind.QuintIn, 0.5, 0.03125)]
    [InlineData(EasingKind.QuintOut, 0.5, 0.96875)]
    [InlineData(EasingKind.SineInOut, 0.5, 0.5)]
    [InlineData(EasingKind.ExpoIn, 0.5, 0.03125)]
    [InlineData(EasingKind.CircInOut, 0.5, 0.5)]
    [InlineData(EasingKind.BounceOut, 0.5, 0.765625)]
    public void Evaluate_matches_the_conventional_formula(EasingKind kind, double t, double expected)
    {
        //Act
        double value = Easing.Evaluate(kind, t);

        //Assert
        value.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void InOut_curves_are_symmetric_about_the_midpoint(EasingKind kind)
    {
        //Arrange - only the symmetric InOut curves (and the step functions) qualify
        string name = kind.ToString();
        if (!name.EndsWith("InOut", StringComparison.Ordinal) && kind != EasingKind.SmoothStep && kind != EasingKind.SmootherStep) { return; }

        //Act + Assert
        foreach (double t in new[] { 0.1, 0.2, 0.3, 0.45 })
        {
            (Easing.Evaluate(kind, t) + Easing.Evaluate(kind, 1 - t)).Should().BeApproximately(1, 1e-9);
        }
    }

    [Fact]
    public void Back_and_elastic_curves_overshoot()
    {
        //Act + Assert
        Easing.BackIn(0.2).Should().BeLessThan(0);
        Easing.BackOut(0.8).Should().BeGreaterThan(1);
        Easing.ElasticOut(0.15).Should().BeGreaterThan(1);
        Easing.BackIn(0.2, 0).Should().BeApproximately(Easing.CubicIn(0.2), 1e-12);
    }

    [Fact]
    public void Monotonic_curves_never_decrease()
    {
        //Arrange
        EasingKind[] monotonic =
        {
            EasingKind.Linear, EasingKind.SmoothStep, EasingKind.SmootherStep, EasingKind.QuadInOut, EasingKind.CubicOut,
            EasingKind.QuartIn, EasingKind.QuintInOut, EasingKind.SineIn, EasingKind.ExpoInOut, EasingKind.CircOut,
        };

        //Act + Assert
        foreach (EasingKind kind in monotonic)
        {
            double previous = 0;
            for (int i = 1; i <= 100; i++)
            {
                double value = Easing.Evaluate(kind, i / 100.0);
                value.Should().BeGreaterThanOrEqualTo(previous - 1e-12);
                previous = value;
            }
        }
    }

    [Theory]
    [InlineData(StepPosition.End, 4, 0.0, 0.0)]
    [InlineData(StepPosition.End, 4, 0.3, 0.25)]
    [InlineData(StepPosition.End, 4, 0.999, 0.75)]
    [InlineData(StepPosition.End, 4, 1.0, 1.0)]
    [InlineData(StepPosition.Start, 4, 0.0, 0.25)]
    [InlineData(StepPosition.Start, 4, 0.8, 1.0)]
    [InlineData(StepPosition.None, 3, 0.0, 0.0)]
    [InlineData(StepPosition.None, 3, 0.5, 0.5)]
    [InlineData(StepPosition.None, 3, 0.7, 1.0)]
    [InlineData(StepPosition.Both, 2, 0.0, 1.0 / 3)]
    [InlineData(StepPosition.Both, 2, 0.6, 2.0 / 3)]
    [InlineData(StepPosition.Both, 2, 1.0, 1.0)]
    public void Steps_follow_the_css_steps_function(StepPosition position, int count, double t, double expected)
    {
        //Act
        double value = Easing.Steps(t, count, position);

        //Assert
        value.Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void Steps_rejects_too_few_steps()
    {
        //Arrange
        Action zero = () => Easing.Steps(0.5, 0);
        Action noneWithOne = () => Easing.Steps(0.5, 1, StepPosition.None);

        //Act + Assert
        zero.Should().Throw<ArgumentOutOfRangeException>();
        noneWithOne.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SmoothStep_between_edges_matches_glsl()
    {
        //Act + Assert
        Easing.SmoothStep(10, 20, 5).Should().Be(0);
        Easing.SmoothStep(10, 20, 25).Should().Be(1);
        Easing.SmoothStep(10, 20, 15).Should().BeApproximately(0.5, 1e-12);
    }

    [Fact]
    public void SmoothStep_rejects_equal_edges()
    {
        //Arrange
        Action act = () => Easing.SmoothStep(3, 3, 1);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lerp_interpolates_and_extrapolates()
    {
        //Act + Assert
        Easing.Lerp(10.0, 20.0, 0.25).Should().Be(12.5);
        Easing.Lerp(10.0, 20.0, 1.5).Should().Be(25);
        Easing.Lerp(2f, 4f, 0.5).Should().Be(3f);
    }
}
