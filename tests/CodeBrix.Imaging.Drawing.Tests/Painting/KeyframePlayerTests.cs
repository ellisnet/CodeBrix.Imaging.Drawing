using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Painting;

public class KeyframePlayerTests
{
    private const int W = 4;
    private const int H = 2;

    //Keyframe k: red = 10 k, green = pixel index, blue = 7, alpha 255 (or 128 when asked)
    private static void Paint(int keyframe, Span<byte> rgba, byte alpha = 255)
    {
        for (int i = 0; i < W * H; i++)
        {
            rgba[i * 4] = (byte)(10 * keyframe);
            rgba[(i * 4) + 1] = (byte)i;
            rgba[(i * 4) + 2] = 7;
            rgba[(i * 4) + 3] = alpha;
        }
    }

    private static byte[] Keyframe(int k, byte alpha = 255)
    {
        var rgba = new byte[W * H * 4];
        Paint(k, rgba, alpha);
        return rgba;
    }

    private static byte[] Expected(int k, float fraction, byte alpha = 255)
    {
        var rgba = new byte[W * H * 4];
        DrawingTransition.Dissolve().Apply(Keyframe(k, alpha), Keyframe(k + 1, alpha), rgba, W, H, fraction);
        return rgba;
    }

    [Fact]
    public void RenderFrame_dissolves_between_the_two_nearest_keyframes()
    {
        //Arrange
        var times = new List<double>();
        using var player = new KeyframePlayer(W, H, 2.0, (k, seconds, rgba) =>
        {
            lock (times) { times.Add(seconds); }
            Paint(k, rgba);
        });
        var frame = new byte[W * H * 4];

        //Act
        player.RenderFrame(5.0, frame);

        //Assert - t 5 = keyframe 2 (time 4) to keyframe 3 (time 6) at u 0.5: red 25
        frame.Should().Equal(Expected(2, 0.5f));
        frame[0].Should().Be(25);
        player.KeyframeAt(5.0).Should().Be(2);
        lock (times) { times.Should().Contain(4.0).And.Contain(6.0); }
    }

    [Fact]
    public void RenderFrame_on_a_keyframe_is_that_keyframe_exactly()
    {
        //Arrange
        using var player = new KeyframePlayer(W, H, 0.5, (k, s, rgba) => Paint(k, rgba));
        var frame = new byte[(W * H * 4) + 3];
        frame[^1] = 99;

        //Act
        player.RenderFrame(1.5, frame);

        //Assert - extra bytes untouched
        frame.AsSpan(0, W * H * 4).ToArray().Should().Equal(Keyframe(3));
        frame[^1].Should().Be(99);
    }

    [Fact]
    public void RenderFrame_after_seeks_matches_painting_on_the_spot_and_keeps_the_cache_bounded()
    {
        //Arrange
        using var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) => Paint(k, rgba), null, 4);
        var frame = new byte[W * H * 4];
        double[] times = { 0.25, 0.75, 1.5, 9.25, 3.75, 0.0, 3.8, 2.1, 20.6, 1.99 };

        //Act and assert
        foreach (double t in times)
        {
            player.RenderFrame(t, frame);
            int k = (int)Math.Floor(t);
            float u = (float)(t - k);
            frame.Should().Equal(u == 0f ? Keyframe(k) : Expected(k, u));
            player.CachedKeyframeCount.Should().BeLessThanOrEqualTo(4);
        }
    }

    [Fact]
    public void RenderFrame_paints_ahead_and_reuses_cached_keyframes()
    {
        //Arrange
        using var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) => Paint(k, rgba));
        var frame = new byte[W * H * 4];

        //Act - steady playback through keyframes 0..3
        for (double t = 0; t < 3; t += 0.25) { player.RenderFrame(t, frame); }
        SpinWait.SpinUntil(() => player.PaintCount >= 5, 5000);

        //Assert - keyframes 0..4 painted once each (4 is the one painted ahead of 2..3)
        player.PaintCount.Should().Be(5);
    }

    [Fact]
    public void Paint_function_is_never_entered_twice_at_once()
    {
        //Arrange
        int inside = 0, most = 0;
        using var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) =>
        {
            int now = Interlocked.Increment(ref inside);
            InterlockedMax(ref most, now);
            Thread.Sleep(2);
            Paint(k, rgba);
            Interlocked.Decrement(ref inside);
        });

        //Act - several threads asking for different times at once
        Parallel.For(0, 24, i =>
        {
            var frame = new byte[W * H * 4];
            player.RenderFrame(i * 0.7, frame);
            int k = (int)Math.Floor(i * 0.7);
            float u = (float)((i * 0.7) - k);
            frame.Should().Equal(u == 0f ? Keyframe(k) : Expected(k, u));
        });

        //Assert
        most.Should().Be(1);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    [Fact]
    public void RenderFrame_rethrows_a_paint_failure_and_paints_afresh_next_time()
    {
        //Arrange
        int failures = 1;
        using var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) =>
        {
            if (k == 1 && Interlocked.Exchange(ref failures, 0) == 1) { throw new InvalidOperationException("brush broke"); }
            Paint(k, rgba);
        });
        var frame = new byte[W * H * 4];

        //Act
        Action first = () => player.RenderFrame(0.5, frame);

        //Assert
        first.Should().Throw<InvalidOperationException>().WithMessage("brush broke");
        player.RenderFrame(0.5, frame);
        frame.Should().Equal(Expected(0, 0.5f));
    }

    [Fact]
    public void Dispose_waits_for_the_painting_in_progress_and_refuses_further_use()
    {
        //Arrange
        using var started = new ManualResetEventSlim();
        bool finished = false;
        var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) =>
        {
            started.Set();
            Thread.Sleep(150);
            Paint(k, rgba);
            Volatile.Write(ref finished, true);
        });
        player.Prefetch(0.0);
        started.Wait(5000, TestContext.Current.CancellationToken).Should().BeTrue();

        bool disposedBefore = player.IsDisposed;

        //Act
        player.Dispose();

        //Assert
        disposedBefore.Should().BeFalse();
        player.IsDisposed.Should().BeTrue();
        Volatile.Read(ref finished).Should().BeTrue();
        Action render = () => player.RenderFrame(0, new byte[W * H * 4]);
        render.Should().Throw<ObjectDisposedException>();
        player.Dispose();
    }

    [Fact]
    public void RenderFrame_dissolves_transparent_keyframes_in_premultiplied_space()
    {
        //Arrange - half-transparent keyframes
        using var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) => Paint(k, rgba, 128));
        var frame = new byte[W * H * 4];

        //Act
        player.RenderFrame(0.25, frame);

        //Assert
        frame.Should().Equal(Expected(0, 0.25f, 128));
        frame[3].Should().Be(128);
    }

    [Fact]
    public void Constructor_and_RenderFrame_check_their_arguments()
    {
        //Arrange
        using var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) => Paint(k, rgba));

        //Act
        Action badInterval = () => new KeyframePlayer(W, H, 0, (k, s, rgba) => { });
        Action noPaint = () => new KeyframePlayer(W, H, 1, null);
        Action smallCache = () => new KeyframePlayer(W, H, 1, (k, s, rgba) => { }, null, 2);
        Action shortFrame = () => player.RenderFrame(0, new byte[3]);
        Action badTime = () => player.RenderFrame(double.NaN, new byte[W * H * 4]);

        //Assert
        badInterval.Should().Throw<ArgumentOutOfRangeException>();
        noPaint.Should().Throw<ArgumentNullException>();
        smallCache.Should().Throw<ArgumentOutOfRangeException>();
        shortFrame.Should().Throw<ArgumentException>();
        badTime.Should().Throw<ArgumentOutOfRangeException>();
        player.Transition.Kind.Should().Be(DrawingTransitionKind.Dissolve);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(77)]
    public void RenderFrame_of_a_large_frame_dissolves_in_parallel_to_the_same_bytes(int alpha)
    {
        //Arrange - big enough for the banded parallel dissolve; a fade through a colour too
        const int Width = 301, Height = 157;
        void PaintLarge(int k, Span<byte> rgba)
        {
            for (int i = 0; i < Width * Height; i++)
            {
                rgba[i * 4] = (byte)((i * 7) + (k * 50));
                rgba[(i * 4) + 1] = (byte)(i / 3);
                rgba[(i * 4) + 2] = (byte)(k * 90);
                rgba[(i * 4) + 3] = (byte)(k % 2 == 0 ? 255 : alpha);
            }
        }
        byte[] Large(int k)
        {
            var rgba = new byte[Width * Height * 4];
            PaintLarge(k, rgba);
            return rgba;
        }
        DrawingTransition fade = DrawingTransition.FadeThroughColor(Color.FromRgba(20, 30, 40, 128));
        using var dissolving = new KeyframePlayer(Width, Height, 1.0, (k, s, rgba) => PaintLarge(k, rgba));
        using var fading = new KeyframePlayer(Width, Height, 1.0, (k, s, rgba) => PaintLarge(k, rgba), fade);
        var frame = new byte[Width * Height * 4];
        var faded = new byte[Width * Height * 4];
        var expected = new byte[Width * Height * 4];
        var expectedFade = new byte[Width * Height * 4];
        DrawingTransition.Dissolve().Apply(Large(1), Large(2), expected, Width, Height, 0.375f);
        fade.Apply(Large(1), Large(2), expectedFade, Width, Height, 0.375f);

        //Act
        dissolving.RenderFrame(1.375, frame);
        fading.RenderFrame(1.375, faded);

        //Assert
        frame.Should().Equal(expected);
        faded.Should().Equal(expectedFade);
    }

    [Fact]
    public void A_wipe_transition_can_replace_the_dissolve()
    {
        //Arrange
        DrawingTransition wipe = DrawingTransition.Wipe(DrawingTransitionDirection.Right);
        using var player = new KeyframePlayer(W, H, 1.0, (k, s, rgba) => Paint(k, rgba), wipe);
        var frame = new byte[W * H * 4];
        var expected = new byte[W * H * 4];
        wipe.Apply(Keyframe(0), Keyframe(1), expected, W, H, 0.5f);

        //Act
        player.RenderFrame(0.5, frame);

        //Assert
        frame.Should().Equal(expected);
    }

    [Fact]
    public void PlaneKeyframePlayer_dissolves_float_keyframes()
    {
        //Arrange - open-ended values (lights above 1) survive the dissolve
        using var player = new PlaneKeyframePlayer(3, 2, 2.0, (k, s, image) => image.Fill(k * 0.75f, (float)s, 1f, 1f));
        var frame = new DrawingPlaneImage(3, 2);
        var a = new DrawingPlaneImage(3, 2);
        var b = new DrawingPlaneImage(3, 2);
        a.Fill(0.75f, 2f, 1f, 1f);
        b.Fill(1.5f, 4f, 1f, 1f);

        //Act
        player.RenderFrame(3.0, frame);
        DrawingPlaneImage expected = DrawingTransition.Dissolve().Apply(a, b, 0.5f);

        //Assert
        frame.GetPixel(2, 1).Should().Be(expected.GetPixel(2, 1));
        frame.GetPixel(0, 0).X.Should().Be(1.125f);
        player.KeyframeAt(3.0).Should().Be(1);
    }

    [Fact]
    public void PlaneKeyframePlayer_on_a_keyframe_copies_it_and_checks_the_destination_size()
    {
        //Arrange
        using var player = new PlaneKeyframePlayer(2, 2, 1.0, (k, s, image) => image.Fill(k, 0f, 0f, 0.5f));
        var frame = new DrawingPlaneImage(2, 2);

        //Act
        player.RenderFrame(4.0, frame);
        Action wrongSize = () => player.RenderFrame(4.0, new DrawingPlaneImage(3, 2));

        //Assert - transparency kept as painted
        frame.GetPixel(1, 1).Should().Be(new Vector4(4f, 0f, 0f, 0.5f));
        wrongSize.Should().Throw<ArgumentException>();
        player.IsDisposed.Should().BeFalse();
    }
}
