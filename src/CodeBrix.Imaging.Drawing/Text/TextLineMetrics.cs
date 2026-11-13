namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// The geometry of one laid-out line of a <see cref="TextLayoutResult"/>: which slice of the text
/// it shows, where it sits vertically (top, height, baseline), and where its ink run starts and
/// how wide it is. All positions are in the layout's own coordinates - calibrated drawing units
/// with (0, 0) at the top-left of the block's box, y down.
/// </summary>
public sealed class TextLineMetrics
{
    internal TextLineMetrics(int index, int start, int length, float top, float height, float baselineOffset,
        float left, float width, float ascent, float descent, int firstCluster, int clusterCount, bool endsParagraph)
    {
        Index = index;
        TextIndex = start;
        TextLength = length;
        Top = top;
        Height = height;
        BaselineOffset = baselineOffset;
        Left = left;
        Width = width;
        Ascent = ascent;
        Descent = descent;
        FirstCluster = firstCluster;
        ClusterCount = clusterCount;
        EndsParagraph = endsParagraph;
    }

    /// <summary>The zero-based line number.</summary>
    public int Index { get; }

    /// <summary>The index of the line's first character in the layout's text.</summary>
    public int TextIndex { get; }

    /// <summary>
    /// How many characters the line covers, including trailing spaces and the line break that
    /// ends it (a trimmed last line covers only what it shows).
    /// </summary>
    public int TextLength { get; }

    /// <summary>The line's top edge.</summary>
    public float Top { get; }

    /// <summary>The line's height (its share of the block's height).</summary>
    public float Height { get; }

    /// <summary>The distance from the line's top edge down to its baseline.</summary>
    public float BaselineOffset { get; }

    /// <summary>The baseline's position: <see cref="Top"/> + <see cref="BaselineOffset"/>.</summary>
    public float Baseline => Top + BaselineOffset;

    /// <summary>Where the line's first character starts, after alignment.</summary>
    public float Left { get; }

    /// <summary>
    /// The line's advance width: from its first character's pen start to its last visible
    /// character's end (tracking between characters included; trailing spaces not).
    /// </summary>
    public float Width { get; }

    /// <summary>The tallest ascent of the fonts on the line (above the baseline).</summary>
    public float Ascent { get; }

    /// <summary>The deepest descent of the fonts on the line (below the baseline, positive).</summary>
    public float Descent { get; }

    /// <summary>The index of the line's first cluster in <see cref="TextLayoutResult.Clusters"/>.</summary>
    public int FirstCluster { get; }

    /// <summary>How many clusters the line holds (0 for an empty line).</summary>
    public int ClusterCount { get; }

    /// <summary>Whether the line ends its paragraph (the text breaks there, or ends).</summary>
    public bool EndsParagraph { get; }

    /// <summary>The line's box: from <see cref="Left"/> across <see cref="Width"/>, from <see cref="Top"/> down <see cref="Height"/>.</summary>
    public RectangleF Bounds => new RectangleF(Left, Top, Width, Height);

    /// <inheritdoc />
    public override string ToString() => $"line {Index}: text [{TextIndex}, {TextIndex + TextLength}), top {Top}, height {Height}, baseline {Baseline}, left {Left}, width {Width}";
}
