// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the slant style of a font.
// </summary>
internal enum SKFontStyleSlant
{
    // <summary>Upright (roman) style.</summary>
    Upright = 0,
    // <summary>Italic style.</summary>
    Italic = 1,
    // <summary>Oblique (slanted) style.</summary>
    Oblique = 2
}
