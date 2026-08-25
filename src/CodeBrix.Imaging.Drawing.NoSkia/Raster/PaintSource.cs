using System;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Supplies the source color for each device pixel while geometry is filled - a solid
/// color, or a bitmap sampled through an inverse transform.
/// </summary>
internal abstract class PaintSource
{
    /// <summary>
    /// Gets the straight-alpha source color for the pixel whose center is at
    /// (<paramref name="x"/> + 0.5, <paramref name="y"/> + 0.5), as components in 0..1.
    /// </summary>
    /// <param name="x">The pixel's horizontal position.</param>
    /// <param name="y">The pixel's vertical position.</param>
    /// <param name="red">The red component, 0..1.</param>
    /// <param name="green">The green component, 0..1.</param>
    /// <param name="blue">The blue component, 0..1.</param>
    /// <param name="alpha">The alpha component, 0..1.</param>
    public abstract void GetColor(int x, int y, out float red, out float green, out float blue, out float alpha);
}

/// <summary>
/// A paint source producing one solid color everywhere.
/// </summary>
internal sealed class SolidPaintSource : PaintSource
{
    private readonly float _red;
    private readonly float _green;
    private readonly float _blue;
    private readonly float _alpha;

    /// <summary>
    /// Creates a solid paint source from a color.
    /// </summary>
    /// <param name="color">The color to paint with.</param>
    public SolidPaintSource(DrawingColor color)
    {
        _red = color.Red / 255f;
        _green = color.Green / 255f;
        _blue = color.Blue / 255f;
        _alpha = color.Alpha / 255f;
    }

    /// <inheritdoc />
    public override void GetColor(int x, int y, out float red, out float green, out float blue, out float alpha)
    {
        red = _red;
        green = _green;
        blue = _blue;
        alpha = _alpha;
    }
}

/// <summary>
/// A paint source that samples a bitmap: each device pixel is mapped through an inverse
/// transform into the bitmap's coordinate space and sampled with nearest or bilinear
/// filtering (bilinear interpolates premultiplied components so transparent texels never
/// bleed dark fringes). The paint alpha modulates the sampled alpha, exactly as a paint's
/// color alpha modulates a bitmap draw.
/// </summary>
internal sealed class BitmapPaintSource : PaintSource
{
    private readonly byte[] _pixels;
    private readonly int _width;
    private readonly int _height;
    private readonly bool _isBgra;
    private readonly Matrix3x2 _deviceToSource;
    private readonly bool _linear;
    private readonly float _alphaScale;
    private readonly int _clampMinX;
    private readonly int _clampMinY;
    private readonly int _clampMaxX;
    private readonly int _clampMaxY;

    /// <summary>
    /// Creates a bitmap paint source.
    /// </summary>
    /// <param name="bitmap">The bitmap to sample.</param>
    /// <param name="deviceToSource">The transform from device coordinates to bitmap coordinates.</param>
    /// <param name="linear"><c>true</c> for bilinear filtering; <c>false</c> for nearest-neighbor.</param>
    /// <param name="alphaScale">The paint alpha modulation, 0..1.</param>
    /// <param name="sourceBounds">
    /// An optional region of the bitmap that sampling is clamped to (for source-rectangle
    /// draws); <c>null</c> clamps to the whole bitmap.
    /// </param>
    public BitmapPaintSource(DrawingBitmap bitmap, Matrix3x2 deviceToSource, bool linear, float alphaScale,
        DrawingRect? sourceBounds = null)
    {
        _pixels = bitmap.PixelBuffer;
        _width = bitmap.Width;
        _height = bitmap.Height;
        _isBgra = bitmap.IsBgra;
        _deviceToSource = deviceToSource;
        _linear = linear;
        _alphaScale = alphaScale;

        if (sourceBounds.HasValue)
        {
            _clampMinX = Math.Clamp((int)sourceBounds.Value.Left, 0, _width - 1);
            _clampMinY = Math.Clamp((int)sourceBounds.Value.Top, 0, _height - 1);
            _clampMaxX = Math.Clamp((int)MathF.Ceiling(sourceBounds.Value.Right) - 1, _clampMinX, _width - 1);
            _clampMaxY = Math.Clamp((int)MathF.Ceiling(sourceBounds.Value.Bottom) - 1, _clampMinY, _height - 1);
        }
        else
        {
            _clampMinX = 0;
            _clampMinY = 0;
            _clampMaxX = _width - 1;
            _clampMaxY = _height - 1;
        }
    }

    /// <inheritdoc />
    public override void GetColor(int x, int y, out float red, out float green, out float blue, out float alpha)
    {
        Vector2 source = Vector2.Transform(new Vector2(x + 0.5f, y + 0.5f), _deviceToSource);

        if (!_linear)
        {
            SampleTexel((int)MathF.Floor(source.X), (int)MathF.Floor(source.Y),
                out red, out green, out blue, out alpha);
            alpha *= _alphaScale;
            return;
        }

        //Bilinear: interpolate the four surrounding texels in premultiplied space
        float sampleX = source.X - 0.5f;
        float sampleY = source.Y - 0.5f;
        int x0 = (int)MathF.Floor(sampleX);
        int y0 = (int)MathF.Floor(sampleY);
        float fx = sampleX - x0;
        float fy = sampleY - y0;

        SamplePremultiplied(x0, y0, out float r00, out float g00, out float b00, out float a00);
        SamplePremultiplied(x0 + 1, y0, out float r10, out float g10, out float b10, out float a10);
        SamplePremultiplied(x0, y0 + 1, out float r01, out float g01, out float b01, out float a01);
        SamplePremultiplied(x0 + 1, y0 + 1, out float r11, out float g11, out float b11, out float a11);

        float premulRed = Lerp2(r00, r10, r01, r11, fx, fy);
        float premulGreen = Lerp2(g00, g10, g01, g11, fx, fy);
        float premulBlue = Lerp2(b00, b10, b01, b11, fx, fy);
        alpha = Lerp2(a00, a10, a01, a11, fx, fy);

        if (alpha > 0)
        {
            red = premulRed / alpha;
            green = premulGreen / alpha;
            blue = premulBlue / alpha;
        }
        else
        {
            red = 0;
            green = 0;
            blue = 0;
        }
        alpha *= _alphaScale;
    }

    private static float Lerp2(float v00, float v10, float v01, float v11, float fx, float fy)
    {
        float top = v00 + ((v10 - v00) * fx);
        float bottom = v01 + ((v11 - v01) * fx);
        return top + ((bottom - top) * fy);
    }

    private void SamplePremultiplied(int x, int y, out float red, out float green, out float blue, out float alpha)
    {
        SampleTexel(x, y, out float straightRed, out float straightGreen, out float straightBlue, out alpha);
        red = straightRed * alpha;
        green = straightGreen * alpha;
        blue = straightBlue * alpha;
    }

    private void SampleTexel(int x, int y, out float red, out float green, out float blue, out float alpha)
    {
        x = Math.Clamp(x, _clampMinX, _clampMaxX);
        y = Math.Clamp(y, _clampMinY, _clampMaxY);

        int offset = ((y * _width) + x) * 4;
        byte c0 = _pixels[offset];
        byte c1 = _pixels[offset + 1];
        byte c2 = _pixels[offset + 2];
        byte a = _pixels[offset + 3];

        red = (_isBgra ? c2 : c0) / 255f;
        green = c1 / 255f;
        blue = (_isBgra ? c0 : c2) / 255f;
        alpha = a / 255f;
    }
}
