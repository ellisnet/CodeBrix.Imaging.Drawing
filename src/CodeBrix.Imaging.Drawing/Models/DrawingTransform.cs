using System;
using System.Numerics;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// A 2D placement in calibrated drawing units - move, rotate and scale about an anchor - that
/// an element (<see cref="DrawingElement.Transform"/>), a group or a whole layer
/// (<see cref="DrawingLayer.Transform"/>) is drawn through. A point p of the content lands at
/// <c>Anchor + Translation + Rotate(Scale(p - Anchor))</c>: scaled about the anchor, rotated
/// about it, then moved. Rotation is in DEGREES and, because y points down, positive turns
/// clockwise on screen. Immutable and thread-safe; <see cref="Identity"/> (no change) is the
/// default everywhere, and the With* methods make changed copies.
/// </summary>
public sealed class DrawingTransform : IEquatable<DrawingTransform>
{
    /// <summary>The transform that changes nothing.</summary>
    public static readonly DrawingTransform Identity = new DrawingTransform(new PointF(0f, 0f));

    /// <summary>How far the content moves, in calibrated drawing units.</summary>
    public PointF Translation { get; }

    /// <summary>The rotation about <see cref="Anchor"/>, degrees; positive turns clockwise on screen.</summary>
    public float Rotation { get; }

    /// <summary>The horizontal scale about <see cref="Anchor"/> (1 = unchanged; negative mirrors).</summary>
    public float ScaleX { get; }

    /// <summary>The vertical scale about <see cref="Anchor"/> (1 = unchanged; negative mirrors).</summary>
    public float ScaleY { get; }

    /// <summary>
    /// The pivot that rotation and scale happen about, in the content's own calibrated
    /// coordinates (for a circle, usually its center). It does not move the content by itself.
    /// </summary>
    public PointF Anchor { get; }

    /// <summary>Indicates whether the transform changes nothing (no move, no rotation, scale 1 both ways).</summary>
    public bool IsIdentity => Translation.X == 0f && Translation.Y == 0f && Rotation == 0f && ScaleX == 1f && ScaleY == 1f;

    /// <summary>
    /// Creates a transform.
    /// </summary>
    /// <param name="translation">How far the content moves, calibrated units.</param>
    /// <param name="rotation">The rotation about the anchor, degrees (positive = clockwise on screen).</param>
    /// <param name="scaleX">The horizontal scale about the anchor.</param>
    /// <param name="scaleY">The vertical scale about the anchor.</param>
    /// <param name="anchor">The pivot of rotation and scale, in the content's coordinates.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any value is not finite.</exception>
    public DrawingTransform(PointF translation, float rotation = 0f, float scaleX = 1f, float scaleY = 1f, PointF anchor = default)
    {
        if (!Single.IsFinite(translation.X) || !Single.IsFinite(translation.Y)) { throw new ArgumentOutOfRangeException(nameof(translation)); }
        if (!Single.IsFinite(rotation)) { throw new ArgumentOutOfRangeException(nameof(rotation)); }
        if (!Single.IsFinite(scaleX)) { throw new ArgumentOutOfRangeException(nameof(scaleX)); }
        if (!Single.IsFinite(scaleY)) { throw new ArgumentOutOfRangeException(nameof(scaleY)); }
        if (!Single.IsFinite(anchor.X) || !Single.IsFinite(anchor.Y)) { throw new ArgumentOutOfRangeException(nameof(anchor)); }
        Translation = translation;
        Rotation = rotation;
        ScaleX = scaleX;
        ScaleY = scaleY;
        Anchor = anchor;
    }

    /// <summary>A transform that only moves the content.</summary>
    /// <param name="x">The horizontal move, calibrated units.</param>
    /// <param name="y">The vertical move, calibrated units.</param>
    /// <returns>The transform.</returns>
    public static DrawingTransform Translate(float x, float y) => new DrawingTransform(new PointF(x, y));

    /// <summary>A transform that only rotates the content about a point.</summary>
    /// <param name="degrees">The rotation, degrees (positive = clockwise on screen).</param>
    /// <param name="anchor">The point to rotate about.</param>
    /// <returns>The transform.</returns>
    public static DrawingTransform Rotate(float degrees, PointF anchor) => new DrawingTransform(new PointF(0f, 0f), degrees, 1f, 1f, anchor);

    /// <summary>A transform that only scales the content about a point, the same both ways.</summary>
    /// <param name="scale">The scale.</param>
    /// <param name="anchor">The point to scale about.</param>
    /// <returns>The transform.</returns>
    public static DrawingTransform Scale(float scale, PointF anchor) => new DrawingTransform(new PointF(0f, 0f), 0f, scale, scale, anchor);

    /// <summary>A copy with a different <see cref="Translation"/>.</summary>
    /// <param name="translation">The new translation.</param>
    /// <returns>The changed copy.</returns>
    public DrawingTransform WithTranslation(PointF translation) => new DrawingTransform(translation, Rotation, ScaleX, ScaleY, Anchor);

    /// <summary>A copy with a different <see cref="Rotation"/>.</summary>
    /// <param name="degrees">The new rotation, degrees.</param>
    /// <returns>The changed copy.</returns>
    public DrawingTransform WithRotation(float degrees) => new DrawingTransform(Translation, degrees, ScaleX, ScaleY, Anchor);

    /// <summary>A copy with the same new scale both ways.</summary>
    /// <param name="scale">The new scale.</param>
    /// <returns>The changed copy.</returns>
    public DrawingTransform WithScale(float scale) => new DrawingTransform(Translation, Rotation, scale, scale, Anchor);

    /// <summary>A copy with a new horizontal and vertical scale.</summary>
    /// <param name="scaleX">The new horizontal scale.</param>
    /// <param name="scaleY">The new vertical scale.</param>
    /// <returns>The changed copy.</returns>
    public DrawingTransform WithScale(float scaleX, float scaleY) => new DrawingTransform(Translation, Rotation, scaleX, scaleY, Anchor);

    /// <summary>A copy with a different <see cref="Anchor"/>.</summary>
    /// <param name="anchor">The new anchor.</param>
    /// <returns>The changed copy.</returns>
    public DrawingTransform WithAnchor(PointF anchor) => new DrawingTransform(Translation, Rotation, ScaleX, ScaleY, anchor);

    /// <summary>
    /// The transform as a matrix (System.Numerics row-vector convention: a point is
    /// <c>Vector2.Transform(p, matrix)</c>).
    /// </summary>
    /// <returns>The matrix.</returns>
    public Matrix3x2 ToMatrix()
    {
        if (IsIdentity) { return Matrix3x2.Identity; }
        return Matrix3x2.CreateTranslation(-Anchor.X, -Anchor.Y)
            * Matrix3x2.CreateScale(ScaleX, ScaleY)
            * Matrix3x2.CreateRotation(Rotation * MathF.PI / 180f)
            * Matrix3x2.CreateTranslation(Anchor.X + Translation.X, Anchor.Y + Translation.Y);
    }

    /// <summary>Where a point of the content lands.</summary>
    /// <param name="point">The point, in the content's coordinates.</param>
    /// <returns>The transformed point.</returns>
    public PointF TransformPoint(PointF point)
    {
        if (IsIdentity) { return point; }
        Vector2 result = Vector2.Transform(new Vector2(point.X, point.Y), ToMatrix());
        return new PointF(result.X, result.Y);
    }

    /// <summary>
    /// The axis-aligned box around a rectangle after the transform (the box of its four
    /// transformed corners) - never smaller than the transformed rectangle itself. The
    /// identity returns the rectangle unchanged.
    /// </summary>
    /// <param name="bounds">The rectangle, in the content's coordinates.</param>
    /// <returns>The box around it after the transform.</returns>
    public RectangleF TransformBounds(RectangleF bounds) => IsIdentity ? bounds : TransformBounds(bounds, ToMatrix());

    /// <inheritdoc />
    public bool Equals(DrawingTransform other)
        => other is not null
            && Translation.X == other.Translation.X && Translation.Y == other.Translation.Y
            && Rotation == other.Rotation && ScaleX == other.ScaleX && ScaleY == other.ScaleY
            && Anchor.X == other.Anchor.X && Anchor.Y == other.Anchor.Y;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is DrawingTransform other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Translation.X, Translation.Y, Rotation, ScaleX, ScaleY, Anchor.X, Anchor.Y);

    /// <summary>Compares two transforms by value.</summary>
    /// <param name="left">The first transform.</param>
    /// <param name="right">The second transform.</param>
    /// <returns><c>true</c> when both are null or both hold the same values.</returns>
    public static bool operator ==(DrawingTransform left, DrawingTransform right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>Compares two transforms by value.</summary>
    /// <param name="left">The first transform.</param>
    /// <param name="right">The second transform.</param>
    /// <returns><c>true</c> when they differ.</returns>
    public static bool operator !=(DrawingTransform left, DrawingTransform right) => !(left == right);

    /// <inheritdoc />
    public override string ToString()
        => $"Translation ({Translation.X}, {Translation.Y}), Rotation {Rotation}, Scale ({ScaleX}, {ScaleY}), Anchor ({Anchor.X}, {Anchor.Y})";

    //Applies the transform to a canvas that draws in the content's calibrated coordinates, as
    //  the same sequence the matrix describes (skipping steps that change nothing)
    internal void ApplyTo(SKCanvas canvas)
    {
        if (IsIdentity) { return; }
        canvas.Translate(Anchor.X + Translation.X, Anchor.Y + Translation.Y);
        if (Rotation != 0f) { canvas.RotateDegrees(Rotation); }
        if (ScaleX != 1f || ScaleY != 1f) { canvas.Scale(ScaleX, ScaleY); }
        if (Anchor.X != 0f || Anchor.Y != 0f) { canvas.Translate(-Anchor.X, -Anchor.Y); }
    }

    //The axis-aligned box of a rectangle's four corners through a matrix
    internal static RectangleF TransformBounds(RectangleF bounds, Matrix3x2 matrix)
    {
        Vector2 a = Vector2.Transform(new Vector2(bounds.Left, bounds.Top), matrix);
        Vector2 b = Vector2.Transform(new Vector2(bounds.Right, bounds.Top), matrix);
        Vector2 c = Vector2.Transform(new Vector2(bounds.Left, bounds.Bottom), matrix);
        Vector2 d = Vector2.Transform(new Vector2(bounds.Right, bounds.Bottom), matrix);
        float left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
        float top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        float right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
        float bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        return new RectangleF(left, top, right - left, bottom - top);
    }
}
