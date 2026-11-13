namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// A keyframe track of points - a position or a scale pair, in calibrated drawing units. Each
/// coordinate interpolates on its own as <c>from + (to - from) * fraction</c>, so the point
/// travels in a straight line between keys (eased along it).
/// </summary>
public sealed class PointTrack : KeyframeTrack<PointF>
{
    /// <summary>Creates an empty track; add keys with <c>Add</c> or a collection initializer.</summary>
    public PointTrack()
    {
    }

    /// <inheritdoc />
    protected override PointF Interpolate(PointF from, PointF to, double fraction)
        => new PointF(Easing.Lerp(from.X, to.X, fraction), Easing.Lerp(from.Y, to.Y, fraction));
}
