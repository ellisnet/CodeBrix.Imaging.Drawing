using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// An immutable display list: the drawing commands a document (typically an SVG) compiles
/// down to, together with the bounds they were recorded against. A picture is the whole
/// consumer story for anything other than rasterizing - walk
/// <see cref="Commands"/> directly, or hand an <see cref="IDrawingCommandVisitor"/> to
/// <see cref="Accept"/> - and it is replayed onto pixels with
/// <see cref="DrawingCanvas.DrawPicture"/>.
/// </summary>
public sealed class DrawingPicture
{
    private readonly DrawingCommand[] _commands;

    /// <summary>
    /// Creates a picture from a sequence of commands.
    /// </summary>
    /// <param name="cullRect">
    /// The bounds the commands were recorded against, in the picture's own coordinate
    /// space. For a loaded SVG this is the document's area in CSS pixels, and its origin
    /// can be non-zero.
    /// </param>
    /// <param name="commands">
    /// The commands, in recording order; <c>null</c> entries are dropped, and a
    /// <c>null</c> sequence produces an empty picture.
    /// </param>
    public DrawingPicture(DrawingRect cullRect, IEnumerable<DrawingCommand> commands)
    {
        CullRect = cullRect;
        _commands = commands != null
            ? commands.Where(command => command != null).ToArray()
            : new DrawingCommand[0];
    }

    /// <summary>
    /// The bounds the commands were recorded against, in the picture's own coordinate
    /// space; the origin can be non-zero.
    /// </summary>
    public DrawingRect CullRect { get; }

    /// <summary>The recorded commands, in recording order.</summary>
    public IReadOnlyList<DrawingCommand> Commands => _commands;

    /// <summary>
    /// Calls the visitor's matching overload for each of this picture's own commands, in
    /// recording order. Nested pictures are NOT walked: a visitor that wants their
    /// commands recurses itself from <see cref="IDrawingCommandVisitor.Visit(DrawPictureCommand)"/>.
    /// Dispatch itself never throws - a <c>null</c> visitor is ignored, and every command
    /// kind has a default do-nothing overload - so the only exceptions that can escape are
    /// the ones the visitor's own code raises.
    /// </summary>
    /// <param name="visitor">The visitor to call; ignored when <c>null</c>.</param>
    public void Accept(IDrawingCommandVisitor visitor)
    {
        if (visitor == null) { return; }

        foreach (DrawingCommand command in _commands)
        {
            command.Dispatch(visitor);
        }
    }
}
