namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Pushes the current transform and clip onto the canvas state stack.
/// </summary>
/// <param name="Count">The number of saved states before this save, as the recorder observed it.</param>
public sealed record SaveCommand(int Count) : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
