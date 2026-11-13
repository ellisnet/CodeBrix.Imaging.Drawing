using CodeBrix.Imaging;

namespace AutumnAviana.Animation;

/// <summary>
/// Where things are, in scene units (1 unit = 1 pixel of the 1920x1080 frame, y down). The camera starts at the
/// top of the scene and pans down by <see cref="PanDistance" />, after which the two lines of text sit centred in
/// the frame as one block.
/// </summary>
internal static class Stage
{
    /// <summary>The frame's width.</summary>
    public const int Width = 1920;

    /// <summary>The frame's height.</summary>
    public const int Height = 1080;

    /// <summary>The frame size.</summary>
    public static readonly Size FrameSize = new Size(Width, Height);

    /// <summary>The ground the AVIANA piles stand on.</summary>
    public const float PileGround = 992f;

    /// <summary>The height of the pile letters (before their leafy fringe).</summary>
    public const float PileLetterHeight = 262f;

    /// <summary>The size of the second line's text (font size, scene units).</summary>
    public const float LineTwoSize = 246f;

    /// <summary>The second line's baseline - the ground the raccoons roll the letters along.</summary>
    public const float LineTwoBaseline = PileGround + 86f + (LineTwoSize * 0.711f);

    /// <summary>
    /// How far the camera moves down: chosen so that the block from the top of the piles to the bottom of the
    /// second line's descenders is centred in the frame.
    /// </summary>
    public static readonly float PanDistance = ((PileGround - PileLetterHeight - 14f + LineTwoBaseline + (LineTwoSize * 0.212f)) / 2f) - (Height / 2f);

    /// <summary>The line the raccoons' feet walk on: a little in front of (below) the letters' baseline.</summary>
    public const float RaccoonGround = LineTwoBaseline + 10f;
}
