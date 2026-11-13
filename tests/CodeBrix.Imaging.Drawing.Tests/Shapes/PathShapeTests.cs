using System;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class PathShapeTests
{
    private static readonly Size Frame = new Size(200, 200);

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255 });
        session.AddLayer("Paths", Color.FromRgb(20, 60, 200));
        return session;
    }

    private static PathGeometry Across() => PathGeometry.Line(new PointF(20, 100), new PointF(180, 100));

    [Fact]
    public void Simple_constructor_outlines_with_round_caps_and_joins()
    {
        //Act
        var shape = new PathShape(Across(), 8f, Color.Red);

        //Assert
        shape.IsStroked.Should().BeTrue();
        shape.IsFilled.Should().BeFalse();
        shape.StrokeStyle.Width.Should().Be(8f);
        shape.StrokeStyle.Cap.Should().Be(PathStrokeCap.Round);
        shape.StrokeStyle.Join.Should().Be(PathStrokeJoin.Round);
        default(PathStrokeCap).Should().Be(PathStrokeCap.Round);
        default(PathStrokeJoin).Should().Be(PathStrokeJoin.Round);
        shape.Morph.Should().BeNull();
        shape.IsTrimmed.Should().BeFalse();
        shape.IsDynamic.Should().BeFalse();
    }

    [Fact]
    public void Constructors_reject_missing_geometry_or_paint()
    {
        //Act
        Action noGeometry = () => _ = new PathShape((PathGeometry)null);
        Action nothingToDraw = () => _ = new PathShape(Across(), (PathStrokeStyle)null);
        Action noFill = () => _ = new PathShape(Across(), (ShapeFill)null);

        //Assert
        noGeometry.Should().Throw<ArgumentNullException>();
        nothingToDraw.Should().Throw<ArgumentException>();
        noFill.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Trim_end_draws_only_the_start_of_the_path()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        PathShape shape = session.DrawPath(Across(), 10f);

        //Act
        shape.TrimEnd = 0.5f;
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        shape.IsDynamic.Should().BeTrue();
        shape.IsTrimmed.Should().BeTrue();
        TestAssets.PixelAt(pixels, 200, 50, 100).A.Should().Be(255);
        TestAssets.PixelAt(pixels, 200, 150, 100).A.Should().Be(0);
        shape.GetDrawnGeometry().GetLength().Should().Be(80f);
    }

    [Fact]
    public void Trim_properties_validate_their_values()
    {
        //Arrange
        var shape = new PathShape(Across());

        //Act
        Action start = () => shape.TrimStart = 1.5f;
        Action end = () => shape.TrimEnd = Single.NaN;
        Action offset = () => shape.TrimOffset = Single.PositiveInfinity;
        Action morph = () => shape.MorphFraction = 0.5f;

        //Assert
        start.Should().Throw<ArgumentOutOfRangeException>();
        end.Should().Throw<ArgumentOutOfRangeException>();
        offset.Should().Throw<ArgumentOutOfRangeException>();
        morph.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Styled_path_fills_under_its_outline_over_a_transparent_background()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var style = new PathStrokeStyle(6f, PathStrokeCap.Butt, PathStrokeJoin.Miter);
        session.DrawShape(new PathShape(PathGeometry.Rectangle(new RectangleF(40, 40, 120, 120)), style, Color.Black, Color.FromRgb(250, 200, 0)));

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.PixelAt(pixels, 200, 100, 100).Should().Be(new Rgba32(250, 200, 0, 255));
        TestAssets.PixelAt(pixels, 200, 40, 100).Should().Be(new Rgba32(0, 0, 0, 255));
        TestAssets.PixelAt(pixels, 200, 10, 10).A.Should().Be(0);
    }

    [Fact]
    public void Gradient_filled_path_paints_its_stops()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawShape(new PathShape(PathGeometry.Circle(new PointF(100, 100), 80),
            new LinearGradientFill(new PointF(20, 0), new PointF(180, 0), Color.Red, Color.Blue)));

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        Rgba32 left = TestAssets.PixelAt(pixels, 200, 30, 100);
        Rgba32 right = TestAssets.PixelAt(pixels, 200, 170, 100);
        left.R.Should().BeGreaterThan(200);
        right.B.Should().BeGreaterThan(200);
    }

    [Fact]
    public void Dashed_outline_leaves_gaps()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var style = new PathStrokeStyle(8f, PathStrokeCap.Butt, dashPattern: new[] { 20f, 20f });
        session.DrawShape(new PathShape(Across(), style));

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        //Dashes along x 20-40, 60-80, 100-120, 140-160; gaps between
        TestAssets.PixelAt(pixels, 200, 30, 100).A.Should().Be(255);
        TestAssets.PixelAt(pixels, 200, 50, 100).A.Should().Be(0);
        TestAssets.PixelAt(pixels, 200, 70, 100).A.Should().Be(255);
        TestAssets.PixelAt(pixels, 200, 90, 100).A.Should().Be(0);
        TestAssets.PixelAt(pixels, 200, 170, 100).A.Should().Be(0);
    }

    [Fact]
    public void Morphing_shape_draws_its_morph_at_the_fraction()
    {
        //Arrange
        var morph = new PathMorph(PathGeometry.Rectangle(new RectangleF(10, 10, 40, 40)), PathGeometry.Rectangle(new RectangleF(150, 150, 40, 40)), 16);
        using DrawingSession session = CreateSession();
        var shape = new PathShape(morph, 0f, 1f, null, isFilled: true);
        session.DrawShape(shape);

        //Act
        shape.MorphFraction = 0.5f;
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        shape.Geometry.GetBounds().Should().Be(new RectangleF(80, 80, 40, 40));
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().Be(255);
        TestAssets.PixelAt(pixels, 200, 30, 30).A.Should().Be(0);
    }

    [Fact]
    public void Bounds_cover_the_drawn_part_and_the_stroke_reach()
    {
        //Arrange
        var round = new PathShape(Across(), 10f);
        var miter = new PathShape(Across(), new PathStrokeStyle(10f, PathStrokeCap.Square, PathStrokeJoin.Miter, 3f));

        //Act
        round.TrimEnd = 0.5f;
        RectangleF? roundBounds = round.GetBounds();
        RectangleF? miterBounds = miter.GetBounds();

        //Assert
        roundBounds.Should().Be(new RectangleF(15, 95, 90, 10));
        miterBounds.Value.Left.Should().Be(5f);
        round.GetOutline().GetLength().Should().Be(80f);
    }

    [Fact]
    public void Live_render_follows_the_trim_as_time_changes_and_matches_a_fresh_render()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        PathShape shape = session.DrawPath(PathGeometry.Circle(new PointF(100, 100), 70), 12f);
        session.Animations.Bind(shape, AnimatedProperty.TrimEnd, new FloatTrack { { 0, 0f }, { 1, 1f } });
        var info = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        //Act
        session.Time = 0.2;
        session.Render(canvas, info);
        session.Time = 0.7;
        session.Render(canvas, info);
        byte[] live = bitmap.Bytes;

        using DrawingSession fresh = CreateSession();
        PathShape copy = fresh.DrawPath(PathGeometry.Circle(new PointF(100, 100), 70), 12f);
        copy.TrimEnd = 0.7f;
        using var freshBitmap = new SKBitmap(info);
        using var freshCanvas = new SKCanvas(freshBitmap);
        fresh.Render(freshCanvas, info);

        //Assert
        shape.TrimEnd.Should().Be(0.7f);
        live.Should().Equal(freshBitmap.Bytes);
    }
}
