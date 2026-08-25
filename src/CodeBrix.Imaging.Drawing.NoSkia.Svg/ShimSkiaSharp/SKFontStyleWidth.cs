// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the width (stretch) of a font.
// </summary>
internal enum SKFontStyleWidth
{
    // <summary>Ultra-condensed width.</summary>
    UltraCondensed = 1,
    // <summary>Extra-condensed width.</summary>
    ExtraCondensed = 2,
    // <summary>Condensed width.</summary>
    Condensed = 3,
    // <summary>Semi-condensed width.</summary>
    SemiCondensed = 4,
    // <summary>Normal width.</summary>
    Normal = 5,
    // <summary>Semi-expanded width.</summary>
    SemiExpanded = 6,
    // <summary>Expanded width.</summary>
    Expanded = 7,
    // <summary>Extra-expanded width.</summary>
    ExtraExpanded = 8,
    // <summary>Ultra-expanded width.</summary>
    UltraExpanded = 9
}
