using System;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// A point on a path found by distance along it: where it is, which way the path runs there
/// and how far along it lies. Returned by <see cref="PathGeometry.GetPointAt(float)"/> and
/// <see cref="PathGeometry.GetPointAtDistance(float)"/>.
/// </summary>
public readonly struct PathPoint : IEquatable<PathPoint>
{
    /// <summary>Creates a path point.</summary>
    /// <param name="position">The point, in calibrated drawing units.</param>
    /// <param name="tangent">The unit direction of travel there.</param>
    /// <param name="distance">The distance along the path, calibrated units.</param>
    /// <param name="contourIndex">The index of the contour the point lies on.</param>
    public PathPoint(PointF position, PointF tangent, float distance, int contourIndex)
    {
        Position = position;
        Tangent = tangent;
        Distance = distance;
        ContourIndex = contourIndex;
    }

    /// <summary>The point, in calibrated drawing units.</summary>
    public PointF Position { get; }

    /// <summary>The unit vector of the direction the path runs at the point (length 1; (1, 0) for a path with no extent).</summary>
    public PointF Tangent { get; }

    /// <summary>
    /// The direction of travel as an angle in degrees: 0 points right, 90 points down (the
    /// screen's y axis points down), so it is the rotation to give an element that should
    /// face along the path (see <see cref="CodeBrix.Imaging.Drawing.Models.DrawingTransform.Rotation"/>).
    /// </summary>
    public float Angle => (float)(Math.Atan2(Tangent.Y, Tangent.X) * (180.0 / Math.PI));

    /// <summary>The distance along the whole path from its start, calibrated units.</summary>
    public float Distance { get; }

    /// <summary>The index (in <see cref="PathGeometry.Contours"/>) of the contour the point lies on; -1 for an empty path.</summary>
    public int ContourIndex { get; }

    /// <summary>Indicates whether two points are equal in every value.</summary>
    /// <param name="other">The other point.</param>
    /// <returns><c>true</c> when they are equal.</returns>
    public bool Equals(PathPoint other)
        => Position.Equals(other.Position) && Tangent.Equals(other.Tangent)
            && Distance.Equals(other.Distance) && ContourIndex == other.ContourIndex;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is PathPoint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Position, Tangent, Distance, ContourIndex);

    /// <summary>Indicates whether two points are equal.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><c>true</c> when they are equal.</returns>
    public static bool operator ==(PathPoint left, PathPoint right) => left.Equals(right);

    /// <summary>Indicates whether two points differ.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns><c>true</c> when they differ.</returns>
    public static bool operator !=(PathPoint left, PathPoint right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"{Position} at {Distance} heading {Angle} deg";
}
