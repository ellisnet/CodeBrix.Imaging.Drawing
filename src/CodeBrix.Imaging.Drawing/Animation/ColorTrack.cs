using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// A keyframe track of colors. Colors interpolate exactly as a <see cref="ColorRamp"/> does:
/// straight (not premultiplied) 8-bit sRGB, channel by channel with alpha included, each
/// channel <c>from + (to - from) * fraction</c> rounded to the nearest level (halves away from
/// zero) and clamped to 0..255 - so a track and a ramp through the same colors agree, and an
/// overshooting curve saturates rather than wrapping.
/// </summary>
public sealed class ColorTrack : KeyframeTrack<Color>
{
    /// <summary>Creates an empty track; add keys with <c>Add</c> or a collection initializer.</summary>
    public ColorTrack()
    {
    }

    /// <inheritdoc />
    protected override Color Interpolate(Color from, Color to, double fraction)
    {
        Rgba32 a = from.ToPixel<Rgba32>();
        Rgba32 b = to.ToPixel<Rgba32>();
        return Color.FromRgba(ColorRamp.Channel(a.R, b.R, fraction), ColorRamp.Channel(a.G, b.G, fraction),
            ColorRamp.Channel(a.B, b.B, fraction), ColorRamp.Channel(a.A, b.A, fraction));
    }
}
