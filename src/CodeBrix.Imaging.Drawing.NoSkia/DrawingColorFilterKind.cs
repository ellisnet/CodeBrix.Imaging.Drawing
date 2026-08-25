namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// What a <see cref="DrawingColorFilter"/> does to each pixel - the discriminator that
/// says which of the filter's inspection properties carry meaningful values.
/// </summary>
public enum DrawingColorFilterKind
{
    /// <summary>A 4x5 color matrix, in <see cref="DrawingColorFilter.Matrix"/>.</summary>
    ColorMatrix = 0,

    /// <summary>Per-channel 256-entry lookup tables.</summary>
    Table = 1,

    /// <summary>A constant color blended over each pixel.</summary>
    BlendMode = 2,

    /// <summary>The luminance-to-alpha filter that realizes SVG luminance masks.</summary>
    LumaColor = 3,
}
