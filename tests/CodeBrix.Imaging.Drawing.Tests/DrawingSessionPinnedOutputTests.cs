using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using CodeBrix.Imaging.Formats.Png;
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
/// Pins the output of a session that uses none of the animation-model features (transforms,
/// groups, time keys, camera, animated properties) to reference images rendered BEFORE that
/// model existed. The references live in References/ beside this file, one pair per package,
/// rendered on Linux x64. The managed (NoSkia) rasterizer is the same code on every OS, so its
/// output must match its references byte for byte. A native rasterizer may anti-alias a few
/// edge pixels differently on another OS, so the SkiaSharp suite compares with its references
/// through the shared image tolerance, and pins the guarantee that matters exactly instead:
/// within one run, the cached live render equals a fresh render and merely constructing the
/// newer drawing objects changes no byte.
/// </summary>
public class DrawingSessionPinnedOutputTests
{
#if NOSKIA
    private const string PackageName = "noskia";
#else
    private const string PackageName = "skia";
#endif

    private static readonly Size CalibrationSize = new Size(640, 400);
    private static readonly Size ExportSize = new Size(480, 300);
    private static readonly SKImageInfo LiveInfo = new SKImageInfo(420, 300, SKColorType.Rgba8888, SKAlphaType.Premul);

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = CalibrationSize,
            BackgroundFillColor = Color.FromRgb(240, 236, 228),
            SurfaceClearColor = Color.FromRgb(20, 20, 30),
            StrokeWidth = 18f,
        });
        session.Fonts.RegisterFont(TestAssets.OpenSansPath);
        return session;
    }

    //The first half of the scene: translucent highlighter layers, strokes, every shape kind
    private static void DrawFirstHalf(DrawingSession session)
    {
        DrawingLayer pain = session.AddLayer("Pain", Color.FromRgb(255, 30, 230));
        session.PointerPressedNormalized(0.10f, 0.20f);
        session.PointerMovedNormalized(0.30f, 0.35f);
        session.PointerMovedNormalized(0.55f, 0.25f);
        session.PointerReleased();
        session.PointerPressedNormalized(0.15f, 0.30f);
        session.PointerMovedNormalized(0.50f, 0.32f);
        session.PointerReleased();
        session.PointerPressedNormalized(0.80f, 0.80f);
        session.PointerReleased();
        session.DrawCircle(420, 140, 60, thickness: 9);
        session.DrawEllipse(520, 300, 80, 40, filled: true, color: Color.FromRgba(30, 120, 220, 200));
        session.DrawRectangle(40, 260, 160, 100, thickness: 6, cornerRadius: 18);
        session.DrawArrow(250, 360, 380, 250, thickness: 7, color: Color.FromRgb(20, 140, 60));
        pain.Opacity = 0.8f;

        session.ActiveLayer = session.AddLayer("Numbness", Color.FromRgb(30, 128, 204));
        session.DrawLine(30, 30, 610, 370, thickness: 12);
        session.DrawPolyline(new List<(float X, float Y)> { (300, 40), (360, 120), (260, 120) }, closed: true, filled: true);
    }

    //The second half: gradient fills, text, a third layer and a whole-frame fade
    private static void DrawSecondHalf(DrawingSession session)
    {
        DrawingLayer fills = session.AddLayer("Fills", Color.White);
        session.ActiveLayer = fills;
        session.DrawShape(new RectangleShape(440, 20, 180, 90, new LinearGradientFill(new PointF(440, 20), new PointF(620, 110),
            new[] { new ColorStop(0f, Color.FromRgb(255, 200, 40)), new ColorStop(0.5f, Color.FromRgba(255, 60, 60, 160)), new ColorStop(1f, Color.FromRgb(90, 40, 200)) }),
            cornerRadius: 12));
        session.DrawShape(new CircleShape(110, 120, 70, new RadialGradientFill(new PointF(95, 105), 80, Color.FromRgb(255, 250, 220), Color.FromRgba(255, 140, 60, 0))));
        session.DrawText("Pinned output", 320, 385, 34, Color.FromRgb(40, 30, 60), TextAlign.Center, tracking: 0.04f);
        session.DrawShape(new TextShape("Grad", 470, 220, 48, session.Fonts, new LinearGradientFill(new PointF(470, 180), new PointF(470, 230), Color.Red, Color.Blue)));
        fills.Opacity = 0.9f;
        session.LayerOpacity = 160;
        session.Opacity = 0.85f;
    }

    [Fact]
    public void ExportPixels_of_a_scene_without_animation_features_matches_the_pinned_reference()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        DrawFirstHalf(session);
        DrawSecondHalf(session);

        //Act
        byte[] pixels = session.ExportPixels(ExportSize);

        //Assert
        AssertMatchesReference(pixels, ExportSize.Width, ExportSize.Height, "pinned-export-" + PackageName + ".png");
    }

    [Fact]
    public void Live_render_through_the_caches_matches_the_pinned_reference()
    {
        //Arrange - three renders, so the layer caches draw incrementally and the scene cache rebuilds
        using DrawingSession session = CreateSession();
        using var bitmap = new SKBitmap(LiveInfo);
        using var canvas = new SKCanvas(bitmap);
        DrawFirstHalf(session);
        session.Render(canvas, LiveInfo);
        DrawSecondHalf(session);
        session.Render(canvas, LiveInfo);
        session.ActiveLayer = session.GetLayer("Numbness");
        session.DrawCircle(320, 200, 30, filled: true, color: Color.FromRgb(250, 250, 250));
        session.PointerPressedNormalized(0.6f, 0.6f);
        session.PointerMovedNormalized(0.7f, 0.5f);

        //Act - the last render also carries an in-progress stroke
        session.Render(canvas, LiveInfo);
        byte[] pixels = bitmap.Bytes;

        //Assert
        AssertMatchesReference(pixels, LiveInfo.Width, LiveInfo.Height, "pinned-live-" + PackageName + ".png");
    }

    [Fact]
    public void Live_render_through_the_caches_equals_one_fresh_render_byte_for_byte()
    {
        //Arrange - the incremental path: three renders while the scene grows
        using DrawingSession cached = CreateSession();
        using var cachedBitmap = new SKBitmap(LiveInfo);
        using var cachedCanvas = new SKCanvas(cachedBitmap);
        DrawFirstHalf(cached);
        cached.Render(cachedCanvas, LiveInfo);
        DrawSecondHalf(cached);
        cached.Render(cachedCanvas, LiveInfo);
        cached.Render(cachedCanvas, LiveInfo);

        //  and the same finished scene rendered once by a new session
        using DrawingSession fresh = CreateSession();
        using var freshBitmap = new SKBitmap(LiveInfo);
        using var freshCanvas = new SKCanvas(freshBitmap);
        DrawFirstHalf(fresh);
        DrawSecondHalf(fresh);

        //Act
        fresh.Render(freshCanvas, LiveInfo);

        //Assert
        CountDifferences(cachedBitmap.Bytes, freshBitmap.Bytes).Should().Be(0, "the caches must not change a single byte");
    }

    [Fact]
    public void Constructing_the_newer_drawing_objects_changes_no_exported_or_live_byte()
    {
        //Arrange - the plain scene, exported and rendered live
        using DrawingSession plain = CreateSession();
        DrawFirstHalf(plain);
        DrawSecondHalf(plain);
        byte[] plainExport = plain.ExportPixels(ExportSize);
        using var plainBitmap = new SKBitmap(LiveInfo);
        using var plainCanvas = new SKCanvas(plainBitmap);
        plain.Render(plainCanvas, LiveInfo);

        //  the same scene with animation-model objects created around it but never applied
        using DrawingSession touched = CreateSession();
        var timeline = new AnimationTimeline(4, 30);
        timeline.AddCue("unused", 0, 1);
        var transform = new DrawingTransform(new PointF(10, 20), 30f, 2f, 2f, new PointF(5, 5));
        var group = new DrawingGroup(Color.Red);
        group.Add(new CircleShape(10, 10, 5)).Transform = transform;
        var camera = new DrawingCamera { Pan = new PointF(40, 0), Zoom = 2f };
        var track = new FloatTrack { { 0, 0f }, { 1, 1f } };
        DrawFirstHalf(touched);
        DrawSecondHalf(touched);
        touched.Time = 0;
        touched.Camera = null;

        //Act
        byte[] touchedExport = touched.ExportPixels(ExportSize);
        using var touchedBitmap = new SKBitmap(LiveInfo);
        using var touchedCanvas = new SKCanvas(touchedBitmap);
        touched.Render(touchedCanvas, LiveInfo);

        //Assert
        track.Evaluate(0.5).Should().Be(0.5f);
        camera.IsIdentity.Should().BeFalse();
        CountDifferences(plainExport, touchedExport).Should().Be(0);
        CountDifferences(plainBitmap.Bytes, touchedBitmap.Bytes).Should().Be(0);
    }

    private static int CountDifferences(byte[] expected, byte[] actual)
    {
        actual.Length.Should().Be(expected.Length);
        int differing = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i]) { differing++; }
        }
        return differing;
    }

    private static void AssertMatchesReference(byte[] pixels, int width, int height, string fileName)
    {
        string path = Path.Combine(GetReferencesDirectory(), fileName);
        if (!File.Exists(path) && Environment.GetEnvironmentVariable("CREATE_PINNED_REFERENCES") == "1")
        {
            using Image<Rgba32> created = Image.LoadPixelData<Rgba32>(pixels, width, height, PngFormat.Instance);
            created.SaveAsPng(path);
        }

        File.Exists(path).Should().BeTrue("the pinned reference " + fileName + " must be committed");
        using Image<Rgba32> reference = Image.Load<Rgba32>(path);
        var expected = new byte[width * height * 4];
        reference.Width.Should().Be(width);
        reference.Height.Should().Be(height);
        reference.CopyPixelDataTo(expected);

#if NOSKIA
        //The managed rasterizer is the same code everywhere: exact
        CountDifferences(expected, pixels).Should().Be(0, "the output must be byte-identical to " + fileName);
#else
        //A native rasterizer: the shared tolerance (the exact guarantees are the tests above)
        ImageComparison.AssertSimilar(EncodePng(expected, width, height), EncodePng(pixels, width, height),
            "the output compared with " + fileName);
#endif
    }

#if !NOSKIA
    private static byte[] EncodePng(byte[] pixels, int width, int height)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels, width, height, PngFormat.Instance);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
#endif

    private static string GetReferencesDirectory([CallerFilePath] string thisFilePath = null)
        => Path.Combine(new FileInfo(thisFilePath ?? String.Empty).DirectoryName ?? String.Empty, "References");
}
