namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Draws a region of a decoded image into a destination rectangle. The image's original
/// encoded bytes are carried alongside the decoded pixels, so a consumer that re-emits the
/// picture into a container with its own image support (a PDF writer, for example) can
/// embed the original file instead of re-encoding the raster.
/// </summary>
/// <param name="Image">The decoded image.</param>
/// <param name="Source">The region of <paramref name="Image"/> to draw, in image pixels.</param>
/// <param name="Dest">The rectangle the region is scaled into.</param>
/// <param name="Paint">
/// The paint whose alpha, color filter, and blend mode apply to the draw; or <c>null</c>.
/// </param>
/// <param name="Sampling">The sampling used when the image's pixels are transformed.</param>
/// <param name="EncodedData">
/// The image's original encoded bytes; or <c>null</c> when the producer had none (the
/// image arrived as raw pixels).
/// </param>
/// <param name="EncodedFormat">
/// The format of <paramref name="EncodedData"/>; <c>null</c> when there are no encoded
/// bytes or the format could not be identified.
/// </param>
public sealed record DrawImageCommand(
    DrawingBitmap Image,
    DrawingRect Source,
    DrawingRect Dest,
    DrawingPaint Paint,
    DrawingSamplingOptions Sampling,
    byte[] EncodedData,
    DrawingEncodedImageFormat? EncodedFormat) : DrawingCommand
{
    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
