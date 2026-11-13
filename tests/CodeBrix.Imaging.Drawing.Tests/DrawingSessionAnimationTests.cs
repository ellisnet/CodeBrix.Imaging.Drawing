using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// The animation model drawn: element, group and layer transforms and opacities, time
/// windows, animated shapes and the camera - in exports and through the live render's caches.
/// The calibrated space is 100 x 100 and frames are exported at 100 x 100, so calibrated
/// units are pixels.
/// </summary>
public class DrawingSessionAnimationTests
{
    private static readonly Size Frame = new Size(100, 100);
    private static readonly SKImageInfo LiveInfo = new SKImageInfo(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
    private static readonly Rgba32 Black = new Rgba32(0, 0, 0, 255);
    private static readonly Rgba32 Red = new Rgba32(255, 0, 0, 255);

    private static DrawingSession CreateSession(Color? background = null)
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = Frame,
            LayerOpacity = 255,
            ActiveStrokeOpacity = 255,
            BackgroundFillColor = background ?? Color.Black,
        });
        session.AddLayer("Content", Color.Red);
        return session;
    }

    private static Rgba32 At(byte[] frame, int x, int y) => TestAssets.PixelAt(frame, Frame.Width, x, y);

    private static byte[] RenderLive(DrawingSession session)
    {
        using var bitmap = new SKBitmap(LiveInfo);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, LiveInfo);
        return bitmap.Bytes;
    }

    [Fact]
    public void Element_transform_moves_the_element()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape square = session.DrawRectangle(10, 10, 20, 20, filled: true);

        //Act
        square.Transform = DrawingTransform.Translate(50, 0);
        byte[] frame = session.ExportPixels(Frame);

        //Assert
        At(frame, 20, 20).Should().Be(Black);
        At(frame, 70, 20).Should().Be(Red);
    }

    [Fact]
    public void Element_transform_rotates_and_scales_about_the_anchor()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape bar = session.DrawRectangle(40, 45, 20, 10, filled: true);
        DrawingShape dot = session.DrawRectangle(45, 5, 10, 10, filled: true);

        //Act
        bar.Transform = DrawingTransform.Rotate(90, new PointF(50, 50));
        dot.Transform = DrawingTransform.Scale(2, new PointF(50, 10));
        byte[] frame = session.ExportPixels(Frame);

        //Assert - the bar now stands upright; the dot doubled about its centre
        At(frame, 50, 42).Should().Be(Red);
        At(frame, 42, 50).Should().Be(Black);
        At(frame, 41, 1).Should().Be(Red);
        At(frame, 38, 10).Should().Be(Black);
    }

    [Fact]
    public void Separate_faded_elements_overlap_but_a_faded_group_fades_as_one_unit()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.ActiveLayer.Color = Color.White;
        DrawingShape left = session.DrawRectangle(0, 0, 60, 40, filled: true);
        DrawingShape right = session.DrawRectangle(40, 0, 60, 40, filled: true);
        left.Opacity = 0.5f;
        right.Opacity = 0.5f;
        var group = new DrawingGroup();
        group.Add(new RectangleShape(0, 60, 60, 40, 1, null, isFilled: true));
        group.Add(new RectangleShape(40, 60, 60, 40, 1, null, isFilled: true));
        group.Opacity = 0.5f;
        session.DrawShape(group);

        //Act
        byte[] frame = session.ExportPixels(Frame);

        //Assert - elements: the overlap is brighter; group: the overlap matches the rest
        At(frame, 50, 20).R.Should().BeGreaterThan((byte)(At(frame, 10, 20).R + 40));
        At(frame, 10, 20).R.Should().BeInRange((byte)126, (byte)130);
        At(frame, 50, 80).Should().Be(At(frame, 10, 80));
        At(frame, 10, 80).R.Should().BeInRange((byte)126, (byte)130);
    }

    [Fact]
    public void A_faded_group_over_transparency_carries_its_fade_in_the_alpha()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.Transparent);
        var group = new DrawingGroup(Color.FromRgb(0, 0, 255));
        group.Add(new CircleShape(50, 50, 30, 1, null, isFilled: true));
        group.Add(new CircleShape(60, 50, 30, 1, null, isFilled: true));
        group.Opacity = 0.5f;
        session.DrawShape(group);

        //Act
        byte[] frame = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        Rgba32 overlap = At(frame, 55, 50);
        overlap.A.Should().BeInRange((byte)126, (byte)130);
        overlap.B.Should().Be((byte)255);
        At(frame, 25, 50).A.Should().Be(overlap.A);
        At(frame, 2, 2).A.Should().Be((byte)0);
    }

    [Fact]
    public void Group_transform_moves_its_children_together_and_children_keep_their_own()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var group = new DrawingGroup();
        group.Add(new RectangleShape(0, 0, 10, 10, 1, null, isFilled: true));
        RectangleShape child = group.Add(new RectangleShape(0, 0, 10, 10, 1, null, isFilled: true));
        child.Transform = DrawingTransform.Translate(20, 0);
        var inner = new DrawingGroup(Color.FromRgb(0, 255, 0));
        inner.Add(new RectangleShape(0, 20, 10, 10, 1, null, isFilled: true));
        group.Add(inner);
        group.Transform = DrawingTransform.Translate(40, 40);
        session.DrawShape(group);

        //Act
        byte[] frame = session.ExportPixels(Frame);

        //Assert
        At(frame, 45, 45).Should().Be(Red);
        At(frame, 65, 45).Should().Be(Red);
        At(frame, 45, 65).Should().Be(new Rgba32(0, 255, 0, 255));
        At(frame, 5, 5).Should().Be(Black);
        group.GetBounds().Should().Be(new RectangleF(0, 0, 30, 30));
        group.GetTransformedBounds().Should().Be(new RectangleF(40, 40, 30, 30));
    }

    [Fact]
    public void Layer_transform_moves_the_whole_layer()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(0, 0, 10, 10, filled: true);
        session.DrawLine(0, 50, 20, 50, thickness: 4);

        //Act
        session.ActiveLayer.Transform = DrawingTransform.Translate(50, 10);
        byte[] frame = session.ExportPixels(Frame);

        //Assert
        At(frame, 55, 15).Should().Be(Red);
        At(frame, 5, 5).Should().Be(Black);
        At(frame, 60, 60).Should().Be(Red);
    }

    [Fact]
    public void Time_windows_decide_which_elements_exist()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape square = session.DrawRectangle(40, 40, 20, 20, filled: true);
        square.StartTime = 1;
        square.EndTime = 2;

        //Act
        byte[] before = session.ExportPixels(Frame);
        session.Time = 1.5;
        byte[] during = session.ExportPixels(Frame);
        session.Time = 2;
        byte[] after = session.ExportPixels(Frame);

        //Assert
        At(before, 50, 50).Should().Be(Black);
        At(during, 50, 50).Should().Be(Red);
        At(after, 50, 50).Should().Be(Black);
        square.IsVisibleAt(1).Should().BeTrue();
        square.IsVisibleAt(2).Should().BeFalse();
    }

    [Fact]
    public void Time_keyed_elements_appearing_in_order_stay_incremental_in_the_live_render()
    {
        //Arrange - a hundred lines, each appearing a little later than the one before
        using DrawingSession session = CreateSession();
        for (int i = 0; i < 100; i++)
        {
            session.DrawLine(i, 0, 99 - i, 99, thickness: 1, color: Color.FromRgb((byte)(i * 2), 200, 255)).StartTime = i * 0.1;
        }
        session.Time = 0;
        RenderLive(session);
        int redrawsAfterFirstFrame = session.Renderer.LayerFullRedrawCount;

        //Act - play forward frame by frame
        for (int frame = 1; frame <= 60; frame++)
        {
            session.Time = frame / 10.0;
            RenderLive(session);
        }

        //Assert
        session.Renderer.LayerFullRedrawCount.Should().Be(redrawsAfterFirstFrame);
    }

    [Theory]
    [InlineData(new double[] { 0, 1, 2.5, 4, 6 })]
    [InlineData(new double[] { 6, 2, 3.05, 0.5, 9 })]
    public void The_live_render_after_seeking_matches_a_fresh_render(double[] times)
    {
        //Arrange
        static DrawingSession Build()
        {
            DrawingSession session = CreateSession();
            for (int i = 0; i < 60; i++)
            {
                DrawingShape line = session.DrawLine(i, 0, 59 - i, 99, thickness: 2, color: Color.FromRgb((byte)(i * 4), 120, 255));
                line.StartTime = i * 0.1;
                if (i % 7 == 0) { line.EndTime = (i * 0.1) + 1; }
            }
            DrawingShape mover = session.DrawCircle(10, 80, 8, filled: true, color: Color.White);
            session.Animations.Bind(mover, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 6, new PointF(70, -60) } });
            return session;
        }

        using DrawingSession played = Build();
        foreach (double time in times)
        {
            //Act
            played.Time = time;
            byte[] live = RenderLive(played);
            using DrawingSession fresh = Build();
            fresh.Time = time;
            byte[] expected = RenderLive(fresh);

            //Assert
            live.Should().Equal(expected);
        }
    }

    [Fact]
    public void Moving_an_element_redraws_only_its_own_layer()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(0, 0, 30, 30, filled: true);
        session.ActiveLayer = session.AddLayer("Moving", Color.White);
        DrawingShape mover = session.DrawCircle(50, 50, 5, filled: true);
        RenderLive(session);
        int redraws = session.Renderer.LayerFullRedrawCount;

        //Act
        mover.Transform = DrawingTransform.Translate(10, 0);
        byte[] live = RenderLive(session);

        //Assert
        session.Renderer.LayerFullRedrawCount.Should().Be(redraws + 1);
        TestAssets.PixelAt(live, 100, 60, 50).R.Should().Be((byte)255);
        TestAssets.PixelAt(live, 100, 60, 50).G.Should().Be((byte)255);
    }

    [Fact]
    public void Animated_shape_is_rebuilt_for_the_session_time()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var radius = new FloatTrack { { 0, 5f }, { 1, 40f } };
        var grow = new AnimatedShape(t => new CircleShape(50, 50, radius.Evaluate(t), 1, Color.White, isFilled: true));
        session.DrawShape(grow);

        //Act
        byte[] small = session.ExportPixels(Frame);
        session.Time = 1;
        byte[] large = session.ExportPixels(Frame);

        //Assert
        At(small, 50, 20).Should().Be(Black);
        At(large, 50, 20).Should().Be(new Rgba32(255, 255, 255, 255));
        grow.Time.Should().Be(1);
        grow.GetBounds().Should().Be(new RectangleF(10, 10, 80, 80));
    }

    [Fact]
    public void Animated_shapes_inside_groups_are_rebuilt_too()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var group = new DrawingGroup();
        AnimatedShape inner = group.Add(new AnimatedShape(t => new RectangleShape((float)(t * 10), 0, 5, 5, 1, null, isFilled: true)));
        session.DrawShape(group);

        //Act
        session.Time = 3;

        //Assert
        inner.Time.Should().Be(3);
        At(session.ExportPixels(Frame), 32, 2).Should().Be(Red);
    }

    [Fact]
    public void Camera_pans_zooms_and_respects_parallax()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(45, 45, 10, 10, filled: true);
        session.ActiveLayer = session.AddLayer("Hud", Color.White);
        session.DrawRectangle(0, 0, 10, 10, filled: true);
        session.ActiveLayer.Parallax = 0f;

        //Act
        session.Camera = new DrawingCamera { Pan = new PointF(30, 0) };
        byte[] panned = session.ExportPixels(Frame);
        session.Camera.Pan = new PointF(0, 0);
        session.Camera.Zoom = 3;
        byte[] zoomed = session.ExportPixels(Frame);

        //Assert - the scene slid left; the HUD square stayed put; zoom tripled the square
        At(panned, 20, 50).Should().Be(Red);
        At(panned, 50, 50).Should().Be(Black);
        At(panned, 5, 5).Should().Be(new Rgba32(255, 255, 255, 255));
        At(zoomed, 36, 50).Should().Be(Red);
        At(zoomed, 5, 5).Should().Be(new Rgba32(255, 255, 255, 255));
    }

    [Fact]
    public void Camera_over_transparency_keeps_the_background_clear()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.Transparent);
        session.DrawRectangle(40, 40, 20, 20, filled: true);
        session.Camera = new DrawingCamera { Rotation = 45 };

        //Act
        byte[] frame = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        At(frame, 50, 50).Should().Be(Red);
        At(frame, 50, 38).Should().Be(Red);
        At(frame, 41, 41).A.Should().Be((byte)0);
    }

    [Fact]
    public void Camera_changes_redraw_layers_in_the_live_render()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(45, 45, 10, 10, filled: true);
        session.Camera = new DrawingCamera();
        RenderLive(session);

        //Act
        session.Camera.Pan = new PointF(-30, 0);
        byte[] live = RenderLive(session);

        //Assert
        TestAssets.PixelAt(live, 100, 80, 50).R.Should().Be((byte)255);
        TestAssets.PixelAt(live, 100, 50, 50).R.Should().Be((byte)0);
    }

    [Fact]
    public void A_transformed_stroke_draws_like_the_same_stroke_moved()
    {
        //Arrange
        using DrawingSession moved = CreateSession();
        using DrawingSession shifted = CreateSession();
        var original = new Stroke(6f);
        original.AddPoint(10, 20);
        original.AddPoint(30, 40);
        original.AddPoint(50, 25);
        original.Transform = DrawingTransform.Translate(20, 30);
        moved.ActiveLayer.AddStroke(original);
        var reference = new Stroke(6f);
        reference.AddPoint(30, 50);
        reference.AddPoint(50, 70);
        reference.AddPoint(70, 55);
        shifted.ActiveLayer.AddStroke(reference);

        //Act
        byte[] a = moved.ExportPixels(Frame);
        byte[] b = shifted.ExportPixels(Frame);

        //Assert
        int differing = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 2) { differing++; }
        }
        differing.Should().BeLessThan(20);
        At(a, 50, 70).Should().Be(Red);
    }

    [Fact]
    public void Opacity_zero_draws_nothing_and_plain_elements_stay_plain()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape square = session.DrawRectangle(40, 40, 20, 20, filled: true);

        //Act
        square.Opacity = 0f;
        byte[] frame = session.ExportPixels(Frame);

        //Assert
        At(frame, 50, 50).Should().Be(Black);
        new CircleShape(0, 0, 1).Transform.Should().Be(DrawingTransform.Identity);
    }
}
