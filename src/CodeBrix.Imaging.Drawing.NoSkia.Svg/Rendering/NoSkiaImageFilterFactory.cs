using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Raster;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.Imaging.Processing;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

// <summary>
// Builds fully managed evaluators for compiled SVG filter chains (shim
// <see cref="SKImageFilter"/> graphs). The first tier of SVG filter primitives is
// evaluated for real - feGaussianBlur, feOffset, feMerge, feFlood, feColorMatrix /
// feComponentTransfer (color filters), feBlend / feComposite (blend and arithmetic
// composition), and feImage - which covers the filters found in typical document SVGs.
// Exotic primitives (lighting, displacement, morphology, convolution, turbulence) pass
// their input through unchanged and report through <see cref="UnsupportedPrimitive"/>,
// so a filtered element always still renders.
// <para>
// Every primitive's parameters stay in SVG user units; the device scale arrives at
// evaluation time from the canvas (the transform in force when the filtered layer
// opened), so one built filter renders correctly at any output scale.
// </para>
// </summary>
internal sealed class NoSkiaImageFilterFactory : INoSkiaImageFilterFactory
{
    // <summary>
    // The display-list replayer used for color, paint, and picture conversions. Assign
    // after constructing the <see cref="NoSkiaModel"/> (the two reference each other).
    // </summary>
    public NoSkiaModel Model { get; set; }

    // <summary>
    // An optional callback invoked with the name of each filter primitive that could not
    // be evaluated and passed its input through unchanged.
    // </summary>
    public Action<string> UnsupportedPrimitive { get; set; }

    // <inheritdoc />
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

        public override string Description => Describe(_filter);

        public override DrawingBitmap Apply(DrawingBitmap source, float deviceScale)
        {
            DrawingBitmap result = _factory.Evaluate(_filter, source, deviceScale);
            return result ?? source.Copy();
        }
    }

    private static string Describe(SKImageFilter filter)
    {
        //Descriptive only - the primitive chain, outermost first, with the parameters that
        //  make one instance of a primitive tell apart from another
        switch (filter)
        {
            case null:
                return "source";
            case BlurImageFilter blur:
                return $"blur({blur.SigmaX}, {blur.SigmaY}) <- {Describe(blur.Input)}";
            case OffsetImageFilter offset:
                return $"offset({offset.Dx}, {offset.Dy}) <- {Describe(offset.Input)}";
            case ColorFilterImageFilter colorFilter:
                return $"colorFilter <- {Describe(colorFilter.Input)}";
            case MergeImageFilter merge:
            {
                var members = new List<string>();
                foreach (SKImageFilter member in merge.Filters ?? new SKImageFilter[0])
                {
                    members.Add(Describe(member));
                }
                return $"merge({String.Join(", ", members)})";
            }
            case BlendModeImageFilter blend:
                return $"blend({blend.Mode}, {Describe(blend.Background)}, {Describe(blend.Foreground)})";
            case ArithmeticImageFilter arithmetic:
                return $"arithmetic({arithmetic.K1}, {arithmetic.K2}, {arithmetic.K3}, {arithmetic.K4}, "
                    + $"{Describe(arithmetic.Background)}, {Describe(arithmetic.Foreground)})";
            case PaintImageFilter _:
                return "paint";
            case ShaderImageFilter shader:
                return $"shader({shader.Shader?.GetType().Name ?? "none"})";
            case PictureImageFilter _:
                return "picture";
            case ImageImageFilter _:
                return "image";
            case TileImageFilter tile:
                return $"tile <- {Describe(tile.Input)}";
            case DilateImageFilter dilate:
                return $"dilate({dilate.RadiusX}, {dilate.RadiusY}) <- {Describe(dilate.Input)}";
            case ErodeImageFilter erode:
                return $"erode({erode.RadiusX}, {erode.RadiusY}) <- {Describe(erode.Input)}";
            case DisplacementMapEffectImageFilter displacement:
                return $"displacementMap <- {Describe(displacement.Input)}";
            case MatrixConvolutionImageFilter convolution:
                return $"convolveMatrix <- {Describe(convolution.Input)}";
            default:
                //Every remaining primitive is one of the lighting filters
                return TrimSuffix(filter.GetType().Name);
        }
    }

    private static string TrimSuffix(string typeName)
        => typeName.EndsWith("ImageFilter", StringComparison.Ordinal)
            ? typeName.Substring(0, typeName.Length - "ImageFilter".Length)
            : typeName;

    private DrawingBitmap Evaluate(SKImageFilter filter, DrawingBitmap source, float deviceScale)
    {
        switch (filter)
        {
            case null:
                return source.Copy();

            case BlurImageFilter blur:
            {
                using DrawingBitmap input = Evaluate(blur.Input, source, deviceScale);
                float sigma = ((MathF.Abs(blur.SigmaX) + MathF.Abs(blur.SigmaY)) / 2f) * deviceScale;
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
                return Crop(output, filter, deviceScale);
            }

            case OffsetImageFilter offset:
            {
                using DrawingBitmap input = Evaluate(offset.Input, source, deviceScale);
                var output = new DrawingBitmap(input.Info);
                int deltaX = (int)MathF.Round(offset.Dx * deviceScale);
                int deltaY = (int)MathF.Round(offset.Dy * deviceScale);
                BlitShifted(input, output, deltaX, deltaY);
                return Crop(output, filter, deviceScale);
            }

            case ColorFilterImageFilter colorFilter:
            {
                DrawingBitmap output = Evaluate(colorFilter.Input, source, deviceScale);
                DrawingColorFilter converted = Model?.ToDrawingColorFilter(colorFilter.ColorFilter);
                if (converted != null)
                {
                    ApplyColorFilter(output, converted);
                }
                return Crop(output, filter, deviceScale);
            }

            case MergeImageFilter merge:
            {
                var output = new DrawingBitmap(source.Info);
                if (merge.Filters != null)
                {
                    foreach (SKImageFilter memberFilter in merge.Filters)
                    {
                        using DrawingBitmap member = Evaluate(memberFilter, source, deviceScale);
                        BlendOnto(output, member, DrawingBlendMode.SrcOver);
                    }
                }
                return Crop(output, filter, deviceScale);
            }

            case BlendModeImageFilter blend:
            {
                using DrawingBitmap background = Evaluate(blend.Background, source, deviceScale);
                using DrawingBitmap foreground = Evaluate(blend.Foreground, source, deviceScale);
                DrawingBitmap output = background.Copy();
                BlendOnto(output, foreground, Model?.ToDrawingBlendMode(blend.Mode) ?? DrawingBlendMode.SrcOver);
                return Crop(output, filter, deviceScale);
            }

            case ArithmeticImageFilter arithmetic:
            {
                using DrawingBitmap background = Evaluate(arithmetic.Background, source, deviceScale);
                using DrawingBitmap foreground = Evaluate(arithmetic.Foreground, source, deviceScale);
                DrawingBitmap output = ApplyArithmetic(foreground, background,
                    arithmetic.K1, arithmetic.K2, arithmetic.K3, arithmetic.K4);
                return Crop(output, filter, deviceScale);
            }

            case PaintImageFilter paintFilter:
            {
                //A solid color across the (cropped) filter region. A paint carrying a
                //  shader - which is how feTurbulence compiles - keeps only its color;
                //  converting the shader is what reports the degradation, so the caller
                //  learns the primitive was dropped rather than silently flattened
                if (paintFilter.Paint?.Shader is { } paintShader)
                {
                    Model?.ToDrawingShader(paintShader);
                }

                var output = new DrawingBitmap(source.Info);
                DrawingColor color = paintFilter.Paint?.Color is { } shimColor && Model != null
                    ? Model.ToDrawingColor(shimColor)
                    : DrawingColor.Empty;
                output.Erase(color);
                return Crop(output, filter, deviceScale);
            }

            case ShaderImageFilter shaderFilter:
            {
                //Only solid-color shaders (feFlood) evaluate; turbulence etc. yield transparency
                if (shaderFilter.Shader is ColorShader colorShader && Model != null)
                {
                    var output = new DrawingBitmap(source.Info);
                    output.Erase(Model.ToDrawingColor(colorShader.Color));
                    return Crop(output, filter, deviceScale);
                }
                //Reported through the model so the caller learns WHICH shader degraded
                //  (a dropped feTurbulence reads differently from an unsupported
                //  primitive); with no model there is only the primitive-level callback
                if (Model != null)
                {
                    Model.ReportUnsupportedShader(shaderFilter.Shader);
                }
                else
                {
                    UnsupportedPrimitive?.Invoke(shaderFilter.Shader?.GetType().Name ?? "ShaderImageFilter");
                }
                return new DrawingBitmap(source.Info);
            }

            case PictureImageFilter picture:
            {
                //feImage referencing vector content: replay the picture
                var output = new DrawingBitmap(source.Info);
                if (picture.Picture != null && Model != null)
                {
                    using var canvas = new DrawingCanvas(output);
                    canvas.Scale(deviceScale);
                    Model.Draw(picture.Picture, canvas);
                }
                return Crop(output, filter, deviceScale);
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
                        canvas.Scale(deviceScale);
                        canvas.DrawBitmap(decoded, Model.ToDrawingRect(image.Src), Model.ToDrawingRect(image.Dst),
                            new DrawingSamplingOptions(DrawingFilterMode.Linear));
                    }
                }
                return Crop(output, filter, deviceScale);
            }

            case TileImageFilter tile:
            {
                UnsupportedPrimitive?.Invoke("feTile");
                return Evaluate(tile.Input, source, deviceScale);
            }

            case DilateImageFilter dilate:
            {
                UnsupportedPrimitive?.Invoke("feMorphology (dilate)");
                return Evaluate(dilate.Input, source, deviceScale);
            }

            case ErodeImageFilter erode:
            {
                UnsupportedPrimitive?.Invoke("feMorphology (erode)");
                return Evaluate(erode.Input, source, deviceScale);
            }

            case DisplacementMapEffectImageFilter displacement:
            {
                UnsupportedPrimitive?.Invoke("feDisplacementMap");
                return Evaluate(displacement.Input, source, deviceScale);
            }

            case MatrixConvolutionImageFilter convolution:
            {
                UnsupportedPrimitive?.Invoke("feConvolveMatrix");
                return Evaluate(convolution.Input, source, deviceScale);
            }

            case DistantLitDiffuseImageFilter lit1:
            {
                UnsupportedPrimitive?.Invoke("feDiffuseLighting (distant)");
                return Evaluate(lit1.Input, source, deviceScale);
            }

            case DistantLitSpecularImageFilter lit2:
            {
                UnsupportedPrimitive?.Invoke("feSpecularLighting (distant)");
                return Evaluate(lit2.Input, source, deviceScale);
            }

            case PointLitDiffuseImageFilter lit3:
            {
                UnsupportedPrimitive?.Invoke("feDiffuseLighting (point)");
                return Evaluate(lit3.Input, source, deviceScale);
            }

            case PointLitSpecularImageFilter lit4:
            {
                UnsupportedPrimitive?.Invoke("feSpecularLighting (point)");
                return Evaluate(lit4.Input, source, deviceScale);
            }

            case SpotLitDiffuseImageFilter lit5:
            {
                UnsupportedPrimitive?.Invoke("feDiffuseLighting (spot)");
                return Evaluate(lit5.Input, source, deviceScale);
            }

            case SpotLitSpecularImageFilter lit6:
            {
                UnsupportedPrimitive?.Invoke("feSpecularLighting (spot)");
                return Evaluate(lit6.Input, source, deviceScale);
            }

            default:
            {
                UnsupportedPrimitive?.Invoke(filter.GetType().Name);
                return source.Copy();
            }
        }
    }

    private static DrawingBitmap Crop(DrawingBitmap bitmap, SKImageFilter filter, float deviceScale)
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

        int left = Math.Max(0, (int)MathF.Floor(clip.Value.Left * deviceScale));
        int top = Math.Max(0, (int)MathF.Floor(clip.Value.Top * deviceScale));
        int right = Math.Min(bitmap.Width, (int)MathF.Ceiling(clip.Value.Right * deviceScale));
        int bottom = Math.Min(bitmap.Height, (int)MathF.Ceiling(clip.Value.Bottom * deviceScale));

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
