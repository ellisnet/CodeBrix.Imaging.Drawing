// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Identifies a single channel of a color value.
/// </summary>
public enum SKColorChannel
{
    /// <summary>The red channel.</summary>
    R = 0,
    /// <summary>The green channel.</summary>
    G = 1,
    /// <summary>The blue channel.</summary>
    B = 2,
    /// <summary>The alpha (transparency) channel.</summary>
    A = 3
}
