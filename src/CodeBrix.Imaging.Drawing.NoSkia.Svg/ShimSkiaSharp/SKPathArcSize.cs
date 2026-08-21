// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies whether the arc sweep is the smaller or larger arc.
/// </summary>
public enum SKPathArcSize
{
    /// <summary>The smaller of the two possible arcs.</summary>
    Small = 0,
    /// <summary>The larger of the two possible arcs.</summary>
    Large = 1
}
