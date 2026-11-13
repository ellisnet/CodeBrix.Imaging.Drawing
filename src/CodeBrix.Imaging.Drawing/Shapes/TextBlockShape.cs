using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.Text;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A laid-out block of text (a <see cref="TextLayoutResult"/> - several lines, wrapped, aligned,
/// trimmed) drawn with its box's top-left corner at (<see cref="X"/>, <see cref="Y"/>): filled in a
/// flat color or a gradient <see cref="DrawingShape.Fill"/>, outlined with a
/// <see cref="PathStrokeStyle"/>, or both. Runs that carry their own color or fill paint in it.
/// <para>
/// REVEAL - the typewriter effect: <see cref="RevealFraction"/> (0..1) or <see cref="RevealCount"/>
/// shows only the first characters, words or lines (<see cref="RevealUnit"/>), each unit appearing
/// whole, or fading in over <see cref="RevealSoftness"/> units. Bind float tracks to them through
/// <see cref="DrawingAnimations"/> (AnimatedProperty RevealFraction, RevealCount). Changing them
/// makes the shape dynamic (its layer redraws); a block that never changes is a static mark.
/// </para>
/// <para>
/// PIECES: <see cref="CreatePieces(TextUnit)"/> splits the block into one shape per character,
/// word or line - same layout, same style, same position - so each can be moved, faded, scaled or
/// turned on its own (see <see cref="DrawingAnimations.BindStagger"/>). For draw-on (trim), dashes
/// or text along a path, take the glyph outlines (<see cref="TextLayoutResult.GetOutline(float, float)"/>)
/// into a <see cref="PathShape"/>.
/// </para>
/// </summary>
public sealed class TextBlockShape : DrawingShape
{
    private readonly SKColor? _fillColor;
    private readonly int _first;
    private readonly int _count;
    private TextUnit _revealUnit = TextUnit.Character;
    private float _reveal = Single.PositiveInfinity;
    private float _revealSoftness;
    private TextStrokePlacement _strokePlacement;
    private bool _mergeOverlaps;
    private Dictionary<int, PathGeometry> _merged;

    /// <summary>
    /// Creates a block filled in a flat color.
    /// </summary>
    /// <param name="layout">The laid-out text.</param>
    /// <param name="x">Where the box's left edge goes, calibrated units.</param>
    /// <param name="y">Where the box's top edge goes, calibrated units.</param>
    /// <param name="color">The text's color; or <c>null</c> for the layer's.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> is null.</exception>
    public TextBlockShape(TextLayoutResult layout, float x, float y, Color? color = null)
        : this(layout, x, y, null, color, null, null, true, 0, -1)
    {
    }

    /// <summary>
    /// Creates a block painted by a fill - for example a gradient across the whole block (fill
    /// geometry is in calibrated units, like the shape's position).
    /// </summary>
    /// <param name="layout">The laid-out text.</param>
    /// <param name="x">Where the box's left edge goes, calibrated units.</param>
    /// <param name="y">Where the box's top edge goes, calibrated units.</param>
    /// <param name="fill">The fill.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public TextBlockShape(TextLayoutResult layout, float x, float y, ShapeFill fill)
        : this(layout, x, y, null, null, null, fill ?? throw new ArgumentNullException(nameof(fill)), true, 0, -1)
    {
    }

    /// <summary>
    /// Creates outlined text: glyph outlines stroked in a style, with an optional fill underneath.
    /// </summary>
    /// <param name="layout">The laid-out text.</param>
    /// <param name="x">Where the box's left edge goes, calibrated units.</param>
    /// <param name="y">Where the box's top edge goes, calibrated units.</param>
    /// <param name="strokeStyle">The outline's width, caps, joins and dashes; or <c>null</c> for no outline.</param>
    /// <param name="strokeColor">The outline's color; or <c>null</c> for the layer's.</param>
    /// <param name="fillColor">A flat fill color; or <c>null</c> for none (unless <paramref name="fill"/> is given).</param>
    /// <param name="fill">A gradient (or any) fill; or <c>null</c>. It wins over <paramref name="fillColor"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there is neither an outline nor a fill.</exception>
    public TextBlockShape(TextLayoutResult layout, float x, float y, PathStrokeStyle strokeStyle, Color? strokeColor = null,
        Color? fillColor = null, ShapeFill fill = null)
        : this(layout, x, y, strokeStyle, strokeColor, GraphicsInterop.ToGraphics(fillColor), fill,
            fillColor.HasValue || fill != null, 0, -1)
    {
        if (strokeStyle == null && !IsFilled)
        {
            throw new ArgumentException("A text block needs an outline, a fill or both.", nameof(strokeStyle));
        }
    }

    private TextBlockShape(TextLayoutResult layout, float x, float y, PathStrokeStyle strokeStyle, Color? color,
        SKColor? fillColor, ShapeFill fill, bool isFilled, int first, int count)
        : base(strokeStyle?.Width ?? 1f, color, fill)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (!Single.IsFinite(x)) { throw new ArgumentOutOfRangeException(nameof(x)); }
        if (!Single.IsFinite(y)) { throw new ArgumentOutOfRangeException(nameof(y)); }
        X = x;
        Y = y;
        StrokeStyle = strokeStyle;
        _fillColor = fillColor;
        IsFilled = isFilled;
        _first = Math.Clamp(first, 0, layout.ClusterCount);
        _count = count < 0 ? layout.ClusterCount - _first : Math.Min(count, layout.ClusterCount - _first);
    }

    /// <summary>The laid-out text the block draws.</summary>
    public TextLayoutResult Layout { get; }

    /// <summary>Where the box's left edge is, calibrated units.</summary>
    public float X { get; }

    /// <summary>Where the box's top edge is, calibrated units.</summary>
    public float Y { get; }

    /// <summary>The block's box in calibrated units: (<see cref="X"/>, <see cref="Y"/>) and the layout's <see cref="TextLayoutResult.Size"/>.</summary>
    public RectangleF BoxBounds => new RectangleF(X, Y, Layout.Width, Layout.Height);

    /// <summary>The outline's style; <c>null</c> when the glyphs are not outlined.</summary>
    public PathStrokeStyle StrokeStyle { get; }

    /// <summary>Indicates whether the glyphs are outlined.</summary>
    public bool IsStroked => StrokeStyle != null;

    /// <summary>Indicates whether the glyphs are filled.</summary>
    public bool IsFilled { get; }

    /// <summary>
    /// Where the outline is drawn: centred on the glyph outlines and over the fill (the default), or
    /// only outside the glyphs, under the fill - the clean outline for fonts whose glyphs overlap
    /// contours (see <see cref="TextStrokePlacement"/>). No effect without an outline.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to an undefined value.</exception>
    public TextStrokePlacement StrokePlacement
    {
        get => _strokePlacement;
        set
        {
            if (!Enum.IsDefined(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _strokePlacement) { return; }
            _strokePlacement = value;
            MarkChanged();
        }
    }

    /// <summary>
    /// The flattening tolerance, calibrated units, of the merged glyph outlines that
    /// <see cref="MergeOverlaps"/> draws.
    /// </summary>
    public const float MergeTolerance = 0.1f;

    /// <summary>
    /// When <c>true</c>, each glyph's overlapping contours are MERGED into one clean outline
    /// before it is filled and stroked (<see cref="PathGeometry.Simplify(float)"/> per glyph,
    /// flattened within <see cref="MergeTolerance"/>), so a centred outline or dashes no longer
    /// trace the lines where a font's strokes overlap inside a glyph (fonts such as Roboto build
    /// glyphs from overlapping contours). <c>false</c> (the default) draws the font's curves as
    /// they are. Separate glyphs are merged one by one, not with each other. Works with either
    /// <see cref="StrokePlacement"/>; <see cref="GetOutline"/> returns the merged outlines too.
    /// </summary>
    public bool MergeOverlaps
    {
        get => _mergeOverlaps;
        set
        {
            if (value == _mergeOverlaps) { return; }
            _mergeOverlaps = value;
            MarkChanged();
        }
    }

    /// <summary>The flat fill color given with an outline; <c>null</c> otherwise.</summary>
    public Color? FillColor => GraphicsInterop.ToImaging(_fillColor);

    /// <summary>The first of the layout's clusters this shape draws (0 unless it is a piece).</summary>
    public int FirstCluster => _first;

    /// <summary>How many of the layout's clusters this shape draws (all of them unless it is a piece).</summary>
    public int ClusterCount => _count;

    /// <summary>What <see cref="RevealFraction"/> and <see cref="RevealCount"/> count: characters (the default), words or lines.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to an undefined value.</exception>
    public TextUnit RevealUnit
    {
        get => _revealUnit;
        set
        {
            if (!Enum.IsDefined(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _revealUnit) { return; }
            _revealUnit = value;
            MarkChanged();
        }
    }

    /// <summary>
    /// How much of the block shows, as a fraction 0..1 of its units (1, the default, shows
    /// everything; 0 nothing): <see cref="RevealCount"/> = fraction x <see cref="GetUnitCount()"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float RevealFraction
    {
        get
        {
            int units = GetUnitCount();
            if (Single.IsPositiveInfinity(_reveal) || units == 0) { return 1f; }
            return Math.Min(1f, _reveal / units);
        }
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            SetReveal(value == 1f ? Single.PositiveInfinity : value * GetUnitCount());
        }
    }

    /// <summary>
    /// How many units show (characters, words or lines by <see cref="RevealUnit"/>); everything by
    /// default. A fraction shows the next unit partly when <see cref="RevealSoftness"/> is above 0;
    /// otherwise a unit appears when the count reaches it (count 3 shows units 0, 1 and 2). Spaces
    /// count as characters.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 0 or to NaN.</exception>
    public float RevealCount
    {
        get => Single.IsPositiveInfinity(_reveal) ? GetUnitCount() : _reveal;
        set
        {
            if (Single.IsNaN(value) || value < 0f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            SetReveal(value);
        }
    }

    /// <summary>
    /// Over how many units a unit fades in as the reveal passes it (0, the default, makes each unit
    /// appear at once - a typewriter; 3 lets the last three units shown fade up in a ramp).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 0, to NaN or infinity.</exception>
    public float RevealSoftness
    {
        get => _revealSoftness;
        set
        {
            if (!Single.IsFinite(value) || value < 0f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _revealSoftness) { return; }
            _revealSoftness = value;
            MarkChanged();
        }
    }

    /// <summary>Indicates whether every unit shows fully.</summary>
    public bool IsFullyRevealed => Single.IsPositiveInfinity(_reveal) || _reveal >= GetUnitCount();

    /// <summary>How many units of the current <see cref="RevealUnit"/> this shape draws.</summary>
    /// <returns>The count.</returns>
    public int GetUnitCount() => GetUnitCount(_revealUnit);

    /// <summary>How many characters, words or lines this shape draws.</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The count.</returns>
    public int GetUnitCount(TextUnit unit)
    {
        if (unit == TextUnit.Character || _count == 0) { return _count; }
        int units = 0;
        int previous = Int32.MinValue;
        IReadOnlyList<TextCluster> clusters = Layout.Clusters;
        for (int i = _first; i < _first + _count; i++)
        {
            int key = unit == TextUnit.Line ? clusters[i].LineIndex : clusters[i].WordIndex;
            if (key < 0 || key == previous) { continue; }
            units++;
            previous = key;
        }
        return units;
    }

    /// <summary>
    /// Splits the block into one shape per character, word or line (in reading order, as
    /// <see cref="TextLayoutResult.GetPieces(TextUnit)"/> lists them), each drawing only its own
    /// clusters with this shape's position, color, fill and outline - so together they draw exactly
    /// what this shape draws, and each can be animated on its own. Character pieces include spaces
    /// (which draw nothing). The pieces start fully revealed.
    /// </summary>
    /// <param name="unit">The kind of piece.</param>
    /// <returns>New shapes, one per piece.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unit"/> is not defined.</exception>
    public IReadOnlyList<TextBlockShape> CreatePieces(TextUnit unit)
    {
        var shapes = new List<TextBlockShape>();
        foreach (TextPiece piece in Layout.GetPieces(unit))
        {
            int first = Math.Max(piece.FirstCluster, _first);
            int end = Math.Min(piece.FirstCluster + piece.ClusterCount, _first + _count);
            if (end < first || (piece.ClusterCount > 0 && end == first)) { continue; }
            shapes.Add(new TextBlockShape(Layout, X, Y, StrokeStyle, Color, _fillColor, Fill, IsFilled, first, end - first)
            {
                _strokePlacement = _strokePlacement,
                _mergeOverlaps = _mergeOverlaps,
            });
        }
        return shapes;
    }

    /// <summary>
    /// The same pieces as <see cref="CreatePieces(TextUnit)"/>, gathered in a new
    /// <see cref="DrawingGroup"/> (its children, in order) - one element to add, whose children can
    /// each be animated (and whose own transform moves them all).
    /// </summary>
    /// <param name="unit">The kind of piece.</param>
    /// <returns>The group.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unit"/> is not defined.</exception>
    public DrawingGroup CreatePieceGroup(TextUnit unit)
    {
        var group = new DrawingGroup();
        foreach (TextBlockShape piece in CreatePieces(unit)) { group.Add(piece); }
        return group;
    }

    /// <summary>
    /// The glyph outlines that currently show (any part of the reveal), at the shape's position -
    /// closed contours with the non-zero fill rule (merged per glyph, as polygons, when
    /// <see cref="MergeOverlaps"/> is set); <see cref="PathGeometry.Empty"/> when nothing shows.
    /// </summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline()
    {
        var builder = new PathGeometryBuilder();
        bool any = false;
        IReadOnlyList<TextCluster> clusters = Layout.Clusters;
        UnitCounter counter = NewCounter();
        for (int i = _first; i < _first + _count; i++)
        {
            TextCluster cluster = clusters[i];
            float alpha = AlphaOf(counter.Next(cluster));
            if (alpha <= 0f || !cluster.HasInk) { continue; }
            if (_mergeOverlaps) { builder.AddPath(GetMerged(cluster)); }
            else { TextOutline.AppendTo(builder, cluster.Verbs, cluster.Points, X, Y); }
            any = true;
        }
        return any ? builder.Build() : PathGeometry.Empty;
    }

    /// <summary>
    /// The box around the glyphs that currently show (curve control points included), grown by the
    /// outline's reach when outlined; an empty rectangle at (<see cref="X"/>, <see cref="Y"/>) when
    /// nothing shows.
    /// </summary>
    /// <returns>The bounds, calibrated units.</returns>
    public override RectangleF? GetBounds()
    {
        RectangleF? ink = null;
        IReadOnlyList<TextCluster> clusters = Layout.Clusters;
        UnitCounter counter = NewCounter();
        for (int i = _first; i < _first + _count; i++)
        {
            TextCluster cluster = clusters[i];
            float alpha = AlphaOf(counter.Next(cluster));
            if (alpha <= 0f || !cluster.InkBounds.HasValue) { continue; }
            ink = TextLayoutResult.Union(ink, cluster.InkBounds);
        }
        if (!ink.HasValue) { return new RectangleF(X, Y, 0f, 0f); }
        var box = new RectangleF(ink.Value.X + X, ink.Value.Y + Y, ink.Value.Width, ink.Value.Height);
        return StrokeStyle == null ? box : Inflate(box, StrokeStyle.Width / 2f * StrokeStyle.ReachFactor);
    }

    /// <summary>
    /// The area the shown glyphs cover: their outlines when filled, and the band their outline
    /// covers when outlined (stroke style, dashes included) - any part of the reveal counts.
    /// </summary>
    /// <param name="tolerance">How closely an outline's band is followed, calibrated units.</param>
    /// <returns>One or two geometries; none when nothing shows.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
    {
        PathGeometry glyphs = GetOutline();
        var areas = new List<PathGeometry>(2);
        if (glyphs.IsEmpty) { return areas; }
        if (IsFilled) { areas.Add(glyphs); }
        if (StrokeStyle != null) { areas.Add(glyphs.ToStrokeOutline(StrokeStyle, tolerance)); }
        return areas;
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        IReadOnlyList<TextCluster> clusters = Layout.Clusters;
        IReadOnlyList<TextRunDescriptor> runs = Layout.Runs;
        UnitCounter counter = NewCounter();

        //Fully shown glyphs are drawn together, one batch per run that paints differently; partly
        //  shown ones each fade in a layer of their own
        var batches = new List<(int Key, List<TextCluster> Clusters)>();
        for (int i = _first; i < _first + _count; i++)
        {
            TextCluster cluster = clusters[i];
            float alpha = AlphaOf(counter.Next(cluster));
            if (alpha <= 0f || !cluster.HasInk) { continue; }
            int key = PaintKey(runs[cluster.RunIndex], cluster.RunIndex);
            if (alpha >= 1f)
            {
                List<TextCluster> batch = null;
                foreach ((int k, List<TextCluster> list) in batches)
                {
                    if (k == key) { batch = list; break; }
                }
                if (batch == null)
                {
                    batch = new List<TextCluster>();
                    batches.Add((key, batch));
                }
                batch.Add(cluster);
            }
            else
            {
                using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha(DrawingRenderer.ScaleAlpha(255, alpha)) };
                canvas.SaveLayer(layerPaint);
                Paint(canvas, new List<TextCluster> { cluster }, key < 0 ? null : runs[key], color);
                canvas.Restore();
            }
        }

        foreach ((int key, List<TextCluster> batch) in batches)
        {
            Paint(canvas, batch, key < 0 ? null : runs[key], color);
        }
    }

    private void SetReveal(float value)
    {
        if (value == _reveal) { return; }
        _reveal = value;
        MarkChanged();
    }

    //A unit is fully shown once the count reaches its end; with softness it fades in over the
    //  `softness` units before that
    private float AlphaOf(int unit)
    {
        if (Single.IsPositiveInfinity(_reveal)) { return 1f; }
        if (unit < 0) { return 0f; }
        float end = unit + 1f;
        if (_revealSoftness <= 0f) { return end <= _reveal ? 1f : 0f; }
        return Math.Clamp(((_reveal - end) / _revealSoftness) + 1f, 0f, 1f);
    }

    private UnitCounter NewCounter() => new UnitCounter(_revealUnit, _first);

    //Runs that paint like the shape share one batch (-1); a run with its own color or fill gets its own
    private static int PaintKey(TextRunDescriptor run, int index) => run.Fill != null || run.Color.HasValue ? index : -1;

    private void Paint(SKCanvas canvas, List<TextCluster> glyphs, TextRunDescriptor run, SKColor color)
    {
        using SKPath path = BuildPath(glyphs);

        SKColor runColor = run != null && run.Color.HasValue ? GraphicsInterop.ToGraphics(run.Color.Value) : color;
        bool outside = StrokeStyle != null && _strokePlacement == TextStrokePlacement.Outside;
        if (outside)
        {
            //The stroke in a layer of its own, its inside half cut away by the glyphs' shape (DstOut)
            canvas.SaveLayer(null);
            DrawStroke(canvas, glyphs, path, run, runColor, color);
            using var cut = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Black, IsAntialias = true, BlendMode = SKBlendMode.DstOut };
            canvas.DrawPath(path, cut);
            canvas.Restore();
        }
        if (IsFilled)
        {
            using SKPaint fillPaint = run != null
                ? CreateFillPaint(runColor, run.Fill)
                : CreateFillPaint(_fillColor ?? color, Fill);
            canvas.DrawPath(path, fillPaint);
        }
        if (StrokeStyle != null && !outside)
        {
            DrawStroke(canvas, glyphs, path, run, runColor, color);
        }
    }

    private void DrawStroke(SKCanvas canvas, List<TextCluster> glyphs, SKPath path, TextRunDescriptor run, SKColor runColor, SKColor color)
    {
        using SKPaint strokePaint = CreateStrokePaint(run != null && !IsFilled ? runColor : color);
        if (StrokeStyle.IsDashed)
        {
            var geometry = new PathGeometryBuilder();
            foreach (TextCluster cluster in glyphs)
            {
                if (_mergeOverlaps) { geometry.AddPath(GetMerged(cluster)); }
                else { TextOutline.AppendTo(geometry, cluster.Verbs, cluster.Points, X, Y); }
            }
            PathGeometry dashed = geometry.Build().Dash(StrokeStyle.DashPatternValue, StrokeStyle.DashOffset);
            if (dashed.IsEmpty) { return; }
            using SKPath dashedPath = dashed.ToBackendPath();
            canvas.DrawPath(dashedPath, strokePaint);
        }
        else
        {
            canvas.DrawPath(path, strokePaint);
        }
    }

    private SKPaint CreateStrokePaint(SKColor color) => new SKPaint
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

    //The glyphs' outlines as one backend path: the font's curves, or the merged polygons
    private SKPath BuildPath(List<TextCluster> glyphs)
    {
        if (_mergeOverlaps)
        {
            var geometry = new PathGeometryBuilder();
            foreach (TextCluster cluster in glyphs) { geometry.AddPath(GetMerged(cluster)); }
            return geometry.Build().ToBackendPath();
        }
        var pathBuilder = new SKPathBuilder();
        foreach (TextCluster cluster in glyphs) { AppendTo(pathBuilder, cluster); }
        return pathBuilder.Detach();
    }

    //One glyph's contours merged into a clean outline at the shape's position (kept per glyph)
    private PathGeometry GetMerged(TextCluster cluster)
    {
        Dictionary<int, PathGeometry> merged = _merged;
        if (merged == null)
        {
            merged = new Dictionary<int, PathGeometry>();
            _merged = merged;
        }
        lock (merged)
        {
            if (merged.TryGetValue(cluster.Index, out PathGeometry geometry)) { return geometry; }
            var builder = new PathGeometryBuilder();
            TextOutline.AppendTo(builder, cluster.Verbs, cluster.Points, X, Y);
            geometry = builder.Build().Simplify(MergeTolerance);
            merged.Add(cluster.Index, geometry);
            return geometry;
        }
    }

    private void AppendTo(SKPathBuilder builder, TextCluster cluster)
    {
        float dx = X;
        float dy = Y;
        float[] p = cluster.Points;
        int index = 0;
        foreach (byte verb in cluster.Verbs)
        {
            switch (verb)
            {
                case TextOutline.VerbMove:
                    builder.MoveTo(new SKPoint(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case TextOutline.VerbLine:
                    builder.LineTo(new SKPoint(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case TextOutline.VerbQuad:
                    builder.QuadTo(new SKPoint(p[index] + dx, p[index + 1] + dy), new SKPoint(p[index + 2] + dx, p[index + 3] + dy));
                    index += 4;
                    break;
                case TextOutline.VerbCubic:
                    builder.CubicTo(new SKPoint(p[index] + dx, p[index + 1] + dy), new SKPoint(p[index + 2] + dx, p[index + 3] + dy),
                        new SKPoint(p[index + 4] + dx, p[index + 5] + dy));
                    index += 6;
                    break;
                default:
                    builder.Close();
                    break;
            }
        }
    }

    //Numbers the units of the clusters in a range as they are walked in order: characters by
    //  position, words and lines by how many distinct ones came before (a space takes the number
    //  of the word after it, which it is drawn - not at all - with)
    private struct UnitCounter
    {
        private readonly TextUnit _unit;
        private readonly int _first;
        private int _units;
        private int _previous;

        public UnitCounter(TextUnit unit, int first)
        {
            _unit = unit;
            _first = first;
            _units = -1;
            _previous = Int32.MinValue;
        }

        public int Next(TextCluster cluster)
        {
            if (_unit == TextUnit.Character) { return cluster.Index - _first; }
            int key = _unit == TextUnit.Line ? cluster.LineIndex : cluster.WordIndex;
            if (key < 0) { return Math.Max(0, _units + 1); }
            if (key != _previous)
            {
                _units++;
                _previous = key;
            }
            return _units;
        }
    }
}
