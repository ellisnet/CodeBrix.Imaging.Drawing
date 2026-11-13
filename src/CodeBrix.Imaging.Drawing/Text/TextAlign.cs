namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// How a line of text sits about its anchor x: the anchor is the left edge, the center or
/// the right edge of the line's advance width (tracking included). In a laid-out block
/// (<see cref="TextLayoutOptions.Align"/>) the same values place each line within the
/// block's width, and <see cref="Justify"/> also becomes available.
/// </summary>
public enum TextAlign
{
    /// <summary>The text starts at the anchor and runs to its right.</summary>
    Left = 0,

    /// <summary>The text is centered on the anchor.</summary>
    Center = 1,

    /// <summary>The text ends at the anchor.</summary>
    Right = 2,

    /// <summary>
    /// Text blocks only: every wrapped line is stretched to the block's width by widening its
    /// spaces; the last line of each paragraph (and a line without spaces) stays left-aligned.
    /// A single-line <see cref="CodeBrix.Imaging.Drawing.Shapes.TextShape"/> does not accept it.
    /// </summary>
    Justify = 3,
}
