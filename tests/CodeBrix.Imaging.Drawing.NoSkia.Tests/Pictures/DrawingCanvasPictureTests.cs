using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Pictures;

/// <summary>
/// Covers <see cref="DrawingCanvas.DrawPicture"/>: replaying a recorded display list must
/// produce exactly the pixels the same canvas calls produce by hand, for every command
/// kind - transforms, clips, layers, paths, images, nested pictures, and text.
/// </summary>
public class DrawingCanvasPictureTests
{
    private static readonly DrawingImageInfo CanvasInfo =
        new DrawingImageInfo(120, 90, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);

    //An outliner that answers with a fixed square at the requested origin, so text replay
    //  can be verified without dragging a font stack into the core assembly's tests
    private sealed class SquareOutliner : IDrawingTextOutliner
    {
        private readonly float _size;

        public SquareOutliner(float size)
        {
            _size = size;
        }

        public DrawingPath GetOutline(string text, DrawingTextStyle style, DrawingPoint origin)
        {
            if (String.IsNullOrEmpty(text)) { return null; }

            var builder = new DrawingPathBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                builder.AddRect(DrawingRect.Create(origin.X + (i * _size), origin.Y, _size, _size));
            }
            return builder.Detach();
        }
    }

    private static DrawingPath BuildRect(float x, float y, float width, float height)
    {
        var builder = new DrawingPathBuilder();
        builder.AddRect(DrawingRect.Create(x, y, width, height));
        return builder.Detach();
    }

    private static DrawingBitmap BuildStripes(int width, int height)
    {
        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            width, height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, ((x + y) % 2) == 0 ? DrawingColors.Red : DrawingColors.Blue);
            }
        }
        return bitmap;
    }

    private static DrawingBitmap Render(Action<DrawingCanvas> draw)
    {
        var bitmap = new DrawingBitmap(CanvasInfo);
        using var canvas = new DrawingCanvas(bitmap);
        canvas.Clear(DrawingColors.White);
        draw(canvas);
        return bitmap;
    }

    private static void AssertIdentical(DrawingBitmap expected, DrawingBitmap actual, string label)
    {
        actual.Width.Should().Be(expected.Width);
        actual.Height.Should().Be(expected.Height);
        actual.Bytes.Should().Equal(expected.Bytes, $"the replayed picture must match {label} pixel for pixel");
    }

    [Fact]
    public void Replaying_a_picture_matches_the_same_calls_made_directly()
    {
        //Arrange
        DrawingPath backdrop = BuildRect(5f, 5f, 60f, 40f);
        DrawingPath inner = BuildRect(0f, 0f, 30f, 30f);
        DrawingPath nestedPath = BuildRect(2f, 2f, 12f, 12f);
        using DrawingBitmap stripes = BuildStripes(8, 8);

        var backdropPaint = new DrawingPaint { Color = DrawingColors.Green, IsAntialias = true };
        var innerPaint = new DrawingPaint
        {
            Color = DrawingColors.Blue,
            Style = DrawingPaintStyle.Stroke,
            StrokeWidth = 3f,
            IsAntialias = true,
        };
        var layerPaint = new DrawingPaint { Color = DrawingColors.White.WithAlpha(140) };
        var nestedPaint = new DrawingPaint { Color = DrawingColors.Magenta, IsAntialias = true };
        var imagePaint = new DrawingPaint { Color = DrawingColors.White.WithAlpha(200) };
        var sampling = new DrawingSamplingOptions(DrawingFilterMode.Linear);

        Matrix3x2 translate = Matrix3x2.CreateTranslation(20f, 15f);
        Matrix3x2 scale = Matrix3x2.CreateScale(1.5f);

        var nested = new DrawingPicture(DrawingRect.Create(0, 0, 20, 20), new DrawingCommand[]
        {
            new SaveCommand(0),
            new SetMatrixCommand(scale, scale),
            new DrawPathCommand(nestedPath, nestedPaint),
            new RestoreCommand(0),
        });

        var picture = new DrawingPicture(DrawingRect.Create(0, 0, 120, 90), new DrawingCommand[]
        {
            new SaveCommand(0),
            new ClipRectCommand(DrawingRect.Create(2, 2, 100, 70), DrawingClipOperation.Intersect, true),
            new DrawPathCommand(backdrop, backdropPaint),
            new SetMatrixCommand(translate, translate),
            new SaveLayerCommand(1, layerPaint, DrawingRect.Create(0, 0, 30, 30)),
            new ClipPathCommand(inner, DrawingClipOperation.Intersect, false),
            new DrawPathCommand(inner, innerPaint),
            new RestoreCommand(1),
            new DrawImageCommand(stripes, DrawingRect.Create(0, 0, 8, 8),
                DrawingRect.Create(40, 10, 24, 24), imagePaint, sampling, null, null),
            new DrawPictureCommand(nested),
            new RestoreCommand(0),
        });

        //Act
        using DrawingBitmap byHand = Render(canvas =>
        {
            canvas.Save();
            canvas.ClipRect(DrawingRect.Create(2, 2, 100, 70), DrawingClipOperation.Intersect, true);
            canvas.DrawPath(backdrop, backdropPaint);
            canvas.Concat(translate);
            canvas.SaveLayer(layerPaint);
            canvas.ClipPath(inner, DrawingClipOperation.Intersect, false);
            canvas.DrawPath(inner, innerPaint);
            canvas.Restore();
            canvas.DrawBitmap(stripes, DrawingRect.Create(0, 0, 8, 8),
                DrawingRect.Create(40, 10, 24, 24), sampling, imagePaint);
            canvas.Save();
            canvas.Concat(scale);
            canvas.DrawPath(nestedPath, nestedPaint);
            canvas.Restore();
            canvas.Restore();
        });
        using DrawingBitmap replayed = Render(canvas => canvas.DrawPicture(picture));

        //Assert
        AssertIdentical(byHand, replayed, "the same calls made by hand");
    }

    [Fact]
    public void Replay_honors_the_transform_the_canvas_already_carries()
    {
        //Arrange
        DrawingPath square = BuildRect(0f, 0f, 20f, 20f);
        var paint = new DrawingPaint { Color = DrawingColors.Red, IsAntialias = true };
        Matrix3x2 delta = Matrix3x2.CreateTranslation(10f, 10f);
        var picture = new DrawingPicture(DrawingRect.Create(0, 0, 40, 40), new DrawingCommand[]
        {
            new SetMatrixCommand(delta, delta),
            new DrawPathCommand(square, paint),
        });

        //Act
        using DrawingBitmap byHand = Render(canvas =>
        {
            canvas.Scale(2f);
            canvas.Concat(delta);
            canvas.DrawPath(square, paint);
        });
        using DrawingBitmap replayed = Render(canvas =>
        {
            canvas.Scale(2f);
            canvas.DrawPicture(picture);
        });

        //Assert - the recorded delta concatenates onto the canvas scale rather than replacing it
        AssertIdentical(byHand, replayed, "the pre-scaled canvas");
    }

    [Fact]
    public void Text_commands_replay_as_their_outline()
    {
        //Arrange
        var outliner = new SquareOutliner(6f);
        var style = new DrawingTextStyle("Test", "Test", DrawingFontWeight.Normal,
            DrawingFontWidth.Normal, DrawingFontSlant.Upright, 12f, DrawingTextAlign.Left);
        var paint = new DrawingPaint { Color = DrawingColors.Black, IsAntialias = true };
        var picture = new DrawingPicture(DrawingRect.Create(0, 0, 120, 90), new DrawingCommand[]
        {
            new DrawTextCommand("abc", 10f, 20f, paint, style, outliner),
        });

        //Act
        using DrawingPath outline = outliner.GetOutline("abc", style, new DrawingPoint(10f, 20f));
        using DrawingBitmap byHand = Render(canvas => canvas.DrawPath(outline, paint));
        using DrawingBitmap replayed = Render(canvas => canvas.DrawPicture(picture));

        //Assert
        AssertIdentical(byHand, replayed, "the run's outline drawn as a path");
    }

    [Fact]
    public void Positioned_text_replays_each_code_point_separately()
    {
        //Arrange
        var outliner = new SquareOutliner(6f);
        var style = new DrawingTextStyle("Test", "Test", DrawingFontWeight.Normal,
            DrawingFontWidth.Normal, DrawingFontSlant.Upright, 12f, DrawingTextAlign.Left);
        var paint = new DrawingPaint { Color = DrawingColors.Black, IsAntialias = true };
        var positions = new[] { new DrawingPoint(0f, 0f), new DrawingPoint(9f, 4f), new DrawingPoint(18f, 8f) };
        var command = new DrawPositionedTextCommand("abc", positions, 10f, 20f, paint, style, outliner);
        var picture = new DrawingPicture(DrawingRect.Create(0, 0, 120, 90), new DrawingCommand[] { command });

        //Act
        IReadOnlyList<DrawingPath> outlines = command.GetOutlines();
        using DrawingBitmap byHand = Render(canvas =>
        {
            foreach (DrawingPath outline in outlines)
            {
                canvas.DrawPath(outline, paint);
            }
        });
        using DrawingBitmap replayed = Render(canvas => canvas.DrawPicture(picture));

        //Assert
        outlines.Count.Should().Be(3);
        AssertIdentical(byHand, replayed, "each code point filled on its own");
    }

    [Fact]
    public void Text_on_path_is_skipped_without_drawing_or_throwing()
    {
        //Arrange
        var style = new DrawingTextStyle("Test", "Test", DrawingFontWeight.Normal,
            DrawingFontWidth.Normal, DrawingFontSlant.Upright, 12f, DrawingTextAlign.Left);
        var command = new DrawTextOnPathCommand("abc", BuildRect(0f, 0f, 40f, 40f), 0f, 0f,
            new DrawingPaint { Color = DrawingColors.Black }, style, new SquareOutliner(6f));
        var picture = new DrawingPicture(DrawingRect.Create(0, 0, 120, 90),
            new DrawingCommand[] { command });

        //Act
        using DrawingBitmap blank = Render(canvas => { });
        using DrawingBitmap replayed = Render(canvas => canvas.DrawPicture(picture));

        //Assert
        command.GetOutline().Should().BeNull();
        AssertIdentical(blank, replayed, "an untouched canvas");
    }

    [Fact]
    public void Drawing_a_null_picture_is_ignored()
    {
        //Arrange + Act
        using DrawingBitmap blank = Render(canvas => { });
        using DrawingBitmap afterNull = Render(canvas => canvas.DrawPicture(null));

        //Assert
        AssertIdentical(blank, afterNull, "an untouched canvas");
    }
}
