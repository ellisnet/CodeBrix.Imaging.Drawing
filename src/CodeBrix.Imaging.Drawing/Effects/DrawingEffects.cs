using System;

namespace CodeBrix.Imaging.Drawing.Effects;

/// <summary>
/// Pixel effects for an element, a group or a whole layer: a Gaussian blur of the content, an
/// outer glow behind it and a drop shadow behind that. Immutable, with value equality; build one
/// with <see cref="Blur(float)"/>, <see cref="DropShadow(PointF, float, Color)"/> or
/// <see cref="Glow(float, Color, float)"/> and combine with the <c>With...</c> methods. Assign it to
/// <see cref="CodeBrix.Imaging.Drawing.Models.DrawingElement.Effects"/> or
/// <see cref="CodeBrix.Imaging.Drawing.Models.DrawingLayer.Effects"/>; animate the parameters with
/// <see cref="CodeBrix.Imaging.Drawing.Models.DrawingAnimations"/> (BlurRadius, ShadowOffset,
/// ShadowBlurRadius, ShadowColor, GlowRadius, GlowColor).
/// <para>
/// HOW THEY ARE MADE. The content is first drawn as usual into a private bitmap at the output
/// resolution; the effects are then COMPUTED from those pixels in shared managed code (the
/// <see cref="CodeBrix.Imaging.Drawing.Planes"/> Gaussian blur, in straight-alpha float colour),
/// so both packages apply exactly the same arithmetic and differ only by the usual anti-aliasing
/// difference of the content itself. In order: (1) the content is blurred by
/// <see cref="BlurRadius"/>; (2) the glow is the content's coverage blurred by
/// <see cref="GlowRadius"/>, multiplied by <see cref="GlowStrength"/> (capped at full), in
/// <see cref="GlowColor"/>; (3) the shadow is the coverage blurred by
/// <see cref="ShadowBlurRadius"/>, moved by <see cref="ShadowOffset"/>, in
/// <see cref="ShadowColor"/>; the result is the content over the glow over the shadow (the
/// colours' alpha is each effect's opacity). A blur radius follows the CSS convention: the
/// Gaussian's standard deviation is half the radius.
/// </para>
/// <para>
/// SIZES FOLLOW THE CONTENT. Radii and the shadow offset are in the element's OWN units: they
/// grow and shrink with the element's scale, its groups' and its layer's scale, the camera's
/// zoom and the output size, exactly as its outline does - zooming in keeps every shadow, glow
/// and blur in proportion, and an export at twice the size looks the same, only sharper. A
/// radius is scaled by the square root of the area scale (the geometric mean of the two axis
/// scales), so a stretched element gets a round blur of the mean size. For a layer's effects
/// the layer's transform (under the camera) plays the element's part.
/// </para>
/// <para>
/// DIRECTION (<see cref="ShadowDirection"/>). By default (<see cref="Effects.ShadowDirection.FixedInScene"/>)
/// the light is fixed in the scene: the offset keeps its direction however the element, its
/// groups or its layer turn, and turns only with the camera's roll (the whole scene turns). It
/// is still stretched by their scale as seen along the scene's axes (a horizontal scale of 2
/// doubles a horizontal offset; a uniform scale multiplies it whatever the turn) and never
/// mirrored. With <see cref="Effects.ShadowDirection.RotatesWithElement"/> the offset is a vector
/// in the element's own coordinates and goes through its whole transform - it turns, stretches
/// and mirrors with it.
/// </para>
/// </summary>
public sealed class DrawingEffects : IEquatable<DrawingEffects>
{
    /// <summary>No effect at all.</summary>
    public static readonly DrawingEffects None = new DrawingEffects(0f, Color.Transparent, new PointF(0f, 0f), 0f, Color.Transparent, 0f, 1f,
        ShadowDirection.FixedInScene);

    private DrawingEffects(float blurRadius, Color shadowColor, PointF shadowOffset, float shadowBlurRadius,
        Color glowColor, float glowRadius, float glowStrength, ShadowDirection shadowDirection)
    {
        BlurRadius = CheckRadius(blurRadius, nameof(blurRadius));
        ShadowBlurRadius = CheckRadius(shadowBlurRadius, nameof(shadowBlurRadius));
        GlowRadius = CheckRadius(glowRadius, nameof(glowRadius));
        if (!Single.IsFinite(shadowOffset.X) || !Single.IsFinite(shadowOffset.Y)) { throw new ArgumentOutOfRangeException(nameof(shadowOffset)); }
        if (!Single.IsFinite(glowStrength) || glowStrength < 0f) { throw new ArgumentOutOfRangeException(nameof(glowStrength)); }
        if (!Enum.IsDefined(shadowDirection)) { throw new ArgumentOutOfRangeException(nameof(shadowDirection)); }
        ShadowDirection = shadowDirection;
        ShadowColor = shadowColor;
        ShadowOffset = shadowOffset;
        GlowColor = glowColor;
        GlowStrength = glowStrength;
    }

    /// <summary>How much the content itself is blurred, in the element's own units (0 = sharp).</summary>
    public float BlurRadius { get; }

    /// <summary>The shadow's colour; its alpha is the shadow's opacity (transparent = no shadow).</summary>
    public Color ShadowColor { get; }

    /// <summary>
    /// How far the shadow is moved from the content, in the element's own units (positive = right
    /// and down; see <see cref="ShadowDirection"/> for which way "right" is when the element turns).
    /// </summary>
    public PointF ShadowOffset { get; }

    /// <summary>
    /// Whether the shadow's direction is fixed in the scene (the default: it ignores the
    /// element's own turning and follows only the camera's roll) or turns with the element.
    /// </summary>
    public ShadowDirection ShadowDirection { get; }

    /// <summary>How soft the shadow is, in the element's own units (0 = a hard-edged copy).</summary>
    public float ShadowBlurRadius { get; }

    /// <summary>The glow's colour; its alpha is the glow's opacity (transparent = no glow).</summary>
    public Color GlowColor { get; }

    /// <summary>How far the glow spreads, in the element's own units (0 = no glow).</summary>
    public float GlowRadius { get; }

    /// <summary>A multiplier on the glow's blurred coverage, capped at full coverage (1 by default; above 1 the glow is denser).</summary>
    public float GlowStrength { get; }

    /// <summary>Indicates whether the content is blurred.</summary>
    public bool HasBlur => BlurRadius > 0f;

    /// <summary>Indicates whether a shadow is drawn (its colour is not fully transparent).</summary>
    public bool HasShadow => ShadowColor.ToRgba32().A > 0;

    /// <summary>Indicates whether a glow is drawn (a radius above 0, a colour not fully transparent and a strength above 0).</summary>
    public bool HasGlow => GlowRadius > 0f && GlowStrength > 0f && GlowColor.ToRgba32().A > 0;

    /// <summary>Indicates whether nothing would change the content (no blur, shadow or glow).</summary>
    public bool IsEmpty => !HasBlur && !HasShadow && !HasGlow;

    /// <summary>A Gaussian blur of the content.</summary>
    /// <param name="radius">The blur radius, in the element's own units (standard deviation = radius / 2).</param>
    /// <returns>The effects.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is negative or not finite.</exception>
    public static DrawingEffects Blur(float radius) => None.WithBlurRadius(radius);

    /// <summary>A drop shadow behind the content.</summary>
    /// <param name="offset">How far the shadow is moved, in the element's own units.</param>
    /// <param name="blurRadius">How soft it is, in the element's own units.</param>
    /// <param name="color">Its colour; the alpha is its opacity.</param>
    /// <returns>The effects.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is negative or not finite.</exception>
    public static DrawingEffects DropShadow(PointF offset, float blurRadius, Color color) => None.WithShadow(offset, blurRadius, color);

    /// <summary>A drop shadow behind the content, with a choice of how its direction follows the element.</summary>
    /// <param name="offset">How far the shadow is moved, in the element's own units.</param>
    /// <param name="blurRadius">How soft it is, in the element's own units.</param>
    /// <param name="color">Its colour; the alpha is its opacity.</param>
    /// <param name="direction">Fixed in the scene, or turning with the element.</param>
    /// <returns>The effects.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is negative, not finite or undefined.</exception>
    public static DrawingEffects DropShadow(PointF offset, float blurRadius, Color color, ShadowDirection direction)
        => None.WithShadow(offset, blurRadius, color, direction);

    /// <summary>An outer glow behind the content.</summary>
    /// <param name="radius">How far the glow spreads, in the element's own units.</param>
    /// <param name="color">Its colour; the alpha is its opacity.</param>
    /// <param name="strength">A multiplier on its coverage (1 by default).</param>
    /// <returns>The effects.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is negative or not finite.</exception>
    public static DrawingEffects Glow(float radius, Color color, float strength = 1f) => None.WithGlow(radius, color, strength);

    /// <summary>The same effects with another content blur radius.</summary>
    /// <param name="radius">The blur radius, in the element's own units.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithBlurRadius(float radius)
        => new DrawingEffects(radius, ShadowColor, ShadowOffset, ShadowBlurRadius, GlowColor, GlowRadius, GlowStrength, ShadowDirection);

    /// <summary>The same effects with another shadow.</summary>
    /// <param name="offset">How far the shadow is moved, in the element's own units.</param>
    /// <param name="blurRadius">How soft it is, in the element's own units.</param>
    /// <param name="color">Its colour; the alpha is its opacity.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithShadow(PointF offset, float blurRadius, Color color)
        => new DrawingEffects(BlurRadius, color, offset, blurRadius, GlowColor, GlowRadius, GlowStrength, ShadowDirection);

    /// <summary>The same effects with another shadow, direction included.</summary>
    /// <param name="offset">How far the shadow is moved, in the element's own units.</param>
    /// <param name="blurRadius">How soft it is, in the element's own units.</param>
    /// <param name="color">Its colour; the alpha is its opacity.</param>
    /// <param name="direction">Fixed in the scene, or turning with the element.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithShadow(PointF offset, float blurRadius, Color color, ShadowDirection direction)
        => new DrawingEffects(BlurRadius, color, offset, blurRadius, GlowColor, GlowRadius, GlowStrength, direction);

    /// <summary>The same effects with the shadow's direction fixed in the scene or turning with the element.</summary>
    /// <param name="direction">The direction rule.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithShadowDirection(ShadowDirection direction)
        => WithShadow(ShadowOffset, ShadowBlurRadius, ShadowColor, direction);

    /// <summary>The same effects with another shadow offset.</summary>
    /// <param name="offset">How far the shadow is moved, in the element's own units.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithShadowOffset(PointF offset) => WithShadow(offset, ShadowBlurRadius, ShadowColor);

    /// <summary>The same effects with another shadow softness.</summary>
    /// <param name="blurRadius">How soft the shadow is, in the element's own units.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithShadowBlurRadius(float blurRadius) => WithShadow(ShadowOffset, blurRadius, ShadowColor);

    /// <summary>The same effects with another shadow colour.</summary>
    /// <param name="color">The colour; the alpha is the shadow's opacity.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithShadowColor(Color color) => WithShadow(ShadowOffset, ShadowBlurRadius, color);

    /// <summary>The same effects with another glow.</summary>
    /// <param name="radius">How far the glow spreads, in the element's own units.</param>
    /// <param name="color">Its colour; the alpha is its opacity.</param>
    /// <param name="strength">A multiplier on its coverage.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithGlow(float radius, Color color, float strength = 1f)
        => new DrawingEffects(BlurRadius, ShadowColor, ShadowOffset, ShadowBlurRadius, color, radius, strength, ShadowDirection);

    /// <summary>The same effects with another glow radius.</summary>
    /// <param name="radius">How far the glow spreads, in the element's own units.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithGlowRadius(float radius) => WithGlow(radius, GlowColor, GlowStrength);

    /// <summary>The same effects with another glow colour.</summary>
    /// <param name="color">The colour; the alpha is the glow's opacity.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithGlowColor(Color color) => WithGlow(GlowRadius, color, GlowStrength);

    /// <summary>The same effects with another glow strength.</summary>
    /// <param name="strength">A multiplier on the glow's coverage.</param>
    /// <returns>A new instance.</returns>
    public DrawingEffects WithGlowStrength(float strength) => WithGlow(GlowRadius, GlowColor, strength);

    /// <summary>Indicates whether two sets of effects have the same values.</summary>
    /// <param name="other">The other effects.</param>
    /// <returns><c>true</c> when they are equal.</returns>
    public bool Equals(DrawingEffects other)
        => other is not null && BlurRadius.Equals(other.BlurRadius) && ShadowColor.Equals(other.ShadowColor)
            && ShadowOffset.Equals(other.ShadowOffset) && ShadowBlurRadius.Equals(other.ShadowBlurRadius)
            && GlowColor.Equals(other.GlowColor) && GlowRadius.Equals(other.GlowRadius) && GlowStrength.Equals(other.GlowStrength)
            && ShadowDirection == other.ShadowDirection;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is DrawingEffects other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(BlurRadius, ShadowColor, ShadowOffset, ShadowBlurRadius, GlowColor, GlowRadius, GlowStrength, ShadowDirection);

    /// <summary>Indicates whether two sets of effects are equal (both <c>null</c> counts as equal).</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns><c>true</c> when they are equal.</returns>
    public static bool operator ==(DrawingEffects left, DrawingEffects right) => left is null ? right is null : left.Equals(right);

    /// <summary>Indicates whether two sets of effects differ.</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns><c>true</c> when they differ.</returns>
    public static bool operator !=(DrawingEffects left, DrawingEffects right) => !(left == right);

    /// <inheritdoc />
    public override string ToString()
        => $"DrawingEffects (blur {BlurRadius}, shadow {ShadowOffset} r{ShadowBlurRadius} {ShadowColor} {ShadowDirection}, glow r{GlowRadius} {GlowColor} x{GlowStrength})";

    //A stable 64-bit state for render caches
    internal ulong GetStateHash()
    {
        ulong hash = 0x6A09E667F3BCC909UL;
        hash = Models.DrawingElement.MixStamp(hash, (ulong)BitConverter.SingleToInt32Bits(BlurRadius));
        hash = Models.DrawingElement.MixStamp(hash, (ulong)ShadowColor.ToRgba32().PackedValue);
        hash = Models.DrawingElement.MixStamp(hash, (ulong)BitConverter.SingleToInt32Bits(ShadowOffset.X));
        hash = Models.DrawingElement.MixStamp(hash, (ulong)BitConverter.SingleToInt32Bits(ShadowOffset.Y));
        hash = Models.DrawingElement.MixStamp(hash, (ulong)BitConverter.SingleToInt32Bits(ShadowBlurRadius));
        hash = Models.DrawingElement.MixStamp(hash, (ulong)GlowColor.ToRgba32().PackedValue);
        hash = Models.DrawingElement.MixStamp(hash, (ulong)BitConverter.SingleToInt32Bits(GlowRadius));
        hash = Models.DrawingElement.MixStamp(hash, (ulong)BitConverter.SingleToInt32Bits(GlowStrength));
        hash = Models.DrawingElement.MixStamp(hash, (ulong)ShadowDirection);
        return hash;
    }

    private static float CheckRadius(float value, string name)
    {
        if (!Single.IsFinite(value) || value < 0f) { throw new ArgumentOutOfRangeException(name); }
        return value;
    }
}
