using System;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Samples a rasterized tile at a continuous position, wrapping each axis independently
/// with its own <see cref="DrawingShaderTileMode"/> and interpolating bilinearly. The four
/// surrounding texels are interpolated in premultiplied space, so a transparent texel never
/// bleeds a dark fringe into its neighbours, and each of them is wrapped on its own - which
/// is what keeps a repeating tile seamless across its edges instead of clamping to the
/// texels at the boundary.
/// </summary>
internal static class TileSampler
{
    /// <summary>
    /// Samples the tile at a position measured in tile pixels.
    /// </summary>
    /// <param name="tile">The rasterized tile.</param>
    /// <param name="x">The horizontal position, in tile pixels.</param>
    /// <param name="y">The vertical position, in tile pixels.</param>
    /// <param name="tileModeX">How positions outside the tile wrap horizontally.</param>
    /// <param name="tileModeY">How positions outside the tile wrap vertically.</param>
    /// <param name="red">The red component, 0..1, straight alpha.</param>
    /// <param name="green">The green component, 0..1, straight alpha.</param>
    /// <param name="blue">The blue component, 0..1, straight alpha.</param>
    /// <param name="alpha">The alpha component, 0..1.</param>
    public static void Sample(DrawingBitmap tile, float x, float y,
        DrawingShaderTileMode tileModeX, DrawingShaderTileMode tileModeY,
        out float red, out float green, out float blue, out float alpha)
    {
        //Texel centers sit at +0.5, so a sample at pixel center lands exactly on one texel
        float sampleX = x - 0.5f;
        float sampleY = y - 0.5f;
        int x0 = (int)MathF.Floor(sampleX);
        int y0 = (int)MathF.Floor(sampleY);
        float fractionX = sampleX - x0;
        float fractionY = sampleY - y0;

        SamplePremultiplied(tile, x0, y0, tileModeX, tileModeY,
            out float r00, out float g00, out float b00, out float a00);
        SamplePremultiplied(tile, x0 + 1, y0, tileModeX, tileModeY,
            out float r10, out float g10, out float b10, out float a10);
        SamplePremultiplied(tile, x0, y0 + 1, tileModeX, tileModeY,
            out float r01, out float g01, out float b01, out float a01);
        SamplePremultiplied(tile, x0 + 1, y0 + 1, tileModeX, tileModeY,
            out float r11, out float g11, out float b11, out float a11);

        float premultipliedRed = Interpolate(r00, r10, r01, r11, fractionX, fractionY);
        float premultipliedGreen = Interpolate(g00, g10, g01, g11, fractionX, fractionY);
        float premultipliedBlue = Interpolate(b00, b10, b01, b11, fractionX, fractionY);
        alpha = Interpolate(a00, a10, a01, a11, fractionX, fractionY);

        if (alpha > 0)
        {
            red = premultipliedRed / alpha;
            green = premultipliedGreen / alpha;
            blue = premultipliedBlue / alpha;
        }
        else
        {
            red = 0;
            green = 0;
            blue = 0;
        }
    }

    private static float Interpolate(float v00, float v10, float v01, float v11,
        float fractionX, float fractionY)
    {
        float top = v00 + ((v10 - v00) * fractionX);
        float bottom = v01 + ((v11 - v01) * fractionX);
        return top + ((bottom - top) * fractionY);
    }

    private static void SamplePremultiplied(DrawingBitmap tile, int x, int y,
        DrawingShaderTileMode tileModeX, DrawingShaderTileMode tileModeY,
        out float red, out float green, out float blue, out float alpha)
    {
        if (!TryWrap(x, tile.Width, tileModeX, out int texelX)
            || !TryWrap(y, tile.Height, tileModeY, out int texelY))
        {
            //Decal: outside the tile is transparent, so the edge fades instead of smearing
            red = 0;
            green = 0;
            blue = 0;
            alpha = 0;
            return;
        }

        byte[] pixels = tile.PixelBuffer;
        int offset = ((texelY * tile.Width) + texelX) * 4;
        byte c0 = pixels[offset];
        byte c1 = pixels[offset + 1];
        byte c2 = pixels[offset + 2];
        alpha = pixels[offset + 3] / 255f;

        red = (tile.IsBgra ? c2 : c0) / 255f * alpha;
        green = c1 / 255f * alpha;
        blue = (tile.IsBgra ? c0 : c2) / 255f * alpha;
    }

    /// <summary>
    /// Maps a texel index onto the tile according to a tile mode.
    /// </summary>
    /// <param name="value">The texel index, which may be outside the tile.</param>
    /// <param name="extent">The tile's size along this axis, in texels.</param>
    /// <param name="tileMode">How positions outside the tile behave.</param>
    /// <param name="result">The texel index inside the tile.</param>
    /// <returns>
    /// <c>true</c> when the position maps onto a texel; <c>false</c> when it falls outside a
    /// <see cref="DrawingShaderTileMode.Decal"/> tile and is therefore transparent.
    /// </returns>
    private static bool TryWrap(int value, int extent, DrawingShaderTileMode tileMode, out int result)
    {
        switch (tileMode)
        {
            case DrawingShaderTileMode.Repeat:
                result = ((value % extent) + extent) % extent;
                return true;

            case DrawingShaderTileMode.Mirror:
            {
                int period = extent * 2;
                int position = ((value % period) + period) % period;
                result = position < extent ? position : (period - 1 - position);
                return true;
            }

            case DrawingShaderTileMode.Decal:
                result = value;
                return value >= 0 && value < extent;

            default:
                result = Math.Clamp(value, 0, extent - 1);
                return true;
        }
    }
}
