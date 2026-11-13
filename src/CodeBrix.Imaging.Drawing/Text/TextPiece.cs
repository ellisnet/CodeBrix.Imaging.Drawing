using CodeBrix.Imaging.Drawing.Paths;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// One character, word or line of a <see cref="TextLayoutResult"/> (see
/// <see cref="TextLayoutResult.GetPieces(TextUnit)"/>) as a unit of its own: which clusters and
/// characters it covers, its box, its centre (the natural pivot to scale or turn it about) and
/// its combined glyph outline - for animating each piece separately. Positions are in the
/// layout's coordinates (calibrated units, (0, 0) at the box's top-left).
/// </summary>
public sealed class TextPiece
{
    private readonly TextLayoutResult _layout;
    private PathGeometry _outline;

    internal TextPiece(TextLayoutResult layout, TextUnit unit, int index, int firstCluster, int clusterCount,
        int textIndex, int textLength, string text, int lineIndex, RectangleF bounds, RectangleF? inkBounds, bool isWhiteSpace)
    {
        _layout = layout;
        Unit = unit;
        Index = index;
        FirstCluster = firstCluster;
        ClusterCount = clusterCount;
        TextIndex = textIndex;
        TextLength = textLength;
        Text = text;
        LineIndex = lineIndex;
        Bounds = bounds;
        InkBounds = inkBounds;
        IsWhiteSpace = isWhiteSpace;
    }

    /// <summary>What kind of piece it is.</summary>
    public TextUnit Unit { get; }

    /// <summary>Its position among the pieces of its kind.</summary>
    public int Index { get; }

    /// <summary>The index of its first cluster in <see cref="TextLayoutResult.Clusters"/>.</summary>
    public int FirstCluster { get; }

    /// <summary>How many clusters it covers (0 for an empty line).</summary>
    public int ClusterCount { get; }

    /// <summary>The index of its first character in the layout's text.</summary>
    public int TextIndex { get; }

    /// <summary>How many characters of the layout's text it covers.</summary>
    public int TextLength { get; }

    /// <summary>The text it draws (an ellipsis it ends in included).</summary>
    public string Text { get; }

    /// <summary>The line it is on (a line piece: its own index).</summary>
    public int LineIndex { get; }

    /// <summary>Its box: the union of its clusters' boxes (for a line, the line's box).</summary>
    public RectangleF Bounds { get; }

    /// <summary>The box around its outline; <c>null</c> when it draws nothing.</summary>
    public RectangleF? InkBounds { get; }

    /// <summary>The centre of <see cref="Bounds"/>.</summary>
    public PointF Center => new PointF(Bounds.X + (Bounds.Width / 2f), Bounds.Y + (Bounds.Height / 2f));

    /// <summary>Whether it is a space (character pieces only; words and lines never are).</summary>
    public bool IsWhiteSpace { get; }

    /// <summary>
    /// Its glyph outlines combined, in the layout's coordinates; <see cref="PathGeometry.Empty"/>
    /// when it draws nothing. Built on first use.
    /// </summary>
    /// <returns>The outline.</returns>
    public PathGeometry GetOutline()
    {
        PathGeometry outline = _outline;
        if (outline == null)
        {
            outline = _layout.BuildOutline(FirstCluster, ClusterCount, 0f, 0f);
            _outline = outline;
        }
        return outline;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Unit} {Index} \"{Text}\" (clusters {FirstCluster}+{ClusterCount}, line {LineIndex}) {Bounds}";
}
