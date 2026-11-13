using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Animation;

public class KeyframeTrackTests
{
    [Fact]
    public void Evaluate_holds_the_end_values_outside_the_keys()
    {
        //Arrange
        var track = new FloatTrack { { 1, 10f }, { 3, 30f } };

        //Act + Assert
        track.LoopMode.Should().Be(TrackLoopMode.Hold);
        default(TrackLoopMode).Should().Be(TrackLoopMode.Hold);
        track.Evaluate(0).Should().Be(10f);
        track.Evaluate(1).Should().Be(10f);
        track.Evaluate(3).Should().Be(30f);
        track.Evaluate(99).Should().Be(30f);
        track.Evaluate(double.NaN).Should().Be(10f);
    }

    [Fact]
    public void Evaluate_interpolates_linearly_by_default()
    {
        //Arrange
        var track = new FloatTrack { { 0, 0f }, { 2, 100f } };

        //Act + Assert
        track.Evaluate(0.5).Should().Be(25f);
        track.Evaluate(1.5).Should().Be(75f);
    }

    [Fact]
    public void The_easing_of_a_key_shapes_the_segment_arriving_at_it()
    {
        //Arrange
        var track = new FloatTrack { { 0, 0f }, { 1, 100f, EasingKind.CubicOut }, { 2, 0f } };

        //Act
        float first = track.Evaluate(0.25);
        float second = track.Evaluate(1.25);

        //Assert
        first.Should().Be(Easing.Lerp(0f, 100f, Easing.CubicOut(0.25)));
        second.Should().Be(75f);
    }

    [Fact]
    public void A_custom_curve_shapes_its_segment()
    {
        //Arrange
        var track = new FloatTrack();
        track.Add(0, 0f).Add(4, 8f, t => t * t);

        //Act + Assert
        track.Evaluate(2).Should().Be(2f);
    }

    [Fact]
    public void A_hold_key_keeps_the_previous_value_until_its_time()
    {
        //Arrange
        var track = new FloatTrack();
        track.Add(0, 1f).AddHold(2, 5f).Add(4, 9f);

        //Act + Assert
        track.Evaluate(1.999).Should().Be(1f);
        track.Evaluate(2).Should().Be(5f);
        track.Evaluate(3).Should().Be(7f);
    }

    [Fact]
    public void Keys_at_the_same_time_jump_to_the_later_one()
    {
        //Arrange
        var track = new FloatTrack { { 0, 0f }, { 1, 10f }, { 1, 50f }, { 2, 60f } };

        //Act + Assert
        track.Evaluate(0.5).Should().Be(5f);
        track.Evaluate(1).Should().Be(50f);
        track.Evaluate(1.5).Should().Be(55f);
    }

    [Fact]
    public void Keys_added_out_of_order_are_sorted()
    {
        //Arrange
        var track = new FloatTrack { { 2, 20f }, { 0, 0f }, { 1, 5f } };

        //Act
        Keyframe<float>[] keys = track.GetKeyframes();

        //Assert
        keys[0].Time.Should().Be(0);
        keys[1].Time.Should().Be(1);
        keys[2].Time.Should().Be(2);
        track.StartTime.Should().Be(0);
        track.EndTime.Should().Be(2);
        track.Duration.Should().Be(2);
    }

    [Fact]
    public void Loop_repeats_the_span_in_both_directions()
    {
        //Arrange
        var track = new FloatTrack { { 1, 0f }, { 3, 100f } };
        track.LoopMode = TrackLoopMode.Loop;

        //Act + Assert
        track.Evaluate(2).Should().Be(50f);
        track.Evaluate(4).Should().Be(50f);
        track.Evaluate(3).Should().Be(0f);
        track.Evaluate(0).Should().Be(50f);
    }

    [Fact]
    public void PingPong_plays_forward_then_back()
    {
        //Arrange
        var track = new FloatTrack { { 0, 0f }, { 2, 100f } };
        track.LoopMode = TrackLoopMode.PingPong;

        //Act + Assert
        track.Evaluate(1).Should().Be(50f);
        track.Evaluate(2).Should().Be(100f);
        track.Evaluate(3).Should().Be(50f);
        track.Evaluate(4).Should().Be(0f);
        track.Evaluate(5).Should().Be(50f);
    }

    [Fact]
    public void Evaluate_on_an_empty_track_throws()
    {
        //Arrange
        var track = new FloatTrack();
        Action act = () => track.Evaluate(0);

        //Act + Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PointTrack_interpolates_each_coordinate()
    {
        //Arrange
        var track = new PointTrack { { 0, new PointF(0, 100) }, { 4, new PointF(40, 0) } };

        //Act
        PointF point = track.Evaluate(1);

        //Assert
        point.Should().Be(new PointF(10, 75));
    }

    [Fact]
    public void ColorTrack_interpolates_like_a_color_ramp()
    {
        //Arrange
        Color from = Color.FromRgba(0, 100, 200, 0);
        Color to = Color.FromRgba(255, 0, 100, 255);
        var track = new ColorTrack { { 0, from }, { 1, to } };
        var ramp = new ColorRamp(from, to);

        //Act
        Rgba32 quarter = track.Evaluate(0.25).ToPixel<Rgba32>();

        //Assert
        quarter.Should().Be(new Rgba32(64, 75, 175, 64));
        for (int i = 0; i <= 20; i++)
        {
            track.Evaluate(i / 20.0).ToPixel<Rgba32>().Should().Be(ramp.At(i / 20.0).ToPixel<Rgba32>());
        }
    }

    [Fact]
    public void ColorTrack_saturates_when_a_curve_overshoots()
    {
        //Arrange
        var track = new ColorTrack { { 0, Color.FromRgb(0, 0, 0) }, { 1, Color.FromRgb(250, 0, 0), EasingKind.BackOut } };

        //Act
        Rgba32 overshoot = track.Evaluate(0.6).ToPixel<Rgba32>();

        //Assert
        overshoot.R.Should().Be((byte)255);
    }

    [Fact]
    public void Keyframe_rejects_a_time_that_is_not_finite()
    {
        //Arrange
        Action act = () => _ = new Keyframe<float>(double.NaN, 1f);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_from_many_threads_while_others_evaluate_keeps_every_key_in_time_order()
    {
        //Arrange
        var track = new FloatTrack { { -1, 0f } };
        int failures = 0;

        //Act
        Parallel.For(0, 800, i =>
        {
            try
            {
                if (i % 2 == 0) { track.Add(i / 2, i / 2f); }
                else { track.Evaluate(i / 4.0); }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref failures);
            }
        });

        //Assert
        failures.Should().Be(0);
        Keyframe<float>[] keys = track.GetKeyframes();
        keys.Length.Should().Be(401);
        for (int k = 1; k < keys.Length; k++)
        {
            keys[k].Time.Should().BeGreaterThan(keys[k - 1].Time);
            keys[k].Value.Should().Be((float)keys[k].Time);
        }
    }
}
