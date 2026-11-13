using System;
using System.Linq;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingRandomTests
{
    //Reference values computed with an independent implementation of the documented algorithm
    //  (SplitMix64 seeding + xoshiro256**); they pin the sequence for every future release

    [Fact]
    public void NextUInt64_matches_the_reference_sequence_for_seed_0()
    {
        //Arrange
        var random = new DrawingRandom(0);

        //Act
        ulong[] values = Enumerable.Range(0, 4).Select(_ => random.NextUInt64()).ToArray();

        //Assert
        values.Should().Equal(0x99EC5F36CB75F2B4UL, 0xBF6E1F784956452AUL, 0x1A5F849D4933E6E0UL, 0x6AA594F1262D2D2CUL);
    }

    [Fact]
    public void NextUInt64_matches_the_reference_sequence_for_seed_42()
    {
        //Arrange
        var random = new DrawingRandom(42);

        //Act
        ulong[] values = Enumerable.Range(0, 4).Select(_ => random.NextUInt64()).ToArray();

        //Assert
        values.Should().Equal(0x15780B2E0C2EC716UL, 0x6104D9866D113A7EUL, 0xAE17533239E499A1UL, 0xECB8AD4703B360A1UL);
    }

    [Fact]
    public void NextUInt64_matches_the_reference_sequence_for_a_negative_seed()
    {
        //Arrange
        var random = new DrawingRandom(-1);

        //Act
        ulong first = random.NextUInt64();

        //Assert
        first.Should().Be(0x8F5520D52A7EAD08UL);
    }

    [Fact]
    public void NextDouble_uses_the_top_53_bits()
    {
        //Arrange
        var random = new DrawingRandom(12345);

        //Act
        double[] values = Enumerable.Range(0, 3).Select(_ => random.NextDouble()).ToArray();

        //Assert
        values.Should().Equal(0.7438081631565894, 0.13004553462783452, 0.9633344930128545);
    }

    [Fact]
    public void NextSingle_uses_the_top_24_bits()
    {
        //Arrange
        var a = new DrawingRandom(0);
        var b = new DrawingRandom(0);

        //Act
        float value = a.NextSingle();

        //Assert
        value.Should().Be((b.NextUInt64() >> 40) * (1f / 16777216f));
    }

    [Fact]
    public void ForKey_derives_the_documented_seed()
    {
        //Arrange + Act
        DrawingRandom keyed = DrawingRandom.ForKey(7, 3);

        //Assert
        keyed.Seed.Should().Be(5112237441497708052L);
        keyed.NextUInt64().Should().Be(0xD0EA68C108EC9A94UL);
    }

    [Fact]
    public void NextInt_matches_the_reference_lemire_draws()
    {
        //Arrange
        var random = new DrawingRandom(99);

        //Act
        int[] values = Enumerable.Range(0, 5).Select(_ => random.NextInt(10)).ToArray();

        //Assert
        values.Should().Equal(3, 5, 3, 8, 7);
    }

    [Fact]
    public void Same_seed_gives_the_same_sequence_and_different_seeds_differ()
    {
        //Arrange
        var a = new DrawingRandom(2024);
        var b = new DrawingRandom(2024);
        var c = new DrawingRandom(2025);

        //Act
        double[] first = Enumerable.Range(0, 100).Select(_ => a.Normal(5, 2)).ToArray();
        double[] second = Enumerable.Range(0, 100).Select(_ => b.Normal(5, 2)).ToArray();
        double[] third = Enumerable.Range(0, 100).Select(_ => c.Normal(5, 2)).ToArray();

        //Assert
        second.Should().Equal(first);
        third.Should().NotEqual(first);
    }

    [Fact]
    public void Uniform_stays_in_range_and_is_low_plus_span_times_next_double()
    {
        //Arrange
        var random = new DrawingRandom(5);
        var twin = new DrawingRandom(5);

        //Act + Assert
        for (int i = 0; i < 1000; i++)
        {
            double value = random.Uniform(-3, 7);
            value.Should().Be(-3 + (10 * twin.NextDouble()));
            value.Should().BeInRange(-3, 7);
        }
    }

    [Fact]
    public void Normal_has_the_requested_mean_and_deviation()
    {
        //Arrange
        var random = new DrawingRandom(77);
        const int Count = 200000;

        //Act
        double[] values = Enumerable.Range(0, Count).Select(_ => random.Normal(10, 3)).ToArray();
        double mean = values.Average();
        double deviation = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Sum() / Count);

        //Assert
        mean.Should().BeApproximately(10, 0.05);
        deviation.Should().BeApproximately(3, 0.05);
    }

    [Fact]
    public void NextInt_covers_the_range_evenly()
    {
        //Arrange
        var random = new DrawingRandom(3);
        var counts = new int[7];

        //Act
        for (int i = 0; i < 70000; i++) { counts[random.NextInt(7)]++; }

        //Assert
        foreach (int count in counts) { count.Should().BeInRange(9500, 10500); }
    }

    [Fact]
    public void NextInt_with_minimum_offsets_the_range()
    {
        //Arrange
        var random = new DrawingRandom(8);

        //Act + Assert
        for (int i = 0; i < 1000; i++) { random.NextInt(-5, 5).Should().BeInRange(-5, 4); }
    }

    [Fact]
    public void NextInt_rejects_empty_ranges()
    {
        //Arrange
        var random = new DrawingRandom(1);
        Action zero = () => random.NextInt(0);
        Action inverted = () => random.NextInt(5, 5);

        //Act + Assert
        zero.Should().Throw<ArgumentOutOfRangeException>();
        inverted.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForKey_streams_are_independent_of_each_other()
    {
        //Arrange
        DrawingRandom a = DrawingRandom.ForKey(1, 0);
        DrawingRandom b = DrawingRandom.ForKey(1, 1);
        DrawingRandom a2 = DrawingRandom.ForKey(1, 0);

        //Act
        b.NextUInt64();

        //Assert
        a.NextUInt64().Should().Be(a2.NextUInt64());
        DrawingRandom.ForKey(1, 0).Seed.Should().NotBe(DrawingRandom.ForKey(1, 1).Seed);
    }
}
