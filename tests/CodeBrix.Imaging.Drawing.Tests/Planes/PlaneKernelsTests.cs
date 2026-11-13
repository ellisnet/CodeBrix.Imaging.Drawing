using System;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class PlaneKernelsTests
{
    //Runs a pipeline with the row-parallel kernels limited to `threads` threads (0 = no limit)
    private static T WithThreads<T>(int threads, Func<T> run)
    {
        int previous = PlaneKernels.MaxDegreeOfParallelism;
        try
        {
            PlaneKernels.MaxDegreeOfParallelism = threads;
            return run();
        }
        finally
        {
            PlaneKernels.MaxDegreeOfParallelism = previous;
        }
    }

    //A large, busy, partly transparent frame put through every row-parallel operation
    private static byte[] Pipeline()
    {
        DrawingPlaneImage image = new DrawingPlaneImage(320, 180).FillParallel((x, y) => new Vector4(
            MathF.Sin(x * 0.05f) + 1f, (y % 17) / 16f, ((x * y) % 255) / 128f, x < 40 ? 0f : ((x + y) % 9) / 8f));
        DrawingPlaneImage glow = image.CreateBloom(4, new[] { (2f, 0.8f), (6f, 0.9f) }, 0.6f);
        image.CompositeAdd(glow).GaussianBlur(2.5f).ApplyVignette(160f, 86f, 240f, 110f, 0.38f, 0.4f).ApplyToneCurve(0.15f, 1.08f);
        DrawingPlaneImage half = image.Downsampled(2).Resized(320, 180, DrawingPlaneResampling.Bilinear);
        DrawingNoise.Fill(half.A, DrawingNoiseKind.Gradient, 3, 0.02, 0.5, 3);
        image.Lerp(half, 0.3f);
        return image.ToRgba();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(0)]
    public void Results_do_not_depend_on_the_thread_count(int threads)
    {
        //Arrange
        byte[] singleThreaded = WithThreads(1, Pipeline);

        //Act
        byte[] parallel = WithThreads(threads, Pipeline);

        //Assert
        parallel.Should().Equal(singleThreaded);
    }

    [Fact]
    public void Results_are_identical_across_repeated_runs()
    {
        //Arrange + Act
        byte[] first = Pipeline();
        byte[] second = Pipeline();

        //Assert
        second.Should().Equal(first);
    }

    [Fact]
    public void UnitToByte_round_trips_every_byte()
    {
        //Arrange + Act + Assert
        for (int b = 0; b < 256; b++)
        {
            PlaneKernels.UnitToByte(PlaneKernels.ByteToUnit((byte)b)).Should().Be((byte)b);
        }
    }

    [Theory]
    [InlineData(0.502f / 255f, 1)]
    [InlineData(0.5f, 128)]
    [InlineData(0.49f / 255f, 0)]
    [InlineData(254.52f / 255f, 255)]
    [InlineData(-3f, 0)]
    [InlineData(7f, 255)]
    [InlineData(float.NaN, 0)]
    [InlineData(float.PositiveInfinity, 255)]
    public void UnitToByte_rounds_half_up_and_clamps(float value, int expected)
    {
        //Arrange + Act
        byte result = PlaneKernels.UnitToByte(value);

        //Assert
        result.Should().Be((byte)expected);
    }

    [Fact]
    public void MixBytes_with_equal_alpha_is_the_straight_formula()
    {
        //Arrange
        var destination = new byte[4];

        //Act
        PlaneKernels.MixBytes(10, 20, 30, 77, 110, 120, 130, 77, 0.25f, destination, 0);

        //Assert
        destination.Should().Equal(new byte[]
        {
            (byte)((10 * 0.75f) + (110 * 0.25f) + 0.5f),
            (byte)((20 * 0.75f) + (120 * 0.25f) + 0.5f),
            (byte)((30 * 0.75f) + (130 * 0.25f) + 0.5f),
            77,
        });
    }

    [Fact]
    public void MixUnit_matches_MixBytes_on_exactly_representable_values()
    {
        //Arrange
        var destination = new byte[4];

        //Act
        PlaneKernels.MixUnit(1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0.5f, out float r, out float g, out float b, out float a);
        PlaneKernels.MixBytes(255, 0, 0, 255, 0, 0, 255, 0, 0.5f, destination, 0);

        //Assert - an opaque pixel fading to transparent keeps its colour
        new Vector4(r, g, b, a).Should().Be(new Vector4(1f, 0f, 0f, 0.5f));
        destination.Should().Equal(new byte[] { 255, 0, 0, 128 });
    }
}
