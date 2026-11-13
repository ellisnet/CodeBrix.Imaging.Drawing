using System;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingFrameAccumulatorTests
{
    [Fact]
    public void Opaque_byte_frames_average_to_the_rounded_mean()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(1, 1);

        //Act
        accumulator.Add(new byte[] { 10, 0, 255, 255 });
        accumulator.Add(new byte[] { 11, 1, 0, 255 });
        accumulator.Add(new byte[] { 12, 1, 0, 255 });
        byte[] result = accumulator.GetAverage();

        //Assert - (33 / 3 = 11), (2 / 3 = 0.67 -> 1), (255 / 3 = 85)
        result.Should().Equal(new byte[] { 11, 1, 85, 255 });
        accumulator.Count.Should().Be(3);
    }

    [Fact]
    public void Half_up_rounding_is_exact_integer_arithmetic()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(1, 1);

        //Act
        accumulator.Add(new byte[] { 0, 0, 0, 255 });
        accumulator.Add(new byte[] { 1, 3, 0, 255 });
        byte[] result = accumulator.GetAverage();

        //Assert - 0.5 rounds up to 1, 1.5 rounds up to 2
        result.Should().Equal(new byte[] { 1, 2, 0, 255 });
    }

    [Fact]
    public void Byte_frames_are_averaged_in_premultiplied_space()
    {
        //Arrange - an object over a transparent background in one of two sub-frames
        var accumulator = new DrawingFrameAccumulator(1, 1);

        //Act
        accumulator.Add(new byte[] { 200, 100, 0, 255 });
        accumulator.Add(new byte[] { 0, 0, 0, 0 });
        byte[] result = accumulator.GetAverage();

        //Assert - half coverage, the object's own colour (no darkening towards black)
        result.Should().Equal(new byte[] { 200, 100, 0, 128 });
    }

    [Fact]
    public void Transparent_frames_average_to_transparent()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(2, 1);

        //Act
        accumulator.Add(new byte[] { 9, 9, 9, 0, 0, 0, 0, 0 });
        accumulator.Add(new byte[8]);

        //Assert
        accumulator.GetAverage().Should().AllBeEquivalentTo((byte)0);
    }

    [Fact]
    public void Single_byte_frame_round_trips_wherever_it_is_covered()
    {
        //Arrange
        byte[] frame = { 1, 2, 3, 4, 250, 128, 7, 255, 50, 60, 70, 0 };
        var accumulator = new DrawingFrameAccumulator(3, 1);

        //Act
        accumulator.Add(frame);
        byte[] result = accumulator.GetAverage();

        //Assert - an uncovered pixel's colour is dropped, as premultiplied averaging defines
        result.Should().Equal(new byte[] { 1, 2, 3, 4, 250, 128, 7, 255, 0, 0, 0, 0 });
    }

    [Fact]
    public void Float_images_are_averaged_in_premultiplied_space()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(1, 1);

        //Act
        accumulator.Add(new DrawingPlaneImage(1, 1).Fill(1f, 0.5f, 0f, 1f));
        accumulator.Add(new DrawingPlaneImage(1, 1));
        accumulator.Add(new DrawingPlaneImage(1, 1).Fill(0f, 0.5f, 1f, 0.5f));
        DrawingPlaneImage result = accumulator.GetAverageImage();

        //Assert - A = 1.5 / 3; C = sum(C * A) / sum(A)
        result.GetPixel(0, 0).Should().Be(new Vector4((float)(1.0 / 1.5), (float)(0.75 / 1.5), (float)(0.5 / 1.5), 0.5f));
    }

    [Fact]
    public void One_float_image_averages_to_itself()
    {
        //Arrange
        DrawingPlaneImage image = new DrawingPlaneImage(3, 2).Fill((x, y) => new Vector4(x * 0.37f, y * 1.3f, 0.123f, (x + 1) * 0.3f));
        var accumulator = new DrawingFrameAccumulator(3, 2);

        //Act
        accumulator.Add(image);
        DrawingPlaneImage result = accumulator.GetAverageImage();

        //Assert
        result.R.Data.Should().Equal(image.R.Data);
        result.G.Data.Should().Equal(image.G.Data);
        result.A.Data.Should().Equal(image.A.Data);
    }

    [Fact]
    public void Opaque_float_images_average_to_the_mean()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(1, 1);

        //Act
        accumulator.Add(new DrawingPlaneImage(1, 1).Fill(0.25f, 2f, 0f, 1f));
        accumulator.Add(new DrawingPlaneImage(1, 1).Fill(0.75f, 0f, 0f, 1f));

        //Assert
        accumulator.GetAverageImage().GetPixel(0, 0).Should().Be(new Vector4(0.5f, 1f, 0f, 1f));
    }

    [Fact]
    public void Mixing_input_kinds_is_rejected_until_reset()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(1, 1);
        accumulator.Add(new byte[] { 1, 2, 3, 255 });
        Action mixed = () => accumulator.Add(new DrawingPlaneImage(1, 1));

        //Act + Assert
        mixed.Should().Throw<InvalidOperationException>();
        accumulator.Reset();
        accumulator.Add(new DrawingPlaneImage(1, 1).Fill(1f, 1f, 1f, 1f));
        accumulator.GetAverage().Should().Equal(new byte[] { 255, 255, 255, 255 });
    }

    [Fact]
    public void GetAverage_without_frames_is_rejected()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(1, 1);
        Action act = () => accumulator.GetAverage();

        //Act + Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Byte_frames_can_be_read_as_a_float_image()
    {
        //Arrange
        var accumulator = new DrawingFrameAccumulator(1, 1);
        accumulator.Add(new byte[] { 255, 51, 0, 255 });
        accumulator.Add(new byte[] { 255, 51, 0, 0 });

        //Act
        DrawingPlaneImage result = accumulator.GetAverageImage();

        //Assert
        result.GetPixel(0, 0).Should().Be(new Vector4(1f, 0.2f, 0f, 0.5f));
    }
}
