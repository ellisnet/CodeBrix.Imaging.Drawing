using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class FrameCacheTests
{
    private static readonly Size Frame = new Size(64, 48);
    private static readonly AnimationTimeline Timeline = new AnimationTimeline(4, 30);

    //A complete, time-pure scene
    private static DrawingSession CreateScene()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(20, 20, 40) });
        session.AddLayer("Content", Color.White);
        DrawingShape ball = session.DrawCircle(8, 24, 6, filled: true, color: Color.Orange);
        session.Animations.Bind(ball, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 4, new PointF(48, 0), EasingKind.SineInOut } });
        for (int i = 0; i < 10; i++) { session.DrawLine(i * 6, 0, i * 6, 47, thickness: 1).StartTime = i * 0.3; }
        return session;
    }

    private static void WaitFor(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(30)) { throw new TimeoutException("The cache did not get there in time."); }
            Thread.Sleep(2);
        }
    }

    [Fact]
    public void Frames_ahead_of_the_play_head_are_exactly_the_sequence_renderer_s_frames()
    {
        //Arrange
        using var cache = new FrameCache(Timeline, CreateScene, Frame, capacity: 12, workerCount: 3);
        var expected = new List<byte[]>();
        new FrameSequenceRenderer(Timeline, CreateScene, Frame).Render(30, 41, (f, s, rgba) => expected.Add(rgba.ToArray()), TestContext.Current.CancellationToken);

        //Act
        cache.SetPlayHead(30);
        WaitFor(() => cache.Count == 12);

        //Assert
        var buffer = new byte[cache.FrameBytes];
        for (int f = 30; f <= 41; f++)
        {
            cache.TryGetFrame(f, buffer).Should().BeTrue();
            buffer.Should().Equal(expected[f - 30]);
        }
        cache.Contains(42).Should().BeFalse();
        cache.TryGetFrame(42, buffer).Should().BeFalse();
    }

    [Fact]
    public void Workers_keep_their_sessions_across_seeks()
    {
        //Arrange
        int built = 0;
        using var cache = new FrameCache(Timeline, () => { Interlocked.Increment(ref built); return CreateScene(); }, Frame, capacity: 4, workerCount: 2);

        //Act - several seeks, each waited out
        foreach (int head in new[] { 0, 60, 10, 100 })
        {
            cache.SetPlayHead(head);
            WaitFor(() => cache.Contains(head) && cache.Contains(head + 3));
        }

        //Assert - one scene per worker at most, never one per request
        built.Should().BeInRange(1, 2);
        cache.PlayHead.Should().Be(100);
    }

    [Fact]
    public void A_seek_drops_frames_that_left_the_window_but_keeps_a_few_behind()
    {
        //Arrange
        using var cache = new FrameCache(Timeline, CreateScene, Frame, capacity: 6) { KeepBehind = 2 };
        cache.SetPlayHead(10);
        WaitFor(() => cache.Count == 6);

        //Act
        cache.SetPlayHead(13);

        //Assert - 10 is three behind (dropped), 11 and 12 kept; never more than capacity + behind
        cache.Contains(10).Should().BeFalse();
        cache.Contains(11).Should().BeTrue();
        cache.Contains(12).Should().BeTrue();
        WaitFor(() => cache.Contains(18));
        cache.Count.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public void Only_the_frames_ShouldRender_accepts_are_rendered()
    {
        //Arrange - every third frame
        using var cache = new FrameCache(Timeline, CreateScene, Frame, capacity: 5) { ShouldRender = f => f % 3 == 0 };

        //Act
        cache.SetPlayHead(1);
        WaitFor(() => cache.Count == 5);

        //Assert - the first five wanted frames from the head
        foreach (int f in new[] { 3, 6, 9, 12, 15 }) { cache.Contains(f).Should().BeTrue(); }
        cache.Contains(4).Should().BeFalse();
    }

    [Fact]
    public async Task The_play_head_and_reads_may_come_from_other_threads_while_workers_render()
    {
        //Arrange
        using var cache = new FrameCache(Timeline, CreateScene, Frame, capacity: 8, workerCount: 3);
        var reference = new FrameSequenceRenderer(Timeline, CreateScene, Frame) { MaxDegreeOfParallelism = 2 };
        var expected = new byte[Timeline.FrameCount][];
        reference.Render(0, Timeline.FrameCount - 1, (f, s, rgba) => expected[f] = rgba.ToArray(), TestContext.Current.CancellationToken);
        int mismatches = 0;
        int hits = 0;

        //Act - one thread scrubs the play head while two read whatever is ready
        Task scrub = Task.Run(() =>
        {
            for (int i = 0; i < 300; i++) { cache.SetPlayHead((i * 7) % Timeline.FrameCount); Thread.Sleep(1); }
        }, TestContext.Current.CancellationToken);
        Task[] readers = new Task[2];
        for (int r = 0; r < readers.Length; r++)
        {
            readers[r] = Task.Run(() =>
            {
                var buffer = new byte[cache.FrameBytes];
                while (!scrub.IsCompleted)
                {
                    for (int f = 0; f < Timeline.FrameCount; f++)
                    {
                        if (!cache.TryGetFrame(f, buffer)) { continue; }
                        Interlocked.Increment(ref hits);
                        if (!buffer.AsSpan().SequenceEqual(expected[f])) { Interlocked.Increment(ref mismatches); }
                    }
                }
            }, TestContext.Current.CancellationToken);
        }
        await Task.WhenAll(readers);
        await scrub;

        //Assert
        cache.Failure.Should().BeNull();
        hits.Should().BeGreaterThan(0);
        mismatches.Should().Be(0);
    }

    [Fact]
    public void A_failing_scene_stops_the_cache_and_reports_the_exception()
    {
        //Arrange
        using var cache = new FrameCache(Timeline, () => throw new InvalidOperationException("no scene"), Frame);

        //Act
        cache.SetPlayHead(0);
        WaitFor(() => cache.Failure != null);

        //Assert
        cache.Failure.Should().BeOfType<InvalidOperationException>();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Dispose_waits_for_the_workers_and_disposes_their_sessions()
    {
        //Arrange
        var sessions = new List<DrawingSession>();
        var cache = new FrameCache(Timeline, () => { DrawingSession s = CreateScene(); lock (sessions) { sessions.Add(s); } return s; }, Frame, capacity: 3, workerCount: 2);
        cache.SetPlayHead(0);
        WaitFor(() => cache.Count == 3);

        //Act
        cache.Dispose();
        cache.Dispose();
        Action seek = () => cache.SetPlayHead(5);

        //Assert
        seek.Should().Throw<ObjectDisposedException>();
        sessions.Count.Should().BeGreaterThan(0);
        foreach (DrawingSession session in sessions) { session.IsDisposed.Should().BeTrue(); }
    }

    [Fact]
    public void Constructor_and_reads_validate_their_arguments()
    {
        //Act
        Action capacity = () => new FrameCache(Timeline, CreateScene, Frame, capacity: 0);
        Action workers = () => new FrameCache(Timeline, CreateScene, Frame, workerCount: 0);
        Action factory = () => new FrameCache(Timeline, null, Frame);
        using var cache = new FrameCache(Timeline, CreateScene, Frame);
        Action small = () => cache.TryGetFrame(0, new byte[10]);

        //Assert
        capacity.Should().Throw<ArgumentOutOfRangeException>();
        workers.Should().Throw<ArgumentOutOfRangeException>();
        factory.Should().Throw<ArgumentNullException>();
        small.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_sequence_renderer_that_keeps_sessions_calls_its_factory_once_per_worker()
    {
        //Arrange
        int built = 0;
        var renderer = new FrameSequenceRenderer(Timeline, () => { Interlocked.Increment(ref built); return CreateScene(); }, Frame)
        {
            MaxDegreeOfParallelism = 3,
            KeepSessions = true,
        };
        var fresh = new FrameSequenceRenderer(Timeline, CreateScene, Frame);
        var kept = new List<byte[]>();
        var expected = new List<byte[]>();

        //Act
        renderer.Render(0, 5, (f, s, rgba) => { }, TestContext.Current.CancellationToken);
        renderer.Render(40, 47, (f, s, rgba) => kept.Add(rgba.ToArray()), TestContext.Current.CancellationToken);
        renderer.Render(10, 20, (f, s, rgba) => { }, TestContext.Current.CancellationToken);
        int keptCount = renderer.KeptSessionCount;
        fresh.Render(40, 47, (f, s, rgba) => expected.Add(rgba.ToArray()), TestContext.Current.CancellationToken);
        renderer.ReleaseSessions();

        //Assert
        built.Should().Be(3);
        keptCount.Should().Be(3);
        renderer.KeptSessionCount.Should().Be(0);
        for (int i = 0; i < kept.Count; i++) { kept[i].Should().Equal(expected[i]); }
    }
}
