using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Pictures;

/// <summary>
/// Covers the display-list model itself: how a <see cref="DrawingPicture"/> is built, that
/// what it exposes cannot be changed behind its back, and the visitor contract - every
/// overload optional, one level of dispatch, and no exception of its own.
/// </summary>
public class DrawingPictureTests
{
    //A visitor that overrides NOTHING: this type existing and compiling IS the stability
    //  guarantee that IDrawingCommandVisitor's defaults are meant to provide
    private sealed class EmptyVisitor : IDrawingCommandVisitor
    {
    }

    private sealed class CountingVisitor : IDrawingCommandVisitor
    {
        public List<DrawingCommand> Visited { get; } = new List<DrawingCommand>();

        public void Visit(SaveCommand command) => Visited.Add(command);

        public void Visit(RestoreCommand command) => Visited.Add(command);

        public void Visit(DrawPathCommand command) => Visited.Add(command);

        public void Visit(DrawPictureCommand command) => Visited.Add(command);
    }

    private sealed class RecursingVisitor : IDrawingCommandVisitor
    {
        public int PathCount { get; private set; }

        public void Visit(DrawPathCommand command) => PathCount++;

        public void Visit(DrawPictureCommand command) => command.Picture.Accept(this);
    }

    private sealed class ThrowingVisitor : IDrawingCommandVisitor
    {
        public void Visit(SaveCommand command) => throw new InvalidOperationException("from the visitor");
    }

    private static DrawingPath BuildSquare(float size)
    {
        var builder = new DrawingPathBuilder();
        builder.AddRect(DrawingRect.Create(0, 0, size, size));
        return builder.Detach();
    }

    [Fact]
    public void Picture_keeps_its_cull_rect_and_commands_in_order()
    {
        //Arrange
        var cullRect = new DrawingRect(1, 2, 41, 52);
        var commands = new DrawingCommand[]
        {
            new SaveCommand(0),
            new DrawPathCommand(BuildSquare(10f), new DrawingPaint()),
            new RestoreCommand(0),
        };

        //Act
        var picture = new DrawingPicture(cullRect, commands);

        //Assert
        picture.CullRect.Should().Be(cullRect);
        picture.Commands.Count.Should().Be(3);
        picture.Commands[0].Should().BeOfType<SaveCommand>();
        picture.Commands[1].Should().BeOfType<DrawPathCommand>();
        picture.Commands[2].Should().BeOfType<RestoreCommand>();
    }

    [Fact]
    public void Picture_drops_null_commands_and_accepts_a_null_sequence()
    {
        //Arrange
        var commands = new DrawingCommand[] { new SaveCommand(0), null, new RestoreCommand(0) };

        //Act
        var withNulls = new DrawingPicture(DrawingRect.Empty, commands);
        var fromNull = new DrawingPicture(DrawingRect.Empty, null);

        //Assert
        withNulls.Commands.Count.Should().Be(2);
        fromNull.Commands.Count.Should().Be(0);
    }

    [Fact]
    public void Picture_commands_are_a_snapshot_of_the_sequence_it_was_built_from()
    {
        //Arrange
        var commands = new List<DrawingCommand> { new SaveCommand(0) };
        var picture = new DrawingPicture(DrawingRect.Empty, commands);

        //Act
        commands.Add(new RestoreCommand(0));
        commands[0] = new SaveCommand(99);

        //Assert
        picture.Commands.Count.Should().Be(1);
        picture.Commands[0].Should().Be(new SaveCommand(0));
    }

    [Fact]
    public void Commands_are_values_that_compare_by_content()
    {
        //Arrange
        var matrix = Matrix3x2.CreateScale(2f);

        //Act
        var first = new SetMatrixCommand(matrix, matrix);
        var second = new SetMatrixCommand(matrix, matrix);

        //Assert
        first.Should().Be(second);
        first.Should().NotBe(new SetMatrixCommand(Matrix3x2.Identity, matrix));
    }

    [Fact]
    public void A_visitor_that_overrides_nothing_still_runs()
    {
        //Arrange
        var picture = new DrawingPicture(DrawingRect.Empty, new DrawingCommand[]
        {
            new SaveCommand(0),
            new DrawPathCommand(BuildSquare(10f), new DrawingPaint()),
            new DrawTextOnPathCommand("abc", BuildSquare(10f), 0f, 0f, new DrawingPaint(), null, null),
            new RestoreCommand(0),
        });

        //Act
        Exception thrown = Record.Exception(() => picture.Accept(new EmptyVisitor()));

        //Assert
        thrown.Should().BeNull();
    }

    [Fact]
    public void Accept_visits_every_command_once_in_order()
    {
        //Arrange
        var save = new SaveCommand(0);
        var path = new DrawPathCommand(BuildSquare(10f), new DrawingPaint());
        var restore = new RestoreCommand(0);
        var picture = new DrawingPicture(DrawingRect.Empty, new DrawingCommand[] { save, path, restore });
        var visitor = new CountingVisitor();

        //Act
        picture.Accept(visitor);

        //Assert
        visitor.Visited.Count.Should().Be(3);
        visitor.Visited[0].Should().BeSameAs(save);
        visitor.Visited[1].Should().BeSameAs(path);
        visitor.Visited[2].Should().BeSameAs(restore);
    }

    [Fact]
    public void Accept_visits_one_level_only_and_the_visitor_recurses_itself()
    {
        //Arrange
        var nested = new DrawingPicture(DrawingRect.Empty, new DrawingCommand[]
        {
            new DrawPathCommand(BuildSquare(5f), new DrawingPaint()),
            new DrawPathCommand(BuildSquare(6f), new DrawingPaint()),
        });
        var outer = new DrawingPicture(DrawingRect.Empty, new DrawingCommand[]
        {
            new DrawPathCommand(BuildSquare(7f), new DrawingPaint()),
            new DrawPictureCommand(nested),
        });

        //Act
        var shallow = new CountingVisitor();
        outer.Accept(shallow);
        var deep = new RecursingVisitor();
        outer.Accept(deep);

        //Assert - the shallow visitor never sees the nested paths; the recursing one does
        shallow.Visited.Count.Should().Be(2);
        deep.PathCount.Should().Be(3);
    }

    [Fact]
    public void Accept_ignores_a_null_visitor()
    {
        //Arrange
        var picture = new DrawingPicture(DrawingRect.Empty, new DrawingCommand[] { new SaveCommand(0) });

        //Act
        Exception thrown = Record.Exception(() => picture.Accept(null));

        //Assert
        thrown.Should().BeNull();
    }

    [Fact]
    public void Accept_lets_the_visitors_own_exception_through()
    {
        //Arrange
        var picture = new DrawingPicture(DrawingRect.Empty, new DrawingCommand[] { new SaveCommand(0) });

        //Act
        Exception thrown = Record.Exception(() => picture.Accept(new ThrowingVisitor()));

        //Assert - dispatch adds no failure of its own, and hides none either
        thrown.Should().BeOfType<InvalidOperationException>();
    }
}
