using System;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.Images;
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

public class ImageShapeTests
{
    private static readonly Size Frame = new Size(80, 40);

    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255 });
        session.AddLayer("Pictures", Color.Black);
        return session;
    }

    [Fact]
    public void Nearest_sampling_draws_each_source_pixel_as_a_block()
    {
        //Arrange
        using ImageSource source = ImageSource.FromRgba(ImageSourceTests.Pixels(), 4, 2);
        using DrawingSession session = CreateSession();
        session.DrawImage(source, new RectangleF(0, 0, 80, 40), ImageSampling.Nearest);

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert - 20 x 20 blocks; sample the middle of each
        TestAssets.PixelAt(pixels, 80, 10, 10).Should().Be(new Rgba32(255, 0, 0, 255));
        TestAssets.PixelAt(pixels, 80, 70, 10).Should().Be(new Rgba32(255, 255, 0, 255));
        TestAssets.PixelAt(pixels, 80, 30, 30).Should().Be(new Rgba32(40, 50, 60, 255));
        TestAssets.PixelAt(pixels, 80, 50, 30).A.Should().Be(0);
        TestAssets.PixelAt(pixels, 80, 10, 30).A.Should().BeInRange((byte)127, (byte)129);
    }

    [Fact]
    public void Source_rectangle_draws_only_that_part()
    {
        //Arrange
        using ImageSource source = ImageSource.FromRgba(ImageSourceTests.Pixels(), 4, 2);
        using DrawingSession session = CreateSession();
        session.DrawShape(new ImageShape(source, new RectangleF(0, 0, 80, 40), new Rectangle(1, 0, 1, 1), ImageSampling.Nearest));

        //Act
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.PixelAt(pixels, 80, 5, 5).Should().Be(new Rgba32(0, 255, 0, 255));
        TestAssets.PixelAt(pixels, 80, 75, 35).Should().Be(new Rgba32(0, 255, 0, 255));
    }

    [Fact]
    public void Tint_multiplies_every_channel_and_can_be_animated()
    {
        //Arrange
        using ImageSource source = ImageSource.FromRgba(new byte[] { 200, 200, 200, 255 }, 1, 1);
        using DrawingSession session = CreateSession();
        ImageShape shape = session.DrawImage(source, new RectangleF(0, 0, 80, 40), ImageSampling.Nearest);
        session.Animations.Bind(shape, AnimatedProperty.Tint, new ColorTrack { { 0, Color.White }, { 1, Color.FromRgba(255, 0, 0, 128) } });

        //Act
        session.Time = 1;
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        shape.Tint.Should().Be(Color.FromRgba(255, 0, 0, 128));
        Rgba32 pixel = TestAssets.PixelAt(pixels, 80, 40, 20);
        pixel.R.Should().BeInRange((byte)198, (byte)202);
        pixel.G.Should().Be(0);
        pixel.A.Should().BeInRange((byte)127, (byte)129);
    }

    [Fact]
    public void Image_takes_the_element_transform_opacity_and_time_window()
    {
        //Arrange
        using ImageSource source = ImageSource.FromRgba(new byte[] { 0, 0, 255, 255 }, 1, 1);
        using DrawingSession session = CreateSession();
        ImageShape shape = session.DrawImage(source, new RectangleF(0, 0, 20, 20), ImageSampling.Nearest);
        shape.Transform = DrawingTransform.Translate(50, 10);
        shape.Opacity = 0.5f;
        shape.StartTime = 1;

        //Act
        byte[] before = session.ExportPixels(Frame, includeBackground: false);
        session.Time = 1;
        byte[] after = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.CountInked(before).Should().Be(0);
        TestAssets.PixelAt(after, 80, 60, 20).B.Should().Be(255);
        TestAssets.PixelAt(after, 80, 60, 20).A.Should().BeInRange((byte)127, (byte)129);
        TestAssets.PixelAt(after, 80, 5, 5).A.Should().Be(0);
        shape.GetBounds().Should().Be(new RectangleF(0, 0, 20, 20));
        shape.GetTransformedBounds().Should().Be(new RectangleF(50, 10, 20, 20));
    }

    [Fact]
    public void A_disposed_source_draws_nothing()
    {
        //Arrange
        ImageSource source = ImageSource.FromRgba(new byte[] { 0, 0, 255, 255 }, 1, 1);
        using DrawingSession session = CreateSession();
        session.DrawImage(source, new RectangleF(0, 0, 80, 40));

        //Act
        source.Dispose();
        byte[] pixels = session.ExportPixels(Frame, includeBackground: false);

        //Assert
        TestAssets.CountInked(pixels).Should().Be(0);
    }

    [Fact]
    public void Constructor_rejects_a_source_rectangle_outside_the_picture()
    {
        //Arrange
        using ImageSource source = ImageSource.FromRgba(ImageSourceTests.Pixels(), 4, 2);

        //Act
        Action act = () => _ = new ImageShape(source, new RectangleF(0, 0, 10, 10), new Rectangle(2, 0, 3, 1));
        Action noSource = () => _ = new ImageShape(null, new RectangleF(0, 0, 10, 10));

        //Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        noSource.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(SpritePlayback.Loop, new[] { 0, 1, 2, 0, 1, 2, 0 })]
    [InlineData(SpritePlayback.PingPong, new[] { 0, 1, 2, 1, 0, 1, 2 })]
    [InlineData(SpritePlayback.Hold, new[] { 0, 1, 2, 2, 2, 2, 2 })]
    public void Sprite_frame_advances_with_time_by_its_playback_mode(SpritePlayback playback, int[] expected)
    {
        //Arrange
        using ImageSource image = ImageSource.FromRgba(new byte[12], 3, 1);
        using SpriteSheet sheet = SpriteSheet.FromGrid(image, 3, 1);
        var sprite = new SpriteShape(sheet, new RectangleF(0, 0, 10, 10), 10, playback);

        //Act
        var frames = new int[expected.Length];
        for (int i = 0; i < frames.Length; i++) { frames[i] = sprite.GetFrameAt(i / 10.0); }

        //Assert
        frames.Should().Equal(expected);
    }

    [Fact]
    public void Sprite_frame_index_and_playback_start_offset_the_running_frame()
    {
        //Arrange
        using ImageSource image = ImageSource.FromRgba(new byte[16], 4, 1);
        using SpriteSheet sheet = SpriteSheet.FromGrid(image, 4, 1);
        var timed = new SpriteShape(sheet, new RectangleF(0, 0, 10, 10), 2);
        var still = new SpriteShape(sheet, new RectangleF(0, 0, 10, 10));

        //Act
        timed.FrameIndex = 1;
        timed.PlaybackStartTime = 10;
        still.FrameIndex = 6;

        //Assert
        timed.GetFrameAt(10).Should().Be(1);
        timed.GetFrameAt(11).Should().Be(3);
        timed.GetFrameAt(9.5).Should().Be(0);
        still.GetFrameAt(123).Should().Be(2);
        still.IsDynamic.Should().BeTrue();
    }

    [Fact]
    public void Sprite_shows_the_frame_for_the_session_time_live_and_exported()
    {
        //Arrange - a red frame then a green frame, 1 frame per second
        using ImageSource image = ImageSource.FromRgba(new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }, 2, 1);
        using SpriteSheet sheet = SpriteSheet.FromGrid(image, 2, 1);
        using DrawingSession session = CreateSession();
        session.DrawSprite(sheet, new RectangleF(0, 0, 80, 40), 1);
        var info = new SKImageInfo(80, 40, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        //Act
        session.Render(canvas, info);
        byte[] first = session.ExportPixels(Frame, includeBackground: false);
        session.Time = 1.5;
        session.Render(canvas, info);
        byte[] second = session.ExportPixels(Frame, includeBackground: false);
        SKColor livePixel = bitmap.GetPixel(40, 20);

        //Assert
        TestAssets.PixelAt(first, 80, 40, 20).Should().Be(new Rgba32(255, 0, 0, 255));
        TestAssets.PixelAt(second, 80, 40, 20).Should().Be(new Rgba32(0, 255, 0, 255));
        livePixel.Green.Should().Be(255);
        livePixel.Red.Should().Be(0);
    }

    [Fact]
    public void Sprite_frame_index_binding_rounds_down()
    {
        //Arrange
        using ImageSource image = ImageSource.FromRgba(new byte[16], 4, 1);
        using SpriteSheet sheet = SpriteSheet.FromGrid(image, 4, 1);
        using DrawingSession session = CreateSession();
        SpriteShape sprite = session.DrawSprite(sheet, new RectangleF(0, 0, 10, 10));
        session.Animations.Bind(sprite, AnimatedProperty.FrameIndex, new FloatTrack { { 0, 0f }, { 1, 4f } });

        //Act
        session.Time = 0.6;

        //Assert
        sprite.FrameIndex.Should().Be(2);
    }

    [Fact]
    public void DrawSprite_passes_its_sampling_and_defaults_to_linear()
    {
        //Arrange
        using ImageSource image = ImageSource.FromRgba(new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }, 2, 1);
        using SpriteSheet sheet = SpriteSheet.FromGrid(image, 2, 1);
        using DrawingSession session = CreateSession();

        //Act
        SpriteShape smooth = session.DrawSprite(sheet, new RectangleF(0, 0, 80, 40), 1);
        SpriteShape blocky = session.DrawSprite(sheet, new RectangleF(0, 0, 80, 40), 1, SpritePlayback.Hold, ImageSampling.Nearest);

        //Assert
        smooth.Sampling.Should().Be(ImageSampling.Linear);
        default(ImageSampling).Should().Be(ImageSampling.Linear);
        blocky.Sampling.Should().Be(ImageSampling.Nearest);
        blocky.Playback.Should().Be(SpritePlayback.Hold);
    }
}
