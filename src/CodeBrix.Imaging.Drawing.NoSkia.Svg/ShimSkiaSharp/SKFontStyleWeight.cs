// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the weight (boldness) of a font.
// </summary>
internal enum SKFontStyleWeight
{
    // <summary>Weight 0 – invisible.</summary>
    Invisible = 0,
    // <summary>Weight 100 – thin.</summary>
    Thin = 100,
    // <summary>Weight 200 – extra light.</summary>
    ExtraLight = 200,
    // <summary>Weight 300 – light.</summary>
    Light = 300,
    // <summary>Weight 400 – normal (regular).</summary>
    Normal = 400,
    // <summary>Weight 500 – medium.</summary>
    Medium = 500,
    // <summary>Weight 600 – semi-bold.</summary>
    SemiBold = 600,
    // <summary>Weight 700 – bold.</summary>
    Bold = 700,
    // <summary>Weight 800 – extra bold.</summary>
    ExtraBold = 800,
    // <summary>Weight 900 – black (heavy).</summary>
    Black = 900,
    // <summary>Weight 1000 – extra black.</summary>
    ExtraBlack = 1000
}
