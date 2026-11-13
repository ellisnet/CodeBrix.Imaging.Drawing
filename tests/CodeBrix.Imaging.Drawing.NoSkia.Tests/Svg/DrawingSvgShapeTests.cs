using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Svg;

/// <summary>
/// Covers the SVG session element: a loaded document drawn into a destination as an ordinary
/// element, and its parts (element ids) moved, turned, faded, hidden and trimmed at draw time
/// without changing the loaded document.
/// </summary>
public class DrawingSvgShapeTests
{
    private static readonly Size Frame = new Size(400, 200);

    //Document units are half the calibrated units when fitted into the whole 400 x 200 frame
    private const string Markup =
@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink"" width=""200"" height=""100"" viewBox=""0 0 200 100"">
  <defs><rect id=""tile"" width=""10"" height=""10"" fill=""#ffff00""/></defs>
  <rect id=""back"" x=""0"" y=""0"" width=""200"" height=""100"" fill=""#202030""/>
  <g id=""left"">
    <rect id=""arm"" x=""40"" y=""45"" width=""40"" height=""10"" fill=""#ff0000""/>
  </g>
  <rect id=""right"" x=""140"" y=""30"" width=""20"" height=""40"" fill=""#00ff00""/>
  <path id=""line"" d=""M 10 90 L 190 90"" stroke=""#0000ff"" stroke-width=""4"" fill=""none""/>
  <use id=""tileA"" xlink:href=""#tile"" x=""10"" y=""10""/>
  <use id=""tileB"" xlink:href=""#tile"" x=""180"" y=""10""/>
  <g id=""nothing""/>
</svg>";

    private const string MaskMarkup =
@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""100"" viewBox=""0 0 200 100"">
  <mask id=""m""><rect id=""window"" x=""0"" y=""0"" width=""100"" height=""100"" fill=""#ffffff""/></mask>
  <rect id=""masked"" x=""0"" y=""0"" width=""200"" height=""100"" fill=""#ff0000"" mask=""url(#m)""/>
</svg>";

    private static DrawingSvg Load(string markup = Markup)
    {
        var svg = new DrawingSvg();
        svg.FromSvg(markup).Should().BeTrue();
        return svg;
    }

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255 });
        session.AddLayer("Svg", Color.White);
        return session;
    }

    //The colour at a point given in DOCUMENT units (x2 to calibrated), over transparency
    private static Rgba32 At(DrawingSession session, float docX, float docY)
    {
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);
        int i = (((int)(docY * 2) * Frame.Width) + (int)(docX * 2)) * 4;
        return new Rgba32(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    [Fact]
    public void The_shape_draws_the_document_fitted_into_its_destination()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();

        //Act
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));

        //Assert
        At(session, 60, 50).Should().Be(new Rgba32(255, 0, 0, 255));
        At(session, 150, 50).Should().Be(new Rgba32(0, 255, 0, 255));
        At(session, 15, 15).Should().Be(new Rgba32(255, 255, 0, 255));
        shape.GetBounds().Should().Be(new RectangleF(0, 0, 400, 200));
        shape.ContentBounds.Should().Be(new RectangleF(0, 0, 400, 200));
        shape.MapToCalibrated(new PointF(60, 50)).Should().Be(new PointF(120, 100));
    }

    [Fact]
    public void Parts_are_discoverable_with_their_document_bounds()
    {
        //Arrange
        using DrawingSvg svg = Load();
        var shape = new DrawingSvgShape(svg, new RectangleF(0, 0, 400, 200));

        //Act
        IReadOnlyList<string> ids = shape.GetPartIds();
        DrawingSvgPart arm = shape.GetPart("arm");

        //Assert
        ids.Should().Contain("back", "left", "arm", "right", "line", "tileA", "tileB", "tile");
        arm.DocumentBounds.Should().Be(new RectangleF(40, 45, 40, 10));
        arm.Center.Should().Be(new PointF(60, 50));
        arm.Node.Id.Should().Be("arm");
        shape.HasPart("nope").Should().BeFalse();
        shape.Parts.Count.Should().Be(1);
        shape.GetPart("tile").DocumentBounds.Width.Should().Be(180f);
    }

    [Fact]
    public void A_part_turns_about_its_own_centre_and_moves_in_document_units()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));
        DrawingSvgPart arm = shape.GetPart("arm");

        //Act
        arm.Rotation = 90;
        Rgba32 turnedTip = At(session, 60, 33);
        Rgba32 turnedSide = At(session, 45, 50);
        RectangleF bounds = shape.GetPartBounds("arm").Value;
        arm.Rotation = 0;
        arm.Translation = new PointF(0, 30);
        Rgba32 moved = At(session, 60, 80);
        Rgba32 vacated = At(session, 60, 50);

        //Assert
        turnedTip.Should().Be(new Rgba32(255, 0, 0, 255));
        turnedSide.Should().Be(new Rgba32(32, 32, 48, 255));
        bounds.X.Should().BeApproximately(110f, 0.01f);
        bounds.Y.Should().BeApproximately(60f, 0.01f);
        bounds.Width.Should().BeApproximately(20f, 0.01f);
        bounds.Height.Should().BeApproximately(80f, 0.01f);
        moved.Should().Be(new Rgba32(255, 0, 0, 255));
        vacated.Should().Be(new Rgba32(32, 32, 48, 255));
    }

    [Fact]
    public void A_child_override_rides_inside_its_parents()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));

        //Act - the arm moves down 30, then its group turns half round about (100, 50)
        shape.GetPart("arm").Translation = new PointF(0, 30);
        DrawingSvgPart left = shape.GetPart("left");
        left.Pivot = new PointF(100, 50);
        left.Rotation = 180;

        //Assert
        At(session, 140, 20).Should().Be(new Rgba32(255, 0, 0, 255));
        At(session, 60, 80).Should().Be(new Rgba32(32, 32, 48, 255));
        shape.GetPartBounds("arm").Value.X.Should().BeApproximately(240f, 0.01f);
    }

    [Fact]
    public void Opacity_and_visibility_fade_or_hide_a_part()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));

        //Act
        shape.GetPart("back").IsVisible = false;
        shape.GetPart("right").Opacity = 0.5f;
        Rgba32 hiddenBack = At(session, 100, 20);
        Rgba32 halfGreen = At(session, 150, 50);

        //Assert
        hiddenBack.A.Should().Be(0);
        halfGreen.G.Should().Be(255);
        ((int)halfGreen.A).Should().BeInRange(126, 129);
    }

    [Fact]
    public void An_id_inside_a_used_element_is_on_every_instance_and_a_use_id_on_one()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));

        //Act
        shape.GetPart("tileA").IsVisible = false;
        Rgba32 firstGone = At(session, 15, 15);
        Rgba32 secondStays = At(session, 185, 15);
        shape.GetPart("tileA").IsVisible = true;
        shape.GetPart("tile").IsVisible = false;
        Rgba32 bothGone = At(session, 185, 15);

        //Assert
        firstGone.Should().Be(new Rgba32(32, 32, 48, 255));
        secondStays.Should().Be(new Rgba32(255, 255, 0, 255));
        bothGone.Should().Be(new Rgba32(32, 32, 48, 255));
    }

    [Fact]
    public void Trimming_a_part_draws_its_strokes_on()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));

        //Act
        shape.GetPart("line").TrimEnd = 0.5f;

        //Assert
        At(session, 50, 90).Should().Be(new Rgba32(0, 0, 255, 255));
        At(session, 150, 90).Should().Be(new Rgba32(32, 32, 48, 255));
        At(session, 60, 50).Should().Be(new Rgba32(255, 0, 0, 255));
    }

    [Fact]
    public void Mask_content_parts_are_drawn_with_their_overrides()
    {
        //Arrange
        using DrawingSvg svg = Load(MaskMarkup);
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));
        Rgba32 inside = At(session, 50, 50);

        //Act
        shape.GetPart("window").Translation = new PointF(100, 0);

        //Assert
        inside.Should().Be(new Rgba32(255, 0, 0, 255));
        At(session, 50, 50).A.Should().Be(0);
        At(session, 150, 50).Should().Be(new Rgba32(255, 0, 0, 255));
    }

    [Fact]
    public void Overrides_never_change_the_loaded_document()
    {
        //Arrange
        using DrawingSvg svg = Load();
        byte[] before = svg.RasterizeToPng();
        DrawingPicture picture = svg.Picture;
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));
        byte[] untouched = session.ExportPixels(Frame, false);

        //Act
        shape.GetPart("arm").Rotation = 45;
        shape.GetPart("line").TrimEnd = 0.2f;
        _ = session.ExportPixels(Frame, false);
        shape.ResetParts();
        byte[] reset = session.ExportPixels(Frame, false);

        //Assert
        svg.Picture.Should().BeSameAs(picture);
        svg.RasterizeToPng().Should().Equal(before);
        reset.Should().Equal(untouched);
    }

    [Fact]
    public void A_re_recorded_scene_without_visible_change_draws_the_loaded_picture_byte_for_byte()
    {
        //Arrange - hiding an empty group forces the part renderer's walk without changing any pixel
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));
        byte[] loaded = session.ExportPixels(Frame, false);

        //Act
        shape.GetPart("nothing").IsVisible = false;
        byte[] rerecorded = session.ExportPixels(Frame, false);

        //Assert
        rerecorded.Should().Equal(loaded);
    }

    [Fact]
    public void Bound_tracks_drive_parts_and_the_live_render_matches_a_fresh_one()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));
        shape.BindPart(session.Animations, "arm", AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 2, 180f } });
        shape.BindPart(session.Animations, "right", AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 2, new PointF(-40, 0) } });
        shape.BindPart(session.Animations, "line", AnimatedProperty.TrimEnd, new FloatTrack { { 0, 0f }, { 2, 1f } });
        var info = new DrawingImageInfo(Frame.Width, Frame.Height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);
        using var bitmap = new DrawingBitmap(info);
        using var canvas = new DrawingCanvas(bitmap);

        //Act
        session.Time = 0.4;
        session.Render(canvas, info);
        session.Time = 1.0;
        session.Render(canvas, info);
        byte[] live = bitmap.Bytes;

        using DrawingSession fresh = CreateSession();
        DrawingSvgShape copy = fresh.DrawSvg(svg, new RectangleF(0, 0, 400, 200));
        copy.GetPart("arm").Rotation = 90;
        copy.GetPart("right").Translation = new PointF(-20, 0);
        copy.GetPart("line").TrimEnd = 0.5f;
        using var freshBitmap = new DrawingBitmap(info);
        using var freshCanvas = new DrawingCanvas(freshBitmap);
        fresh.Render(freshCanvas, info);

        //Assert
        shape.GetPart("arm").Rotation.Should().Be(90f);
        live.Should().Equal(freshBitmap.Bytes);
    }

    [Theory]
    [InlineData(DrawingSvgAspect.Meet, 0f, 100f, 400f, 200f)]
    [InlineData(DrawingSvgAspect.Slice, 0f, 0f, 400f, 400f)]
    [InlineData(DrawingSvgAspect.Stretch, 0f, 0f, 400f, 400f)]
    [InlineData(DrawingSvgAspect.None, 0f, 0f, 200f, 100f)]
    public void Aspect_options_fit_the_document(DrawingSvgAspect aspect, float x, float y, float width, float height)
    {
        //Arrange
        using DrawingSvg svg = Load();

        //Act
        var shape = new DrawingSvgShape(svg, new RectangleF(0, 0, 400, 400), aspect);

        //Assert
        shape.GetBounds().Should().Be(new RectangleF(x, y, width, height));
        shape.GetOutline().GetBounds().Should().Be(new RectangleF(x, y, width, height));
    }

    [Fact]
    public void Element_transforms_opacity_masks_and_effects_apply_to_the_whole_document()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        var shape = new DrawingSvgShape(svg, new RectangleF(0, 0, 200, 100))
        {
            Transform = DrawingTransform.Translate(200, 100),
            Opacity = 0.5f,
            Effects = DrawingEffects.DropShadow(new PointF(4, 4), 3f, Color.Black),
        };
        var group = new DrawingGroup { Mask = DrawingMask.Inside(PathGeometry.Rectangle(new RectangleF(200, 100, 100, 100))) };
        group.Add(shape);
        session.DrawShape(group);

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);
        Rgba32 kept = new Rgba32(pixels[((150 * 400) + 220) * 4], pixels[((150 * 400) + 220) * 4 + 1], pixels[((150 * 400) + 220) * 4 + 2], pixels[((150 * 400) + 220) * 4 + 3]);
        byte cut = pixels[((150 * 400) + 350) * 4 + 3];
        byte outside = pixels[((50 * 400) + 50) * 4 + 3];

        //Assert
        ((int)kept.A).Should().BeInRange(120, 135);
        cut.Should().Be(0);
        outside.Should().Be(0);
        shape.GetTransformedBounds().Should().Be(new RectangleF(200, 100, 200, 100));
    }

    [Fact]
    public void A_safe_area_check_measures_the_element()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using DrawingSession session = CreateSession();
        DrawingSvgShape shape = session.DrawSvg(svg, new RectangleF(0, 0, 400, 200));
        shape.IsSafeAreaChecked = true;

        //Act
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe, Frame);

        //Assert
        findings.Count.Should().Be(4);
    }

    [Fact]
    public void Invalid_arguments_throw()
    {
        //Arrange
        using DrawingSvg svg = Load();
        using var empty = new DrawingSvg();
        var shape = new DrawingSvgShape(svg, new RectangleF(0, 0, 10, 10));

        //Act
        Action unloaded = () => _ = new DrawingSvgShape(empty, new RectangleF(0, 0, 10, 10));
        Action nullSvg = () => _ = new DrawingSvgShape(null, new RectangleF(0, 0, 10, 10));
        Action unknown = () => shape.GetPart("missing");
        Action badOpacity = () => shape.GetPart("arm").Opacity = 2f;
        Action wrongTrack = () => shape.BindPart(new DrawingAnimations(), "arm", AnimatedProperty.Color, new FloatTrack { { 0, 0f } });

        //Assert
        unloaded.Should().Throw<ArgumentException>();
        nullSvg.Should().Throw<ArgumentNullException>();
        unknown.Should().Throw<ArgumentException>();
        badOpacity.Should().Throw<ArgumentOutOfRangeException>();
        wrongTrack.Should().Throw<ArgumentException>();
    }
}
