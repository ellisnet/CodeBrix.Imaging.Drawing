using System;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// An element's own frame of reference - a centre, a direction, and the element's extent along
/// and across that direction - in the element's own calibrated coordinates (before its own
/// transform). A brushstroke reports its centre line; a rectangle its centre with the x axis.
/// An ownership map measures every pixel an element owns in these axes ("how far along the
/// stroke, how far across it"), which is what bristle ridges, along-the-stroke ramps and other
/// brush textures are computed from. The direction is in DEGREES, clockwise on screen from +x
/// (y points down) - the unit of <see cref="DrawingTransform.Rotation"/> and
/// <see cref="Paths.PathPoint.Angle"/>.
/// </summary>
public readonly struct ElementAxes : IEquatable<ElementAxes>
{
    /// <summary>
    /// Creates a frame of reference.
    /// </summary>
    /// <param name="center">The origin: the element's centre, in its own calibrated coordinates.</param>
    /// <param name="angle">The direction of the "along" axis, in degrees, clockwise on screen from the +x axis (y points down).</param>
    /// <param name="length">The element's extent along that axis (end to end); 0 when it has none.</param>
    /// <param name="width">The element's extent across it (side to side); 0 when it has none.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite, or <paramref name="length"/> or <paramref name="width"/> is negative.</exception>
    public ElementAxes(PointF center, float angle, float length, float width)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y)) { throw new ArgumentOutOfRangeException(nameof(center)); }
        if (!float.IsFinite(angle)) { throw new ArgumentOutOfRangeException(nameof(angle)); }
        if (!float.IsFinite(length) || length < 0f) { throw new ArgumentOutOfRangeException(nameof(length)); }
        if (!float.IsFinite(width) || width < 0f) { throw new ArgumentOutOfRangeException(nameof(width)); }
        Center = center;
        Angle = angle;
        Length = length;
        Width = width;
    }

    /// <summary>The origin, in the element's own calibrated coordinates.</summary>
    public PointF Center { get; }

    /// <summary>The direction of the "along" axis, in degrees, clockwise on screen from +x (y points down).</summary>
    public float Angle { get; }

    /// <summary>The element's extent along the axis, end to end.</summary>
    public float Length { get; }

    /// <summary>The element's extent across the axis, side to side.</summary>
    public float Width { get; }

    /// <summary>
    /// The axes of an axis-aligned box: centred on it, along +x, its width as the length and its
    /// height as the width - what an ownership map uses for an element that reports no axes of
    /// its own.
    /// </summary>
    /// <param name="bounds">The box, calibrated units.</param>
    /// <returns>The axes.</returns>
    public static ElementAxes FromBounds(RectangleF bounds)
        => new ElementAxes(new PointF(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f)), 0f,
            Math.Max(0f, bounds.Width), Math.Max(0f, bounds.Height));

    /// <inheritdoc />
    public bool Equals(ElementAxes other)
        => Center.Equals(other.Center) && Angle.Equals(other.Angle) && Length.Equals(other.Length) && Width.Equals(other.Width);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is ElementAxes other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Center, Angle, Length, Width);

    /// <summary>Compares two frames of reference value by value.</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns><c>true</c> when every value is equal.</returns>
    public static bool operator ==(ElementAxes left, ElementAxes right) => left.Equals(right);

    /// <summary>Compares two frames of reference value by value.</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns><c>true</c> when any value differs.</returns>
    public static bool operator !=(ElementAxes left, ElementAxes right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString()
        => FormattableString.Invariant($"ElementAxes(center {Center.X}, {Center.Y}; angle {Angle} deg; length {Length}; width {Width})");
}
