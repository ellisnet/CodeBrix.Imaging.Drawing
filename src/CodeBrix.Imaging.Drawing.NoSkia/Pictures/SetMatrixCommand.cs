using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Changes the coordinate space. Replay concatenates <paramref name="Delta"/> onto the
/// canvas's current transform - never <paramref name="Total"/> - so any transform applied
/// to the canvas before the picture is replayed (output scaling, placement) survives.
/// </summary>
/// <param name="Delta">The transform to concatenate onto the current one.</param>
/// <param name="Total">
/// The recorder's total transform after this command, relative to the picture's own
/// coordinate space. Informational: useful for mapping recorded geometry into picture
/// space without replaying.
/// </param>
public sealed record SetMatrixCommand(Matrix3x2 Delta, Matrix3x2 Total) : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
