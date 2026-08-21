// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies the rule used to determine the interior of a path.
/// </summary>
public enum SKPathFillType
{
    /// <summary>Uses the non-zero winding rule.</summary>
    Winding = 0,
    /// <summary>Uses the even-odd rule.</summary>
    EvenOdd = 1
}
