// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies the winding direction for path construction.
/// </summary>
public enum SKPathDirection
{
    /// <summary>Clockwise direction.</summary>
    Clockwise = 0,
    /// <summary>Counter-clockwise direction.</summary>
    CounterClockwise = 1
}
