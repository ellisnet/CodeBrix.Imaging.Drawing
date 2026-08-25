// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the color space used for color interpretation.
// </summary>
internal enum SKColorSpace
{
    // <summary>The standard sRGB color space.</summary>
    Srgb = 0,
    // <summary>The linear sRGB color space.</summary>
    SrgbLinear = 1
}
