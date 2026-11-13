namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// The direction a wipe's edge, or a sliding / pushing frame, TRAVELS across the picture.
/// <see cref="Right"/> means the incoming frame appears at the left edge and advances to the right.
/// </summary>
public enum DrawingTransitionDirection
{
    /// <summary>Travels towards the left; the incoming frame enters from the right edge.</summary>
    Left = 0,

    /// <summary>Travels towards the right; the incoming frame enters from the left edge.</summary>
    Right = 1,

    /// <summary>Travels upwards; the incoming frame enters from the bottom edge.</summary>
    Up = 2,

    /// <summary>Travels downwards; the incoming frame enters from the top edge.</summary>
    Down = 3,
}
