// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Represents a 3×3 affine transformation matrix.
/// </summary>
public struct SKMatrix : IEquatable<SKMatrix>
{
    /// <summary>Gets or sets the horizontal scale factor.</summary>
    public float ScaleX { get; set; }

    /// <summary>Gets or sets the horizontal skew factor.</summary>
    public float SkewX { get; set; }

    /// <summary>Gets or sets the horizontal translation.</summary>
    public float TransX { get; set; }

    /// <summary>Gets or sets the vertical scale factor.</summary>
    public float ScaleY { get; set; }

    /// <summary>Gets or sets the vertical skew factor.</summary>
    public float SkewY { get; set; }

    /// <summary>Gets or sets the vertical translation.</summary>
    public float TransY { get; set; }

    /// <summary>Gets or sets the first perspective value.</summary>
    public float Persp0 { get; set; }

    /// <summary>Gets or sets the second perspective value.</summary>
    public float Persp1 { get; set; }

    /// <summary>Gets or sets the third perspective value.</summary>
    public float Persp2 { get; set; }

    internal const float DegreesToRadians = (float)Math.PI / 180.0f;

    /// <summary>An empty (zero) matrix.</summary>
    public static readonly SKMatrix Empty;

    /// <summary>The identity matrix.</summary>
    public static readonly SKMatrix Identity = new() { ScaleX = 1, ScaleY = 1, Persp2 = 1 };

    /// <summary>Gets a value indicating whether this matrix is the identity matrix.</summary>
    public bool IsIdentity => Equals(Identity);

    /// <summary>Creates a new identity matrix.</summary>
    /// <returns>The identity matrix.</returns>
    public static SKMatrix CreateIdentity()
    {
        return new() { ScaleX = 1, ScaleY = 1, Persp2 = 1 };
    }

    /// <summary>
    /// Creates a translation matrix.
    /// </summary>
    /// <param name="x">The horizontal translation.</param>
    /// <param name="y">The vertical translation.</param>
    /// <returns>A translation matrix.</returns>
    public static SKMatrix CreateTranslation(float x, float y)
    {
        if (x == 0 && y == 0)
        {
            return Identity;
        }

        return new SKMatrix
        {
            ScaleX = 1,
            ScaleY = 1,
            TransX = x,
            TransY = y,
            Persp2 = 1,
        };
    }

    /// <summary>
    /// Creates a scaling matrix.
    /// </summary>
    /// <param name="x">The horizontal scale factor.</param>
    /// <param name="y">The vertical scale factor.</param>
    /// <returns>A scaling matrix.</returns>
    public static SKMatrix CreateScale(float x, float y)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (x == 1 && y == 1)
        {
            return Identity;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        return new SKMatrix
        {
            ScaleX = x,
            ScaleY = y,
            Persp2 = 1,
        };
    }

    /// <summary>
    /// Creates a scaling matrix centered on a pivot point.
    /// </summary>
    /// <param name="x">The horizontal scale factor.</param>
    /// <param name="y">The vertical scale factor.</param>
    /// <param name="pivotX">The X coordinate of the pivot point.</param>
    /// <param name="pivotY">The Y coordinate of the pivot point.</param>
    /// <returns>A scaling matrix.</returns>
    public static SKMatrix CreateScale(float x, float y, float pivotX, float pivotY)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (x == 1 && y == 1)
        {
            return Identity;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        var tx = pivotX - x * pivotX;
        var ty = pivotY - y * pivotY;

        return new SKMatrix
        {
            ScaleX = x,
            ScaleY = y,
            TransX = tx,
            TransY = ty,
            Persp2 = 1,
        };
    }

    /// <summary>
    /// Creates a rotation matrix.
    /// </summary>
    /// <param name="radians">The rotation angle in radians.</param>
    /// <returns>A rotation matrix.</returns>
    public static SKMatrix CreateRotation(float radians)
    {
        if (radians == 0)
        {
            return Identity;
        }

        var sin = (float)Math.Sin(radians);
        var cos = (float)Math.Cos(radians);

        var matrix = Identity;
        SetSinCos(ref matrix, sin, cos);
        return matrix;
    }

    /// <summary>
    /// Creates a rotation matrix centered on a pivot point.
    /// </summary>
    /// <param name="radians">The rotation angle in radians.</param>
    /// <param name="pivotX">The X coordinate of the pivot point.</param>
    /// <param name="pivotY">The Y coordinate of the pivot point.</param>
    /// <returns>A rotation matrix.</returns>
    public static SKMatrix CreateRotation(float radians, float pivotX, float pivotY)
    {
        if (radians == 0)
        {
            return Identity;
        }

        var sin = (float)Math.Sin(radians);
        var cos = (float)Math.Cos(radians);

        var matrix = Identity;
        SetSinCos(ref matrix, sin, cos, pivotX, pivotY);
        return matrix;
    }

    /// <summary>
    /// Creates a rotation matrix from an angle in degrees.
    /// </summary>
    /// <param name="degrees">The rotation angle in degrees.</param>
    /// <returns>A rotation matrix.</returns>
    public static SKMatrix CreateRotationDegrees(float degrees)
    {
        if (degrees == 0)
        {
            return Identity;
        }

        return CreateRotation(degrees * DegreesToRadians);
    }

    /// <summary>
    /// Creates a rotation matrix from an angle in degrees, centered on a pivot point.
    /// </summary>
    /// <param name="degrees">The rotation angle in degrees.</param>
    /// <param name="pivotX">The X coordinate of the pivot point.</param>
    /// <param name="pivotY">The Y coordinate of the pivot point.</param>
    /// <returns>A rotation matrix.</returns>
    public static SKMatrix CreateRotationDegrees(float degrees, float pivotX, float pivotY)
    {
        if (degrees == 0)
        {
            return Identity;
        }

        return CreateRotation(degrees * DegreesToRadians, pivotX, pivotY);
    }

    /// <summary>
    /// Creates a skew (shear) matrix.
    /// </summary>
    /// <param name="x">The horizontal skew factor.</param>
    /// <param name="y">The vertical skew factor.</param>
    /// <returns>A skew matrix.</returns>
    public static SKMatrix CreateSkew(float x, float y)
    {
        if (x == 0 && y == 0)
        {
            return Identity;
        }

        return new SKMatrix
        {
            ScaleX = 1,
            SkewX = x,
            SkewY = y,
            ScaleY = 1,
            Persp2 = 1,
        };
    }

    private static void SetSinCos(ref SKMatrix matrix, float sin, float cos)
    {
        matrix.ScaleX = cos;
        matrix.SkewX = -sin;
        matrix.TransX = 0;
        matrix.SkewY = sin;
        matrix.ScaleY = cos;
        matrix.TransY = 0;
        matrix.Persp0 = 0;
        matrix.Persp1 = 0;
        matrix.Persp2 = 1;
    }

    private static void SetSinCos(ref SKMatrix matrix, float sin, float cos, float pivotx, float pivoty)
    {
        var oneMinusCos = 1 - cos;
        matrix.ScaleX = cos;
        matrix.SkewX = -sin;
        matrix.TransX = Dot(sin, pivoty, oneMinusCos, pivotx);
        matrix.SkewY = sin;
        matrix.ScaleY = cos;
        matrix.TransY = Dot(-sin, pivotx, oneMinusCos, pivoty);
        matrix.Persp0 = 0;
        matrix.Persp1 = 0;
        matrix.Persp2 = 1;
    }

    private static float Dot(float a, float b, float c, float d)
    {
        return a * b + c * d;
    }

    private static float MulAddMul(float a, float b, float c, float d)
    {
        return (float)(a * (double)b + c * (double)d);
    }

    private static SKMatrix Concat(SKMatrix a, SKMatrix b)
    {
        return new()
        {
            ScaleX = MulAddMul(a.ScaleX, b.ScaleX, a.SkewX, b.SkewY),
            SkewX = MulAddMul(a.ScaleX, b.SkewX, a.SkewX, b.ScaleY),
            TransX = MulAddMul(a.ScaleX, b.TransX, a.SkewX, b.TransY) + a.TransX,
            SkewY = MulAddMul(a.SkewY, b.ScaleX, a.ScaleY, b.SkewY),
            ScaleY = MulAddMul(a.SkewY, b.SkewX, a.ScaleY, b.ScaleY),
            TransY = MulAddMul(a.SkewY, b.TransX, a.ScaleY, b.TransY) + a.TransY,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }

    /// <summary>
    /// Initializes a new <see cref="SKMatrix"/> with all nine matrix values.
    /// </summary>
    /// <param name="scaleX">The horizontal scale factor.</param>
    /// <param name="skewX">The horizontal skew factor.</param>
    /// <param name="transX">The horizontal translation.</param>
    /// <param name="skewY">The vertical skew factor.</param>
    /// <param name="scaleY">The vertical scale factor.</param>
    /// <param name="transY">The vertical translation.</param>
    /// <param name="persp0">The first perspective value.</param>
    /// <param name="persp1">The second perspective value.</param>
    /// <param name="persp2">The third perspective value.</param>
    public SKMatrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY, float persp0, float persp1, float persp2)
    {
        ScaleX = scaleX;
        SkewX = skewX;
        TransX = transX;
        SkewY = skewY;
        ScaleY = scaleY;
        TransY = transY;
        Persp0 = persp0;
        Persp1 = persp1;
        Persp2 = persp2;
    }

    /// <summary>
    /// Returns the result of multiplying this matrix by <paramref name="matrix"/> (this × matrix).
    /// </summary>
    /// <param name="matrix">The matrix to concatenate.</param>
    /// <returns>The concatenated matrix.</returns>
    public readonly SKMatrix PreConcat(SKMatrix matrix)
    {
        return Concat(this, matrix);
    }

    /// <summary>
    /// Returns the result of multiplying <paramref name="matrix"/> by this matrix (matrix × this).
    /// </summary>
    /// <param name="matrix">The matrix to post-concatenate.</param>
    /// <returns>The concatenated matrix.</returns>
    public readonly SKMatrix PostConcat(SKMatrix matrix)
    {
        return Concat(matrix, this);
    }

    /// <summary>
    /// Maps a rectangle through this transformation matrix.
    /// </summary>
    /// <param name="source">The source rectangle.</param>
    /// <returns>The transformed bounding rectangle.</returns>
    public readonly SKRect MapRect(SKRect source)
    {
        var tl = MapPoint(new SKPoint(source.Left, source.Top));
        var tr = MapPoint(new SKPoint(source.Right, source.Top));
        var br = MapPoint(new SKPoint(source.Right, source.Bottom));
        var bl = MapPoint(new SKPoint(source.Left, source.Bottom));

        var left = Math.Min(Math.Min(tl.X, tr.X), Math.Min(br.X, bl.X));
        var top = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(br.Y, bl.Y));
        var right = Math.Max(Math.Max(tl.X, tr.X), Math.Max(br.X, bl.X));
        var bottom = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(br.Y, bl.Y));

        return new SKRect(left, top, right, bottom);
    }

    /// <summary>
    /// Maps a rectangle through this transformation matrix, modifying it in place.
    /// </summary>
    /// <param name="rect">The rectangle to transform.</param>
    public void MapRect(ref SKRect rect)
    {
        rect = MapRect(rect);
    }

    /// <summary>
    /// Maps a point through this transformation matrix.
    /// </summary>
    /// <param name="source">The source point.</param>
    /// <returns>The transformed point.</returns>
    public readonly SKPoint MapPoint(SKPoint source)
    {
        return new SKPoint(
            source.X * ScaleX + source.Y * SkewX + TransX,
            source.X * SkewY + source.Y * ScaleY + TransY);
    }

    /// <summary>
    /// Attempts to compute the inverse of this matrix.
    /// </summary>
    /// <param name="inverse">When this method returns, contains the inverse matrix if successful.</param>
    /// <returns><c>true</c> if the matrix is invertible; otherwise, <c>false</c>.</returns>
    public bool TryInvert(out SKMatrix inverse)
    {
        var det = ScaleX * ScaleY - SkewX * SkewY;
        if (det == 0)
        {
            inverse = Identity;
            return false;
        }

        var invDet = 1f / det;

        inverse = new SKMatrix
        {
            ScaleX = ScaleY * invDet,
            SkewX = -SkewX * invDet,
            TransX = (SkewX * TransY - ScaleY * TransX) * invDet,
            SkewY = -SkewY * invDet,
            ScaleY = ScaleX * invDet,
            TransY = (SkewY * TransX - ScaleX * TransY) * invDet,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };

        return true;
    }

    /// <inheritdoc />
    public bool Equals(SKMatrix other)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        return ScaleX == other.ScaleX &&
               SkewY == other.SkewY &&
               SkewX == other.SkewX &&
               ScaleY == other.ScaleY &&
               TransX == other.TransX &&
               TransY == other.TransY;
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is SKMatrix other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return ScaleX.GetHashCode() + SkewY.GetHashCode() +
               SkewX.GetHashCode() + ScaleY.GetHashCode() +
               TransX.GetHashCode() + TransY.GetHashCode();
    }

    /// <summary>Determines whether two matrices are equal.</summary>
    /// <param name="value1">The first matrix.</param>
    /// <param name="value2">The second matrix.</param>
    /// <returns><c>true</c> if the matrices are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(SKMatrix value1, SKMatrix value2)
    {
        return value1.Equals(value2);
    }

    /// <summary>Determines whether two matrices are not equal.</summary>
    /// <param name="value1">The first matrix.</param>
    /// <param name="value2">The second matrix.</param>
    /// <returns><c>true</c> if the matrices are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(SKMatrix value1, SKMatrix value2)
    {
        return !value1.Equals(value2);
    }

    /// <summary>Multiplies two matrices (pre-concatenation).</summary>
    /// <param name="value1">The first matrix.</param>
    /// <param name="value2">The second matrix.</param>
    /// <returns>The product of the two matrices.</returns>
    public static SKMatrix operator *(SKMatrix value1, SKMatrix value2)
    {
        return value1.PreConcat(value2);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return FormattableString.Invariant($"{ScaleX}, {SkewX}, {TransX}, {SkewY}, {ScaleY}, {TransY}");
    }
}
