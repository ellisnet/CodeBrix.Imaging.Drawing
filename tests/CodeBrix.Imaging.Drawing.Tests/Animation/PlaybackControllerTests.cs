using System;
using CodeBrix.Imaging.Drawing.Animation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Animation;

public class PlaybackControllerTests
{
    [Fact]
    public void Starts_paused_at_zero_and_ignores_advances_while_paused()
    {
        //Arrange
        var playback = new PlaybackController(10);

        //Act
        bool ended = playback.Advance(0.05);

        //Assert
        ended.Should().BeFalse();
        playback.IsPlaying.Should().BeFalse();
        playback.Time.Should().Be(0);
    }

    [Fact]
    public void Advance_moves_by_real_time_capped_so_a_slow_frame_pauses()
    {
        //Arrange
        var playback = new PlaybackController(10);
        playback.Play();

        //Act
        playback.Advance(0.04);
        playback.Advance(2.5);

        //Assert - the 2.5 s stall counts as only the 0.1 s cap
        playback.Time.Should().BeApproximately(0.14, 1e-12);
    }

    [Fact]
    public void Speed_scales_time_and_a_negative_speed_plays_backwards()
    {
        //Arrange
        var playback = new PlaybackController(10) { Speed = 2, MaxStep = 1 };
        playback.Play();

        //Act
        playback.Advance(0.5);
        double forward = playback.Time;
        playback.Speed = -1;
        playback.Advance(0.25);

        //Assert
        forward.Should().Be(1.0);
        playback.Time.Should().Be(0.75);
    }

    [Fact]
    public void Reaching_the_end_stops_and_reports_it_once()
    {
        //Arrange
        var playback = new PlaybackController(1) { MaxStep = 1 };
        playback.Play();
        playback.Advance(0.9);

        //Act
        bool first = playback.Advance(0.5);
        bool second = playback.Advance(0.5);

        //Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        playback.Time.Should().Be(1);
        playback.IsAtEnd.Should().BeTrue();
        playback.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void Toggle_at_the_end_plays_again_from_the_start()
    {
        //Arrange
        var playback = new PlaybackController(1);
        playback.Seek(1);

        //Act
        playback.Toggle();

        //Assert
        playback.IsPlaying.Should().BeTrue();
        playback.Time.Should().Be(0);
        playback.Toggle();
        playback.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void Looping_wraps_around_and_keeps_playing()
    {
        //Arrange
        var playback = new PlaybackController(1) { IsLooping = true, MaxStep = 1 };
        playback.Play();
        playback.Advance(0.75);

        //Act
        bool wrapped = playback.Advance(0.5);

        //Assert
        wrapped.Should().BeTrue();
        playback.Time.Should().BeApproximately(0.25, 1e-12);
        playback.IsPlaying.Should().BeTrue();
    }

    [Fact]
    public void Seek_and_SeekBy_clamp_to_the_animation()
    {
        //Arrange
        var playback = new PlaybackController(5);

        //Act + Assert
        playback.Seek(7);
        playback.Time.Should().Be(5);
        playback.SeekBy(-6);
        playback.Time.Should().Be(0);
        playback.Seek(double.NaN);
        playback.Time.Should().Be(0);
    }

    [Fact]
    public void Step_pauses_and_moves_by_whole_frames()
    {
        //Arrange
        var playback = new PlaybackController(1, 10);
        playback.Play();
        playback.Seek(0.25);

        //Act
        playback.Step();
        double next = playback.Time;
        playback.Step(-5);

        //Assert
        playback.IsPlaying.Should().BeFalse();
        next.Should().BeApproximately(0.3, 1e-12);
        playback.Time.Should().Be(0);
        playback.Frame.Should().Be(0);
    }

    [Fact]
    public void Restart_goes_to_zero_and_plays()
    {
        //Arrange
        var playback = new PlaybackController(3);
        playback.Seek(2);

        //Act
        playback.Restart();

        //Assert
        playback.Time.Should().Be(0);
        playback.IsPlaying.Should().BeTrue();
    }

    [Fact]
    public void Settings_reject_invalid_values()
    {
        //Arrange
        var playback = new PlaybackController(3);
        Action speed = () => playback.Speed = double.NaN;
        Action step = () => playback.MaxStep = 0;

        //Act + Assert
        speed.Should().Throw<ArgumentOutOfRangeException>();
        step.Should().Throw<ArgumentOutOfRangeException>();
    }
}
