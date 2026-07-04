using System;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// A single point of a <see cref="Stroke"/>, expressed in calibrated (device-independent)
/// drawing coordinates, together with the time at which the point was added relative to
/// the start of its stroke.
/// </summary>
public readonly struct StrokePoint : IEquatable<StrokePoint>
{
    /// <summary>
    /// The horizontal position of the point, in calibrated drawing coordinates.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// The vertical position of the point, in calibrated drawing coordinates.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// The number of milliseconds that had elapsed since the start of the stroke when
    /// this point was added - useful for later playback of a drawing at its original speed.
    /// </summary>
    public int TimeOffsetMs { get; }

    /// <summary>
    /// Creates a new calibrated stroke point.
    /// </summary>
    /// <param name="x">The horizontal position of the point, in calibrated drawing coordinates.</param>
    /// <param name="y">The vertical position of the point, in calibrated drawing coordinates.</param>
    /// <param name="timeOffsetMs">
    /// The number of milliseconds elapsed since the start of the stroke; pass zero when
    /// playback timing is not needed.
    /// </param>
    public StrokePoint(int x, int y, int timeOffsetMs = 0)
    {
        X = x;
        Y = y;
        TimeOffsetMs = timeOffsetMs;
    }

    /// <summary>
    /// Indicates whether this point has the same position as another point; the
    /// <see cref="TimeOffsetMs"/> value is not considered.
    /// </summary>
    /// <param name="other">The point to compare with this point.</param>
    /// <returns><c>true</c> when both points have the same <see cref="X"/> and <see cref="Y"/> values.</returns>
    public bool IsSamePositionAs(StrokePoint other) => X == other.X && Y == other.Y;

    /// <inheritdoc />
    public bool Equals(StrokePoint other) => X == other.X && Y == other.Y && TimeOffsetMs == other.TimeOffsetMs;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is StrokePoint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, TimeOffsetMs);

    /// <summary>
    /// Indicates whether two stroke points are equal.
    /// </summary>
    /// <param name="left">The first point to compare.</param>
    /// <param name="right">The second point to compare.</param>
    /// <returns><c>true</c> when the two points are equal.</returns>
    public static bool operator ==(StrokePoint left, StrokePoint right) => left.Equals(right);

    /// <summary>
    /// Indicates whether two stroke points are not equal.
    /// </summary>
    /// <param name="left">The first point to compare.</param>
    /// <param name="right">The second point to compare.</param>
    /// <returns><c>true</c> when the two points are not equal.</returns>
    public static bool operator !=(StrokePoint left, StrokePoint right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"({X},{Y}) @{TimeOffsetMs}ms";
}
