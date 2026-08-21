using System;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Pixel compositing for every <see cref="DrawingBlendMode"/>: the Porter-Duff operators,
/// the W3C separable blend modes, and the non-separable (hue/saturation/color/luminosity)
/// modes. All math runs on straight-alpha float components in 0..1.
/// </summary>
internal static class Blender
{
    /// <summary>
    /// Composites one source color over one destination color.
    /// </summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="sourceRed">The source red, 0..1 (straight alpha).</param>
    /// <param name="sourceGreen">The source green, 0..1.</param>
    /// <param name="sourceBlue">The source blue, 0..1.</param>
    /// <param name="sourceAlpha">The source alpha (already multiplied by coverage), 0..1.</param>
    /// <param name="destRed">The destination red, 0..1 (straight alpha).</param>
    /// <param name="destGreen">The destination green, 0..1.</param>
    /// <param name="destBlue">The destination blue, 0..1.</param>
    /// <param name="destAlpha">The destination alpha, 0..1.</param>
    /// <param name="outRed">The result red, 0..1 (straight alpha).</param>
    /// <param name="outGreen">The result green, 0..1.</param>
    /// <param name="outBlue">The result blue, 0..1.</param>
    /// <param name="outAlpha">The result alpha, 0..1.</param>
    public static void Blend(DrawingBlendMode mode,
        float sourceRed, float sourceGreen, float sourceBlue, float sourceAlpha,
        float destRed, float destGreen, float destBlue, float destAlpha,
        out float outRed, out float outGreen, out float outBlue, out float outAlpha)
    {
        //Premultiplied components
        float sr = sourceRed * sourceAlpha, sg = sourceGreen * sourceAlpha, sb = sourceBlue * sourceAlpha;
        float dr = destRed * destAlpha, dg = destGreen * destAlpha, db = destBlue * destAlpha;
        float sa = sourceAlpha, da = destAlpha;

        float pr, pg, pb, pa;
        switch (mode)
        {
            case DrawingBlendMode.Clear:
                pr = pg = pb = pa = 0;
                break;
            case DrawingBlendMode.Src:
                pr = sr; pg = sg; pb = sb; pa = sa;
                break;
            case DrawingBlendMode.Dst:
                pr = dr; pg = dg; pb = db; pa = da;
                break;
            case DrawingBlendMode.SrcOver:
                pr = sr + (dr * (1 - sa)); pg = sg + (dg * (1 - sa));
                pb = sb + (db * (1 - sa)); pa = sa + (da * (1 - sa));
                break;
            case DrawingBlendMode.DstOver:
                pr = dr + (sr * (1 - da)); pg = dg + (sg * (1 - da));
                pb = db + (sb * (1 - da)); pa = da + (sa * (1 - da));
                break;
            case DrawingBlendMode.SrcIn:
                pr = sr * da; pg = sg * da; pb = sb * da; pa = sa * da;
                break;
            case DrawingBlendMode.DstIn:
                pr = dr * sa; pg = dg * sa; pb = db * sa; pa = da * sa;
                break;
            case DrawingBlendMode.SrcOut:
                pr = sr * (1 - da); pg = sg * (1 - da); pb = sb * (1 - da); pa = sa * (1 - da);
                break;
            case DrawingBlendMode.DstOut:
                pr = dr * (1 - sa); pg = dg * (1 - sa); pb = db * (1 - sa); pa = da * (1 - sa);
                break;
            case DrawingBlendMode.SrcATop:
                pr = (sr * da) + (dr * (1 - sa)); pg = (sg * da) + (dg * (1 - sa));
                pb = (sb * da) + (db * (1 - sa)); pa = da;
                break;
            case DrawingBlendMode.DstATop:
                pr = (dr * sa) + (sr * (1 - da)); pg = (dg * sa) + (sg * (1 - da));
                pb = (db * sa) + (sb * (1 - da)); pa = sa;
                break;
            case DrawingBlendMode.Xor:
                pr = (sr * (1 - da)) + (dr * (1 - sa)); pg = (sg * (1 - da)) + (dg * (1 - sa));
                pb = (sb * (1 - da)) + (db * (1 - sa)); pa = (sa * (1 - da)) + (da * (1 - sa));
                break;
            case DrawingBlendMode.Plus:
                pr = Math.Min(1f, sr + dr); pg = Math.Min(1f, sg + dg);
                pb = Math.Min(1f, sb + db); pa = Math.Min(1f, sa + da);
                break;
            case DrawingBlendMode.Modulate:
                pr = sr * dr; pg = sg * dg; pb = sb * db; pa = sa * da;
                break;
            default:
                BlendFormula(mode, sourceRed, sourceGreen, sourceBlue, sa,
                    destRed, destGreen, destBlue, da, out pr, out pg, out pb, out pa);
                break;
        }

        outAlpha = Math.Clamp(pa, 0f, 1f);
        if (outAlpha <= 0)
        {
            outRed = outGreen = outBlue = 0;
            return;
        }
        outRed = Math.Clamp(pr / outAlpha, 0f, 1f);
        outGreen = Math.Clamp(pg / outAlpha, 0f, 1f);
        outBlue = Math.Clamp(pb / outAlpha, 0f, 1f);
    }

    //W3C compositing-and-blending: Co = (1-ab)·as·Cs + (1-as)·ab·Cb + as·ab·B(Cb, Cs),
    //composited with source-over alpha
    private static void BlendFormula(DrawingBlendMode mode,
        float cs0, float cs1, float cs2, float sa,
        float cb0, float cb1, float cb2, float da,
        out float pr, out float pg, out float pb, out float pa)
    {
        float b0, b1, b2;
        if (mode == DrawingBlendMode.Hue || mode == DrawingBlendMode.Saturation
            || mode == DrawingBlendMode.Color || mode == DrawingBlendMode.Luminosity)
        {
            NonSeparable(mode, cs0, cs1, cs2, cb0, cb1, cb2, out b0, out b1, out b2);
        }
        else
        {
            b0 = Separable(mode, cb0, cs0);
            b1 = Separable(mode, cb1, cs1);
            b2 = Separable(mode, cb2, cs2);
        }

        pr = ((1 - da) * sa * cs0) + ((1 - sa) * da * cb0) + (sa * da * b0);
        pg = ((1 - da) * sa * cs1) + ((1 - sa) * da * cb1) + (sa * da * b1);
        pb = ((1 - da) * sa * cs2) + ((1 - sa) * da * cb2) + (sa * da * b2);
        pa = sa + (da * (1 - sa));
    }

    private static float Separable(DrawingBlendMode mode, float backdrop, float source)
    {
        switch (mode)
        {
            case DrawingBlendMode.Multiply:
                return backdrop * source;
            case DrawingBlendMode.Screen:
                return backdrop + source - (backdrop * source);
            case DrawingBlendMode.Overlay:
                return Separable(DrawingBlendMode.HardLight, source, backdrop);
            case DrawingBlendMode.Darken:
                return Math.Min(backdrop, source);
            case DrawingBlendMode.Lighten:
                return Math.Max(backdrop, source);
            case DrawingBlendMode.ColorDodge:
                if (backdrop <= 0) { return 0; }
                return source >= 1 ? 1 : Math.Min(1f, backdrop / (1 - source));
            case DrawingBlendMode.ColorBurn:
                if (backdrop >= 1) { return 1; }
                return source <= 0 ? 0 : 1 - Math.Min(1f, (1 - backdrop) / source);
            case DrawingBlendMode.HardLight:
                return source <= 0.5f
                    ? backdrop * 2 * source
                    : Separable(DrawingBlendMode.Screen, backdrop, (2 * source) - 1);
            case DrawingBlendMode.SoftLight:
            {
                if (source <= 0.5f)
                {
                    return backdrop - ((1 - (2 * source)) * backdrop * (1 - backdrop));
                }
                float d = backdrop <= 0.25f
                    ? ((((16 * backdrop) - 12) * backdrop) + 4) * backdrop
                    : MathF.Sqrt(backdrop);
                return backdrop + (((2 * source) - 1) * (d - backdrop));
            }
            case DrawingBlendMode.Difference:
                return Math.Abs(backdrop - source);
            case DrawingBlendMode.Exclusion:
                return backdrop + source - (2 * backdrop * source);
            default:
                return source; //Unknown modes behave as normal
        }
    }

    private static void NonSeparable(DrawingBlendMode mode,
        float cs0, float cs1, float cs2, float cb0, float cb1, float cb2,
        out float r, out float g, out float b)
    {
        switch (mode)
        {
            case DrawingBlendMode.Hue:
                SetLum(SetSatColor(cs0, cs1, cs2, Sat(cb0, cb1, cb2)), Lum(cb0, cb1, cb2), out r, out g, out b);
                break;
            case DrawingBlendMode.Saturation:
                SetLum(SetSatColor(cb0, cb1, cb2, Sat(cs0, cs1, cs2)), Lum(cb0, cb1, cb2), out r, out g, out b);
                break;
            case DrawingBlendMode.Color:
                SetLum((cs0, cs1, cs2), Lum(cb0, cb1, cb2), out r, out g, out b);
                break;
            default: //Luminosity
                SetLum((cb0, cb1, cb2), Lum(cs0, cs1, cs2), out r, out g, out b);
                break;
        }
    }

    private static float Lum(float r, float g, float b) => (0.3f * r) + (0.59f * g) + (0.11f * b);

    private static float Sat(float r, float g, float b)
        => Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));

    private static (float R, float G, float B) SetSatColor(float r, float g, float b, float saturation)
    {
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        if (max <= min) { return (0, 0, 0); }

        float Rescale(float channel) => (channel - min) * saturation / (max - min);
        return (Rescale(r), Rescale(g), Rescale(b));
    }

    private static void SetLum((float R, float G, float B) color, float lum,
        out float r, out float g, out float b)
    {
        float delta = lum - Lum(color.R, color.G, color.B);
        r = color.R + delta;
        g = color.G + delta;
        b = color.B + delta;

        //Clip back into gamut
        float currentLum = Lum(r, g, b);
        float min = Math.Min(r, Math.Min(g, b));
        float max = Math.Max(r, Math.Max(g, b));
        if (min < 0)
        {
            float scale = currentLum / (currentLum - min);
            r = currentLum + ((r - currentLum) * scale);
            g = currentLum + ((g - currentLum) * scale);
            b = currentLum + ((b - currentLum) * scale);
        }
        if (max > 1)
        {
            float scale = (1 - currentLum) / (max - currentLum);
            r = currentLum + ((r - currentLum) * scale);
            g = currentLum + ((g - currentLum) * scale);
            b = currentLum + ((b - currentLum) * scale);
        }
    }
}
