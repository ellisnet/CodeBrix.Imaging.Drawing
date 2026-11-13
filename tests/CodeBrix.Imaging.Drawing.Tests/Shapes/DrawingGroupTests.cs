using System;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class DrawingGroupTests
{
    [Fact]
    public void Add_Remove_and_Clear_manage_the_children_in_order()
    {
        //Arrange
        var group = new DrawingGroup();
        var a = new CircleShape(0, 0, 1);
        var b = new LineShape(0, 0, 1, 1);

        //Act
        group.Add(a);
        group.Add(b);
        bool removed = group.Remove(a);

        //Assert
        removed.Should().BeTrue();
        group.GetChildren().Should().Equal(b);
        group.Clear();
        group.Count.Should().Be(0);
    }

    [Fact]
    public void A_group_cannot_contain_itself()
    {
        //Arrange
        var outer = new DrawingGroup();
        var inner = outer.Add(new DrawingGroup());
        Action self = () => outer.Add(outer);
        Action cycle = () => inner.Add(outer);
        Action none = () => outer.Add<DrawingElement>(null);

        //Act + Assert
        self.Should().Throw<ArgumentException>();
        cycle.Should().Throw<ArgumentException>();
        none.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bounds_cover_the_transformed_children_and_respect_time()
    {
        //Arrange
        var group = new DrawingGroup();
        group.Add(new RectangleShape(0, 0, 10, 10, 1, null, isFilled: true));
        RectangleShape late = group.Add(new RectangleShape(0, 0, 10, 10, 1, null, isFilled: true));
        late.Transform = DrawingTransform.Translate(50, 20);
        late.StartTime = 5;

        //Act + Assert
        group.GetBounds().Should().Be(new RectangleF(0, 0, 60, 30));
        group.GetBoundsAt(1).Should().Be(new RectangleF(0, 0, 10, 10));
        new DrawingGroup().GetBounds().Should().Be(RectangleF.Empty);
    }

    [Fact]
    public void Bounds_are_unknown_when_a_child_is_unknown()
    {
        //Arrange
        var group = new DrawingGroup();
        group.Add(new CircleShape(0, 0, 4));
        group.Add(new Stroke());

        //Act + Assert
        group.GetBounds().Should().BeNull();
    }

    [Fact]
    public void ContentColor_is_settable_and_starts_empty()
    {
        //Arrange
        var group = new DrawingGroup(Color.Blue);

        //Act
        group.ContentColor = Color.Green;

        //Assert
        group.Color.Should().Be(Color.Blue);
        group.ContentColor.Should().Be(Color.Green);
    }

    [Fact]
    public void Animated_shape_reports_its_current_shape()
    {
        //Arrange
        var shape = new AnimatedShape(t => t < 1 ? null : new CircleShape(0, 0, (float)t));

        //Act + Assert
        shape.Current.Should().BeNull();
        shape.GetBounds().Should().Be(RectangleF.Empty);
        shape.Update(2);
        shape.Current.Should().BeOfType<CircleShape>();
        shape.Time.Should().Be(2);
    }

    [Fact]
    public void Animated_shape_rejects_a_factory_that_returns_itself()
    {
        //Arrange
        AnimatedShape self = null;
        self = new AnimatedShape(t => t > 0 ? self : null);
        Action selfReturn = () => self.Update(1);
        Func<double, DrawingShape> none = null;
        Action missing = () => _ = new AnimatedShape(none);

        //Act + Assert
        selfReturn.Should().Throw<InvalidOperationException>();
        missing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_and_Remove_from_many_threads_keep_the_children_consistent()
    {
        //Arrange
        var group = new DrawingGroup();
        var lines = new LineShape[600];
        for (int i = 0; i < lines.Length; i++) { lines[i] = new LineShape(i, 0, i, 10); }

        //Act
        Parallel.For(0, lines.Length, i => group.Add(lines[i]));
        Parallel.For(0, lines.Length / 2, i => group.Remove(lines[i * 2]));

        //Assert
        group.Count.Should().Be(lines.Length / 2);
        DrawingElement[] children = group.GetChildren();
        children.Length.Should().Be(lines.Length / 2);
        foreach (DrawingElement child in children)
        {
            (Array.IndexOf(lines, child) % 2).Should().Be(1);
        }
    }
}
