namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How a run of text is positioned horizontally relative to the origin its command
/// carries.
/// </summary>
public enum DrawingTextAlign
{
    /// <summary>The origin is the run's left edge.</summary>
    Left = 0,

    /// <summary>The origin is the run's horizontal center.</summary>
    Center = 1,

    /// <summary>The origin is the run's right edge.</summary>
    Right = 2,
}
