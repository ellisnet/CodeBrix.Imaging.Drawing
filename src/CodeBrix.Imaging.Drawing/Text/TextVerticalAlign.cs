namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// Where a laid-out block's lines sit vertically within the box height
/// (<see cref="TextLayoutOptions.MaxHeight"/>). Without a box height there is nothing to align
/// within and the lines start at the top.
/// </summary>
public enum TextVerticalAlign
{
    /// <summary>The first line starts at the top of the box.</summary>
    Top = 0,

    /// <summary>The lines are centered in the box.</summary>
    Center = 1,

    /// <summary>The last line ends at the bottom of the box.</summary>
    Bottom = 2,
}
