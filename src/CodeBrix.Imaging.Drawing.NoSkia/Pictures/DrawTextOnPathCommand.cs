namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Draws a run of text laid out along a path. Recorded for completeness so that a consumer
/// can see the text, its path, and its style; laying glyphs along a path is not
/// implemented, so <see cref="GetOutline"/> always returns <c>null</c> and replay through
/// <see cref="DrawingCanvas.DrawPicture"/> skips the command without drawing or throwing.
/// </summary>
/// <param name="Text">The run's text.</param>
/// <param name="Path">The path the text follows.</param>
/// <param name="HorizontalOffset">The distance along the path at which the run starts.</param>
/// <param name="VerticalOffset">The offset perpendicular to the path.</param>
/// <param name="Paint">The paint the run would be filled and/or stroked with.</param>
/// <param name="Style">The typeface selection the run was recorded with.</param>
/// <param name="Outliner">The outliner the producer supplied; or <c>null</c>.</param>
public sealed record DrawTextOnPathCommand(
    string Text,
    DrawingPath Path,
    float HorizontalOffset,
    float VerticalOffset,
    DrawingPaint Paint,
    DrawingTextStyle Style,
    IDrawingTextOutliner Outliner) : DrawingCommand
{
    /// <summary>
    /// Always returns <c>null</c>: laying text along a path is not implemented.
    /// </summary>
    /// <returns><c>null</c>.</returns>
    public DrawingPath GetOutline() => null;

    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
