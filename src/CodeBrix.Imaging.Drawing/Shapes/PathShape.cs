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
/// A general path (a <see cref="PathGeometry"/>, or a <see cref="PathMorph"/> between two paths)
/// stroked, filled or both - with a flat colour or a gradient <see cref="DrawingShape.Fill"/>,
/// and any caps, joins and dash pattern (<see cref="PathStrokeStyle"/>).
/// <para>
/// Three properties may change while the shape is on a layer - they are what animation drives:
/// the TRIM range (<see cref="TrimStart"/>, <see cref="TrimEnd"/>, <see cref="TrimOffset"/>: draw
/// only part of the path, measured along its length - animate <see cref="TrimEnd"/> from 0 to 1
/// to draw the stroke on), and the <see cref="MorphFraction"/> of a morphing path. Bind float
/// tracks to them through <see cref="DrawingAnimations"/> (AnimatedProperty TrimStart, TrimEnd,
/// TrimOffset, MorphFraction). The trim applies to the fill too (each trimmed piece fills as if
/// closed), as in animation tools. Changing any of them makes the shape dynamic (its layer
/// redraws); a path that never changes is a static mark like every other shape. Every
/// measurement (trimming, dashing, morphing) is shared managed arithmetic, identical in both
/// packages; the drawing is each package's anti-aliased canvas.
/// </para>
/// </summary>
public sealed class PathShape : DrawingShape
{
    private readonly PathGeometry _geometry;
    private readonly SKColor? _fillColor;
    private float _trimStart;
    private float _trimEnd = 1f;
    private float _trimOffset;
    private float _morphFraction;
    private Drawn _drawn;

    /// <summary>
    /// Creates a path outlined with round caps and joins (or filled in the shape's colour), like
    /// the other shapes.
    /// </summary>
    /// <param name="geometry">The path, in calibrated drawing units.</param>
    /// <param name="strokeThickness">The outline thickness; positive for an outline. When filled it is not used, and 0 is accepted.</param>
    /// <param name="color">The colour; or <c>null</c> for the layer's.</param>
    /// <param name="isFilled"><c>true</c> to fill the path instead of outlining it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness is negative, or 0 for an outline.</exception>
    public PathShape(PathGeometry geometry, float strokeThickness = Stroke.DefaultWidth, Color? color = null, bool isFilled = false)
        : base(strokeThickness, color, null, isFilled)
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        StrokeStyle = isFilled ? null : new PathStrokeStyle(strokeThickness);
        IsFilled = isFilled;
    }

    /// <summary>
    /// Creates a path filled in a flat colour, with no outline - the plain way to draw a filled
    /// area (its <see cref="DrawingShape.StrokeThickness"/> is 0: there is no outline).
    /// </summary>
    /// <param name="geometry">The path, in calibrated drawing units.</param>
    /// <param name="fillColor">The fill colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    public PathShape(PathGeometry geometry, Color fillColor)
        : this(geometry, 0f, fillColor, isFilled: true)
    {
    }

    /// <summary>Creates a path filled by a gradient (or any fill), with no outline.</summary>
    /// <param name="geometry">The path, in calibrated drawing units.</param>
    /// <param name="fill">The fill.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public PathShape(PathGeometry geometry, ShapeFill fill)
        : base(Stroke.DefaultWidth, (Color?)null, fill ?? throw new ArgumentNullException(nameof(fill)))
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        IsFilled = true;
    }

    /// <summary>
    /// Creates a path with a styled outline and an optional fill underneath it.
    /// </summary>
    /// <param name="geometry">The path, in calibrated drawing units.</param>
    /// <param name="strokeStyle">The outline's width, caps, joins and dashes; or <c>null</c> for no outline.</param>
    /// <param name="strokeColor">The outline's colour; or <c>null</c> for the layer's.</param>
    /// <param name="fillColor">A flat fill colour; or <c>null</c> for none (unless <paramref name="fill"/> is given).</param>
    /// <param name="fill">A gradient (or any) fill; or <c>null</c>. It wins over <paramref name="fillColor"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there is neither an outline nor a fill.</exception>
    public PathShape(PathGeometry geometry, PathStrokeStyle strokeStyle, Color? strokeColor = null, Color? fillColor = null, ShapeFill fill = null)
        : base(strokeStyle?.Width ?? Stroke.DefaultWidth, strokeColor, fill)
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        if (strokeStyle == null && !fillColor.HasValue && fill == null)
        {
            throw new ArgumentException("A path shape needs an outline, a fill or both.", nameof(strokeStyle));
        }
        StrokeStyle = strokeStyle;
        _fillColor = GraphicsInterop.ToGraphics(fillColor);
        IsFilled = fillColor.HasValue || fill != null;
    }

    /// <summary>
    /// Creates a morphing path outlined with round caps and joins (or filled), showing
    /// <paramref name="morph"/> at <paramref name="fraction"/>.
    /// </summary>
    /// <param name="morph">The morph between two paths.</param>
    /// <param name="fraction">The starting <see cref="MorphFraction"/>, 0..1.</param>
    /// <param name="strokeThickness">The outline thickness; positive for an outline. When filled it is not used, and 0 is accepted.</param>
    /// <param name="color">The colour; or <c>null</c> for the layer's.</param>
    /// <param name="isFilled"><c>true</c> to fill instead of outlining.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="morph"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the fraction is outside 0..1, or the thickness is negative (or 0 for an outline).</exception>
    public PathShape(PathMorph morph, float fraction, float strokeThickness = Stroke.DefaultWidth, Color? color = null, bool isFilled = false)
        : this(morph?.From ?? throw new ArgumentNullException(nameof(morph)), strokeThickness, color, isFilled)
    {
        Morph = morph;
        _morphFraction = CheckFraction(fraction, nameof(fraction));
    }

    /// <summary>Creates a morphing path with a styled outline and an optional fill.</summary>
    /// <param name="morph">The morph between two paths.</param>
    /// <param name="fraction">The starting <see cref="MorphFraction"/>, 0..1.</param>
    /// <param name="strokeStyle">The outline's style; or <c>null</c> for no outline.</param>
    /// <param name="strokeColor">The outline's colour; or <c>null</c> for the layer's.</param>
    /// <param name="fillColor">A flat fill colour; or <c>null</c> for none.</param>
    /// <param name="fill">A gradient (or any) fill; or <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="morph"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there is neither an outline nor a fill.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the fraction is outside 0..1.</exception>
    public PathShape(PathMorph morph, float fraction, PathStrokeStyle strokeStyle, Color? strokeColor = null, Color? fillColor = null, ShapeFill fill = null)
        : this(morph?.From ?? throw new ArgumentNullException(nameof(morph)), strokeStyle, strokeColor, fillColor, fill)
    {
        Morph = morph;
        _morphFraction = CheckFraction(fraction, nameof(fraction));
    }

    /// <summary>The morph this shape shows; <c>null</c> for a fixed path.</summary>
    public PathMorph Morph { get; }

    /// <summary>
    /// The path as it currently stands, before trimming: the fixed path, or the morph at
    /// <see cref="MorphFraction"/>.
    /// </summary>
    public PathGeometry Geometry => GetDrawn().Base;

    /// <summary>The outline's style; <c>null</c> when the path is not outlined.</summary>
    public PathStrokeStyle StrokeStyle { get; }

    /// <summary>Indicates whether the path is outlined.</summary>
    public bool IsStroked => StrokeStyle != null;

    /// <summary>Indicates whether the path is filled.</summary>
    public bool IsFilled { get; }

    /// <summary>
    /// The flat fill colour given with a styled outline; <c>null</c> otherwise (a shape filled by
    /// the simple constructor fills in its <see cref="DrawingShape.Color"/>, a gradient fill in its
    /// <see cref="DrawingShape.Fill"/>).
    /// </summary>
    public Color? FillColor => GraphicsInterop.ToImaging(_fillColor);

    /// <summary>Where the drawn part starts, as a fraction 0..1 of the path's length (0 by default).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float TrimStart
    {
        get => _trimStart;
        set
        {
            float v = CheckFraction(value, nameof(value));
            if (v == _trimStart) { return; }
            _trimStart = v;
            Changed();
        }
    }

    /// <summary>Where the drawn part ends, as a fraction 0..1 of the path's length (1 by default).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float TrimEnd
    {
        get => _trimEnd;
        set
        {
            float v = CheckFraction(value, nameof(value));
            if (v == _trimEnd) { return; }
            _trimEnd = v;
            Changed();
        }
    }

    /// <summary>
    /// A shift of the drawn part along the path, as a fraction of its length (any value; 1 = once
    /// round, wrapping past the end) - animate it to run a fixed-length part round a loop.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or infinity.</exception>
    public float TrimOffset
    {
        get => _trimOffset;
        set
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _trimOffset) { return; }
            _trimOffset = value;
            Changed();
        }
    }

    /// <summary>Indicates whether only part of the path is drawn (the trim is not 0..1).</summary>
    public bool IsTrimmed => _trimStart != 0f || _trimEnd != 1f;

    /// <summary>How far the morph has gone, 0..1 (0 for a fixed path).</summary>
    /// <exception cref="InvalidOperationException">Thrown when set on a shape without a <see cref="Morph"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float MorphFraction
    {
        get => _morphFraction;
        set
        {
            if (Morph == null) { throw new InvalidOperationException("Only a shape made from a PathMorph has a morph fraction."); }
            float v = CheckFraction(value, nameof(value));
            if (v == _morphFraction) { return; }
            _morphFraction = v;
            Changed();
        }
    }

    /// <summary>The path exactly as drawn: <see cref="Geometry"/> after the trim (dashes are not split out).</summary>
    /// <returns>The geometry.</returns>
    public PathGeometry GetDrawnGeometry() => GetDrawn().Trimmed;

    /// <summary>The drawn path (see <see cref="GetDrawnGeometry"/>) - the shape's outline as geometry.</summary>
    /// <returns>The geometry.</returns>
    public override PathGeometry GetOutline() => GetDrawn().Trimmed;

    /// <summary>
    /// The area the shape's OUTLINE (stroke) covers as it stands - the path after the trim and
    /// the morph, the dashes cut, stroked in <see cref="StrokeStyle"/> - as a fillable polygon
    /// geometry (see <see cref="PathGeometry.ToStrokeOutline(PathStrokeStyle, float)"/>). The fill
    /// is not included; unite it yourself with <c>GetDrawnGeometry()</c> when wanted.
    /// </summary>
    /// <param name="tolerance">How closely curves and arcs are followed, calibrated units.</param>
    /// <returns>The stroke's area; <c>null</c> when the shape is not outlined.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    public PathGeometry GetStrokeOutline(float tolerance = PathGeometry.DefaultFlattenTolerance)
        => StrokeStyle == null ? null : GetDrawn().Trimmed.ToStrokeOutline(StrokeStyle, tolerance);

    /// <summary>
    /// The box around the drawn path (its exact curve extent), grown by the outline's reach: half
    /// the width, times the miter limit for miter joins and the square root of 2 for square caps.
    /// An empty drawn path gives an empty rectangle at its start.
    /// </summary>
    /// <returns>The bounds, calibrated units.</returns>
    public override RectangleF? GetBounds()
    {
        PathGeometry drawn = GetDrawn().Trimmed;
        if (drawn.IsEmpty)
        {
            PointF start = GetDrawn().Base.GetPointAt(_trimStart).Position;
            return new RectangleF(start.X, start.Y, 0f, 0f);
        }
        RectangleF bounds = drawn.GetBounds();
        return StrokeStyle == null ? bounds : Inflate(bounds, StrokeStyle.Width / 2f * StrokeStyle.ReachFactor);
    }

    /// <summary>
    /// The area the path covers as drawn: the trimmed path under its fill rule when filled, and
    /// the band its outline covers (<see cref="GetStrokeOutline(float)"/>) when outlined.
    /// </summary>
    /// <param name="tolerance">How closely the outline's band is followed, calibrated units.</param>
    /// <returns>One or two geometries; none when the trimmed path is empty.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
    {
        var areas = new List<PathGeometry>(2);
        PathGeometry drawn = GetDrawn().Trimmed;
        if (drawn.IsEmpty) { return areas; }
        if (IsFilled) { areas.Add(drawn); }
        if (StrokeStyle != null) { areas.Add(GetStrokeOutline(tolerance)); }
        return areas;
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        Drawn drawn = GetDrawn();
        if (drawn.Trimmed.IsEmpty) { return; }
        if (IsFilled)
        {
            using SKPath fillPath = drawn.Trimmed.ToBackendPath();
            using SKPaint fillPaint = CreateFillPaint(_fillColor ?? color, Fill);
            canvas.DrawPath(fillPath, fillPaint);
        }
        if (StrokeStyle != null)
        {
            PathGeometry outline = drawn.Dashed;
            if (outline.IsEmpty) { return; }
            using SKPath strokePath = outline.ToBackendPath();
            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = color,
                StrokeWidth = StrokeStyle.Width,
                StrokeCap = StrokeStyle.Cap switch
                {
                    PathStrokeCap.Butt => SKStrokeCap.Butt,
                    PathStrokeCap.Square => SKStrokeCap.Square,
                    _ => SKStrokeCap.Round,
                },
                StrokeJoin = StrokeStyle.Join switch
                {
                    PathStrokeJoin.Miter => SKStrokeJoin.Miter,
                    PathStrokeJoin.Bevel => SKStrokeJoin.Bevel,
                    _ => SKStrokeJoin.Round,
                },
                StrokeMiter = StrokeStyle.MiterLimit,
                IsAntialias = true,
            };
            canvas.DrawPath(strokePath, strokePaint);
        }
    }

    private void Changed()
    {
        _drawn = null;
        MarkChanged();
    }

    //The geometry for the current trim and morph, computed once per change
    private Drawn GetDrawn()
    {
        Drawn drawn = _drawn;
        if (drawn != null) { return drawn; }
        PathGeometry geometry = Morph != null ? Morph.GetPath(_morphFraction) : _geometry;
        PathGeometry trimmed = geometry.Trim(_trimStart, _trimEnd, _trimOffset);
        PathGeometry dashed = StrokeStyle != null && StrokeStyle.IsDashed
            ? trimmed.Dash(StrokeStyle.DashPatternValue, StrokeStyle.DashOffset)
            : trimmed;
        drawn = new Drawn(geometry, trimmed, dashed);
        _drawn = drawn;
        return drawn;
    }

    private static float CheckFraction(float value, string name)
    {
        if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(name); }
        return value;
    }

    private sealed class Drawn
    {
        public Drawn(PathGeometry geometry, PathGeometry trimmed, PathGeometry dashed)
        {
            Base = geometry;
            Trimmed = trimmed;
            Dashed = dashed;
        }

        public PathGeometry Base { get; }

        public PathGeometry Trimmed { get; }

        public PathGeometry Dashed { get; }
    }
}
