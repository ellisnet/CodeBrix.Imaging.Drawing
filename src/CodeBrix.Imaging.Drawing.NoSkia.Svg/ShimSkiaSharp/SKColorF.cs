// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>
// Represents a color with single-precision floating-point RGBA components.
// </summary>
internal readonly struct SKColorF
{
    // <summary>Gets the red component (0.0–1.0).</summary>
    public float Red { get; }
    // <summary>Gets the green component (0.0–1.0).</summary>
    public float Green { get; }
    // <summary>Gets the blue component (0.0–1.0).</summary>
    public float Blue { get; }
    // <summary>Gets the alpha component (0.0–1.0).</summary>
    public float Alpha { get; }

    // <summary>A default (transparent black) color.</summary>
    public static readonly SKColorF Empty = default;

    // <summary>
    // Initializes a new <see cref="SKColorF"/> with the specified RGBA components.
    // </summary>
    // <param name="red">The red component.</param>
    // <param name="green">The green component.</param>
    // <param name="blue">The blue component.</param>
    // <param name="alpha">The alpha component.</param>
    public SKColorF(float red, float green, float blue, float alpha)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    // <summary>Converts an <see cref="SKColorF"/> to an <see cref="SKColor"/>.</summary>
    // <param name="color">The color to convert.</param>
    public static implicit operator SKColor(SKColorF color)
    {
        return new(
            (byte)(color.Red * 255.0f),
            (byte)(color.Green * 255.0f),
            (byte)(color.Blue * 255.0f),
            (byte)(color.Alpha * 255.0f));
    }

    // <inheritdoc />
    public override string ToString()
        => FormattableString.Invariant($"{Red}, {Green}, {Blue}, {Alpha}");
}
