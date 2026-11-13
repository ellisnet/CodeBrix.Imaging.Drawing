using System;

namespace CodeBrix.Imaging.Drawing.Layout;

/// <summary>
/// A position described relative to the frame or to the safe area, so one description fits
/// any frame shape: an <see cref="Anchor"/> point of the <see cref="Reference"/> rectangle,
/// moved by <see cref="Offset"/> (calibrated units) and by <see cref="FractionOffset"/>
/// (fractions of the reference rectangle's width and height). "Centred, 5% of the safe
/// height above the bottom safe edge" is
/// <c>new LayoutPosition(LayoutAnchor.BottomCenter, LayoutReference.SafeArea, fractionY: -0.05f)</c>.
/// Resolve it with <see cref="DrawingLayout.Place(LayoutPosition)"/>.
/// </summary>
public readonly struct LayoutPosition : IEquatable<LayoutPosition>
{
    /// <summary>
    /// Creates a position.
    /// </summary>
    /// <param name="anchor">The point of the reference rectangle to start from.</param>
    /// <param name="reference">The rectangle to measure against; the frame by default.</param>
    /// <param name="offsetX">A horizontal move in calibrated units (positive = right).</param>
    /// <param name="offsetY">A vertical move in calibrated units (positive = down).</param>
    /// <param name="fractionX">A horizontal move as a fraction of the reference width (positive = right).</param>
    /// <param name="fractionY">A vertical move as a fraction of the reference height (positive = down).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an offset is not finite.</exception>
    public LayoutPosition(LayoutAnchor anchor, LayoutReference reference = LayoutReference.Frame,
        float offsetX = 0f, float offsetY = 0f, float fractionX = 0f, float fractionY = 0f)
    {
        if (!Single.IsFinite(offsetX)) { throw new ArgumentOutOfRangeException(nameof(offsetX)); }
        if (!Single.IsFinite(offsetY)) { throw new ArgumentOutOfRangeException(nameof(offsetY)); }
        if (!Single.IsFinite(fractionX)) { throw new ArgumentOutOfRangeException(nameof(fractionX)); }
        if (!Single.IsFinite(fractionY)) { throw new ArgumentOutOfRangeException(nameof(fractionY)); }
        Anchor = anchor;
        Reference = reference;
        Offset = new PointF(offsetX, offsetY);
        FractionOffset = new PointF(fractionX, fractionY);
    }

    /// <summary>The point of the reference rectangle to start from.</summary>
    public LayoutAnchor Anchor { get; }

    /// <summary>The rectangle the position is measured against.</summary>
    public LayoutReference Reference { get; }

    /// <summary>The move in calibrated units.</summary>
    public PointF Offset { get; }

    /// <summary>The move as fractions of the reference rectangle's width (X) and height (Y).</summary>
    public PointF FractionOffset { get; }

    /// <inheritdoc />
    public bool Equals(LayoutPosition other)
        => Anchor == other.Anchor && Reference == other.Reference && Offset == other.Offset && FractionOffset == other.FractionOffset;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is LayoutPosition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Anchor, Reference, Offset, FractionOffset);

    /// <summary>Compares two positions by value.</summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns><c>true</c> when they are equal.</returns>
    public static bool operator ==(LayoutPosition left, LayoutPosition right) => left.Equals(right);

    /// <summary>Compares two positions by value.</summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns><c>true</c> when they differ.</returns>
    public static bool operator !=(LayoutPosition left, LayoutPosition right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"{Anchor} of {Reference}, offset {Offset}, fraction {FractionOffset}";
}
