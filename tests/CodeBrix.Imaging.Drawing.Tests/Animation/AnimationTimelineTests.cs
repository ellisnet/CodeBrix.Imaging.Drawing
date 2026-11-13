using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Animation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Animation;

public class AnimationTimelineTests
{
    [Fact]
    public void Constructor_computes_frames_that_start_before_the_end()
    {
        //Arrange + Act
        var whole = new AnimationTimeline(21.0, 30);
        var fractional = new AnimationTimeline(305.56, 30);
        var tiny = new AnimationTimeline(0.001, 30);

        //Assert
        whole.FrameCount.Should().Be(630);
        fractional.FrameCount.Should().Be(9167);
        tiny.FrameCount.Should().Be(1);
        whole.FrameDuration.Should().BeApproximately(1.0 / 30, 1e-15);
        whole.Duration.Should().Be(21.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_rejects_a_duration_that_is_not_positive_and_finite(double duration)
    {
        //Arrange
        Action act = () => _ = new AnimationTimeline(duration);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FrameAt_and_TimeOfFrame_round_trip_every_frame()
    {
        //Arrange
        var timeline = new AnimationTimeline(10.0, 29.97);

        //Act + Assert
        for (int frame = 0; frame < timeline.FrameCount; frame++)
        {
            timeline.FrameAt(timeline.TimeOfFrame(frame)).Should().Be(frame);
        }
    }

    [Fact]
    public void FrameAt_floors_and_clamps()
    {
        //Arrange
        var timeline = new AnimationTimeline(2.0, 10);

        //Act + Assert
        timeline.FrameAt(0.149).Should().Be(1);
        timeline.FrameAt(-5).Should().Be(0);
        timeline.FrameAt(99).Should().Be(19);
        timeline.FrameAt(double.NaN).Should().Be(0);
        timeline.Clamp(3).Should().Be(2.0);
    }

    [Fact]
    public void FormatTimecode_counts_hours_minutes_seconds_and_frames()
    {
        //Arrange
        var timeline = new AnimationTimeline(4000, 30);

        //Act + Assert
        timeline.FormatTimecode(0).Should().Be("00:00:00:00");
        timeline.FormatTimecode(12.25).Should().Be("00:00:12:07");
        timeline.FormatTimecode(3725.5).Should().Be("01:02:05:15");
        timeline.FormatTimecode(-3).Should().Be("00:00:00:00");
    }

    [Fact]
    public void Cue_progress_is_linear_eased_and_clamped()
    {
        //Arrange
        var timeline = new AnimationTimeline(10);
        timeline.AddCue("move", 2, 6);
        timeline.AddCue("fade", 2, 6, EasingKind.SmoothStep);

        //Act + Assert
        timeline.Progress("move", 1).Should().Be(0);
        timeline.Progress("move", 3).Should().Be(0.25);
        timeline.Progress("move", 7).Should().Be(1);
        timeline.Progress("fade", 3).Should().Be(Easing.SmoothStep(0.25));
        timeline.GetCue("fade").LinearProgress(5).Should().Be(0.75);
    }

    [Fact]
    public void Cue_with_a_custom_curve_uses_it()
    {
        //Arrange
        var timeline = new AnimationTimeline(10);
        TimelineCue cue = timeline.AddCue("ease", 0, 4, CubicBezierEasing.EaseInOut.Evaluate);

        //Act + Assert
        cue.HasCustomCurve.Should().BeTrue();
        cue.Progress(1).Should().Be(CubicBezierEasing.EaseInOut.Evaluate(0.25));
    }

    [Fact]
    public void Markers_report_progress_as_a_step_and_are_active_in_their_frame()
    {
        //Arrange
        var timeline = new AnimationTimeline(10, 10);
        TimelineCue beat = timeline.AddMarker("beat", 2.0);
        timeline.AddCue("intro", 0, 3);

        //Act
        IReadOnlyList<TimelineCue> atBeat = timeline.GetActiveCues(2.05);
        IReadOnlyList<TimelineCue> later = timeline.GetActiveCues(2.15);

        //Assert
        beat.IsMarker.Should().BeTrue();
        beat.Progress(1.99).Should().Be(0);
        beat.Progress(2.0).Should().Be(1);
        atBeat.Select(c => c.Name).Should().Equal("beat", "intro");
        later.Select(c => c.Name).Should().Equal("intro");
    }

    [Fact]
    public void GetActiveCues_uses_half_open_ranges()
    {
        //Arrange
        var timeline = new AnimationTimeline(10);
        timeline.AddCue("a", 0, 2);
        timeline.AddCue("b", 2, 4);

        //Act + Assert
        timeline.GetActiveCues(2).Select(c => c.Name).Should().Equal("b");
        timeline.GetActiveCues(4).Should().BeEmpty();
        timeline.GetCue("a").IsActiveAt(0).Should().BeTrue();
        timeline.GetCue("a").IsActiveAt(2).Should().BeFalse();
        timeline.GetCue("b").StartTime.Should().Be(2);
        timeline.GetCue("b").EndTime.Should().Be(4);
        timeline.GetCue("b").Duration.Should().Be(2);
    }

    [Fact]
    public void AddCue_rejects_duplicate_names_and_backwards_ranges()
    {
        //Arrange
        var timeline = new AnimationTimeline(10);
        timeline.AddCue("intro", 0, 1);
        Action duplicate = () => timeline.AddCue("intro", 2, 3);
        Action backwards = () => timeline.AddCue("bad", 3, 2);
        Action unknown = () => timeline.Progress("missing", 1);

        //Act + Assert
        duplicate.Should().Throw<ArgumentException>();
        backwards.Should().Throw<ArgumentOutOfRangeException>();
        unknown.Should().Throw<ArgumentException>();
        timeline.GetCue("missing").Should().BeNull();
    }

    [Fact]
    public void AddCue_from_many_threads_keeps_every_cue_and_lets_one_name_win_once()
    {
        //Arrange
        var timeline = new AnimationTimeline(10);
        int duplicatesAccepted = 0;

        //Act
        Parallel.For(0, 200, i => timeline.AddCue("cue " + i, i / 20.0, i / 20.0 + 1));
        Parallel.For(0, 64, _ =>
        {
            try
            {
                timeline.AddCue("shared", 1, 2);
                Interlocked.Increment(ref duplicatesAccepted);
            }
            catch (ArgumentException)
            {
            }
        });

        //Assert
        timeline.Cues.Count.Should().Be(201);
        timeline.Cues.Select(cue => cue.Name).Distinct().Count().Should().Be(201);
        duplicatesAccepted.Should().Be(1);
        timeline.GetCue("cue 117").Should().NotBeNull();
    }
}
