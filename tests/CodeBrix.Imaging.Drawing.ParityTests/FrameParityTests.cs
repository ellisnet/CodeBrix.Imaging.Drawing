using System;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for the animation-frame features. Everything COMPUTED in shared managed code -
/// easing values, color ramps, text layout and glyph-outline bounds, safe-area zones and
/// findings, particle data - must be bit-identical between the two backends. Everything
/// drawn through each backend's antialiased canvas (gradients, glyph fills, fades,
/// overlays, raw frames, plane-image and mask exports, particle dots) must match within the
/// existing image tolerance.
/// </summary>
public class FrameParityTests
{
    private static (IFrameAdapter Skia, IFrameAdapter NoSkia) CreateBoth(Size calibrationSize)
        => (new SkiaFrameAdapter(calibrationSize), new NoSkiaFrameAdapter(calibrationSize));

    private static void BuildBoth(IFrameAdapter skia, IFrameAdapter noSkia, Action<IFrameAdapter> build)
    {
        build(skia);
        build(noSkia);
    }

    private static byte[] RawToPng(byte[] rgba, Size size)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgba, size.Width, size.Height, PngFormat.Instance);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static void BuildGradientScene(IFrameAdapter session)
    {
        session.SetBackgroundFillColor(Color.FromRgb(7, 6, 16));
        session.AddLayer("Sky", Color.Black);
        session.DrawLinearGradientRectangle(0, 0, 800, 600, new PointF(0, 0), new PointF(0, 600),
            new[] { (0f, Color.FromRgb(44, 20, 58)), (0.45f, Color.FromRgb(26, 13, 42)), (1f, Color.FromRgb(7, 6, 16)) }, spread: 0);
        session.DrawRadialGradientCircle(400, 300, 220, new PointF(400, 300), 220,
            new[] { (0.05f, Color.FromRgba(255, 218, 160, 140)), (0.45f, Color.FromRgba(255, 218, 160, 35)), (1f, Color.FromRgba(255, 218, 160, 0)) });
        session.DrawLinearGradientRectangle(50, 450, 300, 100, new PointF(50, 0), new PointF(110, 0),
            new[] { (0f, Color.Red), (1f, Color.Blue) }, spread: 2);
    }

    [Fact]
    public void Easing_and_color_ramps_are_bit_identical()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(10, 10));
        using (skia)
        using (noSkia)
        {
            var colors = new[] { Color.FromRgb(246, 112, 168), Color.FromRgb(232, 146, 92), Color.FromRgb(156, 122, 248) };

            //Act + Assert
            for (int i = 0; i <= 64; i++)
            {
                double t = i / 64.0;
                skia.EvaluateAllEasings(t).Should().Equal(noSkia.EvaluateAllEasings(t));
                skia.EvaluateCubicBezier(0.25, 0.1, 0.25, 1, t).Should().Be(noSkia.EvaluateCubicBezier(0.25, 0.1, 0.25, 1, t));
                skia.RampAt(colors, t).Should().Be(noSkia.RampAt(colors, t));
            }
        }
    }

    [Fact]
    public void Text_layout_is_bit_identical()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(1920, 1080));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, session =>
            {
                session.RegisterFont(SvgTestAssets.TestFontPath);
                session.AddLayer("Text", Color.White);
            });

            //Act + Assert - the same widths and outline bounds, to the last bit
            foreach ((string text, int align, float tracking) in new[]
            {
                ("Venus Rosette", 0, 0.02f), ("EARTH YEARS", 1, 0.2f), ("2923", 2, 0f), ("Earth and Venus, joined", 1, 0f),
            })
            {
                (float Width, RectangleF? Bounds) a = skia.DrawText(text, 960, 500, 50, Color.White, align, tracking);
                (float Width, RectangleF? Bounds) b = noSkia.DrawText(text, 960, 500, 50, Color.White, align, tracking);
                a.Width.Should().Be(b.Width);
                a.Bounds.Should().Be(b.Bounds);
            }
        }
    }

    [Fact]
    public void Text_rendering_matches()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 300));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, session =>
            {
                session.RegisterFont(SvgTestAssets.TestFontPath);
                session.SetBackgroundFillColor(Color.FromRgb(20, 16, 40));
                session.AddLayer("Text", Color.White);
                session.DrawText("Venus Rosette", 40, 110, 80, Color.FromRgb(242, 228, 242), 0, 0.02f);
                session.DrawText("EARTH YEARS  VENUS YEARS", 400, 200, 28, Color.FromRgb(160, 140, 178), 1, 0.2f);
                session.DrawText("8.00", 760, 280, 60, Color.FromRgb(108, 198, 255), 2, 0f);
            });

            //Act
            byte[] skiaPng = skia.ExportPng(new Size(800, 300), true);
            byte[] noSkiaPng = noSkia.ExportPng(new Size(800, 300), true);

            //Assert
            ImageComparison.AssertSimilar(skiaPng, noSkiaPng, "text");
        }
    }

    [Fact]
    public void Gradient_fills_match()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 600));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, BuildGradientScene);

            //Act
            byte[] skiaPng = skia.ExportPng(new Size(400, 300), true);
            byte[] noSkiaPng = noSkia.ExportPng(new Size(400, 300), true);

            //Assert
            ImageComparison.AssertSimilar(skiaPng, noSkiaPng, "gradient fills");
        }
    }

    [Fact]
    public void Raw_frames_match_and_equal_each_backend_png()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 600));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, BuildGradientScene);
            var size = new Size(200, 150);

            //Act
            byte[] skiaRaw = skia.ExportPixels(size, false);
            byte[] noSkiaRaw = noSkia.ExportPixels(size, false);

            //Assert - each backend's raw frame is its own PNG, decoded; across backends near-identical
            RawToPng(skiaRaw, size).Length.Should().BeGreaterThan(0);
            ImageComparison.Compare(RawToPng(skiaRaw, size), skia.ExportPng(size, false)).MaxDelta.Should().Be(0);
            ImageComparison.Compare(RawToPng(noSkiaRaw, size), noSkia.ExportPng(size, false)).MaxDelta.Should().Be(0);
            ImageComparison.AssertSimilar(RawToPng(skiaRaw, size), RawToPng(noSkiaRaw, size), "raw frames over transparency");
        }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.6f)]
    [InlineData(0.15f)]
    public void Whole_frame_fade_matches(float opacity)
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 600));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, session =>
            {
                BuildGradientScene(session);
                session.AddLayer("Over", Color.White);
                session.DrawRectangle(300, 200, 200, 200, Color.FromRgb(240, 240, 255));
                session.SetLayerOpacity("Over", 0.7f);
                session.SetOpacity(opacity);
            });

            //Act + Assert - opaque background and over transparency
            ImageComparison.AssertSimilar(skia.ExportPng(new Size(400, 300), true), noSkia.ExportPng(new Size(400, 300), true),
                "fade over background");
            ImageComparison.AssertSimilar(skia.ExportPng(new Size(400, 300), false), noSkia.ExportPng(new Size(400, 300), false),
                "fade over transparency");
        }
    }

    [Fact]
    public void Safe_area_zones_and_findings_are_bit_identical()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(1080, 1920));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, session =>
            {
                session.RegisterFont(SvgTestAssets.TestFontPath);
                session.AddLayer("Titles", Color.White);
                session.MarkLayerForSafeArea("Titles");
                session.DrawText("Venus Rosette", 540, 1192, 60, Color.White, 1, 0.02f);
                session.DrawRectangle(160, 1299, 760, 2, Color.White);
                session.DrawText("2923", 818, 1448, 62, Color.White, 1, 0f);
            });

            //Act + Assert
            foreach (string preset in new[] { "InstagramReelsAd", "YouTubeShortsAd", "TikTok", "OrdinaryPortraitPost", "InstagramFeedPreview" })
            {
                skia.GetZones(preset, new Size(1080, 1920)).Should().Equal(noSkia.GetZones(preset, new Size(1080, 1920)));
                skia.CheckSafeArea(preset, new Size(1080, 1920)).Should().Equal(noSkia.CheckSafeArea(preset, new Size(1080, 1920)));
            }
            skia.CheckSafeArea("InstagramReelsAd", new Size(1080, 1920)).Count.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void Safe_area_overlay_matches()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(1080, 1920));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, session =>
            {
                session.SetBackgroundFillColor(Color.FromRgb(30, 30, 30));
                session.AddLayer("Art", Color.White);
                session.DrawRectangle(100, 100, 880, 1720, Color.FromRgb(90, 140, 200));
            });

            //Act + Assert
            ImageComparison.AssertSimilar(
                skia.ExportPngWithOverlay(new Size(270, 480), true, "InstagramReelsAd", Color.FromRgba(255, 48, 96, 110)),
                noSkia.ExportPngWithOverlay(new Size(270, 480), true, "InstagramReelsAd", Color.FromRgba(255, 48, 96, 110)),
                "overlay over a frame");
            ImageComparison.AssertSimilar(
                skia.ExportPngWithOverlay(new Size(270, 480), false, "TikTok", Color.FromRgba(0, 0, 0, 160)),
                noSkia.ExportPngWithOverlay(new Size(270, 480), false, "TikTok", Color.FromRgba(0, 0, 0, 160)),
                "overlay over transparency");
        }
    }

    //A mask as an opaque grey frame (value 0..1 -> grey level), so the image tolerance can compare it
    private static byte[] MaskToPng(float[] mask, Size size)
    {
        var rgba = new byte[mask.Length * 4];
        for (int i = 0; i < mask.Length; i++)
        {
            byte level = (byte)((Math.Clamp(mask[i], 0f, 1f) * 255f) + 0.5f);
            rgba[i * 4] = level;
            rgba[(i * 4) + 1] = level;
            rgba[(i * 4) + 2] = level;
            rgba[(i * 4) + 3] = 255;
        }
        return RawToPng(rgba, size);
    }

    private static void BuildMaskScene(IFrameAdapter session)
    {
        session.SetBackgroundFillColor(Color.FromRgb(240, 236, 220));
        session.AddLayer("Shapes", Color.Black);
        session.DrawRectangle(60, 60, 400, 300, Color.FromRgb(200, 40, 90));
        session.DrawRadialGradientCircle(560, 380, 180, new PointF(560, 380), 180,
            new[] { (0f, Color.FromRgba(30, 90, 220, 255)), (1f, Color.FromRgba(30, 90, 220, 0)) });
        session.AddLayer("Top", Color.Black);
        session.DrawRectangle(300, 250, 300, 250, Color.FromRgba(20, 160, 60, 128));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Plane_image_exports_equal_each_backends_raw_frame_and_match_across_backends(bool includeBackground)
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 600));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, BuildMaskScene);
            var size = new Size(200, 150);

            //Act
            byte[] skiaPlane = skia.ExportPlaneImageRgba(size, includeBackground);
            byte[] noSkiaPlane = noSkia.ExportPlaneImageRgba(size, includeBackground);

            //Assert - within a backend the float image holds exactly the raw frame (new or reused
            //  image); across backends the rasterized frames agree within the tolerance
            skiaPlane.Should().Equal(skia.ExportPixels(size, includeBackground));
            noSkiaPlane.Should().Equal(noSkia.ExportPixels(size, includeBackground));
            skia.ExportIntoPlaneImageRgba(size, includeBackground).Should().Equal(skiaPlane);
            noSkia.ExportIntoPlaneImageRgba(size, includeBackground).Should().Equal(noSkiaPlane);
            ImageComparison.AssertSimilar(RawToPng(skiaPlane, size), RawToPng(noSkiaPlane, size),
                includeBackground ? "plane image over background" : "plane image over transparency");
        }
    }

    [Fact]
    public void Masks_match_and_a_single_layer_mask_is_that_layers_alpha()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 600));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, session =>
            {
                session.SetBackgroundFillColor(Color.White);
                session.AddLayer("Only", Color.Black);
                session.DrawRectangle(60, 60, 400, 300, Color.FromRgba(200, 40, 90, 200));
                session.DrawRadialGradientCircle(560, 380, 180, new PointF(560, 380), 180,
                    new[] { (0f, Color.FromRgba(30, 90, 220, 255)), (1f, Color.FromRgba(30, 90, 220, 0)) });
            });
            var size = new Size(200, 150);

            //Act
            float[] skiaMask = skia.ExportMask(size, "Only");
            float[] noSkiaMask = noSkia.ExportMask(size, "Only");
            byte[] skiaFrame = skia.ExportPixels(size, false);
            byte[] noSkiaFrame = noSkia.ExportPixels(size, false);

            //Assert - in each backend the mask is exactly the layer's alpha / 255 (layer opacity
            //  is full and nothing is faded); across backends the coverage matches
            for (int i = 0; i < skiaMask.Length; i++)
            {
                skiaMask[i].Should().Be(skiaFrame[(i * 4) + 3] / 255f);
                noSkiaMask[i].Should().Be(noSkiaFrame[(i * 4) + 3] / 255f);
            }
            ImageComparison.AssertSimilar(MaskToPng(skiaMask, size), MaskToPng(noSkiaMask, size), "single-layer mask");
        }
    }

    [Fact]
    public void Whole_drawing_masks_match()
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 600));
        using (skia)
        using (noSkia)
        {
            BuildBoth(skia, noSkia, BuildMaskScene);
            var size = new Size(200, 150);

            //Act
            float[] skiaMask = skia.ExportMask(size, null);
            float[] noSkiaMask = noSkia.ExportMask(size, null);

            //Assert - fully inside the opaque rectangle both are exactly 1; untouched corner exactly 0
            skiaMask[(40 * 200) + 40].Should().Be(1f);
            noSkiaMask[(40 * 200) + 40].Should().Be(1f);
            skiaMask[(145 * 200) + 5].Should().Be(0f);
            noSkiaMask[(145 * 200) + 5].Should().Be(0f);
            ImageComparison.AssertSimilar(MaskToPng(skiaMask, size), MaskToPng(noSkiaMask, size), "whole-drawing mask");
        }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0f)]
    public void Particles_are_computed_identically_and_drawn_alike(float softness)
    {
        //Arrange
        (IFrameAdapter skia, IFrameAdapter noSkia) = CreateBoth(new Size(800, 600));
        using (skia)
        using (noSkia)
        {
            skia.AddLayer("Sparks", Color.Black);
            noSkia.AddLayer("Sparks", Color.Black);

            //Act
            (int Count, RectangleF? Bounds) skiaShape = skia.DrawParticleFountain(41, 400f, 420f, 1.7, softness);
            (int Count, RectangleF? Bounds) noSkiaShape = noSkia.DrawParticleFountain(41, 400f, 420f, 1.7, softness);

            //Assert - the particle data is shared managed arithmetic (exact); the dots are drawn
            //  through each canvas (tolerance), over transparency
            skiaShape.Count.Should().BeGreaterThan(10);
            noSkiaShape.Count.Should().Be(skiaShape.Count);
            noSkiaShape.Bounds.Should().Be(skiaShape.Bounds);
            ImageComparison.AssertSimilar(skia.ExportPng(new Size(400, 300), false), noSkia.ExportPng(new Size(400, 300), false),
                "particles over transparency");
        }
    }
}
