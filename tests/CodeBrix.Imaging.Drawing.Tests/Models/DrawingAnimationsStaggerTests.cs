using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class DrawingAnimationsStaggerTests
{
    private static List<DrawingElement> Squares(int count)
    {
        var list = new List<DrawingElement>();
        for (int i = 0; i < count; i++) { list.Add(new RectangleShape(i * 20, 0, 10, 10, 1f, Color.Black, isFilled: true)); }
        return list;
    }

    [Fact]
    public void BindStagger_fades_each_element_in_after_the_one_before()
    {
        //Arrange
        var animations = new DrawingAnimations();
        List<DrawingElement> squares = Squares(3);

        //Act
        IReadOnlyList<AnimationBinding> bindings = animations.BindStagger(squares, new StaggerOptions
        {
            StartTime = 1, Interval = 0.5, Duration = 1, Easing = EasingKind.Linear,
        });
        animations.Apply(1.5);

        //Assert
        bindings.Count.Should().Be(3);
        squares[0].Opacity.Should().BeApproximately(0.5f, 0.0001f);
        squares[1].Opacity.Should().Be(0f);
        squares[2].Opacity.Should().Be(0f);
        animations.Apply(10);
        squares[2].Opacity.Should().Be(1f);
        animations.Apply(0);
        squares[0].Opacity.Should().Be(0f);
    }

    [Fact]
    public void BindStagger_moves_scales_and_turns_about_each_centre()
    {
        //Arrange
        var animations = new DrawingAnimations();
        List<DrawingElement> squares = Squares(2);

        //Act
        animations.BindStagger(squares, new StaggerOptions
        {
            Interval = 1, Duration = 1, FromOffset = new PointF(0, 30), FromScale = 0.5f, FromRotation = 90f, Easing = EasingKind.Linear,
        });
        animations.Apply(0);

        //Assert
        squares[1].Transform.Anchor.Should().Be(new PointF(25, 5));
        squares[0].Transform.Translation.Should().Be(new PointF(0, 30));
        squares[0].Transform.ScaleX.Should().Be(0.5f);
        squares[0].Transform.Rotation.Should().Be(90f);
        animations.Apply(5);
        squares[1].Transform.Translation.Should().Be(new PointF(0, 0));
        squares[1].Transform.ScaleY.Should().Be(1f);
        squares[1].Transform.Rotation.Should().Be(0f);
    }

    [Theory]
    [InlineData(StaggerOrder.Forward, new[] { 0, 1, 2, 3, 4 })]
    [InlineData(StaggerOrder.Reverse, new[] { 4, 3, 2, 1, 0 })]
    [InlineData(StaggerOrder.CenterOut, new[] { 2, 1, 0, 1, 2 })]
    public void BindStagger_orders_the_starts(StaggerOrder order, int[] ranks)
    {
        //Arrange
        var animations = new DrawingAnimations();
        List<DrawingElement> squares = Squares(5);

        //Act
        animations.BindStagger(squares, new StaggerOptions { Order = order, Interval = 1, Duration = 0.5, Easing = EasingKind.Linear });

        //Assert - each element is half way in a quarter second after its start
        for (int i = 0; i < 5; i++)
        {
            animations.Apply((ranks[i] * 1.0) + 0.25);
            squares[i].Opacity.Should().BeApproximately(0.5f, 0.0001f);
        }
    }

    [Fact]
    public void BindStagger_rejects_bad_input()
    {
        //Arrange
        var animations = new DrawingAnimations();

        //Act
        Action nullList = () => animations.BindStagger(null);
        Action nullElement = () => animations.BindStagger(new DrawingElement[] { null });
        Action badInterval = () => animations.BindStagger(Squares(1), new StaggerOptions { Interval = -1 });
        Action badOpacity = () => animations.BindStagger(Squares(1), new StaggerOptions { FromOpacity = 2 });

        //Assert
        nullList.Should().Throw<ArgumentNullException>();
        nullElement.Should().Throw<ArgumentNullException>();
        badInterval.Should().Throw<ArgumentOutOfRangeException>();
        badOpacity.Should().Throw<ArgumentOutOfRangeException>();
        animations.Count.Should().Be(0);
    }
}
