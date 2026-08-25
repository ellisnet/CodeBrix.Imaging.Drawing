namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Receives the commands of a <see cref="DrawingPicture"/>. Every method has a default
/// do-nothing implementation, so an implementation only overrides the commands it cares
/// about - and, as a stability guarantee, a visitor written today keeps compiling and
/// running unchanged when new command kinds are added to this interface in a later
/// release. The corollary is that a visitor is never told about a command it did not
/// override, so a consumer that must handle everything should implement all of them.
/// <para>
/// <see cref="DrawingPicture.Accept"/> visits one level only: a visitor that wants to see
/// the contents of a nested picture must recurse itself from
/// <see cref="Visit(DrawPictureCommand)"/> by calling <c>command.Picture.Accept(this)</c>.
/// </para>
/// </summary>
public interface IDrawingCommandVisitor
{
    /// <summary>Visits a clip-path command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(ClipPathCommand command) { }

    /// <summary>Visits a clip-rectangle command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(ClipRectCommand command) { }

    /// <summary>Visits a draw-image command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(DrawImageCommand command) { }

    /// <summary>
    /// Visits a draw-picture command. Recurse with <c>command.Picture.Accept(this)</c> to
    /// see the nested picture's own commands.
    /// </summary>
    /// <param name="command">The command being visited.</param>
    void Visit(DrawPictureCommand command) { }

    /// <summary>Visits a draw-path command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(DrawPathCommand command) { }

    /// <summary>Visits a draw-text command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(DrawTextCommand command) { }

    /// <summary>Visits a positioned-text command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(DrawPositionedTextCommand command) { }

    /// <summary>Visits a text-on-path command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(DrawTextOnPathCommand command) { }

    /// <summary>Visits a save command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(SaveCommand command) { }

    /// <summary>Visits a restore command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(RestoreCommand command) { }

    /// <summary>Visits a save-layer command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(SaveLayerCommand command) { }

    /// <summary>Visits a set-matrix command.</summary>
    /// <param name="command">The command being visited.</param>
    void Visit(SetMatrixCommand command) { }
}
