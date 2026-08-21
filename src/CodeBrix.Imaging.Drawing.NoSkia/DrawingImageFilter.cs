namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A whole-surface image effect assigned to <see cref="DrawingPaint.ImageFilter"/> and
/// applied when a <see cref="DrawingCanvas.SaveLayer(DrawingPaint)"/> layer is restored -
/// the managed counterpart of a SkiaSharp <c>SKImageFilter</c>. This base class defines
/// only the evaluation contract; concrete effects (blur, offset, merge, and the other SVG
/// filter primitives) live in the SVG rendering assembly, which builds them from parsed
/// SVG filter chains.
/// </summary>
public abstract class DrawingImageFilter
{
    /// <summary>
    /// Applies the effect to a layer's pixels. The source bitmap holds the layer's content
    /// in device coordinates; the result must be a NEW bitmap of the same size holding the
    /// filtered content (the source must not be modified).
    /// </summary>
    /// <param name="source">The layer's rendered content.</param>
    /// <returns>A new bitmap holding the filtered content.</returns>
    public abstract DrawingBitmap Apply(DrawingBitmap source);
}
