namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How source pixels combine with destination pixels, API-compatible with the SkiaSharp
/// <c>SKBlendMode</c> enumeration: the twelve Porter-Duff compositing operators plus the
/// standard separable and non-separable blend modes.
/// </summary>
public enum DrawingBlendMode
{
    /// <summary>Destination pixels covered by the source are cleared to transparent.</summary>
    Clear = 0,

    /// <summary>The source replaces the destination.</summary>
    Src = 1,

    /// <summary>The destination is kept; the source is ignored.</summary>
    Dst = 2,

    /// <summary>The source composites over the destination (the default).</summary>
    SrcOver = 3,

    /// <summary>The destination composites over the source.</summary>
    DstOver = 4,

    /// <summary>The source, but only where the destination is opaque.</summary>
    SrcIn = 5,

    /// <summary>The destination, but only where the source is opaque.</summary>
    DstIn = 6,

    /// <summary>The source, but only where the destination is transparent.</summary>
    SrcOut = 7,

    /// <summary>The destination, but only where the source is transparent.</summary>
    DstOut = 8,

    /// <summary>The source atop the destination, bounded by the destination's alpha.</summary>
    SrcATop = 9,

    /// <summary>The destination atop the source, bounded by the source's alpha.</summary>
    DstATop = 10,

    /// <summary>The exclusive-or of source and destination coverage.</summary>
    Xor = 11,

    /// <summary>The clamped sum of source and destination.</summary>
    Plus = 12,

    /// <summary>The product of source and destination (premultiplied multiply).</summary>
    Modulate = 13,

    /// <summary>Inverse of the product of the inverses.</summary>
    Screen = 14,

    /// <summary>Multiplies or screens, depending on the destination.</summary>
    Overlay = 15,

    /// <summary>The darker of source and destination.</summary>
    Darken = 16,

    /// <summary>The lighter of source and destination.</summary>
    Lighten = 17,

    /// <summary>Brightens the destination to reflect the source.</summary>
    ColorDodge = 18,

    /// <summary>Darkens the destination to reflect the source.</summary>
    ColorBurn = 19,

    /// <summary>Multiplies or screens, depending on the source.</summary>
    HardLight = 20,

    /// <summary>Darkens or lightens, softly, depending on the source.</summary>
    SoftLight = 21,

    /// <summary>The absolute difference of source and destination.</summary>
    Difference = 22,

    /// <summary>A lower-contrast difference.</summary>
    Exclusion = 23,

    /// <summary>The product of source and destination colors after alpha compositing.</summary>
    Multiply = 24,

    /// <summary>The source's hue with the destination's saturation and luminosity.</summary>
    Hue = 25,

    /// <summary>The source's saturation with the destination's hue and luminosity.</summary>
    Saturation = 26,

    /// <summary>The source's hue and saturation with the destination's luminosity.</summary>
    Color = 27,

    /// <summary>The source's luminosity with the destination's hue and saturation.</summary>
    Luminosity = 28,
}
