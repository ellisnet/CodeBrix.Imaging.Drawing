namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// The unit a laid-out block is revealed or split by - for a typewriter reveal, or for
/// animating each character, word or line as its own element.
/// </summary>
public enum TextUnit
{
    /// <summary>
    /// One character as the font shaped it (a <see cref="TextCluster"/>): usually one letter,
    /// a space, an accented letter with its mark, or a ligature such as "fi" that the font
    /// draws as one glyph.
    /// </summary>
    Character = 0,

    /// <summary>A run of non-space characters on one line (spaces belong to no word).</summary>
    Word = 1,

    /// <summary>One laid-out line.</summary>
    Line = 2,
}
