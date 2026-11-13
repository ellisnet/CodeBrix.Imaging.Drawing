extern alias noskia;
extern alias skia;

using System.Collections.Generic;
using System.Numerics;
using System.Text;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using SilverAssertions;
using Xunit;
using NA = noskia::CodeBrix.Imaging.Drawing.Animation;
using NI = noskia::CodeBrix.Imaging.Drawing.Images;
using NM = noskia::CodeBrix.Imaging.Drawing.Models;
using NP = noskia::CodeBrix.Imaging.Drawing.Paths;
using NS = noskia::CodeBrix.Imaging.Drawing.Shapes;
using NT = noskia::CodeBrix.Imaging.Drawing.Text;
using SA = skia::CodeBrix.Imaging.Drawing.Animation;
using SI = skia::CodeBrix.Imaging.Drawing.Images;
using SM = skia::CodeBrix.Imaging.Drawing.Models;
using SP = skia::CodeBrix.Imaging.Drawing.Paths;
using SS = skia::CodeBrix.Imaging.Drawing.Shapes;
using ST = skia::CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for the path geometry: everything a path COMPUTES - lengths, points and directions
/// at fractions of the length, bounds, trimmed and dashed pieces, flattened polylines, morphs,
/// transforms, shape outlines, motion along a path, sprite frame choice - is shared managed
/// arithmetic, so the two assemblies must agree EXACTLY (compared through the round-trip
/// float text of every value). Each scenario is written once per alias.
/// </summary>
public class PathParityTests
{
    private const string Signature =
        "M 40 120 C 80 40, 140 40, 160 120 S 240 200, 280 120 Q 320 60 360 120 T 440 120 A 40 30 15 1 1 520 120 L 560 80 Z m 20 20 h 30 v 30 z";

    private static readonly float[] Fractions = BuildFractions();

    private static float[] BuildFractions()
    {
        var fractions = new float[101];
        for (int i = 0; i <= 100; i++) { fractions[i] = i / 100f; }
        return fractions;
    }

    private static string Describe(SP.PathGeometry g)
    {
        var text = new StringBuilder();
        text.Append(g.GetLength().ToString("R")).Append('|').Append(g.GetBounds()).Append('|').Append(g.ToSvgPathData()).Append('|');
        foreach (float f in Fractions)
        {
            SP.PathPoint p = g.GetPointAt(f);
            text.Append(p.Position.X.ToString("R")).Append(',').Append(p.Position.Y.ToString("R")).Append(',')
                .Append(p.Tangent.X.ToString("R")).Append(',').Append(p.Tangent.Y.ToString("R")).Append(',')
                .Append(p.Distance.ToString("R")).Append(',').Append(p.ContourIndex).Append(';');
        }
        foreach (SP.PathPolyline line in g.Flatten(0.2f))
        {
            text.Append(line.IsClosed ? 'Z' : 'O');
            foreach (PointF point in line.GetPoints()) { text.Append(point.X.ToString("R")).Append(' ').Append(point.Y.ToString("R")).Append(' '); }
        }
        return text.ToString();
    }

    private static string Describe(NP.PathGeometry g)
    {
        var text = new StringBuilder();
        text.Append(g.GetLength().ToString("R")).Append('|').Append(g.GetBounds()).Append('|').Append(g.ToSvgPathData()).Append('|');
        foreach (float f in Fractions)
        {
            NP.PathPoint p = g.GetPointAt(f);
            text.Append(p.Position.X.ToString("R")).Append(',').Append(p.Position.Y.ToString("R")).Append(',')
                .Append(p.Tangent.X.ToString("R")).Append(',').Append(p.Tangent.Y.ToString("R")).Append(',')
                .Append(p.Distance.ToString("R")).Append(',').Append(p.ContourIndex).Append(';');
        }
        foreach (NP.PathPolyline line in g.Flatten(0.2f))
        {
            text.Append(line.IsClosed ? 'Z' : 'O');
            foreach (PointF point in line.GetPoints()) { text.Append(point.X.ToString("R")).Append(' ').Append(point.Y.ToString("R")).Append(' '); }
        }
        return text.ToString();
    }

    private static IEnumerable<SP.PathGeometry> SkiaGeometries()
    {
        SP.PathGeometry signature = SP.PathGeometry.Parse(Signature);
        yield return signature;
        yield return SP.PathGeometry.Star(new PointF(100, 100), 80, 33.3f, 7, 11f);
        yield return SP.PathGeometry.RoundedPolygon(new[] { new PointF(10, 10), new PointF(200, 30), new PointF(150, 180), new PointF(30, 140) }, 25);
        yield return SP.PathGeometry.RoundedRectangle(new RectangleF(5, 5, 300, 120), 40);
        yield return new SP.PathGeometryBuilder().AddArc(new PointF(50, 50), 40, 20, 30, 250).Build();
        yield return signature.Trim(0.2f, 0.85f, 0.5f);
        yield return signature.Trim(0f, 0.3f, 0.9f);
        yield return signature.Dash(new[] { 13f, 7f, 0f, 7f }, 4.5f);
        yield return signature.Transformed(Matrix3x2.CreateRotation(0.7f) * Matrix3x2.CreateScale(1.3f, 0.8f));
        yield return signature.Reversed();
        var morph = new SP.PathMorph(signature, SP.PathGeometry.Star(new PointF(300, 120), 100, 40, 5));
        yield return morph.GetPath(0.37f);
        yield return new SP.PathMorph(SP.PathGeometry.Line(new PointF(0, 0), new PointF(100, 30)), SP.PathGeometry.Circle(new PointF(50, 50), 40)).GetPath(0.61f);
    }

    private static IEnumerable<NP.PathGeometry> NoSkiaGeometries()
    {
        NP.PathGeometry signature = NP.PathGeometry.Parse(Signature);
        yield return signature;
        yield return NP.PathGeometry.Star(new PointF(100, 100), 80, 33.3f, 7, 11f);
        yield return NP.PathGeometry.RoundedPolygon(new[] { new PointF(10, 10), new PointF(200, 30), new PointF(150, 180), new PointF(30, 140) }, 25);
        yield return NP.PathGeometry.RoundedRectangle(new RectangleF(5, 5, 300, 120), 40);
        yield return new NP.PathGeometryBuilder().AddArc(new PointF(50, 50), 40, 20, 30, 250).Build();
        yield return signature.Trim(0.2f, 0.85f, 0.5f);
        yield return signature.Trim(0f, 0.3f, 0.9f);
        yield return signature.Dash(new[] { 13f, 7f, 0f, 7f }, 4.5f);
        yield return signature.Transformed(Matrix3x2.CreateRotation(0.7f) * Matrix3x2.CreateScale(1.3f, 0.8f));
        yield return signature.Reversed();
        var morph = new NP.PathMorph(signature, NP.PathGeometry.Star(new PointF(300, 120), 100, 40, 5));
        yield return morph.GetPath(0.37f);
        yield return new NP.PathMorph(NP.PathGeometry.Line(new PointF(0, 0), new PointF(100, 30)), NP.PathGeometry.Circle(new PointF(50, 50), 40)).GetPath(0.61f);
    }

    [Fact]
    public void Path_measurement_trimming_dashing_flattening_and_morphing_are_identical()
    {
        //Act
        var skia = new List<string>();
        foreach (SP.PathGeometry g in SkiaGeometries()) { skia.Add(Describe(g)); }
        var noSkia = new List<string>();
        foreach (NP.PathGeometry g in NoSkiaGeometries()) { noSkia.Add(Describe(g)); }

        //Assert
        noSkia.Count.Should().Be(skia.Count);
        for (int i = 0; i < skia.Count; i++)
        {
            noSkia[i].Should().Be(skia[i], "geometry case " + i);
        }
    }

    [Fact]
    public void Shape_outlines_are_identical()
    {
        //Arrange
        var skiaFonts = new ST.FontRegistry();
        skiaFonts.RegisterFont(SvgTestAssets.TestFontPath);
        var noSkiaFonts = new NT.FontRegistry();
        noSkiaFonts.RegisterFont(SvgTestAssets.TestFontPath);

        //Act
        var skia = new[]
        {
            new SS.ArrowShape(10, 20, 200, 90, 6, null, 40).GetOutline(),
            new SS.RectangleShape(10, 20, 100, 60, cornerRadius: 12).GetOutline(),
            new SS.EllipseShape(80, 60, 50, 25).GetOutline(),
            new SS.TextShape("Draw on!", 20, 100, 48, skiaFonts, tracking: 0.05f).GetOutline(),
        };
        var noSkia = new[]
        {
            new NS.ArrowShape(10, 20, 200, 90, 6, null, 40).GetOutline(),
            new NS.RectangleShape(10, 20, 100, 60, cornerRadius: 12).GetOutline(),
            new NS.EllipseShape(80, 60, 50, 25).GetOutline(),
            new NS.TextShape("Draw on!", 20, 100, 48, noSkiaFonts, tracking: 0.05f).GetOutline(),
        };

        //Assert
        for (int i = 0; i < skia.Length; i++)
        {
            Describe(noSkia[i]).Should().Be(Describe(skia[i]), "outline case " + i);
        }
    }

    [Fact]
    public void Motion_along_a_path_and_sprite_frames_are_identical()
    {
        //Arrange
        SP.PathGeometry skiaPath = SP.PathGeometry.Parse(Signature);
        NP.PathGeometry noSkiaPath = NP.PathGeometry.Parse(Signature);
        var skiaAnimations = new SM.DrawingAnimations();
        var noSkiaAnimations = new NM.DrawingAnimations();
        var skiaMarker = new SS.CircleShape(0, 0, 4);
        var noSkiaMarker = new NS.CircleShape(0, 0, 4);
        skiaAnimations.BindAlongPath(skiaMarker, skiaPath, new SA.FloatTrack { { 0, 0f }, { 3, 1f, SA.EasingKind.CubicInOut } }, true, 90f);
        noSkiaAnimations.BindAlongPath(noSkiaMarker, noSkiaPath, new NA.FloatTrack { { 0, 0f }, { 3, 1f, NA.EasingKind.CubicInOut } }, true, 90f);
        using SI.ImageSource skiaImage = SI.ImageSource.FromRgba(new byte[5 * 4], 5, 1);
        using NI.ImageSource noSkiaImage = NI.ImageSource.FromRgba(new byte[5 * 4], 5, 1);
        using SI.SpriteSheet skiaSheet = SI.SpriteSheet.FromGrid(skiaImage, 5, 1);
        using NI.SpriteSheet noSkiaSheet = NI.SpriteSheet.FromGrid(noSkiaImage, 5, 1);
        var skiaSprite = new SS.SpriteShape(skiaSheet, new RectangleF(0, 0, 1, 1), 12.5, SI.SpritePlayback.PingPong) { PlaybackStartTime = 0.3 };
        var noSkiaSprite = new NS.SpriteShape(noSkiaSheet, new RectangleF(0, 0, 1, 1), 12.5, NI.SpritePlayback.PingPong) { PlaybackStartTime = 0.3 };

        //Act and Assert
        for (int i = 0; i <= 90; i++)
        {
            double t = i / 30.0;
            skiaAnimations.Apply(t);
            noSkiaAnimations.Apply(t);
            noSkiaMarker.Transform.Translation.Should().Be(skiaMarker.Transform.Translation);
            noSkiaMarker.Transform.Rotation.Should().Be(skiaMarker.Transform.Rotation);
            noSkiaSprite.GetFrameAt(t).Should().Be(skiaSprite.GetFrameAt(t));
        }
    }
}
