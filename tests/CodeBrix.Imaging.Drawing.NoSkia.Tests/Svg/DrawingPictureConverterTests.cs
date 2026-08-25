using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model.Services;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.SvgParse;
using SilverAssertions;
using Xunit;

//The test project aliases the SK names onto the Drawing workalike types (see
//  NoSkiaTypeAliases.cs), so the compiler's own intermediate types need their own names here
using ShimCanvas = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKCanvas;
using ShimPaint = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKPaint;
using ShimPath = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKPath;
using ShimRect = CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.SKRect;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// Proves that the Drawing-named display list a document loads into IS the compiled
/// document - that converting the compiler's own display list and replaying the conversion
/// produces exactly the pixels replaying the compiler's list produces, command for command,
/// for every sample SVG in the repository.
/// <para>
/// This is deliberately the one test file that drives both paths, so it names the compiler's
/// intermediate types on purpose; every other test reaches the renderer through
/// <see cref="DrawingSvg"/> alone. When those intermediate types are made internal, this
/// file is the reason the test project needs <c>InternalsVisibleTo</c>.
/// </para>
/// </summary>
public class DrawingPictureConverterTests
{
    /// <summary>The sample names, as xUnit theory data.</summary>
    public static IEnumerable<object[]> SampleNames()
    {
        foreach (string name in SvgTestAssets.GetSampleNames())
        {
            yield return new object[] { name };
        }
    }

    /// <summary>Counts the commands a converted picture holds, nested pictures included.</summary>
    private sealed class CommandCounter : IDrawingCommandVisitor
    {
        public int Count { get; private set; }

        public void Visit(ClipPathCommand command) => Count++;

        public void Visit(ClipRectCommand command) => Count++;

        public void Visit(DrawImageCommand command) => Count++;

        public void Visit(DrawPathCommand command) => Count++;

        public void Visit(DrawTextCommand command) => Count++;

        public void Visit(DrawPositionedTextCommand command) => Count++;

        public void Visit(DrawTextOnPathCommand command) => Count++;

        public void Visit(SaveCommand command) => Count++;

        public void Visit(RestoreCommand command) => Count++;

        public void Visit(SaveLayerCommand command) => Count++;

        public void Visit(SetMatrixCommand command) => Count++;

        public void Visit(DrawPictureCommand command)
        {
            Count++;
            command.Picture?.Accept(this);
        }
    }

    private static int CountShimCommands(SKPicture picture)
    {
        var count = 0;
        if (picture?.Commands == null) { return count; }

        foreach (CanvasCommand command in picture.Commands)
        {
            count++;
            if (command is DrawPictureCanvasCommand drawPicture)
            {
                count += CountShimCommands(drawPicture.Picture);
            }
        }
        return count;
    }

    private static SKPicture CompileShimPicture(string sampleName, out NoSkiaModel model)
    {
        var fonts = new NoSkiaFontRegistry();
        fonts.RegisterFont(SvgTestAssets.TestFontPath);
        var assetLoader = new ImagingSvgAssetLoader(fonts);

        SvgDocument document = SvgService.Open(SvgTestAssets.GetSvgPath(sampleName));
        SKPicture picture = SvgSceneRuntime.CreateModel(document, assetLoader);

        var textRenderer = new NoSkiaTextRenderer(assetLoader);
        var filterFactory = new NoSkiaImageFilterFactory();
        model = new NoSkiaModel(textRenderer, filterFactory);
        textRenderer.Model = model;
        filterFactory.Model = model;

        //A pattern fill's tile is a compiled picture in its own right, so the replay path
        //  needs the same conversion the facade wires up - without it a pattern would
        //  degrade here and tile there, and the two paths could not be compared
        var tileConverter = new DrawingPictureConverter(model, new ImagingTextOutliner(assetLoader, model));
        model.ConvertPicture = tileConverter.Convert;
        return picture;
    }

    private static DrawingBitmap Rasterize(DrawingRect bounds, float scale, Action<DrawingCanvas> draw)
    {
        int width = Math.Max(1, (int)MathF.Ceiling(bounds.Width * scale));
        int height = Math.Max(1, (int)MathF.Ceiling(bounds.Height * scale));

        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            width, height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        using var canvas = new DrawingCanvas(bitmap);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        draw(canvas);
        return bitmap;
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void The_converted_picture_replays_to_the_same_pixels_as_the_compiled_one(string sampleName)
    {
        //Arrange
        SKPicture shimPicture = CompileShimPicture(sampleName, out NoSkiaModel model);
        shimPicture.Should().NotBeNull();

        using var svg = new DrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        svg.Load(SvgTestAssets.GetSvgPath(sampleName)).Should().BeTrue();

        //Act
        using DrawingBitmap fromShim = Rasterize(
            model.ToDrawingRect(shimPicture.CullRect), SvgTestAssets.RasterScale,
            canvas => model.Draw(shimPicture, canvas));
        using DrawingBitmap fromPicture = svg.RasterizeToBitmap(SvgTestAssets.RasterScale);

        //Assert - the same engine drawing the same commands: not similar, identical
        fromPicture.Width.Should().Be(fromShim.Width);
        fromPicture.Height.Should().Be(fromShim.Height);
        fromPicture.Bytes.Should().Equal(fromShim.Bytes,
            $"the converted display list for '{sampleName}' must replay to the compiled one's pixels");
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void The_converted_picture_holds_one_command_per_compiled_command(string sampleName)
    {
        //Arrange
        SKPicture shimPicture = CompileShimPicture(sampleName, out NoSkiaModel _);
        using var svg = new DrawingSvg();
        svg.Fonts.RegisterFont(SvgTestAssets.TestFontPath);
        svg.Load(SvgTestAssets.GetSvgPath(sampleName)).Should().BeTrue();

        //Act
        var counter = new CommandCounter();
        svg.Picture.Accept(counter);

        //Assert
        counter.Count.Should().Be(CountShimCommands(shimPicture),
            $"the conversion of '{sampleName}' must be one command in, one command out");
    }

    [Fact]
    public void One_compiled_picture_converts_to_one_drawing_picture()
    {
        //Arrange - a compiled picture drawn twice, exactly what a use element produces
        var recorder = new SKPictureRecorder();
        ShimCanvas innerCanvas = recorder.BeginRecording(ShimRect.Create(0f, 0f, 10f, 10f));
        innerCanvas.DrawPath(new ShimPath(), new ShimPaint());
        SKPicture inner = recorder.EndRecording();

        ShimCanvas outerCanvas = recorder.BeginRecording(ShimRect.Create(0f, 0f, 20f, 20f));
        outerCanvas.DrawPicture(inner);
        outerCanvas.DrawPicture(inner);
        SKPicture outer = recorder.EndRecording();

        var converter = new DrawingPictureConverter(new NoSkiaModel(), null);

        //Act
        DrawingPicture converted = converter.Convert(outer);

        //Assert - both draws point at one converted instance, so a consumer that caches per
        //  picture emits the shared content once
        converted.Commands.Should().HaveCount(2);
        var first = (DrawPictureCommand)converted.Commands[0];
        var second = (DrawPictureCommand)converted.Commands[1];
        ReferenceEquals(first.Picture, second.Picture).Should().BeTrue();
        ReferenceEquals(converter.Convert(inner), first.Picture).Should().BeTrue(
            "converting the same compiled picture again must reuse the conversion");
    }
}
