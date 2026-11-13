using System;
using CodeBrix.Imaging.Drawing.Extensions;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionPixelExportTests
{
    private static DrawingSession CreateSession(Color background)
    {
        var session = new DrawingSession(new DrawingSessionOptions { BackgroundFillColor = background, LayerOpacity = 255 });
        session.AddLayer("Ink", Color.FromRgb(200, 40, 90));
        session.DrawCircle(500, 500, 300, thickness: 40);
        session.DrawRectangle(100, 100, 200, 150, filled: true, color: Color.FromRgba(20, 160, 60, 128));
        return session;
    }

    [Fact]
    public void ExportPixels_matches_the_imaging_image_export()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);
        var size = new Size(120, 120);

        //Act
        byte[] pixels = session.ExportPixels(size);
        using Image<Rgba32> image = session.ExportImagingImage(size);

        //Assert - same bytes, same layout (R, G, B, A, rows top to bottom)
        pixels.Length.Should().Be(120 * 120 * 4);
        for (int y = 0; y < 120; y += 7)
        {
            for (int x = 0; x < 120; x += 5)
            {
                TestAssets.PixelAt(pixels, 120, x, y).Should().Be(image[x, y]);
            }
        }
    }

    [Fact]
    public void ExportPixels_with_a_transparent_background_keeps_straight_alpha()
    {
        //Arrange - transparent fill; a half-transparent rectangle over nothing
        using DrawingSession session = CreateSession(Color.Transparent);

        //Act
        byte[] pixels = session.ExportPixels(new Size(100, 100), includeBackground: false);

        //Assert - outside every shape fully transparent; the translucent rectangle keeps its
        //  full color with alpha 128 (straight, not premultiplied)
        TestAssets.PixelAt(pixels, 100, 50, 50).A.Should().Be((byte)0);
        Rgba32 translucent = TestAssets.PixelAt(pixels, 100, 15, 15);
        translucent.A.Should().BeInRange((byte)126, (byte)130);
        translucent.R.Should().BeInRange((byte)18, (byte)22);
        translucent.G.Should().BeInRange((byte)158, (byte)162);
        translucent.B.Should().BeInRange((byte)58, (byte)62);
    }

    [Fact]
    public void ExportPixels_into_a_span_fills_only_the_frame()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);
        var buffer = new byte[(64 * 32 * 4) + 8];
        buffer.AsSpan(64 * 32 * 4).Fill(0xAB);

        //Act
        session.ExportPixels(buffer.AsSpan(), new Size(64, 32));

        //Assert - the frame letterboxes the square drawing; the trailing bytes are untouched
        buffer[(64 * 32 * 4) + 7].Should().Be((byte)0xAB);
        TestAssets.PixelAt(buffer, 64, 32, 16).A.Should().Be((byte)255);
        TestAssets.PixelAt(buffer, 64, 2, 16).A.Should().Be((byte)0);
    }

    [Fact]
    public void ExportPixels_rejects_a_buffer_that_is_too_small()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);
        Action act = () => session.ExportPixels(new byte[100], new Size(10, 10));

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExportPixels_without_a_size_uses_the_default_export_size()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(40, 30) });

        //Act
        byte[] pixels = session.ExportPixels();

        //Assert
        pixels.Length.Should().Be(40 * 30 * 4);
    }

    [Fact]
    public void ExportPixels_to_a_new_array_takes_either_size_type()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.Transparent);

        //Act
        byte[] imaging = session.ExportPixels(new Size(120, 80), false);
        byte[] backend = session.ExportPixels(new SKSizeI(120, 80), false);
        Action empty = () => session.ExportPixels(new SKSizeI(0, 80));

        //Assert
        backend.Should().Equal(imaging);
        empty.Should().Throw<ArgumentOutOfRangeException>();
    }
}
