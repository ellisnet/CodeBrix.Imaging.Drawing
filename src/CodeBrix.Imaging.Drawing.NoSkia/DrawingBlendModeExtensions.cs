namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Classifies <see cref="DrawingBlendMode"/> values by the family they belong to. A
/// consumer that re-emits a picture into a container with its own compositing model (PDF,
/// for example) needs to know which modes are plain coverage compositing and which are
/// blend functions, because the two map to different constructs.
/// <see cref="DrawingBlendMode.Plus"/> and <see cref="DrawingBlendMode.Modulate"/> belong
/// to none of the three families - they are arithmetic modes with no Porter-Duff or CSS
/// blend equivalent.
/// </summary>
public static class DrawingBlendModeExtensions
{
    /// <summary>
    /// Indicates whether the mode is one of the twelve Porter-Duff compositing operators
    /// (<see cref="DrawingBlendMode.Clear"/> through <see cref="DrawingBlendMode.Xor"/>).
    /// </summary>
    /// <param name="mode">The mode to classify.</param>
    /// <returns><c>true</c> when the mode is a Porter-Duff operator.</returns>
    public static bool IsPorterDuff(this DrawingBlendMode mode)
        => mode >= DrawingBlendMode.Clear && mode <= DrawingBlendMode.Xor;

    /// <summary>
    /// Indicates whether the mode is a separable blend function - one that combines each
    /// color channel independently (<see cref="DrawingBlendMode.Screen"/> through
    /// <see cref="DrawingBlendMode.Multiply"/>).
    /// </summary>
    /// <param name="mode">The mode to classify.</param>
    /// <returns><c>true</c> when the mode is a separable blend function.</returns>
    public static bool IsSeparableBlend(this DrawingBlendMode mode)
        => mode >= DrawingBlendMode.Screen && mode <= DrawingBlendMode.Multiply;

    /// <summary>
    /// Indicates whether the mode is a non-separable blend function - one that works on
    /// hue, saturation, color, and luminosity as a whole
    /// (<see cref="DrawingBlendMode.Hue"/> through <see cref="DrawingBlendMode.Luminosity"/>).
    /// </summary>
    /// <param name="mode">The mode to classify.</param>
    /// <returns><c>true</c> when the mode is a non-separable blend function.</returns>
    public static bool IsNonSeparableBlend(this DrawingBlendMode mode)
        => mode >= DrawingBlendMode.Hue && mode <= DrawingBlendMode.Luminosity;
}
