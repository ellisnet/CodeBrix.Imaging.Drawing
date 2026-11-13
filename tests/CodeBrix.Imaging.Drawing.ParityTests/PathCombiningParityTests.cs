extern alias noskia;
extern alias skia;

using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;
using NE = noskia::CodeBrix.Imaging.Drawing.Effects;
using NM = noskia::CodeBrix.Imaging.Drawing.Models;
using NMeaning = noskia::CodeBrix.Imaging.Drawing.MaskMeaning;
using NP = noskia::CodeBrix.Imaging.Drawing.Paths;
using NPl = noskia::CodeBrix.Imaging.Drawing.Planes;
using NS = noskia::CodeBrix.Imaging.Drawing.Shapes;
using NSession = noskia::CodeBrix.Imaging.Drawing.DrawingSession;
using NSessionOptions = noskia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using NT = noskia::CodeBrix.Imaging.Drawing.Text;
using SE = skia::CodeBrix.Imaging.Drawing.Effects;
using SM = skia::CodeBrix.Imaging.Drawing.Models;
using SMeaning = skia::CodeBrix.Imaging.Drawing.MaskMeaning;
using SP = skia::CodeBrix.Imaging.Drawing.Paths;
using SPl = skia::CodeBrix.Imaging.Drawing.Planes;
using SS = skia::CodeBrix.Imaging.Drawing.Shapes;
using SSession = skia::CodeBrix.Imaging.Drawing.DrawingSession;
using SSessionOptions = skia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using ST = skia::CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for path combining, stroke-to-outline, inflating, merged text outlines, the two mask
/// meanings and effects that follow scale and camera. What the polygon tools COMPUTE is exact
/// integer arithmetic on points both assemblies flatten identically, so every result is
/// compared EXACTLY (its SVG path data, round-trip numbers, and fill rule). What is drawn from
/// them, the masks and the effects go through each backend's canvas and match within the image
/// tolerance, over an opaque background and over transparency.
/// </summary>
public class PathCombiningParityTests
{
    private static readonly Size Frame = new Size(400, 300);
    private static readonly string RobotoPath = Path.Combine(SvgTestAssets.AssetsDirectory, "Fonts", "Roboto-Regular.ttf");
    private const string Signature =
        "M 40 120 C 80 40, 140 40, 160 120 S 240 200, 280 120 Q 320 60 360 120 T 440 120 A 40 30 15 1 1 520 120 L 560 80 Z m 20 20 h 30 v 30 z";

    private static string Describe(SP.PathGeometry g) => g == null ? "null" : g.FillRule + "|" + g.Contours.Count + "|" + g.ToSvgPathData();

    private static string Describe(NP.PathGeometry g) => g == null ? "null" : g.FillRule + "|" + g.Contours.Count + "|" + g.ToSvgPathData();

    private static IEnumerable<SP.PathGeometry> SkiaResults()
    {
        SP.PathGeometry signature = SP.PathGeometry.Parse(Signature);
        SP.PathGeometry star = SP.PathGeometry.Star(new PointF(300, 120), 120, 50, 7, 10);
        SP.PathGeometry circle = SP.PathGeometry.Circle(new PointF(200, 100), 70);
        SP.PathGeometry evenOdd = SP.PathGeometry.Combine(circle, SP.PathGeometry.Circle(new PointF(210, 100), 30)).WithFillRule(SP.PathFillRule.EvenOdd);
        yield return signature.Union(star);
        yield return signature.Intersect(star, 0.1f);
        yield return star.Subtract(circle);
        yield return circle.ExclusiveOr(star, 1f);
        yield return evenOdd.Union(star);
        yield return signature.Simplify();
        foreach (SP.PathBooleanOperation op in new[] { SP.PathBooleanOperation.Union, SP.PathBooleanOperation.Intersection, SP.PathBooleanOperation.Difference, SP.PathBooleanOperation.ExclusiveOr })
        {
            yield return SP.PathGeometry.CombineAll(new[] { signature, star, circle, evenOdd }, op);
        }
        foreach (SP.PathStrokeCap cap in new[] { SP.PathStrokeCap.Butt, SP.PathStrokeCap.Round, SP.PathStrokeCap.Square })
        {
            foreach (SP.PathStrokeJoin join in new[] { SP.PathStrokeJoin.Miter, SP.PathStrokeJoin.Round, SP.PathStrokeJoin.Bevel })
            {
                yield return signature.ToStrokeOutline(new SP.PathStrokeStyle(9, cap, join, 3f));
            }
        }
        yield return star.ToStrokeOutline(new SP.PathStrokeStyle(5, SP.PathStrokeCap.Round, SP.PathStrokeJoin.Miter, 4f, new[] { 14f, 6f, 0f, 6f }, 3f));
        yield return star.Inflate(12, SP.PathStrokeJoin.Miter);
        yield return star.Inflate(12);
        yield return signature.Deflate(4, SP.PathStrokeJoin.Bevel);
        var shape = new SS.PathShape(signature, new SP.PathStrokeStyle(6f, SP.PathStrokeCap.Square)) { TrimStart = 0.2f, TrimEnd = 0.7f, TrimOffset = 0.4f };
        yield return shape.GetStrokeOutline();
    }

    private static IEnumerable<NP.PathGeometry> NoSkiaResults()
    {
        NP.PathGeometry signature = NP.PathGeometry.Parse(Signature);
        NP.PathGeometry star = NP.PathGeometry.Star(new PointF(300, 120), 120, 50, 7, 10);
        NP.PathGeometry circle = NP.PathGeometry.Circle(new PointF(200, 100), 70);
        NP.PathGeometry evenOdd = NP.PathGeometry.Combine(circle, NP.PathGeometry.Circle(new PointF(210, 100), 30)).WithFillRule(NP.PathFillRule.EvenOdd);
        yield return signature.Union(star);
        yield return signature.Intersect(star, 0.1f);
        yield return star.Subtract(circle);
        yield return circle.ExclusiveOr(star, 1f);
        yield return evenOdd.Union(star);
        yield return signature.Simplify();
        foreach (NP.PathBooleanOperation op in new[] { NP.PathBooleanOperation.Union, NP.PathBooleanOperation.Intersection, NP.PathBooleanOperation.Difference, NP.PathBooleanOperation.ExclusiveOr })
        {
            yield return NP.PathGeometry.CombineAll(new[] { signature, star, circle, evenOdd }, op);
        }
        foreach (NP.PathStrokeCap cap in new[] { NP.PathStrokeCap.Butt, NP.PathStrokeCap.Round, NP.PathStrokeCap.Square })
        {
            foreach (NP.PathStrokeJoin join in new[] { NP.PathStrokeJoin.Miter, NP.PathStrokeJoin.Round, NP.PathStrokeJoin.Bevel })
            {
                yield return signature.ToStrokeOutline(new NP.PathStrokeStyle(9, cap, join, 3f));
            }
        }
        yield return star.ToStrokeOutline(new NP.PathStrokeStyle(5, NP.PathStrokeCap.Round, NP.PathStrokeJoin.Miter, 4f, new[] { 14f, 6f, 0f, 6f }, 3f));
        yield return star.Inflate(12, NP.PathStrokeJoin.Miter);
        yield return star.Inflate(12);
        yield return signature.Deflate(4, NP.PathStrokeJoin.Bevel);
        var shape = new NS.PathShape(signature, new NP.PathStrokeStyle(6f, NP.PathStrokeCap.Square)) { TrimStart = 0.2f, TrimEnd = 0.7f, TrimOffset = 0.4f };
        yield return shape.GetStrokeOutline();
    }

    [Fact]
    public void Path_combining_stroke_outlines_and_inflating_are_identical()
    {
        //Act
        var skia = new List<string>();
        foreach (SP.PathGeometry g in SkiaResults()) { skia.Add(Describe(g)); }
        var noSkia = new List<string>();
        foreach (NP.PathGeometry g in NoSkiaResults()) { noSkia.Add(Describe(g)); }

        //Assert
        noSkia.Count.Should().Be(skia.Count);
        for (int i = 0; i < skia.Count; i++)
        {
            skia[i].Length.Should().BeGreaterThan(20, "result " + i + " is not empty");
            noSkia[i].Should().Be(skia[i], "result " + i);
        }
    }

    [Fact]
    public void Merged_text_outlines_are_identical()
    {
        //Arrange
        var skiaFonts = new ST.FontRegistry();
        skiaFonts.RegisterFont(RobotoPath);
        var noSkiaFonts = new NT.FontRegistry();
        noSkiaFonts.RegisterFont(RobotoPath);
        ST.TextLayoutResult skiaLayout = ST.TextLayoutEngine.Layout(skiaFonts, "Hello & WTF 48%", 64);
        NT.TextLayoutResult noSkiaLayout = NT.TextLayoutEngine.Layout(noSkiaFonts, "Hello & WTF 48%", 64);

        //Act
        string skia = Describe(new SS.TextBlockShape(skiaLayout, 10, 20) { MergeOverlaps = true }.GetOutline());
        string noSkia = Describe(new NS.TextBlockShape(noSkiaLayout, 10, 20) { MergeOverlaps = true }.GetOutline());
        string skiaSimplified = Describe(skiaLayout.GetOutline(10, 20).Simplify(0.1f));

        //Assert
        noSkia.Should().Be(skia);
        skia.Length.Should().BeGreaterThan(1000);
        skiaSimplified.Length.Should().BeGreaterThan(1000);
    }

    private static byte[] RawToPng(byte[] rgba, Size size)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgba, size.Width, size.Height, PngFormat.Instance);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static void Compare(byte[] skia, byte[] noSkia, string label)
        => ImageComparison.AssertSimilar(RawToPng(skia, Frame), RawToPng(noSkia, Frame), label);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Stroke_outlines_and_combined_paths_draw_alike(bool includeBackground)
    {
        //Arrange
        using var skia = new SSession(new SSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(236, 232, 224) });
        skia.AddLayer("A", Color.FromRgb(30, 40, 120));
        SP.PathGeometry sStar = SP.PathGeometry.Star(new PointF(200, 150), 120, 50, 6);
        skia.DrawPath(sStar.ExclusiveOr(SP.PathGeometry.Circle(new PointF(200, 150), 70)), filled: true);
        skia.DrawPath(SP.PathGeometry.Parse(Signature).Transformed(System.Numerics.Matrix3x2.CreateScale(0.6f)).ToStrokeOutline(new SP.PathStrokeStyle(8, SP.PathStrokeCap.Square, SP.PathStrokeJoin.Bevel)), filled: true, color: Color.FromRgb(200, 60, 30));
        using var noSkia = new NSession(new NSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(236, 232, 224) });
        noSkia.AddLayer("A", Color.FromRgb(30, 40, 120));
        NP.PathGeometry nStar = NP.PathGeometry.Star(new PointF(200, 150), 120, 50, 6);
        noSkia.DrawPath(nStar.ExclusiveOr(NP.PathGeometry.Circle(new PointF(200, 150), 70)), filled: true);
        noSkia.DrawPath(NP.PathGeometry.Parse(Signature).Transformed(System.Numerics.Matrix3x2.CreateScale(0.6f)).ToStrokeOutline(new NP.PathStrokeStyle(8, NP.PathStrokeCap.Square, NP.PathStrokeJoin.Bevel)), filled: true, color: Color.FromRgb(200, 60, 30));

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, includeBackground);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, includeBackground);

        //Assert
        Compare(skiaPixels, noSkiaPixels, "combined paths, background " + includeBackground);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Effects_under_scale_rotation_and_camera_draw_alike(bool includeBackground)
    {
        //Arrange
        using var skia = new SSession(new SSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(236, 232, 224) });
        skia.AddLayer("A", Color.FromRgb(30, 40, 120));
        skia.DrawRectangle(120, 90, 80, 60, filled: true).Effects = SE.DrawingEffects.DropShadow(new PointF(8, 10), 6, Color.FromRgba(0, 0, 0, 180)).WithGlow(6, Color.Gold);
        skia.Layers[0].GetElements()[0].Transform = new SM.DrawingTransform(new PointF(10, 0), 35f, 1.5f, 1.2f, new PointF(160, 120));
        skia.DrawCircle(290, 180, 30, filled: true).Effects = SE.DrawingEffects.DropShadow(new PointF(12, 0), 3, Color.FromRgba(80, 0, 0, 200), SE.ShadowDirection.RotatesWithElement);
        skia.Layers[0].GetElements()[1].Transform = SM.DrawingTransform.Rotate(70, new PointF(290, 180));
        skia.Camera = new SM.DrawingCamera { Zoom = 1.3f, Rotation = 8f };
        using var noSkia = new NSession(new NSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(236, 232, 224) });
        noSkia.AddLayer("A", Color.FromRgb(30, 40, 120));
        noSkia.DrawRectangle(120, 90, 80, 60, filled: true).Effects = NE.DrawingEffects.DropShadow(new PointF(8, 10), 6, Color.FromRgba(0, 0, 0, 180)).WithGlow(6, Color.Gold);
        noSkia.Layers[0].GetElements()[0].Transform = new NM.DrawingTransform(new PointF(10, 0), 35f, 1.5f, 1.2f, new PointF(160, 120));
        noSkia.DrawCircle(290, 180, 30, filled: true).Effects = NE.DrawingEffects.DropShadow(new PointF(12, 0), 3, Color.FromRgba(80, 0, 0, 200), NE.ShadowDirection.RotatesWithElement);
        noSkia.Layers[0].GetElements()[1].Transform = NM.DrawingTransform.Rotate(70, new PointF(290, 180));
        noSkia.Camera = new NM.DrawingCamera { Zoom = 1.3f, Rotation = 8f };

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, includeBackground);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, includeBackground);

        //Assert
        Compare(skiaPixels, noSkiaPixels, "transformed effects, background " + includeBackground);
    }

    [Fact]
    public void Coverage_and_visible_alpha_masks_match()
    {
        //Arrange - a faded layer, a faded element and a session fade
        using var skia = new SSession(new SSessionOptions { CalibrationSize = Frame, LayerOpacity = 160 });
        skia.AddLayer("A", Color.FromRgb(30, 40, 120));
        skia.DrawCircle(150, 150, 90, filled: true);
        skia.DrawRectangle(200, 60, 150, 120, filled: true).Opacity = 0.5f;
        skia.Layers[0].Opacity = 0.7f;
        skia.Opacity = 0.9f;
        using var noSkia = new NSession(new NSessionOptions { CalibrationSize = Frame, LayerOpacity = 160 });
        noSkia.AddLayer("A", Color.FromRgb(30, 40, 120));
        noSkia.DrawCircle(150, 150, 90, filled: true);
        noSkia.DrawRectangle(200, 60, 150, 120, filled: true).Opacity = 0.5f;
        noSkia.Layers[0].Opacity = 0.7f;
        noSkia.Opacity = 0.9f;

        //Act
        var skiaVisible = new SPl.DrawingPlane(400, 300);
        skia.ExportMask(skiaVisible, SMeaning.VisibleAlpha);
        var noSkiaVisible = new NPl.DrawingPlane(400, 300);
        noSkia.ExportMask(noSkiaVisible, NMeaning.VisibleAlpha);
        SPl.DrawingPlane skiaCoverage = skia.ExportMask(Frame, SMeaning.Coverage);
        NPl.DrawingPlane noSkiaCoverage = noSkia.ExportMask(Frame, NMeaning.Coverage);

        //Assert - each meaning matches across the backends; visible alpha is fainter than coverage
        Compare(ToGrey(skiaVisible.ToBytes()), ToGrey(noSkiaVisible.ToBytes()), "visible alpha");
        Compare(ToGrey(skiaCoverage.ToBytes()), ToGrey(noSkiaCoverage.ToBytes()), "coverage");
        skiaVisible[150, 150].Should().BeLessThan(skiaCoverage[150, 150]);
        noSkiaVisible[150, 150].Should().BeLessThan(noSkiaCoverage[150, 150]);
    }

    private static byte[] ToGrey(byte[] values)
    {
        var rgba = new byte[values.Length * 4];
        for (int i = 0; i < values.Length; i++)
        {
            rgba[i * 4] = values[i];
            rgba[(i * 4) + 1] = values[i];
            rgba[(i * 4) + 2] = values[i];
            rgba[(i * 4) + 3] = 255;
        }
        return rgba;
    }
}
