namespace CodeBrix.Imaging.Drawing.Images;

/// <summary>
/// How a <see cref="CodeBrix.Imaging.Drawing.Shapes.SpriteShape"/> maps its running frame number
/// onto the frames of its sheet.
/// </summary>
public enum SpritePlayback
{
    /// <summary>Repeats the frames in order forever (0, 1, 2, 0, 1, 2, ...), in both directions of time.</summary>
    Loop = 0,

    /// <summary>Plays the frames forward then back without repeating the ends (0, 1, 2, 1, 0, 1, ...).</summary>
    PingPong = 1,

    /// <summary>Plays the frames once and holds the last one (and the first one before the start).</summary>
    Hold = 2,
}
