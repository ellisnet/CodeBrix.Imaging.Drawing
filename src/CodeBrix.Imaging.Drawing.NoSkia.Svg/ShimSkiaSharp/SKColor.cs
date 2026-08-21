// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>
/// Represents a color with 8-bit RGBA components.
/// </summary>
public readonly struct SKColor
{
    /// <summary>Gets the red component (0–255).</summary>
    public byte Red { get; }

    /// <summary>Gets the green component (0–255).</summary>
    public byte Green { get; }

    /// <summary>Gets the blue component (0–255).</summary>
    public byte Blue { get; }

    /// <summary>Gets the alpha component (0–255).</summary>
    public byte Alpha { get; }

    /// <summary>A default (transparent black) color.</summary>
    public static readonly SKColor Empty = default;

    /// <summary>
    /// Initializes a new <see cref="SKColor"/> with the specified RGBA components.
    /// </summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    /// <param name="alpha">The alpha component.</param>
    public SKColor(byte red, byte green, byte blue, byte alpha)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    /// <summary>Converts an <see cref="SKColor"/> to an <see cref="SKColorF"/>.</summary>
    /// <param name="color">The color to convert.</param>
    public static implicit operator SKColorF(SKColor color)
    {
        return new(
            color.Red * (1 / 255.0f),
            color.Green * (1 / 255.0f),
            color.Blue * (1 / 255.0f),
            color.Alpha * (1 / 255.0f));
    }

    /// <inheritdoc />
    public override string ToString()
        => FormattableString.Invariant($"{Red}, {Green}, {Blue}, {Alpha}");
}
