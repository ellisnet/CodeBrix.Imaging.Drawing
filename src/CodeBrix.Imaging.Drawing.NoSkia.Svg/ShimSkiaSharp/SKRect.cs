// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Represents an axis-aligned rectangle defined by left, top, right, and bottom edges.
/// </summary>
public struct SKRect
{
    /// <summary>Gets or sets the left edge.</summary>
    public float Left { get; set; }

    /// <summary>Gets or sets the top edge.</summary>
    public float Top { get; set; }

    /// <summary>Gets or sets the right edge.</summary>
    public float Right { get; set; }

    /// <summary>Gets or sets the bottom edge.</summary>
    public float Bottom { get; set; }

    /// <summary>Gets the top-left corner.</summary>
    public SKPoint TopLeft => new(Left, Top);

    /// <summary>Gets the top-right corner.</summary>
    public SKPoint TopRight => new(Right, Top);

    /// <summary>Gets the bottom-left corner.</summary>
    public SKPoint BottomLeft => new(Left, Bottom);

    /// <summary>Gets the bottom-right corner.</summary>
    public SKPoint BottomRight => new(Right, Bottom);

    /// <summary>An empty rectangle.</summary>
    public static readonly SKRect Empty = default;

    /// <summary>Gets a value indicating whether the rectangle is empty.</summary>
    public readonly bool IsEmpty => Left == default && Top == default && Right == default && Bottom == default;

    /// <summary>Gets the width of the rectangle.</summary>
    public readonly float Width => Right - Left;

    /// <summary>Gets the height of the rectangle.</summary>
    public readonly float Height => Bottom - Top;

    /// <summary>Gets the size of the rectangle.</summary>
    public readonly SKSize Size => new(Width, Height);

    /// <summary>Gets the top-left location of the rectangle.</summary>
    public readonly SKPoint Location => new(Left, Top);

    /// <summary>
    /// Initializes a new <see cref="SKRect"/> with the specified edges.
    /// </summary>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    public SKRect(float left, float top, float right, float bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    /// <summary>
    /// Creates a rectangle from position and size.
    /// </summary>
    /// <param name="x">The X coordinate of the top-left corner.</param>
    /// <param name="y">The Y coordinate of the top-left corner.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new <see cref="SKRect"/>.</returns>
    public static SKRect Create(float x, float y, float width, float height)
    {
        return new()
        {
            Left = x,
            Top = y,
            Right = x + width,
            Bottom = y + height
        };
    }

    /// <summary>
    /// Creates a rectangle at the origin with the specified size.
    /// </summary>
    /// <param name="size">The size of the rectangle.</param>
    /// <returns>A new <see cref="SKRect"/>.</returns>
    public static SKRect Create(SKSize size)
    {
        return Create(0, 0, size.Width, size.Height);
    }

    /// <summary>
    /// Determines whether this rectangle contains the specified point.
    /// </summary>
    /// <param name="p">The point to test.</param>
    /// <returns><c>true</c> if the point is inside the rectangle; otherwise, <c>false</c>.</returns>
    public bool Contains(SKPoint p)
    {
        return p.X >= Left && p.X <= Left + Width &&
               p.Y >= Top && p.Y <= Top + Height;
    }

    /// <summary>
    /// Determines whether this rectangle fully contains the specified rectangle.
    /// </summary>
    /// <param name="r">The rectangle to test.</param>
    /// <returns><c>true</c> if <paramref name="r"/> is fully contained; otherwise, <c>false</c>.</returns>
    public bool Contains(SKRect r)
    {
        return Contains(r.TopLeft) && Contains(r.BottomRight);
    }

    /// <summary>
    /// Returns the smallest rectangle that contains both specified rectangles.
    /// </summary>
    /// <param name="a">The first rectangle.</param>
    /// <param name="b">The second rectangle.</param>
    /// <returns>The union rectangle.</returns>
    public static SKRect Union(SKRect a, SKRect b)
    {
        return new(
            Math.Min(a.Left, b.Left),
            Math.Min(a.Top, b.Top),
            Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));
    }

    /// <inheritdoc />
    public override string ToString()
        => FormattableString.Invariant($"{Left}, {Top}, {Width}, {Height}");
}
