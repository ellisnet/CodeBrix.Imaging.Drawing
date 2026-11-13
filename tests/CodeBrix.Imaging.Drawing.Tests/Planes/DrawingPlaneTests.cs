using System;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingPlaneTests
{
    private static DrawingPlane Row(params float[] values) => new DrawingPlane(values.Length, 1, (float[])values.Clone());

    [Fact]
    public void Constructor_creates_zeroed_row_major_plane()
    {
        //Arrange + Act
        var plane = new DrawingPlane(3, 2);

        //Assert
        plane.Width.Should().Be(3);
        plane.Height.Should().Be(2);
        plane.Length.Should().Be(6);
        plane.Size.Should().Be(new Size(3, 2));
        plane.Data.Should().AllBeEquivalentTo(0f);
    }

    [Fact]
    public void Indexer_reads_and_writes_row_major_data()
    {
        //Arrange
        var plane = new DrawingPlane(3, 2);

        //Act
        plane[2, 1] = 5f;

        //Assert
        plane.Data[(1 * 3) + 2].Should().Be(5f);
        plane[2, 1].Should().Be(5f);
        plane.GetRow(1)[2].Should().Be(5f);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 5)]
    public void Constructor_rejects_empty_sizes(int width, int height)
    {
        //Arrange
        Action act = () => _ = new DrawingPlane(width, height);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_over_array_wraps_without_copying()
    {
        //Arrange
        var data = new float[] { 1f, 2f, 3f, 4f };

        //Act
        var plane = new DrawingPlane(2, 2, data);
        data[3] = 9f;

        //Assert
        plane.Data.Should().BeSameAs(data);
        plane[1, 1].Should().Be(9f);
    }

    [Fact]
    public void Constructor_over_array_rejects_wrong_length()
    {
        //Arrange
        Action act = () => _ = new DrawingPlane(2, 2, new float[3]);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Indexer_rejects_positions_outside_the_plane()
    {
        //Arrange
        var plane = new DrawingPlane(2, 2);
        Action act = () => _ = plane[2, 0];

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Clone_is_independent()
    {
        //Arrange
        var plane = new DrawingPlane(2, 2, 3f);

        //Act
        DrawingPlane copy = plane.Clone();
        copy[0, 0] = 7f;

        //Assert
        plane[0, 0].Should().Be(3f);
        copy.Data.Should().NotBeSameAs(plane.Data);
    }

    [Fact]
    public void Fill_rectangle_is_clipped_to_the_plane()
    {
        //Arrange
        var plane = new DrawingPlane(4, 3);

        //Act
        plane.Fill(new Rectangle(2, 1, 10, 10), 1f);

        //Assert
        plane.Data.Should().Equal(
            0f, 0f, 0f, 0f,
            0f, 0f, 1f, 1f,
            0f, 0f, 1f, 1f);
    }

    [Fact]
    public void Fill_rectangle_outside_changes_nothing()
    {
        //Arrange
        var plane = new DrawingPlane(4, 3);

        //Act
        plane.Fill(new Rectangle(-5, -5, 3, 3), 1f);

        //Assert
        plane.Data.Should().AllBeEquivalentTo(0f);
    }

    [Fact]
    public void Fill_function_receives_absolute_positions()
    {
        //Arrange
        var plane = new DrawingPlane(3, 3);

        //Act
        plane.Fill(new Rectangle(1, 1, 2, 2), (x, y) => (x * 10) + y);

        //Assert
        plane.Data.Should().Equal(
            0f, 0f, 0f,
            0f, 11f, 21f,
            0f, 12f, 22f);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(0)]
    public void FillParallel_matches_Fill_for_every_thread_count(int threads)
    {
        //Arrange
        Func<int, int, float> function = (x, y) => MathF.Sin(x * 0.37f) * MathF.Cos(y * 0.11f) + (x * y * 1e-4f);
        DrawingPlane expected = new DrawingPlane(300, 257).Fill(function);

        //Act
        DrawingPlane actual = new DrawingPlane(300, 257).FillParallel(function, threads);

        //Assert
        actual.Data.Should().Equal(expected.Data);
    }

    [Fact]
    public void Add_with_scale_and_subtract_follow_the_formula()
    {
        //Arrange
        DrawingPlane plane = Row(1f, 2f, 3f);
        DrawingPlane other = Row(0.5f, 1f, -2f);

        //Act
        plane.Add(other, 2f).Subtract(Row(1f, 1f, 1f)).Add(0.25f);

        //Assert
        plane.Data.Should().Equal(1.25f, 3.25f, -1.75f);
    }

    [Fact]
    public void Values_are_not_clamped()
    {
        //Arrange
        var plane = new DrawingPlane(1, 1, 0.75f);

        //Act
        plane.Add(new DrawingPlane(1, 1, 0.75f));

        //Assert
        plane[0, 0].Should().Be(1.5f);
    }

    [Fact]
    public void Multiply_by_constant_and_plane()
    {
        //Arrange
        DrawingPlane plane = Row(1f, 2f, 3f);

        //Act
        plane.Multiply(2f).Multiply(Row(0.5f, 0f, -1f));

        //Assert
        plane.Data.Should().Equal(1f, 0f, -6f);
    }

    [Fact]
    public void Lerp_uses_a_times_one_minus_t_plus_b_times_t()
    {
        //Arrange
        DrawingPlane plane = Row(0f, 1f, 0.3f);
        DrawingPlane other = Row(1f, 0f, 0.9f);
        const float t = 0.2f;
        float s = 1f - t;

        //Act
        plane.Lerp(other, t);

        //Assert
        plane.Data.Should().Equal((0f * s) + (1f * t), (1f * s) + (0f * t), (0.3f * s) + (0.9f * t));
    }

    [Fact]
    public void Lerp_with_mask_mixes_per_pixel()
    {
        //Arrange
        DrawingPlane plane = Row(0f, 0f, 0f);

        //Act
        plane.Lerp(Row(1f, 1f, 1f), Row(0f, 0.5f, 1f));

        //Assert
        plane.Data.Should().Equal(0f, 0.5f, 1f);
    }

    [Fact]
    public void Min_Max_and_Clamp_limit_values()
    {
        //Arrange
        DrawingPlane a = Row(-1f, 0.5f, 2f);
        DrawingPlane b = Row(-1f, 0.5f, 2f);
        DrawingPlane c = Row(-1f, 0.5f, 2f, float.NaN);

        //Act
        a.Min(1f);
        b.Max(0f);
        c.Clamp(0f, 1f);

        //Assert
        a.Data.Should().Equal(-1f, 0.5f, 1f);
        b.Data.Should().Equal(0f, 0.5f, 2f);
        c.Data.Should().Equal(0f, 0.5f, 1f, 0f);
    }

    [Fact]
    public void Min_and_Max_with_planes_take_the_per_pixel_extreme()
    {
        //Arrange
        DrawingPlane a = Row(1f, 5f);
        DrawingPlane b = Row(1f, 5f);

        //Act
        a.Min(Row(2f, 3f));
        b.Max(Row(2f, 3f));

        //Assert
        a.Data.Should().Equal(1f, 3f);
        b.Data.Should().Equal(2f, 5f);
    }

    [Fact]
    public void Clamp_rejects_inverted_range()
    {
        //Arrange
        Action act = () => Row(1f).Clamp(1f, 0f);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Arithmetic_rejects_planes_of_another_size()
    {
        //Arrange
        Action act = () => new DrawingPlane(2, 2).Add(new DrawingPlane(2, 3));

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyToneCurve_is_v_over_one_plus_k_v_times_gain()
    {
        //Arrange
        DrawingPlane plane = Row(0f, 0.5f, 1f, 4f);

        //Act
        plane.ApplyToneCurve(0.15f, 1.08f);

        //Assert
        plane.Data.Should().Equal(
            0f,
            0.5f / (1f + (0.15f * 0.5f)) * 1.08f,
            1f / (1f + (0.15f * 1f)) * 1.08f,
            4f / (1f + (0.15f * 4f)) * 1.08f);
    }

    [Fact]
    public void ApplyGamma_raises_positive_values_and_leaves_the_rest()
    {
        //Arrange
        DrawingPlane plane = Row(0.25f, 0f, -0.5f, 4f);

        //Act
        plane.ApplyGamma(0.5f);

        //Assert
        plane.Data.Should().Equal(MathF.Pow(0.25f, 0.5f), 0f, -0.5f, MathF.Pow(4f, 0.5f));
    }

    [Fact]
    public void ApplyGamma_rejects_non_positive_exponent()
    {
        //Arrange
        Action act = () => Row(1f).ApplyGamma(0f);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Apply_runs_function_on_every_value()
    {
        //Arrange
        DrawingPlane plane = Row(1f, 2f);

        //Act
        plane.Apply(v => (v * v) + 1f);

        //Assert
        plane.Data.Should().Equal(2f, 5f);
    }

    [Fact]
    public void CreateVignette_follows_the_documented_formula()
    {
        //Arrange
        const float cx = 4f, cy = 2f, rx = 3f, ry = 2f, strength = 0.38f, minimum = 0.4f;

        //Act
        DrawingPlane vignette = DrawingPlane.CreateVignette(9, 5, cx, cy, rx, ry, strength, minimum);

        //Assert
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                double dx = (x - (double)cx) / rx, dy = (y - (double)cy) / ry;
                double expected = Math.Clamp(1 - (strength * ((dx * dx) + (dy * dy))), minimum, 1);
                vignette[x, y].Should().Be((float)expected);
            }
        }
        vignette[4, 2].Should().Be(1f);
        vignette[0, 0].Should().Be(0.4f);
    }

    [Fact]
    public void ApplyVignette_equals_multiplying_by_CreateVignette()
    {
        //Arrange
        DrawingPlane plane = new DrawingPlane(140, 130).Fill((x, y) => 0.3f + (x * 0.01f));
        DrawingPlane expected = plane.Clone().Multiply(DrawingPlane.CreateVignette(140, 130, 70f, 60f, 90f, 80f, 0.5f, 0.2f));

        //Act
        plane.ApplyVignette(70f, 60f, 90f, 80f, 0.5f, 0.2f);

        //Assert
        plane.Data.Should().Equal(expected.Data);
    }

    [Theory]
    [InlineData(1f, 4)]
    [InlineData(3f, 12)]
    [InlineData(0.1f, 0)]
    [InlineData(0.625f, 3)]
    public void GaussianKernel_radius_is_four_sigma_rounded(float sigma, int expectedRadius)
    {
        //Arrange + Act
        float[] kernel = PlaneKernels.GaussianKernel(sigma, out int radius);

        //Assert
        radius.Should().Be(expectedRadius);
        kernel.Length.Should().Be((2 * expectedRadius) + 1);
    }

    [Fact]
    public void GaussianKernel_weights_are_double_gaussians_normalised_in_double_then_rounded_once()
    {
        //Arrange
        const double sigma = 1.5;
        double sum = 0;
        for (int i = -6; i <= 6; i++) { sum += Math.Exp(-0.5 * i * i / (sigma * sigma)); }

        //Act
        float[] kernel = PlaneKernels.GaussianKernel(1.5f, out int radius);

        //Assert
        radius.Should().Be(6);
        for (int i = -6; i <= 6; i++)
        {
            kernel[i + 6].Should().Be((float)(Math.Exp(-0.5 * i * i / (sigma * sigma)) / sum));
        }
    }

    [Fact]
    public void GaussianKernel_small_case_matches_the_hand_derived_weights()
    {
        //Arrange - sigma 0.35: radius (int)(1.4 + 0.5) = 1; side weight exp(-0.5 / 0.35^2) = 0.0168798818,
        //  sum 1.0337597636, so side 0.0168798818 / 1.0337597636 and centre 1 / 1.0337597636. Rounding the
        //  side weight to float BEFORE dividing would give the next float up, 0.0163286328.
        const float side = 0.016328630968928337f;
        const float centre = 0.967342734336853f;

        //Act
        float[] kernel = PlaneKernels.GaussianKernel(0.35f, out int radius);

        //Assert
        radius.Should().Be(1);
        kernel.Should().Equal(side, centre, side);
        kernel[0].Should().NotBe(0.016328632831573486f);
    }

    [Fact]
    public void GaussianBlur_of_an_impulse_is_the_kernel_outer_product()
    {
        //Arrange
        var plane = new DrawingPlane(21, 21);
        plane[10, 10] = 1f;
        float[] kernel = PlaneKernels.GaussianKernel(1.2f, out int radius);

        //Act
        plane.GaussianBlur(1.2f);

        //Assert
        for (int y = 0; y < 21; y++)
        {
            for (int x = 0; x < 21; x++)
            {
                int kx = x - 10 + radius, ky = y - 10 + radius;
                float expected = (kx >= 0 && kx < kernel.Length && ky >= 0 && ky < kernel.Length) ? kernel[ky] * kernel[kx] : 0f;
                plane[x, y].Should().Be(expected);
            }
        }
    }

    [Fact]
    public void GaussianBlur_mirrors_at_the_edge_including_the_edge_pixel()
    {
        //Arrange - an impulse in the first column: column -1 mirrors to 0, -2 to 1, ...
        DrawingPlane plane = Row(1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        float[] kernel = PlaneKernels.GaussianKernel(1f, out int radius);

        //Vertically a one-row plane mirrors onto itself, so every row offset reads the same row
        float Vertical(float value)
        {
            float total = 0f;
            foreach (float weight in kernel) { total += weight * value; }
            return total;
        }

        //Act
        plane.GaussianBlur(1f);

        //Assert - pixel 0 sees the impulse at offsets -1 (mirrored) and 0; pixel 2 at -3 (mirrored)
        //  and -2; pixel 4 only at -4; pixel 5 is out of reach of both copies
        radius.Should().Be(4);
        plane[0, 0].Should().Be(Vertical((kernel[radius - 1] * 1f) + (kernel[radius] * 1f)));
        plane[2, 0].Should().Be(Vertical((kernel[radius - 3] * 1f) + (kernel[radius - 2] * 1f)));
        plane[4, 0].Should().Be(Vertical(kernel[0] * 1f));
        plane[5, 0].Should().Be(0f);
    }

    [Fact]
    public void GaussianBlur_keeps_a_flat_plane_flat()
    {
        //Arrange
        var plane = new DrawingPlane(30, 20, 0.5f);

        //Act
        plane.GaussianBlur(4f);

        //Assert
        foreach (float v in plane.Data) { v.Should().BeApproximately(0.5f, 1e-6f); }
    }

    [Fact]
    public void GaussianBlur_with_zero_sigma_does_nothing()
    {
        //Arrange
        DrawingPlane plane = Row(1f, 2f, 3f);

        //Act
        plane.GaussianBlur(0f);

        //Assert
        plane.Data.Should().Equal(1f, 2f, 3f);
    }

    [Fact]
    public void GaussianBlur_handles_kernels_wider_than_the_plane()
    {
        //Arrange
        DrawingPlane plane = Row(1f, 0f);

        //Act
        plane.GaussianBlur(5f);

        //Assert - mirroring repeats, so a two-pixel plane averages towards 0.5 and the sum stays near 1
        (plane[0, 0] + plane[1, 0]).Should().BeApproximately(1f, 1e-5f);
        plane[0, 0].Should().BeGreaterThan(plane[1, 0]);
    }

    [Fact]
    public void GaussianBlur_rejects_nan_sigma()
    {
        //Arrange
        Action act = () => Row(1f).GaussianBlur(float.NaN);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Resized_nearest_copies_the_pixel_under_each_centre()
    {
        //Arrange
        DrawingPlane plane = Row(10f, 20f, 30f, 40f);

        //Act
        DrawingPlane down = plane.Resized(2, 1, DrawingPlaneResampling.Nearest);
        DrawingPlane up = Row(1f, 2f).Resized(4, 1, DrawingPlaneResampling.Nearest);
        DrawingPlane odd = Row(1f, 2f, 3f).Resized(2, 1, DrawingPlaneResampling.Nearest);

        //Assert
        down.Data.Should().Equal(20f, 40f);
        up.Data.Should().Equal(1f, 1f, 2f, 2f);
        odd.Data.Should().Equal(1f, 3f);
    }

    [Fact]
    public void Resized_bilinear_uses_the_pixel_centre_grid()
    {
        //Arrange
        DrawingPlane plane = Row(0f, 1f);

        //Act
        DrawingPlane result = plane.Resized(4, 1, DrawingPlaneResampling.Bilinear);
        DrawingPlane byDefault = plane.Resized(4, 1);

        //Assert - positions -0.25 (clamped), 0.25, 0.75, 1.25 (clamped)
        result.Data.Should().Equal(0f, 0.25f, 0.75f, 1f);
        byDefault.Data.Should().Equal(result.Data);
        default(DrawingPlaneResampling).Should().Be(DrawingPlaneResampling.Bilinear);
    }

    [Fact]
    public void Resized_bilinear_aligned_corners_hits_both_ends()
    {
        //Arrange
        DrawingPlane plane = Row(0f, 1f);

        //Act
        DrawingPlane result = plane.Resized(4, 1, DrawingPlaneResampling.BilinearAlignedCorners);

        //Assert
        result.Data.Should().Equal(0f, (float)(1.0 / 3.0), (float)(2.0 / 3.0), 1f);
    }

    [Fact]
    public void Resized_bilinear_in_two_dimensions_follows_the_weight_formula()
    {
        //Arrange - 2x2 source stretched over 3x3 with corners aligned: the centre is the mean
        var plane = new DrawingPlane(2, 2, new[] { 0f, 1f, 2f, 3f });

        //Act
        DrawingPlane result = plane.Resized(3, 3, DrawingPlaneResampling.BilinearAlignedCorners);

        //Assert
        result.Data.Should().Equal(
            0f, 0.5f, 1f,
            1f, 1.5f, 2f,
            2f, 2.5f, 3f);
    }

    [Fact]
    public void Resized_from_a_single_pixel_fills_with_it()
    {
        //Arrange
        var plane = new DrawingPlane(1, 1, 0.7f);

        //Act
        DrawingPlane result = plane.Resized(3, 2, DrawingPlaneResampling.Bilinear);

        //Assert
        result.Data.Should().AllBeEquivalentTo(0.7f);
    }

    [Fact]
    public void Resized_leaves_the_original_unchanged()
    {
        //Arrange
        DrawingPlane plane = Row(0f, 1f);

        //Act
        DrawingPlane result = plane.Resized(4, 1);

        //Assert
        result.Should().NotBeSameAs(plane);
        plane.Data.Should().Equal(0f, 1f);
    }

    [Fact]
    public void Downsampled_averages_blocks_including_partial_edge_blocks()
    {
        //Arrange
        var plane = new DrawingPlane(3, 3, new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f });

        //Act
        DrawingPlane result = plane.Downsampled(2);

        //Assert
        result.Width.Should().Be(2);
        result.Height.Should().Be(2);
        result.Data.Should().Equal((1f + 2f + 4f + 5f) / 4f, (3f + 6f) / 2f, (7f + 8f) / 2f, 9f);
    }

    [Fact]
    public void Downsampled_rejects_zero_factor()
    {
        //Arrange
        Action act = () => Row(1f).Downsampled(0);

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateBloom_is_downsample_weighted_blurs_gain_then_resize()
    {
        //Arrange
        DrawingPlane light = new DrawingPlane(64, 48).Fill((x, y) => (x == 20 && y == 30) ? 8f : 0f);
        DrawingPlane small = light.Downsampled(4);
        DrawingPlane tight = small.Clone().GaussianBlur(1.5f);
        DrawingPlane wide = small.Clone().GaussianBlur(4f);
        DrawingPlane expected = new DrawingPlane(small.Width, small.Height).Add(tight, 0.8f).Add(wide, 0.9f).Multiply(0.6f)
            .Resized(64, 48, DrawingPlaneResampling.BilinearAlignedCorners);

        //Act
        DrawingPlane bloom = light.CreateBloom(4, new[] { (1.5f, 0.8f), (4f, 0.9f) }, 0.6f, null, DrawingPlaneResampling.BilinearAlignedCorners);

        //Assert
        bloom.Data.Should().Equal(expected.Data);
        light[20, 30].Should().Be(8f);
    }

    [Fact]
    public void CreateBloom_can_target_another_output_size()
    {
        //Arrange
        var light = new DrawingPlane(32, 16, 1f);

        //Act
        DrawingPlane bloom = light.CreateBloom(4, new[] { (2f, 1f) }, 1f, new Size(64, 32));

        //Assert
        bloom.Size.Should().Be(new Size(64, 32));
    }

    [Fact]
    public void CreateBloom_rejects_empty_blur_list()
    {
        //Arrange
        Action act = () => Row(1f).CreateBloom(1, Array.Empty<(float, float)>());

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToBytes_uses_round_half_up_and_clamps()
    {
        //Arrange
        DrawingPlane plane = Row(-0.1f, 0f, 0.5f, 0.498f, 1f, 1.5f, float.NaN, 1f / 255f);

        //Act
        byte[] bytes = plane.ToBytes();

        //Assert
        bytes.Should().Equal(new byte[] { 0, 0, 128, 127, 255, 255, 0, 1 });
    }

    [Fact]
    public void FromBytes_and_ToBytes_round_trip_every_byte_value()
    {
        //Arrange
        var bytes = new byte[256];
        for (int i = 0; i < 256; i++) { bytes[i] = (byte)i; }

        //Act
        byte[] back = DrawingPlane.FromBytes(bytes, 16, 16).ToBytes();

        //Assert
        back.Should().Equal(bytes);
    }

    [Fact]
    public void CopyFromBytes_overwrites_the_plane_like_FromBytes()
    {
        //Arrange
        var bytes = new byte[256];
        for (int i = 0; i < 256; i++) { bytes[i] = (byte)(255 - i); }
        var plane = new DrawingPlane(16, 16, 0.5f);

        //Act
        plane.CopyFromBytes(bytes);
        Action tooFew = () => plane.CopyFromBytes(new byte[255]);

        //Assert
        plane.Data.Should().Equal(DrawingPlane.FromBytes(bytes, 16, 16).Data);
        plane.ToBytes().Should().Equal(bytes);
        tooFew.Should().Throw<ArgumentException>();
    }
}
