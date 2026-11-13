using System;
using System.IO;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Images;

public class ImageSourceTests
{
    //A 4 x 2 picture: every pixel different, one half-transparent and one fully transparent
    internal static byte[] Pixels()
    {
        return new byte[]
        {
            255, 0, 0, 255,    0, 255, 0, 255,    0, 0, 255, 255,    255, 255, 0, 255,
            10, 20, 30, 128,   40, 50, 60, 255,   70, 80, 90, 0,     200, 100, 50, 255,
        };
    }

    [Fact]
    public void FromRgba_copies_the_pixels_exactly_including_transparency()
    {
        //Arrange
        byte[] rgba = Pixels();

        //Act
        using ImageSource source = ImageSource.FromRgba(rgba, 4, 2);
        rgba[0] = 1;

        //Assert
        source.Width.Should().Be(4);
        source.Height.Should().Be(2);
        source.Size.Should().Be(new Size(4, 2));
        byte[] copy = source.ToRgba();
        copy[0].Should().Be(255);
        copy.AsSpan(4).ToArray().Should().Equal(Pixels().AsSpan(4).ToArray());
    }

    [Fact]
    public void FromBgra_swaps_red_and_blue()
    {
        //Arrange
        var bgra = new byte[] { 1, 2, 3, 4 };

        //Act
        using ImageSource source = ImageSource.FromBgra(bgra, 1, 1);

        //Assert
        source.ToRgba().Should().Equal((byte)3, (byte)2, (byte)1, (byte)4);
    }

    [Fact]
    public void FromEncoded_FromImage_and_FromPlaneImage_agree()
    {
        //Arrange
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(Pixels(), 4, 2, PngFormat.Instance);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        //Act
        using ImageSource decoded = ImageSource.FromEncoded(stream.ToArray());
        using ImageSource copied = ImageSource.FromImagingImage(image);
        using ImageSource fromPlanes = ImageSource.FromPlaneImage(DrawingPlaneImage.FromRgba(Pixels(), 4, 2));

        //Assert
        decoded.ToRgba().Should().Equal(copied.ToRgba());
        fromPlanes.ToRgba().Should().Equal(Pixels());
    }

    [Fact]
    public void Crop_copies_a_part_and_rejects_areas_outside_the_picture()
    {
        //Arrange
        using ImageSource source = ImageSource.FromRgba(Pixels(), 4, 2);

        //Act
        using ImageSource part = source.Crop(new Rectangle(1, 1, 2, 1));
        Action outside = () => source.Crop(new Rectangle(3, 0, 2, 1));

        //Assert
        part.ToRgba().Should().Equal((byte)40, (byte)50, (byte)60, (byte)255, (byte)70, (byte)80, (byte)90, (byte)0);
        outside.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Factories_reject_bad_input_and_a_disposed_source_says_so()
    {
        //Arrange
        ImageSource source = ImageSource.FromRgba(Pixels(), 4, 2);

        //Act
        source.Dispose();
        Action shortBuffer = () => ImageSource.FromRgba(new byte[10], 4, 2);
        Action zero = () => ImageSource.FromRgba(new byte[16], 0, 4);
        Action garbage = () => ImageSource.FromEncoded(new byte[] { 1, 2, 3 });
        Action read = () => source.ToRgba();

        //Assert
        source.IsDisposed.Should().BeTrue();
        shortBuffer.Should().Throw<ArgumentException>();
        zero.Should().Throw<ArgumentOutOfRangeException>();
        garbage.Should().Throw<ArgumentException>();
        read.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Sprite_sheet_from_a_grid_cuts_cells_in_reading_order_and_owns_them()
    {
        //Arrange
        using ImageSource image = ImageSource.FromRgba(Pixels(), 4, 2);

        //Act
        SpriteSheet sheet = SpriteSheet.FromGrid(image, 2, 2, 3);
        ImageSource third = sheet.GetFrame(2);
        sheet.Dispose();

        //Assert
        sheet.IsDisposed.Should().BeTrue();
        sheet.FrameCount.Should().Be(3);
        sheet.OwnsFrames.Should().BeTrue();
        third.IsDisposed.Should().BeTrue();
        image.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Sprite_sheet_of_given_frames_leaves_them_to_the_caller()
    {
        //Arrange
        using ImageSource a = ImageSource.FromRgba(Pixels(), 4, 2);
        using ImageSource b = ImageSource.FromRgba(Pixels(), 4, 2);

        //Act
        var sheet = new SpriteSheet(new[] { a, b });
        bool disposedBefore = sheet.IsDisposed;
        sheet.Dispose();
        Action empty = () => _ = new SpriteSheet(Array.Empty<ImageSource>());
        Action badGrid = () => SpriteSheet.FromGrid(a, 5, 1);

        //Assert
        sheet.FrameCount.Should().Be(2);
        sheet.GetFrame(1).Should().BeSameAs(b);
        disposedBefore.Should().BeFalse();
        sheet.IsDisposed.Should().BeTrue();
        a.IsDisposed.Should().BeFalse();
        empty.Should().Throw<ArgumentException>();
        badGrid.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Sprite_sheet_from_rectangles_cuts_each_one()
    {
        //Arrange
        using ImageSource image = ImageSource.FromRgba(Pixels(), 4, 2);

        //Act
        using SpriteSheet sheet = SpriteSheet.FromRectangles(image, new[] { new Rectangle(3, 1, 1, 1), new Rectangle(0, 0, 1, 1) });

        //Assert
        sheet.GetFrame(0).ToRgba().Should().Equal((byte)200, (byte)100, (byte)50, (byte)255);
        sheet.GetFrame(1).ToRgba().Should().Equal((byte)255, (byte)0, (byte)0, (byte)255);
    }
}
