namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The pixel filtering used when sampling a bitmap.
/// </summary>
public enum DrawingFilterMode
{
    /// <summary>Sample the single nearest pixel.</summary>
    Nearest = 0,

    /// <summary>Interpolate between the four nearest pixels (bilinear filtering).</summary>
    Linear = 1,
}
