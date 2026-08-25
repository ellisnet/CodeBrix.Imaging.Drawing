namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Draws one run of text at a baseline origin. The run keeps its characters, so a consumer
/// that can emit real text (a PDF writer, a search index) has them; a consumer that only
/// draws asks <see cref="GetOutline"/> for the filled outline the run renders as.
/// </summary>
/// <param name="Text">The run's text.</param>
/// <param name="X">The horizontal position of the run's origin.</param>
/// <param name="Y">The vertical position of the run's baseline.</param>
/// <param name="Paint">The paint the outline is filled and/or stroked with.</param>
/// <param name="Style">The typeface selection the run was recorded with.</param>
/// <param name="Outliner">
/// The outliner the producer supplied, which turns <paramref name="Text"/> into a path; or
/// <c>null</c> when no outline can be produced.
/// </param>
public sealed record DrawTextCommand(
    string Text,
    float X,
    float Y,
    DrawingPaint Paint,
    DrawingTextStyle Style,
    IDrawingTextOutliner Outliner) : DrawingCommand
{
    /// <summary>
    /// Builds the filled outline this run renders as, in the coordinate space the command
    /// was recorded in.
    /// </summary>
    /// <returns>
    /// The outline; or <c>null</c> when the command carries no outliner, or the outliner
    /// produced nothing.
    /// </returns>
    public DrawingPath GetOutline()
        => Outliner?.GetOutline(Text, Style, new DrawingPoint(X, Y));

    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
