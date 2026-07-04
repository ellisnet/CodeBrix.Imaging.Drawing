using System;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionImageFactoryTests
{
    private static byte[] EncodePng(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height));
        bitmap.Erase(color);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void CreateForImage_derives_landscape_calibration_size()
    {
        //Arrange + Act - a 2:1 landscape image
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(200, 100, SKColors.Gray), CalibrationSizing.DeriveFromBackgroundImage);

        //Assert
        session.CalibrationSize.Should().Be(new SKSizeI(1000, 500));
    }

    [Fact]
    public void CreateForImage_derives_portrait_calibration_size()
    {
        //Arrange + Act - a 1:2 portrait image
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(100, 200, SKColors.Gray), CalibrationSizing.DeriveFromBackgroundImage);

        //Assert
        session.CalibrationSize.Should().Be(new SKSizeI(500, 1000));
    }

    [Fact]
    public void CreateForImage_from_options_respects_options_calibration_size()
    {
        //Arrange
        var options = new DrawingSessionOptions { CalibrationSize = new SKSizeI(123, 456) };

        //Act - FromOptions must never silently replace the caller's calibration size
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(200, 100, SKColors.Gray), CalibrationSizing.FromOptions, options);

        //Assert
        session.CalibrationSize.Should().Be(new SKSizeI(123, 456));
    }

    [Fact]
    public void CreateForImage_from_options_without_options_uses_documented_default()
    {
        //Arrange + Act
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(200, 100, SKColors.Gray), CalibrationSizing.FromOptions);

        //Assert
        session.CalibrationSize.Should().Be(DrawingSessionOptions.DefaultCalibrationSize);
    }

    [Fact]
    public void CreateForImage_accepts_explicit_calibration_size()
    {
        //Arrange + Act
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(200, 100, SKColors.Gray), new SKSizeI(2000, 1000));

        //Assert
        session.CalibrationSize.Should().Be(new SKSizeI(2000, 1000));
    }

    [Fact]
    public void CreateForImage_rejects_degenerate_explicit_calibration_size()
    {
        //Arrange
        Action act = () => DrawingSession.CreateForImage(
            EncodePng(10, 10, SKColors.Gray), new SKSizeI(0, 100));

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateForImage_rejects_undefined_sizing_value()
    {
        //Arrange
        Action act = () => DrawingSession.CreateForImage(
            EncodePng(10, 10, SKColors.Gray), (CalibrationSizing)99);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateForImage_sets_background_image()
    {
        //Arrange + Act
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(40, 30, SKColors.Yellow), CalibrationSizing.DeriveFromBackgroundImage);

        //Assert
        session.BackgroundImage.Should().NotBeNull();
        session.BackgroundImage.Width.Should().Be(40);
        session.BackgroundImage.Height.Should().Be(30);
    }

    [Fact]
    public void CreateForImage_applies_other_options_when_deriving()
    {
        //Arrange
        var options = new DrawingSessionOptions
        {
            CalibrationSize = new SKSizeI(123, 456), //not used when deriving
            LayerOpacity = 200,
            StrokeWidth = 33f,
        };

        //Act
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(200, 100, SKColors.Gray), CalibrationSizing.DeriveFromBackgroundImage, options);

        //Assert
        session.CalibrationSize.Should().Be(new SKSizeI(1000, 500));
        session.LayerOpacity.Should().Be((byte)200);
        session.StrokeWidth.Should().Be(33f);
    }

    [Fact]
    public void CreateForImage_rejects_null_bytes()
    {
        //Arrange
        Action act = () => DrawingSession.CreateForImage((byte[])null, CalibrationSizing.DeriveFromBackgroundImage);

        //Act + Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateForImage_rejects_undecodable_bytes()
    {
        //Arrange
        Action act = () => DrawingSession.CreateForImage(
            new byte[] { 9, 9, 9 }, CalibrationSizing.DeriveFromBackgroundImage);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateForImage_bitmap_overload_leaves_ownership_with_caller()
    {
        //Arrange
        using var bitmap = new SKBitmap(new SKImageInfo(80, 40));
        bitmap.Erase(SKColors.Green);

        //Act
        DrawingSession session = DrawingSession.CreateForImage(bitmap, CalibrationSizing.DeriveFromBackgroundImage);
        session.CalibrationSize.Should().Be(new SKSizeI(1000, 500));
        session.Dispose();

        //Assert - the caller's bitmap is still usable after the session is disposed
        bitmap.GetPixel(1, 1).Should().Be(SKColors.Green);
    }

    [Fact]
    public void DefaultExportSize_uses_background_image_resolution()
    {
        //Arrange
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(320, 240, SKColors.Gray), CalibrationSizing.DeriveFromBackgroundImage);

        //Act + Assert
        session.DefaultExportSize.Should().Be(new SKSizeI(320, 240));
    }

    [Fact]
    public void DefaultExportSize_falls_back_to_calibration_size()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new SKSizeI(800, 600),
        });

        //Act + Assert
        session.DefaultExportSize.Should().Be(new SKSizeI(800, 600));
    }

    [Fact]
    public void Parameterless_export_produces_image_at_background_resolution()
    {
        //Arrange
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(320, 240, SKColors.Gray), CalibrationSizing.DeriveFromBackgroundImage);
        session.AddLayer("Damage", SKColors.Red);

        //Act
        byte[] png = session.ExportPng();

        //Assert
        using SKBitmap decoded = SKBitmap.Decode(png);
        decoded.Width.Should().Be(320);
        decoded.Height.Should().Be(240);
    }

    [Fact]
    public void Non_square_session_supports_full_draw_and_export_flow()
    {
        //Arrange - the "annotate an emailed photo" flow, end to end
        using DrawingSession session = DrawingSession.CreateForImage(
            EncodePng(400, 200, SKColors.LightGray), CalibrationSizing.DeriveFromBackgroundImage);
        session.AddLayer("Damage", SKColors.Red);

        var info = new SKImageInfo(400, 200);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        session.Render(canvas, info);

        //Act - draw a stroke across the middle with the pointer API
        session.PointerPressed(new SKPoint(100, 100), new SKSize(400, 200));
        session.PointerMoved(new SKPoint(300, 100), new SKSize(400, 200));
        session.PointerReleased();
        byte[] png = session.ExportPng();

        //Assert - the exported image carries the red-tinted stroke at photo resolution
        using SKBitmap decoded = SKBitmap.Decode(png);
        decoded.Width.Should().Be(400);
        decoded.Height.Should().Be(200);
        SKColor pixel = decoded.GetPixel(200, 100);
        pixel.Red.Should().BeGreaterThan(pixel.Blue);
    }
}
