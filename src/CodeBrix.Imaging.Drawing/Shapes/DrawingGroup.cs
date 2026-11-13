using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Rendering;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A group of elements - shapes, strokes, other groups - placed on a layer as ONE element, so
/// they move, turn, scale and fade together through the group's own
/// <see cref="DrawingElement.Transform"/> and <see cref="DrawingElement.Opacity"/>. A group
/// fades as a unit: its children are drawn together first and the result is faded, so
/// overlapping children never show through each other. Children keep their own transforms,
/// opacities and time windows inside the group (a child's coordinates are the group's
/// coordinates). Children without a color of their own draw in the group's
/// <see cref="ContentColor"/>, else the group's own color, else the layer's.
/// <para>
/// The group is live: adding, removing or changing children takes effect on the next render
/// (the layer holding the group redraws). A group cannot contain itself, directly or through
/// another group. Adding and removing children is thread-safe; do not change a group while
/// another thread renders it.
/// </para>
/// </summary>
public sealed class DrawingGroup : DrawingShape
{
    private readonly object _childrenLocker = new object();
    private readonly List<DrawingElement> _children = new List<DrawingElement>();
    private SKColor? _contentColor;
    private DrawingMask _mask;

    /// <summary>
    /// Creates an empty group.
    /// </summary>
    /// <param name="color">The color its children without one of their own draw in; or <c>null</c> (the default) for the layer's.</param>
    public DrawingGroup(Color? color = null)
        : base(1f, color)
    {
        MarkChanged();
    }

    /// <summary>The number of children.</summary>
    public int Count
    {
        get
        {
            lock (_childrenLocker) { return _children.Count; }
        }
    }

    /// <summary>
    /// The color that children without a color of their own are drawn in, overriding the
    /// group's constructor color; or <c>null</c> (the default) to use that color (or the
    /// layer's). Animate it with <see cref="DrawingAnimations"/> and <see cref="AnimatedProperty.Color"/>.
    /// </summary>
    public Color? ContentColor
    {
        get => GraphicsInterop.ToImaging(_contentColor);
        set
        {
            SKColor? color = GraphicsInterop.ToGraphics(value);
            if (color == _contentColor) { return; }
            _contentColor = color;
            MarkChanged();
        }
    }

    internal SKColor? ContentColorValue => _contentColor;

    /// <summary>
    /// What part of the group's content is shown - only inside or outside a path, only where a
    /// layer's elements are, or scaled by a pixel plane (see <see cref="DrawingMask"/>); or
    /// <c>null</c> (the default) for all of it. The mask is in the group's coordinates, so it
    /// moves, turns and scales with the group; the children are drawn together first and the
    /// mask then applied once (before the group's effects and opacity). To mask a single
    /// element, put it in a group.
    /// </summary>
    public DrawingMask Mask
    {
        get => _mask;
        set
        {
            if (ReferenceEquals(value, _mask)) { return; }
            _mask = value;
            MarkChanged();
        }
    }

    /// <summary>
    /// Adds a child on top of the group's existing children.
    /// </summary>
    /// <typeparam name="T">The child's type.</typeparam>
    /// <param name="element">The child: a shape, a stroke or another group.</param>
    /// <returns>The same child, for further setup.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="element"/> is this group or contains it.</exception>
    public T Add<T>(T element) where T : DrawingElement
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (ReferenceEquals(element, this) || (element is DrawingGroup group && group.ContainsDeep(this)))
        {
            throw new ArgumentException("A group cannot contain itself.", nameof(element));
        }
        lock (_childrenLocker) { _children.Add(element); }
        MarkChanged();
        return element;
    }

    /// <summary>Removes a child.</summary>
    /// <param name="element">The child to remove.</param>
    /// <returns><c>true</c> when it was a child.</returns>
    public bool Remove(DrawingElement element)
    {
        bool removed;
        lock (_childrenLocker) { removed = _children.Remove(element); }
        if (removed) { MarkChanged(); }
        return removed;
    }

    /// <summary>Removes every child.</summary>
    public void Clear()
    {
        bool changed;
        lock (_childrenLocker)
        {
            changed = _children.Count > 0;
            _children.Clear();
        }
        if (changed) { MarkChanged(); }
    }

    /// <summary>Returns a snapshot copy of the children, in drawing order.</summary>
    /// <returns>A new array holding the children.</returns>
    public DrawingElement[] GetChildren()
    {
        lock (_childrenLocker) { return _children.ToArray(); }
    }

    /// <summary>
    /// Every child's outline after the child's own transform, in the group's coordinates (time
    /// windows ignored); <see cref="PathGeometry.Empty"/> for an empty group, <c>null</c> when any
    /// child's outline is unknown.
    /// </summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline()
    {
        DrawingElement[] children = GetChildren();
        var parts = new PathGeometry[children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            PathGeometry outline = children[i].GetTransformedOutline();
            if (outline == null) { return null; }
            parts[i] = outline;
        }
        return parts.Length == 0 ? PathGeometry.Empty : PathGeometry.Combine(parts);
    }

    /// <summary>
    /// The area every child covers (see <see cref="DrawingShape.GetCoverageAreas(float)"/>),
    /// each through the child's own transform, in the group's coordinates - time windows and
    /// opacities ignored (the hard-edged mask and the ownership map apply them, child by child,
    /// at the render time). Children whose area is unknown contribute nothing.
    /// </summary>
    /// <param name="tolerance">How closely outline bands are followed, in the group's units.</param>
    /// <returns>The children's areas, in drawing order.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
    {
        var areas = new List<PathGeometry>();
        foreach (DrawingElement child in GetChildren())
        {
            IReadOnlyList<PathGeometry> childAreas = TransformedAreas(child, tolerance);
            if (childAreas != null) { areas.AddRange(childAreas); }
        }
        return areas;
    }

    //An element's covered areas after its own transform (null when unknown); the tolerance is
    //  divided by the transform's largest stretch so it holds after the transform too
    internal static IReadOnlyList<PathGeometry> TransformedAreas(DrawingElement element, float tolerance)
    {
        HardEdgeMatrix matrix = HardEdgeMatrix.From(element.Transform.ToMatrix());
        double stretch = matrix.MaxStretch;
        if (!(stretch > 0) || !double.IsFinite(stretch)) { return null; }
        float local = (float)(tolerance / stretch);
        if (!(local > 0f) || !float.IsFinite(local)) { return null; }
        IReadOnlyList<PathGeometry> areas;
        switch (element)
        {
            case Stroke stroke:
                PathGeometry area = CoverageCollector.StrokeArea(stroke, local);
                areas = area == null ? null : new[] { area };
                break;
            case DrawingShape shape:
                areas = shape.GetCoverageAreas(local);
                break;
            default:
                areas = null;
                break;
        }
        if (areas == null || element.Transform.IsIdentity) { return areas; }
        var result = new PathGeometry[areas.Count];
        Matrix3x2 transform = element.Transform.ToMatrix();
        for (int i = 0; i < areas.Count; i++) { result[i] = areas[i].Transformed(transform); }
        return result;
    }

    /// <summary>
    /// The box around every child (each after its own transform), in the group's coordinates
    /// - before the group's own transform; <see cref="RectangleF.Empty"/> for an empty group,
    /// <c>null</c> when any child's bounds are unknown. Time windows are ignored (see
    /// <see cref="GetBoundsAt(double)"/>).
    /// </summary>
    /// <returns>The bounds; or <c>null</c> when unknown.</returns>
    public override RectangleF? GetBounds() => GetBoundsCore(null);

    /// <summary>
    /// The box around the children that exist at a moment (see
    /// <see cref="DrawingElement.IsVisibleAt(double)"/>), each after its own transform, in the
    /// group's coordinates.
    /// </summary>
    /// <param name="seconds">The moment.</param>
    /// <returns>The bounds (<see cref="RectangleF.Empty"/> when no child exists then); or <c>null</c> when any is unknown.</returns>
    public RectangleF? GetBoundsAt(double seconds) => GetBoundsCore(seconds);

    /// <summary>
    /// Draws every child (time windows are not applied - renderers draw the children that
    /// exist at the session's time) at its own transform and opacity, in the group's
    /// coordinates. The group's own transform and opacity are applied by whoever draws the
    /// group as an element.
    /// </summary>
    /// <param name="canvas">The canvas, already transformed to calibrated space.</param>
    /// <param name="color">The color children without one of their own are drawn in.</param>
    public override void Draw(SKCanvas canvas, SKColor color)
        => ElementPainter.DrawGroupChildren(canvas, this, color, null);

    internal override long GetRenderStamp(double time)
    {
        //A hash over the group's own state and each child's (visibility, state) in order:
        //  a change anywhere inside changes it
        ulong hash = 14695981039346656037UL;
        hash = MixStamp(hash, (ulong)Stamp);
        DrawingMask mask = _mask;
        if (mask != null) { hash = MixStamp(hash, (ulong)mask.GetStamp(time)); }
        foreach (DrawingElement child in GetChildren())
        {
            bool visible = child.IsVisibleAt(time);
            hash = MixStamp(hash, visible ? 1UL : 2UL);
            if (visible) { hash = MixStamp(hash, (ulong)child.GetRenderStamp(time)); }
        }
        return (long)hash;
    }

    internal bool ContainsDeep(DrawingElement element)
    {
        foreach (DrawingElement child in GetChildren())
        {
            if (ReferenceEquals(child, element)) { return true; }
            if (child is DrawingGroup group && group.ContainsDeep(element)) { return true; }
        }
        return false;
    }

    private RectangleF? GetBoundsCore(double? seconds)
    {
        RectangleF? union = null;
        foreach (DrawingElement child in GetChildren())
        {
            if (seconds.HasValue && !child.IsVisibleAt(seconds.Value)) { continue; }
            RectangleF? bounds = child is DrawingGroup group && seconds.HasValue
                ? TransformedGroupBounds(group, seconds.Value)
                : child.GetTransformedBounds();
            if (!bounds.HasValue) { return null; }
            union = union.HasValue ? RectangleF.Union(union.Value, bounds.Value) : bounds.Value;
        }
        return union ?? RectangleF.Empty;
    }

    private static RectangleF? TransformedGroupBounds(DrawingGroup group, double seconds)
    {
        RectangleF? local = group.GetBoundsAt(seconds);
        if (!local.HasValue || group.Transform.IsIdentity) { return local; }
        return group.Transform.TransformBounds(local.Value);
    }
}
