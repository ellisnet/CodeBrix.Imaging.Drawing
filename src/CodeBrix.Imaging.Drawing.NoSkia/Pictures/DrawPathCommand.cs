namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Draws a path, filled and/or stroked per the paint's style.
/// </summary>
/// <param name="Path">The path to draw.</param>
/// <param name="Paint">The paint to draw it with.</param>
public sealed record DrawPathCommand(DrawingPath Path, DrawingPaint Paint) : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
