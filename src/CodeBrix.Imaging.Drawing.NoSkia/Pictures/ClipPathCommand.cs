namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Combines a path, under the transform in force when the command runs, into the canvas
/// clip. Nested and transformed clip shapes are already flattened into this one path by
/// the recorder.
/// </summary>
/// <param name="Path">The path to clip with.</param>
/// <param name="Operation">How the path combines with the current clip.</param>
/// <param name="Antialias">Whether the clip edge carries fractional coverage.</param>
public sealed record ClipPathCommand(DrawingPath Path, DrawingClipOperation Operation, bool Antialias)
    : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
