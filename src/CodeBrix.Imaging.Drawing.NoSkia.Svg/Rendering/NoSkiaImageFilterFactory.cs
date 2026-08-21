using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Raster;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.Imaging.Processing;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

/// <summary>
/// Builds fully managed evaluators for compiled SVG filter chains (shim
/// <see cref="SKImageFilter"/> graphs). The first tier of SVG filter primitives is
/// evaluated for real - feGaussianBlur, feOffset, feMerge, feFlood, feColorMatrix /
/// feComponentTransfer (color filters), feBlend / feComposite (blend and arithmetic
/// composition), and feImage - which covers the filters found in typical document SVGs.
/// Exotic primitives (lighting, displacement, morphology, convolution, turbulence) pass
/// their input through unchanged and report through <see cref="UnsupportedPrimitive"/>,
/// so a filtered element always still renders.
/// </summary>
public sealed class NoSkiaImageFilterFactory : INoSkiaImageFilterFactory
{
    /// <summary>
    /// The display-list replayer used for color, paint, and picture conversions. Assign
    /// after constructing the <see cref="NoSkiaModel"/> (the two reference each other).
    /// </summary>
    public NoSkiaModel Model { get; set; }

    /// <summary>
    /// The uniform device scale of the render (the raster scale the facade applies before
    /// replaying the picture). Blur radii and offsets are recorded in user units and are
    /// scaled by this factor when evaluated on the device-space layer.
    /// </summary>
    public float DeviceScale { get; set; } = 1f;

    /// <summary>
    /// An optional callback invoked with the name of each filter primitive that could not
    /// be evaluated and passed its input through unchanged.
    /// </summary>
    public Action<string> UnsupportedPrimitive { get; set; }

    /// <inheritdoc />
    public DrawingImageFilter Create(SKImageFilter filter)
    {
        if (filter == null) { return null; }
        return new FilterEvaluator(this, filter);
    }

    private sealed class FilterEvaluator : DrawingImageFilter
    {
        private readonly NoSkiaImageFilterFactory _factory;
        private readonly SKImageFilter _filter;

        public FilterEvaluator(NoSkiaImageFilterFactory factory, SKImageFilter filter)
        {
            _factory = factory;
            _filter = filter;
        }

        public override DrawingBitmap Apply(DrawingBitmap source)
        {
            DrawingBitmap result = _factory.Evaluate(_filter, source);
            return result ?? source.Copy();
        }
    }

    private DrawingBitmap Evaluate(SKImageFilter filter, DrawingBitmap source)
    {
        switch (filter)
        {
            case null:
                return source.Copy();

            case BlurImageFilter blur:
            {
                using DrawingBitmap input = Evaluate(blur.Input, source);
                float sigma = ((MathF.Abs(blur.SigmaX) + MathF.Abs(blur.SigmaY)) / 2f) * DeviceScale;
                DrawingBitmap output;
                if (sigma < 0.01f)
                {
                    output = input.Copy();
                }
                else
                {
                    using Image<Rgba32> image = input.ToImagingRgba();
                    image.Mutate(x => x.GaussianBlur(sigma));
                    output = new DrawingBitmap(input.Info);
                    output.LoadFromImagingRgba(image);
                }
                return Crop(output, filter);
            }

            case OffsetImageFilter offset:
            {
                using DrawingBitmap input = Evaluate(offset.Input, source);
                var output = new DrawingBitmap(input.Info);
                int deltaX = (int)MathF.Round(offset.Dx * DeviceScale);
                int deltaY = (int)MathF.Round(offset.Dy * DeviceScale);
                BlitShifted(input, output, deltaX, deltaY);
                return Crop(output, filter);
            }

            case ColorFilterImageFilter colorFilter:
            {
                DrawingBitmap output = Evaluate(colorFilter.Input, source);
                DrawingColorFilter converted = Model?.ToDrawingColorFilter(colorFilter.ColorFilter);
                if (converted != null)
                {
                    ApplyColorFilter(output, converted);
                }
                return Crop(output, filter);
            }

            case MergeImageFilter merge:
            {
                var output = new DrawingBitmap(source.Info);
                if (merge.Filters != null)
                {
                    foreach (SKImageFilter memberFilter in merge.Filters)
                    {
                        using DrawingBitmap member = Evaluate(memberFilter, source);
                        BlendOnto(output, member, DrawingBlendMode.SrcOver);
                    }
                }
                return Crop(output, filter);
            }

            case BlendModeImageFilter blend:
            {
                using DrawingBitmap background = Evaluate(blend.Background, source);
                using DrawingBitmap foreground = Evaluate(blend.Foreground, source);
                DrawingBitmap output = background.Copy();
                BlendOnto(output, foreground, Model?.ToDrawingBlendMode(blend.Mode) ?? DrawingBlendMode.SrcOver);
                return Crop(output, filter);
            }

            case ArithmeticImageFilter arithmetic:
            {
                using DrawingBitmap background = Evaluate(arithmetic.Background, source);
                using DrawingBitmap foreground = Evaluate(arithmetic.Foreground, source);
                DrawingBitmap output = ApplyArithmetic(foreground, background,
                    arithmetic.K1, arithmetic.K2, arithmetic.K3, arithmetic.K4);
                return Crop(output, filter);
            }

            case PaintImageFilter paintFilter:
            {
                //feFlood: a solid color across the (cropped) filter region
                var output = new DrawingBitmap(source.Info);
                DrawingColor color = paintFilter.Paint?.Color is { } shimColor && Model != null
                    ? Model.ToDrawingColor(shimColor)
                    : DrawingColor.Empty;
                output.Erase(color);
                return Crop(output, filter);
            }

            case ShaderImageFilter shaderFilter:
            {
                //Only solid-color shaders (feFlood) evaluate; turbulence etc. yield transparency
                if (shaderFilter.Shader is ColorShader colorShader && Model != null)
                {
                    var output = new DrawingBitmap(source.Info);
                    output.Erase(Model.ToDrawingColor(colorShader.Color));
                    return Crop(output, filter);
                }
                UnsupportedPrimitive?.Invoke(shaderFilter.Shader?.GetType().Name ?? "ShaderImageFilter");
                return new DrawingBitmap(source.Info);
            }

            case PictureImageFilter picture:
            {
                //feImage referencing vector content: replay the picture
                var output = new DrawingBitmap(source.Info);
                if (picture.Picture != null && Model != null)
                {
                    using var canvas = new DrawingCanvas(output);
                    canvas.Scale(DeviceScale);
                    Model.Draw(picture.Picture, canvas);
                }
                return Crop(output, filter);
            }

            case ImageImageFilter image:
            {
                //feImage referencing raster content: decode and draw into the dest rect
                var output = new DrawingBitmap(source.Info);
                DrawingBitmap decoded = image.Image?.Data != null ? DrawingBitmap.Decode(image.Image.Data) : null;
                if (decoded != null && Model != null)
                {
                    using (decoded)
                    using (var canvas = new DrawingCanvas(output))
                    {
                        canvas.Scale(DeviceScale);
                        canvas.DrawBitmap(decoded, Model.ToDrawingRect(image.Src), Model.ToDrawingRect(image.Dst),
                            new DrawingSamplingOptions(DrawingFilterMode.Linear));
                    }
                }
                return Crop(output, filter);
            }

            case TileImageFilter tile:
            {
                UnsupportedPrimitive?.Invoke("feTile");
                return Evaluate(tile.Input, source);
            }

            case DilateImageFilter dilate:
            {
                UnsupportedPrimitive?.Invoke("feMorphology (dilate)");
                return Evaluate(dilate.Input, source);
            }

            case ErodeImageFilter erode:
            {
                UnsupportedPrimitive?.Invoke("feMorphology (erode)");
                return Evaluate(erode.Input, source);
            }

            case DisplacementMapEffectImageFilter displacement:
            {
                UnsupportedPrimitive?.Invoke("feDisplacementMap");
                return Evaluate(displacement.Input, source);
            }

            case MatrixConvolutionImageFilter convolution:
            {
                UnsupportedPrimitive?.Invoke("feConvolveMatrix");
                return Evaluate(convolution.Input, source);
            }

            case DistantLitDiffuseImageFilter lit1:
            {
                UnsupportedPrimitive?.Invoke("feDiffuseLighting (distant)");
                return Evaluate(lit1.Input, source);
            }

            case DistantLitSpecularImageFilter lit2:
            {
                UnsupportedPrimitive?.Invoke("feSpecularLighting (distant)");
                return Evaluate(lit2.Input, source);
            }

            case PointLitDiffuseImageFilter lit3:
            {
                UnsupportedPrimitive?.Invoke("feDiffuseLighting (point)");
                return Evaluate(lit3.Input, source);
            }

            case PointLitSpecularImageFilter lit4:
            {
                UnsupportedPrimitive?.Invoke("feSpecularLighting (point)");
                return Evaluate(lit4.Input, source);
            }

            case SpotLitDiffuseImageFilter lit5:
            {
                UnsupportedPrimitive?.Invoke("feDiffuseLighting (spot)");
                return Evaluate(lit5.Input, source);
            }

            case SpotLitSpecularImageFilter lit6:
            {
                UnsupportedPrimitive?.Invoke("feSpecularLighting (spot)");
                return Evaluate(lit6.Input, source);
            }

            default:
            {
                UnsupportedPrimitive?.Invoke(filter.GetType().Name);
                return source.Copy();
            }
        }
    }

    private DrawingBitmap Crop(DrawingBitmap bitmap, SKImageFilter filter)
    {
        SKRect? clip = filter switch
        {
            ArithmeticImageFilter f => f.Clip,
            BlendModeImageFilter f => f.Clip,
            BlurImageFilter f => f.Clip,
            ColorFilterImageFilter f => f.Clip,
            MergeImageFilter f => f.Clip,
            OffsetImageFilter f => f.Clip,
            PaintImageFilter f => f.Clip,
            ShaderImageFilter f => f.Clip,
            PictureImageFilter f => f.Clip,
            _ => null,
        };
        if (!clip.HasValue) { return bitmap; }

        int left = Math.Max(0, (int)MathF.Floor(clip.Value.Left * DeviceScale));
        int top = Math.Max(0, (int)MathF.Floor(clip.Value.Top * DeviceScale));
        int right = Math.Min(bitmap.Width, (int)MathF.Ceiling(clip.Value.Right * DeviceScale));
        int bottom = Math.Min(bitmap.Height, (int)MathF.Ceiling(clip.Value.Bottom * DeviceScale));

        byte[] pixels = bitmap.PixelBuffer;
        for (int y = 0; y < bitmap.Height; y++)
        {
            if (y >= top && y < bottom)
            {
                int rowOffset = y * bitmap.RowBytes;
                if (left > 0) { Array.Clear(pixels, rowOffset, left * 4); }
                if (right < bitmap.Width) { Array.Clear(pixels, rowOffset + (right * 4), (bitmap.Width - right) * 4); }
            }
            else
            {
                Array.Clear(pixels, y * bitmap.RowBytes, bitmap.RowBytes);
            }
        }
        return bitmap;
    }

    private static void BlitShifted(DrawingBitmap source, DrawingBitmap target, int deltaX, int deltaY)
    {
        byte[] sourcePixels = source.PixelBuffer;
        byte[] targetPixels = target.PixelBuffer;

        for (int y = 0; y < source.Height; y++)
        {
            int targetY = y + deltaY;
            if (targetY < 0 || targetY >= target.Height) { continue; }

            int copyStartX = Math.Max(0, deltaX);
            int copyEndX = Math.Min(target.Width, source.Width + deltaX);
            if (copyEndX <= copyStartX) { continue; }

            int sourceOffset = (y * source.RowBytes) + ((copyStartX - deltaX) * 4);
            int targetOffset = (targetY * target.RowBytes) + (copyStartX * 4);
            Buffer.BlockCopy(sourcePixels, sourceOffset, targetPixels, targetOffset, (copyEndX - copyStartX) * 4);
        }
    }

    private static void ApplyColorFilter(DrawingBitmap bitmap, DrawingColorFilter colorFilter)
    {
        byte[] pixels = bitmap.PixelBuffer;
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            float red = pixels[offset] / 255f;
            float green = pixels[offset + 1] / 255f;
            float blue = pixels[offset + 2] / 255f;
            float alpha = pixels[offset + 3] / 255f;
            colorFilter.Apply(ref red, ref green, ref blue, ref alpha);
            pixels[offset] = ToByte(red);
            pixels[offset + 1] = ToByte(green);
            pixels[offset + 2] = ToByte(blue);
            pixels[offset + 3] = ToByte(alpha);
        }
    }

    private static void BlendOnto(DrawingBitmap target, DrawingBitmap source, DrawingBlendMode mode)
    {
        byte[] sourcePixels = source.PixelBuffer;
        byte[] targetPixels = target.PixelBuffer;
        int length = Math.Min(sourcePixels.Length, targetPixels.Length);

        for (int offset = 0; offset < length; offset += 4)
        {
            float alpha = sourcePixels[offset + 3] / 255f;
            if (mode == DrawingBlendMode.SrcOver && alpha <= 0) { continue; }

            float red = sourcePixels[offset] / 255f;
            float green = sourcePixels[offset + 1] / 255f;
            float blue = sourcePixels[offset + 2] / 255f;
            if (mode == DrawingBlendMode.SrcOver)
            {
                PolygonFiller.BlendPixel(targetPixels, offset, false, red, green, blue, alpha);
            }
            else
            {
                PolygonFiller.BlendPixelWithMode(targetPixels, offset, false, mode, red, green, blue, alpha, 1f);
            }
        }
    }

    private static DrawingBitmap ApplyArithmetic(DrawingBitmap foreground, DrawingBitmap background,
        float k1, float k2, float k3, float k4)
    {
        //feComposite operator="arithmetic": result = k1*i*j + k2*i + k3*j + k4 on
        //premultiplied components (i = foreground, j = background)
        var output = new DrawingBitmap(background.Info);
        byte[] foregroundPixels = foreground.PixelBuffer;
        byte[] backgroundPixels = background.PixelBuffer;
        byte[] outputPixels = output.PixelBuffer;
        int length = Math.Min(foregroundPixels.Length, backgroundPixels.Length);

        for (int offset = 0; offset < length; offset += 4)
        {
            float foregroundAlpha = foregroundPixels[offset + 3] / 255f;
            float backgroundAlpha = backgroundPixels[offset + 3] / 255f;
            float outAlpha = Math.Clamp(
                (k1 * foregroundAlpha * backgroundAlpha) + (k2 * foregroundAlpha) + (k3 * backgroundAlpha) + k4,
                0f, 1f);

            float outR = 0, outG = 0, outB = 0;
            for (int channel = 0; channel < 3; channel++)
            {
                float i = (foregroundPixels[offset + channel] / 255f) * foregroundAlpha;
                float j = (backgroundPixels[offset + channel] / 255f) * backgroundAlpha;
                float premul = Math.Clamp((k1 * i * j) + (k2 * i) + (k3 * j) + k4, 0f, 1f);
                float straight = outAlpha > 0 ? Math.Clamp(premul / outAlpha, 0f, 1f) : 0f;
                if (channel == 0) { outR = straight; }
                else if (channel == 1) { outG = straight; }
                else { outB = straight; }
            }

            outputPixels[offset] = ToByte(outR);
            outputPixels[offset + 1] = ToByte(outG);
            outputPixels[offset + 2] = ToByte(outB);
            outputPixels[offset + 3] = ToByte(outAlpha);
        }
        return output;
    }

    private static byte ToByte(float value)
    {
        float scaled = (value * 255f) + 0.5f;
        if (scaled <= 0) { return 0; }
        if (scaled >= 255f) { return 255; }
        return (byte)scaled;
    }
}
