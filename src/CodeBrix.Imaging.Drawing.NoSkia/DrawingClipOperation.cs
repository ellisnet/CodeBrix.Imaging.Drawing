namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How a new clip shape combines with the current clip, API-compatible with the SkiaSharp
/// <c>SKClipOperation</c> enumeration.
/// </summary>
public enum DrawingClipOperation
{
    /// <summary>The clip becomes the region OUTSIDE the shape, intersected with the current clip.</summary>
    Difference = 0,

    /// <summary>The clip becomes the region inside the shape, intersected with the current clip.</summary>
    Intersect = 1,
}
