using System.Collections.Generic;
using System.Linq;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.Drawing.TestSupport;
using SilverAssertions;
using Xunit;

//The test project aliases the SK names onto the Drawing workalike types (see
//  NoSkiaTypeAliases.cs), so the compiler's own intermediate types need their own names here
using ShimCanvas = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKCanvas;
using ShimPaint = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKPaint;
using ShimPath = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKPath;
using ShimPoint = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKPoint;
using ShimRect = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKRect;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// One test per <see cref="DrawingSvgWarningKind"/>: every way a document can render as
/// something other than what it asked for must say so, with the reason typed rather than
/// buried in a message. Two of the kinds guard display-list shapes the SVG compiler cannot
/// currently produce - a glyph-identifier run and a text-on-path run - so those are raised
/// from hand-built commands rather than from markup.
/// </summary>
public class DrawingSvgWarningTests
{
    private static List<DrawingSvgWarningKind> Convert(string markup, bool render, bool withFont = true)
    {
        using var svg = new DrawingSvg();
        if (withFont) { svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath); }
        svg.FromSvg(markup).Should().BeTrue();
        if (render) { svg.RasterizeToPng(1f); }
        return svg.Warnings.Select(warning => warning.Kind).ToList();
    }

    private static List<DrawingSvgWarning> ConvertCommands(System.Action<ShimCanvas> record)
    {
        var recorder = new SKPictureRecorder();
        ShimCanvas canvas = recorder.BeginRecording(ShimRect.Create(0f, 0f, 100f, 100f));
        record(canvas);

        var warnings = new List<DrawingSvgWarning>();
        var converter = new DrawingPictureConverter(new NoSkiaModel(), null)
        {
            Warning = (kind, message) => warnings.Add(new DrawingSvgWarning(kind, message)),
        };
        converter.Convert(recorder.EndRecording());
        return warnings;
    }

    [Fact]
    public void An_unsupported_filter_primitive_is_reported_once_the_document_is_rendered()
    {
        //Arrange + Act - feMorphology passes its input through unchanged
        List<DrawingSvgWarningKind> kinds = Convert(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""100"" height=""100"" viewBox=""0 0 100 100"">
  <defs><filter id=""f""><feMorphology operator=""dilate"" radius=""2""/></filter></defs>
  <rect x=""10"" y=""10"" width=""50"" height=""50"" fill=""red"" filter=""url(#f)""/>
</svg>", render: true);

        //Assert
        kinds.Should().Contain(DrawingSvgWarningKind.UnsupportedFilterPrimitive);
    }

    [Fact]
    public void A_filter_primitive_warning_waits_for_the_render_that_evaluates_it()
    {
        //Arrange + Act - the same document, loaded but never drawn
        List<DrawingSvgWarningKind> kinds = Convert(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""100"" height=""100"" viewBox=""0 0 100 100"">
  <defs><filter id=""f""><feMorphology operator=""dilate"" radius=""2""/></filter></defs>
  <rect x=""10"" y=""10"" width=""50"" height=""50"" fill=""red"" filter=""url(#f)""/>
</svg>", render: false);

        //Assert - a filter graph is built at load and evaluated at draw, so this is where
        //  the warning comes from; nothing is known about it before the first render
        kinds.Should().BeEmpty();
    }

    [Fact]
    public void A_turbulence_primitive_reports_that_it_was_dropped()
    {
        //Arrange + Act
        List<DrawingSvgWarningKind> kinds = Convert(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""100"" height=""100"" viewBox=""0 0 100 100"">
  <defs><filter id=""f""><feTurbulence baseFrequency=""0.05"" numOctaves=""2""/></filter></defs>
  <rect x=""10"" y=""10"" width=""50"" height=""50"" fill=""red"" filter=""url(#f)""/>
</svg>", render: true);

        //Assert
        kinds.Should().Contain(DrawingSvgWarningKind.TurbulenceDropped);
    }

    [Fact]
    public void A_pattern_fill_is_tiled_and_reports_nothing()
    {
        //Arrange + Act - patterns render as tiles, so a document using one degrades in no
        //  way and has nothing to report
        List<DrawingSvgWarningKind> kinds = Convert(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""200"" viewBox=""0 0 200 200"">
  <defs>
    <pattern id=""p"" width=""10"" height=""10"" patternUnits=""userSpaceOnUse"">
      <circle cx=""5"" cy=""5"" r=""4"" fill=""red""/>
    </pattern>
  </defs>
  <rect x=""10"" y=""10"" width=""100"" height=""100"" fill=""url(#p)""/>
</svg>", render: true);

        //Assert
        kinds.Should().BeEmpty();
    }

    [Fact]
    public void A_document_with_text_and_no_registered_fonts_says_so()
    {
        //Arrange + Act
        List<DrawingSvgWarningKind> kinds = Convert(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""60"" viewBox=""0 0 200 60"">
  <text x=""10"" y=""30"" font-size=""16"">Hello</text>
</svg>", render: false, withFont: false);

        //Assert
        kinds.Should().Contain(DrawingSvgWarningKind.NoFontsRegistered);
    }

    [Fact]
    public void A_document_with_no_text_never_asks_for_fonts()
    {
        //Arrange + Act
        List<DrawingSvgWarningKind> kinds = Convert(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""60"" height=""60"" viewBox=""0 0 60 60"">
  <rect x=""10"" y=""10"" width=""40"" height=""40"" fill=""red""/>
</svg>", render: true, withFont: false);

        //Assert
        kinds.Should().BeEmpty();
    }

    [Fact]
    public void A_glyph_identifier_run_is_reported_and_skipped()
    {
        //Arrange + Act - a run of glyph ids carries no characters to shape
        List<DrawingSvgWarning> warnings = ConvertCommands(canvas => canvas.DrawText(
            SKTextBlob.CreatePositionedGlyphs(new ushort[] { 3, 4 },
                new[] { new ShimPoint(0f, 0f), new ShimPoint(8f, 0f) }),
            0f, 0f, new ShimPaint()));

        //Assert
        warnings.Select(warning => warning.Kind).Should().Equal(
            new[] { DrawingSvgWarningKind.GlyphIdTextRunUnsupported });
    }

    [Fact]
    public void A_text_on_path_run_is_reported_and_recorded_without_being_drawn()
    {
        //Arrange
        var path = new ShimPath();
        path.MoveTo(0f, 0f);
        path.LineTo(50f, 0f);

        //Act
        List<DrawingSvgWarning> warnings = ConvertCommands(
            canvas => canvas.DrawTextOnPath("along", path, 0f, 0f, new ShimPaint()));

        //Assert
        warnings.Select(warning => warning.Kind).Should().Equal(
            new[] { DrawingSvgWarningKind.TextOnPathUnsupported });
    }

    [Fact]
    public void The_same_warning_is_reported_once_however_often_it_happens()
    {
        //Arrange - two elements sharing one unsupported filter, rendered twice
        using var svg = new DrawingSvg();
        svg.FromSvg(@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""100"" viewBox=""0 0 200 100"">
  <defs><filter id=""f""><feMorphology operator=""dilate"" radius=""2""/></filter></defs>
  <rect x=""10"" y=""10"" width=""50"" height=""50"" fill=""red"" filter=""url(#f)""/>
  <rect x=""100"" y=""10"" width=""50"" height=""50"" fill=""blue"" filter=""url(#f)""/>
</svg>").Should().BeTrue();

        //Act
        svg.RasterizeToPng(1f);
        svg.RasterizeToPng(2f);

        //Assert
        svg.Warnings.Should().HaveCount(1);
        svg.Warnings[0].Kind.Should().Be(DrawingSvgWarningKind.UnsupportedFilterPrimitive);
        svg.Warnings[0].Message.Should().Contain("feMorphology");
    }

    [Fact]
    public void Loading_another_document_starts_a_new_set_of_warnings()
    {
        //Arrange
        using var svg = new DrawingSvg();
        svg.FromSvg(@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""60"" viewBox=""0 0 200 60"">
  <text x=""10"" y=""30"" font-size=""16"">Hello</text>
</svg>").Should().BeTrue();
        svg.Warnings.Should().NotBeEmpty();

        //Act
        svg.FromSvg(@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""60"" height=""60"" viewBox=""0 0 60 60"">
  <rect x=""10"" y=""10"" width=""40"" height=""40"" fill=""red""/>
</svg>").Should().BeTrue();

        //Assert
        svg.Warnings.Should().BeEmpty();
    }
}
