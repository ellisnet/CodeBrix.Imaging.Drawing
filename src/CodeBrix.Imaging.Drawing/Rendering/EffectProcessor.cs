using System;
using System.Numerics;
using System.Runtime.InteropServices;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.PixelFormats;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Rendering;

/// <summary>
/// Computes blur, glow and drop shadow from rendered pixels with the shared planes arithmetic,
/// so both packages apply the same effect to what their canvases drew. Works on the box around
/// the content's coverage, grown by the effects' reach. Sizes follow the content's whole linear
/// map to the device (element, groups, layer, camera, calibration): radii by the square root of
/// its area scale, the shadow offset through the map itself (turning with the element) or
/// through its stretch alone, turned by the scene's rotation (fixed in the scene).
/// </summary>
internal static class EffectProcessor
{
    //Draws an element (or anything) through effects: `draw` renders into a transparent bitmap of
    //  the target's device size whose transform matches `canvas`; the processed pixels are then
    //  composited onto `canvas` 1:1 in device space at `alpha`. `local` is the element's own
    //  transform, which `draw` applies inside the canvas's current one
    internal static void DrawThrough(SKCanvas canvas, PaintContext context, DrawingEffects effects, byte alpha,
        Matrix3x2 local, Action<SKCanvas> draw)
    {
        EffectSizes sizes = Measure(effects, local * GetTotalMatrix(canvas), context.SceneRotation);
        using var offscreen = new SKBitmap(context.Width, context.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var offscreenCanvas = new SKCanvas(offscreen))
        {
            offscreenCanvas.Clear(SKColors.Transparent);
            offscreenCanvas.Concat(canvas.TotalMatrix);
            draw(offscreenCanvas);
        }
        using SKBitmap processed = Apply(offscreen, effects, sizes, out int left, out int top);
        if (processed == null) { return; }
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha(alpha) };
        canvas.Save();
        canvas.ResetMatrix();
        canvas.DrawBitmap(processed, SKRect.Create(left, top, processed.Width, processed.Height),
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
        canvas.Restore();
    }

    //Replaces a premultiplied layer bitmap's content with the processed content (same size).
    //  `device` maps the layer's units to device pixels (layer transform, camera, calibration)
    internal static SKBitmap ApplyToLayer(SKBitmap bitmap, DrawingEffects effects, Matrix3x2 device, Matrix3x2 sceneRotation)
    {
        EffectSizes sizes = Measure(effects, device, sceneRotation);
        var result = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        using SKBitmap processed = Apply(bitmap, effects, sizes, out int left, out int top);
        if (processed != null)
        {
            canvas.DrawBitmap(processed, SKRect.Create(left, top, processed.Width, processed.Height),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), null);
        }
        return result;
    }

    //The processed pixels of the region around the source's coverage, as a straight-alpha
    //  bitmap placed at (left, top); null when the source is empty
    internal static SKBitmap Apply(SKBitmap source, DrawingEffects effects, EffectSizes sizes, out int left, out int top)
    {
        left = 0;
        top = 0;
        int width = source.Width, height = source.Height;
        byte[] rgba = ReadStraight(source);

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                if (rgba[row + (x * 4) + 3] == 0) { continue; }
                if (x < minX) { minX = x; }
                if (x > maxX) { maxX = x; }
                if (y < minY) { minY = y; }
                if (y > maxY) { maxY = y; }
            }
        }
        if (maxX < 0) { return null; }

        float scale = sizes.RadiusScale;
        float blurSigma = effects.BlurRadius * 0.5f * scale;
        float shadowSigma = effects.ShadowBlurRadius * 0.5f * scale;
        float glowSigma = effects.GlowRadius * 0.5f * scale;
        float offsetX = sizes.OffsetX, offsetY = sizes.OffsetY;
        int reach = Reach(effects.HasBlur ? blurSigma : 0f);
        int outer = 0;
        if (effects.HasGlow) { outer = Math.Max(outer, Reach(glowSigma)); }
        if (effects.HasShadow)
        {
            outer = Math.Max(outer, Reach(shadowSigma) + (int)Math.Ceiling(Math.Max(Math.Abs(offsetX), Math.Abs(offsetY))));
        }
        reach += outer;

        int rx = Math.Max(0, minX - reach), ry = Math.Max(0, minY - reach);
        int rr = Math.Min(width - 1, maxX + reach), rb = Math.Min(height - 1, maxY + reach);
        int rw = rr - rx + 1, rh = rb - ry + 1;
        var region = new byte[rw * rh * 4];
        for (int y = 0; y < rh; y++)
        {
            Buffer.BlockCopy(rgba, (((y + ry) * width) + rx) * 4, region, y * rw * 4, rw * 4);
        }

        DrawingPlaneImage content = DrawingPlaneImage.FromRgba(region, rw, rh);
        if (effects.HasBlur) { content.GaussianBlur(blurSigma); }

        DrawingPlaneImage result;
        if (!effects.HasShadow && !effects.HasGlow)
        {
            result = content;
        }
        else
        {
            result = new DrawingPlaneImage(rw, rh);
            if (effects.HasShadow)
            {
                DrawingPlane coverage = content.A.Clone();
                if (shadowSigma > 0f) { coverage.GaussianBlur(shadowSigma); }
                DrawingPlane shifted = Shift(coverage, offsetX, offsetY);
                result.CompositeOver(Colored(effects.ShadowColor, shifted));
            }
            if (effects.HasGlow)
            {
                DrawingPlane coverage = content.A.Clone();
                coverage.GaussianBlur(glowSigma);
                coverage.Multiply(effects.GlowStrength).Min(1f);
                result.CompositeOver(Colored(effects.GlowColor, coverage));
            }
            result.CompositeOver(content);
        }

        left = rx;
        top = ry;
        return DrawingMask.CreateStraightBitmap(result.ToRgba(), rw, rh);
    }

    //How big the effects are on the device. `device` is the content's linear map from its own
    //  units to device pixels (row vectors: x' = x M11 + y M21); `sceneRotation` the scene's turn.
    //  Radii scale by sqrt|det|. A scene-fixed offset v becomes P (v R): R turns it with the
    //  scene, P = sqrt(F^T F) (F^T F = C C^T for the column form C = F^T, so P is the map's left
    //  stretch - its scale along the scene's axes with its rotation taken out). For a map that is
    //  a uniform scale s times the scene rotation this is exactly s (v R), and for F = s I it is
    //  exactly v s, the arithmetic of a plain scale
    internal static EffectSizes Measure(DrawingEffects effects, Matrix3x2 device, Matrix3x2 sceneRotation)
    {
        double a = device.M11, b = device.M12, c = device.M21, d = device.M22;
        double det = Math.Abs((a * d) - (b * c));
        float radiusScale = (float)Math.Sqrt(det);
        double ox = effects.ShadowOffset.X, oy = effects.ShadowOffset.Y;
        if (ox == 0.0 && oy == 0.0) { return new EffectSizes(radiusScale, 0f, 0f); }

        if (effects.ShadowDirection == ShadowDirection.RotatesWithElement)
        {
            return new EffectSizes(radiusScale, (float)((ox * a) + (oy * c)), (float)((ox * b) + (oy * d)));
        }

        double vx = ox, vy = oy;
        if (!sceneRotation.IsIdentity)
        {
            vx = (ox * sceneRotation.M11) + (oy * sceneRotation.M21);
            vy = (ox * sceneRotation.M12) + (oy * sceneRotation.M22);
        }
        //F^T F, its square root by the 2 x 2 closed form (M + sqrt(det M) I) / sqrt(tr M + 2 sqrt(det M)),
        //  where sqrt(det M) = |det F|
        double m11 = (a * a) + (c * c), m12 = (a * b) + (c * d), m22 = (b * b) + (d * d);
        double t = Math.Sqrt(m11 + m22 + (2.0 * det));
        if (t <= 0.0) { return new EffectSizes(radiusScale, 0f, 0f); }
        double p11 = (m11 + det) / t, p12 = m12 / t, p22 = (m22 + det) / t;
        return new EffectSizes(radiusScale, (float)((p11 * vx) + (p12 * vy)), (float)((p12 * vx) + (p22 * vy)));
    }

    //The canvas's current transform as a row-vector matrix (the managed canvas keeps one already)
    internal static Matrix3x2 GetTotalMatrix(SKCanvas canvas)
    {
#if NOSKIA
        return canvas.TotalMatrix;
#else
        SKMatrix m = canvas.TotalMatrix;
        return new Matrix3x2(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);
#endif
    }

    //The pixels a Gaussian of this sigma reaches (the planes' kernel radius, plus one)
    private static int Reach(float sigma) => sigma > 0f ? (int)((4f * sigma) + 0.5f) + 1 : 0;

    //An image of one straight colour whose alpha is the colour's alpha times the coverage
    private static DrawingPlaneImage Colored(Color color, DrawingPlane coverage)
    {
        Rgba32 c = color.ToRgba32();
        int w = coverage.Width, h = coverage.Height;
        coverage.Multiply(c.A / 255f);
        return new DrawingPlaneImage(new DrawingPlane(w, h, c.R / 255f), new DrawingPlane(w, h, c.G / 255f),
            new DrawingPlane(w, h, c.B / 255f), coverage);
    }

    //The plane moved by a (fractional) offset, sampled bilinearly; zero where it moved in from outside
    private static DrawingPlane Shift(DrawingPlane plane, float offsetX, float offsetY)
    {
        if (offsetX == 0f && offsetY == 0f) { return plane; }
        int w = plane.Width, h = plane.Height;
        float[] source = plane.Data;
        var result = new DrawingPlane(w, h);
        float[] target = result.Data;
        float fx = -offsetX, fy = -offsetY;
        int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
        float wx = fx - ix, wy = fy - iy;
        float w00 = (1f - wx) * (1f - wy), w10 = wx * (1f - wy), w01 = (1f - wx) * wy, w11 = wx * wy;
        for (int y = 0; y < h; y++)
        {
            int sy = y + iy;
            for (int x = 0; x < w; x++)
            {
                int sx = x + ix;
                float value = (Sample(source, w, h, sx, sy) * w00) + (Sample(source, w, h, sx + 1, sy) * w10)
                    + (Sample(source, w, h, sx, sy + 1) * w01) + (Sample(source, w, h, sx + 1, sy + 1) * w11);
                target[(y * w) + x] = value;
            }
        }
        return result;
    }

    private static float Sample(float[] data, int w, int h, int x, int y)
        => x < 0 || y < 0 || x >= w || y >= h ? 0f : data[(y * w) + x];

    //A bitmap's pixels as straight-alpha RGBA bytes (tightly packed)
    internal static byte[] ReadStraight(SKBitmap bitmap)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var bytes = new byte[width * height * 4];
        using SKImage image = SKImage.FromBitmap(bitmap);
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), width * 4, 0, 0))
            {
                throw new InvalidOperationException("The rendered pixels could not be read.");
            }
        }
        finally
        {
            handle.Free();
        }
        return bytes;
    }

    //Effect sizes on the device: the factor for radii and the shadow offset in pixels
    internal readonly struct EffectSizes
    {
        internal EffectSizes(float radiusScale, float offsetX, float offsetY)
        {
            RadiusScale = radiusScale;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        internal float RadiusScale { get; }

        internal float OffsetX { get; }

        internal float OffsetY { get; }
    }
}
