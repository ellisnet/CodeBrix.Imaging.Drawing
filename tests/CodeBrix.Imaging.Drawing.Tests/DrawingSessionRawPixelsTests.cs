using System;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionRawPixelsTests
{
    private const int PixelWidth = 40;
    private const int PixelHeight = 30;

    //Left half red, right half blue (tightly packed BGRA) - asymmetric so mirroring is testable
    private static byte[] CreateTestPixels()
    {
        var pixels = new byte[PixelWidth * PixelHeight * 4];
        for (int y = 0; y < PixelHeight; y++)
        {
            for (int x = 0; x < PixelWidth; x++)
            {
                int offset = ((y * PixelWidth) + x) * 4;
                if (x < PixelWidth / 2)
                {
                    pixels[offset + 2] = 255;   //red
                }
                else
                {
                    pixels[offset] = 255;       //blue
                }
                pixels[offset + 3] = 255;
            }
        }
        return pixels;
    }

    private static SKColor GetExportPixel(DrawingSession session, int x, int y)
    {
        byte[] png = session.ExportPng(new Size(PixelWidth, PixelHeight));
        using SKBitmap decoded = SKBitmap.Decode(png);
        return decoded.GetPixel(x, y);
    }

    [Fact]
    public void CreateForImage_from_bgra_pixels_derives_the_calibration_size()
    {
        //Arrange + Act - a 4:3 image with DeriveFromBackgroundImage sizing
        using DrawingSession session = DrawingSession.CreateForImage(
            CreateTestPixels(), PixelWidth, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage);

        //Assert - long side becomes CalibrationLongSide, short side keeps the aspect ratio
        session.CalibrationSize.Width.Should().Be(DrawingSession.CalibrationLongSide);
        session.CalibrationSize.Height.Should().Be(750);
        session.DefaultExportSize.Should().Be(new Size(PixelWidth, PixelHeight));
    }

    [Fact]
    public void CreateForImage_from_bgra_pixels_shows_the_image_unmirrored_by_default()
    {
        //Arrange + Act
        using DrawingSession session = DrawingSession.CreateForImage(
            CreateTestPixels(), PixelWidth, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage);

        //Assert - red stays on the left
        SKColor left = GetExportPixel(session, 4, PixelHeight / 2);
        left.Red.Should().Be((byte)255);
        left.Blue.Should().Be((byte)0);
    }

    [Fact]
    public void CreateForImage_from_bgra_pixels_can_mirror_horizontally()
    {
        //Arrange + Act
        using DrawingSession session = DrawingSession.CreateForImage(
            CreateTestPixels(), PixelWidth, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage,
            mirrorHorizontally: true);

        //Assert - mirrored, the LEFT side is now blue and the RIGHT side red
        SKColor left = GetExportPixel(session, 4, PixelHeight / 2);
        SKColor right = GetExportPixel(session, PixelWidth - 4, PixelHeight / 2);
        left.Blue.Should().Be((byte)255);
        left.Red.Should().Be((byte)0);
        right.Red.Should().Be((byte)255);
        right.Blue.Should().Be((byte)0);
    }

    [Fact]
    public void CreateForImage_from_bgra_pixels_accepts_an_explicit_calibration_size()
    {
        //Arrange + Act
        using DrawingSession session = DrawingSession.CreateForImage(
            CreateTestPixels(), PixelWidth, PixelHeight, new Size(400, 300));

        //Assert
        session.CalibrationSize.Should().Be(new Size(400, 300));
    }

    [Fact]
    public void CreateForImage_from_bgra_pixels_validates_its_arguments()
    {
        //Arrange
        byte[] pixels = CreateTestPixels();

        //Act + Assert
        Assert.Throws<ArgumentNullException>(() => DrawingSession.CreateForImage(
            null, PixelWidth, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage));
        Assert.Throws<ArgumentOutOfRangeException>(() => DrawingSession.CreateForImage(
            pixels, 0, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage));
        Assert.Throws<ArgumentOutOfRangeException>(() => DrawingSession.CreateForImage(
            pixels, PixelWidth, -1, CalibrationSizing.DeriveFromBackgroundImage));
        //A buffer too small for the stated dimensions
        Assert.Throws<ArgumentException>(() => DrawingSession.CreateForImage(
            pixels, PixelWidth * 2, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage));
    }

    [Fact]
    public void SetBackgroundImage_from_bgra_pixels_replaces_the_background()
    {
        //Arrange
        using var session = new DrawingSession();

        //Act
        session.SetBackgroundImage(CreateTestPixels(), PixelWidth, PixelHeight);

        //Assert
        session.BackgroundImage.Width.Should().Be(PixelWidth);
        session.BackgroundImage.Height.Should().Be(PixelHeight);
        session.DefaultExportSize.Should().Be(new Size(PixelWidth, PixelHeight));
    }

    [Fact]
    public void SetBackgroundImage_from_bgra_pixels_can_mirror_horizontally()
    {
        //Arrange - match the calibration aspect to the image's so the export is full-bleed
        //  (with the default square space, a 4:3 export letterboxes left and right)
        using var session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(400, 300),
        });

        //Act
        session.SetBackgroundImage(CreateTestPixels(), PixelWidth, PixelHeight, mirrorHorizontally: true);

        //Assert
        SKColor left = GetExportPixel(session, 4, PixelHeight / 2);
        left.Blue.Should().Be((byte)255);
    }

    [Fact]
    public void GetDrawingRect_returns_the_centered_aspect_fit_rectangle()
    {
        //Arrange - a 4:3 drawing space in a square view letterboxes top and bottom
        using DrawingSession session = DrawingSession.CreateForImage(
            CreateTestPixels(), PixelWidth, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage);

        //Act
        RectangleF rect = session.GetDrawingRect(new SizeF(400, 400));

        //Assert
        rect.X.Should().Be(0f);
        rect.Y.Should().Be(50f);
        rect.Width.Should().Be(400f);
        rect.Height.Should().Be(300f);
    }

    [Fact]
    public void GetDrawingRect_is_empty_for_an_unusable_view_size()
    {
        //Arrange
        using var session = new DrawingSession();

        //Act + Assert
        session.GetDrawingRect(new SizeF(0, 100)).Width.Should().Be(0f);
    }

    [Fact]
    public void ScaleToView_scales_lengths_with_the_drawing_rectangle()
    {
        //Arrange - calibration 1000x750; in a 400x400 view the drawing rect is 400 wide,
        //  so calibrated lengths scale by 0.4
        using DrawingSession session = DrawingSession.CreateForImage(
            CreateTestPixels(), PixelWidth, PixelHeight, CalibrationSizing.DeriveFromBackgroundImage);

        //Act + Assert
        session.ScaleToView(30f, new SizeF(400, 400)).Should().Be(12f);
    }

    [Fact]
    public void ScaleToView_returns_the_input_for_unusable_sizes()
    {
        //Arrange
        using var session = new DrawingSession();

        //Act + Assert
        session.ScaleToView(30f, new SizeF(0, 0)).Should().Be(30f);
    }
}
