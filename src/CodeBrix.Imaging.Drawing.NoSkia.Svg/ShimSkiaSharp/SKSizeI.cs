// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Represents a size with integer dimensions.
/// </summary>
public readonly struct SKSizeI
{
    /// <summary>Gets the width.</summary>
    public int Width { get; }

    /// <summary>Gets the height.</summary>
    public int Height { get; }

    /// <summary>An empty size with zero dimensions.</summary>
    public static readonly SKSizeI Empty;

    /// <summary>Gets a value indicating whether both dimensions are zero.</summary>
    public readonly bool IsEmpty => Width == default && Height == default;

    /// <summary>
    /// Initializes a new <see cref="SKSizeI"/> with the specified dimensions.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public SKSizeI(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <inheritdoc />
    public override string ToString()
        => FormattableString.Invariant($"{Width}, {Height}");
}
