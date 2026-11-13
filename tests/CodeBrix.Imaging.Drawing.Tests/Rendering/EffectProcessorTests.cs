using System;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Rendering;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Rendering;

public class EffectProcessorTests
{
    private static readonly DrawingEffects Shadow = DrawingEffects.DropShadow(new PointF(10, 0), 4, Color.Black);

    private static Matrix3x2 Turn(float degrees) => Matrix3x2.CreateRotation(degrees * MathF.PI / 180f);

    [Fact]
    public void Measure_of_a_plain_scale_is_exactly_the_scale()
    {
        //Act
        EffectProcessor.EffectSizes sizes = EffectProcessor.Measure(DrawingEffects.DropShadow(new PointF(3.3f, -7.1f), 4, Color.Black),
            Matrix3x2.CreateScale(1.37f), Matrix3x2.Identity);

        //Assert - the arithmetic a plain scale always had: radius x s, offset x s (float products)
        sizes.RadiusScale.Should().Be(1.37f);
        sizes.OffsetX.Should().Be(3.3f * 1.37f);
        sizes.OffsetY.Should().Be(-7.1f * 1.37f);
    }

    [Fact]
    public void Measure_scales_radii_by_the_geometric_mean_and_the_offset_per_axis()
    {
        //Act
        EffectProcessor.EffectSizes sizes = EffectProcessor.Measure(DrawingEffects.DropShadow(new PointF(10, 10), 4, Color.Black),
            Matrix3x2.CreateScale(4f, 1f), Matrix3x2.Identity);

        //Assert
        sizes.RadiusScale.Should().Be(2f);
        sizes.OffsetX.Should().Be(40f);
        sizes.OffsetY.Should().Be(10f);
    }

    [Fact]
    public void Measure_keeps_a_scene_fixed_shadow_pointing_the_same_way_when_the_element_turns()
    {
        //Act
        EffectProcessor.EffectSizes sizes = EffectProcessor.Measure(Shadow, Matrix3x2.CreateScale(2f) * Turn(90), Matrix3x2.Identity);

        //Assert
        sizes.RadiusScale.Should().BeApproximately(2f, 1e-5f);
        sizes.OffsetX.Should().BeApproximately(20f, 1e-4f);
        sizes.OffsetY.Should().BeApproximately(0f, 1e-4f);
    }

    [Fact]
    public void Measure_turns_a_shadow_that_rotates_with_the_element()
    {
        //Act - 90 degrees clockwise on screen (y down): right becomes down
        EffectProcessor.EffectSizes sizes = EffectProcessor.Measure(Shadow.WithShadowDirection(ShadowDirection.RotatesWithElement),
            Matrix3x2.CreateScale(2f) * Turn(90), Matrix3x2.Identity);

        //Assert
        sizes.OffsetX.Should().BeApproximately(0f, 1e-4f);
        sizes.OffsetY.Should().BeApproximately(20f, 1e-4f);
    }

    [Fact]
    public void Measure_turns_a_scene_fixed_shadow_with_the_scene_rotation()
    {
        //Arrange - the whole device map turned by the scene (a camera roll), the element unturned in the scene
        Matrix3x2 roll = Turn(-90);

        //Act
        EffectProcessor.EffectSizes sizes = EffectProcessor.Measure(Shadow, Matrix3x2.CreateScale(3f) * roll, roll);

        //Assert - right in the scene is up on the device
        sizes.RadiusScale.Should().BeApproximately(3f, 1e-5f);
        sizes.OffsetX.Should().BeApproximately(0f, 1e-4f);
        sizes.OffsetY.Should().BeApproximately(-30f, 1e-4f);
    }

    [Fact]
    public void Measure_never_mirrors_a_scene_fixed_shadow_but_mirrors_one_that_rotates_with_the_element()
    {
        //Arrange
        Matrix3x2 mirror = Matrix3x2.CreateScale(-1f, 1f);

        //Act
        EffectProcessor.EffectSizes fixedSizes = EffectProcessor.Measure(Shadow, mirror, Matrix3x2.Identity);
        EffectProcessor.EffectSizes turning = EffectProcessor.Measure(Shadow.WithShadowDirection(ShadowDirection.RotatesWithElement), mirror, Matrix3x2.Identity);

        //Assert
        fixedSizes.OffsetX.Should().Be(10f);
        turning.OffsetX.Should().Be(-10f);
        fixedSizes.RadiusScale.Should().Be(1f);
    }

    [Fact]
    public void Measure_of_a_zero_offset_or_a_collapsed_map_moves_nothing()
    {
        //Act
        EffectProcessor.EffectSizes none = EffectProcessor.Measure(DrawingEffects.Glow(5, Color.White), Matrix3x2.CreateScale(2f), Matrix3x2.Identity);
        EffectProcessor.EffectSizes collapsed = EffectProcessor.Measure(Shadow, Matrix3x2.CreateScale(0f), Matrix3x2.Identity);

        //Assert
        none.OffsetX.Should().Be(0f);
        none.OffsetY.Should().Be(0f);
        none.RadiusScale.Should().Be(2f);
        collapsed.OffsetX.Should().Be(0f);
        collapsed.RadiusScale.Should().Be(0f);
    }
}
