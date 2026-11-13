using System;
using CodeBrix.Imaging.Drawing.Paths;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// One positioned character of a <see cref="TextLayoutResult"/> as the font shaped it - usually
/// one letter or space, but an accented letter with its mark, or a ligature such as "fi" that the
/// font draws as one glyph, is one cluster too - with its pen origin, advance, box and glyph
/// outline. Positions are in the layout's coordinates (calibrated units, (0, 0) at the box's
/// top-left, y down). Clusters are what a typewriter reveal counts and what per-character
/// animation splits by.
/// </summary>
public sealed class TextCluster
{
    private readonly byte[] _verbs;
    private readonly float[] _points;
    private PathGeometry _outline;

    internal TextCluster(int index, int textIndex, int textLength, string text, int lineIndex, int wordIndex, int runIndex,
        bool isWhiteSpace, bool isEllipsis, PointF origin, float advance, RectangleF bounds, byte[] verbs, float[] points,
        RectangleF? inkBounds)
    {
        Index = index;
        TextIndex = textIndex;
        TextLength = textLength;
        Text = text;
        LineIndex = lineIndex;
        WordIndex = wordIndex;
        RunIndex = runIndex;
        IsWhiteSpace = isWhiteSpace;
        IsEllipsis = isEllipsis;
        Origin = origin;
        Advance = advance;
        Bounds = bounds;
        _verbs = verbs ?? Array.Empty<byte>();
        _points = points ?? Array.Empty<float>();
        InkBounds = inkBounds;
    }

    /// <summary>The cluster's position in <see cref="TextLayoutResult.Clusters"/>.</summary>
    public int Index { get; }

    /// <summary>The index of its first character in the layout's text (for the ellipsis: where the text was cut).</summary>
    public int TextIndex { get; }

    /// <summary>How many characters of the layout's text it covers (0 for the ellipsis).</summary>
    public int TextLength { get; }

    /// <summary>The characters it draws (the ellipsis text for the ellipsis).</summary>
    public string Text { get; }

    /// <summary>The line it is on.</summary>
    public int LineIndex { get; }

    /// <summary>The word it belongs to; -1 for a space.</summary>
    public int WordIndex { get; }

    /// <summary>The run (<see cref="TextRunDescriptor"/>) it came from.</summary>
    public int RunIndex { get; }

    /// <summary>Whether it is white space (it draws nothing).</summary>
    public bool IsWhiteSpace { get; }

    /// <summary>Whether it is the ellipsis a trimmed block ends in.</summary>
    public bool IsEllipsis { get; }

    /// <summary>The pen's start for it, on the baseline.</summary>
    public PointF Origin { get; }

    /// <summary>How far it advances the pen (kerning, tracking excluded; justified spaces include their stretch).</summary>
    public float Advance { get; }

    /// <summary>Its box: from <see cref="Origin"/> across <see cref="Advance"/>, the full height of its line.</summary>
    public RectangleF Bounds { get; }

    /// <summary>
    /// The box around its outline (curve control points included, so it may be slightly larger
    /// than the ink); <c>null</c> when it draws nothing.
    /// </summary>
    public RectangleF? InkBounds { get; }

    /// <summary>Whether it draws anything.</summary>
    public bool HasInk => _verbs.Length > 0;

    internal byte[] Verbs => _verbs;

    internal float[] Points => _points;

    /// <summary>
    /// The glyph outline exactly as drawn, in the layout's coordinates (closed contours, non-zero
    /// fill rule); <see cref="PathGeometry.Empty"/> when it draws nothing. Built on first use.
    /// </summary>
    /// <returns>The outline.</returns>
    public PathGeometry GetOutline()
    {
        PathGeometry outline = _outline;
        if (outline != null) { return outline; }
        if (_verbs.Length == 0)
        {
            outline = PathGeometry.Empty;
        }
        else
        {
            var builder = new PathGeometryBuilder();
            TextOutline.AppendTo(builder, _verbs, _points, 0f, 0f);
            outline = builder.Build();
        }
        _outline = outline;
        return outline;
    }

    /// <inheritdoc />
    public override string ToString() => $"cluster {Index} \"{Text}\" (text {TextIndex}+{TextLength}, line {LineIndex}, word {WordIndex}) at {Origin}, advance {Advance}";
}
