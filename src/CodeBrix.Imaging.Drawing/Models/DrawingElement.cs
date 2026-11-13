using System;
using System.Threading;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Paths;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// The base type of everything that can be placed on a <see cref="DrawingLayer"/>: a
/// freehand <see cref="Stroke"/>, or a geometric shape derived from
/// <see cref="CodeBrix.Imaging.Drawing.Shapes.DrawingShape"/>. Elements on a layer render
/// in the order they were added.
/// <para>
/// For animation an element can also carry a <see cref="Transform"/>, an <see cref="Opacity"/>
/// and a time window (<see cref="StartTime"/>, <see cref="EndTime"/>) - all optional, all
/// changeable after the element is on a layer. Left at their defaults (identity, 1, always)
/// the element draws exactly as it always has.
/// </para>
/// </summary>
public abstract class DrawingElement
{
    //One counter for the whole process: every change to an element's appearance takes the
    //  next value, so a stamp identifies a state uniquely and only ever grows
    private static long s_lastStamp;

    private DrawingTransform _transform = DrawingTransform.Identity;
    private float _opacity = 1f;
    private double? _startTime;
    private double? _endTime;
    private long _stamp;
    private bool _isDynamic;
    private DrawingEffects _effects;

    /// <summary>
    /// Marks this element as one that matters for a safe-area check: when <c>true</c>,
    /// <see cref="CodeBrix.Imaging.Drawing.DrawingSession.CheckSafeArea(CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaGuide, Size, CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaCheckOptions)"/>
    /// reports it if it reaches into an unsafe area, whatever its layer's
    /// <see cref="DrawingLayer.IsSafeAreaChecked"/> says. Defaults to <c>false</c>. The flag
    /// is pure metadata: it never changes how the element is drawn.
    /// </summary>
    public bool IsSafeAreaChecked { get; set; }

    /// <summary>
    /// Where the element is drawn: moved, rotated and scaled about an anchor, in calibrated
    /// drawing units. <see cref="DrawingTransform.Identity"/> (the default) draws it where its
    /// own coordinates say; setting <c>null</c> restores the identity. Changing it takes effect
    /// on the next render (a layer holding a changed element redraws that layer's cache).
    /// </summary>
    public DrawingTransform Transform
    {
        get => _transform;
        set
        {
            DrawingTransform transform = value ?? DrawingTransform.Identity;
            if (transform.Equals(_transform)) { return; }
            _transform = transform;
            MarkChanged();
        }
    }

    /// <summary>
    /// The element's opacity, 0..1 (default 1). Below 1 the element is drawn as ONE unit at
    /// that opacity - the parts of a multi-part shape (an arrow's head and shaft, a group's
    /// children) do not show through each other. 0 draws nothing.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or a value outside 0..1.</exception>
    public float Opacity
    {
        get => _opacity;
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _opacity) { return; }
            _opacity = value;
            MarkChanged();
        }
    }

    /// <summary>
    /// When the element starts to exist, in seconds of the session's
    /// <see cref="CodeBrix.Imaging.Drawing.DrawingSession.Time"/>; or <c>null</c> (the default)
    /// for "from the beginning". Before it the element is not drawn, measured or checked.
    /// Seeking backwards needs no rebuilding: the element simply disappears again.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN.</exception>
    public double? StartTime
    {
        get => _startTime;
        set
        {
            if (value.HasValue && Double.IsNaN(value.Value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _startTime = value;
            _isDynamic = true;
        }
    }

    /// <summary>
    /// When the element stops existing (exclusive), in seconds of the session's time; or
    /// <c>null</c> (the default) for "until the end". The element exists while
    /// <c>StartTime &lt;= time &lt; EndTime</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN.</exception>
    public double? EndTime
    {
        get => _endTime;
        set
        {
            if (value.HasValue && Double.IsNaN(value.Value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _endTime = value;
            _isDynamic = true;
        }
    }

    /// <summary>
    /// Pixel effects - a blur of the element, a glow and a drop shadow behind it - computed from
    /// its drawn pixels in shared managed code (see <see cref="DrawingEffects"/>); or <c>null</c>
    /// (the default) for none. They are applied inside the element's opacity (the shadow fades
    /// with it) and after its own transform, in the frame's orientation. Setting it makes the
    /// element dynamic; an element with effects costs an offscreen render and the blur work each
    /// time its layer is drawn.
    /// </summary>
    public DrawingEffects Effects
    {
        get => _effects;
        set
        {
            if (value == _effects) { return; }
            _effects = value;
            MarkChanged();
        }
    }

    private protected DrawingElement()
    {
        //Only Stroke and DrawingShape derive directly from this type; custom element
        //  kinds derive from DrawingShape, whose Draw method renderers know how to call
    }

    /// <summary>
    /// Indicates whether the element exists at a moment: <see cref="StartTime"/> (when set) is
    /// at or before it and <see cref="EndTime"/> (when set) is after it.
    /// </summary>
    /// <param name="seconds">The moment, seconds.</param>
    /// <returns><c>true</c> when the element exists then.</returns>
    public bool IsVisibleAt(double seconds)
        => (!_startTime.HasValue || seconds >= _startTime.Value) && (!_endTime.HasValue || seconds < _endTime.Value);

    /// <summary>
    /// The area the element covers after its own <see cref="Transform"/>, as an axis-aligned
    /// rectangle in calibrated drawing units (its layer's transform and the camera are not
    /// included): the element's own bounds (a shape's <c>GetBounds()</c>, a stroke's
    /// <c>GetBounds()</c>) passed through <see cref="DrawingTransform.TransformBounds(RectangleF)"/>.
    /// With the identity transform it is exactly the element's own bounds.
    /// </summary>
    /// <returns>The bounds; or <c>null</c> when unknown (an empty stroke, a custom shape without bounds).</returns>
    public RectangleF? GetTransformedBounds()
    {
        RectangleF? local = GetLocalBounds();
        if (!local.HasValue || _transform.IsIdentity) { return local; }
        return _transform.TransformBounds(local.Value);
    }

    //The element's bounds in its own coordinates (before its Transform)
    internal virtual RectangleF? GetLocalBounds() => null;

    /// <summary>
    /// The element's geometry (a shape's <c>GetOutline()</c>, a stroke's <c>GetOutline()</c>)
    /// passed through its own <see cref="Transform"/> - exact, because the transform is affine -
    /// in calibrated drawing units (its layer's transform and the camera are not included).
    /// </summary>
    /// <returns>The geometry; or <c>null</c> when unknown (an empty stroke, a custom shape without an outline).</returns>
    public PathGeometry GetTransformedOutline()
    {
        PathGeometry local = GetLocalOutline();
        if (local == null || _transform.IsIdentity) { return local; }
        return local.Transformed(_transform.ToMatrix());
    }

    //The element's geometry in its own coordinates (before its Transform)
    internal virtual PathGeometry GetLocalOutline() => null;

    //True when the element has effects that change its pixels
    internal bool HasEffects => _effects != null && !_effects.IsEmpty;

    //True once anything beyond a plain, always-present mark has been asked of the element
    //  (a transform, an opacity, a time window - or the element is a group or animated shape).
    //  It never resets, so renderers may treat a non-dynamic element as unchanging
    internal bool IsDynamic => _isDynamic;

    //The element's appearance state: changes whenever something that alters its pixels changes
    internal long Stamp => _stamp;

    //The state a renderer's cache compares at a given time; groups and animated shapes fold
    //  in what they contain
    internal virtual long GetRenderStamp(double time) => _stamp;

    //Folds one more value into a running 64-bit state hash (a SplitMix64-style finalizer over
    //  both), for elements whose render state is made of other elements' states
    internal static ulong MixStamp(ulong hash, ulong value)
    {
        ulong z = hash ^ (value + 0x9E3779B97F4A7C15UL + (hash << 6) + (hash >> 2));
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    //Records a change of appearance (and makes the element dynamic for good)
    internal void MarkChanged()
    {
        _isDynamic = true;
        _stamp = Interlocked.Increment(ref s_lastStamp);
    }
}
