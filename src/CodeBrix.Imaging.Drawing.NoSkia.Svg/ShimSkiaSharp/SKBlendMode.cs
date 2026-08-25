// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Specifies the Porter-Duff or advanced blend mode used when compositing source and destination pixels.
// </summary>
internal enum SKBlendMode
{
    // <summary>Replaces destination with zero (fully transparent).</summary>
    Clear = 0,
    // <summary>Replaces destination with source.</summary>
    Src = 1,
    // <summary>Preserves destination, ignoring source.</summary>
    Dst = 2,
    // <summary>Source over destination.</summary>
    SrcOver = 3,
    // <summary>Destination over source.</summary>
    DstOver = 4,
    // <summary>Source clipped to destination alpha.</summary>
    SrcIn = 5,
    // <summary>Destination clipped to source alpha.</summary>
    DstIn = 6,
    // <summary>Source where destination is transparent.</summary>
    SrcOut = 7,
    // <summary>Destination where source is transparent.</summary>
    DstOut = 8,
    // <summary>Source atop destination.</summary>
    SrcATop = 9,
    // <summary>Destination atop source.</summary>
    DstATop = 10,
    // <summary>Exclusive-or of source and destination.</summary>
    Xor = 11,
    // <summary>Sum of source and destination (clamped).</summary>
    Plus = 12,
    // <summary>Product of source and destination.</summary>
    Modulate = 13,
    // <summary>Screen blend mode.</summary>
    Screen = 14,
    // <summary>Overlay blend mode.</summary>
    Overlay = 15,
    // <summary>Selects the darker of source and destination.</summary>
    Darken = 16,
    // <summary>Selects the lighter of source and destination.</summary>
    Lighten = 17,
    // <summary>Brightens destination to reflect source.</summary>
    ColorDodge = 18,
    // <summary>Darkens destination to reflect source.</summary>
    ColorBurn = 19,
    // <summary>Hard-light blend mode.</summary>
    HardLight = 20,
    // <summary>Soft-light blend mode.</summary>
    SoftLight = 21,
    // <summary>Absolute difference of source and destination.</summary>
    Difference = 22,
    // <summary>Similar to Difference but lower contrast.</summary>
    Exclusion = 23,
    // <summary>Multiplies source and destination.</summary>
    Multiply = 24,
    // <summary>Uses hue of source with saturation and luminosity of destination.</summary>
    Hue = 25,
    // <summary>Uses saturation of source with hue and luminosity of destination.</summary>
    Saturation = 26,
    // <summary>Uses hue and saturation of source with luminosity of destination.</summary>
    Color = 27,
    // <summary>Uses luminosity of source with hue and saturation of destination.</summary>
    Luminosity = 28
}
