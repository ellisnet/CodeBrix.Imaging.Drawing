namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// A keyframe track of single-precision values - a position along one axis, a rotation in
/// degrees, a scale, an opacity. Values interpolate as <c>from + (to - from) * fraction</c>
/// (<see cref="Easing.Lerp(float, float, double)"/>), so overshooting curves extrapolate.
/// </summary>
public sealed class FloatTrack : KeyframeTrack<float>
{
    /// <summary>Creates an empty track; add keys with <c>Add</c> or a collection initializer.</summary>
    public FloatTrack()
    {
    }

    /// <inheritdoc />
    protected override float Interpolate(float from, float to, double fraction) => Easing.Lerp(from, to, fraction);
}
