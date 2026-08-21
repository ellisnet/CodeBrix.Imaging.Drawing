using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using Xunit;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Renders identical drawing sessions through the SkiaSharp backend and the fully managed
/// NoSkia backend and asserts the exported images are near-identical. Each scenario is
/// built once (against <see cref="ISessionAdapter"/>) and executed against both backends.
/// </summary>
public class SessionParityTests
{
    private static (byte[] Skia, byte[] NoSkia) RenderBoth(
        Action<ISessionAdapter> build, Func<ISessionAdapter, byte[]> export)
    {
        using var skiaAdapter = new SkiaSessionAdapter();
        using var noSkiaAdapter = new NoSkiaSessionAdapter();
        build(skiaAdapter);
        build(noSkiaAdapter);
        return (export(skiaAdapter), export(noSkiaAdapter));
    }

    private static byte[] CreateBackgroundPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = new Rgba32(
                    (byte)(x * 255 / width),
                    (byte)(y * 255 / height),
                    128, 255);
                if (x > width / 3 && x < (2 * width) / 3 && y > height / 4 && y < (3 * height) / 4)
                {
                    pixel = new Rgba32(240, 240, 240, 255);
                }
                image[x, y] = pixel;
            }
        }

        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static void BuildHighlighterScenario(ISessionAdapter session)
    {
        session.AddLayer("Pain", Color.FromRgb(255, 30, 230));
        session.AddLayer("Numbness", Color.FromRgb(30, 128, 204));
        session.SetBackgroundFillColor(Color.White);
        session.SetStrokeWidth(40f);

        session.StrokeThrough(new (float, float)[] { (0.1f, 0.1f), (0.5f, 0.3f), (0.9f, 0.15f) });
        session.StrokeThrough(new (float, float)[] { (0.2f, 0.2f), (0.5f, 0.3f), (0.7f, 0.5f) }); //overlaps the first
        session.StrokeThrough(new (float, float)[] { (0.5f, 0.5f) }); //a single-point dot

        session.SetActiveLayer("Numbness");
        session.SetStrokeWidth(25f);
        session.StrokeThrough(new (float, float)[] { (0.1f, 0.8f), (0.4f, 0.6f), (0.8f, 0.85f), (0.9f, 0.6f) });
    }

    private static void BuildShapeCatalogScenario(ISessionAdapter session)
    {
        session.AddLayer("Shapes", Color.FromRgb(200, 30, 30));
        session.SetLayerOpacity(255);
        session.SetBackgroundFillColor(Color.White);

        session.DrawLine(50, 80, 950, 120, thickness: 18);
        session.DrawArrow(120, 850, 420, 500, thickness: 22);
        session.DrawCircle(250, 300, 140, thickness: 16);
        session.DrawCircle(700, 250, 90, thickness: 16, color: Color.FromRgb(30, 30, 220), filled: true);
        session.DrawEllipse(650, 650, 220, 110, thickness: 20);
        session.DrawRectangle(80, 380, 240, 180, thickness: 14, color: Color.FromRgb(20, 150, 60));
        session.DrawRectangle(500, 800, 320, 150, thickness: 14, filled: true, cornerRadius: 40);
        session.DrawPolyline(
            new (float, float)[] { (520, 60), (620, 260), (420, 130), (720, 130), (470, 260) },
            thickness: 12, closed: true);
    }

    [Fact]
    public void Freehand_highlighter_strokes_match()
    {
        //Arrange + Act
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            BuildHighlighterScenario,
            session => session.ExportPng(new Size(500, 500)));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "highlighter strokes");
    }

    [Fact]
    public void Shape_catalog_matches()
    {
        //Arrange + Act
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            BuildShapeCatalogScenario,
            session => session.ExportPng(new Size(600, 600)));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "shape catalog");
    }

    [Fact]
    public void Translucent_shapes_over_white_match()
    {
        //Arrange + Act
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            session =>
            {
                session.AddLayer("Ink", Color.FromRgb(255, 30, 230));
                session.SetBackgroundFillColor(Color.White);
                session.DrawCircle(400, 400, 250, thickness: 60);
                session.DrawCircle(600, 500, 250, thickness: 60); //overlapping translucent rims
                session.DrawRectangle(200, 650, 500, 250, thickness: 30, filled: true);
            },
            session => session.ExportPng(new Size(400, 400)));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "translucent shapes");
    }

    [Fact]
    public void Background_image_with_strokes_matches()
    {
        //Arrange
        byte[] background = CreateBackgroundPng(400, 300);

        //Act
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            session =>
            {
                session.AddLayer("Notes", Color.FromRgb(240, 200, 20));
                session.SetBackgroundImage(background);
                session.SetStrokeWidth(35f);
                session.StrokeThrough(new (float, float)[] { (0.15f, 0.2f), (0.5f, 0.5f), (0.85f, 0.3f) });
                session.DrawArrow(200, 700, 500, 480, thickness: 20);
            },
            session => session.ExportPng(new Size(400, 300)));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "background image with strokes");
    }

    [Fact]
    public void Background_image_scaled_export_matches()
    {
        //Arrange - exporting at a size other than the background's exercises the resampling path
        byte[] background = CreateBackgroundPng(400, 300);

        //Act
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            session =>
            {
                session.AddLayer("Notes", Color.FromRgb(20, 20, 210));
                session.SetBackgroundImage(background);
                session.StrokeThrough(new (float, float)[] { (0.1f, 0.9f), (0.9f, 0.1f) });
            },
            session => session.ExportPng(new Size(200, 150)));

        //Assert - resamplers differ slightly more than rasterizers, so allow a wider margin
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "scaled background export",
            maxMeanDelta: 2.0, maxFractionNoticeable: 0.05);
    }

    [Fact]
    public void Transparent_export_matches()
    {
        //Arrange + Act - no background: the layers composite over transparency
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            BuildHighlighterScenario,
            session => session.ExportPng(new Size(500, 500), includeBackground: false));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "transparent export");
    }

    [Fact]
    public void Opaque_ink_matches()
    {
        //Arrange + Act
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            session =>
            {
                session.AddLayer("Marker", Color.FromRgb(10, 10, 10));
                session.SetLayerOpacity(255);
                session.SetBackgroundFillColor(Color.White);
                session.SetStrokeWidth(30f);
                session.StrokeThrough(new (float, float)[]
                {
                    (0.1f, 0.5f), (0.3f, 0.2f), (0.5f, 0.8f), (0.7f, 0.2f), (0.9f, 0.5f), //sharp zigzag joins
                });
            },
            session => session.ExportPng(new Size(500, 500)));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "opaque zigzag ink");
    }

    [Fact]
    public void Filled_self_intersecting_polyline_matches()
    {
        //Arrange + Act - a five-pointed star's self-intersections exercise the fill rule
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            session =>
            {
                session.AddLayer("Star", Color.FromRgb(200, 160, 20));
                session.SetLayerOpacity(255);
                session.SetBackgroundFillColor(Color.White);
                session.DrawPolyline(
                    new (float, float)[] { (500, 80), (620, 780), (140, 350), (860, 350), (380, 780) },
                    thickness: 10, filled: true);
            },
            session => session.ExportPng(new Size(500, 500)));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "filled star");
    }

    [Fact]
    public void Jpeg_export_matches()
    {
        //Arrange + Act - two different JPEG encoders are in play, so the margin is wider
        (byte[] skiaBytes, byte[] noSkiaBytes) = RenderBoth(
            session =>
            {
                BuildShapeCatalogScenario(session);
            },
            session => session.ExportJpeg(new Size(600, 600), quality: 90));

        //Assert
        ImageComparison.AssertSimilar(skiaBytes, noSkiaBytes, "JPEG export",
            maxMeanDelta: 3.0, maxFractionNoticeable: 0.05);
    }
}
