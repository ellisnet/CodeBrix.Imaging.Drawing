namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Pops the most recently saved canvas state; when that state opened a layer, the layer is
/// composited.
/// </summary>
/// <param name="Count">The number of saved states after this restore, as the recorder observed it.</param>
public sealed record RestoreCommand(int Count) : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
