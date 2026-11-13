using System;
using CodeBrix.Imaging.Drawing.Models;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// Where a formation object is and how it is turned and scaled at one moment - for content drawn
/// CENTRED ON (0, 0): <see cref="ToTransform"/> puts that centre at (<see cref="X"/>,
/// <see cref="Y"/>).
/// </summary>
public readonly struct FormationPose : IEquatable<FormationPose>
{
    /// <summary>Creates a pose.</summary>
    /// <param name="x">The centre's x, calibrated units.</param>
    /// <param name="y">The centre's y, calibrated units.</param>
    /// <param name="rotation">The rotation, degrees clockwise.</param>
    /// <param name="scaleX">The width scale (negative = mirrored, e.g. seen from behind).</param>
    /// <param name="scaleY">The height scale.</param>
    public FormationPose(float x, float y, float rotation = 0f, float scaleX = 1f, float scaleY = 1f)
    {
        X = x;
        Y = y;
        Rotation = rotation;
        ScaleX = scaleX;
        ScaleY = scaleY;
    }

    /// <summary>The centre's x, calibrated units.</summary>
    public float X { get; }

    /// <summary>The centre's y, calibrated units.</summary>
    public float Y { get; }

    /// <summary>The centre, calibrated units.</summary>
    public PointF Position => new PointF(X, Y);

    /// <summary>The rotation, degrees clockwise.</summary>
    public float Rotation { get; }

    /// <summary>The width scale (negative = mirrored).</summary>
    public float ScaleX { get; }

    /// <summary>The height scale.</summary>
    public float ScaleY { get; }

    /// <summary>The pose as a drawing transform of content drawn centred on (0, 0): translation (X, Y), the rotation and scales, anchor (0, 0).</summary>
    /// <returns>The transform.</returns>
    public DrawingTransform ToTransform() => new DrawingTransform(new PointF(X, Y), Rotation, ScaleX, ScaleY);

    /// <inheritdoc />
    public bool Equals(FormationPose other)
        => X.Equals(other.X) && Y.Equals(other.Y) && Rotation.Equals(other.Rotation) && ScaleX.Equals(other.ScaleX) && ScaleY.Equals(other.ScaleY);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is FormationPose other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, Rotation, ScaleX, ScaleY);

    /// <summary>Compares two poses value by value.</summary>
    /// <param name="left">The first pose.</param>
    /// <param name="right">The second pose.</param>
    /// <returns><c>true</c> when every value is equal.</returns>
    public static bool operator ==(FormationPose left, FormationPose right) => left.Equals(right);

    /// <summary>Compares two poses value by value.</summary>
    /// <param name="left">The first pose.</param>
    /// <param name="right">The second pose.</param>
    /// <returns><c>true</c> when any value differs.</returns>
    public static bool operator !=(FormationPose left, FormationPose right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y}) {Rotation} deg, scale {ScaleX} x {ScaleY}";
}
