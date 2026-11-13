namespace CodeBrix.Imaging.Drawing.Fills;

/// <summary>
/// How a gradient paints the area beyond its first and last stop.
/// </summary>
public enum GradientSpread
{
    /// <summary>The end colors extend outward unchanged (the conventional default).</summary>
    Pad = 0,

    /// <summary>The gradient repeats, starting over from its first stop.</summary>
    Repeat = 1,

    /// <summary>The gradient repeats, reversing direction each time (a mirror image).</summary>
    Reflect = 2,
}
