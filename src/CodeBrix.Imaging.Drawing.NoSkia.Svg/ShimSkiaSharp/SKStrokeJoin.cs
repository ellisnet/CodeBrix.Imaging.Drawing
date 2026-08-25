// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the join style applied at corners of stroked paths.
// </summary>
internal enum SKStrokeJoin
{
    // <summary>Sharp corner join.</summary>
    Miter = 0,
    // <summary>Rounded corner join.</summary>
    Round = 1,
    // <summary>Beveled (flat) corner join.</summary>
    Bevel = 2
}
