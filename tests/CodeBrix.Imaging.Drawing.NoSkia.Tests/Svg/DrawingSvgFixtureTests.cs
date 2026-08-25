using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.SvgParse;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// Covers what a loaded document turns into, on documents small enough to reason about by
/// hand: where an anchored text run's origin lands, how nested transforms are recorded, what
/// a layer reports as its bounds, that an embedded image keeps the bytes it arrived as, and
/// that walking the display list with a visitor draws the same thing replaying it does.
/// Every fixture is inline markup, so nothing here depends on a reference image.
/// </summary>
public class DrawingSvgFixtureTests
{
    /// <summary>Flattens a picture's own commands into short names, for sequence assertions.</summary>
    private sealed class KindLister : IDrawingCommandVisitor
    {
        public List<string> Kinds { get; } = new List<string>();

        public void Visit(SaveCommand command) => Kinds.Add("Save");

        public void Visit(RestoreCommand command) => Kinds.Add("Restore");

        public void Visit(SaveLayerCommand command) => Kinds.Add("SaveLayer");

        public void Visit(SetMatrixCommand command) => Kinds.Add("SetMatrix");

        public void Visit(ClipRectCommand command) => Kinds.Add("ClipRect");

        public void Visit(ClipPathCommand command) => Kinds.Add("ClipPath");

        public void Visit(DrawPathCommand command) => Kinds.Add("DrawPath");

        public void Visit(DrawImageCommand command) => Kinds.Add("DrawImage");

        public void Visit(DrawPictureCommand command) => Kinds.Add("DrawPicture");

        public void Visit(DrawTextCommand command) => Kinds.Add("DrawText");

        public void Visit(DrawPositionedTextCommand command) => Kinds.Add("DrawPositionedText");
    }

    /// <summary>Collects commands of one kind from a picture and everything nested in it.</summary>
    private sealed class Collector : IDrawingCommandVisitor
    {
        public List<DrawingCommand> Commands { get; } = new List<DrawingCommand>();

        public void Visit(SaveLayerCommand command) => Commands.Add(command);

        public void Visit(SetMatrixCommand command) => Commands.Add(command);

        public void Visit(DrawPathCommand command) => Commands.Add(command);

        public void Visit(DrawImageCommand command) => Commands.Add(command);

        public void Visit(DrawTextCommand command) => Commands.Add(command);

        public void Visit(DrawPositionedTextCommand command) => Commands.Add(command);

        public void Visit(DrawPictureCommand command) => command.Picture?.Accept(this);

        public IEnumerable<T> OfKind<T>() where T : DrawingCommand => Commands.OfType<T>();
    }

    /// <summary>
    /// Re-emits a display list onto a canvas through the visitor interface alone - what a
    /// consumer writes when it wants to see every command rather than just the pixels.
    /// </summary>
    private sealed class ReplayingVisitor : IDrawingCommandVisitor
    {
        private readonly DrawingCanvas _canvas;

        public ReplayingVisitor(DrawingCanvas canvas)
        {
            _canvas = canvas;
        }

        public void Visit(SaveCommand command) => _canvas.Save();

        public void Visit(RestoreCommand command) => _canvas.Restore();

        public void Visit(SaveLayerCommand command)
        {
            if (command.Paint != null) { _canvas.SaveLayer(command.Paint); }
            else { _canvas.SaveLayer(); }
        }

        public void Visit(SetMatrixCommand command) => _canvas.Concat(command.Delta);

        public void Visit(ClipRectCommand command)
            => _canvas.ClipRect(command.Rect, command.Operation, command.Antialias);

        public void Visit(ClipPathCommand command)
        {
            if (command.Path != null) { _canvas.ClipPath(command.Path, command.Operation, command.Antialias); }
        }

        public void Visit(DrawPathCommand command)
        {
            if (command.Path != null && command.Paint != null) { _canvas.DrawPath(command.Path, command.Paint); }
        }

        public void Visit(DrawImageCommand command)
        {
            if (command.Image != null)
            {
                _canvas.DrawBitmap(command.Image, command.Source, command.Dest, command.Sampling, command.Paint);
            }
        }

        public void Visit(DrawPictureCommand command) => command.Picture?.Accept(this);

        public void Visit(DrawTextCommand command) => Fill(command.GetOutline(), command.Paint);

        public void Visit(DrawPositionedTextCommand command)
        {
            foreach (DrawingPath outline in command.GetOutlines()) { Fill(outline, command.Paint); }
        }

        private void Fill(DrawingPath outline, DrawingPaint paint)
        {
            if (outline == null || outline.IsEmpty || paint == null) { return; }
            _canvas.DrawPath(outline, paint);
        }
    }

    private static DrawingSvg LoadMarkup(string markup, bool withFont = true)
    {
        var svg = new DrawingSvg();
        if (withFont) { svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath); }
        svg.FromSvg(markup).Should().BeTrue();
        return svg;
    }

    private static List<string> ListKinds(DrawingPicture picture)
    {
        var lister = new KindLister();
        picture.Accept(lister);
        return lister.Kinds;
    }

    private static Collector CollectFrom(DrawingPicture picture)
    {
        var collector = new Collector();
        picture.Accept(collector);
        return collector;
    }

    [Fact]
    public void Text_anchoring_is_resolved_into_each_run_origin()
    {
        //Arrange - one x, three anchors: the compiler resolves the anchor into the origin,
        //  so every recorded run is left-aligned and only its X differs
        using DrawingSvg svg = LoadMarkup(@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""800"" height=""120"" viewBox=""0 0 800 120"">
  <text x=""400"" y=""40"" font-family=""Open Sans"" font-size=""16"" text-anchor=""middle"">Middle</text>
  <text x=""400"" y=""70"" font-family=""Open Sans"" font-size=""16"" text-anchor=""end"">End</text>
  <text x=""400"" y=""100"" font-family=""Open Sans"" font-size=""16"" text-anchor=""start"">Start</text>
</svg>");

        //Act
        List<DrawTextCommand> runs = CollectFrom(svg.Picture).OfKind<DrawTextCommand>().ToList();

        //Assert
        runs.Should().HaveCount(3);
        runs.Select(run => run.Text).Should().Equal(new[] { "Middle", "End", "Start" });
        runs.Select(run => run.Style.Align).Should().AllBeEquivalentTo(DrawingTextAlign.Left);
        runs.Select(run => run.X).Should().Equal(new[] { 374f, 371f, 400f });
        runs.Select(run => run.Y).Should().Equal(new[] { 40f, 70f, 100f });
        runs.Select(run => run.Style.FamilyName).Should().AllBeEquivalentTo("Open Sans");
        runs.Select(run => run.Style.Size).Should().AllBeEquivalentTo(16f);
    }

    [Fact]
    public void A_run_emitted_as_positioned_glyphs_keeps_the_runs_origin_and_renders_the_same()
    {
        //Arrange
        const string Markup = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""800"" height=""120"" viewBox=""0 0 800 120"">
  <text x=""400"" y=""40"" font-family=""Open Sans"" font-size=""16"" text-anchor=""middle"">Middle</text>
</svg>";
        using DrawingSvg asRuns = LoadMarkup(Markup);
        using var asGlyphs = new DrawingSvg { TextEmission = DrawingSvgTextEmission.PositionedGlyphs };
        asGlyphs.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        asGlyphs.FromSvg(Markup).Should().BeTrue();

        //Act
        DrawPositionedTextCommand positioned =
            CollectFrom(asGlyphs.Picture).OfKind<DrawPositionedTextCommand>().Single();
        DrawTextCommand run = CollectFrom(asRuns.Picture).OfKind<DrawTextCommand>().Single();

        //Assert - one position per code point, the first at the run's own origin, and the
        //  same anchored origin the whole-run form carries
        positioned.Text.Should().Be("Middle");
        positioned.Positions.Should().HaveCount(6);
        positioned.Positions[0].X.Should().Be(0f);
        positioned.X.Should().Be(run.X);
        positioned.Y.Should().Be(run.Y);
        positioned.Style.Align.Should().Be(DrawingTextAlign.Left);
        positioned.Positions.Select(position => position.X).Should().BeInAscendingOrder();

        ImageComparison.AssertSimilar(asRuns.RasterizeToPng(2f), asGlyphs.RasterizeToPng(2f),
            "the same run emitted as whole-run text and as positioned glyphs", 4.0, 0.08);
    }

    [Fact]
    public void A_restored_transform_is_not_re_declared_for_the_next_sibling()
    {
        //Arrange - a scaled group inside a translated group, then a sibling of the scaled
        //  group: the sibling must draw under the translation the Restore brought back
        using DrawingSvg svg = LoadMarkup(@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""200"" viewBox=""0 0 200 200"">
  <g transform=""translate(20,30)"">
    <g transform=""scale(2)""><rect x=""0"" y=""0"" width=""10"" height=""10"" fill=""red""/></g>
    <rect x=""0"" y=""0"" width=""5"" height=""5"" fill=""blue""/>
  </g>
</svg>");

        //Act
        List<string> kinds = ListKinds(svg.Picture);
        List<SetMatrixCommand> matrices = CollectFrom(svg.Picture).OfKind<SetMatrixCommand>().ToList();

        //Assert - two transforms recorded, and nothing between the Restore and the sibling
        kinds.Should().Equal(new[]
        {
            "Save", "Save", "SetMatrix", "Save", "SetMatrix", "Save", "DrawPicture", "Restore", "Restore",
            "Save", "DrawPicture", "Restore", "Restore", "Restore",
        });
        matrices.Should().HaveCount(2);
        matrices[0].Delta.Should().Be(System.Numerics.Matrix3x2.CreateTranslation(20f, 30f));
        matrices[1].Delta.Should().Be(System.Numerics.Matrix3x2.CreateScale(2f));

        //  the delta is what replay concatenates; the total is where the recorder stood
        matrices[1].Total.Should().Be(
            System.Numerics.Matrix3x2.CreateScale(2f) * System.Numerics.Matrix3x2.CreateTranslation(20f, 30f));
    }

    [Fact]
    public void A_declared_size_in_millimeters_is_reported_beside_the_pixel_bounds()
    {
        //Arrange
        using DrawingSvg svg = LoadMarkup(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""80mm"" height=""30mm"" viewBox=""0 0 80 30"">
  <rect x=""0"" y=""0"" width=""80"" height=""30"" fill=""red""/>
</svg>");

        //Act + Assert - the document's own units, and the same size as CSS pixels at 96 DPI
        svg.DeclaredWidth.Type.Should().Be(SvgUnitType.Millimeter);
        svg.DeclaredWidth.Value.Should().Be(80f);
        svg.DeclaredHeight.Type.Should().Be(SvgUnitType.Millimeter);
        svg.DeclaredHeight.Value.Should().Be(30f);
        svg.Bounds.Width.Should().Be(302f);
        svg.Bounds.Height.Should().Be(113f);
    }

    [Fact]
    public void A_group_opacity_layer_reports_the_area_it_draws_into()
    {
        //Arrange
        using DrawingSvg svg = LoadMarkup(
            @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""200"" viewBox=""0 0 200 200"">
  <g opacity=""0.5""><rect x=""20"" y=""30"" width=""40"" height=""50"" fill=""red""/></g>
</svg>");

        //Act
        SaveLayerCommand layer = CollectFrom(svg.Picture).OfKind<SaveLayerCommand>().Single();

        //Assert - the layer composites at half alpha, and reports the area its content
        //  claims: the group's compiled sub-picture, whose cull rectangle starts at the
        //  origin, so the hint over-covers the rectangle it actually paints
        layer.Paint.Color.Alpha.Should().Be(128);
        layer.Bounds.Should().NotBeNull();
        layer.Bounds.Value.Should().Be(new DrawingRect(0f, 0f, 60f, 80f));
    }

    [Fact]
    public void An_embedded_image_keeps_the_bytes_it_arrived_as()
    {
        //Arrange - a real JPEG, embedded as a data URI
        byte[] jpeg = BuildJpeg(8, 6);
        using DrawingSvg svg = LoadMarkup(
            $@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink""
     width=""100"" height=""100"" viewBox=""0 0 100 100"">
  <image x=""10"" y=""20"" width=""40"" height=""30""
         xlink:href=""data:image/jpeg;base64,{Convert.ToBase64String(jpeg)}""/>
</svg>");

        //Act
        DrawImageCommand image = CollectFrom(svg.Picture).OfKind<DrawImageCommand>().Single();

        //Assert - the decoded pixels for drawing, AND the original file for a consumer that
        //  can embed it (a PDF writer) instead of re-encoding the raster
        image.EncodedFormat.Should().Be(DrawingEncodedImageFormat.Jpeg);
        image.EncodedData.Should().Equal(jpeg);
        image.Image.Should().NotBeNull();
        image.Image.Width.Should().Be(8);
        image.Image.Height.Should().Be(6);
        image.Dest.Should().Be(DrawingRect.Create(10f, 20f, 40f, 30f));
    }

    [Fact]
    public void A_visitor_that_re_emits_the_commands_draws_what_replaying_them_draws()
    {
        //Arrange - a sample with text, transforms, clips, and layers in it
        using var svg = new DrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        svg.Load(SvgTestAssets.GetSvgPath("text-basic")).Should().BeTrue();

        //Act
        using DrawingBitmap replayed = Render(svg, canvas => canvas.DrawPicture(svg.Picture));
        using DrawingBitmap visited = Render(svg, canvas => svg.Picture.Accept(new ReplayingVisitor(canvas)));

        //Assert - the visitor sees everything the canvas acts on, so the two agree exactly
        visited.Bytes.Should().Equal(replayed.Bytes);
    }

    private static DrawingBitmap Render(DrawingSvg svg, Action<DrawingCanvas> draw)
    {
        DrawingRect bounds = svg.Bounds;
        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            (int)MathF.Ceiling(bounds.Width), (int)MathF.Ceiling(bounds.Height),
            DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        using var canvas = new DrawingCanvas(bitmap);
        canvas.Translate(-bounds.Left, -bounds.Top);
        draw(canvas);
        return bitmap;
    }

    private static byte[] BuildJpeg(int width, int height)
    {
        using var bitmap = new DrawingBitmap(new DrawingImageInfo(
            width, height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, ((x + y) % 2) == 0 ? DrawingColors.Red : DrawingColors.Blue);
            }
        }

        using DrawingImage image = DrawingImage.FromBitmap(bitmap);
        using DrawingData data = image.Encode(DrawingEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }
}
