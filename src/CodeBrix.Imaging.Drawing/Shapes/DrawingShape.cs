using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

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
    public Color? Color => GraphicsInterop.ToImaging(_color);

    /// <summary>
    /// The optional fill that paints the inside of a filled shape instead of its flat
    /// <see cref="Color"/> - for example a <see cref="LinearGradientFill"/> or a
    /// <see cref="RadialGradientFill"/>; or <c>null</c> (the default, and the value for
    /// every shape created without one) to paint in the flat color exactly as before.
    /// Outlines always draw in the flat color.
    /// </summary>
    public ShapeFill Fill { get; }

    //The name-neutral accessor that this library's own rendering code reads, so the public
    //  accessor below can carry a different name in each package without an #if at any call site
    internal SKColor? ColorValue => _color;

#if NOSKIA
    /// <summary>Gets <see cref="Color"/> as a <see cref="DrawingColor"/>.</summary>
    /// <returns>The shape's color as a backend color; or <c>null</c> to use the owning layer's color.</returns>
    public DrawingColor? GetColorAsDrawing() => _color;
#else
    /// <summary>Gets <see cref="Color"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The shape's color as a SkiaSharp color; or <c>null</c> to use the owning layer's color.</returns>
    public SKColor? GetColorAsSkia() => _color;
#endif

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
        : this(strokeThickness, GraphicsInterop.ToGraphics(color))
    {
    }

    /// <summary>
    /// Initializes the common shape values, including an optional <see cref="Fill"/>.
    /// </summary>
    /// <param name="strokeThickness">The outline thickness, in calibrated drawing units; must be positive.</param>
    /// <param name="color">The shape's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="fill">The fill that paints the shape's inside; or <c>null</c> to paint in the flat color.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="strokeThickness"/> is zero or negative.</exception>
    protected DrawingShape(float strokeThickness, Color? color, ShapeFill fill)
        : this(strokeThickness, GraphicsInterop.ToGraphics(color))
    {
        Fill = fill;
    }

    //For a shape whose thickness is unused (a filled-only path): 0 is then a valid thickness
    private protected DrawingShape(float strokeThickness, Color? color, ShapeFill fill, bool allowZeroThickness)
    {
        if (strokeThickness < 0 || (strokeThickness == 0 && !allowZeroThickness)) { throw new ArgumentOutOfRangeException(nameof(strokeThickness)); }
        StrokeThickness = strokeThickness;
        _color = GraphicsInterop.ToGraphics(color);
        Fill = fill;
    }

    /// <summary>
    /// Reports the area the shape covers, as an axis-aligned rectangle in calibrated
    /// drawing units, including the outline's thickness - what a safe-area check measures
    /// against. The default implementation returns <c>null</c>, meaning "unknown": custom
    /// shapes that do not override it keep compiling and drawing as before, and a check
    /// skips them (or lists them as not checkable, when asked to). The rectangle may
    /// over-cover the drawn ink slightly (curves are bounded by their control points) but
    /// never under-covers it.
    /// </summary>
    /// <returns>The shape's bounds in calibrated drawing units; or <c>null</c> when unknown.</returns>
    public virtual RectangleF? GetBounds() => null;

    internal override RectangleF? GetLocalBounds() => GetBounds();

    /// <summary>
    /// Reports the shape's GEOMETRY as a path in calibrated drawing units, before its own
    /// transform: the line an outline follows (its centre line - the stroke's thickness is not
    /// included) or the boundary of a filled area - for shape masks, polygon work and motion
    /// along a shape's edge (<see cref="PathGeometry.Flatten(float)"/> turns it into polygons).
    /// Every built-in shape reports one. The default implementation returns <c>null</c>, meaning
    /// "unknown": custom shapes that do not override it keep compiling and drawing as before.
    /// </summary>
    /// <returns>The shape's geometry; or <c>null</c> when unknown.</returns>
    public virtual PathGeometry GetOutline() => null;

    internal override PathGeometry GetLocalOutline() => GetOutline();

    /// <summary>
    /// Reports the AREA the shape covers when drawn, as fillable geometry in calibrated drawing
    /// units before its own transform: a point is covered when it lies inside ANY of the
    /// returned geometries under that geometry's own fill rule. A filled shape reports its
    /// outline; an outlined shape the area its outline's thickness covers (the stroke turned
    /// into a polygon, with the caps and joins it is drawn with). This is what the hard-edged
    /// mask (<see cref="MaskMeaning.HardEdged"/>) and the ownership map fill, under one shared
    /// pixel rule, so both packages give the same pixels. Paint plays no part: colours, fills
    /// and their alpha, effects and masks do not change it. Every built-in shape reports one.
    /// The default implementation returns <c>null</c>, meaning "unknown": a custom shape that
    /// does not override it covers nothing in those maps.
    /// </summary>
    /// <param name="tolerance">How closely curves are followed when an outline's thickness is turned into a polygon, calibrated units (positive).</param>
    /// <returns>The covered geometries; or <c>null</c> when unknown.</returns>
    public virtual IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance) => null;

    /// <summary>
    /// Reports the shape's own frame of reference - centre, direction, length and width - in
    /// its own calibrated coordinates, which an ownership map measures each owned pixel in (see
    /// <see cref="ElementAxes"/>). A brushstroke, a line, a rectangle, an ellipse and a circle
    /// report one; the default implementation returns <c>null</c>, and the map then uses the
    /// shape's bounds (<see cref="ElementAxes.FromBounds(RectangleF)"/>).
    /// </summary>
    /// <returns>The axes; or <c>null</c> when the shape has none of its own.</returns>
    public virtual ElementAxes? GetAxes() => null;

    //The area of an outline drawn with the standard outline paint (round caps and joins)
    internal IReadOnlyList<PathGeometry> OutlineArea(PathGeometry geometry, float tolerance)
        => geometry == null
            ? null
            : new[] { geometry.ToStrokeOutline(new PathStrokeStyle(StrokeThickness, PathStrokeCap.Round, PathStrokeJoin.Round), tolerance) };

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

    /// <summary>
    /// Creates the standard fill paint for shape drawing, painted by
    /// <paramref name="fill"/> when one is given: antialiased, solid fill. With a fill the
    /// paint's color is fully opaque, so the fill's own stop colors (and their alpha) are
    /// what is painted; without one this is the same as <see cref="CreateFillPaint(SKColor)"/>.
    /// </summary>
    /// <param name="color">The resolved color to draw with when <paramref name="fill"/> is null.</param>
    /// <param name="fill">The fill to paint with; or <c>null</c> for the flat color.</param>
    /// <returns>A new paint that the caller must dispose.</returns>
    protected static SKPaint CreateFillPaint(SKColor color, ShapeFill fill)
    {
        SKPaint paint = CreateFillPaint(color);
        if (fill != null)
        {
            paint.Color = SKColors.Black;
            //A fresh shader per paint, so a fill never holds a backend resource. The paint keeps
            //  its own reference to it (the native engine counts references), so this handle is
            //  released at once; the managed engine's shaders are ordinary objects
            SKShader shader = fill.CreateShader();
            paint.Shader = shader;
            (shader as IDisposable)?.Dispose();
        }
        return paint;
    }

    //Grows a rectangle by half an outline thickness on every side (the outline straddles
    //  the geometry, and round caps/joins reach exactly that far)
    internal static RectangleF BoundsOf(params PointF[] points)
    {
        float left = points[0].X, top = points[0].Y, right = left, bottom = top;
        for (int i = 1; i < points.Length; i++)
        {
            left = Math.Min(left, points[i].X);
            top = Math.Min(top, points[i].Y);
            right = Math.Max(right, points[i].X);
            bottom = Math.Max(bottom, points[i].Y);
        }
        return new RectangleF(left, top, right - left, bottom - top);
    }

    internal static RectangleF Inflate(RectangleF rect, float halfThickness)
        => new RectangleF(rect.X - halfThickness, rect.Y - halfThickness,
            rect.Width + (2f * halfThickness), rect.Height + (2f * halfThickness));
}
