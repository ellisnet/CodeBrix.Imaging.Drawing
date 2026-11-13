using System;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// One segment of a <see cref="PathContour"/>: a straight line, a quadratic Bezier curve or
/// a cubic Bezier curve, from <see cref="Start"/> to <see cref="End"/>, in calibrated drawing
/// units.
/// </summary>
public readonly struct PathSegment : IEquatable<PathSegment>
{
    /// <summary>
    /// Creates a segment. For a <see cref="PathSegmentKind.Line"/> the control points are
    /// ignored (and stored as the start and end points); for a
    /// <see cref="PathSegmentKind.Quadratic"/> curve the second control point is ignored (and
    /// stored as the first).
    /// </summary>
    /// <param name="kind">The kind of segment.</param>
    /// <param name="start">The start point.</param>
    /// <param name="control1">The first (or only) control point.</param>
    /// <param name="control2">The second control point of a cubic curve.</param>
    /// <param name="end">The end point.</param>
    public PathSegment(PathSegmentKind kind, PointF start, PointF control1, PointF control2, PointF end)
    {
        Kind = kind;
        Start = start;
        End = end;
        switch (kind)
        {
            case PathSegmentKind.Line:
                Control1 = start;
                Control2 = end;
                break;
            case PathSegmentKind.Quadratic:
                Control1 = control1;
                Control2 = control1;
                break;
            default:
                Control1 = control1;
                Control2 = control2;
                break;
        }
    }

    /// <summary>The kind of segment.</summary>
    public PathSegmentKind Kind { get; }

    /// <summary>The start point (the previous segment's end, or the contour's start).</summary>
    public PointF Start { get; }

    /// <summary>The first control point of a curve (the start point for a line).</summary>
    public PointF Control1 { get; }

    /// <summary>The second control point of a cubic curve (the first control point of a quadratic curve, the end point for a line).</summary>
    public PointF Control2 { get; }

    /// <summary>The end point.</summary>
    public PointF End { get; }

    /// <summary>Creates a straight line segment.</summary>
    /// <param name="start">The start point.</param>
    /// <param name="end">The end point.</param>
    /// <returns>The segment.</returns>
    public static PathSegment Line(PointF start, PointF end) => new PathSegment(PathSegmentKind.Line, start, start, end, end);

    /// <summary>Creates a quadratic Bezier segment.</summary>
    /// <param name="start">The start point.</param>
    /// <param name="control">The control point.</param>
    /// <param name="end">The end point.</param>
    /// <returns>The segment.</returns>
    public static PathSegment Quadratic(PointF start, PointF control, PointF end)
        => new PathSegment(PathSegmentKind.Quadratic, start, control, control, end);

    /// <summary>Creates a cubic Bezier segment.</summary>
    /// <param name="start">The start point.</param>
    /// <param name="control1">The first control point.</param>
    /// <param name="control2">The second control point.</param>
    /// <param name="end">The end point.</param>
    /// <returns>The segment.</returns>
    public static PathSegment Cubic(PointF start, PointF control1, PointF control2, PointF end)
        => new PathSegment(PathSegmentKind.Cubic, start, control1, control2, end);

    /// <summary>Indicates whether two segments are of the same kind through the same points.</summary>
    /// <param name="other">The other segment.</param>
    /// <returns><c>true</c> when they are equal.</returns>
    public bool Equals(PathSegment other)
        => Kind == other.Kind && Start.Equals(other.Start) && Control1.Equals(other.Control1)
            && Control2.Equals(other.Control2) && End.Equals(other.End);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is PathSegment other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Start, Control1, Control2, End);

    /// <summary>Indicates whether two segments are equal.</summary>
    /// <param name="left">The first segment.</param>
    /// <param name="right">The second segment.</param>
    /// <returns><c>true</c> when they are equal.</returns>
    public static bool operator ==(PathSegment left, PathSegment right) => left.Equals(right);

    /// <summary>Indicates whether two segments differ.</summary>
    /// <param name="left">The first segment.</param>
    /// <param name="right">The second segment.</param>
    /// <returns><c>true</c> when they differ.</returns>
    public static bool operator !=(PathSegment left, PathSegment right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"{Kind} {Start} -> {End}";
}
