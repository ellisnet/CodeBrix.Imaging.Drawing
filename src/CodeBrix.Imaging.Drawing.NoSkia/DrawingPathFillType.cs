namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The rule that decides which regions are inside a filled path, API-compatible with the
/// SkiaSharp <c>SKPathFillType</c> enumeration.
/// </summary>
public enum DrawingPathFillType
{
    /// <summary>A region is inside when its winding number is non-zero.</summary>
    Winding = 0,

    /// <summary>A region is inside when it is crossed an odd number of times.</summary>
    EvenOdd = 1,
}
