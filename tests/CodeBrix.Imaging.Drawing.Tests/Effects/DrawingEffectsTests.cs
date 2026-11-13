using System;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
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

namespace CodeBrix.Imaging.Drawing.Tests.Effects;

public class DrawingEffectsTests
{
    private static readonly Size Frame = new Size(200, 200);

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255 });
        session.AddLayer("Effects", Color.FromRgb(250, 250, 250));
        return session;
    }

    [Fact]
    public void Factories_and_with_methods_build_equal_values()
    {
        //Act
        DrawingEffects shadow = DrawingEffects.DropShadow(new PointF(4, 6), 8, Color.Black);
        DrawingEffects combined = shadow.WithGlow(10, Color.Gold, 2f).WithBlurRadius(3);

        //Assert
        DrawingEffects.None.IsEmpty.Should().BeTrue();
        shadow.HasShadow.Should().BeTrue();
        shadow.HasGlow.Should().BeFalse();
        combined.HasBlur.Should().BeTrue();
        combined.HasGlow.Should().BeTrue();
        combined.GlowStrength.Should().Be(2f);
        combined.ShadowOffset.Should().Be(new PointF(4, 6));
        (combined == DrawingEffects.DropShadow(new PointF(4, 6), 8, Color.Black).WithGlow(10, Color.Gold, 2f).WithBlurRadius(3)).Should().BeTrue();
        (combined != shadow).Should().BeTrue();
        DrawingEffects.Glow(10, Color.Transparent).HasGlow.Should().BeFalse();
    }

    [Fact]
    public void Factories_reject_negative_or_non_finite_values()
    {
        //Act
        Action blur = () => DrawingEffects.Blur(-1);
        Action shadow = () => DrawingEffects.DropShadow(new PointF(Single.NaN, 0), 1, Color.Black);
        Action glow = () => DrawingEffects.Glow(Single.PositiveInfinity, Color.White);
        Action strength = () => DrawingEffects.Glow(1, Color.White, -2f);

        //Assert
        blur.Should().Throw<ArgumentOutOfRangeException>();
        shadow.Should().Throw<ArgumentOutOfRangeException>();
        glow.Should().Throw<ArgumentOutOfRangeException>();
        strength.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Drop_shadow_appears_at_the_offset_behind_the_element_over_transparency()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape square = session.DrawRectangle(40, 40, 60, 60, filled: true);
        square.Effects = DrawingEffects.DropShadow(new PointF(30, 30), 0, Color.FromRgba(0, 0, 0, 255));

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        square.IsDynamic.Should().BeTrue();
        TestAssets.PixelAt(pixels, 200, 70, 70).Should().Be(new Rgba32(250, 250, 250, 255));
        TestAssets.PixelAt(pixels, 200, 120, 120).Should().Be(new Rgba32(0, 0, 0, 255));
        TestAssets.PixelAt(pixels, 200, 20, 20).A.Should().Be(0);
    }

    [Fact]
    public void Soft_shadow_fades_out_and_the_opacity_fades_shadow_and_element_together()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape dot = session.DrawCircle(100, 100, 30, filled: true);
        dot.Effects = DrawingEffects.DropShadow(new PointF(0, 0), 20, Color.Black);

        //Act
        byte[] solid = session.ExportPixels(Frame, includeBackground: false);
        dot.Opacity = 0.5f;
        byte[] faded = session.ExportPixels(Frame, includeBackground: false);

        //Assert - the shadow reaches beyond the disc, fading with distance
        byte near = TestAssets.PixelAt(solid, 200, 100, 135).A;
        byte far = TestAssets.PixelAt(solid, 200, 100, 150).A;
        near.Should().BeGreaterThan(far);
        far.Should().BeGreaterThan(0);
        TestAssets.PixelAt(solid, 200, 100, 199).A.Should().Be(0);
        TestAssets.PixelAt(faded, 200, 100, 100).A.Should().BeInRange((byte)126, (byte)129);
    }

    [Fact]
    public void Glow_spreads_its_colour_around_the_element()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape dot = session.DrawCircle(100, 100, 20, filled: true, color: Color.White);
        dot.Effects = DrawingEffects.Glow(24, Color.FromRgb(255, 0, 0), 2f);

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        Rgba32 halo = TestAssets.PixelAt(pixels, 200, 100, 126);
        halo.R.Should().Be(255);
        halo.G.Should().Be(0);
        halo.A.Should().BeGreaterThan(40);
        TestAssets.PixelAt(pixels, 200, 100, 100).Should().Be(new Rgba32(255, 255, 255, 255));
    }

    [Fact]
    public void Blur_softens_the_element_edge()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape square = session.DrawRectangle(50, 50, 100, 100, filled: true);

        //Act
        byte[] sharp = session.ExportPixels(Frame, includeBackground: false);
        square.Effects = DrawingEffects.Blur(10);
        byte[] blurred = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.PixelAt(sharp, 200, 45, 100).A.Should().Be(0);
        TestAssets.PixelAt(blurred, 200, 45, 100).A.Should().BeGreaterThan(0);
        TestAssets.PixelAt(blurred, 200, 50, 100).A.Should().BeInRange((byte)110, (byte)145);
        TestAssets.PixelAt(blurred, 200, 100, 100).A.Should().Be(255);
    }

    [Fact]
    public void Layer_effects_apply_to_the_whole_layer_and_the_live_cache_matches_a_fresh_render()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingLayer layer = session.Layers[0];
        session.DrawRectangle(40, 40, 50, 50, filled: true);
        layer.Effects = DrawingEffects.DropShadow(new PointF(20, 20), 4, Color.Black);
        var info = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        //Act - render, add an element, render again
        session.Render(canvas, info);
        session.DrawRectangle(110, 110, 40, 40, filled: true);
        session.Render(canvas, info);
        byte[] exported = session.ExportPixels(Frame, includeBackground: false);

        using DrawingSession fresh = CreateSession();
        fresh.DrawRectangle(40, 40, 50, 50, filled: true);
        fresh.DrawRectangle(110, 110, 40, 40, filled: true);
        fresh.Layers[0].Effects = DrawingEffects.DropShadow(new PointF(20, 20), 4, Color.Black);
        using var freshBitmap = new SKBitmap(info);
        using var freshCanvas = new SKCanvas(freshBitmap);
        fresh.Render(freshCanvas, info);

        //Assert
        TestAssets.PixelAt(exported, 200, 160, 165).A.Should().BeGreaterThan(200);
        bitmap.Bytes.Should().Equal(freshBitmap.Bytes);
    }

    [Fact]
    public void Effect_parameters_animate_through_bindings()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape dot = session.DrawCircle(100, 100, 20, filled: true);
        DrawingLayer layer = session.Layers[0];
        session.Animations.Bind(dot, AnimatedProperty.ShadowOffset, new PointTrack { { 0, new PointF(0, 0) }, { 1, new PointF(10, 20) } });
        session.Animations.Bind(dot, AnimatedProperty.ShadowBlurRadius, new FloatTrack { { 0, 0f }, { 1, 6f } });
        session.Animations.Bind(dot, AnimatedProperty.ShadowColor, new ColorTrack { { 0, Color.Black } });
        session.Animations.Bind(dot, AnimatedProperty.BlurRadius, new FloatTrack { { 0, -5f }, { 1, 2f } });
        session.Animations.Bind(dot, AnimatedProperty.GlowRadius, new FloatTrack { { 0, 3f } });
        session.Animations.Bind(dot, AnimatedProperty.GlowColor, new ColorTrack { { 0, Color.Red } });
        session.Animations.Bind(layer, AnimatedProperty.GlowRadius, new FloatTrack { { 0, 9f } });

        //Act
        session.Time = 0;
        float blurAtStart = dot.Effects.BlurRadius;
        session.Time = 1;

        //Assert
        blurAtStart.Should().Be(0f);
        dot.Effects.ShadowOffset.Should().Be(new PointF(10, 20));
        dot.Effects.ShadowBlurRadius.Should().Be(6f);
        dot.Effects.ShadowColor.Should().Be(Color.Black);
        dot.Effects.BlurRadius.Should().Be(2f);
        dot.Effects.GlowRadius.Should().Be(3f);
        dot.Effects.GlowColor.Should().Be(Color.Red);
        layer.Effects.GlowRadius.Should().Be(9f);
    }

    private static readonly Color Ink = Color.FromRgb(0, 0, 255);
    private static readonly Color ShadowRed = Color.FromRgb(255, 0, 0);

    private static bool IsInk(Rgba32 p) => p.A > 200 && p.B > 200 && p.R < 60;

    private static bool IsShadow(Rgba32 p) => p.A > 200 && p.R > 200 && p.B < 60;

    //The box (inclusive pixel columns and rows) of the pixels that pass a test; (-1, -1, -1, -1) when none
    private static (int MinX, int MinY, int MaxX, int MaxY) Extent(byte[] rgba, int width, Func<Rgba32, bool> test)
    {
        int height = rgba.Length / 4 / width;
        int minX = -1, minY = -1, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!test(TestAssets.PixelAt(rgba, width, x, y))) { continue; }
                if (minX < 0 || x < minX) { minX = x; }
                if (x > maxX) { maxX = x; }
                if (minY < 0) { minY = y; }
                maxY = y;
            }
        }
        return (minX, minY, maxX, maxY);
    }

    private static DrawingShape HardShadowSquare(DrawingSession session, ShadowDirection direction = ShadowDirection.FixedInScene)
    {
        DrawingShape square = session.DrawRectangle(90, 90, 20, 20, filled: true, color: Ink);
        square.Effects = DrawingEffects.DropShadow(new PointF(10, 0), 0, ShadowRed, direction);
        return square;
    }

    [Fact]
    public void Shadow_direction_takes_part_in_equality_and_rejects_undefined_values()
    {
        //Act
        DrawingEffects shadow = DrawingEffects.DropShadow(new PointF(4, 6), 8, Color.Black);
        DrawingEffects turning = shadow.WithShadowDirection(ShadowDirection.RotatesWithElement);
        Action undefined = () => shadow.WithShadowDirection((ShadowDirection)7);

        //Assert
        shadow.ShadowDirection.Should().Be(ShadowDirection.FixedInScene);
        turning.ShadowDirection.Should().Be(ShadowDirection.RotatesWithElement);
        (turning == shadow).Should().BeFalse();
        (turning == DrawingEffects.DropShadow(new PointF(4, 6), 8, Color.Black, ShadowDirection.RotatesWithElement)).Should().BeTrue();
        turning.WithBlurRadius(2).ShadowDirection.Should().Be(ShadowDirection.RotatesWithElement);
        undefined.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Shadow_offset_grows_with_the_element_scale()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawingShape square = HardShadowSquare(session);
        square.Transform = new DrawingTransform(new PointF(0, 0), 0f, 2f, 2f, new PointF(100, 100));

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert - the 40-pixel square's shadow reaches 20 pixels (2 x 10) past it
        var ink = Extent(pixels, 200, IsInk);
        var shadow = Extent(pixels, 200, IsShadow);
        ink.MaxX.Should().BeInRange(118, 120);
        shadow.MaxX.Should().BeInRange(ink.MaxX + 19, ink.MaxX + 21);
    }

    [Fact]
    public void Shadow_offset_and_glow_grow_with_the_camera_zoom()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        HardShadowSquare(session);
        session.Camera = new DrawingCamera { Zoom = 2f };

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        var ink = Extent(pixels, 200, IsInk);
        var shadow = Extent(pixels, 200, IsShadow);
        ink.MaxX.Should().BeInRange(118, 120);
        shadow.MaxX.Should().BeInRange(ink.MaxX + 19, ink.MaxX + 21);
    }

    [Fact]
    public void A_scene_fixed_shadow_ignores_the_element_turning_and_a_turning_one_follows_it()
    {
        //Arrange
        using DrawingSession fixedSession = CreateSession();
        HardShadowSquare(fixedSession).Transform = DrawingTransform.Rotate(90, new PointF(100, 100));
        using DrawingSession turningSession = CreateSession();
        HardShadowSquare(turningSession, ShadowDirection.RotatesWithElement).Transform = DrawingTransform.Rotate(90, new PointF(100, 100));

        //Act
        byte[] fixedPixels = fixedSession.ExportPixels(Frame, includeBackground: false);
        byte[] turningPixels = turningSession.ExportPixels(Frame, includeBackground: false);

        //Assert - fixed: still 10 to the right; turning: 90 degrees clockwise, so 10 down
        var fixedInk = Extent(fixedPixels, 200, IsInk);
        var fixedShadow = Extent(fixedPixels, 200, IsShadow);
        fixedShadow.MaxX.Should().BeInRange(fixedInk.MaxX + 9, fixedInk.MaxX + 11);
        fixedShadow.MaxY.Should().Be(fixedInk.MaxY);
        var turningInk = Extent(turningPixels, 200, IsInk);
        var turningShadow = Extent(turningPixels, 200, IsShadow);
        turningShadow.MaxY.Should().BeInRange(turningInk.MaxY + 9, turningInk.MaxY + 11);
        turningShadow.MaxX.Should().Be(turningInk.MaxX);
    }

    [Fact]
    public void A_scene_fixed_shadow_turns_with_the_camera_roll()
    {
        //Arrange - the camera rolls 90 degrees, so the scene turns 90 degrees anticlockwise on screen
        using DrawingSession session = CreateSession();
        HardShadowSquare(session);
        session.Camera = new DrawingCamera { Rotation = 90f };

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert - "right" in the scene is up in the frame
        var ink = Extent(pixels, 200, IsInk);
        var shadow = Extent(pixels, 200, IsShadow);
        shadow.MinY.Should().BeInRange(ink.MinY - 11, ink.MinY - 9);
        shadow.MaxX.Should().Be(ink.MaxX);
    }

    [Fact]
    public void Glow_reach_grows_with_the_element_scale()
    {
        //Arrange
        using DrawingSession plain = CreateSession();
        plain.DrawRectangle(90, 90, 20, 20, filled: true, color: Ink).Effects = DrawingEffects.Glow(12, Color.FromRgb(0, 255, 0));
        using DrawingSession scaled = CreateSession();
        DrawingShape square = scaled.DrawRectangle(90, 90, 20, 20, filled: true, color: Ink);
        square.Effects = DrawingEffects.Glow(12, Color.FromRgb(0, 255, 0));
        square.Transform = DrawingTransform.Scale(2f, new PointF(100, 100));

        //Act
        byte[] plainPixels = plain.ExportPixels(Frame, includeBackground: false);
        byte[] scaledPixels = scaled.ExportPixels(Frame, includeBackground: false);

        //Assert - how far the glow reaches past the ink doubles
        int plainReach = Extent(plainPixels, 200, p => p.A > 8).MaxX - Extent(plainPixels, 200, IsInk).MaxX;
        int scaledReach = Extent(scaledPixels, 200, p => p.A > 8).MaxX - Extent(scaledPixels, 200, IsInk).MaxX;
        plainReach.Should().BeGreaterThan(4);
        ((double)scaledReach / plainReach).Should().BeInRange(1.7, 2.3);
    }

    [Fact]
    public void Layer_effects_follow_the_layer_scale()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(90, 90, 20, 20, filled: true, color: Ink);
        DrawingLayer layer = session.Layers[0];
        layer.Effects = DrawingEffects.DropShadow(new PointF(10, 0), 0, ShadowRed);
        layer.Transform = DrawingTransform.Scale(2f, new PointF(100, 100));

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        var ink = Extent(pixels, 200, IsInk);
        var shadow = Extent(pixels, 200, IsShadow);
        shadow.MaxX.Should().BeInRange(ink.MaxX + 19, ink.MaxX + 21);
    }

    [Fact]
    public void Effects_are_resolution_independent_over_transparency()
    {
        //Arrange - turned, scaled and zoomed content with every effect, drawn at 1x and 2x
        static byte[] Render(int size)
        {
            using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255 });
            session.AddLayer("Effects", Color.FromRgb(250, 250, 250));
            var group = new DrawingGroup();
            DrawingShape card = new RectangleShape(70, 80, 50, 34, color: Color.FromRgb(40, 120, 220), isFilled: true, cornerRadius: 6f);
            card.Effects = DrawingEffects.DropShadow(new PointF(6, 8), 5, Color.FromRgba(0, 0, 0, 200)).WithGlow(4, Color.Gold, 0.8f);
            DrawingShape dot = new CircleShape(120, 120, 12, color: Color.FromRgb(230, 60, 60), isFilled: true);
            dot.Effects = DrawingEffects.Blur(2).WithShadow(new PointF(-4, 5), 3, Color.FromRgba(20, 0, 60, 180), ShadowDirection.RotatesWithElement);
            group.Add(card);
            group.Add(dot);
            group.Transform = new DrawingTransform(new PointF(4, -6), 25f, 1.3f, 1.1f, new PointF(100, 100));
            session.DrawShape(group);
            session.Camera = new DrawingCamera { Zoom = 1.2f, Rotation = 12f };
            return session.ExportPixels(new Size(size, size), includeBackground: false);
        }

        //Act
        byte[] small = Render(200);
        byte[] large = Render(400);
        byte[] reduced = DrawingPlaneImage.FromRgba(large, 400, 400).Downsampled(2).ToRgba();

        //Assert - the same picture, only sharper: compared premultiplied, a small mean difference
        long total = 0;
        int noticeable = 0;
        for (int i = 0; i < small.Length; i += 4)
        {
            int worst = 0;
            for (int c = 0; c < 4; c++)
            {
                int a = c == 3 ? small[i + 3] : small[i + c] * small[i + 3] / 255;
                int b = c == 3 ? reduced[i + 3] : reduced[i + c] * reduced[i + 3] / 255;
                int delta = Math.Abs(a - b);
                total += delta;
                worst = Math.Max(worst, delta);
            }
            if (worst > 24) { noticeable++; }
        }
        double mean = total / (double)small.Length;
        TestAssets.CountInked(small).Should().BeGreaterThan(2000);
        mean.Should().BeLessThan(1.5);
        (noticeable / (small.Length / 4.0)).Should().BeLessThan(0.02);
    }
}
