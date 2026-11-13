using System;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class PathAnimationTests
{
    private static readonly PathGeometry Square = PathGeometry.Rectangle(new RectangleF(0, 0, 100, 100));

    [Fact]
    public void BindAlongPath_puts_the_anchor_on_the_path_and_turns_with_it()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var marker = new CircleShape(0, 0, 5) { Transform = new DrawingTransform(new PointF(0, 0), 0, 2, 2) };
        AnimationBinding binding = animations.BindAlongPath(marker, Square, new FloatTrack { { 0, 0f }, { 1, 1f } }, orientToPath: true, rotationOffset: 10f);

        //Act
        animations.Apply(0.375);

        //Assert - 150 along the square: halfway down the right side, heading down
        binding.Property.Should().Be(AnimatedProperty.Position);
        binding.Target.Should().BeSameAs(marker);
        marker.Transform.Translation.Should().Be(new PointF(100, 50));
        marker.Transform.Rotation.Should().Be(100f);
        marker.Transform.ScaleX.Should().Be(2f);
    }

    [Fact]
    public void BindAlongPath_with_an_anchor_places_the_anchor_and_keeps_rotation_when_not_oriented()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var marker = new RectangleShape(40, 40, 20, 20) { Transform = new DrawingTransform(new PointF(0, 0), 30f, 1, 1, new PointF(50, 50)) };
        animations.BindAlongPath(marker, Square, new FloatTrack { { 0, 0.125f } });

        //Act
        animations.Apply(0);

        //Assert - the anchor (50, 50) lands on (50, 0)
        marker.Transform.Translation.Should().Be(new PointF(0, -50));
        marker.Transform.TransformPoint(new PointF(50, 50)).Should().Be(new PointF(50, 0));
        marker.Transform.Rotation.Should().Be(30f);
    }

    [Fact]
    public void Trim_and_morph_properties_bind_to_path_shapes_only()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var shape = new PathShape(Square);
        var morphing = new PathShape(new PathMorph(Square, PathGeometry.Circle(new PointF(50, 50), 50)), 0f);
        var track = new FloatTrack { { 0, 0.25f }, { 1, 2f } };
        animations.Bind(shape, AnimatedProperty.TrimStart, new FloatTrack { { 0, 0.1f } });
        animations.Bind(shape, AnimatedProperty.TrimEnd, track);
        animations.Bind(shape, AnimatedProperty.TrimOffset, new FloatTrack { { 0, 3.5f } });
        animations.Bind(morphing, AnimatedProperty.MorphFraction, track);

        //Act
        animations.Apply(1);
        Action onCircle = () => animations.Bind(new CircleShape(0, 0, 1), AnimatedProperty.TrimEnd, track);
        Action noMorph = () => animations.Bind(shape, AnimatedProperty.MorphFraction, track);
        Action notSprite = () => animations.Bind(shape, AnimatedProperty.FrameIndex, track);
        Action tintOnPath = () => animations.Bind(shape, AnimatedProperty.Tint, new ColorTrack { { 0, Color.Red } });

        //Assert
        shape.TrimStart.Should().Be(0.1f);
        shape.TrimEnd.Should().Be(1f);
        shape.TrimOffset.Should().Be(3.5f);
        morphing.MorphFraction.Should().Be(1f);
        onCircle.Should().Throw<ArgumentException>();
        noMorph.Should().Throw<ArgumentException>();
        notSprite.Should().Throw<ArgumentException>();
        tintOnPath.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Session_helpers_add_path_image_and_sprite_shapes_to_the_active_layer()
    {
        //Arrange
        using var session = new DrawingSession();
        session.AddLayer("Layer", Color.Black);
        using var source = ImageSource.FromRgba(new byte[8], 2, 1);
        using var sheet = SpriteSheet.FromGrid(source, 2, 1);

        //Act
        PathShape path = session.DrawPath(Square, 4f, Color.Red, filled: true);
        ImageShape image = session.DrawImage(source, new RectangleF(0, 0, 10, 10));
        SpriteShape sprite = session.DrawSprite(sheet, new RectangleF(0, 0, 10, 10), 12, SpritePlayback.PingPong);

        //Assert
        session.StrokeCount.Should().Be(3);
        path.IsFilled.Should().BeTrue();
        image.Source.Should().BeSameAs(source);
        sprite.FramesPerSecond.Should().Be(12);
        sprite.Playback.Should().Be(SpritePlayback.PingPong);
    }
}
