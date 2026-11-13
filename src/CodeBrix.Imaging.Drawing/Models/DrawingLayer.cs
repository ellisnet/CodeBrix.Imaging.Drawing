using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Shapes;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// A named collection of drawing elements - freehand <see cref="Stroke"/> values and
/// geometric <see cref="DrawingShape"/> values - that are rendered together in the layer's
/// color (unless a shape carries its own). Layers are composited independently, so
/// overlapping elements within one layer never darken each other when the layer is
/// rendered translucently (the "highlighter" effect).
/// </summary>
public sealed class DrawingLayer
{
    private readonly List<DrawingElement> _elements = new List<DrawingElement>();
    private readonly object _elementsLocker = new object();

    private SKColor _color;
    private float _opacity = 1f;
    private DrawingTransform _transform = DrawingTransform.Identity;
    private float _parallax = 1f;

    /// <summary>
    /// The display name of the layer (for example <c>"Pain"</c>). Layer names are treated
    /// as case-sensitive identifiers and never change after the layer is created.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The color that the layer's elements are drawn with (except shapes that carry their
    /// own color). Changing the color causes the layer to be fully re-rendered on the
    /// next draw.
    /// </summary>
    public Color Color
    {
        get => GraphicsInterop.ToImaging(_color);
        set => SetColor(GraphicsInterop.ToGraphics(value));
    }

    /// <summary>
    /// Sets <see cref="Color"/> from a <see cref="SKColor"/>, for callers working
    /// in the rendering backend's value types.
    /// </summary>
    /// <param name="color">The color that the layer's elements are drawn with.</param>
    public void SetColor(SKColor color)
    {
        if (_color != color)
        {
            _color = color;
            BumpResetVersion();
        }
    }

    //The name-neutral accessor that this library's own rendering code reads, so the public
    //  accessor below can carry a different name in each package without an #if at any call site
    internal SKColor ColorValue => _color;

#if NOSKIA
    /// <summary>Gets <see cref="Color"/> as a <see cref="DrawingColor"/>.</summary>
    /// <returns>The layer color as a backend color.</returns>
    public DrawingColor GetColorAsDrawing() => _color;
#else
    /// <summary>Gets <see cref="Color"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The layer color as a SkiaSharp color.</returns>
    public SKColor GetColorAsSkia() => _color;
#endif

    /// <summary>
    /// A multiplier, 0..1, on the alpha this layer is composited with: the layer is drawn at
    /// the renderer's <c>LayerOpacity</c> multiplied by this value (and by the session's
    /// whole-drawing <c>Opacity</c>). The default of 1 composites exactly as before; animate
    /// it to fade one layer in or out. Changing it never re-rasterizes the layer's elements.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or a value outside 0..1.</exception>
    public float Opacity
    {
        get => _opacity;
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _opacity = value;
        }
    }

    /// <summary>
    /// Whether the layer is shown; <c>true</c> by default (exactly today's behaviour). A hidden
    /// layer is SKIPPED: the live render neither updates its cache nor composites it (it costs
    /// nothing while hidden, unlike a layer at <see cref="Opacity"/> 0, whose cache is still kept
    /// up to date), and exports, plane-image exports, masks of every meaning, ownership maps and
    /// safe-area checks leave it out as if it were not in the session. Its elements, animations
    /// and cache are kept (its bindings and animated shapes still follow the session's time, so it
    /// is up to date the moment it shows), and showing it again costs at most one redraw of this
    /// layer. A hidden
    /// layer still works as another layer's matte (<c>DrawingMask.FromLayer</c>, which never used
    /// the matte layer's own opacity either). Animate it as a step value with a
    /// <c>BoolTrack</c> bound to <see cref="AnimatedProperty.IsVisible"/>.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Where the whole layer is drawn: every element on it moved, rotated and scaled about an
    /// anchor, in calibrated drawing units - applied while drawing (not by resampling a
    /// picture), so the result is as sharp as untransformed content. The identity (the
    /// default) draws the layer exactly as before; setting <c>null</c> restores it. A change
    /// redraws this layer's cache on the next render.
    /// </summary>
    public DrawingTransform Transform
    {
        get => _transform;
        set => _transform = value ?? DrawingTransform.Identity;
    }

    /// <summary>
    /// How much the session's camera moves this layer: 1 (the default) moves it fully with the
    /// scene, 0 fixes it to the frame (a title or HUD the camera never touches), values between
    /// give the depth effect of a distant background, values above 1 a near foreground. The
    /// camera's pan and rotation are multiplied by it and its zoom raised to its power.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not finite.</exception>
    public float Parallax
    {
        get => _parallax;
        set
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _parallax = value;
        }
    }

    /// <summary>
    /// What part of the layer's content is shown - only inside or outside a path, only where
    /// another layer's elements are, or scaled by a pixel plane (see <see cref="DrawingMask"/>);
    /// or <c>null</c> (the default) for all of it. The mask is in the layer's element coordinates
    /// and is applied to the layer's whole content once, before its <see cref="Effects"/> and its
    /// opacity. A layer with a mask or effects is redrawn whole whenever anything on it (or in its
    /// mask) changes, instead of incrementally. Changing it takes effect on the next render.
    /// </summary>
    public DrawingMask Mask { get; set; }

    /// <summary>
    /// Pixel effects - blur, glow and drop shadow - applied to the layer's whole content (after
    /// its <see cref="Mask"/>), computed in shared managed code (see <see cref="DrawingEffects"/>);
    /// or <c>null</c> (the default) for none. Changing it takes effect on the next render.
    /// </summary>
    public DrawingEffects Effects { get; set; }

    //True when the layer's bitmap is post-processed (masked or with effects)
    internal bool HasPostProcessing => Mask != null || (Effects != null && !Effects.IsEmpty);

    /// <summary>
    /// Marks every element on this layer as one that matters for a safe-area check (see
    /// <see cref="DrawingElement.IsSafeAreaChecked"/>, which marks single elements).
    /// Defaults to <c>false</c>; it never changes how the layer is drawn.
    /// </summary>
    public bool IsSafeAreaChecked { get; set; }

    /// <summary>
    /// The total number of completed elements (strokes and shapes) currently on the layer.
    /// </summary>
    public int ElementCount
    {
        get
        {
            lock (_elementsLocker) { return _elements.Count; }
        }
    }

    /// <summary>
    /// A version counter that increases every time the layer changes in a way that
    /// invalidates previously rendered content (an element was removed, the layer was
    /// cleared, or the color changed). Renderers compare this value to decide whether
    /// their cached layer bitmap must be discarded and fully redrawn.
    /// </summary>
    public int ResetVersion { get; private set; }

    /// <summary>
    /// Creates a new, empty drawing layer.
    /// </summary>
    /// <param name="name">The display name of the layer.</param>
    /// <param name="color">The color that the layer's elements are drawn with.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public DrawingLayer(string name, SKColor color)
    {
        if (String.IsNullOrWhiteSpace(name)) { throw new ArgumentException("A layer name is required.", nameof(name)); }
        Name = name.Trim();
        _color = color;
    }

    /// <summary>
    /// Creates a new, empty drawing layer.
    /// </summary>
    /// <param name="name">The display name of the layer.</param>
    /// <param name="color">The color that the layer's elements are drawn with.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public DrawingLayer(string name, Color color)
        : this(name, GraphicsInterop.ToGraphics(color))
    {
    }

    /// <summary>
    /// Adds a completed stroke to the end of the layer's element list.
    /// </summary>
    /// <param name="stroke">The stroke to add; empty strokes (no points) are ignored.</param>
    /// <returns><c>true</c> when the stroke was added; <c>false</c> when it was null or empty.</returns>
    public bool AddStroke(Stroke stroke)
    {
        if (stroke == null || stroke.PointCount < 1) { return false; }
        lock (_elementsLocker)
        {
            _elements.Add(stroke);
        }
        return true;
    }

    /// <summary>
    /// Adds a geometric shape to the end of the layer's element list.
    /// </summary>
    /// <param name="shape">The shape to add.</param>
    /// <returns><c>true</c> when the shape was added; <c>false</c> when it was null.</returns>
    public bool AddShape(DrawingShape shape)
    {
        if (shape == null) { return false; }
        lock (_elementsLocker)
        {
            _elements.Add(shape);
        }
        return true;
    }

    /// <summary>
    /// Removes the most recently added element (stroke or shape) from the layer - i.e. a
    /// simple "undo".
    /// </summary>
    /// <returns><c>true</c> when an element was removed; <c>false</c> when the layer was already empty.</returns>
    public bool RemoveLastElement()
    {
        lock (_elementsLocker)
        {
            if (_elements.Count < 1) { return false; }
            _elements.RemoveAt(_elements.Count - 1);
        }
        BumpResetVersion();
        return true;
    }

    /// <summary>
    /// Removes all elements from the layer.
    /// </summary>
    public void Clear()
    {
        bool changed;
        lock (_elementsLocker)
        {
            changed = _elements.Count > 0;
            _elements.Clear();
        }
        if (changed) { BumpResetVersion(); }
    }

    /// <summary>
    /// Returns a snapshot copy of the layer's elements (strokes and shapes), in the order
    /// they were added - which is the order they render in.
    /// </summary>
    /// <returns>A new array holding the layer's elements.</returns>
    public DrawingElement[] GetElements()
    {
        lock (_elementsLocker) { return _elements.ToArray(); }
    }

    /// <summary>
    /// Returns a snapshot copy of only the freehand strokes on the layer, in the order
    /// they were added.
    /// </summary>
    /// <returns>A new array holding the layer's strokes.</returns>
    public Stroke[] GetStrokes()
    {
        lock (_elementsLocker)
        {
            var strokes = new List<Stroke>(_elements.Count);
            foreach (DrawingElement element in _elements)
            {
                if (element is Stroke stroke) { strokes.Add(stroke); }
            }
            return strokes.ToArray();
        }
    }

    private void BumpResetVersion() => ResetVersion = unchecked(ResetVersion + 1);

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({ElementCount} elements)";
}
