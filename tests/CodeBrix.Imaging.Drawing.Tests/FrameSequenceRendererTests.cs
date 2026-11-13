using System;
using System.Collections.Generic;
using System.Threading;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class FrameSequenceRendererTests
{
    private static readonly Size Frame = new Size(96, 64);

    //A complete, time-pure scene: everything that changes is driven by the session's time
    private static DrawingSession CreateScene(bool transparent = false)
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = Frame,
            LayerOpacity = 255,
            BackgroundFillColor = transparent ? Color.Transparent : Color.FromRgb(10, 12, 30),
        });
        session.AddLayer("Content", Color.White);
        for (int i = 0; i < 20; i++)
        {
            session.DrawLine(i * 4, 0, 95 - (i * 4), 63, thickness: 2, color: Color.FromRgb((byte)(i * 12), 160, 220)).StartTime = i * 0.05;
        }
        DrawingShape ball = session.DrawCircle(10, 32, 6, filled: true, color: Color.FromRgb(255, 180, 60));
        session.Animations.Bind(ball, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 1, new PointF(70, 0), EasingKind.CubicInOut } });
        session.Animations.Bind(ball, AnimatedProperty.Opacity, new FloatTrack { { 0, 0.3f }, { 1, 1f } });
        var group = new DrawingGroup(Color.FromRgb(120, 255, 120));
        group.Add(new RectangleShape(40, 40, 16, 16, 1, null, isFilled: true));
        group.Add(new CircleShape(56, 48, 8, 1, null, isFilled: true));
        session.Animations.Bind(group, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 1, 180f } });
        session.Animations.Bind(group, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(52, 48) } });
        session.DrawShape(group);
        return session;
    }

    private static List<(int Frame, double Seconds, byte[] Rgba)> Collect(FrameSequenceRenderer renderer, int first, int last)
    {
        var frames = new List<(int, double, byte[])>();
        renderer.Render(first, last, (frame, seconds, rgba) => frames.Add((frame, seconds, rgba.ToArray())), TestContext.Current.CancellationToken);
        return frames;
    }

    [Fact]
    public void Render_delivers_each_frame_in_order_as_the_session_exports_it()
    {
        //Arrange
        var timeline = new AnimationTimeline(1, 24);
        var renderer = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame);
        using DrawingSession reference = CreateScene();

        //Act
        List<(int Frame, double Seconds, byte[] Rgba)> frames = Collect(renderer, 3, 9);

        //Assert
        frames.Count.Should().Be(7);
        for (int i = 0; i < frames.Count; i++)
        {
            frames[i].Frame.Should().Be(3 + i);
            frames[i].Seconds.Should().Be(timeline.TimeOfFrame(3 + i));
            reference.Time = frames[i].Seconds;
            frames[i].Rgba.Should().Equal(reference.ExportPixels(Frame));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    public void Parallel_rendering_is_identical_to_one_thread_and_stays_in_order(int threads)
    {
        //Arrange
        var timeline = new AnimationTimeline(1, 30);
        var single = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame);
        var parallel = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame) { MaxDegreeOfParallelism = threads };

        //Act
        List<(int Frame, double Seconds, byte[] Rgba)> expected = Collect(single, 0, timeline.FrameCount - 1);
        List<(int Frame, double Seconds, byte[] Rgba)> actual = Collect(parallel, 0, timeline.FrameCount - 1);

        //Assert
        actual.Count.Should().Be(expected.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            actual[i].Frame.Should().Be(i);
            actual[i].Rgba.Should().Equal(expected[i].Rgba);
        }
    }

    [Fact]
    public void Render_into_caller_buffers_fills_each_buffer_with_its_frame()
    {
        //Arrange
        var timeline = new AnimationTimeline(1, 10);
        var renderer = new FrameSequenceRenderer(timeline, () => CreateScene(transparent: true), Frame)
        {
            MaxDegreeOfParallelism = 3,
            IncludeBackground = false,
        };
        var buffers = new List<byte[]>();
        for (int i = 0; i < 5; i++) { buffers.Add(new byte[renderer.FrameBytes]); }
        using DrawingSession reference = CreateScene(transparent: true);

        //Act
        renderer.Render(4, buffers, TestContext.Current.CancellationToken);

        //Assert - over transparency: the alpha carries the frame
        for (int i = 0; i < buffers.Count; i++)
        {
            reference.Time = timeline.TimeOfFrame(4 + i);
            buffers[i].Should().Equal(reference.ExportPixels(Frame, includeBackground: false));
        }
        TestAssets.PixelAt(buffers[0], Frame.Width, 1, 32).A.Should().Be((byte)0);
    }

    [Fact]
    public void RenderTimeRange_renders_the_frames_that_start_inside_the_range()
    {
        //Arrange
        var timeline = new AnimationTimeline(2, 10);
        var renderer = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame);
        var seen = new List<int>();

        //Act
        int count = renderer.RenderTimeRange(0.25, 0.6, (frame, seconds, rgba) => seen.Add(frame), TestContext.Current.CancellationToken);
        int none = renderer.RenderTimeRange(5, 6, (frame, seconds, rgba) => seen.Add(frame), TestContext.Current.CancellationToken);

        //Assert
        count.Should().Be(3);
        none.Should().Be(0);
        seen.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void PrepareFrame_runs_after_the_time_is_set()
    {
        //Arrange
        var timeline = new AnimationTimeline(1, 4);
        var times = new List<double>();
        var renderer = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame)
        {
            PrepareFrame = (session, seconds) => times.Add(session.Time - seconds),
        };

        //Act
        Collect(renderer, 0, 3);

        //Assert
        times.Should().Equal(0.0, 0.0, 0.0, 0.0);
    }

    [Fact]
    public void Cancellation_stops_the_render()
    {
        //Arrange
        var timeline = new AnimationTimeline(10, 30);
        var renderer = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame) { MaxDegreeOfParallelism = 3 };
        using var source = new CancellationTokenSource();
        int delivered = 0;

        //Act
        Action act = () => renderer.Render(0, 299, (frame, seconds, rgba) =>
        {
            if (++delivered == 5) { source.Cancel(); }
        }, source.Token);

        //Assert
        act.Should().Throw<OperationCanceledException>();
        delivered.Should().Be(5);
    }

    [Fact]
    public void An_exception_on_a_worker_reaches_the_caller()
    {
        //Arrange
        var timeline = new AnimationTimeline(1, 30);
        var renderer = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame)
        {
            MaxDegreeOfParallelism = 4,
            PrepareFrame = (session, seconds) =>
            {
                if (seconds > 0.5) { throw new InvalidOperationException("scene failed"); }
            },
        };

        //Act
        Action act = () => Collect(renderer, 0, 29);

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("scene failed");
    }

    [Fact]
    public void Arguments_are_validated()
    {
        //Arrange
        var timeline = new AnimationTimeline(1);
        var renderer = new FrameSequenceRenderer(timeline, () => CreateScene(), Frame);
        Action noFactory = () => _ = new FrameSequenceRenderer(timeline, null, Frame);
        Action backwards = () => renderer.Render(5, 4, (f, s, r) => { }, TestContext.Current.CancellationToken);
        Action threads = () => renderer.MaxDegreeOfParallelism = 0;
        Action small = () => renderer.Render(0, new[] { new byte[10] }, TestContext.Current.CancellationToken);

        //Act + Assert
        noFactory.Should().Throw<ArgumentNullException>();
        backwards.Should().Throw<ArgumentOutOfRangeException>();
        threads.Should().Throw<ArgumentOutOfRangeException>();
        small.Should().Throw<ArgumentException>();
    }
}
