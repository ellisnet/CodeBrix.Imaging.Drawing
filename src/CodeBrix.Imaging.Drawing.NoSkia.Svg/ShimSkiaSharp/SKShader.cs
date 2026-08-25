// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

// <summary>Represents an abstract shader used for filling paint operations with colors, gradients, or patterns.</summary>
internal abstract record SKShader : IDeepCloneable<SKShader>
{
    // <summary>Creates a shader that fills with a solid color.</summary>
    // <param name="color">The color to fill with.</param>
    // <param name="colorSpace">The color space of the color.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreateColor(SKColor color, SKColorSpace colorSpace)
        => new ColorShader(color, colorSpace);

    // <summary>Creates a linear gradient shader.</summary>
    // <param name="start">The start point of the gradient.</param>
    // <param name="end">The end point of the gradient.</param>
    // <param name="colors">The gradient colors.</param>
    // <param name="colorSpace">The color space for the gradient colors.</param>
    // <param name="colorPos">The positions of each color along the gradient, or <c>null</c> for even distribution.</param>
    // <param name="mode">The tile mode for the gradient.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode)
        => new LinearGradientShader(start, end, colors, colorSpace, colorPos, mode, null);

    // <summary>Creates a linear gradient shader with a local transformation matrix.</summary>
    // <param name="start">The start point of the gradient.</param>
    // <param name="end">The end point of the gradient.</param>
    // <param name="colors">The gradient colors.</param>
    // <param name="colorSpace">The color space for the gradient colors.</param>
    // <param name="colorPos">The positions of each color along the gradient, or <c>null</c> for even distribution.</param>
    // <param name="mode">The tile mode for the gradient.</param>
    // <param name="localMatrix">The local transformation matrix.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
        => new LinearGradientShader(start, end, colors, colorSpace, colorPos, mode, localMatrix);

    // <summary>Creates a Perlin noise fractal noise shader.</summary>
    // <param name="baseFrequencyX">The base frequency in the X direction.</param>
    // <param name="baseFrequencyY">The base frequency in the Y direction.</param>
    // <param name="numOctaves">The number of octaves.</param>
    // <param name="seed">The random seed value.</param>
    // <param name="tileSize">The tile size for the noise pattern.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize)
        => new PerlinNoiseFractalNoiseShader(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);

    // <summary>Creates a Perlin noise turbulence shader.</summary>
    // <param name="baseFrequencyX">The base frequency in the X direction.</param>
    // <param name="baseFrequencyY">The base frequency in the Y direction.</param>
    // <param name="numOctaves">The number of octaves.</param>
    // <param name="seed">The random seed value.</param>
    // <param name="tileSize">The tile size for the noise pattern.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize)
        => new PerlinNoiseTurbulenceShader(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);

    // <summary>Creates a shader from a recorded picture.</summary>
    // <param name="src">The source picture.</param>
    // <param name="tmx">The tile mode in the X direction.</param>
    // <param name="tmy">The tile mode in the Y direction.</param>
    // <param name="localMatrix">The local transformation matrix.</param>
    // <param name="tile">The tile rectangle.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix, SKRect tile)
        => new PictureShader(src, tmx, tmy, localMatrix, tile);

    // <summary>Creates a radial gradient shader.</summary>
    // <param name="center">The center point of the gradient.</param>
    // <param name="radius">The radius of the gradient.</param>
    // <param name="colors">The gradient colors.</param>
    // <param name="colorSpace">The color space for the gradient colors.</param>
    // <param name="colorPos">The positions of each color along the gradient, or <c>null</c> for even distribution.</param>
    // <param name="mode">The tile mode for the gradient.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode)
        => new RadialGradientShader(center, radius, colors, colorSpace, colorPos, mode, null);

    // <summary>Creates a radial gradient shader with a local transformation matrix.</summary>
    // <param name="center">The center point of the gradient.</param>
    // <param name="radius">The radius of the gradient.</param>
    // <param name="colors">The gradient colors.</param>
    // <param name="colorSpace">The color space for the gradient colors.</param>
    // <param name="colorPos">The positions of each color along the gradient, or <c>null</c> for even distribution.</param>
    // <param name="mode">The tile mode for the gradient.</param>
    // <param name="localMatrix">The local transformation matrix.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
        => new RadialGradientShader(center, radius, colors, colorSpace, colorPos, mode, localMatrix);

    // <summary>Creates a two-point conical gradient shader.</summary>
    // <param name="start">The start point of the gradient.</param>
    // <param name="startRadius">The radius at the start point.</param>
    // <param name="end">The end point of the gradient.</param>
    // <param name="endRadius">The radius at the end point.</param>
    // <param name="colors">The gradient colors.</param>
    // <param name="colorSpace">The color space for the gradient colors.</param>
    // <param name="colorPos">The positions of each color along the gradient, or <c>null</c> for even distribution.</param>
    // <param name="mode">The tile mode for the gradient.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode)
        => new TwoPointConicalGradientShader(start, startRadius, end, endRadius, colors, colorSpace, colorPos, mode, null);

    // <summary>Creates a two-point conical gradient shader with a local transformation matrix.</summary>
    // <param name="start">The start point of the gradient.</param>
    // <param name="startRadius">The radius at the start point.</param>
    // <param name="end">The end point of the gradient.</param>
    // <param name="endRadius">The radius at the end point.</param>
    // <param name="colors">The gradient colors.</param>
    // <param name="colorSpace">The color space for the gradient colors.</param>
    // <param name="colorPos">The positions of each color along the gradient, or <c>null</c> for even distribution.</param>
    // <param name="mode">The tile mode for the gradient.</param>
    // <param name="localMatrix">The local transformation matrix.</param>
    // <returns>A new <see cref="SKShader"/> instance.</returns>
    public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
        => new TwoPointConicalGradientShader(start, startRadius, end, endRadius, colors, colorSpace, colorPos, mode, localMatrix);

    // <inheritdoc />
    public SKShader DeepClone() => DeepClone(new CloneContext());

    internal SKShader DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKShader existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            SKShader clone = this switch
            {
                ColorShader colorShader => new ColorShader(colorShader.Color, colorShader.ColorSpace),
                LinearGradientShader linearGradientShader => new LinearGradientShader(linearGradientShader.Start, linearGradientShader.End, CloneHelpers.CloneArray(linearGradientShader.Colors, context), linearGradientShader.ColorSpace, CloneHelpers.CloneArray(linearGradientShader.ColorPos, context), linearGradientShader.Mode, linearGradientShader.LocalMatrix),
                PerlinNoiseFractalNoiseShader perlinNoiseFractalNoiseShader => new PerlinNoiseFractalNoiseShader(perlinNoiseFractalNoiseShader.BaseFrequencyX, perlinNoiseFractalNoiseShader.BaseFrequencyY, perlinNoiseFractalNoiseShader.NumOctaves, perlinNoiseFractalNoiseShader.Seed, perlinNoiseFractalNoiseShader.TileSize),
                PerlinNoiseTurbulenceShader perlinNoiseTurbulenceShader => new PerlinNoiseTurbulenceShader(perlinNoiseTurbulenceShader.BaseFrequencyX, perlinNoiseTurbulenceShader.BaseFrequencyY, perlinNoiseTurbulenceShader.NumOctaves, perlinNoiseTurbulenceShader.Seed, perlinNoiseTurbulenceShader.TileSize),
                PictureShader pictureShader => new PictureShader(pictureShader.Src?.DeepClone(context), pictureShader.TmX, pictureShader.TmY, pictureShader.LocalMatrix, pictureShader.Tile),
                RadialGradientShader radialGradientShader => new RadialGradientShader(radialGradientShader.Center, radialGradientShader.Radius, CloneHelpers.CloneArray(radialGradientShader.Colors, context), radialGradientShader.ColorSpace, CloneHelpers.CloneArray(radialGradientShader.ColorPos, context), radialGradientShader.Mode, radialGradientShader.LocalMatrix),
                TwoPointConicalGradientShader twoPointConicalGradientShader => new TwoPointConicalGradientShader(twoPointConicalGradientShader.Start, twoPointConicalGradientShader.StartRadius, twoPointConicalGradientShader.End, twoPointConicalGradientShader.EndRadius, CloneHelpers.CloneArray(twoPointConicalGradientShader.Colors, context), twoPointConicalGradientShader.ColorSpace, CloneHelpers.CloneArray(twoPointConicalGradientShader.ColorPos, context), twoPointConicalGradientShader.Mode, twoPointConicalGradientShader.LocalMatrix),
                _ => throw new NotSupportedException($"Unsupported {nameof(SKShader)} type: {GetType().Name}.")
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

// <summary>A shader that fills with a solid color.</summary>
// <param name="Color">The fill color.</param>
// <param name="ColorSpace">The color space of the color.</param>
internal record ColorShader(SKColor Color, SKColorSpace ColorSpace) : SKShader;

// <summary>A shader that applies a linear gradient between two points.</summary>
// <param name="Start">The start point of the gradient.</param>
// <param name="End">The end point of the gradient.</param>
// <param name="Colors">The gradient colors.</param>
// <param name="ColorSpace">The color space for the gradient colors.</param>
// <param name="ColorPos">The positions of each color along the gradient.</param>
// <param name="Mode">The tile mode for the gradient.</param>
// <param name="LocalMatrix">An optional local transformation matrix.</param>
internal record LinearGradientShader(SKPoint Start, SKPoint End, SKColorF[] Colors, SKColorSpace ColorSpace, float[] ColorPos, SKShaderTileMode Mode, SKMatrix? LocalMatrix) : SKShader;

// <summary>A shader that generates Perlin fractal noise.</summary>
// <param name="BaseFrequencyX">The base frequency in the X direction.</param>
// <param name="BaseFrequencyY">The base frequency in the Y direction.</param>
// <param name="NumOctaves">The number of octaves.</param>
// <param name="Seed">The random seed value.</param>
// <param name="TileSize">The tile size for the noise pattern.</param>
internal record PerlinNoiseFractalNoiseShader(float BaseFrequencyX, float BaseFrequencyY, int NumOctaves, float Seed, SKPointI TileSize) : SKShader;

// <summary>A shader that generates Perlin turbulence noise.</summary>
// <param name="BaseFrequencyX">The base frequency in the X direction.</param>
// <param name="BaseFrequencyY">The base frequency in the Y direction.</param>
// <param name="NumOctaves">The number of octaves.</param>
// <param name="Seed">The random seed value.</param>
// <param name="TileSize">The tile size for the noise pattern.</param>
internal record PerlinNoiseTurbulenceShader(float BaseFrequencyX, float BaseFrequencyY, int NumOctaves, float Seed, SKPointI TileSize) : SKShader;

// <summary>A shader created from a recorded picture.</summary>
// <param name="Src">The source picture.</param>
// <param name="TmX">The tile mode in the X direction.</param>
// <param name="TmY">The tile mode in the Y direction.</param>
// <param name="LocalMatrix">The local transformation matrix.</param>
// <param name="Tile">The tile rectangle.</param>
internal record PictureShader(SKPicture Src, SKShaderTileMode TmX, SKShaderTileMode TmY, SKMatrix LocalMatrix, SKRect Tile) : SKShader;

// <summary>A shader that applies a radial gradient.</summary>
// <param name="Center">The center point of the gradient.</param>
// <param name="Radius">The radius of the gradient.</param>
// <param name="Colors">The gradient colors.</param>
// <param name="ColorSpace">The color space for the gradient colors.</param>
// <param name="ColorPos">The positions of each color along the gradient.</param>
// <param name="Mode">The tile mode for the gradient.</param>
// <param name="LocalMatrix">An optional local transformation matrix.</param>
internal record RadialGradientShader(SKPoint Center, float Radius, SKColorF[] Colors, SKColorSpace ColorSpace, float[] ColorPos, SKShaderTileMode Mode, SKMatrix? LocalMatrix) : SKShader;

// <summary>A shader that applies a two-point conical gradient.</summary>
// <param name="Start">The start point of the gradient.</param>
// <param name="StartRadius">The radius at the start point.</param>
// <param name="End">The end point of the gradient.</param>
// <param name="EndRadius">The radius at the end point.</param>
// <param name="Colors">The gradient colors.</param>
// <param name="ColorSpace">The color space for the gradient colors.</param>
// <param name="ColorPos">The positions of each color along the gradient.</param>
// <param name="Mode">The tile mode for the gradient.</param>
// <param name="LocalMatrix">An optional local transformation matrix.</param>
internal record TwoPointConicalGradientShader(SKPoint Start, float StartRadius, SKPoint End, float EndRadius, SKColorF[] Colors, SKColorSpace ColorSpace, float[] ColorPos, SKShaderTileMode Mode, SKMatrix? LocalMatrix) : SKShader;
