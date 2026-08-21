// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies the operation used when applying a clip.
/// </summary>
public enum SKClipOperation
{
    /// <summary>Subtract the clip region from the current clip.</summary>
    Difference = 0,
    /// <summary>Intersect the clip region with the current clip.</summary>
    Intersect = 1
}
