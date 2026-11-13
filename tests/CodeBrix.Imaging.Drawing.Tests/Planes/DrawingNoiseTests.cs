using System;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingNoiseTests
{
    [Fact]
    public void Gradient_noise_is_zero_at_lattice_points()
    {
        //Arrange + Act + Assert
        for (int y = -3; y <= 3; y++)
        {
            for (int x = -3; x <= 3; x++)
            {
                DrawingNoise.Gradient(x, y, 11).Should().Be(0);
                DrawingNoise.Gradient(x, y, 2.0, 11).Should().Be(0);
            }
        }
    }

    [Fact]
    public void Value_noise_at_a_lattice_point_is_the_hashed_lattice_value()
    {
        //Arrange - h = Mix(Mix(Mix(seed) + ix) + iy), value = (h >> 11) * 2^-53 * 2 - 1
        ulong h = DrawingRandom.Mix(DrawingRandom.Mix(DrawingRandom.Mix(5UL) + 3UL) + unchecked((ulong)-2L));
        double expected = ((h >> 11) * (1.0 / 9007199254740992.0) * 2.0) - 1.0;

        //Act
        double value = DrawingNoise.Value(3, -2, 5);

        //Assert
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData(DrawingNoiseKind.Value)]
    [InlineData(DrawingNoiseKind.Gradient)]
    public void Noise_stays_within_minus_one_to_one(DrawingNoiseKind kind)
    {
        //Arrange
        var random = new DrawingRandom(4);
        double low = 0, high = 0;

        //Act
        for (int i = 0; i < 20000; i++)
        {
            double x = random.Uniform(-50, 50), y = random.Uniform(-50, 50), z = random.Uniform(-50, 50);
            double a = DrawingNoise.Sample(kind, x, y, 9);
            double b = DrawingNoise.Sample(kind, x, y, z, 9);
            double c = DrawingNoise.Fractal(kind, x, y, z, 9, 5);
            low = Math.Min(low, Math.Min(a, Math.Min(b, c)));
            high = Math.Max(high, Math.Max(a, Math.Max(b, c)));
        }

        //Assert - bounded, and actually uses a good part of the range
        low.Should().BeGreaterThanOrEqualTo(-1);
        high.Should().BeLessThanOrEqualTo(1);
        low.Should().BeLessThan(-0.4);
        high.Should().BeGreaterThan(0.4);
    }

    [Theory]
    [InlineData(DrawingNoiseKind.Value)]
    [InlineData(DrawingNoiseKind.Gradient)]
    public void Noise_is_continuous(DrawingNoiseKind kind)
    {
        //Arrange
        var random = new DrawingRandom(6);

        //Act + Assert - a tiny step never makes a jump
        for (int i = 0; i < 2000; i++)
        {
            double x = random.Uniform(-20, 20), y = random.Uniform(-20, 20), t = random.Uniform(0, 10);
            double here = DrawingNoise.Sample(kind, x, y, t, 1);
            double near = DrawingNoise.Sample(kind, x + 1e-4, y - 1e-4, t + 1e-4, 1);
            Math.Abs(here - near).Should().BeLessThan(0.01);
        }
    }

    [Fact]
    public void Noise_is_deterministic_and_seed_dependent()
    {
        //Arrange + Act
        double a = DrawingNoise.Gradient(1.25, 7.5, 0.5, 100);
        double b = DrawingNoise.Gradient(1.25, 7.5, 0.5, 100);
        double c = DrawingNoise.Gradient(1.25, 7.5, 0.5, 101);

        //Assert
        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void Fractal_with_one_octave_equals_plain_noise()
    {
        //Arrange + Act + Assert
        DrawingNoise.Fractal(DrawingNoiseKind.Gradient, 3.3, 4.4, 8, octaves: 1)
            .Should().Be(DrawingNoise.Gradient(3.3, 4.4, 8));
        DrawingNoise.Fractal(DrawingNoiseKind.Value, 3.3, 4.4, 1.1, 8, octaves: 1)
            .Should().Be(DrawingNoise.Value(3.3, 4.4, 1.1, 8));
    }

    [Fact]
    public void Fractal_sums_octaves_with_documented_weights()
    {
        //Arrange
        const double x = 0.37, y = 1.91;
        double expected = (DrawingNoise.Value(x, y, 20) + (0.5 * DrawingNoise.Value(x * 2, y * 2, 21))) / 1.5;

        //Act
        double value = DrawingNoise.Fractal(DrawingNoiseKind.Value, x, y, 20, octaves: 2);

        //Assert
        value.Should().Be(expected);
    }

    [Fact]
    public void Fractal_rejects_bad_octave_counts()
    {
        //Arrange
        Action none = () => DrawingNoise.Fractal(DrawingNoiseKind.Value, 0, 0, 1, octaves: 0);
        Action many = () => DrawingNoise.Fractal(DrawingNoiseKind.Value, 0, 0, 1, octaves: 31);

        //Act + Assert
        none.Should().Throw<ArgumentOutOfRangeException>();
        many.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fill_samples_pixel_centres()
    {
        //Arrange
        var plane = new DrawingPlane(7, 5);

        //Act
        DrawingNoise.Fill(plane, DrawingNoiseKind.Gradient, 42, 0.1, z: 2.5, octaves: 3, offsetX: 4, offsetY: -1);

        //Assert
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                double expected = DrawingNoise.Fractal(DrawingNoiseKind.Gradient, (x + 0.5 + 4) * 0.1, (y + 0.5 - 1) * 0.1, 2.5, 42, 3);
                plane[x, y].Should().Be((float)expected);
            }
        }
    }

    [Fact]
    public void Fill_without_time_uses_2d_noise()
    {
        //Arrange
        var plane = new DrawingPlane(3, 3);

        //Act
        DrawingNoise.Fill(plane, DrawingNoiseKind.Value, 3, 0.25);

        //Assert
        plane[1, 2].Should().Be((float)DrawingNoise.Value(1.5 * 0.25, 2.5 * 0.25, 3));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(0)]
    public void Fill_is_identical_for_every_thread_count(int threads)
    {
        //Arrange
        DrawingPlane expected;
        int previous = PlaneKernels.MaxDegreeOfParallelism;
        try
        {
            PlaneKernels.MaxDegreeOfParallelism = 1;
            expected = DrawingNoise.Fill(new DrawingPlane(256, 128), DrawingNoiseKind.Gradient, 5, 0.03, 1.0, 4);

            //Act
            PlaneKernels.MaxDegreeOfParallelism = threads;
            DrawingPlane actual = DrawingNoise.Fill(new DrawingPlane(256, 128), DrawingNoiseKind.Gradient, 5, 0.03, 1.0, 4);

            //Assert
            actual.Data.Should().Equal(expected.Data);
        }
        finally
        {
            PlaneKernels.MaxDegreeOfParallelism = previous;
        }
    }

    [Fact]
    public void Time_moves_the_noise_smoothly()
    {
        //Arrange
        var a = new DrawingPlane(64, 64);
        var b = new DrawingPlane(64, 64);
        var c = new DrawingPlane(64, 64);

        //Act
        DrawingNoise.Fill(a, DrawingNoiseKind.Gradient, 1, 0.05, 0.0);
        DrawingNoise.Fill(b, DrawingNoiseKind.Gradient, 1, 0.05, 0.01);
        DrawingNoise.Fill(c, DrawingNoiseKind.Gradient, 1, 0.05, 0.5);

        //Assert - a small time step changes little, a big one changes a lot
        double small = 0, large = 0;
        for (int i = 0; i < a.Length; i++)
        {
            small = Math.Max(small, Math.Abs(a.Data[i] - b.Data[i]));
            large = Math.Max(large, Math.Abs(a.Data[i] - c.Data[i]));
        }
        small.Should().BeLessThan(0.05);
        large.Should().BeGreaterThan(0.2);
    }
}
