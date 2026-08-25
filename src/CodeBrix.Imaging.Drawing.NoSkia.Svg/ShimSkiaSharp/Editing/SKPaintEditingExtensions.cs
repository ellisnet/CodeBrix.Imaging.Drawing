// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp.Editing; //Was previously: namespace ShimSkiaSharp.Editing;

// <summary>
// Provides extension methods for editing <see cref="SKPaint"/> properties.
// </summary>
internal static class SKPaintEditingExtensions
{
    // <summary>
    // Applies a color transformation to the paint's color value.
    // </summary>
    // <param name="paint">The paint to modify.</param>
    // <param name="transform">A function that transforms the current color to a new color.</param>
    public static void ApplyColorTransform(this SKPaint paint, Func<SKColor, SKColor> transform)
    {
        if (paint is null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        if (paint.Color is { } color)
        {
            paint.Color = transform(color);
        }
    }

    // <summary>
    // Applies a shader transformation to the paint's shader value.
    // </summary>
    // <param name="paint">The paint to modify.</param>
    // <param name="transform">A function that transforms the current shader to a new shader.</param>
    public static void ApplyShaderTransform(this SKPaint paint, Func<SKShader, SKShader> transform)
    {
        if (paint is null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        if (paint.Shader is { } shader)
        {
            paint.Shader = transform(shader);
        }
    }
}
