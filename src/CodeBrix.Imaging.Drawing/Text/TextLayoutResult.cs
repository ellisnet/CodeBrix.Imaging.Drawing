using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using CodeBrix.Imaging.Drawing.Paths;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// A block of text laid out by <see cref="TextLayoutEngine"/>: its measured size, every line's
/// metrics, every character's (cluster's) position, box and glyph outline, and the same grouped
/// into words and lines for per-piece animation. Immutable and thread-safe; the geometry was
/// computed entirely in shared managed code (CodeBrix.Imaging's font support plus this library's
/// line breaking), so the two packages produce exactly the same values. Draw it with a
/// <see cref="CodeBrix.Imaging.Drawing.Shapes.TextBlockShape"/>.
/// <para>
/// Coordinates are the layout's own: calibrated drawing units with (0, 0) at the top-left of the
/// block's box (<see cref="Size"/>) and y down; a shape places that box where it draws it.
/// </para>
/// </summary>
public sealed class TextLayoutResult
{
    private readonly TextLineMetrics[] _lines;
    private readonly TextCluster[] _clusters;
    private readonly TextRunDescriptor[] _runs;
    private readonly TextLayoutOptions _options;
    private readonly TextPiece[][] _pieces = new TextPiece[3][];
    private PathGeometry _outline;

    internal TextLayoutResult(string text, TextRunDescriptor[] runs, TextLayoutOptions options, SizeF size,
        TextLineMetrics[] lines, TextCluster[] clusters, int wordCount, bool isTruncated, int shownTextEnd)
    {
        Text = text;
        _runs = runs;
        _options = options;
        Size = size;
        _lines = lines;
        _clusters = clusters;
        WordCount = wordCount;
        IsTruncated = isTruncated;
        ShownTextLength = shownTextEnd;

        float left = Single.PositiveInfinity, top = Single.PositiveInfinity, right = Single.NegativeInfinity, bottom = Single.NegativeInfinity;
        foreach (TextLineMetrics line in lines)
        {
            left = Math.Min(left, line.Left);
            right = Math.Max(right, line.Left + line.Width);
            top = Math.Min(top, line.Top);
            bottom = Math.Max(bottom, line.Top + line.Height);
        }
        ContentBounds = lines.Length == 0 ? RectangleF.Empty : new RectangleF(left, top, right - left, bottom - top);

        RectangleF? ink = null;
        foreach (TextCluster cluster in clusters) { ink = Union(ink, cluster.InkBounds); }
        InkBounds = ink;
    }

    /// <summary>The laid-out text: every run's text, concatenated.</summary>
    public string Text { get; }

    /// <summary>The runs the text came from, in order.</summary>
    public IReadOnlyList<TextRunDescriptor> Runs => _runs;

    /// <summary>A copy of the options the block was laid out with.</summary>
    public TextLayoutOptions Options => _options.Clone();

    /// <summary>
    /// The block's box: <see cref="TextLayoutOptions.MaxWidth"/> wide when set (otherwise as wide
    /// as the widest line), <see cref="TextLayoutOptions.MaxHeight"/> tall when set (otherwise as
    /// tall as the lines). This is the box the lines are aligned within.
    /// </summary>
    public SizeF Size { get; }

    /// <summary>The box's width (<see cref="Size"/>).</summary>
    public float Width => Size.Width;

    /// <summary>The box's height (<see cref="Size"/>).</summary>
    public float Height => Size.Height;

    /// <summary>
    /// The area the lines actually take: from the leftmost line start to the rightmost line end,
    /// from the first line's top to the last line's bottom - the measured size of the text.
    /// </summary>
    public RectangleF ContentBounds { get; }

    /// <summary>The box around every glyph outline (control points included); <c>null</c> when nothing draws.</summary>
    public RectangleF? InkBounds { get; }

    /// <summary>The lines, top to bottom.</summary>
    public IReadOnlyList<TextLineMetrics> Lines => _lines;

    /// <summary>How many lines were kept.</summary>
    public int LineCount => _lines.Length;

    /// <summary>The characters as the font shaped them (see <see cref="TextCluster"/>), in reading order.</summary>
    public IReadOnlyList<TextCluster> Clusters => _clusters;

    /// <summary>How many clusters there are (spaces and an ellipsis included).</summary>
    public int ClusterCount => _clusters.Length;

    /// <summary>How many words there are (runs of non-space clusters on a line).</summary>
    public int WordCount { get; }

    /// <summary>Whether lines were dropped because they did not fit (see <see cref="TextLayoutOptions.Trimming"/>).</summary>
    public bool IsTruncated { get; }

    /// <summary>Whether the last line ends in the ellipsis.</summary>
    public bool HasEllipsis => _clusters.Length > 0 && _clusters[_clusters.Length - 1].IsEllipsis;

    /// <summary>How many leading characters of <see cref="Text"/> the block shows (all of them unless truncated).</summary>
    public int ShownTextLength { get; }

    /// <summary>One line's metrics.</summary>
    /// <param name="lineIndex">The line, 0 to <see cref="LineCount"/> - 1.</param>
    /// <returns>The metrics.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lineIndex"/> is out of range.</exception>
    public TextLineMetrics GetLineMetrics(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= _lines.Length) { throw new ArgumentOutOfRangeException(nameof(lineIndex)); }
        return _lines[lineIndex];
    }

    /// <summary>How many units of a kind the block has: clusters, words or lines.</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The count.</returns>
    public int GetUnitCount(TextUnit unit) => unit switch
    {
        TextUnit.Word => WordCount,
        TextUnit.Line => _lines.Length,
        _ => _clusters.Length,
    };

    /// <summary>
    /// The block's characters, words or lines as pieces of their own, in reading order - each with
    /// its box, centre and outline, for animating them one by one. Character pieces include the
    /// spaces (which draw nothing); word pieces are the runs of non-space characters on a line; line
    /// pieces are the lines.
    /// </summary>
    /// <param name="unit">The kind of piece.</param>
    /// <returns>The pieces (the same list on every call).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unit"/> is not defined.</exception>
    public IReadOnlyList<TextPiece> GetPieces(TextUnit unit)
    {
        if (!Enum.IsDefined(unit)) { throw new ArgumentOutOfRangeException(nameof(unit)); }
        lock (_pieces)
        {
            TextPiece[] pieces = _pieces[(int)unit];
            if (pieces == null)
            {
                pieces = BuildPieces(unit);
                _pieces[(int)unit] = pieces;
            }
            return pieces;
        }
    }

    /// <summary>
    /// Every glyph outline of the block combined, in the layout's coordinates (non-zero fill rule) -
    /// to stroke, fill, trim (draw on), dash, mask or morph with a
    /// <see cref="CodeBrix.Imaging.Drawing.Shapes.PathShape"/>.
    /// </summary>
    /// <returns>The outline; <see cref="PathGeometry.Empty"/> when nothing draws.</returns>
    public PathGeometry GetOutline()
    {
        PathGeometry outline = _outline;
        if (outline == null)
        {
            outline = BuildOutline(0, _clusters.Length, 0f, 0f);
            _outline = outline;
        }
        return outline;
    }

    /// <summary>The block's outline moved so that its box's top-left corner is at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">Where the box's left edge goes.</param>
    /// <param name="y">Where the box's top edge goes.</param>
    /// <returns>The moved outline.</returns>
    public PathGeometry GetOutline(float x, float y) => x == 0f && y == 0f ? GetOutline() : BuildOutline(0, _clusters.Length, x, y);

    /// <summary>The cluster that covers a character of <see cref="Text"/>.</summary>
    /// <param name="textIndex">The character's index.</param>
    /// <returns>The cluster's index; or -1 when no cluster covers it (a line break, a cut-off character).</returns>
    public int GetClusterIndexAt(int textIndex)
    {
        int low = 0, high = _clusters.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            TextCluster cluster = _clusters[mid];
            if (cluster.IsEllipsis || textIndex < cluster.TextIndex) { high = mid - 1; }
            else if (textIndex >= cluster.TextIndex + cluster.TextLength) { low = mid + 1; }
            else { return mid; }
        }
        return -1;
    }

    /// <summary>The line that shows a character of <see cref="Text"/> (the text's end counts as on the last line).</summary>
    /// <param name="textIndex">The character's index.</param>
    /// <returns>The line's index; or -1 when the character was cut off.</returns>
    public int GetLineIndexAt(int textIndex)
    {
        for (int i = 0; i < _lines.Length; i++)
        {
            TextLineMetrics line = _lines[i];
            if (textIndex >= line.TextIndex && textIndex < line.TextIndex + line.TextLength) { return i; }
        }
        if (_lines.Length > 0 && textIndex == ShownTextLength) { return _lines.Length - 1; }
        return -1;
    }

    /// <summary>
    /// The box of the character at an index: its cluster's box; for a character without one (a line
    /// break, the text's end) a zero-width box at the end of its line.
    /// </summary>
    /// <param name="textIndex">The character's index, 0 to the text's length.</param>
    /// <returns>The box, in the layout's coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the text, or was cut off.</exception>
    public RectangleF GetRectForIndex(int textIndex)
    {
        if (textIndex < 0 || textIndex > Text.Length) { throw new ArgumentOutOfRangeException(nameof(textIndex)); }
        int cluster = GetClusterIndexAt(textIndex);
        if (cluster >= 0) { return _clusters[cluster].Bounds; }
        int lineIndex = GetLineIndexAt(textIndex);
        if (lineIndex < 0) { throw new ArgumentOutOfRangeException(nameof(textIndex), "The character was cut off by trimming."); }
        TextLineMetrics line = _lines[lineIndex];
        return new RectangleF(line.Left + line.Width, line.Top, 0f, line.Height);
    }

    /// <summary>
    /// The block's glyphs set along a path, each turned to follow it: every cluster's horizontal
    /// centre is placed on the path at <paramref name="startDistance"/> plus its distance from the
    /// box's left edge, its baseline (the first line's) laid along the path's direction there.
    /// Clusters that fall off an open path are left out; on a closed path they wrap round. Shared
    /// managed arithmetic (no trigonometry: the path's unit tangent turns each glyph), identical in
    /// both packages.
    /// </summary>
    /// <param name="path">The path to follow, in the coordinates the result should have.</param>
    /// <param name="startDistance">How far along the path (calibrated units) the box's left edge starts.</param>
    /// <param name="verticalOffset">How far below the path the baseline sits (negative: above), calibrated units.</param>
    /// <returns>The combined outline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance is not finite.</exception>
    public PathGeometry GetOutlineAlongPath(PathGeometry path, float startDistance = 0f, float verticalOffset = 0f)
        => PathGeometry.Combine(ToArray(GetClusterOutlinesAlongPath(path, startDistance, verticalOffset)));

    /// <summary>
    /// The same placement as <see cref="GetOutlineAlongPath"/>, one outline per cluster (in
    /// <see cref="Clusters"/> order; <see cref="PathGeometry.Empty"/> for a cluster that draws
    /// nothing or falls off the path) - to draw or animate the glyphs along the path one by one.
    /// </summary>
    /// <param name="path">The path to follow.</param>
    /// <param name="startDistance">How far along the path the box's left edge starts.</param>
    /// <param name="verticalOffset">How far below the path the baseline sits (negative: above).</param>
    /// <returns>The outlines.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance is not finite.</exception>
    public IReadOnlyList<PathGeometry> GetClusterOutlinesAlongPath(PathGeometry path, float startDistance = 0f, float verticalOffset = 0f)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (!Single.IsFinite(startDistance)) { throw new ArgumentOutOfRangeException(nameof(startDistance)); }
        if (!Single.IsFinite(verticalOffset)) { throw new ArgumentOutOfRangeException(nameof(verticalOffset)); }

        var result = new PathGeometry[_clusters.Length];
        float length = path.GetLength();
        bool closed = path.Contours.Count == 1 && path.Contours[0].IsClosed;
        float baseline = _lines.Length > 0 ? _lines[0].Baseline : 0f;
        for (int i = 0; i < _clusters.Length; i++)
        {
            TextCluster cluster = _clusters[i];
            result[i] = PathGeometry.Empty;
            if (!cluster.HasInk || path.IsEmpty) { continue; }
            float centre = cluster.Origin.X + (cluster.Advance / 2f);
            float distance = startDistance + centre;
            if (closed && length > 0f)
            {
                distance -= MathF.Floor(distance / length) * length;
            }
            else if (distance < 0f || distance > length)
            {
                continue;
            }
            PathPoint point = path.GetPointAtDistance(distance);
            float tx = point.Tangent.X, ty = point.Tangent.Y;
            //p' = P + (x - centre) T + (y - baseline + offset) N, with N = (-ty, tx) (y down)
            var matrix = new Matrix3x2(
                tx, ty,
                -ty, tx,
                point.Position.X - (centre * tx) + ((baseline - verticalOffset) * ty),
                point.Position.Y - (centre * ty) + ((verticalOffset - baseline) * tx));
            result[i] = cluster.GetOutline().Transformed(matrix);
        }
        return result;
    }

    //Adds clusters [first, first + count) to one outline, moved by (dx, dy)
    internal PathGeometry BuildOutline(int first, int count, float dx, float dy)
    {
        var builder = new PathGeometryBuilder();
        bool any = false;
        int end = Math.Min(_clusters.Length, first + count);
        for (int i = Math.Max(0, first); i < end; i++)
        {
            TextCluster cluster = _clusters[i];
            if (!cluster.HasInk) { continue; }
            TextOutline.AppendTo(builder, cluster.Verbs, cluster.Points, dx, dy);
            any = true;
        }
        return any ? builder.Build() : PathGeometry.Empty;
    }

    internal static RectangleF? Union(RectangleF? a, RectangleF? b)
    {
        if (!a.HasValue) { return b; }
        if (!b.HasValue) { return a; }
        RectangleF x = a.Value, y = b.Value;
        float left = Math.Min(x.Left, y.Left), top = Math.Min(x.Top, y.Top);
        float right = Math.Max(x.Right, y.Right), bottom = Math.Max(x.Bottom, y.Bottom);
        return new RectangleF(left, top, right - left, bottom - top);
    }

    private static PathGeometry[] ToArray(IReadOnlyList<PathGeometry> list)
    {
        var array = new PathGeometry[list.Count];
        for (int i = 0; i < array.Length; i++) { array[i] = list[i]; }
        return array;
    }

    private TextPiece[] BuildPieces(TextUnit unit)
    {
        var pieces = new List<TextPiece>();
        if (unit == TextUnit.Character)
        {
            foreach (TextCluster c in _clusters)
            {
                pieces.Add(new TextPiece(this, unit, pieces.Count, c.Index, 1, c.TextIndex, c.TextLength, c.Text,
                    c.LineIndex, c.Bounds, c.InkBounds, c.IsWhiteSpace));
            }
        }
        else if (unit == TextUnit.Word)
        {
            int i = 0;
            while (i < _clusters.Length)
            {
                if (_clusters[i].WordIndex < 0) { i++; continue; }
                int word = _clusters[i].WordIndex;
                int first = i;
                while (i < _clusters.Length && _clusters[i].WordIndex == word) { i++; }
                pieces.Add(Group(unit, pieces.Count, first, i - first, _clusters[first].LineIndex, null));
            }
        }
        else
        {
            foreach (TextLineMetrics line in _lines)
            {
                pieces.Add(Group(unit, pieces.Count, line.FirstCluster, line.ClusterCount, line.Index, line));
            }
        }
        return pieces.ToArray();
    }

    private TextPiece Group(TextUnit unit, int index, int first, int count, int lineIndex, TextLineMetrics line)
    {
        var text = new StringBuilder();
        RectangleF? box = null;
        RectangleF? ink = null;
        int textStart = line?.TextIndex ?? (count > 0 ? _clusters[first].TextIndex : 0);
        int textEnd = textStart;
        for (int i = first; i < first + count; i++)
        {
            TextCluster c = _clusters[i];
            text.Append(c.Text);
            box = Union(box, c.Bounds);
            ink = Union(ink, c.InkBounds);
            if (!c.IsEllipsis) { textEnd = Math.Max(textEnd, c.TextIndex + c.TextLength); }
        }
        int textLength = line?.TextLength ?? (textEnd - textStart);
        RectangleF bounds = line?.Bounds ?? box ?? RectangleF.Empty;
        return new TextPiece(this, unit, index, first, count, textStart, textLength, text.ToString(), lineIndex, bounds, ink, false);
    }
}
