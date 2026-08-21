namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The kinds of segment a path is built from, API-compatible with the SkiaSharp
/// <c>DrawingPathVerb</c> enumeration (reduced to the verbs this managed implementation stores).
/// </summary>
public enum DrawingPathVerb
{
    /// <summary>Begin a new contour at a point (one point).</summary>
    Move = 0,

    /// <summary>A straight line to a point (one point).</summary>
    Line = 1,

    /// <summary>A quadratic Bezier curve (two points: control, end).</summary>
    Quad = 2,

    /// <summary>A cubic Bezier curve (three points: two controls, end).</summary>
    Cubic = 3,

    /// <summary>Close the current contour back to its starting point (no points).</summary>
    Close = 4,
}
