// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies horizontal text alignment.
/// </summary>
public enum SKTextAlign
{
    /// <summary>Align text to the left.</summary>
    Left = 0,
    /// <summary>Center the text.</summary>
    Center = 1,
    /// <summary>Align text to the right.</summary>
    Right = 2
}
