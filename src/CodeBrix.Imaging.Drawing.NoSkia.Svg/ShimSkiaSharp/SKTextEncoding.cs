// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Specifies the encoding used for text data.
/// </summary>
public enum SKTextEncoding
{
    /// <summary>UTF-8 encoding.</summary>
    Utf8 = 0,
    /// <summary>UTF-16 encoding.</summary>
    Utf16 = 1,
    /// <summary>UTF-32 encoding.</summary>
    Utf32 = 2,
    /// <summary>Glyph identifier encoding.</summary>
    GlyphId = 3
}
