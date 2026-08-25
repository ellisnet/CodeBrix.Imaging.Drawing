namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// One recorded drawing operation in a <see cref="DrawingPicture"/>. The set of commands
/// is closed - every command is one of the sealed types in this namespace, and no new kind
/// can be introduced from outside this assembly - so a consumer that handles all of them
/// through <see cref="IDrawingCommandVisitor"/> handles every picture it will ever be
/// given. Commands are values: two commands with the same content are equal.
/// </summary>
public abstract record DrawingCommand
{
    private protected DrawingCommand()
    {
    }

    /// <summary>
    /// Calls the visitor overload for this command's concrete type. Kept internal so that
    /// the command set stays closed.
    /// </summary>
    /// <param name="visitor">The visitor to call.</param>
    internal abstract void Dispatch(IDrawingCommandVisitor visitor);
}
