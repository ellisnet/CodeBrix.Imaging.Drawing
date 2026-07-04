using System;
using CodeBrix.Imaging.Drawing.Models;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// The base type for geometric drawing primitives (lines, circles, rectangles, arrows,
/// etc.) that can be placed on a <see cref="DrawingLayer"/> alongside freehand strokes -
/// typically added programmatically, for example by computer-vision code annotating a
/// live video feed. Shapes participate fully in the layer model: they render into the
/// layer's cache, composite at the layer opacity, export, and undo exactly like strokes.
/// Derive from this class to create custom shape kinds.
/// </summary>
public abstract class DrawingShape : DrawingElement
{
    private readonly SKColor? _color;

    /// <summary>
    /// The outline thickness of the shape, in calibrated drawing units.
    /// </summary>
    public float StrokeThickness { get; }

    /// <summary>
    /// The color the shape is drawn with; or <c>null</c> to draw in the owning layer's
    /// color. A shape with its own color still composites at the layer's opacity.
    /// </summary>
    public Color? Color => SkiaInterop.ToImaging(_color);

    /// <summary>Gets <see cref="Color"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The shape's color as a SkiaSharp color; or <c>null</c> to use the owning layer's color.</returns>
    public SKColor? GetColorAsSkia() => _color;

    /// <summary>
    /// Initializes the common shape values.
    /// </summary>
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units; must be positive.</param>
    /// <param name="color">The shape's color; or <c>null</c> to use the owning layer's color.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="strokeThickness"/> is zero or negative.</exception>
    protected DrawingShape(float strokeThickness, SKColor? color)
    {
        if (strokeThickness <= 0) { throw new ArgumentOutOfRangeException(nameof(strokeThickness)); }
        StrokeThickness = strokeThickness;
        _color = color;
    }

    /// <summary>
    /// Initializes the common shape values.
    /// </summary>
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units; must be positive.</param>
    /// <param name="color">The shape's color; or <c>null</c> to use the owning layer's color.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="strokeThickness"/> is zero or negative.</exception>
    protected DrawingShape(float strokeThickness, Color? color)
        : this(strokeThickness, SkiaInterop.ToSK(color))
    {
    }

    /// <summary>
    /// Draws the shape. The canvas is pre-transformed to the calibrated drawing space, so
    /// implementations work entirely in calibrated coordinates - including paint stroke
    /// widths, which the transform scales to the output size automatically.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto, already transformed to calibrated space.</param>
    /// <param name="color">The resolved color to draw with (the shape's own color, or the layer's).</param>
    public abstract void Draw(SKCanvas canvas, SKColor color);

    /// <summary>
    /// Creates the standard outline paint for shape drawing: antialiased, round-capped
    /// and round-joined, at this shape's <see cref="StrokeThickness"/>.
    /// </summary>
    /// <param name="color">The resolved color to draw with.</param>
    /// <returns>A new paint that the caller must dispose.</returns>
    protected SKPaint CreateOutlinePaint(SKColor color) => new SKPaint
    {
        Style = SKPaintStyle.Stroke,
        Color = color,
        StrokeWidth = StrokeThickness,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        IsAntialias = true,
    };

    /// <summary>
    /// Creates the standard fill paint for shape drawing: antialiased, solid fill.
    /// </summary>
    /// <param name="color">The resolved color to draw with.</param>
    /// <returns>A new paint that the caller must dispose.</returns>
    protected static SKPaint CreateFillPaint(SKColor color) => new SKPaint
    {
        Style = SKPaintStyle.Fill,
        Color = color,
        IsAntialias = true,
    };
}
