// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies how a shape is drawn (fill, stroke, or both).
/// </summary>
public enum SKPaintStyle
{
    /// <summary>Fill the interior of the shape.</summary>
    Fill = 0,
    /// <summary>Stroke the outline of the shape.</summary>
    Stroke = 1,
    /// <summary>Both stroke the outline and fill the interior.</summary>
    StrokeAndFill = 2
}
