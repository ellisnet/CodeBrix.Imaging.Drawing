namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// How <see cref="DrawingPlane.Resized"/> and
/// <see cref="DrawingPlaneImage.Resized"/> choose the source
/// values for each output pixel. Every rule is exact integer or float arithmetic, so a resize
/// gives bit-identical results in both packages.
/// </summary>
public enum DrawingPlaneResampling
{
    /// <summary>
    /// Bilinear interpolation on the pixel-centre grid: output pixel <c>i</c> samples source
    /// position <c>(i + 0.5) * sourceSize / outputSize - 0.5</c>, clamped to the source, so the
    /// image's outer edges line up. This is the usual choice for enlarging or shrinking a picture.
    /// </summary>
    Bilinear = 0,
    /// <summary>
    /// Each output pixel copies the source pixel whose area contains the output pixel's centre:
    /// output index <c>i</c> reads source index <c>floor((2i + 1) * sourceSize / (2 * outputSize))</c>,
    /// computed in integers. Values are copied exactly, never blended.
    /// </summary>
    Nearest = 1,
    /// <summary>
    /// Bilinear interpolation on the corner-aligned grid: output pixel <c>i</c> samples source
    /// position <c>i * (sourceSize - 1) / (outputSize - 1)</c>, so the first and last output pixels
    /// land exactly on the first and last source pixels. This is the "stretch a small glow plane
    /// over the frame" choice.
    /// </summary>
    BilinearAlignedCorners = 2,
}
