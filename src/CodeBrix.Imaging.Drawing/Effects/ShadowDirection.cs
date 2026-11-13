namespace CodeBrix.Imaging.Drawing.Effects;

/// <summary>
/// Which way a <see cref="DrawingEffects"/> drop shadow falls when the element turns: the
/// light can be fixed in the scene (the default) or carried round with the element.
/// </summary>
public enum ShadowDirection
{
    /// <summary>
    /// The light is fixed in the SCENE: the shadow offset keeps its direction however the
    /// element, its groups or its layer turn (their scale still stretches it), and turns only
    /// when the whole scene turns - with the camera's roll.
    /// </summary>
    FixedInScene = 0,

    /// <summary>
    /// The shadow turns (and is mirrored and stretched) with the element: the offset is in the
    /// element's own coordinates, through its transform, its groups', its layer's and the camera.
    /// </summary>
    RotatesWithElement = 1,
}
