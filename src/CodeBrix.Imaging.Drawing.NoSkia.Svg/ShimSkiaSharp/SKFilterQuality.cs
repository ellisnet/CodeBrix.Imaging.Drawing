// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies the level of quality when filtering (scaling/transforming) images.
/// </summary>
public enum SKFilterQuality
{
    /// <summary>Nearest-neighbor filtering (fastest, lowest quality).</summary>
    None = 0,
    /// <summary>Bilinear filtering.</summary>
    Low = 1,
    /// <summary>Bilinear filtering with mipmaps.</summary>
    Medium = 2,
    /// <summary>Bicubic filtering (slowest, highest quality).</summary>
    High = 3
}
