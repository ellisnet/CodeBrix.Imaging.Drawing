namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Everything the rasterizer needs to composite a fill: the paint source, the optional
/// color filter and blend mode, the optional clip coverage mask, and the anti-aliasing
/// choice.
/// </summary>
internal sealed class FillContext
{
    /// <summary>The source of per-pixel paint colors.</summary>
    public PaintSource Paint;

    /// <summary>An optional color filter applied to the paint's output.</summary>
    public DrawingColorFilter ColorFilter;

    /// <summary>How pixels combine with the destination.</summary>
    public DrawingBlendMode BlendMode = DrawingBlendMode.SrcOver;

    /// <summary>
    /// An optional clip coverage mask (one 0..1 value per target pixel, row-major);
    /// <c>null</c> means unclipped.
    /// </summary>
    public float[] ClipMask;

    /// <summary>Whether fractional edge coverage composites as partial alpha.</summary>
    public bool Antialias;
}
