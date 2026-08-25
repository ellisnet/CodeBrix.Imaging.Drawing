// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Represents a point in 3D space with single-precision floating-point coordinates.
// </summary>
internal readonly struct SKPoint3
{
    // <summary>Gets the X coordinate.</summary>
    public float X { get; }

    // <summary>Gets the Y coordinate.</summary>
    public float Y { get; }

    // <summary>Gets the Z coordinate.</summary>
    public float Z { get; }

    // <summary>An empty point at the origin.</summary>
    public static readonly SKPoint3 Empty;

    // <summary>Gets a value indicating whether all coordinates are zero.</summary>
    public readonly bool IsEmpty => X == default && Y == default && Z == default;

    // <summary>
    // Initializes a new <see cref="SKPoint3"/> with the specified coordinates.
    // </summary>
    // <param name="x">The X coordinate.</param>
    // <param name="y">The Y coordinate.</param>
    // <param name="z">The Z coordinate.</param>
    public SKPoint3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    // <inheritdoc />
    public override string ToString()
        => FormattableString.Invariant($"{X}, {Y}, {Z}");
}
