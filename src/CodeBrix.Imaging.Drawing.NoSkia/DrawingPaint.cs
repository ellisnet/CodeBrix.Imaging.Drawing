using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Describes how geometry is drawn - its color, fill/stroke style, stroke geometry, and
/// anti-aliasing - reduced to the properties this managed implementation renders.
/// </summary>
public sealed class DrawingPaint : IDisposable
{
    /// <summary>
    /// The paint's color; opaque black by default. When a bitmap is
    /// drawn with a paint, the color's alpha modulates the bitmap's alpha.
    /// </summary>
    public DrawingColor Color { get; set; } = DrawingColors.Black;

    /// <summary>
    /// Whether geometry is filled, stroked, or both; <see cref="DrawingPaintStyle.Fill"/> by default.
    /// </summary>
    public DrawingPaintStyle Style { get; set; } = DrawingPaintStyle.Fill;

    /// <summary>
    /// The stroke width, in the coordinate space the geometry is expressed in. A width of
    /// zero requests a hairline (rendered one device pixel wide).
    /// </summary>
    public float StrokeWidth { get; set; }

    /// <summary>
    /// How the ends of open stroked paths are capped; <see cref="DrawingStrokeCap.Butt"/> by default.
    /// </summary>
    public DrawingStrokeCap StrokeCap { get; set; } = DrawingStrokeCap.Butt;

    /// <summary>
    /// How stroked path corners are joined; <see cref="DrawingStrokeJoin.Miter"/> by default.
    /// </summary>
    public DrawingStrokeJoin StrokeJoin { get; set; } = DrawingStrokeJoin.Miter;

    /// <summary>
    /// The miter limit for <see cref="DrawingStrokeJoin.Miter"/> joins - the maximum ratio of
    /// miter length to stroke width before the join falls back to a bevel; 4 by default.
    /// </summary>
    public float StrokeMiter { get; set; } = 4f;

    /// <summary>
    /// Whether edges are anti-aliased; <c>false</c> by default.
    /// </summary>
    public bool IsAntialias { get; set; }

    /// <summary>
    /// An optional shader (solid color or gradient) that supplies the paint's colors. When
    /// set, it replaces <see cref="Color"/>'s RGB - but <see cref="Color"/>'s alpha still
    /// modulates the shader's output.
    /// </summary>
    public DrawingShader Shader { get; set; }

    /// <summary>
    /// An optional per-pixel color transformation applied to the paint's output.
    /// </summary>
    public DrawingColorFilter ColorFilter { get; set; }

    /// <summary>
    /// How the paint's pixels combine with the destination;
    /// <see cref="DrawingBlendMode.SrcOver"/> by default.
    /// </summary>
    public DrawingBlendMode BlendMode { get; set; } = DrawingBlendMode.SrcOver;

    /// <summary>
    /// An optional path modification (dashing) applied before stroking.
    /// </summary>
    public DrawingPathEffect PathEffect { get; set; }

    /// <summary>
    /// An optional whole-surface effect applied when a
    /// <see cref="DrawingCanvas.SaveLayer(DrawingPaint)"/> layer drawn with this paint is
    /// restored.
    /// </summary>
    public DrawingImageFilter ImageFilter { get; set; }

    /// <summary>
    /// Creates a copy of this paint (sharing its shader/filter/effect instances, which are
    /// immutable).
    /// </summary>
    /// <returns>The copied paint.</returns>
    public DrawingPaint Clone() => (DrawingPaint)MemberwiseClone();

    /// <summary>
    /// Releases the paint. The managed implementation holds no unmanaged resources; this
    /// exists so callers can treat the paint as disposable.
    /// </summary>
    public void Dispose()
    {
    }
}
