// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the cap style applied to the start and end of stroked lines.
// </summary>
internal enum SKStrokeCap
{
    // <summary>No extension beyond the endpoint.</summary>
    Butt = 0,
    // <summary>A semicircle is added at the endpoint.</summary>
    Round = 1,
    // <summary>A half-square is added at the endpoint.</summary>
    Square = 2
}
