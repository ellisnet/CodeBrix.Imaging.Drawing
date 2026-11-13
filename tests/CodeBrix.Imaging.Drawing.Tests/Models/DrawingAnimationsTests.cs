using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class DrawingAnimationsTests
{
    [Fact]
    public void Session_time_applies_element_bindings()
    {
        //Arrange
        using var session = new DrawingSession();
        session.AddLayer("Moving", Color.White);
        DrawingShape box = session.DrawRectangle(0, 0, 10, 10, filled: true);
        session.Animations.Bind(box, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 2, new PointF(100, 40) } });
        session.Animations.Bind(box, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 2, 90f } });
        session.Animations.Bind(box, AnimatedProperty.Scale, new FloatTrack { { 0, 1f }, { 2, 3f } });
        session.Animations.Bind(box, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(5, 5) } });
        session.Animations.Bind(box, AnimatedProperty.Opacity, new FloatTrack { { 0, 1f }, { 2, 0f } });

        //Act
        session.Time = 1;

        //Assert
        box.Transform.Translation.Should().Be(new PointF(50, 20));
        box.Transform.Rotation.Should().Be(45f);
        box.Transform.ScaleX.Should().Be(2f);
        box.Transform.ScaleY.Should().Be(2f);
        box.Transform.Anchor.Should().Be(new PointF(5, 5));
        box.Opacity.Should().Be(0.5f);
        session.Time.Should().Be(1);
    }

    [Fact]
    public void Opacity_bindings_clamp_overshooting_tracks()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var circle = new CircleShape(0, 0, 5);
        animations.Bind(circle, AnimatedProperty.Opacity, new FloatTrack { { 0, -1f }, { 1, 2f } });

        //Act + Assert
        animations.Apply(0);
        circle.Opacity.Should().Be(0f);
        animations.Apply(1);
        circle.Opacity.Should().Be(1f);
    }

    [Fact]
    public void Layer_bindings_set_transform_opacity_and_color()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var layer = new DrawingLayer("L", Color.Black);
        animations.Bind(layer, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 1, new PointF(10, 20) } });
        animations.Bind(layer, AnimatedProperty.ScaleX, new FloatTrack { { 0, 1f }, { 1, 3f } });
        animations.Bind(layer, AnimatedProperty.Opacity, new FloatTrack { { 0, 0f }, { 1, 1f } });
        animations.Bind(layer, AnimatedProperty.Color, new ColorTrack { { 0, Color.Black }, { 1, Color.White } });

        //Act
        animations.Apply(0.5);

        //Assert
        layer.Transform.Translation.Should().Be(new PointF(5, 10));
        layer.Transform.ScaleX.Should().Be(2f);
        layer.Transform.ScaleY.Should().Be(1f);
        layer.Opacity.Should().Be(0.5f);
        layer.Color.ToPixel<Rgba32>().Should().Be(new Rgba32(128, 128, 128, 255));
    }

    [Fact]
    public void Camera_and_group_bindings_set_their_properties()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var camera = new DrawingCamera();
        var group = new DrawingGroup();
        animations.Bind(camera, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 1, new PointF(8, 0) } });
        animations.Bind(camera, AnimatedProperty.Scale, new FloatTrack { { 0, 1f }, { 1, 2f } });
        animations.Bind(camera, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 1, 10f } });
        animations.Bind(camera, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(3, 4) } });
        animations.Bind(group, AnimatedProperty.Color, new ColorTrack { { 0, Color.Red } });

        //Act
        animations.Apply(0.5);

        //Assert
        camera.Pan.Should().Be(new PointF(4, 0));
        camera.Zoom.Should().Be(1.5f);
        camera.Rotation.Should().Be(5f);
        camera.Pivot.Should().Be(new PointF(3, 4));
        group.ContentColor.Should().Be(Color.Red);
    }

    [Fact]
    public void Bind_rejects_a_track_of_the_wrong_kind()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var circle = new CircleShape(0, 0, 5);
        var camera = new DrawingCamera();
        Action pointRotation = () => animations.Bind(circle, AnimatedProperty.Rotation, new PointTrack());
        Action floatPosition = () => animations.Bind(circle, AnimatedProperty.Position, new FloatTrack());
        Action cameraOpacity = () => animations.Bind(camera, AnimatedProperty.Opacity, new FloatTrack());
        Action groupScale = () => animations.Bind(new DrawingGroup(), AnimatedProperty.Scale, new ColorTrack());

        //Act + Assert
        pointRotation.Should().Throw<ArgumentException>();
        floatPosition.Should().Throw<ArgumentException>();
        cameraOpacity.Should().Throw<ArgumentException>();
        groupScale.Should().Throw<ArgumentException>();
        animations.Count.Should().Be(0);
    }

    [Fact]
    public void Bindings_apply_in_order_and_can_be_removed()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var log = new List<string>();
        AnimationBinding first = animations.Bind(t => log.Add("a" + t));
        animations.Bind(new FloatTrack { { 0, 7f } }, v => log.Add("b" + v));

        //Act
        animations.Apply(1);
        animations.Remove(first);
        animations.Apply(2);

        //Assert
        log.Should().Equal("a1", "b7", "b7");
        animations.Count.Should().Be(1);
        animations.GetBindings()[0].Property.Should().BeNull();
    }

    [Fact]
    public void Session_time_rejects_values_that_are_not_finite()
    {
        //Arrange
        using var session = new DrawingSession();
        Action act = () => session.Time = double.NaN;

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Bind_and_Remove_from_many_threads_keep_the_bindings_consistent()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var bindings = new AnimationBinding[500];

        //Act
        Parallel.For(0, bindings.Length, i => bindings[i] = animations.Bind(_ => { }));
        Parallel.For(0, bindings.Length / 2, i => animations.Remove(bindings[i]));

        //Assert
        animations.Count.Should().Be(bindings.Length - bindings.Length / 2);
        animations.GetBindings().Should().HaveCount(bindings.Length - bindings.Length / 2);
    }

    [Fact]
    public void Bind_a_color_track_to_a_group_held_as_an_element_animates_its_content_color()
    {
        //Arrange
        var animations = new DrawingAnimations();
        DrawingElement group = new DrawingGroup();
        DrawingElement line = new LineShape(0, 0, 10, 10);
        var track = new ColorTrack { { 0, Color.Black }, { 1, Color.White } };
        Action notAGroup = () => animations.Bind(line, AnimatedProperty.Color, track);

        //Act
        AnimationBinding binding = animations.Bind(group, AnimatedProperty.Color, track);
        animations.Apply(1);

        //Assert
        binding.Property.Should().Be(AnimatedProperty.Color);
        ((DrawingGroup)group).ContentColor.Should().Be(Color.White);
        notAGroup.Should().Throw<ArgumentException>();
    }
}
