namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Combines an axis-aligned rectangle, under the transform in force when the command runs,
/// into the canvas clip.
/// </summary>
/// <param name="Rect">The rectangle to clip with.</param>
/// <param name="Operation">How the rectangle combines with the current clip.</param>
/// <param name="Antialias">Whether the clip edge carries fractional coverage.</param>
public sealed record ClipRectCommand(DrawingRect Rect, DrawingClipOperation Operation, bool Antialias)
    : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
