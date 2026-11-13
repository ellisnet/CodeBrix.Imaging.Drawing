namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// The kind of one segment of a <see cref="PathContour"/>. Arcs are stored as cubic Bezier
/// segments (converted when the path is built), so these three kinds describe every path.
/// </summary>
public enum PathSegmentKind
{
    /// <summary>A straight line from the start point to the end point.</summary>
    Line = 0,

    /// <summary>A quadratic Bezier curve with one control point.</summary>
    Quadratic = 1,

    /// <summary>A cubic Bezier curve with two control points.</summary>
    Cubic = 2,
}
