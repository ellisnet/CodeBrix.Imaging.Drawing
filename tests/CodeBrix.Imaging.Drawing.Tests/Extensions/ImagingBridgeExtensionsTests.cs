using System;
using CodeBrix.Imaging.Drawing.Extensions;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Extensions;

public class ImagingBridgeExtensionsTests
{
    [Fact]
    public void ToImagingImage_converts_bitmap_pixels_to_rgba()
    {
        //Arrange
        using var bitmap = new SKBitmap(new SKImageInfo(4, 3));
        bitmap.Erase(new SKColor(10, 20, 30, 255));

        //Act
        using Image<Rgba32> image = bitmap.ToImagingImage();

        //Assert
        image.Width.Should().Be(4);
        image.Height.Should().Be(3);
        Rgba32 pixel = image[0, 0];
        pixel.R.Should().Be((byte)10);
        pixel.G.Should().Be((byte)20);
        pixel.B.Should().Be((byte)30);
        pixel.A.Should().Be((byte)255);
    }

    [Fact]
    public void ToImagingImage_converts_skimage()
    {
        //Arrange
        using var bitmap = new SKBitmap(new SKImageInfo(5, 5));
        bitmap.Erase(SKColors.Yellow);
        using SKImage skImage = SKImage.FromBitmap(bitmap);

        //Act
        using Image<Rgba32> image = skImage.ToImagingImage();

        //Assert
        image.Width.Should().Be(5);
        image[2, 2].R.Should().Be((byte)255);
        image[2, 2].G.Should().Be((byte)255);
        image[2, 2].B.Should().Be((byte)0);
    }

    [Fact]
    public void ToImagingImage_rejects_null_bitmap()
    {
        //Arrange
        Action act = () => ((SKBitmap)null).ToImagingImage();

        //Act + Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportImagingImage_renders_session_to_imaging_image()
    {
        //Arrange
        using var session = new DrawingSession();
        session.BackgroundFillColor = SKColors.White;
        session.AddLayer("Pain", SKColors.Red);

        //Act
        using Image<Rgba32> image = session.ExportImagingImage(new SKSizeI(64, 64));

        //Assert
        image.Width.Should().Be(64);
        image.Height.Should().Be(64);
        image[32, 32].R.Should().Be((byte)255);
    }

    [Fact]
    public void ExportImagingImage_rejects_null_session()
    {
        //Arrange
        Action act = () => ((DrawingSession)null).ExportImagingImage(new SKSizeI(10, 10));

        //Act + Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
