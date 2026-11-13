using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// A float colour image made of four <see cref="DrawingPlane"/> values - <see cref="R"/>,
/// <see cref="G"/>, <see cref="B"/> and <see cref="A"/> - for painting frames in open-ended float
/// colour (lights may add past 1.0; a tone curve or the 8-bit conversion brings them back).
/// <para>
/// ALPHA IS STRAIGHT (not premultiplied): R, G and B hold the colour as it would look fully
/// opaque, and A holds coverage, normally 0..1. A new image is transparent black (all zeros).
/// The rules every operation follows:
/// </para>
/// <list type="bullet">
///   <item>Colour arithmetic (<see cref="Add(DrawingPlaneImage, float)"/>, <see cref="Multiply(float)"/>,
///   <see cref="Min(float)"/>, <see cref="Clamp(float, float)"/>, <see cref="Apply(Func{float, float})"/>,
///   tone curve, gamma, vignette) changes R, G and B only and leaves A alone. Work on
///   <see cref="A"/> directly to change alpha.</item>
///   <item>Compositing (<see cref="CompositeOver(DrawingPlaneImage, float)"/>, <see cref="CompositeAdd(DrawingPlaneImage, float)"/>,
///   <see cref="BlendColor(Color, DrawingPlane)"/>, <see cref="BlendImage(DrawingPlaneImage, DrawingPlane)"/>)
///   is Porter-Duff arithmetic on alpha clamped to 0..1, with straight colour in and out.</item>
///   <item>Mixing two images (<see cref="Lerp(DrawingPlaneImage, float)"/>, the transitions) and
///   every filter that combines neighbouring pixels (blur, bilinear resize, downsample, bloom)
///   works in PREMULTIPLIED space internally, so transparent pixels never bleed a dark fringe;
///   colour comes back as premultiplied / alpha, and 0 where alpha is 0. When the alphas involved
///   are all equal (for example a fully opaque image) the colour planes are processed directly -
///   mathematically the same result, with no rounding drift - and A is left as it is.</item>
/// </list>
/// <para>
/// Like <see cref="DrawingPlane"/>, same-size operations work IN PLACE and return this image;
/// size-changing ones return a new image. All arithmetic is managed code with a documented
/// order, so both drawing packages produce bit-identical results.
/// </para>
/// </summary>
public sealed class DrawingPlaneImage
{
    /// <summary>
    /// Creates a transparent black image (every value 0).
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is less than 1.</exception>
    public DrawingPlaneImage(int width, int height)
    {
        R = new DrawingPlane(width, height);
        G = new DrawingPlane(width, height);
        B = new DrawingPlane(width, height);
        A = new DrawingPlane(width, height);
    }

    /// <summary>
    /// Creates a transparent black image (every value 0).
    /// </summary>
    /// <param name="size">The size in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is less than 1.</exception>
    public DrawingPlaneImage(Size size)
        : this(size.Width, size.Height)
    {
    }

    /// <summary>
    /// Creates an image over four existing planes WITHOUT copying them - the image's planes ARE
    /// the given instances.
    /// </summary>
    /// <param name="red">The red plane.</param>
    /// <param name="green">The green plane (same size).</param>
    /// <param name="blue">The blue plane (same size).</param>
    /// <param name="alpha">The straight alpha plane (same size).</param>
    /// <exception cref="ArgumentNullException">Thrown when any plane is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ or a plane is passed twice.</exception>
    public DrawingPlaneImage(DrawingPlane red, DrawingPlane green, DrawingPlane blue, DrawingPlane alpha)
    {
        if (red == null) { throw new ArgumentNullException(nameof(red)); }
        red.RequireSameSize(green, nameof(green));
        red.RequireSameSize(blue, nameof(blue));
        red.RequireSameSize(alpha, nameof(alpha));
        if (ReferenceEquals(red, green) || ReferenceEquals(red, blue) || ReferenceEquals(red, alpha)
            || ReferenceEquals(green, blue) || ReferenceEquals(green, alpha) || ReferenceEquals(blue, alpha))
        {
            throw new ArgumentException("Each channel needs its own plane.", nameof(alpha));
        }
        R = red;
        G = green;
        B = blue;
        A = alpha;
    }

    /// <summary>The red plane (straight colour).</summary>
    public DrawingPlane R { get; }

    /// <summary>The green plane (straight colour).</summary>
    public DrawingPlane G { get; }

    /// <summary>The blue plane (straight colour).</summary>
    public DrawingPlane B { get; }

    /// <summary>The alpha (coverage) plane; 0 = transparent, 1 = opaque.</summary>
    public DrawingPlane A { get; }

    /// <summary>The width in pixels.</summary>
    public int Width => R.Width;

    /// <summary>The height in pixels.</summary>
    public int Height => R.Height;

    /// <summary>The size in pixels.</summary>
    public Size Size => R.Size;

    /// <summary>
    /// Reads one pixel as (X = red, Y = green, Z = blue, W = alpha), straight alpha.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The pixel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the position is outside the image.</exception>
    public Vector4 GetPixel(int x, int y) => new Vector4(R[x, y], G[x, y], B[x, y], A[x, y]);

    /// <summary>
    /// Writes one pixel from (X = red, Y = green, Z = blue, W = alpha), straight alpha.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="value">The pixel.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the position is outside the image.</exception>
    public void SetPixel(int x, int y, Vector4 value)
    {
        R[x, y] = value.X;
        G[x, y] = value.Y;
        B[x, y] = value.Z;
        A[x, y] = value.W;
    }

    /// <summary>
    /// Makes an independent copy of the image (all four planes are copied).
    /// </summary>
    /// <returns>A new image with the same size and values.</returns>
    public DrawingPlaneImage Clone() => new DrawingPlaneImage(R.Clone(), G.Clone(), B.Clone(), A.Clone());

    /// <summary>
    /// Copies every value into another image of the same size.
    /// </summary>
    /// <param name="destination">The image to overwrite.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public void CopyTo(DrawingPlaneImage destination)
    {
        RequireSameSize(destination, nameof(destination));
        R.CopyTo(destination.R);
        G.CopyTo(destination.G);
        B.CopyTo(destination.B);
        A.CopyTo(destination.A);
    }

    //---------------------------------------------------------------------------------------------
    //Filling

    /// <summary>
    /// Makes the whole image transparent black (every value 0).
    /// </summary>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Clear()
    {
        R.Clear();
        G.Clear();
        B.Clear();
        A.Clear();
        return this;
    }

    /// <summary>
    /// Fills the whole image with one colour. A CodeBrix.Imaging <see cref="Color"/> is taken
    /// through its 8-bit form: each channel is <c>byte / 255f</c>, alpha included.
    /// </summary>
    /// <param name="color">The colour (straight alpha).</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Fill(Color color)
    {
        Vector4 c = ToVector(color);
        return Fill(c.X, c.Y, c.Z, c.W);
    }

    /// <summary>
    /// Fills the whole image with one colour given as float channels (straight alpha, open-ended).
    /// </summary>
    /// <param name="red">The red value.</param>
    /// <param name="green">The green value.</param>
    /// <param name="blue">The blue value.</param>
    /// <param name="alpha">The alpha value; 1 = opaque.</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Fill(float red, float green, float blue, float alpha = 1f)
    {
        R.Fill(red);
        G.Fill(green);
        B.Fill(blue);
        A.Fill(alpha);
        return this;
    }

    /// <summary>
    /// Fills a rectangle with one colour (8-bit channels divided by 255). The rectangle is clipped
    /// to the image.
    /// </summary>
    /// <param name="area">The pixel rectangle.</param>
    /// <param name="color">The colour (straight alpha).</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Fill(Rectangle area, Color color)
    {
        Vector4 c = ToVector(color);
        R.Fill(area, c.X);
        G.Fill(area, c.Y);
        B.Fill(area, c.Z);
        A.Fill(area, c.W);
        return this;
    }

    /// <summary>
    /// Sets every pixel from a function of its position, row by row from the top on the calling
    /// thread. The function returns (X = red, Y = green, Z = blue, W = alpha), straight alpha.
    /// </summary>
    /// <param name="function">Receives (x, y) and returns the pixel.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlaneImage Fill(Func<int, int, Vector4> function)
        => Fill(new Rectangle(0, 0, Width, Height), function);

    /// <summary>
    /// Sets every pixel inside a rectangle from a function of its (absolute) position. The
    /// rectangle is clipped to the image.
    /// </summary>
    /// <param name="area">The pixel rectangle.</param>
    /// <param name="function">Receives (x, y) and returns the pixel (X = red, Y = green, Z = blue, W = alpha).</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlaneImage Fill(Rectangle area, Func<int, int, Vector4> function)
    {
        if (function == null) { throw new ArgumentNullException(nameof(function)); }
        if (!R.Clip(area, out int x0, out int y0, out int x1, out int y1)) { return this; }
        for (int y = y0; y < y1; y++) { FillRow(function, y, x0, x1); }
        return this;
    }

    /// <summary>
    /// Sets every pixel from a function of its position with the rows shared out across threads.
    /// Each pixel is computed exactly once, so the result equals <see cref="Fill(Func{int, int, Vector4})"/>
    /// whatever the thread count - provided the function is pure and thread-safe.
    /// </summary>
    /// <param name="function">Receives (x, y) and returns the pixel (X = red, Y = green, Z = blue, W = alpha).</param>
    /// <param name="maxDegreeOfParallelism">The most threads to use; 0 or less means no limit.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlaneImage FillParallel(Func<int, int, Vector4> function, int maxDegreeOfParallelism = 0)
        => FillParallel(new Rectangle(0, 0, Width, Height), function, maxDegreeOfParallelism);

    /// <summary>
    /// Sets every pixel inside a rectangle from a function of its position with the rows shared
    /// out across threads (same determinism rule as <see cref="FillParallel(Func{int, int, Vector4}, int)"/>).
    /// </summary>
    /// <param name="area">The pixel rectangle; clipped to the image.</param>
    /// <param name="function">Receives (x, y) and returns the pixel.</param>
    /// <param name="maxDegreeOfParallelism">The most threads to use; 0 or less means no limit.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlaneImage FillParallel(Rectangle area, Func<int, int, Vector4> function, int maxDegreeOfParallelism = 0)
    {
        if (function == null) { throw new ArgumentNullException(nameof(function)); }
        if (!R.Clip(area, out int x0, out int y0, out int x1, out int y1)) { return this; }
        PlaneKernels.ForRows(y1 - y0, int.MaxValue, i => FillRow(function, y0 + i, x0, x1),
            maxDegreeOfParallelism == 0 ? -1 : maxDegreeOfParallelism);
        return this;
    }

    //---------------------------------------------------------------------------------------------
    //Colour arithmetic (R, G, B only; alpha untouched)

    /// <summary>
    /// Adds another image's colour, optionally scaled: <c>C = C + other.C * scale</c> for R, G and B.
    /// Alpha (of both images) is ignored and left unchanged - this is light arithmetic. For
    /// alpha-aware additive compositing use <see cref="CompositeAdd(DrawingPlaneImage, float)"/>.
    /// </summary>
    /// <param name="other">The image to add (same size).</param>
    /// <param name="scale">The factor applied to <paramref name="other"/> first.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage Add(DrawingPlaneImage other, float scale = 1f)
    {
        RequireSameSize(other, nameof(other));
        R.Add(other.R, scale);
        G.Add(other.G, scale);
        B.Add(other.B, scale);
        return this;
    }

    /// <summary>
    /// Adds constants to the colour channels (alpha unchanged).
    /// </summary>
    /// <param name="red">Added to R.</param>
    /// <param name="green">Added to G.</param>
    /// <param name="blue">Added to B.</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Add(float red, float green, float blue)
    {
        R.Add(red);
        G.Add(green);
        B.Add(blue);
        return this;
    }

    /// <summary>
    /// Scales the colour channels by one factor (alpha unchanged).
    /// </summary>
    /// <param name="factor">The factor.</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Multiply(float factor) => Multiply(factor, factor, factor);

    /// <summary>
    /// Scales each colour channel by its own factor (alpha unchanged) - a colour tint.
    /// </summary>
    /// <param name="red">The red factor.</param>
    /// <param name="green">The green factor.</param>
    /// <param name="blue">The blue factor.</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Multiply(float red, float green, float blue)
    {
        R.Multiply(red);
        G.Multiply(green);
        B.Multiply(blue);
        return this;
    }

    /// <summary>
    /// Multiplies the colour channels by a plane, pixel by pixel - a vignette, a texture or a
    /// shading plane (alpha unchanged; to mask coverage multiply <see cref="A"/> instead).
    /// </summary>
    /// <param name="factor">The per-pixel factor (same size).</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factor"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage Multiply(DrawingPlane factor)
    {
        R.Multiply(factor);
        G.Multiply(factor);
        B.Multiply(factor);
        return this;
    }

    /// <summary>
    /// Multiplies the colour channels by another image's colour channels, pixel by pixel
    /// (alpha of both images ignored and left unchanged).
    /// </summary>
    /// <param name="other">The image to multiply by (same size).</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage Multiply(DrawingPlaneImage other)
    {
        RequireSameSize(other, nameof(other));
        R.Multiply(other.R);
        G.Multiply(other.G);
        B.Multiply(other.B);
        return this;
    }

    /// <summary>
    /// Caps the colour channels at <paramref name="value"/> (alpha unchanged).
    /// </summary>
    /// <param name="value">The upper limit.</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Min(float value)
    {
        R.Min(value);
        G.Min(value);
        B.Min(value);
        return this;
    }

    /// <summary>
    /// Raises the colour channels to at least <paramref name="value"/> (alpha unchanged).
    /// </summary>
    /// <param name="value">The lower limit.</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage Max(float value)
    {
        R.Max(value);
        G.Max(value);
        B.Max(value);
        return this;
    }

    /// <summary>
    /// Limits the colour channels to <paramref name="min"/> .. <paramref name="max"/> (alpha
    /// unchanged; NaN becomes <paramref name="min"/>).
    /// </summary>
    /// <param name="min">The lowest allowed value.</param>
    /// <param name="max">The highest allowed value.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    public DrawingPlaneImage Clamp(float min, float max)
    {
        R.Clamp(min, max);
        G.Clamp(min, max);
        B.Clamp(min, max);
        return this;
    }

    /// <summary>
    /// Applies one per-value function to R, G and B (alpha unchanged).
    /// </summary>
    /// <param name="function">Receives a value and returns its replacement.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlaneImage Apply(Func<float, float> function) => Apply(function, function, function);

    /// <summary>
    /// Applies a separate per-value function to each colour channel - a general per-channel tone
    /// curve (alpha unchanged).
    /// </summary>
    /// <param name="red">The red curve.</param>
    /// <param name="green">The green curve.</param>
    /// <param name="blue">The blue curve.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any function is null.</exception>
    public DrawingPlaneImage Apply(Func<float, float> red, Func<float, float> green, Func<float, float> blue)
    {
        if (red == null) { throw new ArgumentNullException(nameof(red)); }
        if (green == null) { throw new ArgumentNullException(nameof(green)); }
        if (blue == null) { throw new ArgumentNullException(nameof(blue)); }
        R.Apply(red);
        G.Apply(green);
        B.Apply(blue);
        return this;
    }

    /// <summary>
    /// Applies the tone curve <c>v / (1 + k * v) * gain</c> to R, G and B (alpha unchanged) - see
    /// <see cref="DrawingPlane.ApplyToneCurve(float, float)"/>.
    /// </summary>
    /// <param name="k">The shoulder strength (e.g. 0.15).</param>
    /// <param name="gain">The final multiplier (e.g. 1.08).</param>
    /// <returns>This image.</returns>
    public DrawingPlaneImage ApplyToneCurve(float k, float gain)
    {
        R.ApplyToneCurve(k, gain);
        G.ApplyToneCurve(k, gain);
        B.ApplyToneCurve(k, gain);
        return this;
    }

    /// <summary>
    /// Raises positive R, G and B values to a power (alpha unchanged) - see
    /// <see cref="DrawingPlane.ApplyGamma(float)"/>.
    /// </summary>
    /// <param name="exponent">The power; must be greater than 0.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="exponent"/> is not greater than 0.</exception>
    public DrawingPlaneImage ApplyGamma(float exponent)
    {
        R.ApplyGamma(exponent);
        G.ApplyGamma(exponent);
        B.ApplyGamma(exponent);
        return this;
    }

    /// <summary>
    /// Darkens R, G and B towards the edges with the factor of
    /// <see cref="DrawingPlane.CreateVignette(int, int, double, double, double, double, double, double)"/>
    /// (alpha unchanged).
    /// </summary>
    /// <param name="centerX">The vignette centre's x, in pixels.</param>
    /// <param name="centerY">The vignette centre's y, in pixels.</param>
    /// <param name="radiusX">The horizontal distance at which the normalised distance reaches 1.</param>
    /// <param name="radiusY">The vertical distance at which the normalised distance reaches 1.</param>
    /// <param name="strength">How much the factor falls per unit of squared normalised distance.</param>
    /// <param name="minimum">The lowest factor.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a radius is not greater than 0.</exception>
    public DrawingPlaneImage ApplyVignette(double centerX, double centerY, double radiusX, double radiusY, double strength, double minimum = 0.0)
        => Multiply(DrawingPlane.CreateVignette(Width, Height, centerX, centerY, radiusX, radiusY, strength, minimum));

    //---------------------------------------------------------------------------------------------
    //Mixing and compositing

    /// <summary>
    /// Mixes this image towards another by a fraction, in premultiplied space (so a transparent
    /// pixel contributes no colour). Per pixel, with <c>s = 1 - t</c>: when the two alphas are
    /// equal, <c>C = Ca * s + Cb * t</c> and alpha is kept; otherwise <c>A = Aa * s + Ab * t</c>
    /// and <c>C = (Ca * Aa * s + Cb * Ab * t) / A</c> (all zeros when A is 0). A t of 0 or less
    /// leaves this image unchanged; 1 or more copies <paramref name="other"/> exactly.
    /// </summary>
    /// <param name="other">The image to move towards (same size).</param>
    /// <param name="t">The fraction.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage Lerp(DrawingPlaneImage other, float t)
    {
        RequireSameSize(other, nameof(other));
        MixInto(this, other, this, t, null);
        return this;
    }

    /// <summary>
    /// Mixes this image towards another by a per-pixel fraction from a mask, with the same
    /// premultiplied rule as <see cref="Lerp(DrawingPlaneImage, float)"/>.
    /// </summary>
    /// <param name="other">The image to move towards (same size).</param>
    /// <param name="mask">The per-pixel fraction (same size); at or below 0 keeps this pixel, at or above 1 takes the other.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage Lerp(DrawingPlaneImage other, DrawingPlane mask)
    {
        RequireSameSize(other, nameof(other));
        R.RequireSameSize(mask, nameof(mask));
        MixInto(this, other, this, 0f, mask);
        return this;
    }

    /// <summary>
    /// Composites another image OVER this one (Porter-Duff source-over), with an overall opacity.
    /// Per pixel, with alphas clamped to 0..1: <c>as = clamp(source.A * opacity)</c>,
    /// <c>ad = clamp(A)</c>; <c>Ao = as + ad * (1 - as)</c> (evaluated as <c>ad + as * (1 - ad)</c>
    /// when ad is the larger, so an opaque side gives exactly 1);
    /// <c>C = (Cs * as + Cd * ad * (1 - as)) / Ao</c>. Shortcuts that are exactly equal to the
    /// formula: as = 0 leaves the pixel unchanged; as = 1 or ad = 0 copies the source colour.
    /// Over an opaque pixel this is <c>Cs * as + Cd * (1 - as)</c>.
    /// </summary>
    /// <param name="source">The image on top (same size).</param>
    /// <param name="opacity">Multiplies the source alpha; 1 = as is.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage CompositeOver(DrawingPlaneImage source, float opacity = 1f)
    {
        RequireSameSize(source, nameof(source));
        float[] sr = source.R.Data, sg = source.G.Data, sb = source.B.Data, sa = source.A.Data;
        ForEachPixel((i, dr, dg, db, da) => Over(dr, dg, db, da, i, sr[i], sg[i], sb[i], sa[i] * opacity));
        return this;
    }

    /// <summary>
    /// Composites another image onto this one ADDITIVELY (Porter-Duff plus, "lighter"), with an
    /// overall opacity: per pixel, with alphas clamped to 0..1, <c>Ao = min(1, as + ad)</c> and
    /// <c>C = (Cs * as + Cd * ad) / Ao</c>; as = 0 leaves the pixel unchanged and ad = 0 copies the
    /// source colour. Over an opaque pixel this is <c>Cd + Cs * as</c> - lights adding up, now
    /// respecting the light's own alpha. Use it for glows made with <see cref="CreateBloom"/>.
    /// </summary>
    /// <param name="source">The image to add (same size).</param>
    /// <param name="opacity">Multiplies the source alpha; 1 = as is.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage CompositeAdd(DrawingPlaneImage source, float opacity = 1f)
    {
        RequireSameSize(source, nameof(source));
        float[] sr = source.R.Data, sg = source.G.Data, sb = source.B.Data, sa = source.A.Data;
        ForEachPixel((i, dr, dg, db, da) =>
        {
            float a = PlaneKernels.Clamp01(sa[i] * opacity);
            if (!(a > 0f)) { return; }
            float d = PlaneKernels.Clamp01(da[i]);
            if (!(d > 0f))
            {
                dr[i] = sr[i];
                dg[i] = sg[i];
                db[i] = sb[i];
                da[i] = a;
                return;
            }
            float ao = a + d;
            if (ao > 1f) { ao = 1f; }
            dr[i] = ((sr[i] * a) + (dr[i] * d)) / ao;
            dg[i] = ((sg[i] * a) + (dg[i] * d)) / ao;
            db[i] = ((sb[i] * a) + (db[i] * d)) / ao;
            da[i] = ao;
        });
        return this;
    }

    /// <summary>
    /// Paints a colour through a coverage mask: per pixel, source-over (see
    /// <see cref="CompositeOver(DrawingPlaneImage, float)"/>) of <paramref name="color"/> with
    /// source alpha <c>color.A * mask</c>. Over an opaque pixel with an opaque colour this is
    /// <c>C * (1 - m) + color * m</c>.
    /// </summary>
    /// <param name="color">The colour (8-bit channels divided by 255, straight alpha).</param>
    /// <param name="mask">The per-pixel coverage, normally 0..1 (same size).</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage BlendColor(Color color, DrawingPlane mask)
    {
        Vector4 c = ToVector(color);
        return BlendColor(c.X, c.Y, c.Z, c.W, mask);
    }

    /// <summary>
    /// Paints a float colour through a coverage mask (see <see cref="BlendColor(Color, DrawingPlane)"/>).
    /// </summary>
    /// <param name="red">The red value.</param>
    /// <param name="green">The green value.</param>
    /// <param name="blue">The blue value.</param>
    /// <param name="alpha">The colour's own alpha.</param>
    /// <param name="mask">The per-pixel coverage, normally 0..1 (same size).</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage BlendColor(float red, float green, float blue, float alpha, DrawingPlane mask)
    {
        R.RequireSameSize(mask, nameof(mask));
        float[] m = mask.Data;
        ForEachPixel((i, dr, dg, db, da) => Over(dr, dg, db, da, i, red, green, blue, alpha * m[i]));
        return this;
    }

    /// <summary>
    /// Paints another image through a coverage mask: per pixel, source-over of
    /// <paramref name="source"/> with source alpha <c>source.A * mask</c>.
    /// </summary>
    /// <param name="source">The image to paint (same size).</param>
    /// <param name="mask">The per-pixel coverage, normally 0..1 (same size).</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlaneImage BlendImage(DrawingPlaneImage source, DrawingPlane mask)
    {
        RequireSameSize(source, nameof(source));
        R.RequireSameSize(mask, nameof(mask));
        float[] sr = source.R.Data, sg = source.G.Data, sb = source.B.Data, sa = source.A.Data, m = mask.Data;
        ForEachPixel((i, dr, dg, db, da) => Over(dr, dg, db, da, i, sr[i], sg[i], sb[i], sa[i] * m[i]));
        return this;
    }

    //---------------------------------------------------------------------------------------------
    //Filters

    /// <summary>
    /// Blurs the image with the Gaussian of <see cref="DrawingPlane.GaussianBlur(float)"/>, in place.
    /// When every alpha value is equal the colour planes are blurred directly and alpha is kept;
    /// otherwise R*A, G*A, B*A and A are blurred and colour becomes blurred(C*A) / blurred(A)
    /// (0 where that alpha is 0).
    /// </summary>
    /// <param name="sigma">The standard deviation in pixels; 0 or less does nothing.</param>
    /// <returns>This image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sigma"/> is NaN or infinite.</exception>
    public DrawingPlaneImage GaussianBlur(float sigma)
    {
        DrawingPlane.ValidateSigma(sigma);
        if (!(sigma > 0f)) { return this; }
        if (HasUniformAlpha())
        {
            R.GaussianBlur(sigma);
            G.GaussianBlur(sigma);
            B.GaussianBlur(sigma);
            return this;
        }
        Premultiply(this, this);
        R.GaussianBlur(sigma);
        G.GaussianBlur(sigma);
        B.GaussianBlur(sigma);
        A.GaussianBlur(sigma);
        Unpremultiply(this);
        return this;
    }

    /// <summary>
    /// Returns a resized copy (this image is unchanged), with the sampling grids of
    /// <see cref="DrawingPlaneResampling"/>. Nearest copies all four channels exactly; the bilinear
    /// modes interpolate in premultiplied space unless every alpha value is equal.
    /// </summary>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    /// <param name="resampling">How source values are chosen.</param>
    /// <returns>A new image of the requested size.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1 or the mode is unknown.</exception>
    public DrawingPlaneImage Resized(int width, int height, DrawingPlaneResampling resampling = DrawingPlaneResampling.Bilinear)
    {
        if (resampling == DrawingPlaneResampling.Nearest)
        {
            return new DrawingPlaneImage(R.Resized(width, height, resampling), G.Resized(width, height, resampling),
                B.Resized(width, height, resampling), A.Resized(width, height, resampling));
        }
        return Spatial(p => p.Resized(width, height, resampling));
    }

    /// <summary>
    /// Returns a copy shrunk by a whole factor with the box filter of
    /// <see cref="DrawingPlane.Downsampled(int)"/> (premultiplied unless every alpha value is equal).
    /// </summary>
    /// <param name="factor">The shrink factor, 1 or more.</param>
    /// <returns>A new, smaller image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="factor"/> is less than 1.</exception>
    public DrawingPlaneImage Downsampled(int factor)
    {
        if (factor < 1) { throw new ArgumentOutOfRangeException(nameof(factor)); }
        return Spatial(p => p.Downsampled(factor));
    }

    /// <summary>
    /// Builds a glow image with the steps of <see cref="DrawingPlane.CreateBloom"/> (this image
    /// is unchanged). When every alpha value is equal, only the colour planes are bloomed and the
    /// result keeps that alpha - add it with <see cref="Add(DrawingPlaneImage, float)"/> for the
    /// classic "lights glow" look on an opaque scene. Otherwise colour and alpha are bloomed in
    /// premultiplied space, giving a straight-alpha glow that spreads its own coverage - composite
    /// it with <see cref="CompositeAdd(DrawingPlaneImage, float)"/> or
    /// <see cref="CompositeOver(DrawingPlaneImage, float)"/>, which also works over transparency.
    /// </summary>
    /// <param name="downsampleFactor">The box-downsample factor before blurring.</param>
    /// <param name="blurs">The (Sigma, Weight) blurs to sum, sigmas in small-image pixels.</param>
    /// <param name="gain">The factor applied to the summed glow (to colour only).</param>
    /// <param name="outputSize">The size of the returned image; null means this image's size.</param>
    /// <param name="upsampling">How the small glow is enlarged again.</param>
    /// <returns>A new image holding only the glow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blurs"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="blurs"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor is less than 1 or a sigma is invalid.</exception>
    public DrawingPlaneImage CreateBloom(int downsampleFactor, IReadOnlyList<(float Sigma, float Weight)> blurs, float gain = 1f,
        Size? outputSize = null, DrawingPlaneResampling upsampling = DrawingPlaneResampling.Bilinear)
    {
        DrawingPlane.ValidateBlurs(blurs);
        if (HasUniformAlpha())
        {
            Size size = outputSize ?? Size;
            return new DrawingPlaneImage(
                R.CreateBloom(downsampleFactor, blurs, gain, outputSize, upsampling),
                G.CreateBloom(downsampleFactor, blurs, gain, outputSize, upsampling),
                B.CreateBloom(downsampleFactor, blurs, gain, outputSize, upsampling),
                new DrawingPlane(size.Width, size.Height, A.Data[0]));
        }

        //Premultiplied: bloom C*A with the gain and A without it; dividing gives a glow whose
        //  colour is the alpha-weighted light colour times the gain
        DrawingPlaneImage premultiplied = Clone();
        Premultiply(premultiplied, premultiplied);
        var result = new DrawingPlaneImage(
            premultiplied.R.CreateBloom(downsampleFactor, blurs, gain, outputSize, upsampling),
            premultiplied.G.CreateBloom(downsampleFactor, blurs, gain, outputSize, upsampling),
            premultiplied.B.CreateBloom(downsampleFactor, blurs, gain, outputSize, upsampling),
            premultiplied.A.CreateBloom(downsampleFactor, blurs, 1f, outputSize, upsampling));
        Unpremultiply(result);
        return result;
    }

    //---------------------------------------------------------------------------------------------
    //8-bit conversion

    /// <summary>
    /// Writes the image as tightly packed 8-bit RGBA, STRAIGHT alpha (R, G, B, A per pixel,
    /// row-major). Every channel uses the rounding rule of <see cref="DrawingPlane.ToBytes(Span{byte})"/>:
    /// NaN and values at or below 0 give 0, at or above 1 give 255, otherwise
    /// <c>(byte)(value * 255 + 0.5)</c>. The colour of a transparent pixel is written as it is.
    /// </summary>
    /// <param name="destination">At least <c>Width * Height * 4</c> bytes.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    public void ToRgba(Span<byte> destination)
    {
        int count = R.Length;
        if (destination.Length < count * 4L) { throw new ArgumentException("The destination is too small.", nameof(destination)); }
        float[] r = R.Data, g = G.Data, b = B.Data, a = A.Data;
        for (int i = 0, o = 0; i < count; i++, o += 4)
        {
            destination[o] = PlaneKernels.UnitToByte(r[i]);
            destination[o + 1] = PlaneKernels.UnitToByte(g[i]);
            destination[o + 2] = PlaneKernels.UnitToByte(b[i]);
            destination[o + 3] = PlaneKernels.UnitToByte(a[i]);
        }
    }

    /// <summary>
    /// Returns the image as tightly packed 8-bit straight-alpha RGBA (see <see cref="ToRgba(Span{byte})"/>).
    /// </summary>
    /// <returns>A new array of <c>Width * Height * 4</c> bytes.</returns>
    public byte[] ToRgba()
    {
        var bytes = new byte[R.Length * 4];
        ToRgba(bytes);
        return bytes;
    }

    /// <summary>
    /// Overwrites the image from tightly packed 8-bit straight-alpha RGBA; each channel becomes
    /// <c>byte / 255f</c>, which <see cref="ToRgba(Span{byte})"/> turns back into the same byte, so
    /// every frame - transparent ones included - round-trips exactly.
    /// </summary>
    /// <param name="rgba">At least <c>Width * Height * 4</c> bytes.</param>
    /// <exception cref="ArgumentException">Thrown when there are too few bytes.</exception>
    public void CopyFromRgba(ReadOnlySpan<byte> rgba)
    {
        int count = R.Length;
        if (rgba.Length < count * 4L) { throw new ArgumentException("There are fewer bytes than pixels * 4.", nameof(rgba)); }
        float[] r = R.Data, g = G.Data, b = B.Data, a = A.Data;
        for (int i = 0, o = 0; i < count; i++, o += 4)
        {
            r[i] = PlaneKernels.ByteToUnit(rgba[o]);
            g[i] = PlaneKernels.ByteToUnit(rgba[o + 1]);
            b[i] = PlaneKernels.ByteToUnit(rgba[o + 2]);
            a[i] = PlaneKernels.ByteToUnit(rgba[o + 3]);
        }
    }

    /// <summary>
    /// Creates an image from tightly packed 8-bit straight-alpha RGBA (see <see cref="CopyFromRgba(ReadOnlySpan{byte})"/>).
    /// </summary>
    /// <param name="rgba">At least <c>width * height * 4</c> bytes.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>A new image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when there are too few bytes.</exception>
    public static DrawingPlaneImage FromRgba(ReadOnlySpan<byte> rgba, int width, int height)
    {
        var image = new DrawingPlaneImage(width, height);
        image.CopyFromRgba(rgba);
        return image;
    }

    /// <summary>
    /// Converts the image to a CodeBrix.Imaging <see cref="Image{TPixel}"/> with <see cref="Rgba32"/>
    /// pixels (straight alpha, the rounding rule of <see cref="ToRgba(Span{byte})"/>).
    /// </summary>
    /// <returns>A new image that the caller must dispose.</returns>
    public Image<Rgba32> ToImagingImage()
    {
        byte[] rgba = ToRgba();
        return Image.LoadPixelData<Rgba32>(rgba, Width, Height, PngFormat.Instance);
    }

    /// <summary>
    /// Creates a float image from a CodeBrix.Imaging <see cref="Image{TPixel}"/> with
    /// <see cref="Rgba32"/> pixels (each channel <c>byte / 255f</c>, straight alpha).
    /// </summary>
    /// <param name="image">The image to read (its first frame).</param>
    /// <returns>A new float image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    public static DrawingPlaneImage FromImagingImage(Image<Rgba32> image)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        var rgba = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(rgba);
        return FromRgba(rgba, image.Width, image.Height);
    }

    //---------------------------------------------------------------------------------------------
    //Internals

    internal static Vector4 ToVector(Color color)
    {
        Rgba32 c = color.ToRgba32();
        return new Vector4(PlaneKernels.ByteToUnit(c.R), PlaneKernels.ByteToUnit(c.G), PlaneKernels.ByteToUnit(c.B), PlaneKernels.ByteToUnit(c.A));
    }

    internal void RequireSameSize(DrawingPlaneImage other, string parameterName)
    {
        if (other == null) { throw new ArgumentNullException(parameterName); }
        if (other.Width != Width || other.Height != Height)
        {
            throw new ArgumentException("The images must be the same size.", parameterName);
        }
    }

    internal bool HasUniformAlpha()
    {
        float[] a = A.Data;
        float first = a[0];
        for (int i = 1; i < a.Length; i++)
        {
            if (a[i] != first) { return false; }
        }
        return !float.IsNaN(first);
    }

    //Mixes a towards b into destination (which may be a or b) with the rule of PlaneKernels.MixUnit;
    //  the fraction is t, or mask[i] when a mask is given
    internal static void MixInto(DrawingPlaneImage a, DrawingPlaneImage b, DrawingPlaneImage destination, float t, DrawingPlane mask)
    {
        float[] ar = a.R.Data, ag = a.G.Data, ab = a.B.Data, aa = a.A.Data;
        float[] br = b.R.Data, bg = b.G.Data, bb = b.B.Data, ba = b.A.Data;
        float[] dr = destination.R.Data, dg = destination.G.Data, db = destination.B.Data, da = destination.A.Data;
        float[] m = mask?.Data;
        int width = a.Width;
        PlaneKernels.ForRows(a.Height, ar.Length, y =>
        {
            int end = (y + 1) * width;
            for (int i = y * width; i < end; i++)
            {
                PlaneKernels.MixUnit(ar[i], ag[i], ab[i], aa[i], br[i], bg[i], bb[i], ba[i], m == null ? t : m[i],
                    out dr[i], out dg[i], out db[i], out da[i]);
            }
        });
    }

    private void FillRow(Func<int, int, Vector4> function, int y, int x0, int x1)
    {
        float[] r = R.Data, g = G.Data, b = B.Data, a = A.Data;
        int row = y * Width;
        for (int x = x0; x < x1; x++)
        {
            Vector4 v = function(x, y);
            r[row + x] = v.X;
            g[row + x] = v.Y;
            b[row + x] = v.Z;
            a[row + x] = v.W;
        }
    }

    private delegate void PixelAction(int index, float[] r, float[] g, float[] b, float[] a);

    private void ForEachPixel(PixelAction action)
    {
        float[] r = R.Data, g = G.Data, b = B.Data, a = A.Data;
        int width = Width;
        PlaneKernels.ForRows(Height, r.Length, y =>
        {
            int end = (y + 1) * width;
            for (int i = y * width; i < end; i++) { action(i, r, g, b, a); }
        });
    }

    //Porter-Duff source-over of one straight-alpha source pixel onto destination pixel i
    private static void Over(float[] dr, float[] dg, float[] db, float[] da, int i, float sr, float sg, float sb, float sourceAlpha)
    {
        float a = PlaneKernels.Clamp01(sourceAlpha);
        if (!(a > 0f)) { return; }
        float d = PlaneKernels.Clamp01(da[i]);
        if (a >= 1f || !(d > 0f))
        {
            dr[i] = sr;
            dg[i] = sg;
            db[i] = sb;
            da[i] = a;
            return;
        }
        float rest = d * (1f - a);
        float ao = d >= a ? d + (a * (1f - d)) : a + rest;
        dr[i] = ((sr * a) + (dr[i] * rest)) / ao;
        dg[i] = ((sg * a) + (dg[i] * rest)) / ao;
        db[i] = ((sb * a) + (db[i] * rest)) / ao;
        da[i] = ao;
    }

    //Runs a size-changing per-plane filter: on the colour planes directly (alpha resampled too,
    //  which keeps it uniform) when alpha is uniform, otherwise in premultiplied space
    private DrawingPlaneImage Spatial(Func<DrawingPlane, DrawingPlane> filter)
    {
        if (HasUniformAlpha())
        {
            DrawingPlane r = filter(R);
            return new DrawingPlaneImage(r, filter(G), filter(B), new DrawingPlane(r.Width, r.Height, A.Data[0]));
        }
        DrawingPlaneImage premultiplied = Clone();
        Premultiply(premultiplied, premultiplied);
        var result = new DrawingPlaneImage(filter(premultiplied.R), filter(premultiplied.G), filter(premultiplied.B), filter(premultiplied.A));
        Unpremultiply(result);
        return result;
    }

    private static void Premultiply(DrawingPlaneImage source, DrawingPlaneImage destination)
    {
        float[] sr = source.R.Data, sg = source.G.Data, sb = source.B.Data, sa = source.A.Data;
        float[] dr = destination.R.Data, dg = destination.G.Data, db = destination.B.Data;
        for (int i = 0; i < sr.Length; i++)
        {
            float a = sa[i];
            dr[i] = sr[i] * a;
            dg[i] = sg[i] * a;
            db[i] = sb[i] * a;
        }
        if (!ReferenceEquals(source, destination)) { source.A.CopyTo(destination.A); }
    }

    private static void Unpremultiply(DrawingPlaneImage image)
    {
        float[] r = image.R.Data, g = image.G.Data, b = image.B.Data, a = image.A.Data;
        for (int i = 0; i < r.Length; i++)
        {
            float alpha = a[i];
            if (alpha == 0f)
            {
                r[i] = 0f;
                g[i] = 0f;
                b[i] = 0f;
            }
            else
            {
                r[i] /= alpha;
                g[i] /= alpha;
                b[i] /= alpha;
            }
        }
    }
}
