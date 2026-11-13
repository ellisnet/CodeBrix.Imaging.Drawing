using System;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingPlaneImageTests
{
    //A transparent image with one opaque pure-red pixel in the middle
    private static DrawingPlaneImage RedDotOnTransparent(int size)
    {
        var image = new DrawingPlaneImage(size, size);
        image.SetPixel(size / 2, size / 2, new Vector4(1f, 0f, 0f, 1f));
        return image;
    }

    [Fact]
    public void New_image_is_transparent_black()
    {
        //Arrange + Act
        var image = new DrawingPlaneImage(4, 3);

        //Assert
        image.Size.Should().Be(new Size(4, 3));
        image.R.Data.Should().AllBeEquivalentTo(0f);
        image.A.Data.Should().AllBeEquivalentTo(0f);
    }

    [Fact]
    public void Constructor_over_planes_keeps_the_instances()
    {
        //Arrange
        var r = new DrawingPlane(2, 2);
        var g = new DrawingPlane(2, 2);
        var b = new DrawingPlane(2, 2);
        var a = new DrawingPlane(2, 2);

        //Act
        var image = new DrawingPlaneImage(r, g, b, a);

        //Assert
        image.R.Should().BeSameAs(r);
        image.A.Should().BeSameAs(a);
    }

    [Fact]
    public void Constructor_over_planes_rejects_a_shared_plane()
    {
        //Arrange
        var r = new DrawingPlane(2, 2);
        Action act = () => _ = new DrawingPlaneImage(r, new DrawingPlane(2, 2), r, new DrawingPlane(2, 2));

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_over_planes_rejects_mismatched_sizes()
    {
        //Arrange
        Action act = () => _ = new DrawingPlaneImage(new DrawingPlane(2, 2), new DrawingPlane(2, 2), new DrawingPlane(2, 3), new DrawingPlane(2, 2));

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fill_color_uses_byte_over_255()
    {
        //Arrange
        var image = new DrawingPlaneImage(2, 2);

        //Act
        image.Fill(Color.FromRgba(255, 128, 0, 64));

        //Assert
        image.GetPixel(1, 1).Should().Be(new Vector4(1f, 128 / 255f, 0f, 64 / 255f));
    }

    [Fact]
    public void Fill_rectangle_color_is_clipped()
    {
        //Arrange
        var image = new DrawingPlaneImage(3, 1);

        //Act
        image.Fill(new Rectangle(1, 0, 5, 5), Color.White);

        //Assert
        image.A.Data.Should().Equal(0f, 1f, 1f);
        image.R.Data.Should().Equal(0f, 1f, 1f);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(0)]
    public void FillParallel_matches_Fill(int threads)
    {
        //Arrange
        Func<int, int, Vector4> function = (x, y) => new Vector4(x * 0.01f, y * 0.02f, (x ^ y) * 0.003f, ((x + y) % 7) / 6f);
        DrawingPlaneImage expected = new DrawingPlaneImage(200, 150).Fill(function);

        //Act
        DrawingPlaneImage actual = new DrawingPlaneImage(200, 150).FillParallel(function, threads);

        //Assert
        actual.R.Data.Should().Equal(expected.R.Data);
        actual.G.Data.Should().Equal(expected.G.Data);
        actual.B.Data.Should().Equal(expected.B.Data);
        actual.A.Data.Should().Equal(expected.A.Data);
    }

    [Fact]
    public void Colour_arithmetic_leaves_alpha_alone()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(1, 1).Fill(0.5f, 0.25f, 1f, 0.3f);
        DrawingPlaneImage light = new DrawingPlaneImage(1, 1).Fill(1f, 1f, 1f, 0f);

        //Act
        image.Add(light, 0.5f).Multiply(2f).Multiply(1f, 0.5f, 0.25f).Add(0.1f, 0f, 0f);

        //Assert
        image.GetPixel(0, 0).Should().Be(new Vector4((((0.5f + 0.5f) * 2f) * 1f) + 0.1f, ((0.25f + 0.5f) * 2f) * 0.5f, ((1f + 0.5f) * 2f) * 0.25f, 0.3f));
    }

    [Fact]
    public void Min_Max_Clamp_and_Apply_act_on_colour_only()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(1, 1).Fill(2f, -1f, 0.5f, 0.75f);

        //Act
        image.Clamp(0f, 1f).Apply(v => v * 0.5f).Max(0.1f).Min(0.4f);

        //Assert
        image.GetPixel(0, 0).Should().Be(new Vector4(0.4f, 0.1f, 0.25f, 0.75f));
    }

    [Fact]
    public void Apply_per_channel_curves()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(1, 1).Fill(1f, 1f, 1f, 1f);

        //Act
        image.Apply(v => v * 2f, v => v * 3f, v => v * 4f);

        //Assert
        image.GetPixel(0, 0).Should().Be(new Vector4(2f, 3f, 4f, 1f));
    }

    [Fact]
    public void ApplyToneCurve_and_ApplyGamma_act_on_colour_only()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(1, 1).Fill(2f, 0.25f, 0f, 0.5f);

        //Act
        image.ApplyToneCurve(0.15f, 1.08f).ApplyGamma(1f);

        //Assert
        image.GetPixel(0, 0).Should().Be(new Vector4(2f / (1f + (0.15f * 2f)) * 1.08f, 0.25f / (1f + (0.15f * 0.25f)) * 1.08f, 0f, 0.5f));
    }

    [Fact]
    public void ApplyVignette_multiplies_colour_by_the_vignette_plane()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(20, 10).Fill(1f, 0.5f, 0.25f, 0.9f);
        DrawingPlane vignette = DrawingPlane.CreateVignette(20, 10, 10f, 5f, 12f, 6f, 0.4f, 0.3f);

        //Act
        image.ApplyVignette(10f, 5f, 12f, 6f, 0.4f, 0.3f);

        //Assert
        image.R.Data.Should().Equal(vignette.Data);
        image.A.Data.Should().AllBeEquivalentTo(0.9f);
    }

    [Fact]
    public void Lerp_of_opaque_images_is_the_straight_formula()
    {
        //Arrange
        DrawingPlaneImage a = new DrawingPlaneImage(1, 1).Fill(0.2f, 0.4f, 0.6f, 1f);
        DrawingPlaneImage b = new DrawingPlaneImage(1, 1).Fill(1f, 0f, 0.5f, 1f);
        const float t = 0.3f;
        float s = 1f - t;

        //Act
        a.Lerp(b, t);

        //Assert
        a.GetPixel(0, 0).Should().Be(new Vector4((0.2f * s) + (1f * t), (0.4f * s) + (0f * t), (0.6f * s) + (0.5f * t), 1f));
    }

    [Fact]
    public void Lerp_from_transparent_takes_no_colour_from_the_transparent_pixel()
    {
        //Arrange - transparent BLACK fading into opaque red must stay red, never darken
        var a = new DrawingPlaneImage(1, 1);
        DrawingPlaneImage b = new DrawingPlaneImage(1, 1).Fill(1f, 0f, 0f, 1f);

        //Act
        a.Lerp(b, 0.5f);

        //Assert
        a.GetPixel(0, 0).Should().Be(new Vector4(1f, 0f, 0f, 0.5f));
    }

    [Fact]
    public void Lerp_at_the_ends_copies_exactly()
    {
        //Arrange
        DrawingPlaneImage a = new DrawingPlaneImage(1, 1).Fill(0.1f, 0.2f, 0.3f, 0.4f);
        DrawingPlaneImage b = new DrawingPlaneImage(1, 1).Fill(0.9f, 0.8f, 0.7f, 0f);
        DrawingPlaneImage a2 = a.Clone();

        //Act
        a.Lerp(b, 0f);
        a2.Lerp(b, 1f);

        //Assert
        a.GetPixel(0, 0).Should().Be(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        a2.GetPixel(0, 0).Should().Be(new Vector4(0.9f, 0.8f, 0.7f, 0f));
    }

    [Fact]
    public void Lerp_with_unequal_alpha_uses_premultiplied_mix()
    {
        //Arrange
        DrawingPlaneImage a = new DrawingPlaneImage(1, 1).Fill(0.2f, 0.4f, 0.6f, 0.5f);
        DrawingPlaneImage b = new DrawingPlaneImage(1, 1).Fill(1f, 0f, 0.5f, 1f);
        const float t = 0.25f;
        float s = 1f - t, wa = 0.5f * s, wb = 1f * t, alpha = wa + wb;

        //Act
        a.Lerp(b, t);

        //Assert
        a.GetPixel(0, 0).Should().Be(new Vector4(((0.2f * wa) + (1f * wb)) / alpha, ((0.4f * wa) + (0f * wb)) / alpha,
            ((0.6f * wa) + (0.5f * wb)) / alpha, alpha));
    }

    [Fact]
    public void Lerp_with_mask_mixes_per_pixel()
    {
        //Arrange
        DrawingPlaneImage a = new DrawingPlaneImage(3, 1).Fill(0f, 0f, 0f, 1f);
        DrawingPlaneImage b = new DrawingPlaneImage(3, 1).Fill(1f, 1f, 1f, 1f);
        var mask = new DrawingPlane(3, 1, new[] { 0f, 0.5f, 1f });

        //Act
        a.Lerp(b, mask);

        //Assert
        a.R.Data.Should().Equal(0f, 0.5f, 1f);
        a.A.Data.Should().AllBeEquivalentTo(1f);
    }

    [Fact]
    public void CompositeOver_opaque_destination_is_the_lerp_form()
    {
        //Arrange
        DrawingPlaneImage destination = new DrawingPlaneImage(1, 1).Fill(0.2f, 0.4f, 0.6f, 1f);
        DrawingPlaneImage source = new DrawingPlaneImage(1, 1).Fill(1f, 0.5f, 0f, 0.25f);

        //Act
        destination.CompositeOver(source);

        //Assert
        destination.GetPixel(0, 0).Should().Be(new Vector4((1f * 0.25f) + (0.2f * 0.75f), (0.5f * 0.25f) + (0.4f * 0.75f),
            (0f * 0.25f) + (0.6f * 0.75f), 1f));
    }

    [Fact]
    public void CompositeOver_transparent_destination_copies_the_source()
    {
        //Arrange
        var destination = new DrawingPlaneImage(1, 1);
        DrawingPlaneImage source = new DrawingPlaneImage(1, 1).Fill(0.3f, 0.6f, 0.9f, 0.35f);

        //Act
        destination.CompositeOver(source);

        //Assert
        destination.GetPixel(0, 0).Should().Be(new Vector4(0.3f, 0.6f, 0.9f, 0.35f));
    }

    [Fact]
    public void CompositeOver_transparent_source_changes_nothing()
    {
        //Arrange
        DrawingPlaneImage destination = new DrawingPlaneImage(1, 1).Fill(0.3f, 0.6f, 0.9f, 0.35f);
        DrawingPlaneImage source = new DrawingPlaneImage(1, 1).Fill(1f, 1f, 1f, 0f);

        //Act
        destination.CompositeOver(source);

        //Assert
        destination.GetPixel(0, 0).Should().Be(new Vector4(0.3f, 0.6f, 0.9f, 0.35f));
    }

    [Fact]
    public void CompositeOver_two_translucent_pixels_follows_porter_duff()
    {
        //Arrange
        DrawingPlaneImage destination = new DrawingPlaneImage(1, 1).Fill(0f, 0f, 1f, 0.5f);
        DrawingPlaneImage source = new DrawingPlaneImage(1, 1).Fill(1f, 0f, 0f, 0.5f);
        float rest = 0.5f * (1f - 0.5f);
        float ao = 0.5f + (0.5f * (1f - 0.5f));

        //Act
        destination.CompositeOver(source);

        //Assert
        destination.GetPixel(0, 0).Should().Be(new Vector4((1f * 0.5f) / ao, 0f, (1f * rest) / ao, ao));
        ao.Should().Be(0.75f);
    }

    [Fact]
    public void CompositeOver_opacity_scales_source_alpha()
    {
        //Arrange
        DrawingPlaneImage destination = new DrawingPlaneImage(1, 1).Fill(0f, 0f, 0f, 1f);
        DrawingPlaneImage source = new DrawingPlaneImage(1, 1).Fill(1f, 1f, 1f, 1f);

        //Act
        destination.CompositeOver(source, 0.5f);

        //Assert
        destination.GetPixel(0, 0).Should().Be(new Vector4(0.5f, 0.5f, 0.5f, 1f));
    }

    [Fact]
    public void CompositeAdd_adds_light_weighted_by_source_alpha()
    {
        //Arrange
        DrawingPlaneImage destination = new DrawingPlaneImage(1, 1).Fill(0.5f, 0.5f, 0.5f, 1f);
        DrawingPlaneImage glow = new DrawingPlaneImage(1, 1).Fill(1f, 0.5f, 0f, 0.5f);

        //Act
        destination.CompositeAdd(glow);

        //Assert - past 1.0 is allowed
        destination.GetPixel(0, 0).Should().Be(new Vector4(0.5f + 0.5f, 0.5f + 0.25f, 0.5f, 1f));
    }

    [Fact]
    public void CompositeAdd_over_transparency_keeps_the_glow_colour()
    {
        //Arrange
        var destination = new DrawingPlaneImage(1, 1);
        DrawingPlaneImage glow = new DrawingPlaneImage(1, 1).Fill(1f, 0.5f, 0f, 0.25f);

        //Act
        destination.CompositeAdd(glow);

        //Assert
        destination.GetPixel(0, 0).Should().Be(new Vector4(1f, 0.5f, 0f, 0.25f));
    }

    [Fact]
    public void BlendColor_paints_through_the_mask()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(3, 1).Fill(0f, 0f, 0f, 1f);
        var mask = new DrawingPlane(3, 1, new[] { 0f, 0.5f, 1f });

        //Act
        image.BlendColor(Color.White, mask);

        //Assert
        image.R.Data.Should().Equal(0f, 0.5f, 1f);
        image.A.Data.Should().AllBeEquivalentTo(1f);
    }

    [Fact]
    public void BlendColor_on_transparency_gives_coverage_alpha()
    {
        //Arrange
        var image = new DrawingPlaneImage(2, 1);
        var mask = new DrawingPlane(2, 1, new[] { 0.25f, 1f });

        //Act
        image.BlendColor(1f, 0.5f, 0f, 1f, mask);

        //Assert
        image.GetPixel(0, 0).Should().Be(new Vector4(1f, 0.5f, 0f, 0.25f));
        image.GetPixel(1, 0).Should().Be(new Vector4(1f, 0.5f, 0f, 1f));
    }

    [Fact]
    public void BlendImage_paints_another_image_through_the_mask()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(2, 1).Fill(0f, 0f, 1f, 1f);
        DrawingPlaneImage paint = new DrawingPlaneImage(2, 1).Fill(1f, 0f, 0f, 1f);
        var mask = new DrawingPlane(2, 1, new[] { 0f, 0.5f });

        //Act
        image.BlendImage(paint, mask);

        //Assert
        image.GetPixel(0, 0).Should().Be(new Vector4(0f, 0f, 1f, 1f));
        image.GetPixel(1, 0).Should().Be(new Vector4(0.5f, 0f, 0.5f, 1f));
    }

    [Fact]
    public void GaussianBlur_of_an_opaque_image_blurs_each_colour_plane_and_keeps_alpha()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(40, 30).Fill((x, y) => new Vector4(x * 0.02f, (y % 5) * 0.2f, 0.5f, 1f));
        DrawingPlane expectedRed = image.R.Clone().GaussianBlur(2f);
        DrawingPlane expectedGreen = image.G.Clone().GaussianBlur(2f);

        //Act
        image.GaussianBlur(2f);

        //Assert
        image.R.Data.Should().Equal(expectedRed.Data);
        image.G.Data.Should().Equal(expectedGreen.Data);
        image.A.Data.Should().AllBeEquivalentTo(1f);
    }

    [Fact]
    public void GaussianBlur_over_transparency_never_darkens_the_colour()
    {
        //Arrange
        DrawingPlaneImage image = RedDotOnTransparent(15);

        //Act
        image.GaussianBlur(1.5f);

        //Assert - wherever the blur reached, the colour is still pure red; alpha spread out
        for (int i = 0; i < image.A.Length; i++)
        {
            if (image.A.Data[i] > 0f)
            {
                image.R.Data[i].Should().Be(1f);
                image.G.Data[i].Should().Be(0f);
            }
            else
            {
                image.R.Data[i].Should().Be(0f);
            }
        }
        image.A[7, 7].Should().BeLessThan(1f);
        image.A[8, 7].Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Resized_bilinear_over_transparency_never_darkens_the_colour()
    {
        //Arrange
        DrawingPlaneImage image = RedDotOnTransparent(5);

        //Act
        DrawingPlaneImage result = image.Resized(17, 13, DrawingPlaneResampling.Bilinear);

        //Assert
        for (int i = 0; i < result.A.Length; i++)
        {
            if (result.A.Data[i] > 0f) { result.R.Data[i].Should().Be(1f); }
        }
    }

    [Fact]
    public void Resized_nearest_copies_all_channels()
    {
        //Arrange
        var image = new DrawingPlaneImage(2, 1);
        image.SetPixel(0, 0, new Vector4(0.1f, 0.2f, 0.3f, 0f));
        image.SetPixel(1, 0, new Vector4(0.4f, 0.5f, 0.6f, 0.7f));

        //Act
        DrawingPlaneImage result = image.Resized(4, 1, DrawingPlaneResampling.Nearest);

        //Assert
        result.GetPixel(1, 0).Should().Be(new Vector4(0.1f, 0.2f, 0.3f, 0f));
        result.GetPixel(2, 0).Should().Be(new Vector4(0.4f, 0.5f, 0.6f, 0.7f));
    }

    [Fact]
    public void Downsampled_weights_colour_by_alpha()
    {
        //Arrange - a 2x1 block: opaque white beside transparent black
        var image = new DrawingPlaneImage(2, 1);
        image.SetPixel(0, 0, new Vector4(1f, 1f, 1f, 1f));

        //Act
        DrawingPlaneImage result = image.Downsampled(2);

        //Assert - half coverage, but still white
        result.GetPixel(0, 0).Should().Be(new Vector4(1f, 1f, 1f, 0.5f));
    }

    [Fact]
    public void Downsampled_of_an_opaque_image_matches_the_planes()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(9, 7).Fill((x, y) => new Vector4(x * 0.1f, y * 0.1f, 0.3f, 1f));

        //Act
        DrawingPlaneImage result = image.Downsampled(3);

        //Assert
        result.R.Data.Should().Equal(image.R.Downsampled(3).Data);
        result.A.Data.Should().AllBeEquivalentTo(1f);
    }

    [Fact]
    public void CreateBloom_of_an_opaque_image_blooms_each_colour_plane()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(32, 32).Fill((x, y) => (x == 10 && y == 12) ? new Vector4(4f, 2f, 1f, 1f) : new Vector4(0f, 0f, 0f, 1f));
        var blurs = new[] { (1f, 0.8f), (3f, 0.9f) };

        //Act
        DrawingPlaneImage glow = image.CreateBloom(2, blurs, 0.6f);

        //Assert
        glow.R.Data.Should().Equal(image.R.CreateBloom(2, blurs, 0.6f).Data);
        glow.B.Data.Should().Equal(image.B.CreateBloom(2, blurs, 0.6f).Data);
        glow.A.Data.Should().AllBeEquivalentTo(1f);
    }

    [Fact]
    public void CreateBloom_over_transparency_spreads_coverage_with_the_light_colour()
    {
        //Arrange
        DrawingPlaneImage image = RedDotOnTransparent(16);

        //Act
        DrawingPlaneImage glow = image.CreateBloom(1, new[] { (2f, 1f) }, 2f);

        //Assert - colour is the light colour times the gain wherever the glow reached
        for (int i = 0; i < glow.A.Length; i++)
        {
            if (glow.A.Data[i] > 0f) { glow.R.Data[i].Should().BeApproximately(2f, 1e-5f); }
        }
        glow.A[9, 8].Should().BeGreaterThan(0f);
    }

    [Fact]
    public void ToRgba_writes_straight_alpha_with_the_rounding_rule()
    {
        //Arrange
        var image = new DrawingPlaneImage(2, 1);
        image.SetPixel(0, 0, new Vector4(1.7f, 0.5f, -0.2f, 0.498f));
        image.SetPixel(1, 0, new Vector4(0.2f, 0.4f, 0.6f, 0f));

        //Act
        byte[] rgba = image.ToRgba();

        //Assert - the colour of the transparent pixel survives (straight alpha)
        rgba.Should().Equal(new byte[] { 255, 128, 0, 127, 51, 102, 153, 0 });
    }

    [Fact]
    public void Rgba_round_trip_is_exact_for_every_byte_value_including_transparent_pixels()
    {
        //Arrange
        var rgba = new byte[64 * 4 * 4];
        for (int i = 0; i < rgba.Length; i++) { rgba[i] = (byte)((i * 37) + (i / 256)); }
        for (int i = 3; i < 64; i += 4) { rgba[i] = 0; }

        //Act
        byte[] back = DrawingPlaneImage.FromRgba(rgba, 16, 16).ToRgba();

        //Assert
        back.Should().Equal(rgba);
    }

    [Fact]
    public void Fully_transparent_image_round_trips_through_rgba()
    {
        //Arrange
        var image = new DrawingPlaneImage(5, 4);

        //Act
        DrawingPlaneImage back = DrawingPlaneImage.FromRgba(image.ToRgba(), 5, 4);

        //Assert
        back.ToRgba().Should().AllBeEquivalentTo((byte)0);
    }

    [Fact]
    public void ImagingImage_round_trip_keeps_pixels_and_transparency()
    {
        //Arrange
        var image = new DrawingPlaneImage(3, 2);
        image.SetPixel(0, 0, new Vector4(1f, 0f, 0f, 1f));
        image.SetPixel(2, 1, new Vector4(0f, 0.5f, 1f, 0.5f));
        byte[] expected = image.ToRgba();

        //Act
        using Image<Rgba32> imaging = image.ToImagingImage();
        DrawingPlaneImage back = DrawingPlaneImage.FromImagingImage(imaging);

        //Assert
        imaging[0, 0].Should().Be(new Rgba32(255, 0, 0, 255));
        imaging[1, 0].A.Should().Be((byte)0);
        imaging[2, 1].Should().Be(new Rgba32(0, 128, 255, 128));
        back.ToRgba().Should().Equal(expected);
    }

    [Fact]
    public void FromRgba_rejects_short_buffers()
    {
        //Arrange
        Action act = () => DrawingPlaneImage.FromRgba(new byte[15], 2, 2);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Clone_and_CopyTo_copy_all_four_planes()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(2, 2).Fill(0.1f, 0.2f, 0.3f, 0.4f);
        var target = new DrawingPlaneImage(2, 2);

        //Act
        DrawingPlaneImage copy = image.Clone();
        image.CopyTo(target);
        image.Clear();

        //Assert
        copy.GetPixel(1, 1).Should().Be(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        target.GetPixel(1, 1).Should().Be(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        image.GetPixel(1, 1).Should().Be(Vector4.Zero);
    }
}
