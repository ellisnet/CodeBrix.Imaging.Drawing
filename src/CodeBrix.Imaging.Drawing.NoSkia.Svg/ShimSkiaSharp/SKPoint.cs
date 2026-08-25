// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Represents a point in 2D space with single-precision floating-point coordinates.
// </summary>
internal readonly struct SKPoint
{
    // <summary>Gets the X coordinate.</summary>
    public float X { get; }

    // <summary>Gets the Y coordinate.</summary>
    public float Y { get; }

    // <summary>An empty point at the origin.</summary>
    public static readonly SKPoint Empty = default;

    // <summary>Gets a value indicating whether both coordinates are zero.</summary>
    public readonly bool IsEmpty => X == default && Y == default;

    // <summary>
    // Initializes a new <see cref="SKPoint"/> with the specified coordinates.
    // </summary>
    // <param name="x">The X coordinate.</param>
    // <param name="y">The Y coordinate.</param>
    public SKPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    // <inheritdoc />
    public override string ToString()
        => FormattableString.Invariant($"{X}, {Y}");
}
