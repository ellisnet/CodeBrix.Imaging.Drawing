using System;
using System.Runtime.InteropServices;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Raster;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// The managed engine keeps recent stroke outlines so a stroked path drawn again with only its
/// transform changed is not stroked again. The cache only skips work: every pixel must be what an
/// uncached draw gives, byte for byte.
/// </summary>
[Collection("StrokeOutlineCache")]
public class StrokeOutlineCacheTests
{
    private static readonly Size Frame = new Size(160, 120);

    //Strokes of every kind (curves, dashes, caps, joins, a hairline-free thin line), moving and turning
    private static DrawingSession CreateScene()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.Black });
        session.AddLayer("Strokes", Color.White);
        PathGeometry wave = PathGeometry.Parse("M 10 60 C 40 10, 70 110, 100 60 S 150 20, 150 60");
        var dashed = new PathShape(wave, new PathStrokeStyle(5, PathStrokeCap.Butt, PathStrokeJoin.Miter, 4, new[] { 12f, 6f }), Color.Orange);
        var star = new PathShape(PathGeometry.Star(new PointF(40, 40), 25, 10, 5), new PathStrokeStyle(3, PathStrokeCap.Square, PathStrokeJoin.Bevel), Color.Cyan);
        var thin = new PathShape(PathGeometry.Circle(new PointF(120, 80), 20), 0.6f, Color.Yellow);
        session.DrawShape(dashed);
        session.DrawShape(star);
        session.DrawShape(thin);
        session.Animations.Bind(dashed, AnimatedProperty.Position, new PointTrack { { 0, new PointF(-10, 0) }, { 1, new PointF(10, 7.5f) } });
        session.Animations.Bind(star, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 1, 90f } });
        session.Animations.Bind(star, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(40, 40) } });
        session.Animations.Bind(thin, AnimatedProperty.Scale, new FloatTrack { { 0, 1f }, { 1, 1.5f } });
        return session;
    }

    private static byte[][] Frames(DrawingSession session)
    {
        var frames = new byte[11][];
        for (int i = 0; i <= 10; i++)
        {
            session.Time = i / 10.0;
            frames[i] = session.ExportPixels(Frame);
        }
        return frames;
    }

    private static byte[][] LiveFrames(DrawingSession session)
    {
        var info = new DrawingImageInfo(160, 120, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);
        using var bitmap = new DrawingBitmap(info);
        using var canvas = new DrawingCanvas(bitmap);
        var frames = new byte[11][];
        for (int i = 0; i <= 10; i++)
        {
            session.Time = i / 10.0;
            session.Render(canvas, info);
            frames[i] = bitmap.Bytes;
        }
        return frames;
    }

    [Fact]
    public void Cached_and_uncached_strokes_give_the_same_bytes_exported_and_live()
    {
        //Arrange
        byte[][] uncached;
        byte[][] uncachedLive;
        try
        {
            StrokeOutlineCache.Enabled = false;
            using DrawingSession plain = CreateScene();
            uncached = Frames(plain);
            uncachedLive = LiveFrames(plain);
        }
        finally
        {
            StrokeOutlineCache.Enabled = true;
        }

        //Act - a cold cache, then the same frames again (hits)
        StrokeOutlineCache.Clear();
        using DrawingSession cached = CreateScene();
        byte[][] first = Frames(cached);
        int entries = StrokeOutlineCache.Count;
        byte[][] again = Frames(cached);
        byte[][] live = LiveFrames(cached);

        //Assert
        entries.Should().BeGreaterThan(0);
        for (int i = 0; i <= 10; i++)
        {
            first[i].Should().Equal(uncached[i]);
            again[i].Should().Equal(uncached[i]);
            live[i].Should().Equal(uncachedLive[i]);
        }
    }
}

/// <summary>A drawing surface over memory the caller owns.</summary>
public class DrawingSurfaceMemoryTests
{
    [Fact]
    public void A_surface_over_caller_memory_starts_from_its_pixels_and_writes_back_on_flush()
    {
        //Arrange - 8 x 4 pixels, rows padded to 40 bytes, the padding marked
        var info = new DrawingImageInfo(8, 4, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);
        const int rowBytes = 40;
        IntPtr memory = Marshal.AllocHGlobal(rowBytes * 4);
        try
        {
            var initial = new byte[rowBytes * 4];
            for (int i = 0; i < initial.Length; i++) { initial[i] = (byte)((i % rowBytes) < 32 ? 0 : 0xAB); }
            for (int x = 0; x < 8; x++) { initial[(1 * rowBytes) + (x * 4)] = 255; initial[(1 * rowBytes) + (x * 4) + 3] = 255; }
            Marshal.Copy(initial, 0, memory, initial.Length);

            //Act
            byte[] beforeFlush = new byte[initial.Length];
            byte[] afterFlush = new byte[initial.Length];
            using (DrawingSurface surface = DrawingSurface.Create(info, memory, rowBytes))
            {
                using var paint = new DrawingPaint { Color = new DrawingColor(0, 0, 255, 255) };
                surface.Canvas.DrawRect(new DrawingRect(0, 2, 8, 4), paint);
                Marshal.Copy(memory, beforeFlush, 0, beforeFlush.Length);
                surface.Flush();
                Marshal.Copy(memory, afterFlush, 0, afterFlush.Length);
                using DrawingImage snapshot = surface.Snapshot();
            }

            //Assert - nothing written until the flush; then the red row kept, the blue rows drawn, padding untouched
            beforeFlush.Should().Equal(initial);
            afterFlush[(1 * rowBytes) + 0].Should().Be(255);
            afterFlush[(2 * rowBytes) + 2].Should().Be(255);
            afterFlush[(3 * rowBytes) + 2].Should().Be(255);
            afterFlush[(0 * rowBytes) + 3].Should().Be(0);
            afterFlush[(2 * rowBytes) + 36].Should().Be(0xAB);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    [Fact]
    public void Creating_over_memory_checks_its_arguments()
    {
        //Arrange
        var info = new DrawingImageInfo(8, 4, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);

        //Act
        Action zero = () => DrawingSurface.Create(info, IntPtr.Zero, 32);
        Action narrow = () => DrawingSurface.Create(info, new IntPtr(1), 31);

        //Assert
        zero.Should().Throw<ArgumentNullException>();
        narrow.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Dispose_writes_the_pixels_back()
    {
        //Arrange
        var info = new DrawingImageInfo(2, 2, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);
        IntPtr memory = Marshal.AllocHGlobal(16);
        try
        {
            Marshal.Copy(new byte[16], 0, memory, 16);

            //Act
            using (DrawingSurface surface = DrawingSurface.Create(info, memory, 8))
            {
                surface.Canvas.Clear(new DrawingColor(10, 20, 30, 255));
            }
            var bytes = new byte[16];
            Marshal.Copy(memory, bytes, 0, 16);

            //Assert
            bytes[12].Should().Be(10);
            bytes[15].Should().Be(255);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }
}
