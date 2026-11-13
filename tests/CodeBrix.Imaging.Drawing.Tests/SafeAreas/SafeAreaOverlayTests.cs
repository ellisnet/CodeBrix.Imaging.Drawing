using System;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.SafeAreas;

public class SafeAreaOverlayTests
{
    private static readonly Size Frame = new Size(108, 192);

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(1080, 1920),
            BackgroundFillColor = Color.White,
            SurfaceClearColor = Color.White,
        });
        session.AddLayer("Art", Color.Black);
        return session;
    }

    [Fact]
    public void Exports_never_include_the_session_overlay()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        byte[] before = session.ExportPixels(Frame);

        //Act
        session.SafeAreaOverlay = new SafeAreaOverlay(SafeAreaGuides.InstagramReelsAd);
        byte[] after = session.ExportPixels(Frame);
        byte[] png = session.ExportPng(Frame);

        //Assert
        after.Should().Equal(before);
        TestAssets.PixelAt(after, 108, 54, 5).Should().Be(new Rgba32(255, 255, 255, 255));
        png.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void An_explicit_overlay_tints_only_the_unsafe_zones()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var overlay = new SafeAreaOverlay(SafeAreaGuides.InstagramReelsAd) { MaskColor = Color.FromRgba(255, 0, 0, 128) };
        var pixels = new byte[108 * 192 * 4];

        //Act
        session.ExportPixels(pixels, Frame, true, overlay);

        //Assert - half red over white in the top band, untouched in the safe middle
        Rgba32 top = TestAssets.PixelAt(pixels, 108, 54, 5);
        top.R.Should().Be((byte)255);
        top.G.Should().BeInRange((byte)125, (byte)129);
        TestAssets.PixelAt(pixels, 108, 54, 90).Should().Be(new Rgba32(255, 255, 255, 255));
    }

    [Fact]
    public void Overlapping_zones_are_not_darker_than_one_zone()
    {
        //Arrange - the top-left corner lies in both the top and the left band
        using DrawingSession session = CreateSession();
        var overlay = new SafeAreaOverlay(SafeAreaGuides.InstagramReelsAd) { MaskColor = Color.FromRgba(0, 0, 255, 100) };

        //Act
        byte[] png = session.ExportPng(Frame, true, overlay);
        using Image<Rgba32> image = Image.Load<Rgba32>(png);

        //Assert
        image[1, 1].Should().Be(image[54, 5]);
        image[1, 1].Should().Be(image[1, 90]);
    }

    [Fact]
    public void An_overlay_over_transparency_carries_its_alpha()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(1080, 1920) });
        var overlay = new SafeAreaOverlay(SafeAreaGuides.TikTok) { MaskColor = Color.FromRgba(0, 255, 0, 90) };
        var pixels = new byte[108 * 192 * 4];

        //Act
        session.ExportPixels(pixels, Frame, false, overlay);

        //Assert - straight alpha: full green at the mask's alpha in a zone, nothing elsewhere
        Rgba32 inZone = TestAssets.PixelAt(pixels, 108, 54, 2);
        inZone.A.Should().BeInRange((byte)88, (byte)92);
        inZone.G.Should().BeGreaterThan((byte)250);
        TestAssets.PixelAt(pixels, 108, 40, 90).A.Should().Be((byte)0);
    }

    [Fact]
    public void The_live_render_shows_the_overlay_only_while_it_is_set()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var info = new SKImageInfo(108, 192, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        //Act
        session.Render(canvas, info);
        SKColor plain = bitmap.GetPixel(54, 5);
        session.SafeAreaOverlay = new SafeAreaOverlay(SafeAreaGuides.InstagramReelsAd) { MaskColor = Color.FromRgba(0, 0, 0, 255) };
        session.Render(canvas, info);
        SKColor masked = bitmap.GetPixel(54, 5);
        SKColor middle = bitmap.GetPixel(54, 90);
        session.SafeAreaOverlay = null;
        session.Render(canvas, info);
        SKColor cleared = bitmap.GetPixel(54, 5);

        //Assert
        plain.Should().Be(SKColors.White);
        masked.Should().Be(SKColors.Black);
        middle.Should().Be(SKColors.White);
        cleared.Should().Be(SKColors.White);
    }

    [Fact]
    public void GetZones_evaluates_time_rules_at_the_given_moment()
    {
        //Arrange
        var overlay = new SafeAreaOverlay(SafeAreaGuides.YouTubeEndScreen) { Duration = 60 };

        //Act
        int early = overlay.GetZones(new SizeF(1920, 1080), 10).Count;
        int late = overlay.GetZones(new SizeF(1920, 1080), 50).Count;

        //Assert
        early.Should().Be(0);
        late.Should().Be(1);
    }

    [Fact]
    public void An_unset_overlay_time_follows_the_session_time_and_a_set_one_overrides_it()
    {
        //Arrange - the end screen covers the whole frame during the final 20 s of a 60 s video
        using DrawingSession session = CreateSession();
        var overlay = new SafeAreaOverlay(SafeAreaGuides.YouTubeEndScreen) { Duration = 60, MaskColor = Color.FromRgba(255, 0, 0, 255) };

        var early = new byte[108 * 192 * 4];
        var late = new byte[108 * 192 * 4];
        var overridden = new byte[108 * 192 * 4];

        //Act
        session.Time = 10;
        session.ExportPixels(early, Frame, true, overlay);
        session.Time = 50;
        session.ExportPixels(late, Frame, true, overlay);
        overlay.Time = 10;
        session.ExportPixels(overridden, Frame, true, overlay);

        //Assert
        TestAssets.PixelAt(early, 108, 54, 90).Should().Be(new Rgba32(255, 255, 255, 255));
        TestAssets.PixelAt(late, 108, 54, 90).Should().Be(new Rgba32(255, 0, 0, 255));
        TestAssets.PixelAt(overridden, 108, 54, 90).Should().Be(new Rgba32(255, 255, 255, 255));
    }

    [Fact]
    public void Overlay_rejects_a_null_guide()
    {
        //Arrange
        Action create = () => _ = new SafeAreaOverlay(null);
        var overlay = new SafeAreaOverlay(SafeAreaGuides.TikTok);
        Action assign = () => overlay.Guide = null;

        //Act + Assert
        create.Should().Throw<ArgumentNullException>();
        assign.Should().Throw<ArgumentNullException>();
        overlay.MaskColor.Should().Be(SafeAreaOverlay.DefaultMaskColor);
    }

    private static readonly Size Square = new Size(200, 200);

    //A 200 x 200 frame with the broadcast title-safe guide: unsafe bands 10 pixels wide on every side
    private static byte[] ExportOverTransparency(SafeAreaOverlay overlay)
    {
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Square });
        var pixels = new byte[200 * 200 * 4];
        session.ExportPixels(pixels, Square, false, overlay);
        return pixels;
    }

    [Fact]
    public void The_defaults_are_a_light_tint_and_a_thin_outline()
    {
        //Act
        var overlay = new SafeAreaOverlay(SafeAreaGuides.BroadcastTitleSafe);

        //Assert
        overlay.MaskColor.Should().Be(SafeAreaOverlay.DefaultMaskColor);
        overlay.Opacity.Should().Be(1f);
        overlay.OutlineColor.Should().Be(SafeAreaOverlay.DefaultOutlineColor);
        overlay.OutlineWidth.Should().Be(SafeAreaOverlay.DefaultOutlineWidth);
        SafeAreaOverlay.DefaultMaskColor.ToRgba32().A.Should().BeLessThan((byte)80);
        SafeAreaOverlay.DefaultOutlineWidth.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Opacity_zero_draws_only_the_outline_just_outside_the_safe_area()
    {
        //Arrange
        var overlay = new SafeAreaOverlay(SafeAreaGuides.BroadcastTitleSafe)
        {
            Opacity = 0f,
            OutlineColor = Color.FromRgba(0, 200, 0, 220),
            OutlineWidth = 3f,
        };

        //Act
        byte[] pixels = ExportOverTransparency(overlay);

        //Assert - the line fills x 7..9 (outside the safe edge at 10); nothing else is drawn
        TestAssets.PixelAt(pixels, 200, 2, 100).A.Should().Be((byte)0);
        Rgba32 line = TestAssets.PixelAt(pixels, 200, 8, 100);
        line.A.Should().BeInRange((byte)219, (byte)221);
        line.G.Should().BeGreaterThan((byte)195);
        TestAssets.PixelAt(pixels, 200, 100, 8).A.Should().BeInRange((byte)219, (byte)221);
        TestAssets.PixelAt(pixels, 200, 8, 8).A.Should().BeInRange((byte)219, (byte)221);
        TestAssets.PixelAt(pixels, 200, 10, 100).A.Should().Be((byte)0);
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().Be((byte)0);
    }

    [Theory]
    [InlineData(1f, 200)]
    [InlineData(0.5f, 100)]
    [InlineData(0.25f, 50)]
    public void Opacity_scales_the_tint_alpha(float opacity, int expected)
    {
        //Arrange
        var overlay = new SafeAreaOverlay(SafeAreaGuides.BroadcastTitleSafe)
        {
            MaskColor = Color.FromRgba(0, 0, 255, 200),
            Opacity = opacity,
            OutlineWidth = 0f,
        };

        //Act
        byte[] pixels = ExportOverTransparency(overlay);

        //Assert - straight alpha round(200 x opacity), the colour unchanged
        Rgba32 zone = TestAssets.PixelAt(pixels, 200, 2, 100);
        ((int)zone.A).Should().BeInRange(expected - 1, expected + 1);
        zone.B.Should().BeGreaterThan((byte)250);
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().Be((byte)0);
    }

    [Fact]
    public void The_outline_can_be_turned_off_by_width_or_colour()
    {
        //Arrange
        var noWidth = new SafeAreaOverlay(SafeAreaGuides.BroadcastTitleSafe) { MaskColor = Color.FromRgba(255, 0, 0, 120), OutlineWidth = 0f };
        var noColor = new SafeAreaOverlay(SafeAreaGuides.BroadcastTitleSafe) { MaskColor = Color.FromRgba(255, 0, 0, 120), OutlineColor = Color.Transparent };

        //Act
        byte[] a = ExportOverTransparency(noWidth);
        byte[] b = ExportOverTransparency(noColor);

        //Assert - the band next to the safe edge looks like the rest of the zone
        TestAssets.PixelAt(a, 200, 9, 100).Should().Be(TestAssets.PixelAt(a, 200, 2, 100));
        b.Should().Equal(a);
    }

    [Fact]
    public void A_whole_frame_zone_is_tinted_without_an_outline()
    {
        //Arrange
        var overlay = new SafeAreaOverlay(SafeAreaGuides.YouTubeEndScreen) { MaskColor = Color.FromRgba(255, 0, 0, 120) };

        //Act
        byte[] pixels = ExportOverTransparency(overlay);

        //Assert
        TestAssets.PixelAt(pixels, 200, 100, 100).Should().Be(TestAssets.PixelAt(pixels, 200, 1, 1));
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().BeInRange((byte)119, (byte)121);
    }

    [Fact]
    public void Opacity_and_outline_width_reject_bad_values()
    {
        //Arrange
        var overlay = new SafeAreaOverlay(SafeAreaGuides.TikTok);

        //Act
        Action high = () => overlay.Opacity = 1.5f;
        Action nan = () => overlay.Opacity = Single.NaN;
        Action negative = () => overlay.OutlineWidth = -1f;
        Action infinite = () => overlay.OutlineWidth = Single.PositiveInfinity;

        //Assert
        high.Should().Throw<ArgumentOutOfRangeException>();
        nan.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
        infinite.Should().Throw<ArgumentOutOfRangeException>();
    }
}
