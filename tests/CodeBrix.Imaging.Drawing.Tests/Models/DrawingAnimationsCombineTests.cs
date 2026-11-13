using System;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class DrawingAnimationsCombineTests
{
    private static FloatTrack Ramp(float from, float to) => new FloatTrack { { 0, from }, { 1, to } };

    [Fact]
    public void An_add_binding_adds_to_the_replace_binding_whatever_the_binding_order()
    {
        //Arrange - the same two bindings made in both orders
        var first = new CircleShape(0, 0, 5);
        var second = new CircleShape(0, 0, 5);
        var a = new DrawingAnimations();
        a.Bind(first, AnimatedProperty.Rotation, Ramp(0, 90));
        a.Bind(first, AnimatedProperty.Rotation, Ramp(10, 20), BindingMode.Add);
        var b = new DrawingAnimations();
        b.Bind(second, AnimatedProperty.Rotation, Ramp(10, 20), BindingMode.Add);
        b.Bind(second, AnimatedProperty.Rotation, Ramp(0, 90));

        //Act
        a.Apply(0.5);
        b.Apply(0.5);

        //Assert
        first.Transform.Rotation.Should().Be(45f + 15f);
        second.Transform.Rotation.Should().Be(first.Transform.Rotation);
    }

    [Fact]
    public void Applying_again_does_not_accumulate_an_addition_without_a_replace_binding()
    {
        //Arrange - only an add binding: it adds to the resting value, never to its own last result
        var shape = new CircleShape(0, 0, 5) { Transform = new DrawingTransform(new PointF(0, 0), 30f) };
        var animations = new DrawingAnimations();
        animations.Bind(shape, AnimatedProperty.Rotation, Ramp(0, 10), BindingMode.Add);

        //Act
        animations.Apply(1);
        animations.Apply(1);
        animations.Apply(0.5);

        //Assert
        shape.Transform.Rotation.Should().Be(35f);
    }

    [Fact]
    public void Multiply_applies_before_add_and_both_combine_with_the_base()
    {
        //Arrange
        var shape = new CircleShape(0, 0, 5);
        var animations = new DrawingAnimations();
        animations.Bind(shape, AnimatedProperty.Scale, new FloatTrack { { 0, 0.1f } }, BindingMode.Add);
        animations.Bind(shape, AnimatedProperty.Scale, new FloatTrack { { 0, 2f } });
        animations.Bind(shape, AnimatedProperty.Scale, new FloatTrack { { 0, 1.5f } }, BindingMode.Multiply);

        //Act
        animations.Apply(0);

        //Assert - (2 x 1.5) + 0.1
        shape.Transform.ScaleX.Should().Be(3.1f);
        shape.Transform.ScaleY.Should().Be(3.1f);
    }

    [Fact]
    public void A_position_offset_adds_to_a_path_binding()
    {
        //Arrange
        var shape = new CircleShape(0, 0, 5);
        var animations = new DrawingAnimations();
        animations.Bind(shape, AnimatedProperty.Position, new PointTrack { { 0, new PointF(3, -4) } }, BindingMode.Add);
        animations.BindAlongPath(shape, PathGeometry.Line(new PointF(0, 0), new PointF(100, 0)), Ramp(0, 1));

        //Act
        animations.Apply(0.25);

        //Assert
        shape.Transform.Translation.Should().Be(new PointF(28, -4));
    }

    [Fact]
    public void Opacity_combines_then_clamps()
    {
        //Arrange
        var shape = new CircleShape(0, 0, 5);
        var animations = new DrawingAnimations();
        animations.Bind(shape, AnimatedProperty.Opacity, new FloatTrack { { 0, 0.8f } });
        animations.Bind(shape, AnimatedProperty.Opacity, new FloatTrack { { 0, 0.5f } }, BindingMode.Add);

        //Act
        animations.Apply(0);

        //Assert
        shape.Opacity.Should().Be(1f);
    }

    [Fact]
    public void A_function_binding_adds_a_computed_wave()
    {
        //Arrange
        var shape = new CircleShape(0, 0, 5);
        var animations = new DrawingAnimations();
        animations.Bind(shape, AnimatedProperty.Rotation, t => (float)(10 * t), BindingMode.Add);
        animations.Bind(shape, AnimatedProperty.Rotation, Ramp(0, 100));
        animations.Bind(shape, AnimatedProperty.ScaleY, t => 2f);

        //Act
        animations.Apply(0.5);

        //Assert
        shape.Transform.Rotation.Should().Be(55f);
        shape.Transform.ScaleY.Should().Be(2f);
        shape.Transform.ScaleX.Should().Be(1f);
    }

    [Fact]
    public void Layer_and_camera_bindings_combine()
    {
        //Arrange
        var layer = new DrawingLayer("Layer", Color.White);
        var camera = new DrawingCamera();
        var animations = new DrawingAnimations();
        animations.Bind(layer, AnimatedProperty.Opacity, new FloatTrack { { 0, 0.5f } }, BindingMode.Multiply);
        animations.Bind(layer, AnimatedProperty.Rotation, new FloatTrack { { 0, 5f } }, BindingMode.Add);
        animations.Bind(camera, AnimatedProperty.Position, new PointTrack { { 0, new PointF(100, 0) } });
        animations.Bind(camera, AnimatedProperty.Position, new PointTrack { { 0, new PointF(1, 2) } }, BindingMode.Add);
        animations.Bind(camera, AnimatedProperty.Scale, new FloatTrack { { 0, 2f } }, BindingMode.Multiply);

        //Act
        animations.Apply(0);
        animations.Apply(0);

        //Assert
        layer.Opacity.Should().Be(0.5f);
        layer.Transform.Rotation.Should().Be(5f);
        camera.Pan.Should().Be(new PointF(101, 2));
        camera.Zoom.Should().Be(2f);
    }

    [Fact]
    public void Removing_an_add_binding_stops_it_and_leaves_the_base()
    {
        //Arrange
        var shape = new CircleShape(0, 0, 5);
        var animations = new DrawingAnimations();
        animations.Bind(shape, AnimatedProperty.Rotation, Ramp(0, 100));
        AnimationBinding wobble = animations.Bind(shape, AnimatedProperty.Rotation, new FloatTrack { { 0, 7f } }, BindingMode.Add);
        animations.Apply(1);

        //Act
        bool removed = animations.Remove(wobble);
        animations.Apply(1);

        //Assert
        removed.Should().BeTrue();
        wobble.Mode.Should().Be(BindingMode.Add);
        shape.Transform.Rotation.Should().Be(100f);
    }

    [Fact]
    public void Replace_mode_is_the_plain_binding_and_later_wins_as_before()
    {
        //Arrange
        var shape = new CircleShape(0, 0, 5);
        var animations = new DrawingAnimations();
        AnimationBinding plain = animations.Bind(shape, AnimatedProperty.Rotation, Ramp(0, 10));
        animations.Bind(shape, AnimatedProperty.Rotation, Ramp(50, 60), BindingMode.Replace);

        //Act
        animations.Apply(1);

        //Assert
        plain.Mode.Should().Be(BindingMode.Replace);
        shape.Transform.Rotation.Should().Be(60f);
    }

    [Fact]
    public void Properties_that_cannot_combine_are_rejected()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var shape = new PathShape(PathGeometry.Circle(new PointF(0, 0), 4));

        //Act
        Action trim = () => animations.Bind(shape, AnimatedProperty.TrimEnd, Ramp(0, 1), BindingMode.Add);
        Action anchor = () => animations.Bind(shape, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(1, 1) } }, BindingMode.Add);
        Action undefined = () => animations.Bind(shape, AnimatedProperty.Rotation, Ramp(0, 1), (BindingMode)9);

        //Assert
        trim.Should().Throw<ArgumentException>();
        anchor.Should().Throw<ArgumentException>();
        undefined.Should().Throw<ArgumentOutOfRangeException>();
    }
}
