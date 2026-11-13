using System;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingTransitionTests
{
    //A frame of `count` opaque pixels whose red channel is the pixel's index times `step`
    private static byte[] Ramp(int count, int step, byte green = 0)
    {
        var rgba = new byte[count * 4];
        for (int i = 0; i < count; i++)
        {
            rgba[i * 4] = (byte)(i * step);
            rgba[(i * 4) + 1] = green;
            rgba[(i * 4) + 3] = 255;
        }
        return rgba;
    }

    private static byte[] Solid(int count, byte r, byte g, byte b, byte a)
    {
        var rgba = new byte[count * 4];
        for (int i = 0; i < count; i++)
        {
            rgba[i * 4] = r;
            rgba[(i * 4) + 1] = g;
            rgba[(i * 4) + 2] = b;
            rgba[(i * 4) + 3] = a;
        }
        return rgba;
    }

    private static byte[] Red(byte[] rgba)
    {
        var reds = new byte[rgba.Length / 4];
        for (int i = 0; i < reds.Length; i++) { reds[i] = rgba[i * 4]; }
        return reds;
    }

    private static byte[] Apply(DrawingTransition transition, byte[] from, byte[] to, int width, int height, float fraction)
    {
        var destination = new byte[from.Length];
        transition.Apply(from, to, destination, width, height, fraction);
        return destination;
    }

    [Fact]
    public void Dissolve_of_opaque_frames_is_a_times_one_minus_u_plus_b_times_u_rounded()
    {
        //Arrange
        byte[] a = Ramp(8, 30);
        byte[] b = Solid(8, 200, 100, 50, 255);
        const float u = 0.37f;
        float v = 1f - u;

        //Act
        byte[] result = Apply(DrawingTransition.Dissolve(), a, b, 8, 1, u);

        //Assert
        for (int i = 0; i < result.Length; i++)
        {
            result[i].Should().Be((byte)((a[i] * v) + (b[i] * u) + 0.5f));
        }
    }

    [Theory]
    [InlineData(DrawingTransitionKind.Dissolve)]
    [InlineData(DrawingTransitionKind.Wipe)]
    [InlineData(DrawingTransitionKind.Slide)]
    [InlineData(DrawingTransitionKind.Push)]
    [InlineData(DrawingTransitionKind.FadeThroughColor)]
    public void Every_transition_gives_the_exact_frames_at_zero_and_one(DrawingTransitionKind kind)
    {
        //Arrange
        DrawingTransition transition = kind switch
        {
            DrawingTransitionKind.Dissolve => DrawingTransition.Dissolve(),
            DrawingTransitionKind.Wipe => DrawingTransition.Wipe(DrawingTransitionDirection.Up, 3f),
            DrawingTransitionKind.Slide => DrawingTransition.Slide(DrawingTransitionDirection.Left),
            DrawingTransitionKind.Push => DrawingTransition.Push(DrawingTransitionDirection.Down),
            _ => DrawingTransition.FadeThroughColor(Color.Black),
        };
        byte[] a = Ramp(12, 20, 7);
        byte[] b = Solid(12, 9, 8, 7, 100);

        //Act
        byte[] start = Apply(transition, a, b, 4, 3, 0f);
        byte[] end = Apply(transition, a, b, 4, 3, 1f);
        byte[] clampedLow = Apply(transition, a, b, 4, 3, -2f);
        byte[] clampedHigh = Apply(transition, a, b, 4, 3, 5f);

        //Assert
        start.Should().Equal(a);
        end.Should().Equal(b);
        clampedLow.Should().Equal(a);
        clampedHigh.Should().Equal(b);
    }

    [Fact]
    public void Dissolve_from_transparent_black_never_darkens()
    {
        //Arrange
        byte[] transparent = new byte[4];
        byte[] red = Solid(1, 255, 0, 0, 255);

        //Act
        byte[] result = Apply(DrawingTransition.Dissolve(), transparent, red, 1, 1, 0.5f);

        //Assert - premultiplied: full red at half coverage (a straight mix would give 128, a dark red)
        result.Should().Equal(new byte[] { 255, 0, 0, 128 });
    }

    [Fact]
    public void Dissolve_between_translucent_pixels_uses_premultiplied_mix()
    {
        //Arrange
        byte[] a = Solid(1, 200, 100, 0, 64);
        byte[] b = Solid(1, 0, 50, 250, 192);
        const float t = 0.25f;
        float s = 1f - t, wa = 64 * s, wb = 192 * t, alpha = wa + wb;

        //Act
        byte[] result = Apply(DrawingTransition.Dissolve(), a, b, 1, 1, t);

        //Assert
        result.Should().Equal(new byte[]
        {
            (byte)((((200 * wa) + (0 * wb)) / alpha) + 0.5f),
            (byte)((((100 * wa) + (50 * wb)) / alpha) + 0.5f),
            (byte)((((0 * wa) + (250 * wb)) / alpha) + 0.5f),
            (byte)(alpha + 0.5f),
        });
    }

    [Fact]
    public void Dissolve_of_two_transparent_frames_stays_transparent()
    {
        //Arrange
        var a = new byte[16];
        var b = new byte[16];

        //Act
        byte[] result = Apply(DrawingTransition.Dissolve(), a, b, 2, 2, 0.5f);

        //Assert
        result.Should().AllBeEquivalentTo((byte)0);
    }

    [Fact]
    public void Dissolve_may_write_over_its_input()
    {
        //Arrange
        byte[] a = Ramp(4, 60);
        byte[] b = Solid(4, 0, 0, 0, 255);
        byte[] expected = Apply(DrawingTransition.Dissolve(), a, b, 4, 1, 0.5f);

        //Act
        DrawingTransition.Dissolve().Apply(a, b, a, 4, 1, 0.5f);

        //Assert
        a.Should().Equal(expected);
    }

    [Theory]
    [InlineData(DrawingTransitionDirection.Right, new byte[] { 1, 1, 0, 0 })]
    [InlineData(DrawingTransitionDirection.Left, new byte[] { 0, 0, 1, 1 })]
    public void Hard_wipe_reveals_the_incoming_frame_from_the_start_side(DrawingTransitionDirection direction, byte[] incoming)
    {
        //Arrange
        byte[] a = Solid(4, 0, 0, 0, 255);
        byte[] b = Solid(4, 1, 0, 0, 255);

        //Act
        byte[] result = Apply(DrawingTransition.Wipe(direction), a, b, 4, 1, 0.5f);

        //Assert
        Red(result).Should().Equal(incoming);
    }

    [Fact]
    public void Hard_vertical_wipes_work_by_rows()
    {
        //Arrange
        byte[] a = Solid(4, 0, 0, 0, 255);
        byte[] b = Solid(4, 1, 0, 0, 255);

        //Act
        byte[] down = Apply(DrawingTransition.Wipe(DrawingTransitionDirection.Down), a, b, 1, 4, 0.25f);
        byte[] up = Apply(DrawingTransition.Wipe(DrawingTransitionDirection.Up), a, b, 1, 4, 0.25f);

        //Assert
        Red(down).Should().Equal(new byte[] { 1, 0, 0, 0 });
        Red(up).Should().Equal(new byte[] { 0, 0, 0, 1 });
    }

    [Fact]
    public void Soft_wipe_weights_follow_the_documented_ramp()
    {
        //Arrange - width 10, softness 4, fraction 0.5: lead = 0.5 * 14 = 7, weight = clamp((7 - (x + 0.5)) / 4)
        byte[] a = Solid(10, 0, 0, 0, 255);
        byte[] b = Solid(10, 200, 0, 0, 255);

        //Act
        byte[] result = Apply(DrawingTransition.Wipe(DrawingTransitionDirection.Right, 4f), a, b, 10, 1, 0.5f);

        //Assert
        var expected = new byte[10];
        for (int x = 0; x < 10; x++)
        {
            float w = Math.Clamp((7f - (x + 0.5f)) / 4f, 0f, 1f);
            expected[x] = (byte)((0 * (1f - w)) + (200 * w) + 0.5f);
        }
        Red(result).Should().Equal(expected);
        expected[0].Should().Be((byte)200);
        expected[7].Should().Be((byte)0);
    }

    [Fact]
    public void Slide_right_covers_the_still_outgoing_frame()
    {
        //Arrange
        byte[] a = Ramp(4, 10);              //reds 0 10 20 30
        byte[] b = Ramp(4, 1);               //reds 0 1 2 3
        for (int i = 0; i < 4; i++) { b[i * 4] += 100; }

        //Act - offset = floor(0.5 * 4 + 0.5) = 2
        byte[] result = Apply(DrawingTransition.Slide(DrawingTransitionDirection.Right), a, b, 4, 1, 0.5f);

        //Assert - incoming's right half enters at the left; outgoing stays put
        Red(result).Should().Equal(new byte[] { 102, 103, 20, 30 });
    }

    [Fact]
    public void Push_right_moves_both_frames()
    {
        //Arrange
        byte[] a = Ramp(4, 10);
        byte[] b = Ramp(4, 1);
        for (int i = 0; i < 4; i++) { b[i * 4] += 100; }

        //Act
        byte[] result = Apply(DrawingTransition.Push(DrawingTransitionDirection.Right), a, b, 4, 1, 0.5f);

        //Assert
        Red(result).Should().Equal(new byte[] { 102, 103, 0, 10 });
    }

    [Fact]
    public void Slide_and_push_left_enter_from_the_right()
    {
        //Arrange
        byte[] a = Ramp(4, 10);
        byte[] b = Ramp(4, 1);
        for (int i = 0; i < 4; i++) { b[i * 4] += 100; }

        //Act - offset = floor(0.25 * 4 + 0.5) = 1
        byte[] slide = Apply(DrawingTransition.Slide(DrawingTransitionDirection.Left), a, b, 4, 1, 0.25f);
        byte[] push = Apply(DrawingTransition.Push(DrawingTransitionDirection.Left), a, b, 4, 1, 0.25f);

        //Assert
        Red(slide).Should().Equal(new byte[] { 0, 10, 20, 100 });
        Red(push).Should().Equal(new byte[] { 10, 20, 30, 100 });
    }

    [Fact]
    public void Slide_and_push_vertically_move_rows()
    {
        //Arrange - 1 x 4 frames
        byte[] a = Ramp(4, 10);
        byte[] b = Ramp(4, 1);
        for (int i = 0; i < 4; i++) { b[i * 4] += 100; }

        //Act
        byte[] down = Apply(DrawingTransition.Push(DrawingTransitionDirection.Down), a, b, 1, 4, 0.25f);
        byte[] up = Apply(DrawingTransition.Slide(DrawingTransitionDirection.Up), a, b, 1, 4, 0.5f);

        //Assert
        Red(down).Should().Equal(new byte[] { 103, 0, 10, 20 });
        Red(up).Should().Equal(new byte[] { 0, 10, 100, 101 });
    }

    [Fact]
    public void Slide_rejects_a_destination_that_overlaps_a_frame()
    {
        //Arrange
        byte[] a = Ramp(4, 10);
        byte[] b = Ramp(4, 1);
        Action act = () => DrawingTransition.Slide(DrawingTransitionDirection.Left).Apply(a, b, a, 4, 1, 0.5f);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Slide_carries_transparent_pixels_unchanged()
    {
        //Arrange
        byte[] a = Solid(4, 10, 20, 30, 0);
        byte[] b = Solid(4, 0, 0, 0, 0);

        //Act
        byte[] result = Apply(DrawingTransition.Slide(DrawingTransitionDirection.Right), a, b, 4, 1, 0.5f);

        //Assert
        result.Should().Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 10, 20, 30, 0, 10, 20, 30, 0 });
    }

    [Fact]
    public void FadeThroughColor_is_the_colour_at_the_midpoint_and_mixes_either_side()
    {
        //Arrange
        byte[] a = Solid(2, 200, 200, 200, 255);
        byte[] b = Solid(2, 0, 100, 0, 255);
        DrawingTransition fade = DrawingTransition.FadeThroughColor(Color.FromRgb(40, 0, 0));

        //Act
        byte[] middle = Apply(fade, a, b, 2, 1, 0.5f);
        byte[] quarter = Apply(fade, a, b, 2, 1, 0.25f);
        byte[] threeQuarters = Apply(fade, a, b, 2, 1, 0.75f);

        //Assert
        middle.Should().Equal(new byte[] { 40, 0, 0, 255, 40, 0, 0, 255 });
        quarter[0].Should().Be((byte)((200 * 0.5f) + (40 * 0.5f) + 0.5f));
        threeQuarters[1].Should().Be((byte)((0 * 0.5f) + (100 * 0.5f) + 0.5f));
    }

    [Fact]
    public void FadeThroughColor_transparent_fades_through_transparency()
    {
        //Arrange
        byte[] a = Solid(1, 255, 0, 0, 255);
        byte[] b = Solid(1, 0, 0, 255, 255);

        //Act
        byte[] middle = Apply(DrawingTransition.FadeThroughColor(Color.Transparent), a, b, 1, 1, 0.5f);
        byte[] quarter = Apply(DrawingTransition.FadeThroughColor(Color.Transparent), a, b, 1, 1, 0.25f);

        //Assert - half way out, still pure red but half covered
        middle.Should().Equal(new byte[] { 0, 0, 0, 0 });
        quarter.Should().Equal(new byte[] { 255, 0, 0, 128 });
    }

    [Fact]
    public void Apply_rejects_bad_arguments()
    {
        //Arrange
        DrawingTransition dissolve = DrawingTransition.Dissolve();
        Action shortFrame = () => dissolve.Apply(new byte[4], new byte[16], new byte[16], 2, 2, 0.5f);
        Action nan = () => dissolve.Apply(new byte[16], new byte[16], new byte[16], 2, 2, float.NaN);
        Action noWidth = () => dissolve.Apply(new byte[16], new byte[16], new byte[16], 0, 2, 0.5f);
        Action badSoftness = () => DrawingTransition.Wipe(DrawingTransitionDirection.Left, -1f);
        Action badDirection = () => DrawingTransition.Push((DrawingTransitionDirection)9);

        //Act + Assert
        shortFrame.Should().Throw<ArgumentException>();
        nan.Should().Throw<ArgumentOutOfRangeException>();
        noWidth.Should().Throw<ArgumentOutOfRangeException>();
        badSoftness.Should().Throw<ArgumentOutOfRangeException>();
        badDirection.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Factories_record_their_settings()
    {
        //Arrange + Act
        DrawingTransition wipe = DrawingTransition.Wipe(DrawingTransitionDirection.Up, 12f);
        DrawingTransition fade = DrawingTransition.FadeThroughColor(Color.White);

        //Assert
        wipe.Kind.Should().Be(DrawingTransitionKind.Wipe);
        wipe.Direction.Should().Be(DrawingTransitionDirection.Up);
        wipe.Softness.Should().Be(12f);
        fade.Kind.Should().Be(DrawingTransitionKind.FadeThroughColor);
        fade.Color.Should().Be(Color.White);
    }

    [Theory]
    [InlineData(DrawingTransitionKind.Dissolve, 0.3f)]
    [InlineData(DrawingTransitionKind.Wipe, 0.45f)]
    [InlineData(DrawingTransitionKind.Slide, 0.6f)]
    [InlineData(DrawingTransitionKind.Push, 0.35f)]
    [InlineData(DrawingTransitionKind.FadeThroughColor, 0.7f)]
    public void Float_images_follow_the_same_rules_as_byte_frames_for_opaque_and_transparent_pixels(DrawingTransitionKind kind, float fraction)
    {
        //Arrange - moves and hard-edged wipes copy pixels exactly, so float and byte results agree after conversion
        DrawingTransition transition = kind switch
        {
            DrawingTransitionKind.Dissolve => DrawingTransition.Dissolve(),
            DrawingTransitionKind.Wipe => DrawingTransition.Wipe(DrawingTransitionDirection.Left),
            DrawingTransitionKind.Slide => DrawingTransition.Slide(DrawingTransitionDirection.Down),
            DrawingTransitionKind.Push => DrawingTransition.Push(DrawingTransitionDirection.Up),
            _ => DrawingTransition.FadeThroughColor(Color.FromRgba(10, 20, 30, 0)),
        };
        DrawingPlaneImage a = new DrawingPlaneImage(6, 5).Fill((x, y) => new Vector4(x / 5f, y / 4f, 0.5f, x == 2 ? 0f : 1f));
        DrawingPlaneImage b = new DrawingPlaneImage(6, 5).Fill((x, y) => new Vector4(1f, 0f, (x + y) / 9f, y == 1 ? 0f : 1f));
        byte[] byteResult = Apply(transition, a.ToRgba(), b.ToRgba(), 6, 5, fraction);

        //Act
        DrawingPlaneImage result = transition.Apply(a, b, fraction);

        //Assert - where both inputs are opaque or both transparent the per-pixel rules coincide;
        //  allow one level for float-vs-byte rounding of the mixed pixels
        byte[] floatResult = result.ToRgba();
        for (int i = 0; i < floatResult.Length; i++)
        {
            Math.Abs(floatResult[i] - byteResult[i]).Should().BeLessThanOrEqualTo(1);
        }
        result.A.Data.Should().NotContain(float.NaN);
    }

    [Fact]
    public void Float_dissolve_of_opaque_images_is_the_straight_formula()
    {
        //Arrange
        DrawingPlaneImage a = new DrawingPlaneImage(2, 1).Fill(0.2f, 0.4f, 0.6f, 1f);
        DrawingPlaneImage b = new DrawingPlaneImage(2, 1).Fill(1f, 0f, 0.5f, 1f);
        const float t = 0.4f;
        float s = 1f - t;

        //Act
        DrawingPlaneImage result = DrawingTransition.Dissolve().Apply(a, b, t);

        //Assert
        result.GetPixel(1, 0).Should().Be(new Vector4((0.2f * s) + (1f * t), (0.4f * s) + (0f * t), (0.6f * s) + (0.5f * t), 1f));
    }

    [Fact]
    public void Float_apply_into_a_destination_may_reuse_an_input_for_per_pixel_kinds()
    {
        //Arrange
        DrawingPlaneImage a = new DrawingPlaneImage(4, 4).Fill(1f, 0f, 0f, 1f);
        DrawingPlaneImage b = new DrawingPlaneImage(4, 4).Fill(0f, 0f, 1f, 1f);
        DrawingPlaneImage expected = DrawingTransition.Wipe(DrawingTransitionDirection.Down, 2f).Apply(a, b, 0.5f);

        //Act
        DrawingTransition.Wipe(DrawingTransitionDirection.Down, 2f).Apply(a, b, a, 0.5f);

        //Assert
        a.R.Data.Should().Equal(expected.R.Data);
        a.B.Data.Should().Equal(expected.B.Data);
    }

    [Fact]
    public void Float_push_rejects_an_input_as_destination()
    {
        //Arrange
        var a = new DrawingPlaneImage(2, 2);
        var b = new DrawingPlaneImage(2, 2);
        Action act = () => DrawingTransition.Push(DrawingTransitionDirection.Left).Apply(a, b, b, 0.5f);

        //Act + Assert
        act.Should().Throw<ArgumentException>();
    }
}
