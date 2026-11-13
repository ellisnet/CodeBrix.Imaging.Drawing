using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// A loaded SVG document (<see cref="DrawingSvg"/>) drawn as a session element: its bounds fitted
/// into a destination rectangle in calibrated units (<see cref="DrawingSvgAspect"/>). As a
/// <see cref="DrawingShape"/> it takes part in everything elements do - transforms, opacity, groups,
/// time windows, the camera, layout, bounds and safe-area checks, masks and effects.
/// <para>
/// PARTS: every element <c>id</c> in the document is a part (<see cref="GetPartIds"/>) whose
/// transform, opacity, visibility and stroke trim can be set or animated
/// (<see cref="GetPart(string)"/>, <see cref="BindPart(DrawingAnimations, string, AnimatedProperty, KeyframeTrack{float})"/>)
/// without changing the loaded document: when a part changes, the shape re-records the compiled
/// scene with the overrides applied at those nodes (a document without overrides draws its loaded
/// picture directly). Ids are matched on the compiled scene: an id inside a &lt;use&gt;d element is
/// on every instance (each turns about its own centre unless a pivot is set; give the &lt;use&gt;
/// element its own id to animate one instance); content inside &lt;defs&gt;, &lt;clipPath&gt;,
/// pattern tiles and filter inputs is not a drawn part, so ids there have no effect - except mask
/// content, which is drawn with its overrides.
/// </para>
/// <para>
/// NoSkia package only. Fonts for SVG text are registered on the <see cref="DrawingSvg"/> before
/// it is loaded, as always.
/// </para>
/// </summary>
public sealed class DrawingSvgShape : DrawingShape
{
    private readonly object _locker = new object();
    private readonly Dictionary<string, DrawingSvgPart> _parts = new Dictionary<string, DrawingSvgPart>(StringComparer.Ordinal);
    private readonly List<DrawingSvgPart> _partList = new List<DrawingSvgPart>();
    private int _version;
    private int _pictureVersion = -1;
    private int _pictureLoad = -1;
    private DrawingPicture _picture;

    /// <summary>
    /// Creates the element.
    /// </summary>
    /// <param name="svg">A loaded document (see <see cref="DrawingSvg.IsLoaded"/>). The shape refers to it; keep it alive.</param>
    /// <param name="destination">Where the document is fitted, calibrated units.</param>
    /// <param name="aspect">How it is fitted; <see cref="DrawingSvgAspect.Meet"/> by default.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="svg"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no document has been loaded.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the destination is not finite or the aspect is not defined.</exception>
    public DrawingSvgShape(DrawingSvg svg, RectangleF destination, DrawingSvgAspect aspect = DrawingSvgAspect.Meet)
        : base(1f, (Color?)null)
    {
        Svg = svg ?? throw new ArgumentNullException(nameof(svg));
        if (!svg.IsLoaded) { throw new ArgumentException("Load an SVG document before creating a shape from it.", nameof(svg)); }
        if (!Single.IsFinite(destination.X) || !Single.IsFinite(destination.Y) || !Single.IsFinite(destination.Width)
            || !Single.IsFinite(destination.Height) || destination.Width < 0f || destination.Height < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }
        if (!Enum.IsDefined(aspect)) { throw new ArgumentOutOfRangeException(nameof(aspect)); }
        Destination = destination;
        Aspect = aspect;
    }

    /// <summary>The document the shape draws.</summary>
    public DrawingSvg Svg { get; }

    /// <summary>The rectangle the document is fitted into, calibrated units.</summary>
    public RectangleF Destination { get; }

    /// <summary>How the document is fitted.</summary>
    public DrawingSvgAspect Aspect { get; }

    /// <summary>The document's bounds in its own coordinates (<see cref="DrawingSvg.Bounds"/>).</summary>
    public RectangleF DocumentBounds
    {
        get
        {
            DrawingRect b = Svg.Bounds;
            return new RectangleF(b.Left, b.Top, b.Width, b.Height);
        }
    }

    /// <summary>Where the document's bounds land, calibrated units (for <see cref="DrawingSvgAspect.Slice"/> it overhangs the destination).</summary>
    public RectangleF ContentBounds
    {
        get
        {
            RectangleF doc = DocumentBounds;
            Fit(out float sx, out float sy, out float dx, out float dy);
            return new RectangleF(dx, dy, doc.Width * sx, doc.Height * sy);
        }
    }

    /// <summary>The parts whose overrides have been asked for (by <see cref="GetPart(string)"/> or a binding), in that order.</summary>
    public IReadOnlyList<DrawingSvgPart> Parts
    {
        get
        {
            lock (_locker) { return _partList.ToArray(); }
        }
    }

    /// <summary>Every element id in the compiled document, in document order (each once).</summary>
    /// <returns>The ids.</returns>
    public IReadOnlyList<string> GetPartIds()
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        SvgSceneDocument scene = Svg.SceneDocument;
        if (scene == null) { return ids; }
        foreach (SvgSceneNode node in scene.Traverse())
        {
            if (!String.IsNullOrWhiteSpace(node.ElementId) && seen.Add(node.ElementId)) { ids.Add(node.ElementId); }
        }
        return ids;
    }

    /// <summary>Indicates whether the compiled document has an element with an id.</summary>
    /// <param name="id">The id.</param>
    /// <returns><c>true</c> when it is a part.</returns>
    public bool HasPart(string id) => id != null && Svg.SceneDocument != null && FindNodes(id).Count > 0;

    /// <summary>The overrides of the part with an id - created (with every value at its default) the first time it is asked for.</summary>
    /// <param name="id">The element id.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the document has no element with that id.</exception>
    public DrawingSvgPart GetPart(string id)
    {
        if (id == null) { throw new ArgumentNullException(nameof(id)); }
        lock (_locker)
        {
            if (_parts.TryGetValue(id, out DrawingSvgPart part)) { return part; }
            List<SvgSceneNode> nodes = FindNodes(id);
            if (nodes.Count == 0) { throw new ArgumentException($"The document has no element with the id '{id}'.", nameof(id)); }
            SKRect union = nodes[0].TransformedBounds;
            for (int i = 1; i < nodes.Count; i++) { union = Union(union, nodes[i].TransformedBounds); }
            DrawingSvgNode wrapper = null;
            Svg.Scene?.TryGetNodeById(id, out wrapper);
            part = new DrawingSvgPart(this, id, new RectangleF(union.Left, union.Top, union.Width, union.Height), wrapper);
            _parts[id] = part;
            _partList.Add(part);
            return part;
        }
    }

    /// <summary>Puts every part's overrides back to their defaults.</summary>
    public void ResetParts()
    {
        foreach (DrawingSvgPart part in Parts) { part.Reset(); }
    }

    /// <summary>
    /// Where a part is drawn now - every node with the id, through its own and its ancestors'
    /// overrides and the fit - as a box in calibrated units (the axis-aligned box of the moved
    /// document box, so a turned part's box covers more than its ink).
    /// </summary>
    /// <param name="id">The element id.</param>
    /// <returns>The box; or <c>null</c> when the document has no such element.</returns>
    public RectangleF? GetPartBounds(string id)
    {
        if (id == null) { return null; }
        List<SvgSceneNode> nodes = FindNodes(id);
        if (nodes.Count == 0) { return null; }
        Dictionary<string, SvgPartState> states = Snapshot();
        Fit(out float sx, out float sy, out float dx, out float dy);
        RectangleF doc = DocumentBounds;
        var fit = new Matrix3x2(sx, 0f, 0f, sy, dx - (doc.X * sx), dy - (doc.Y * sy));
        RectangleF? result = null;
        foreach (SvgSceneNode node in nodes)
        {
            SKRect b = node.TransformedBounds;
            Matrix3x2 m = SvgPartRenderer.GetDocumentMatrix(node, states) * fit;
            RectangleF mapped = DrawingTransform.TransformBounds(new RectangleF(b.Left, b.Top, b.Width, b.Height), m);
            result = result.HasValue ? UnionF(result.Value, mapped) : mapped;
        }
        return result;
    }

    /// <summary>Maps a point in the document's coordinates to calibrated units, through the fit.</summary>
    /// <param name="documentPoint">The point, document units.</param>
    /// <returns>The point, calibrated units.</returns>
    public PointF MapToCalibrated(PointF documentPoint)
    {
        Fit(out float sx, out float sy, out float dx, out float dy);
        RectangleF doc = DocumentBounds;
        return new PointF(dx + ((documentPoint.X - doc.X) * sx), dy + ((documentPoint.Y - doc.Y) * sy));
    }

    /// <summary>
    /// Binds a float track to a part: <see cref="AnimatedProperty.Rotation"/>,
    /// <see cref="AnimatedProperty.Scale"/> (both axes), <see cref="AnimatedProperty.ScaleX"/>,
    /// <see cref="AnimatedProperty.ScaleY"/>, <see cref="AnimatedProperty.Opacity"/>,
    /// <see cref="AnimatedProperty.TrimStart"/> or <see cref="AnimatedProperty.TrimEnd"/> (fractions and
    /// opacity clamped to 0..1). Applied with the session's other bindings whenever its time is set.
    /// </summary>
    /// <param name="animations">The bindings to add to (usually <c>session.Animations</c>).</param>
    /// <param name="id">The part's element id.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the id is unknown or the property does not take a float track.</exception>
    public AnimationBinding BindPart(DrawingAnimations animations, string id, AnimatedProperty property, KeyframeTrack<float> track)
    {
        if (animations == null) { throw new ArgumentNullException(nameof(animations)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        DrawingSvgPart part = GetPart(id);
        Action<float> apply = property switch
        {
            AnimatedProperty.Rotation => v => part.Rotation = Finite(v),
            AnimatedProperty.Scale => v =>
            {
                part.ScaleX = Finite(v, 1f);
                part.ScaleY = Finite(v, 1f);
            },
            AnimatedProperty.ScaleX => v => part.ScaleX = Finite(v, 1f),
            AnimatedProperty.ScaleY => v => part.ScaleY = Finite(v, 1f),
            AnimatedProperty.Opacity => v => part.Opacity = Unit(v),
            AnimatedProperty.TrimStart => v => part.TrimStart = Unit(v),
            AnimatedProperty.TrimEnd => v => part.TrimEnd = Unit(v),
            _ => throw new ArgumentException($"{property} cannot be driven by a float track on an SVG part.", nameof(property)),
        };
        return animations.Bind(track, apply);
    }

    /// <summary>
    /// Binds a point track to a part: <see cref="AnimatedProperty.Position"/> (its translation, document
    /// units), <see cref="AnimatedProperty.Anchor"/> (its pivot) or <see cref="AnimatedProperty.Scale"/>
    /// (X and Y).
    /// </summary>
    /// <param name="animations">The bindings to add to (usually <c>session.Animations</c>).</param>
    /// <param name="id">The part's element id.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the id is unknown or the property does not take a point track.</exception>
    public AnimationBinding BindPart(DrawingAnimations animations, string id, AnimatedProperty property, KeyframeTrack<PointF> track)
    {
        if (animations == null) { throw new ArgumentNullException(nameof(animations)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        DrawingSvgPart part = GetPart(id);
        Action<PointF> apply = property switch
        {
            AnimatedProperty.Position => v => part.Translation = new PointF(Finite(v.X), Finite(v.Y)),
            AnimatedProperty.Anchor => v => part.Pivot = new PointF(Finite(v.X), Finite(v.Y)),
            AnimatedProperty.Scale => v =>
            {
                part.ScaleX = Finite(v.X, 1f);
                part.ScaleY = Finite(v.Y, 1f);
            },
            _ => throw new ArgumentException($"{property} cannot be driven by a point track on an SVG part.", nameof(property)),
        };
        return animations.Bind(track, apply);
    }

    /// <summary>The box the document is drawn in, calibrated units: the fitted document box (for <see cref="DrawingSvgAspect.Slice"/>, the destination it is clipped to).</summary>
    /// <returns>The bounds.</returns>
    public override RectangleF? GetBounds()
    {
        if (Aspect == DrawingSvgAspect.Slice) { return Destination; }
        return ContentBounds;
    }

    /// <summary>The drawn box (<see cref="GetBounds"/>) as a rectangle path.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline()
    {
        RectangleF bounds = GetBounds().Value;
        return PathGeometry.Rectangle(bounds);
    }

    /// <summary>The area the picture covers: its whole drawn box (the document's own transparency is not looked at).</summary>
    /// <param name="tolerance">Not used.</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance) => new[] { GetOutline() };

    /// <inheritdoc />
    public override void Draw(DrawingCanvas canvas, DrawingColor color)
    {
        DrawingPicture picture = GetPicture();
        RectangleF doc = DocumentBounds;
        if (picture == null || doc.Width <= 0f || doc.Height <= 0f) { return; }
        Fit(out float sx, out float sy, out float dx, out float dy);
        canvas.Save();
        if (Aspect == DrawingSvgAspect.Slice)
        {
            canvas.ClipRect(new DrawingRect(Destination.Left, Destination.Top, Destination.Right, Destination.Bottom));
        }
        canvas.Translate(dx, dy);
        canvas.Scale(sx, sy);
        canvas.Translate(-doc.X, -doc.Y);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    internal void PartChanged()
    {
        Interlocked.Increment(ref _version);
        MarkChanged();
    }

    //The loaded picture when no part has an override; otherwise the scene re-recorded with them
    private DrawingPicture GetPicture()
    {
        lock (_locker)
        {
            int version = Volatile.Read(ref _version);
            int load = Svg.LoadVersion;
            if (_picture != null && _pictureVersion == version && _pictureLoad == load) { return _picture; }
            Dictionary<string, SvgPartState> states = SnapshotLocked();
            DrawingPicture picture;
            if (states.Count == 0)
            {
                picture = Svg.Picture;
            }
            else
            {
                lock (Svg.RenderLock)
                {
                    if (Svg.SceneDocument == null || Svg.Converter == null) { return Svg.Picture; }
                    var transient = new HashSet<SKPicture>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
                    SKPicture recorded = SvgPartRenderer.Record(Svg.SceneDocument, states, Svg.Model, transient);
                    picture = recorded == null ? null : Svg.Converter.ConvertTransient(recorded, transient);
                }
            }
            _picture = picture;
            _pictureVersion = version;
            _pictureLoad = load;
            return picture;
        }
    }

    private Dictionary<string, SvgPartState> Snapshot()
    {
        lock (_locker) { return SnapshotLocked(); }
    }

    private Dictionary<string, SvgPartState> SnapshotLocked()
    {
        var states = new Dictionary<string, SvgPartState>(StringComparer.Ordinal);
        foreach (DrawingSvgPart part in _partList)
        {
            if (part.HasOverrides) { states[part.Id] = part.Snapshot(); }
        }
        return states;
    }

    private List<SvgSceneNode> FindNodes(string id)
    {
        var nodes = new List<SvgSceneNode>();
        SvgSceneDocument scene = Svg.SceneDocument;
        if (scene == null) { return nodes; }
        foreach (SvgSceneNode node in scene.Traverse())
        {
            if (String.Equals(node.ElementId, id, StringComparison.Ordinal)) { nodes.Add(node); }
        }
        return nodes;
    }

    //Document units -> calibrated units: scale (sx, sy), then the document's top-left lands at (dx, dy)
    private void Fit(out float sx, out float sy, out float dx, out float dy)
    {
        RectangleF doc = DocumentBounds;
        RectangleF dest = Destination;
        if (doc.Width <= 0f || doc.Height <= 0f)
        {
            sx = sy = 1f;
            dx = dest.X;
            dy = dest.Y;
            return;
        }
        switch (Aspect)
        {
            case DrawingSvgAspect.Stretch:
                sx = dest.Width / doc.Width;
                sy = dest.Height / doc.Height;
                break;
            case DrawingSvgAspect.None:
                sx = sy = 1f;
                dx = dest.X;
                dy = dest.Y;
                return;
            case DrawingSvgAspect.Slice:
                sx = sy = Math.Max(dest.Width / doc.Width, dest.Height / doc.Height);
                break;
            default:
                sx = sy = Math.Min(dest.Width / doc.Width, dest.Height / doc.Height);
                break;
        }
        dx = dest.X + ((dest.Width - (doc.Width * sx)) / 2f);
        dy = dest.Y + ((dest.Height - (doc.Height * sy)) / 2f);
    }

    private static float Finite(float value, float fallback = 0f) => Single.IsFinite(value) ? value : fallback;

    private static float Unit(float value) => Single.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);

    private static SKRect Union(SKRect a, SKRect b)
        => new SKRect(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top), Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom));

    private static RectangleF UnionF(RectangleF a, RectangleF b)
    {
        float left = Math.Min(a.Left, b.Left), top = Math.Min(a.Top, b.Top);
        return new RectangleF(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
    }
}
