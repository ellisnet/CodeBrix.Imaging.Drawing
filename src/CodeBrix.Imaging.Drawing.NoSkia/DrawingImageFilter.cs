namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A whole-surface image effect assigned to <see cref="DrawingPaint.ImageFilter"/> and
/// applied when a <see cref="DrawingCanvas.SaveLayer(DrawingPaint)"/> layer is restored.
/// This base class defines only the evaluation contract; concrete effects (blur, offset,
/// merge, and the other SVG
/// filter primitives) live in the SVG rendering assembly, which builds them from parsed
/// SVG filter chains.
/// <para>
/// A filter holds its parameters in USER space - the coordinate space the document
/// expressed them in - and converts them to device pixels with the scale it is handed at
/// evaluation time. That is what lets one built filter (and therefore one converted
/// picture) render correctly at any output scale.
/// </para>
/// </summary>
public abstract class DrawingImageFilter
{
    /// <summary>
    /// A short, human-readable summary of the effect this filter applies - the primitive
    /// chain it was built from, outermost first. It exists so that a consumer inspecting a
    /// picture can report or log what a layer's filter does; it is descriptive only, and
    /// its exact wording is not a stable contract.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Applies the effect to a layer's pixels. The source bitmap holds the layer's content
    /// in device coordinates; the result must be a NEW bitmap of the same size holding the
    /// filtered content (the source must not be modified).
    /// </summary>
    /// <param name="source">The layer's rendered content.</param>
    /// <param name="deviceScale">
    /// The device pixels per user unit in force when the layer opened - the factor that
    /// converts the filter's user-space parameters (blur radii, offsets, clip regions) to
    /// the source bitmap's pixels.
    /// </param>
    /// <returns>A new bitmap holding the filtered content.</returns>
    public abstract DrawingBitmap Apply(DrawingBitmap source, float deviceScale);
}
