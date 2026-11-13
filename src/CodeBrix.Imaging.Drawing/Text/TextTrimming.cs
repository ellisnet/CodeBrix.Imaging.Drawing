namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// What a laid-out block does when its text does not fit - more lines than
/// <see cref="TextLayoutOptions.MaxLines"/> allows, or than fit in
/// <see cref="TextLayoutOptions.MaxHeight"/>. The lines that do not fit are always dropped;
/// trimming decides whether the last kept line says so.
/// </summary>
public enum TextTrimming
{
    /// <summary>The extra lines are dropped silently.</summary>
    None = 0,

    /// <summary>
    /// The last kept line ends in the ellipsis (<see cref="TextLayoutOptions.Ellipsis"/>),
    /// removing as many characters as needed for it to fit the width.
    /// </summary>
    CharacterEllipsis = 1,

    /// <summary>
    /// The last kept line ends in the ellipsis after the last WHOLE word that fits (a line
    /// holding one word too long for that falls back to cutting characters).
    /// </summary>
    WordEllipsis = 2,
}
