using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Rendering;

public class DebugOverlayTests
{
    private static readonly Size Frame = new Size(320, 180);

    private static DrawingSession CreateSession(bool withFont)
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = Frame,
            LayerOpacity = 255,
            BackgroundFillColor = Color.FromRgb(40, 90, 160),
        });
        if (withFont) { session.Fonts.RegisterFont(TestAssets.OpenSansPath); }
        session.AddLayer("Content", Color.White);
        session.DrawCircle(160, 90, 40, filled: true);
        return session;
    }

    private static AnimationTimeline CreateTimeline()
    {
        var timeline = new AnimationTimeline(10, 30);
        timeline.AddCue("intro", 0, 2);
        timeline.AddCue("title", 1, 5);
        return timeline;
    }

    private static int CountDifferences(byte[] a, byte[] b)
    {
        int count = 0;
        for (int i = 0; i < a.Length; i += 4)
        {
            if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) { count++; }
        }
        return count;
    }

    [Fact]
    public void GetLines_shows_timecode_frame_and_active_cues()
    {
        //Arrange
        var overlay = new DebugOverlay(null, CreateTimeline()) { ShowCues = true };

        //Act
        IReadOnlyList<string> lines = overlay.GetLines(1.5);

        //Assert
        lines.Should().Equal("00:00:01:15   1.500 s", "frame 45 / 300", "cues: intro, title");
    }

    [Fact]
    public void GetLines_without_a_timeline_shows_seconds_only()
    {
        //Arrange
        var overlay = new DebugOverlay(null) { ShowCues = true };

        //Act + Assert
        overlay.GetLines(2.25).Should().Equal("2.250 s");
    }

    [Fact]
    public void An_export_never_includes_the_session_overlay()
    {
        //Arrange
        using DrawingSession session = CreateSession(withFont: true);
        byte[] plain = session.ExportPixels(Frame);

        //Act
        session.DebugOverlay = new DebugOverlay(session.Fonts, CreateTimeline());
        session.Time = 1.5;
        byte[] exported = session.ExportPixels(Frame);

        //Assert
        exported.Should().Equal(plain);
    }

    [Fact]
    public void An_export_includes_the_overlay_when_asked()
    {
        //Arrange
        using DrawingSession session = CreateSession(withFont: true);
        var overlay = new DebugOverlay(session.Fonts, CreateTimeline()) { Corner = DebugOverlayCorner.BottomRight };
        byte[] plain = session.ExportPixels(Frame);
        var withOverlay = new byte[plain.Length];

        //Act
        session.ExportPixels(withOverlay, Frame, true, null, overlay);

        //Assert - the read-out changed pixels, all of them in the bottom-right quarter
        CountDifferences(plain, withOverlay).Should().BeGreaterThan(200);
        for (int y = 0; y < Frame.Height / 2; y++)
        {
            for (int x = 0; x < Frame.Width; x++)
            {
                TestAssets.PixelAt(withOverlay, Frame.Width, x, y).Should().Be(TestAssets.PixelAt(plain, Frame.Width, x, y));
            }
        }
    }

    [Fact]
    public void Without_a_registered_font_the_overlay_fails_soft()
    {
        //Arrange
        using DrawingSession session = CreateSession(withFont: false);
        var overlay = new DebugOverlay(session.Fonts, CreateTimeline());
        var missingFamily = new DebugOverlay(new FontRegistry(), CreateTimeline()) { FamilyName = "Nowhere" };
        byte[] plain = session.ExportPixels(Frame);
        var withOverlay = new byte[plain.Length];

        //Act
        session.ExportPixels(withOverlay, Frame, true, null, overlay);

        //Assert
        overlay.CanDrawText.Should().BeFalse();
        missingFamily.CanDrawText.Should().BeFalse();
        new DebugOverlay(null).CanDrawText.Should().BeFalse();
        withOverlay.Should().Equal(plain);
    }

    [Fact]
    public void The_live_render_shows_the_session_overlay()
    {
        //Arrange
        using DrawingSession session = CreateSession(withFont: true);
        var info = new SKImageInfo(Frame.Width, Frame.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, info);
        byte[] plain = bitmap.Bytes;

        //Act
        session.DebugOverlay = new DebugOverlay(session.Fonts, CreateTimeline());
        session.Render(canvas, info);
        byte[] shown = bitmap.Bytes;

        //Assert
        CountDifferences(plain, shown).Should().BeGreaterThan(200);
    }

    [Fact]
    public void The_overlay_draws_over_transparency()
    {
        //Arrange
        using DrawingSession session = CreateSession(withFont: true);
        var overlay = new DebugOverlay(session.Fonts) { Time = 3 };
        var frame = new byte[Frame.Width * Frame.Height * 4];

        //Act
        session.ExportPixels(frame, Frame, false, null, overlay);

        //Assert - the plate is translucent black in the top-left corner; the rest stays clear
        TestAssets.PixelAt(frame, Frame.Width, 8, 8).A.Should().BeInRange((byte)140, (byte)160);
        TestAssets.PixelAt(frame, Frame.Width, 300, 170).A.Should().Be((byte)0);
    }

    [Fact]
    public void TextSize_rejects_negative_values()
    {
        //Arrange
        var overlay = new DebugOverlay(null);
        Action act = () => overlay.TextSize = -1;

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
