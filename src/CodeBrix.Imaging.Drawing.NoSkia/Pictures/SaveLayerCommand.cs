namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Pushes the canvas state and redirects drawing into an offscreen layer that is
/// composited with <paramref name="Paint"/> when the matching <see cref="RestoreCommand"/>
/// runs - how group opacity, masks, blend modes, and filter effects are recorded.
/// </summary>
/// <param name="Count">The number of saved states before this save, as the recorder observed it.</param>
/// <param name="Paint">
/// The paint the layer is composited with - its alpha, color filter, blend mode, and image
/// filter apply; or <c>null</c> for a plain layer.
/// </param>
/// <param name="Bounds">
/// The union of everything the layer draws, in the layer's own coordinate space; or
/// <c>null</c> when the layer draws nothing or the recorder could not determine it. This is
/// advisory: it lets a consumer size a layer, and it is not used when the picture is
/// replayed through <see cref="DrawingCanvas.DrawPicture"/>.
/// </param>
public sealed record SaveLayerCommand(int Count, DrawingPaint Paint, DrawingRect? Bounds) : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
