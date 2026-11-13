using System;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>DrawingLayer.IsVisible: a hidden layer is skipped everywhere, as if it were not in the session.</summary>
public class DrawingLayerVisibilityTests
{
    private static readonly Size Frame = new Size(100, 100);
    private static readonly SKImageInfo LiveInfo = new SKImageInfo(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul);

    //Two layers; the second (a big translucent disc) is the one hidden
    private static DrawingSession CreateSession(bool withSecond, bool transparent = false)
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = Frame,
            LayerOpacity = 255,
            BackgroundFillColor = transparent ? Color.Transparent : Color.Black,
        });
        session.AddLayer("Base", Color.Red);
        session.DrawRectangle(10, 10, 40, 30, filled: true);
        session.DrawLine(5, 90, 95, 60, thickness: 4);
        if (withSecond)
        {
            DrawingLayer second = session.AddLayer("Second", Color.FromRgba(40, 200, 255, 180));
            session.ActiveLayer = second;
            session.DrawCircle(60, 60, 30, filled: true);
            second.IsSafeAreaChecked = true;
        }
        return session;
    }

    private static byte[] RenderLive(DrawingSession session)
    {
        using var bitmap = new SKBitmap(LiveInfo);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, LiveInfo);
        return bitmap.Bytes;
    }

    [Fact]
    public void A_layer_is_visible_by_default()
    {
        //Assert
        new DrawingLayer("Layer", Color.White).IsVisible.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_hidden_layer_exports_exactly_like_a_session_without_it(bool transparent)
    {
        //Arrange
        using DrawingSession hidden = CreateSession(true, transparent);
        hidden.Layers[1].IsVisible = false;
        using DrawingSession without = CreateSession(false, transparent);

        //Assert
        hidden.ExportPixels(Frame).Should().Equal(without.ExportPixels(Frame));
        hidden.ExportPlaneImage(Frame).ToRgba().Should().Equal(without.ExportPlaneImage(Frame).ToRgba());
    }

    [Fact]
    public void A_hidden_layer_is_left_out_of_every_mask_meaning_and_the_ownership_map()
    {
        //Arrange
        using DrawingSession hidden = CreateSession(true, true);
        hidden.Layers[1].IsVisible = false;
        using DrawingSession without = CreateSession(false, true);

        //Assert
        foreach (MaskMeaning meaning in new[] { MaskMeaning.Coverage, MaskMeaning.VisibleAlpha, MaskMeaning.HardEdged })
        {
            hidden.ExportMask(Frame, meaning).Data.Should().Equal(without.ExportMask(Frame, meaning).Data);
        }
        hidden.ExportMask(hidden.Layers[1], Frame, MaskMeaning.Coverage).Data.Should().OnlyContain(v => v == 0f);
        DrawingOwnershipMap map = hidden.ExportOwnershipMap(Frame);
        map.ElementCount.Should().Be(2);
    }

    [Fact]
    public void A_hidden_layer_is_not_safe_area_checked()
    {
        //Arrange
        using DrawingSession session = CreateSession(true);
        var guide = new SafeAreaGuide("Edges", SafeAreaMargins.Uniform(0.2f));
        int shown = session.CheckSafeArea(guide).Count;

        //Act
        session.Layers[1].IsVisible = false;
        int hidden = session.CheckSafeArea(guide).Count;

        //Assert
        shown.Should().BeGreaterThan(0);
        hidden.Should().Be(0);
    }

    [Fact]
    public void Live_renders_follow_hiding_and_showing_and_match_fresh_renders()
    {
        //Arrange
        using DrawingSession session = CreateSession(true);
        RenderLive(session);

        //Act
        session.Layers[1].IsVisible = false;
        byte[] hiddenLive = RenderLive(session);
        session.Layers[1].IsVisible = true;
        byte[] shownLive = RenderLive(session);

        //Assert
        using DrawingSession without = CreateSession(false);
        using DrawingSession with = CreateSession(true);
        hiddenLive.Should().Equal(RenderLive(without));
        shownLive.Should().Equal(RenderLive(with));
    }

    [Fact]
    public void A_hidden_layer_costs_no_redraw_while_its_content_changes()
    {
        //Arrange - a moving element on the hidden layer
        using DrawingSession session = CreateSession(true);
        DrawingShape mover = session.DrawRectangle(0, 0, 10, 10, filled: true);
        session.Animations.Bind(mover, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 1, new PointF(80, 0) } });
        session.Layers[1].IsVisible = false;
        session.Time = 0;
        RenderLive(session);
        int redraws = session.Renderer.LayerFullRedrawCount;

        //Act
        for (int i = 1; i <= 10; i++)
        {
            session.Time = i / 10.0;
            RenderLive(session);
        }

        //Assert
        session.Renderer.LayerFullRedrawCount.Should().Be(redraws);
    }

    [Fact]
    public void A_bool_track_switches_a_layer_at_its_keys()
    {
        //Arrange
        using DrawingSession session = CreateSession(true);
        DrawingLayer second = session.Layers[1];
        session.Animations.Bind(second, AnimatedProperty.IsVisible, new BoolTrack { { 0, false }, { 1, true } });
        using DrawingSession without = CreateSession(false);
        using DrawingSession with = CreateSession(true);

        //Act
        session.Time = 0.5;
        byte[] before = session.ExportPixels(Frame);
        session.Time = 1;
        byte[] after = session.ExportPixels(Frame);

        //Assert
        second.IsVisible.Should().BeTrue();
        before.Should().Equal(without.ExportPixels(Frame));
        after.Should().Equal(with.ExportPixels(Frame));
    }

    [Fact]
    public void Visibility_binds_only_to_IsVisible()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var layer = new DrawingLayer("Layer", Color.White);

        //Act
        Action wrong = () => animations.Bind(layer, AnimatedProperty.Opacity, new BoolTrack { { 0, true } });

        //Assert
        wrong.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_hidden_layer_still_serves_as_a_matte()
    {
        //Arrange - content kept only where the hidden matte layer's disc is
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.Black });
        DrawingLayer content = session.AddLayer("Content", Color.Red);
        session.DrawRectangle(0, 0, 100, 100, filled: true);
        DrawingLayer matte = session.AddLayer("Matte", Color.White);
        session.ActiveLayer = matte;
        session.DrawCircle(50, 50, 20, filled: true);
        content.Mask = DrawingMask.FromLayer(matte);
        matte.IsVisible = false;

        //Act
        byte[] frame = session.ExportPixels(Frame);

        //Assert - red inside the disc, black (background) outside, no white disc drawn
        TestSupport.TestAssets.PixelAt(frame, 100, 50, 50).R.Should().Be(255);
        TestSupport.TestAssets.PixelAt(frame, 100, 50, 50).G.Should().Be(0);
        TestSupport.TestAssets.PixelAt(frame, 100, 5, 5).R.Should().Be(0);
    }
}
