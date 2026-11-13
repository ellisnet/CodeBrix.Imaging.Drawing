extern alias noskia;
extern alias skia;

using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;
using NC = noskia::CodeBrix.Imaging.Drawing.Captions;
using NF = noskia::CodeBrix.Imaging.Drawing.Fills;
using NL = noskia::CodeBrix.Imaging.Drawing.Layout;
using NM = noskia::CodeBrix.Imaging.Drawing.Models;
using NP = noskia::CodeBrix.Imaging.Drawing.Paths;
using NS = noskia::CodeBrix.Imaging.Drawing.Shapes;
using NSafe = noskia::CodeBrix.Imaging.Drawing.SafeAreas;
using NSession = noskia::CodeBrix.Imaging.Drawing.DrawingSession;
using NSessionOptions = noskia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using NT = noskia::CodeBrix.Imaging.Drawing.Text;
using SC = skia::CodeBrix.Imaging.Drawing.Captions;
using SF = skia::CodeBrix.Imaging.Drawing.Fills;
using SL = skia::CodeBrix.Imaging.Drawing.Layout;
using SM = skia::CodeBrix.Imaging.Drawing.Models;
using SP = skia::CodeBrix.Imaging.Drawing.Paths;
using SS = skia::CodeBrix.Imaging.Drawing.Shapes;
using SSafe = skia::CodeBrix.Imaging.Drawing.SafeAreas;
using SSession = skia::CodeBrix.Imaging.Drawing.DrawingSession;
using SSessionOptions = skia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using ST = skia::CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for text blocks, captions and staggered pieces. Everything text COMPUTES - shaping,
/// kerning, line breaking, alignment, trimming, every cluster's position and glyph outline, text
/// along a path, caption parsing and placement, stagger states - is shared managed code, so the two
/// assemblies must agree EXACTLY (round-trip float text of every value). What they DRAW goes through
/// each backend's anti-aliased canvas and is compared within the image tolerance.
/// </summary>
public class TextParityTests
{
    private static readonly Size Frame = new Size(480, 320);
    private static readonly string RobotoPath = Path.Combine(SvgTestAssets.AssetsDirectory, "Fonts", "Roboto-Regular.ttf");
    private const string Paragraph = "Kerned WAVE To typography, wrapped and justified: office affinity flows on and on until it is trimmed away.";
    private const string Srt = "1\n00:00:00,500 --> 00:00:03,000\nCaptions are <i>burned in</i>, wrapped to\nthe safe area &amp; boxed\n\n2\n00:00:02,000 --> 00:00:04,000\nA second cue\n";

    private static ST.FontRegistry SkiaFonts()
    {
        var fonts = new ST.FontRegistry();
        fonts.RegisterFont(RobotoPath);
        fonts.RegisterFont(SvgTestAssets.TestFontPath);
        return fonts;
    }

    private static NT.FontRegistry NoSkiaFonts()
    {
        var fonts = new NT.FontRegistry();
        fonts.RegisterFont(RobotoPath);
        fonts.RegisterFont(SvgTestAssets.TestFontPath);
        return fonts;
    }

    private static string R(float value) => value.ToString("R");

    private static string R(RectangleF? r) => r.HasValue ? $"{R(r.Value.X)},{R(r.Value.Y)},{R(r.Value.Width)},{R(r.Value.Height)}" : "none";

    private static string Describe(ST.TextLayoutResult layout)
    {
        var text = new StringBuilder();
        text.Append(R(layout.Width)).Append('x').Append(R(layout.Height)).Append('|').Append(R(layout.ContentBounds)).Append('|')
            .Append(R(layout.InkBounds)).Append('|').Append(layout.IsTruncated).Append(layout.ShownTextLength).Append(layout.WordCount).Append('|');
        foreach (ST.TextLineMetrics line in layout.Lines)
        {
            text.Append(line.TextIndex).Append('+').Append(line.TextLength).Append(' ').Append(R(line.Top)).Append(' ').Append(R(line.Height)).Append(' ')
                .Append(R(line.BaselineOffset)).Append(' ').Append(R(line.Left)).Append(' ').Append(R(line.Width)).Append(';');
        }
        foreach (ST.TextCluster c in layout.Clusters)
        {
            text.Append(c.TextIndex).Append('+').Append(c.TextLength).Append(c.Text).Append(c.WordIndex).Append(' ').Append(R(c.Origin.X)).Append(',')
                .Append(R(c.Origin.Y)).Append(' ').Append(R(c.Advance)).Append(' ').Append(R(c.InkBounds)).Append(' ').Append(c.GetOutline().ToSvgPathData()).Append(';');
        }
        return text.ToString();
    }

    private static string Describe(NT.TextLayoutResult layout)
    {
        var text = new StringBuilder();
        text.Append(R(layout.Width)).Append('x').Append(R(layout.Height)).Append('|').Append(R(layout.ContentBounds)).Append('|')
            .Append(R(layout.InkBounds)).Append('|').Append(layout.IsTruncated).Append(layout.ShownTextLength).Append(layout.WordCount).Append('|');
        foreach (NT.TextLineMetrics line in layout.Lines)
        {
            text.Append(line.TextIndex).Append('+').Append(line.TextLength).Append(' ').Append(R(line.Top)).Append(' ').Append(R(line.Height)).Append(' ')
                .Append(R(line.BaselineOffset)).Append(' ').Append(R(line.Left)).Append(' ').Append(R(line.Width)).Append(';');
        }
        foreach (NT.TextCluster c in layout.Clusters)
        {
            text.Append(c.TextIndex).Append('+').Append(c.TextLength).Append(c.Text).Append(c.WordIndex).Append(' ').Append(R(c.Origin.X)).Append(',')
                .Append(R(c.Origin.Y)).Append(' ').Append(R(c.Advance)).Append(' ').Append(R(c.InkBounds)).Append(' ').Append(c.GetOutline().ToSvgPathData()).Append(';');
        }
        return text.ToString();
    }

    private static List<ST.TextLayoutResult> SkiaLayouts(ST.FontRegistry fonts) => new List<ST.TextLayoutResult>
    {
        ST.TextLayoutEngine.Layout(fonts, Paragraph, 30, null, new ST.TextLayoutOptions { MaxWidth = 360, Align = ST.TextAlign.Justify, LineSpacing = 1.2f }),
        ST.TextLayoutEngine.Layout(fonts, Paragraph, 26, "Open Sans", new ST.TextLayoutOptions { MaxWidth = 300, Align = ST.TextAlign.Center, MaxLines = 3, Trimming = ST.TextTrimming.WordEllipsis }),
        ST.TextLayoutEngine.Layout(fonts, Paragraph, 28, null, new ST.TextLayoutOptions { MaxWidth = 280, MaxHeight = 150, VerticalAlign = ST.TextVerticalAlign.Bottom, Trimming = ST.TextTrimming.CharacterEllipsis, Kerning = false }),
        ST.TextLayoutEngine.Layout(fonts, new[]
        {
            new ST.TextRunDescriptor("Mixed ", 50, null) { Tracking = 0.1f },
            new ST.TextRunDescriptor("runs\nand sizes", 24, "Open Sans"),
        }, new ST.TextLayoutOptions { Align = ST.TextAlign.Right }),
    };

    private static List<NT.TextLayoutResult> NoSkiaLayouts(NT.FontRegistry fonts) => new List<NT.TextLayoutResult>
    {
        NT.TextLayoutEngine.Layout(fonts, Paragraph, 30, null, new NT.TextLayoutOptions { MaxWidth = 360, Align = NT.TextAlign.Justify, LineSpacing = 1.2f }),
        NT.TextLayoutEngine.Layout(fonts, Paragraph, 26, "Open Sans", new NT.TextLayoutOptions { MaxWidth = 300, Align = NT.TextAlign.Center, MaxLines = 3, Trimming = NT.TextTrimming.WordEllipsis }),
        NT.TextLayoutEngine.Layout(fonts, Paragraph, 28, null, new NT.TextLayoutOptions { MaxWidth = 280, MaxHeight = 150, VerticalAlign = NT.TextVerticalAlign.Bottom, Trimming = NT.TextTrimming.CharacterEllipsis, Kerning = false }),
        NT.TextLayoutEngine.Layout(fonts, new[]
        {
            new NT.TextRunDescriptor("Mixed ", 50, null) { Tracking = 0.1f },
            new NT.TextRunDescriptor("runs\nand sizes", 24, "Open Sans"),
        }, new NT.TextLayoutOptions { Align = NT.TextAlign.Right }),
    };

    [Fact]
    public void Text_layout_geometry_is_identical()
    {
        //Arrange
        List<ST.TextLayoutResult> skia = SkiaLayouts(SkiaFonts());
        List<NT.TextLayoutResult> noSkia = NoSkiaLayouts(NoSkiaFonts());

        //Assert
        for (int i = 0; i < skia.Count; i++)
        {
            Describe(noSkia[i]).Should().Be(Describe(skia[i]), "layout case " + i);
        }
    }

    [Fact]
    public void Kerned_tracked_lines_and_text_along_a_path_are_identical()
    {
        //Arrange
        ST.FontRegistry skiaFonts = SkiaFonts();
        NT.FontRegistry noSkiaFonts = NoSkiaFonts();
        const string Wave = "M 20 200 C 120 60, 260 60, 360 200 S 560 340, 660 200";

        //Act
        string skia = new SS.TextShape("WAVE To", 10, 80, 60, skiaFonts, tracking: 0.08f).GetOutline().ToSvgPathData() + "|"
            + new SS.TextShape("WAVE To", 10, 80, 60, skiaFonts, (Color?)null, ST.TextAlign.Center, 0f, null, false).GetOutline().ToSvgPathData() + "|"
            + ST.TextLayoutEngine.Layout(skiaFonts, "Words that follow a wave", 30).GetOutlineAlongPath(SP.PathGeometry.Parse(Wave), 15f, -4f).ToSvgPathData();
        string noSkia = new NS.TextShape("WAVE To", 10, 80, 60, noSkiaFonts, tracking: 0.08f).GetOutline().ToSvgPathData() + "|"
            + new NS.TextShape("WAVE To", 10, 80, 60, noSkiaFonts, (Color?)null, NT.TextAlign.Center, 0f, null, false).GetOutline().ToSvgPathData() + "|"
            + NT.TextLayoutEngine.Layout(noSkiaFonts, "Words that follow a wave", 30).GetOutlineAlongPath(NP.PathGeometry.Parse(Wave), 15f, -4f).ToSvgPathData();

        //Assert
        noSkia.Should().Be(skia);
    }

    [Fact]
    public void Captions_parse_and_place_identically()
    {
        //Arrange
        var skiaPresenter = new SC.CaptionPresenter(SC.CaptionTrack.Parse(Srt), SkiaFonts(), new SL.DrawingLayout(Frame, SSafe.SafeAreaGuides.BroadcastTitleSafe));
        var noSkiaPresenter = new NC.CaptionPresenter(NC.CaptionTrack.Parse(Srt), NoSkiaFonts(), new NL.DrawingLayout(Frame, NSafe.SafeAreaGuides.BroadcastTitleSafe));
        var skia = new StringBuilder();
        var noSkia = new StringBuilder();

        //Act
        foreach (double t in new[] { 0.2, 1.0, 2.5, 3.5, 5.0 })
        {
            foreach (SM.DrawingElement e in ((SS.DrawingGroup)skiaPresenter.GetShapeAt(t)).GetChildren())
            {
                skia.Append(R(e.GetTransformedBounds())).Append(';');
            }
            foreach (NM.DrawingElement e in ((NS.DrawingGroup)noSkiaPresenter.GetShapeAt(t)).GetChildren())
            {
                noSkia.Append(R(e.GetTransformedBounds())).Append(';');
            }
            skia.Append('|');
            noSkia.Append('|');
        }
        skia.Append(Describe(skiaPresenter.GetCueLayout(0)));
        noSkia.Append(Describe(noSkiaPresenter.GetCueLayout(0)));

        //Assert
        noSkia.ToString().Should().Be(skia.ToString());
    }

    [Fact]
    public void Staggered_pieces_and_reveals_compute_identically()
    {
        //Arrange
        ST.TextLayoutResult skiaLayout = ST.TextLayoutEngine.Layout(SkiaFonts(), "Stagger these words", 40);
        NT.TextLayoutResult noSkiaLayout = NT.TextLayoutEngine.Layout(NoSkiaFonts(), "Stagger these words", 40);
        SS.DrawingGroup skiaGroup = new SS.TextBlockShape(skiaLayout, 20, 30).CreatePieceGroup(ST.TextUnit.Character);
        NS.DrawingGroup noSkiaGroup = new NS.TextBlockShape(noSkiaLayout, 20, 30).CreatePieceGroup(NT.TextUnit.Character);
        var skiaAnimations = new SM.DrawingAnimations();
        var noSkiaAnimations = new NM.DrawingAnimations();
        skiaAnimations.BindStagger(skiaGroup.GetChildren(), new SM.StaggerOptions { FromOffset = new PointF(0, 25), FromScale = 0.4f, FromRotation = -30, Order = SM.StaggerOrder.CenterOut });
        noSkiaAnimations.BindStagger(noSkiaGroup.GetChildren(), new NM.StaggerOptions { FromOffset = new PointF(0, 25), FromScale = 0.4f, FromRotation = -30, Order = NM.StaggerOrder.CenterOut });
        var skia = new StringBuilder();
        var noSkia = new StringBuilder();

        //Act
        for (int i = 0; i <= 40; i++)
        {
            double t = i * 0.03;
            skiaAnimations.Apply(t);
            noSkiaAnimations.Apply(t);
            foreach (SM.DrawingElement e in skiaGroup.GetChildren()) { skia.Append(R(e.Opacity)).Append(R(e.GetTransformedBounds())).Append(';'); }
            foreach (NM.DrawingElement e in noSkiaGroup.GetChildren()) { noSkia.Append(R(e.Opacity)).Append(R(e.GetTransformedBounds())).Append(';'); }
        }

        //Assert
        noSkia.ToString().Should().Be(skia.ToString());
    }

    private static byte[] RawToPng(byte[] rgba)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgba, Frame.Width, Frame.Height, PngFormat.Instance);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static byte[] DrawSkia(bool background)
    {
        using var session = new SSession(new SSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(24, 26, 40), Fonts = SkiaFonts() });
        session.AddLayer("Text", Color.White);
        ST.TextLayoutResult layout = session.LayoutText(Paragraph, 26, options: new ST.TextLayoutOptions { MaxWidth = 440, Align = ST.TextAlign.Center });
        session.DrawShape(new SS.TextBlockShape(layout, 20, 10, new SF.LinearGradientFill(new PointF(20, 0), new PointF(460, 0), Color.FromRgb(255, 200, 80), Color.FromRgb(80, 200, 255))));
        session.DrawShape(new SS.TextBlockShape(session.LayoutText("OUTLINED", 64), 40, 140, new SP.PathStrokeStyle(2.5f), Color.FromRgb(255, 120, 160)));
        session.DrawShape(new SS.TextBlockShape(session.LayoutText("Typewriter reveal", 34), 40, 230, Color.White) { RevealCount = 9.5f, RevealSoftness = 2f });
        session.DrawCaptions(SC.CaptionTrack.Parse(Srt));
        session.Time = 2.5;
        return session.ExportPixels(Frame, background);
    }

    private static byte[] DrawNoSkia(bool background)
    {
        using var session = new NSession(new NSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(24, 26, 40), Fonts = NoSkiaFonts() });
        session.AddLayer("Text", Color.White);
        NT.TextLayoutResult layout = session.LayoutText(Paragraph, 26, options: new NT.TextLayoutOptions { MaxWidth = 440, Align = NT.TextAlign.Center });
        session.DrawShape(new NS.TextBlockShape(layout, 20, 10, new NF.LinearGradientFill(new PointF(20, 0), new PointF(460, 0), Color.FromRgb(255, 200, 80), Color.FromRgb(80, 200, 255))));
        session.DrawShape(new NS.TextBlockShape(session.LayoutText("OUTLINED", 64), 40, 140, new NP.PathStrokeStyle(2.5f), Color.FromRgb(255, 120, 160)));
        session.DrawShape(new NS.TextBlockShape(session.LayoutText("Typewriter reveal", 34), 40, 230, Color.White) { RevealCount = 9.5f, RevealSoftness = 2f });
        session.DrawCaptions(NC.CaptionTrack.Parse(Srt));
        session.Time = 2.5;
        return session.ExportPixels(Frame, background);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Text_blocks_outlines_reveals_and_captions_draw_alike(bool background)
    {
        //Act
        byte[] skia = DrawSkia(background);
        byte[] noSkia = DrawNoSkia(background);

        //Assert
        ImageComparison.AssertSimilar(RawToPng(skia), RawToPng(noSkia), background ? "text over background" : "text over transparency");
    }
}
