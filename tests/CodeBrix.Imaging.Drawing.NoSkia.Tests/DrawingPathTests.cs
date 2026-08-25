using System.Collections.Generic;
using System.Linq;
using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Covers reading a path back out: a consumer that re-emits geometry into its own model
/// must be able to recover every segment, and a path rebuilt from what it reports must be
/// the same path - same bounds, and the same pixels when it is drawn.
/// </summary>
public class DrawingPathTests
{
    private static readonly DrawingImageInfo CanvasInfo =
        new DrawingImageInfo(80, 80, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);

    private static DrawingPath BuildMixedPath()
    {
        var builder = new DrawingPathBuilder();
        builder.SetFillType(DrawingPathFillType.EvenOdd);
        builder.MoveTo(10f, 10f);
        builder.LineTo(60f, 12f);
        builder.QuadTo(new DrawingPoint(70f, 40f), new DrawingPoint(50f, 62f));
        builder.CubicTo(new DrawingPoint(40f, 70f), new DrawingPoint(20f, 66f), new DrawingPoint(12f, 50f));
        builder.Close();
        return builder.Detach();
    }

    private static DrawingBitmap Fill(DrawingPath path)
    {
        var bitmap = new DrawingBitmap(CanvasInfo);
        using var canvas = new DrawingCanvas(bitmap);
        canvas.Clear(DrawingColors.White);
        canvas.DrawPath(path, new DrawingPaint { Color = DrawingColors.Navy, IsAntialias = true });
        return bitmap;
    }

    [Fact]
    public void Get_verbs_reports_every_segment_with_the_points_that_verb_carries()
    {
        //Arrange
        using DrawingPath path = BuildMixedPath();

        //Act
        List<DrawingPathSegment> segments = path.GetVerbs().ToList();

        //Assert
        segments.Select(segment => segment.Verb).Should().Equal(new[]
        {
            DrawingPathVerb.Move,
            DrawingPathVerb.Line,
            DrawingPathVerb.Quad,
            DrawingPathVerb.Cubic,
            DrawingPathVerb.Close,
        });
        segments.Select(segment => segment.Points.Length).Should().Equal(new[] { 1, 1, 2, 3, 0 });
        segments[0].Points[0].Should().Be(new DrawingPoint(10f, 10f));
        segments[3].Points[2].Should().Be(new DrawingPoint(12f, 50f));
    }

    [Fact]
    public void Get_verbs_on_an_empty_path_reports_nothing()
    {
        //Arrange
        using DrawingPath path = new DrawingPathBuilder().Detach();

        //Act
        List<DrawingPathSegment> segments = path.GetVerbs().ToList();

        //Assert
        path.IsEmpty.Should().BeTrue();
        segments.Should().BeEmpty();
    }

    [Fact]
    public void A_path_rebuilt_from_its_verbs_has_the_same_bounds_and_raster()
    {
        //Arrange
        using DrawingPath original = BuildMixedPath();

        //Act
        var builder = new DrawingPathBuilder();
        builder.SetFillType(original.FillType);
        foreach (DrawingPathSegment segment in original.GetVerbs())
        {
            switch (segment.Verb)
            {
                case DrawingPathVerb.Move:
                    builder.MoveTo(segment.Points[0]);
                    break;
                case DrawingPathVerb.Line:
                    builder.LineTo(segment.Points[0]);
                    break;
                case DrawingPathVerb.Quad:
                    builder.QuadTo(segment.Points[0], segment.Points[1]);
                    break;
                case DrawingPathVerb.Cubic:
                    builder.CubicTo(segment.Points[0], segment.Points[1], segment.Points[2]);
                    break;
                default:
                    builder.Close();
                    break;
            }
        }
        using DrawingPath rebuilt = builder.Detach();

        //Assert
        rebuilt.FillType.Should().Be(original.FillType);
        rebuilt.PointCount.Should().Be(original.PointCount);
        rebuilt.Bounds.Should().Be(original.Bounds);
        using DrawingBitmap originalRaster = Fill(original);
        using DrawingBitmap rebuiltRaster = Fill(rebuilt);
        rebuiltRaster.Bytes.Should().Equal(originalRaster.Bytes);
    }

    [Fact]
    public void Add_path_appends_another_paths_contours()
    {
        //Arrange
        using DrawingPath first = BuildMixedPath();
        var builder = new DrawingPathBuilder();
        builder.SetFillType(first.FillType);

        //Act
        builder.AddPath(first).AddPath(null);
        using DrawingPath combined = builder.Detach();

        //Assert
        combined.PointCount.Should().Be(first.PointCount);
        combined.Bounds.Should().Be(first.Bounds);
    }
}
