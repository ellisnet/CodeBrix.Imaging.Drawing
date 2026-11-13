using System;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// The animatable overrides of one part of a <see cref="DrawingSvgShape"/> - every element of the
/// loaded document carrying one <c>id</c>. The overrides are applied when the shape draws, as if
/// the element were wrapped in an extra group: an extra transform (translation, rotation and scale
/// about a pivot, all in the DOCUMENT's coordinate space - the space of <see cref="DrawingSvg.Bounds"/>
/// and <see cref="DocumentBounds"/>), an opacity, a visibility switch, and a trim of the part's
/// stroked paths (draw-on). The loaded document is never changed. Get a part with
/// <see cref="DrawingSvgShape.GetPart(string)"/>; set its values directly, or drive them with
/// <see cref="DrawingSvgShape.BindPart(DrawingAnimations, string, AnimatedProperty, Animation.KeyframeTrack{float})"/>.
/// <para>
/// Nesting: a part inside another animated part rides along with it - its own override is applied
/// inside its ancestors' (as nested groups would be), and opacities multiply. A trim applies to
/// the part's whole subtree; the nearest trimmed ancestor wins.
/// </para>
/// </summary>
public sealed class DrawingSvgPart
{
    private readonly DrawingSvgShape _owner;
    private PointF _translation;
    private float _rotation;
    private float _scaleX = 1f;
    private float _scaleY = 1f;
    private PointF? _pivot;
    private float _opacity = 1f;
    private bool _isVisible = true;
    private float _trimStart;
    private float _trimEnd = 1f;

    internal DrawingSvgPart(DrawingSvgShape owner, string id, RectangleF documentBounds, DrawingSvgNode node)
    {
        _owner = owner;
        Id = id;
        DocumentBounds = documentBounds;
        Node = node;
    }

    /// <summary>The element id the part stands for.</summary>
    public string Id { get; }

    /// <summary>The first scene node compiled from an element with this id (more nodes share it when a &lt;use&gt; instances it).</summary>
    public DrawingSvgNode Node { get; }

    /// <summary>The part's box in the document's coordinate space as loaded (every node with the id, unioned).</summary>
    public RectangleF DocumentBounds { get; }

    /// <summary>The centre of <see cref="DocumentBounds"/>.</summary>
    public PointF Center => new PointF(DocumentBounds.X + (DocumentBounds.Width / 2f), DocumentBounds.Y + (DocumentBounds.Height / 2f));

    /// <summary>The extra move, in document units; none by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a coordinate is not finite.</exception>
    public PointF Translation
    {
        get => _translation;
        set
        {
            if (!Single.IsFinite(value.X) || !Single.IsFinite(value.Y)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _translation) { return; }
            _translation = value;
            Changed();
        }
    }

    /// <summary>The extra rotation about the <see cref="Pivot"/>, degrees clockwise on screen; 0 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or infinity.</exception>
    public float Rotation
    {
        get => _rotation;
        set
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _rotation) { return; }
            _rotation = value;
            Changed();
        }
    }

    /// <summary>The extra horizontal scale about the <see cref="Pivot"/>; 1 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or infinity.</exception>
    public float ScaleX
    {
        get => _scaleX;
        set
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _scaleX) { return; }
            _scaleX = value;
            Changed();
        }
    }

    /// <summary>The extra vertical scale about the <see cref="Pivot"/>; 1 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or infinity.</exception>
    public float ScaleY
    {
        get => _scaleY;
        set
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _scaleY) { return; }
            _scaleY = value;
            Changed();
        }
    }

    /// <summary>
    /// The point the part turns and scales about, in document coordinates; <c>null</c> (the default)
    /// for the centre of each node's own box - so every &lt;use&gt; instance of a referenced element
    /// turns about its own centre.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a coordinate is not finite.</exception>
    public PointF? Pivot
    {
        get => _pivot;
        set
        {
            if (value.HasValue && (!Single.IsFinite(value.Value.X) || !Single.IsFinite(value.Value.Y)))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (value == _pivot) { return; }
            _pivot = value;
            Changed();
        }
    }

    /// <summary>The part's extra opacity, 0..1, multiplying its own; 1 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float Opacity
    {
        get => _opacity;
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _opacity) { return; }
            _opacity = value;
            Changed();
        }
    }

    /// <summary>Whether the part (and everything inside it) is drawn; <c>true</c> by default.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value == _isVisible) { return; }
            _isVisible = value;
            Changed();
        }
    }

    /// <summary>
    /// Where the part's stroked paths start being drawn, as a fraction 0..1 of each path's length
    /// (0 by default). Fills, images and text are not trimmed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float TrimStart
    {
        get => _trimStart;
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _trimStart) { return; }
            _trimStart = value;
            Changed();
        }
    }

    /// <summary>
    /// Where the part's stroked paths stop being drawn, as a fraction 0..1 of each path's length
    /// (1 by default) - animate it from 0 to 1 to draw the strokes on, every path at once.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float TrimEnd
    {
        get => _trimEnd;
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _trimEnd) { return; }
            _trimEnd = value;
            Changed();
        }
    }

    /// <summary>
    /// The extra transform as one value (its anchor is the <see cref="Pivot"/>, or
    /// <see cref="Center"/> when none is set); setting it sets the translation, rotation, scale and
    /// pivot together.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    public DrawingTransform Transform
    {
        get => new DrawingTransform(_translation, _rotation, _scaleX, _scaleY, _pivot ?? Center);
        set
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }
            _translation = value.Translation;
            _rotation = value.Rotation;
            _scaleX = value.ScaleX;
            _scaleY = value.ScaleY;
            _pivot = value.Anchor;
            Changed();
        }
    }

    /// <summary>Indicates whether any value differs from its default.</summary>
    public bool HasOverrides => HasTransform || _opacity != 1f || !_isVisible || HasTrim;

    private bool HasTransform => _translation.X != 0f || _translation.Y != 0f || _rotation != 0f || _scaleX != 1f || _scaleY != 1f;

    private bool HasTrim => _trimStart != 0f || _trimEnd != 1f;

    /// <summary>Puts every value back to its default (the part then draws exactly as loaded).</summary>
    public void Reset()
    {
        _translation = default;
        _rotation = 0f;
        _scaleX = 1f;
        _scaleY = 1f;
        _pivot = null;
        _opacity = 1f;
        _isVisible = true;
        _trimStart = 0f;
        _trimEnd = 1f;
        Changed();
    }

    //A frozen copy for one re-recording of the scene
    internal SvgPartState Snapshot() => new SvgPartState
    {
        IsVisible = _isVisible,
        Opacity = _opacity,
        HasTransform = HasTransform,
        Translation = _translation,
        Rotation = _rotation,
        ScaleX = _scaleX,
        ScaleY = _scaleY,
        Pivot = _pivot,
        HasTrim = HasTrim,
        TrimStart = _trimStart,
        TrimEnd = _trimEnd,
    };

    private void Changed() => _owner.PartChanged();
}
