namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Draws a nested picture. A visitor that wants to see the nested commands must recurse
/// itself - call <c>command.Picture.Accept(this)</c> - because
/// <see cref="DrawingPicture.Accept"/> visits one level only. Nested pictures may be
/// shared: the same instance can appear in more than one command (an SVG <c>use</c>
/// element, for example), so a consumer that caches per picture should key on the
/// instance.
/// </summary>
/// <param name="Picture">The picture to draw.</param>
public sealed record DrawPictureCommand(DrawingPicture Picture) : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
