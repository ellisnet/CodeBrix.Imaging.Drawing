namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// A keyframe track of on/off values - a switch over time, such as whether a layer is shown
/// (<see cref="CodeBrix.Imaging.Drawing.Models.DrawingLayer.IsVisible"/>). It always STEPS: between
/// two keys the earlier key's value holds, and the track takes the next key's value exactly at
/// that key's time (every key acts as a hold key; easing curves are not used).
/// <c>new BoolTrack { { 0, false }, { 2.5, true }, { 6, false } }</c> is on from 2.5 s until 6 s.
/// </summary>
public sealed class BoolTrack : KeyframeTrack<bool>
{
    /// <summary>Creates an empty track; add keys with <c>Add</c> or a collection initializer.</summary>
    public BoolTrack()
    {
    }

    /// <inheritdoc />
    protected override bool Interpolate(bool from, bool to, double fraction) => from;
}
