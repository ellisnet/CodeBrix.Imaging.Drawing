namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// How a filled path decides which areas are inside when its contours overlap or cross
/// themselves - the two rules of SVG's <c>fill-rule</c>.
/// </summary>
public enum PathFillRule
{
    /// <summary>
    /// A point is inside when the contours wind around it a non-zero number of times
    /// (the default): overlapping shapes drawn the same way round stay filled.
    /// </summary>
    NonZero = 0,

    /// <summary>
    /// A point is inside when a ray from it crosses the contours an odd number of times:
    /// every second nested contour is a hole, whichever way round it is drawn.
    /// </summary>
    EvenOdd = 1,
}
