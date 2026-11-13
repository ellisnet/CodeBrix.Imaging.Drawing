using System;
using CodeBrix.Imaging.Drawing.Animation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Animation;

public class KeyframeDerivationTests
{
    [Fact]
    public void Curve_returns_the_custom_curve_and_null_for_named_easing()
    {
        //Arrange
        Func<double, double> curve = u => u * u * u;
        var custom = new Keyframe<float>(1, 2f, curve);
        var named = new Keyframe<float>(1, 2f, EasingKind.QuadOut);

        //Assert
        custom.Curve.Should().BeSameAs(curve);
        custom.HasCustomCurve.Should().BeTrue();
        named.Curve.Should().BeNull();
        Keyframe<float>.Hold(1, 2f).Curve.Should().BeNull();
    }

    [Fact]
    public void WithValue_keeps_time_easing_curve_and_hold()
    {
        //Arrange
        Func<double, double> curve = u => Math.Sqrt(u);
        var custom = new Keyframe<float>(2, 1f, curve);
        var named = new Keyframe<float>(3, 1f, EasingKind.BounceOut);
        Keyframe<float> hold = Keyframe<float>.Hold(4, 1f);

        //Act
        Keyframe<float> a = custom.WithValue(9f);
        Keyframe<float> b = named.WithValue(9f);
        Keyframe<float> c = hold.WithValue(9f);

        //Assert
        a.Time.Should().Be(2);
        a.Value.Should().Be(9f);
        a.Curve.Should().BeSameAs(curve);
        b.Easing.Should().Be(EasingKind.BounceOut);
        b.HasCustomCurve.Should().BeFalse();
        c.IsHold.Should().BeTrue();
        c.Time.Should().Be(4);
    }

    [Fact]
    public void A_track_mirrored_key_by_key_is_eased_exactly_like_the_original()
    {
        //Arrange - a custom curve, a named one and a hold
        var source = new FloatTrack();
        source.Add(0, 0f).Add(1, 1f, u => u * u).Add(2, 0.25f, EasingKind.SineInOut).AddHold(3, 2f);
        var mirrored = new FloatTrack();
        foreach (Keyframe<float> key in source.GetKeyframes()) { mirrored.Add(key.WithValue(key.Value * 4f)); }

        //Assert
        for (double t = -0.5; t <= 3.5; t += 0.125)
        {
            mirrored.Evaluate(t).Should().Be(source.Evaluate(t) * 4f);
        }
    }

    [Fact]
    public void BoolTrack_steps_at_its_keys_and_loops()
    {
        //Arrange
        var track = new BoolTrack { { 0, false }, { 2.5, true }, { 6, false } };
        var blink = new BoolTrack { { 0, true }, { 0.5, false }, { 1, true } };
        blink.LoopMode = TrackLoopMode.Loop;

        //Assert
        track.Evaluate(-1).Should().BeFalse();
        track.Evaluate(2.4999).Should().BeFalse();
        track.Evaluate(2.5).Should().BeTrue();
        track.Evaluate(5.99).Should().BeTrue();
        track.Evaluate(6).Should().BeFalse();
        blink.Evaluate(10.25).Should().BeTrue();
        blink.Evaluate(10.75).Should().BeFalse();
    }
}
