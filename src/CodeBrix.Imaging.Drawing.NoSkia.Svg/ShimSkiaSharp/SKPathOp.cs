// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the boolean operation to apply when combining two paths.
// </summary>
internal enum SKPathOp
{
    // <summary>Subtract the second path from the first.</summary>
    Difference = 0,
    // <summary>Keep only the area shared by both paths.</summary>
    Intersect = 1,
    // <summary>Keep the area covered by either path.</summary>
    Union = 2,
    // <summary>Keep the area covered by exactly one path.</summary>
    Xor = 3,
    // <summary>Subtract the first path from the second.</summary>
    ReverseDifference = 4
}
