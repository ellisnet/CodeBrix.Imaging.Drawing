using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Covers the blend-mode family classification a consumer needs when it re-emits a picture
/// into a container with its own compositing model.
/// </summary>
public class DrawingBlendModeExtensionsTests
{
    [Theory]
    [InlineData(DrawingBlendMode.Clear)]
    [InlineData(DrawingBlendMode.SrcOver)]
    [InlineData(DrawingBlendMode.Xor)]
    public void Porter_duff_operators_are_recognized(DrawingBlendMode mode)
    {
        //Arrange + Act + Assert
        mode.IsPorterDuff().Should().BeTrue();
        mode.IsSeparableBlend().Should().BeFalse();
        mode.IsNonSeparableBlend().Should().BeFalse();
    }

    [Theory]
    [InlineData(DrawingBlendMode.Screen)]
    [InlineData(DrawingBlendMode.SoftLight)]
    [InlineData(DrawingBlendMode.Multiply)]
    public void Separable_blend_functions_are_recognized(DrawingBlendMode mode)
    {
        //Arrange + Act + Assert
        mode.IsSeparableBlend().Should().BeTrue();
        mode.IsPorterDuff().Should().BeFalse();
        mode.IsNonSeparableBlend().Should().BeFalse();
    }

    [Theory]
    [InlineData(DrawingBlendMode.Hue)]
    [InlineData(DrawingBlendMode.Color)]
    [InlineData(DrawingBlendMode.Luminosity)]
    public void Non_separable_blend_functions_are_recognized(DrawingBlendMode mode)
    {
        //Arrange + Act + Assert
        mode.IsNonSeparableBlend().Should().BeTrue();
        mode.IsPorterDuff().Should().BeFalse();
        mode.IsSeparableBlend().Should().BeFalse();
    }

    [Theory]
    [InlineData(DrawingBlendMode.Plus)]
    [InlineData(DrawingBlendMode.Modulate)]
    public void Plus_and_modulate_belong_to_no_family(DrawingBlendMode mode)
    {
        //Arrange + Act + Assert
        mode.IsPorterDuff().Should().BeFalse();
        mode.IsSeparableBlend().Should().BeFalse();
        mode.IsNonSeparableBlend().Should().BeFalse();
    }
}
