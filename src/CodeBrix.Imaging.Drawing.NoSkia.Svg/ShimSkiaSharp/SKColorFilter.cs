// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>Represents an abstract color filter that can be applied to paint operations.</summary>
public abstract record SKColorFilter : IDeepCloneable<SKColorFilter>
{
    /// <summary>Creates a color filter that applies a color matrix transformation.</summary>
    /// <param name="matrix">The color matrix values.</param>
    /// <returns>A new <see cref="SKColorFilter"/> instance.</returns>
    public static SKColorFilter CreateColorMatrix(float[] matrix)
        => new ColorMatrixColorFilter(matrix);

    /// <summary>Creates a color filter using lookup tables for each color channel.</summary>
    /// <param name="tableA">The lookup table for the alpha channel.</param>
    /// <param name="tableR">The lookup table for the red channel.</param>
    /// <param name="tableG">The lookup table for the green channel.</param>
    /// <param name="tableB">The lookup table for the blue channel.</param>
    /// <returns>A new <see cref="SKColorFilter"/> instance.</returns>
    public static SKColorFilter CreateTable(byte[] tableA, byte[] tableR, byte[] tableG, byte[] tableB)
        => new TableColorFilter(tableA, tableR, tableG, tableB);

    /// <summary>Creates a color filter that applies a blend mode with the specified color.</summary>
    /// <param name="c">The color to blend.</param>
    /// <param name="mode">The blend mode to apply.</param>
    /// <returns>A new <see cref="SKColorFilter"/> instance.</returns>
    public static SKColorFilter CreateBlendMode(SKColor c, SKBlendMode mode)
        => new BlendModeColorFilter(c, mode);

    /// <summary>Creates a color filter that converts colors to their luminance values.</summary>
    /// <returns>A new <see cref="SKColorFilter"/> instance.</returns>
    public static SKColorFilter CreateLumaColor()
        => new LumaColorColorFilter();

    /// <inheritdoc />
    public SKColorFilter DeepClone() => DeepClone(new CloneContext());

    internal SKColorFilter DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKColorFilter existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            SKColorFilter clone = this switch
            {
                BlendModeColorFilter blendModeColorFilter => new BlendModeColorFilter(blendModeColorFilter.Color, blendModeColorFilter.Mode),
                ColorMatrixColorFilter colorMatrixColorFilter => new ColorMatrixColorFilter(CloneHelpers.CloneArray(colorMatrixColorFilter.Matrix, context)),
                LumaColorColorFilter => new LumaColorColorFilter(),
                TableColorFilter tableColorFilter => new TableColorFilter(CloneHelpers.CloneArray(tableColorFilter.TableA, context), CloneHelpers.CloneArray(tableColorFilter.TableR, context), CloneHelpers.CloneArray(tableColorFilter.TableG, context), CloneHelpers.CloneArray(tableColorFilter.TableB, context)),
                _ => throw new NotSupportedException($"Unsupported {nameof(SKColorFilter)} type: {GetType().Name}.")
            };

            context.Add(this, clone);
            return clone;
        }
        finally
        {
            context.Exit(this);
        }
    }
}

/// <summary>A color filter that blends a color using a specified blend mode.</summary>
/// <param name="Color">The color to blend.</param>
/// <param name="Mode">The blend mode to apply.</param>
public record BlendModeColorFilter(SKColor Color, SKBlendMode Mode) : SKColorFilter;

/// <summary>A color filter that applies a color matrix transformation.</summary>
/// <param name="Matrix">The color matrix values.</param>
public record ColorMatrixColorFilter(float[] Matrix) : SKColorFilter;

/// <summary>A color filter that converts colors to their luminance values.</summary>
public record LumaColorColorFilter : SKColorFilter;

/// <summary>A color filter using lookup tables for each color channel.</summary>
/// <param name="TableA">The lookup table for the alpha channel.</param>
/// <param name="TableR">The lookup table for the red channel.</param>
/// <param name="TableG">The lookup table for the green channel.</param>
/// <param name="TableB">The lookup table for the blue channel.</param>
public record TableColorFilter(byte[] TableA, byte[] TableR, byte[] TableG, byte[] TableB) : SKColorFilter;
