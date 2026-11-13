extern alias noskia;
extern alias skia;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using SilverAssertions;
using Xunit;
using NM = noskia::CodeBrix.Imaging.Drawing.Models;
using NMeaning = noskia::CodeBrix.Imaging.Drawing.MaskMeaning;
using NP = noskia::CodeBrix.Imaging.Drawing.Paths;
using NPa = noskia::CodeBrix.Imaging.Drawing.Painting;
using NPl = noskia::CodeBrix.Imaging.Drawing.Planes;
using NS = noskia::CodeBrix.Imaging.Drawing.Shapes;
using NSession = noskia::CodeBrix.Imaging.Drawing.DrawingSession;
using NSessionOptions = noskia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using SM = skia::CodeBrix.Imaging.Drawing.Models;
using SMeaning = skia::CodeBrix.Imaging.Drawing.MaskMeaning;
using SP = skia::CodeBrix.Imaging.Drawing.Paths;
using SPa = skia::CodeBrix.Imaging.Drawing.Painting;
using SPl = skia::CodeBrix.Imaging.Drawing.Planes;
using SS = skia::CodeBrix.Imaging.Drawing.Shapes;
using SSession = skia::CodeBrix.Imaging.Drawing.DrawingSession;
using SSessionOptions = skia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for the painterly tools: hard-edged masks, ownership maps (with the nearest fill,
/// seams, colour pickup, colours applied back and brush textures), the brushstroke outline and
/// keyframe playback. All of it is COMPUTED in shared managed code under one written-down pixel
/// rule - no rasterizer is involved - so the two assemblies must agree EXACTLY: every result is
/// reduced to a SHA-256 of its raw bits (or exact text) and compared for equality. The scene
/// covers every route into the rule: filled and outlined shapes, an open polyline, a turned
/// rounded rectangle, an even-odd path, glyph outlines, a freehand stroke, brushstrokes, a
/// transformed group, a camera with layer parallax, a letterboxed non-integral scale, and both
/// an opaque and a transparent background.
/// </summary>
public class PaintingParityTests
{
    private static readonly Size Calibration = new Size(160, 120);
    private static readonly Size Output = new Size(203, 137);
    private static readonly string RobotoPath = Path.Combine(SvgTestAssets.AssetsDirectory, "Fonts", "Roboto-Regular.ttf");
    private static readonly double[] KeyframeTimes = { 0.0, 0.5, 1.25, 3.999, 7.0, 2.2, 0.75, 11.1 };

    [Fact]
    public void Painting_tools_compute_identical_results_in_both_packages()
    {
        //Act
        List<string> skia = SkiaResults();
        List<string> noSkia = NoSkiaResults();

        //Assert
        noSkia.Count.Should().Be(skia.Count);
        for (int i = 0; i < skia.Count; i++) { noSkia[i].Should().Be(skia[i], "result " + i + " must be identical"); }
    }

    [Fact]
    public void The_parity_scene_exercises_the_rule()
    {
        //Arrange
        using SSession session = SkiaScene(true);

        //Act
        SPl.DrawingPlane mask = session.ExportMask(Output, SMeaning.HardEdged);
        SPa.DrawingOwnershipMap map = session.ExportOwnershipMap(Output);

        //Assert - plenty covered and plenty bare, many owners, unowned pixels to fill
        float covered = mask.Data.Sum();
        covered.Should().BeGreaterThan(Output.Width * Output.Height * 0.2f);
        covered.Should().BeLessThan(Output.Width * Output.Height * 0.95f);
        map.ElementCount.Should().Be(78);
        map.UnownedCount.Should().BeGreaterThan(0);
        Enumerable.Range(0, map.ElementCount).Count(i => map.GetPixelCount(i) > 0).Should().BeGreaterThan(60);
    }

    private static void KeyframePixels(int keyframe, Span<byte> rgba)
    {
        for (int i = 0; i < rgba.Length / 4; i++)
        {
            rgba[i * 4] = (byte)(keyframe * 37);
            rgba[(i * 4) + 1] = (byte)(i * 5);
            rgba[(i * 4) + 2] = (byte)(255 - (keyframe * 11));
            rgba[(i * 4) + 3] = (byte)(keyframe % 2 == 0 ? 255 : 90);
        }
    }

    private static string Hash<T>(T[] values) where T : struct
        => Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(values.AsSpan())));

    //---------------------------------------------------------------------------------------------
    //SKIA

    private static SSession SkiaScene(bool opaqueBackground)
    {
        var session = new SSession(new SSessionOptions
        {
            CalibrationSize = Calibration,
            BackgroundFillColor = opaqueBackground ? Color.FromRgb(240, 230, 200) : Color.Transparent,
            LayerOpacity = 200,
        });
        session.Fonts.RegisterFont(RobotoPath);
        SM.DrawingLayer under = session.AddLayer("Under", Color.FromRgb(40, 90, 200));
        session.DrawCircle(50, 40, 27.3f, filled: true);
        session.DrawEllipse(120, 60, 30, 18, thickness: 5.5f);
        session.DrawPolyline(new[] { (5f, 110f), (40f, 70f), (70f, 112f), (95f, 80f) }, thickness: 4f);
        session.DrawShape(new SS.RectangleShape(90, 10, 40, 22, isFilled: true, cornerRadius: 6) { Transform = SM.DrawingTransform.Rotate(23, new PointF(110, 21)) });
        session.DrawPath(SP.PathGeometry.Combine(SP.PathGeometry.Circle(new PointF(130, 95), 20), SP.PathGeometry.Circle(new PointF(134, 95), 9))
            .WithFillRule(SP.PathFillRule.EvenOdd), filled: true);
        session.DrawText("Inanna", 20, 100, 26, Color.White);
        var stroke = new SM.Stroke(6f);
        foreach ((int x, int y) in new[] { (10, 10), (30, 18), (44, 9), (61, 25) }) { stroke.AddPoint(x, y); }
        under.AddStroke(stroke);
        SM.DrawingLayer over = session.AddLayer("Over", Color.FromRgb(230, 150, 40));
        over.Parallax = 0.6f;
        session.ActiveLayer = over;
        var random = new SPl.DrawingRandom(11);
        for (int i = 0; i < 70; i++)
        {
            session.DrawBrushStroke((float)random.Uniform(0, 160), (float)random.Uniform(0, 120), (float)random.Uniform(12, 40),
                (float)random.Uniform(4, 11), (float)random.Uniform(0, 360), (float)random.Normal(0, 0.12));
        }
        var group = new SS.DrawingGroup { Transform = new SM.DrawingTransform(new PointF(4, -3), 12, 1.2f, 0.9f, new PointF(80, 60)) };
        group.Add(new SS.BrushStrokeShape(70, 55, 50, 14, 17f, -0.1f, 2.5f));
        group.Add(new SS.LineShape(60, 80, 110, 70, 3f));
        session.DrawShape(group);
        session.Camera = new SM.DrawingCamera { Pan = new PointF(6, -4), Zoom = 1.15f, Rotation = 4f };
        return session;
    }

    private static SPl.DrawingPlaneImage SkiaSource()
    {
        var image = new SPl.DrawingPlaneImage(Output.Width / 2, Output.Height / 2);
        image.Fill((x, y) => new Vector4(x / 100f, y / 75f, ((x * 7) + (y * 3)) % 11 / 10f, ((x + y) % 5) / 4f));
        return image;
    }

    private static List<string> SkiaResults()
    {
        var results = new List<string>();
        foreach (bool opaque in new[] { true, false })
        {
            using SSession session = SkiaScene(opaque);
            results.Add(Hash(session.ExportMask(Output, SMeaning.HardEdged).Data));
            results.Add(Hash(session.ExportMask(session.Layers[1], Output, SMeaning.HardEdged).Data));
            SPa.DrawingOwnershipMap map = session.ExportOwnershipMap(Output);
            results.Add(Hash(map.Owners.ToArray()));
            var stats = new List<string>();
            for (int i = 0; i < map.ElementCount; i++) { stats.Add(map.GetPixelCount(i) + "@" + map.GetBounds(i)); }
            results.Add(string.Join(";", stats));
            SPa.DrawingOwnershipMap filled = map.FillUnowned();
            results.Add(Hash(filled.Owners.ToArray()));
            results.Add(Hash(filled.CreateBoundaryPlane(1.3f).Data));
            Vector4[] means = filled.GetMeanColors(SkiaSource());
            results.Add(Hash(means));
            results.Add(Hash(filled.GetMeanColors(SkiaSource().ToRgba(), Output.Width / 2, Output.Height / 2)));
            var painted = new SPl.DrawingPlaneImage(Output);
            filled.ApplyColors(painted, means);
            results.Add(Hash(painted.ToRgba()));
            var texture = new SPl.DrawingPlane(Output);
            filled.FillByElement(texture, p => (float)((Math.Sin(p.Across * 0.9) * 0.1) + (p.NormalizedAlong * 0.05) + p.ElementIndex));
            results.Add(Hash(texture.Data));
        }
        results.Add(string.Join(" ", new SS.BrushStrokeShape(12.5f, 7.25f, 33f, 9f, 120f, 0.13f, 3.3f).GetPoints()));
        results.Add(Hash(SkiaKeyframes()));
        return results;
    }

    private static byte[] SkiaKeyframes()
    {
        var frames = new List<byte>();
        using var player = new SPa.KeyframePlayer(8, 6, 2.0, (k, s, rgba) => KeyframePixels(k, rgba));
        var frame = new byte[8 * 6 * 4];
        foreach (double t in KeyframeTimes)
        {
            player.RenderFrame(t, frame);
            frames.AddRange(frame);
        }
        using var planes = new SPa.PlaneKeyframePlayer(8, 6, 2.0, (k, s, image) => image.Fill((x, y) => new Vector4(k * 0.3f, x / 8f, y / 6f, 0.25f + (k % 3 * 0.25f))));
        var image = new SPl.DrawingPlaneImage(8, 6);
        foreach (double t in KeyframeTimes)
        {
            planes.RenderFrame(t, image);
            frames.AddRange(image.ToRgba());
        }
        return frames.ToArray();
    }

    //---------------------------------------------------------------------------------------------
    //NOSKIA

    private static NSession NoSkiaScene(bool opaqueBackground)
    {
        var session = new NSession(new NSessionOptions
        {
            CalibrationSize = Calibration,
            BackgroundFillColor = opaqueBackground ? Color.FromRgb(240, 230, 200) : Color.Transparent,
            LayerOpacity = 200,
        });
        session.Fonts.RegisterFont(RobotoPath);
        NM.DrawingLayer under = session.AddLayer("Under", Color.FromRgb(40, 90, 200));
        session.DrawCircle(50, 40, 27.3f, filled: true);
        session.DrawEllipse(120, 60, 30, 18, thickness: 5.5f);
        session.DrawPolyline(new[] { (5f, 110f), (40f, 70f), (70f, 112f), (95f, 80f) }, thickness: 4f);
        session.DrawShape(new NS.RectangleShape(90, 10, 40, 22, isFilled: true, cornerRadius: 6) { Transform = NM.DrawingTransform.Rotate(23, new PointF(110, 21)) });
        session.DrawPath(NP.PathGeometry.Combine(NP.PathGeometry.Circle(new PointF(130, 95), 20), NP.PathGeometry.Circle(new PointF(134, 95), 9))
            .WithFillRule(NP.PathFillRule.EvenOdd), filled: true);
        session.DrawText("Inanna", 20, 100, 26, Color.White);
        var stroke = new NM.Stroke(6f);
        foreach ((int x, int y) in new[] { (10, 10), (30, 18), (44, 9), (61, 25) }) { stroke.AddPoint(x, y); }
        under.AddStroke(stroke);
        NM.DrawingLayer over = session.AddLayer("Over", Color.FromRgb(230, 150, 40));
        over.Parallax = 0.6f;
        session.ActiveLayer = over;
        var random = new NPl.DrawingRandom(11);
        for (int i = 0; i < 70; i++)
        {
            session.DrawBrushStroke((float)random.Uniform(0, 160), (float)random.Uniform(0, 120), (float)random.Uniform(12, 40),
                (float)random.Uniform(4, 11), (float)random.Uniform(0, 360), (float)random.Normal(0, 0.12));
        }
        var group = new NS.DrawingGroup { Transform = new NM.DrawingTransform(new PointF(4, -3), 12, 1.2f, 0.9f, new PointF(80, 60)) };
        group.Add(new NS.BrushStrokeShape(70, 55, 50, 14, 17f, -0.1f, 2.5f));
        group.Add(new NS.LineShape(60, 80, 110, 70, 3f));
        session.DrawShape(group);
        session.Camera = new NM.DrawingCamera { Pan = new PointF(6, -4), Zoom = 1.15f, Rotation = 4f };
        return session;
    }

    private static NPl.DrawingPlaneImage NoSkiaSource()
    {
        var image = new NPl.DrawingPlaneImage(Output.Width / 2, Output.Height / 2);
        image.Fill((x, y) => new Vector4(x / 100f, y / 75f, ((x * 7) + (y * 3)) % 11 / 10f, ((x + y) % 5) / 4f));
        return image;
    }

    private static List<string> NoSkiaResults()
    {
        var results = new List<string>();
        foreach (bool opaque in new[] { true, false })
        {
            using NSession session = NoSkiaScene(opaque);
            results.Add(Hash(session.ExportMask(Output, NMeaning.HardEdged).Data));
            results.Add(Hash(session.ExportMask(session.Layers[1], Output, NMeaning.HardEdged).Data));
            NPa.DrawingOwnershipMap map = session.ExportOwnershipMap(Output);
            results.Add(Hash(map.Owners.ToArray()));
            var stats = new List<string>();
            for (int i = 0; i < map.ElementCount; i++) { stats.Add(map.GetPixelCount(i) + "@" + map.GetBounds(i)); }
            results.Add(string.Join(";", stats));
            NPa.DrawingOwnershipMap filled = map.FillUnowned();
            results.Add(Hash(filled.Owners.ToArray()));
            results.Add(Hash(filled.CreateBoundaryPlane(1.3f).Data));
            Vector4[] means = filled.GetMeanColors(NoSkiaSource());
            results.Add(Hash(means));
            results.Add(Hash(filled.GetMeanColors(NoSkiaSource().ToRgba(), Output.Width / 2, Output.Height / 2)));
            var painted = new NPl.DrawingPlaneImage(Output);
            filled.ApplyColors(painted, means);
            results.Add(Hash(painted.ToRgba()));
            var texture = new NPl.DrawingPlane(Output);
            filled.FillByElement(texture, p => (float)((Math.Sin(p.Across * 0.9) * 0.1) + (p.NormalizedAlong * 0.05) + p.ElementIndex));
            results.Add(Hash(texture.Data));
        }
        results.Add(string.Join(" ", new NS.BrushStrokeShape(12.5f, 7.25f, 33f, 9f, 120f, 0.13f, 3.3f).GetPoints()));
        results.Add(Hash(NoSkiaKeyframes()));
        return results;
    }

    private static byte[] NoSkiaKeyframes()
    {
        var frames = new List<byte>();
        using var player = new NPa.KeyframePlayer(8, 6, 2.0, (k, s, rgba) => KeyframePixels(k, rgba));
        var frame = new byte[8 * 6 * 4];
        foreach (double t in KeyframeTimes)
        {
            player.RenderFrame(t, frame);
            frames.AddRange(frame);
        }
        using var planes = new NPa.PlaneKeyframePlayer(8, 6, 2.0, (k, s, image) => image.Fill((x, y) => new Vector4(k * 0.3f, x / 8f, y / 6f, 0.25f + (k % 3 * 0.25f))));
        var image = new NPl.DrawingPlaneImage(8, 6);
        foreach (double t in KeyframeTimes)
        {
            planes.RenderFrame(t, image);
            frames.AddRange(image.ToRgba());
        }
        return frames.ToArray();
    }
}
