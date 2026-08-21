// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp; //Was previously: namespace ShimSkiaSharp;

/// <summary>Represents an abstract image filter that can be applied to drawing operations.</summary>
public abstract record SKImageFilter : IDeepCloneable<SKImageFilter>
{
    /// <summary>Creates an arithmetic image filter that combines two inputs using the formula k1*i*j + k2*i + k3*j + k4.</summary>
    /// <param name="k1">The first coefficient.</param>
    /// <param name="k2">The second coefficient.</param>
    /// <param name="k3">The third coefficient.</param>
    /// <param name="k4">The fourth coefficient.</param>
    /// <param name="enforcePMColor">Whether to enforce premultiplied color.</param>
    /// <param name="background">The background image filter.</param>
    /// <param name="foreground">The optional foreground image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateArithmetic(float k1, float k2, float k3, float k4, bool enforcePMColor, SKImageFilter background, SKImageFilter foreground = null, SKRect? cropRect = null)
        => new ArithmeticImageFilter(k1, k2, k3, k4, enforcePMColor, background, foreground, cropRect);

    /// <summary>Creates a blend mode image filter that composites two inputs using the specified blend mode.</summary>
    /// <param name="mode">The blend mode to apply.</param>
    /// <param name="background">The background image filter.</param>
    /// <param name="foreground">The optional foreground image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateBlendMode(SKBlendMode mode, SKImageFilter background, SKImageFilter foreground = null, SKRect? cropRect = null)
        => new BlendModeImageFilter(mode, background, foreground, cropRect);

    /// <summary>Creates a Gaussian blur image filter.</summary>
    /// <param name="sigmaX">The blur amount in the X direction.</param>
    /// <param name="sigmaY">The blur amount in the Y direction.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateBlur(float sigmaX, float sigmaY, SKImageFilter input = null, SKRect? cropRect = null)
        => new BlurImageFilter(sigmaX, sigmaY, input, cropRect);

    /// <summary>Creates an image filter that applies a color filter.</summary>
    /// <param name="cf">The color filter to apply.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateColorFilter(SKColorFilter cf, SKImageFilter input = null, SKRect? cropRect = null)
        => new ColorFilterImageFilter(cf, input, cropRect);

    /// <summary>Creates a dilation (grow) morphology image filter.</summary>
    /// <param name="radiusX">The dilation radius in the X direction.</param>
    /// <param name="radiusY">The dilation radius in the Y direction.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateDilate(int radiusX, int radiusY, SKImageFilter input = null, SKRect? cropRect = null)
        => new DilateImageFilter(radiusX, radiusY, input, cropRect);

    /// <summary>Creates a displacement map effect image filter.</summary>
    /// <param name="xChannelSelector">The color channel used for X displacement.</param>
    /// <param name="yChannelSelector">The color channel used for Y displacement.</param>
    /// <param name="scale">The displacement scale factor.</param>
    /// <param name="displacement">The displacement map image filter.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateDisplacementMapEffect(SKColorChannel xChannelSelector, SKColorChannel yChannelSelector, float scale, SKImageFilter displacement, SKImageFilter input = null, SKRect? cropRect = null)
        => new DisplacementMapEffectImageFilter(xChannelSelector, yChannelSelector, scale, displacement, input, cropRect);

    /// <summary>Creates a distant light diffuse lighting image filter.</summary>
    /// <param name="direction">The direction of the distant light source.</param>
    /// <param name="lightColor">The color of the light.</param>
    /// <param name="surfaceScale">The surface scale factor.</param>
    /// <param name="kd">The diffuse reflection coefficient.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateDistantLitDiffuse(SKPoint3 direction, SKColor lightColor, float surfaceScale, float kd, SKImageFilter input = null, SKRect? cropRect = null)
        => new DistantLitDiffuseImageFilter(direction, lightColor, surfaceScale, kd, input, cropRect);

    /// <summary>Creates a distant light specular lighting image filter.</summary>
    /// <param name="direction">The direction of the distant light source.</param>
    /// <param name="lightColor">The color of the light.</param>
    /// <param name="surfaceScale">The surface scale factor.</param>
    /// <param name="ks">The specular reflection coefficient.</param>
    /// <param name="shininess">The specular shininess exponent.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateDistantLitSpecular(SKPoint3 direction, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter input = null, SKRect? cropRect = null)
        => new DistantLitSpecularImageFilter(direction, lightColor, surfaceScale, ks, shininess, input, cropRect);

    /// <summary>Creates an erosion (shrink) morphology image filter.</summary>
    /// <param name="radiusX">The erosion radius in the X direction.</param>
    /// <param name="radiusY">The erosion radius in the Y direction.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateErode(int radiusX, int radiusY, SKImageFilter input = null, SKRect? cropRect = null)
        => new ErodeImageFilter(radiusX, radiusY, input, cropRect);

    /// <summary>Creates an image filter from an image with source and destination rectangles.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="src">The source rectangle within the image.</param>
    /// <param name="dst">The destination rectangle.</param>
    /// <param name="filterQuality">The filter quality for image sampling.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateImage(SKImage image, SKRect src, SKRect dst, SKFilterQuality filterQuality)
        => new ImageImageFilter(image, src, dst, filterQuality);

    /// <summary>Creates a matrix convolution image filter.</summary>
    /// <param name="kernelSize">The size of the convolution kernel.</param>
    /// <param name="kernel">The kernel values.</param>
    /// <param name="gain">The gain multiplier applied to the result.</param>
    /// <param name="bias">The bias added to the result.</param>
    /// <param name="kernelOffset">The offset of the kernel center.</param>
    /// <param name="tileMode">The tile mode for reading pixels outside the image bounds.</param>
    /// <param name="convolveAlpha">Whether to convolve the alpha channel.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateMatrixConvolution(SKSizeI kernelSize, float[] kernel, float gain, float bias, SKPointI kernelOffset, SKShaderTileMode tileMode, bool convolveAlpha, SKImageFilter input = null, SKRect? cropRect = null)
        => new MatrixConvolutionImageFilter(kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, cropRect);

    /// <summary>Creates a merge image filter that combines multiple image filters.</summary>
    /// <param name="filters">The image filters to merge.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateMerge(SKImageFilter[] filters, SKRect? cropRect = null)
        => new MergeImageFilter(filters, cropRect);

    /// <summary>Creates an offset image filter that translates the input.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateOffset(float dx, float dy, SKImageFilter input = null, SKRect? cropRect = null)
        => new OffsetImageFilter(dx, dy, input, cropRect);

    /// <summary>Creates an image filter that draws using a paint object.</summary>
    /// <param name="paint">The paint to draw with.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreatePaint(SKPaint paint, SKRect? cropRect = null)
        => new PaintImageFilter(paint, cropRect);

    /// <summary>Creates an image filter that draws using a shader.</summary>
    /// <param name="shader">The shader to draw with.</param>
    /// <param name="dither">Whether to apply dithering.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateShader(SKShader shader, bool dither, SKRect? cropRect = null)
        => new ShaderImageFilter(shader, dither, cropRect);

    /// <summary>Creates an image filter from a recorded picture.</summary>
    /// <param name="picture">The source picture.</param>
    /// <param name="cropRect">The crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreatePicture(SKPicture picture, SKRect cropRect)
        => new PictureImageFilter(picture, cropRect);

    /// <summary>Creates a point light diffuse lighting image filter.</summary>
    /// <param name="location">The position of the point light source.</param>
    /// <param name="lightColor">The color of the light.</param>
    /// <param name="surfaceScale">The surface scale factor.</param>
    /// <param name="kd">The diffuse reflection coefficient.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreatePointLitDiffuse(SKPoint3 location, SKColor lightColor, float surfaceScale, float kd, SKImageFilter input = null, SKRect? cropRect = null)
        => new PointLitDiffuseImageFilter(location, lightColor, surfaceScale, kd, input, cropRect);

    /// <summary>Creates a point light specular lighting image filter.</summary>
    /// <param name="location">The position of the point light source.</param>
    /// <param name="lightColor">The color of the light.</param>
    /// <param name="surfaceScale">The surface scale factor.</param>
    /// <param name="ks">The specular reflection coefficient.</param>
    /// <param name="shininess">The specular shininess exponent.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreatePointLitSpecular(SKPoint3 location, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter input = null, SKRect? cropRect = null)
        => new PointLitSpecularImageFilter(location, lightColor, surfaceScale, ks, shininess, input, cropRect);

    /// <summary>Creates a spot light diffuse lighting image filter.</summary>
    /// <param name="location">The position of the spot light source.</param>
    /// <param name="target">The target point of the spot light.</param>
    /// <param name="specularExponent">The specular exponent controlling the light focus.</param>
    /// <param name="cutoffAngle">The cutoff angle of the spot light cone.</param>
    /// <param name="lightColor">The color of the light.</param>
    /// <param name="surfaceScale">The surface scale factor.</param>
    /// <param name="kd">The diffuse reflection coefficient.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateSpotLitDiffuse(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float kd, SKImageFilter input = null, SKRect? cropRect = null)
        => new SpotLitDiffuseImageFilter(location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, kd, input, cropRect);

    /// <summary>Creates a spot light specular lighting image filter.</summary>
    /// <param name="location">The position of the spot light source.</param>
    /// <param name="target">The target point of the spot light.</param>
    /// <param name="specularExponent">The specular exponent controlling the light focus.</param>
    /// <param name="cutoffAngle">The cutoff angle of the spot light cone.</param>
    /// <param name="lightColor">The color of the light.</param>
    /// <param name="surfaceScale">The surface scale factor.</param>
    /// <param name="ks">The specular reflection coefficient.</param>
    /// <param name="shininess">The specular shininess exponent.</param>
    /// <param name="input">The optional input image filter.</param>
    /// <param name="cropRect">The optional crop rectangle.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateSpotLitSpecular(SKPoint3 location, SKPoint3 target, float specularExponent, float cutoffAngle, SKColor lightColor, float surfaceScale, float ks, float shininess, SKImageFilter input = null, SKRect? cropRect = null)
        => new SpotLitSpecularImageFilter(location, target, specularExponent, cutoffAngle, lightColor, surfaceScale, ks, shininess, input, cropRect);

    /// <summary>Creates a tile image filter that tiles the input within a destination rectangle.</summary>
    /// <param name="src">The source rectangle.</param>
    /// <param name="dst">The destination rectangle.</param>
    /// <param name="input">The input image filter.</param>
    /// <returns>A new <see cref="SKImageFilter"/> instance.</returns>
    public static SKImageFilter CreateTile(SKRect src, SKRect dst, SKImageFilter input)
        => new TileImageFilter(src, dst, input);

    /// <inheritdoc />
    public SKImageFilter DeepClone() => DeepClone(new CloneContext());

    internal SKImageFilter DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKImageFilter existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            SKImageFilter clone = this switch
            {
                ArithmeticImageFilter arithmeticImageFilter => new ArithmeticImageFilter(arithmeticImageFilter.K1, arithmeticImageFilter.K2, arithmeticImageFilter.K3, arithmeticImageFilter.K4, arithmeticImageFilter.EforcePMColor, arithmeticImageFilter.Background?.DeepClone(context), arithmeticImageFilter.Foreground?.DeepClone(context), arithmeticImageFilter.Clip),
                BlendModeImageFilter blendModeImageFilter => new BlendModeImageFilter(blendModeImageFilter.Mode, blendModeImageFilter.Background?.DeepClone(context), blendModeImageFilter.Foreground?.DeepClone(context), blendModeImageFilter.Clip),
                BlurImageFilter blurImageFilter => new BlurImageFilter(blurImageFilter.SigmaX, blurImageFilter.SigmaY, blurImageFilter.Input?.DeepClone(context), blurImageFilter.Clip),
                ColorFilterImageFilter colorFilterImageFilter => new ColorFilterImageFilter(colorFilterImageFilter.ColorFilter?.DeepClone(context), colorFilterImageFilter.Input?.DeepClone(context), colorFilterImageFilter.Clip),
                DilateImageFilter dilateImageFilter => new DilateImageFilter(dilateImageFilter.RadiusX, dilateImageFilter.RadiusY, dilateImageFilter.Input?.DeepClone(context), dilateImageFilter.Clip),
                DisplacementMapEffectImageFilter displacementMapEffectImageFilter => new DisplacementMapEffectImageFilter(displacementMapEffectImageFilter.XChannelSelector, displacementMapEffectImageFilter.YChannelSelector, displacementMapEffectImageFilter.Scale, displacementMapEffectImageFilter.Displacement?.DeepClone(context), displacementMapEffectImageFilter.Input?.DeepClone(context), displacementMapEffectImageFilter.Clip),
                DistantLitDiffuseImageFilter distantLitDiffuseImageFilter => new DistantLitDiffuseImageFilter(distantLitDiffuseImageFilter.Direction, distantLitDiffuseImageFilter.LightColor, distantLitDiffuseImageFilter.SurfaceScale, distantLitDiffuseImageFilter.Kd, distantLitDiffuseImageFilter.Input?.DeepClone(context), distantLitDiffuseImageFilter.Clip),
                DistantLitSpecularImageFilter distantLitSpecularImageFilter => new DistantLitSpecularImageFilter(distantLitSpecularImageFilter.Direction, distantLitSpecularImageFilter.LightColor, distantLitSpecularImageFilter.SurfaceScale, distantLitSpecularImageFilter.Ks, distantLitSpecularImageFilter.Shininess, distantLitSpecularImageFilter.Input?.DeepClone(context), distantLitSpecularImageFilter.Clip),
                ErodeImageFilter erodeImageFilter => new ErodeImageFilter(erodeImageFilter.RadiusX, erodeImageFilter.RadiusY, erodeImageFilter.Input?.DeepClone(context), erodeImageFilter.Clip),
                ImageImageFilter imageImageFilter => new ImageImageFilter(imageImageFilter.Image?.DeepClone(context), imageImageFilter.Src, imageImageFilter.Dst, imageImageFilter.FilterQuality),
                MatrixConvolutionImageFilter matrixConvolutionImageFilter => new MatrixConvolutionImageFilter(matrixConvolutionImageFilter.KernelSize, CloneHelpers.CloneArray(matrixConvolutionImageFilter.Kernel, context), matrixConvolutionImageFilter.Gain, matrixConvolutionImageFilter.Bias, matrixConvolutionImageFilter.KernelOffset, matrixConvolutionImageFilter.TileMode, matrixConvolutionImageFilter.ConvolveAlpha, matrixConvolutionImageFilter.Input?.DeepClone(context), matrixConvolutionImageFilter.Clip),
                MergeImageFilter mergeImageFilter => new MergeImageFilter(CloneHelpers.CloneArray(mergeImageFilter.Filters, context, filter => filter.DeepClone(context)), mergeImageFilter.Clip),
                OffsetImageFilter offsetImageFilter => new OffsetImageFilter(offsetImageFilter.Dx, offsetImageFilter.Dy, offsetImageFilter.Input?.DeepClone(context), offsetImageFilter.Clip),
                PaintImageFilter paintImageFilter => new PaintImageFilter(paintImageFilter.Paint?.DeepClone(context), paintImageFilter.Clip),
                ShaderImageFilter shaderImageFilter => new ShaderImageFilter(shaderImageFilter.Shader?.DeepClone(context), shaderImageFilter.Dither, shaderImageFilter.Clip),
                PictureImageFilter pictureImageFilter => new PictureImageFilter(pictureImageFilter.Picture?.DeepClone(context), pictureImageFilter.Clip),
                PointLitDiffuseImageFilter pointLitDiffuseImageFilter => new PointLitDiffuseImageFilter(pointLitDiffuseImageFilter.Location, pointLitDiffuseImageFilter.LightColor, pointLitDiffuseImageFilter.SurfaceScale, pointLitDiffuseImageFilter.Kd, pointLitDiffuseImageFilter.Input?.DeepClone(context), pointLitDiffuseImageFilter.Clip),
                PointLitSpecularImageFilter pointLitSpecularImageFilter => new PointLitSpecularImageFilter(pointLitSpecularImageFilter.Location, pointLitSpecularImageFilter.LightColor, pointLitSpecularImageFilter.SurfaceScale, pointLitSpecularImageFilter.Ks, pointLitSpecularImageFilter.Shininess, pointLitSpecularImageFilter.Input?.DeepClone(context), pointLitSpecularImageFilter.Clip),
                SpotLitDiffuseImageFilter spotLitDiffuseImageFilter => new SpotLitDiffuseImageFilter(spotLitDiffuseImageFilter.Location, spotLitDiffuseImageFilter.Target, spotLitDiffuseImageFilter.SpecularExponent, spotLitDiffuseImageFilter.CutoffAngle, spotLitDiffuseImageFilter.LightColor, spotLitDiffuseImageFilter.SurfaceScale, spotLitDiffuseImageFilter.Kd, spotLitDiffuseImageFilter.Input?.DeepClone(context), spotLitDiffuseImageFilter.Clip),
                SpotLitSpecularImageFilter spotLitSpecularImageFilter => new SpotLitSpecularImageFilter(spotLitSpecularImageFilter.Location, spotLitSpecularImageFilter.Target, spotLitSpecularImageFilter.SpecularExponent, spotLitSpecularImageFilter.CutoffAngle, spotLitSpecularImageFilter.LightColor, spotLitSpecularImageFilter.SurfaceScale, spotLitSpecularImageFilter.Ks, spotLitSpecularImageFilter.Shininess, spotLitSpecularImageFilter.Input?.DeepClone(context), spotLitSpecularImageFilter.Clip),
                TileImageFilter tileImageFilter => new TileImageFilter(tileImageFilter.Src, tileImageFilter.Dst, tileImageFilter.Input?.DeepClone(context)),
                _ => throw new NotSupportedException($"Unsupported {nameof(SKImageFilter)} type: {GetType().Name}.")
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

/// <summary>An image filter that combines two inputs using an arithmetic formula.</summary>
/// <param name="K1">The first coefficient.</param>
/// <param name="K2">The second coefficient.</param>
/// <param name="K3">The third coefficient.</param>
/// <param name="K4">The fourth coefficient.</param>
/// <param name="EforcePMColor">Whether to enforce premultiplied color.</param>
/// <param name="Background">The background image filter.</param>
/// <param name="Foreground">The foreground image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record ArithmeticImageFilter(float K1, float K2, float K3, float K4, bool EforcePMColor, SKImageFilter Background, SKImageFilter Foreground, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that composites two inputs using a blend mode.</summary>
/// <param name="Mode">The blend mode to apply.</param>
/// <param name="Background">The background image filter.</param>
/// <param name="Foreground">The foreground image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record BlendModeImageFilter(SKBlendMode Mode, SKImageFilter Background, SKImageFilter Foreground, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies a Gaussian blur.</summary>
/// <param name="SigmaX">The blur amount in the X direction.</param>
/// <param name="SigmaY">The blur amount in the Y direction.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record BlurImageFilter(float SigmaX, float SigmaY, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies a color filter.</summary>
/// <param name="ColorFilter">The color filter to apply.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record ColorFilterImageFilter(SKColorFilter ColorFilter, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies a dilation morphology operation.</summary>
/// <param name="RadiusX">The dilation radius in the X direction.</param>
/// <param name="RadiusY">The dilation radius in the Y direction.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record DilateImageFilter(int RadiusX, int RadiusY, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that displaces pixels using a displacement map.</summary>
/// <param name="XChannelSelector">The color channel used for X displacement.</param>
/// <param name="YChannelSelector">The color channel used for Y displacement.</param>
/// <param name="Scale">The displacement scale factor.</param>
/// <param name="Displacement">The displacement map image filter.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record DisplacementMapEffectImageFilter(SKColorChannel XChannelSelector, SKColorChannel YChannelSelector, float Scale, SKImageFilter Displacement, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies distant light diffuse lighting.</summary>
/// <param name="Direction">The direction of the distant light source.</param>
/// <param name="LightColor">The color of the light.</param>
/// <param name="SurfaceScale">The surface scale factor.</param>
/// <param name="Kd">The diffuse reflection coefficient.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record DistantLitDiffuseImageFilter(SKPoint3 Direction, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies distant light specular lighting.</summary>
/// <param name="Direction">The direction of the distant light source.</param>
/// <param name="LightColor">The color of the light.</param>
/// <param name="SurfaceScale">The surface scale factor.</param>
/// <param name="Ks">The specular reflection coefficient.</param>
/// <param name="Shininess">The specular shininess exponent.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record DistantLitSpecularImageFilter(SKPoint3 Direction, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies an erosion morphology operation.</summary>
/// <param name="RadiusX">The erosion radius in the X direction.</param>
/// <param name="RadiusY">The erosion radius in the Y direction.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record ErodeImageFilter(int RadiusX, int RadiusY, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter created from a source image.</summary>
/// <param name="Image">The source image.</param>
/// <param name="Src">The source rectangle within the image.</param>
/// <param name="Dst">The destination rectangle.</param>
/// <param name="FilterQuality">The filter quality for image sampling.</param>
public record ImageImageFilter(SKImage Image, SKRect Src, SKRect Dst, SKFilterQuality FilterQuality) : SKImageFilter;

/// <summary>An image filter that applies a matrix convolution.</summary>
/// <param name="KernelSize">The size of the convolution kernel.</param>
/// <param name="Kernel">The kernel values.</param>
/// <param name="Gain">The gain multiplier applied to the result.</param>
/// <param name="Bias">The bias added to the result.</param>
/// <param name="KernelOffset">The offset of the kernel center.</param>
/// <param name="TileMode">The tile mode for reading pixels outside the image bounds.</param>
/// <param name="ConvolveAlpha">Whether to convolve the alpha channel.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record MatrixConvolutionImageFilter(SKSizeI KernelSize, float[] Kernel, float Gain, float Bias, SKPointI KernelOffset, SKShaderTileMode TileMode, bool ConvolveAlpha, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that merges multiple image filters.</summary>
/// <param name="Filters">The image filters to merge.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record MergeImageFilter(SKImageFilter[] Filters, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that translates the input by an offset.</summary>
/// <param name="Dx">The horizontal offset.</param>
/// <param name="Dy">The vertical offset.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record OffsetImageFilter(float Dx, float Dy, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that draws using a paint object.</summary>
/// <param name="Paint">The paint to draw with.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record PaintImageFilter(SKPaint Paint, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that draws using a shader.</summary>
/// <param name="Shader">The shader to draw with.</param>
/// <param name="Dither">Whether to apply dithering.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record ShaderImageFilter(SKShader Shader, bool Dither, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter created from a recorded picture.</summary>
/// <param name="Picture">The source picture.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record PictureImageFilter(SKPicture Picture, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies point light diffuse lighting.</summary>
/// <param name="Location">The position of the point light source.</param>
/// <param name="LightColor">The color of the light.</param>
/// <param name="SurfaceScale">The surface scale factor.</param>
/// <param name="Kd">The diffuse reflection coefficient.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record PointLitDiffuseImageFilter(SKPoint3 Location, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies point light specular lighting.</summary>
/// <param name="Location">The position of the point light source.</param>
/// <param name="LightColor">The color of the light.</param>
/// <param name="SurfaceScale">The surface scale factor.</param>
/// <param name="Ks">The specular reflection coefficient.</param>
/// <param name="Shininess">The specular shininess exponent.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record PointLitSpecularImageFilter(SKPoint3 Location, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies spot light diffuse lighting.</summary>
/// <param name="Location">The position of the spot light source.</param>
/// <param name="Target">The target point of the spot light.</param>
/// <param name="SpecularExponent">The specular exponent controlling the light focus.</param>
/// <param name="CutoffAngle">The cutoff angle of the spot light cone.</param>
/// <param name="LightColor">The color of the light.</param>
/// <param name="SurfaceScale">The surface scale factor.</param>
/// <param name="Kd">The diffuse reflection coefficient.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record SpotLitDiffuseImageFilter(SKPoint3 Location, SKPoint3 Target, float SpecularExponent, float CutoffAngle, SKColor LightColor, float SurfaceScale, float Kd, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that applies spot light specular lighting.</summary>
/// <param name="Location">The position of the spot light source.</param>
/// <param name="Target">The target point of the spot light.</param>
/// <param name="SpecularExponent">The specular exponent controlling the light focus.</param>
/// <param name="CutoffAngle">The cutoff angle of the spot light cone.</param>
/// <param name="LightColor">The color of the light.</param>
/// <param name="SurfaceScale">The surface scale factor.</param>
/// <param name="Ks">The specular reflection coefficient.</param>
/// <param name="Shininess">The specular shininess exponent.</param>
/// <param name="Input">The input image filter.</param>
/// <param name="Clip">The optional clip rectangle.</param>
public record SpotLitSpecularImageFilter(SKPoint3 Location, SKPoint3 Target, float SpecularExponent, float CutoffAngle, SKColor LightColor, float SurfaceScale, float Ks, float Shininess, SKImageFilter Input, SKRect? Clip) : SKImageFilter;

/// <summary>An image filter that tiles the input within a destination rectangle.</summary>
/// <param name="Src">The source rectangle.</param>
/// <param name="Dst">The destination rectangle.</param>
/// <param name="Input">The input image filter.</param>
public record TileImageFilter(SKRect Src, SKRect Dst, SKImageFilter Input) : SKImageFilter;
