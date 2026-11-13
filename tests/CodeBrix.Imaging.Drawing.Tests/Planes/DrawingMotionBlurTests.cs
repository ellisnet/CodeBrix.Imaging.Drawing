using System;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingMotionBlurTests
{
    [Fact]
    public void Average_of_byte_frames_matches_the_accumulator()
    {
        //Arrange
        var frames = new[] { new byte[] { 0, 0, 0, 255, 9, 9, 9, 0 }, new byte[] { 255, 255, 255, 255, 9, 9, 9, 255 } };

        //Act
        byte[] result = DrawingMotionBlur.Average(frames, 2, 1);

        //Assert
        result.Should().Equal(new byte[] { 128, 128, 128, 255, 9, 9, 9, 128 });
    }

    [Fact]
    public void Average_of_float_images_matches_the_accumulator()
    {
        //Arrange
        var frames = new[]
        {
            new DrawingPlaneImage(2, 2).Fill(1f, 0f, 0f, 1f),
            new DrawingPlaneImage(2, 2).Fill(0f, 0f, 1f, 1f),
        };

        //Act
        DrawingPlaneImage result = DrawingMotionBlur.Average(frames);

        //Assert
        result.GetPixel(1, 1).Should().Be(new Vector4(0.5f, 0f, 0.5f, 1f));
    }

    [Fact]
    public void Average_rejects_empty_and_mismatched_input()
    {
        //Arrange
        Action empty = () => DrawingMotionBlur.Average(Array.Empty<DrawingPlaneImage>());
        Action mismatched = () => DrawingMotionBlur.Average(new[] { new DrawingPlaneImage(2, 2), new DrawingPlaneImage(3, 2) });
        Action shortFrame = () => DrawingMotionBlur.Average(new[] { new byte[3] }, 1, 1);

        //Act + Assert
        empty.Should().Throw<ArgumentException>();
        mismatched.Should().Throw<ArgumentException>();
        shortFrame.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetSampleTimes_centres_samples_in_the_open_shutter()
    {
        //Arrange + Act
        double[] times = DrawingMotionBlur.GetSampleTimes(2.0, 0.04, 4, 0.5);

        //Assert - the shutter is open for 0.02 s; four slices of 0.005 s, sampled at their centres
        times.Should().Equal(2.0 + (0.5 / 4 * 0.02), 2.0 + (1.5 / 4 * 0.02), 2.0 + (2.5 / 4 * 0.02), 2.0 + (3.5 / 4 * 0.02));
    }
}
