using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// One rectangular plane of open-ended float values - a single colour channel, an alpha channel,
/// a coverage mask or any per-pixel quantity (light, height, noise). Values are NOT clamped:
/// lights may add up past 1.0 and a tone curve or the 8-bit conversion brings them back.
/// <para>
/// Storage is row-major: the value of pixel (x, y) is <c>Data[y * Width + x]</c>, and
/// <see cref="Data"/> is the live backing array for fast direct access.
/// </para>
/// <para>
/// Operations that keep the size (fills, arithmetic, blur, tone curve, ...) change the plane IN
/// PLACE and return the same instance, so calls chain; call <see cref="Clone"/> first to keep the
/// original. Operations that change the size (<see cref="Resized"/>, <see cref="Downsampled"/>,
/// <see cref="CreateBloom"/>)
/// return a NEW plane and leave this one unchanged.
/// </para>
/// <para>
/// Every computation is plain managed arithmetic with a documented order of operations, so both
/// drawing packages produce bit-identical values. Large planes are processed a row per thread;
/// every value is still computed by exactly one thread in a fixed order, so results never
/// depend on the number of threads. A plane is not safe to change from two threads at once.
/// </para>
/// </summary>
public sealed class DrawingPlane
{
    /// <summary>
    /// Creates a plane filled with zeros.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is less than 1, or the plane would hold more than <see cref="Array.MaxLength"/> values.</exception>
    public DrawingPlane(int width, int height)
    {
        ValidateSize(width, height);
        Width = width;
        Height = height;
        Data = new float[width * height];
    }

    /// <summary>
    /// Creates a plane with every value set to <paramref name="value"/>.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="value">The initial value of every pixel.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is less than 1.</exception>
    public DrawingPlane(int width, int height, float value)
        : this(width, height)
    {
        Fill(value);
    }

    /// <summary>
    /// Creates a plane filled with zeros.
    /// </summary>
    /// <param name="size">The size in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is less than 1.</exception>
    public DrawingPlane(Size size)
        : this(size.Width, size.Height)
    {
    }

    /// <summary>
    /// Creates a plane over an existing array WITHOUT copying it: the plane's <see cref="Data"/>
    /// is <paramref name="data"/>, so later changes through either are seen by both.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="data">The row-major values; its length must be exactly <c>width * height</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when the array length is not <c>width * height</c>.</exception>
    public DrawingPlane(int width, int height, float[] data)
    {
        if (data == null) { throw new ArgumentNullException(nameof(data)); }
        ValidateSize(width, height);
        if (data.Length != width * height)
        {
            throw new ArgumentException("The array length must be exactly width * height.", nameof(data));
        }
        Width = width;
        Height = height;
        Data = data;
    }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>The size in pixels.</summary>
    public Size Size => new Size(Width, Height);

    /// <summary>The number of values (<c>Width * Height</c>).</summary>
    public int Length => Data.Length;

    /// <summary>
    /// The live backing array, row-major: pixel (x, y) is at index <c>y * Width + x</c>. Reading
    /// and writing it directly is the fastest way to do custom per-pixel work.
    /// </summary>
    public float[] Data { get; }

    /// <summary>
    /// Gets or sets the value of one pixel.
    /// </summary>
    /// <param name="x">The column, 0 to <c>Width - 1</c>.</param>
    /// <param name="y">The row, 0 to <c>Height - 1</c>.</param>
    /// <returns>The pixel's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the position is outside the plane.</exception>
    public float this[int x, int y]
    {
        get
        {
            CheckPosition(x, y);
            return Data[(y * Width) + x];
        }
        set
        {
            CheckPosition(x, y);
            Data[(y * Width) + x] = value;
        }
    }

    /// <summary>
    /// The whole backing array as a span.
    /// </summary>
    /// <returns>A span over <see cref="Data"/>.</returns>
    public Span<float> AsSpan() => Data.AsSpan();

    /// <summary>
    /// One row of the backing array as a span.
    /// </summary>
    /// <param name="y">The row, 0 to <c>Height - 1</c>.</param>
    /// <returns>A span of <see cref="Width"/> values.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="y"/> is outside the plane.</exception>
    public Span<float> GetRow(int y)
    {
        if (y < 0 || y >= Height) { throw new ArgumentOutOfRangeException(nameof(y)); }
        return Data.AsSpan(y * Width, Width);
    }

    /// <summary>
    /// Makes an independent copy of the plane.
    /// </summary>
    /// <returns>A new plane with the same size and values.</returns>
    public DrawingPlane Clone()
    {
        var copy = new DrawingPlane(Width, Height);
        Array.Copy(Data, copy.Data, Data.Length);
        return copy;
    }

    /// <summary>
    /// Copies every value into another plane of the same size.
    /// </summary>
    /// <param name="destination">The plane to overwrite.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public void CopyTo(DrawingPlane destination)
    {
        RequireSameSize(destination, nameof(destination));
        Array.Copy(Data, destination.Data, Data.Length);
    }

    //---------------------------------------------------------------------------------------------
    //Filling

    /// <summary>
    /// Sets every value to zero.
    /// </summary>
    /// <returns>This plane.</returns>
    public DrawingPlane Clear()
    {
        Array.Clear(Data);
        return this;
    }

    /// <summary>
    /// Sets every value to <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>This plane.</returns>
    public DrawingPlane Fill(float value)
    {
        Array.Fill(Data, value);
        return this;
    }

    /// <summary>
    /// Sets every value inside a rectangle to <paramref name="value"/>. The rectangle is clipped to
    /// the plane; a rectangle wholly outside changes nothing.
    /// </summary>
    /// <param name="area">The pixel rectangle.</param>
    /// <param name="value">The value.</param>
    /// <returns>This plane.</returns>
    public DrawingPlane Fill(Rectangle area, float value)
    {
        if (!Clip(area, out int x0, out int y0, out int x1, out int y1)) { return this; }
        for (int y = y0; y < y1; y++) { Data.AsSpan((y * Width) + x0, x1 - x0).Fill(value); }
        return this;
    }

    /// <summary>
    /// Sets every value from a function of its pixel position, row by row from the top on the
    /// calling thread.
    /// </summary>
    /// <param name="function">Receives (x, y) - the pixel's column and row - and returns its value.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlane Fill(Func<int, int, float> function)
        => Fill(new Rectangle(0, 0, Width, Height), function);

    /// <summary>
    /// Sets every value inside a rectangle from a function of its pixel position (the function
    /// receives absolute plane coordinates). The rectangle is clipped to the plane.
    /// </summary>
    /// <param name="area">The pixel rectangle.</param>
    /// <param name="function">Receives (x, y) and returns the pixel's value.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlane Fill(Rectangle area, Func<int, int, float> function)
    {
        if (function == null) { throw new ArgumentNullException(nameof(function)); }
        if (!Clip(area, out int x0, out int y0, out int x1, out int y1)) { return this; }
        for (int y = y0; y < y1; y++)
        {
            int row = y * Width;
            for (int x = x0; x < x1; x++) { Data[row + x] = function(x, y); }
        }
        return this;
    }

    /// <summary>
    /// Sets every value from a function of its pixel position, with the rows shared out across
    /// threads. Each pixel is computed exactly once, so the result is identical to
    /// <see cref="Fill(Func{int, int, float})"/> whatever the thread count or scheduling - PROVIDED
    /// the function is pure (depends only on x and y and on state nobody changes meanwhile) and
    /// safe to call from several threads at once.
    /// </summary>
    /// <param name="function">Receives (x, y) and returns the pixel's value.</param>
    /// <param name="maxDegreeOfParallelism">The most threads to use; 0 or less means no limit.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlane FillParallel(Func<int, int, float> function, int maxDegreeOfParallelism = 0)
        => FillParallel(new Rectangle(0, 0, Width, Height), function, maxDegreeOfParallelism);

    /// <summary>
    /// Sets every value inside a rectangle from a function of its pixel position, with the rows
    /// shared out across threads (see <see cref="FillParallel(Func{int, int, float}, int)"/> for
    /// the determinism rule). The rectangle is clipped to the plane.
    /// </summary>
    /// <param name="area">The pixel rectangle.</param>
    /// <param name="function">Receives (x, y) and returns the pixel's value.</param>
    /// <param name="maxDegreeOfParallelism">The most threads to use; 0 or less means no limit.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlane FillParallel(Rectangle area, Func<int, int, float> function, int maxDegreeOfParallelism = 0)
    {
        if (function == null) { throw new ArgumentNullException(nameof(function)); }
        if (!Clip(area, out int x0, out int y0, out int x1, out int y1)) { return this; }
        PlaneKernels.ForRows(y1 - y0, int.MaxValue, i =>
        {
            int y = y0 + i;
            int row = y * Width;
            for (int x = x0; x < x1; x++) { Data[row + x] = function(x, y); }
        }, maxDegreeOfParallelism == 0 ? -1 : maxDegreeOfParallelism);
        return this;
    }

    //---------------------------------------------------------------------------------------------
    //Arithmetic (in place)

    /// <summary>
    /// Adds a constant to every value.
    /// </summary>
    /// <param name="value">The amount to add.</param>
    /// <returns>This plane.</returns>
    public DrawingPlane Add(float value)
    {
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { d[i] += value; }
        return this;
    }

    /// <summary>
    /// Adds another plane, optionally scaled: <c>this = this + other * scale</c>.
    /// </summary>
    /// <param name="other">The plane to add (same size).</param>
    /// <param name="scale">The factor applied to <paramref name="other"/> first; 1 adds it as is.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlane Add(DrawingPlane other, float scale = 1f)
    {
        RequireSameSize(other, nameof(other));
        float[] d = Data, o = other.Data;
        for (int i = 0; i < d.Length; i++) { d[i] += o[i] * scale; }
        return this;
    }

    /// <summary>
    /// Subtracts another plane: <c>this = this - other</c>.
    /// </summary>
    /// <param name="other">The plane to subtract (same size).</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlane Subtract(DrawingPlane other)
    {
        RequireSameSize(other, nameof(other));
        float[] d = Data, o = other.Data;
        for (int i = 0; i < d.Length; i++) { d[i] -= o[i]; }
        return this;
    }

    /// <summary>
    /// Multiplies every value by a constant (scaling).
    /// </summary>
    /// <param name="factor">The factor.</param>
    /// <returns>This plane.</returns>
    public DrawingPlane Multiply(float factor)
    {
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { d[i] *= factor; }
        return this;
    }

    /// <summary>
    /// Multiplies by another plane, pixel by pixel - for example applying a coverage mask.
    /// </summary>
    /// <param name="other">The plane to multiply by (same size).</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlane Multiply(DrawingPlane other)
    {
        RequireSameSize(other, nameof(other));
        float[] d = Data, o = other.Data;
        for (int i = 0; i < d.Length; i++) { d[i] *= o[i]; }
        return this;
    }

    /// <summary>
    /// Moves every value towards another plane by a fraction:
    /// <c>this = this * (1 - t) + other * t</c>, with <c>1 - t</c> computed once as a float.
    /// </summary>
    /// <param name="other">The plane to move towards (same size).</param>
    /// <param name="t">The fraction; 0 keeps this plane, 1 gives <paramref name="other"/>. Not clamped.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlane Lerp(DrawingPlane other, float t)
    {
        RequireSameSize(other, nameof(other));
        float s = 1f - t;
        float[] d = Data, o = other.Data;
        for (int i = 0; i < d.Length; i++) { d[i] = (d[i] * s) + (o[i] * t); }
        return this;
    }

    /// <summary>
    /// Moves every value towards another plane by a per-pixel fraction taken from a mask:
    /// <c>this = this * (1 - m) + other * m</c>.
    /// </summary>
    /// <param name="other">The plane to move towards (same size).</param>
    /// <param name="mask">The per-pixel fraction (same size); values are used as they are, not clamped.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlane Lerp(DrawingPlane other, DrawingPlane mask)
    {
        RequireSameSize(other, nameof(other));
        RequireSameSize(mask, nameof(mask));
        float[] d = Data, o = other.Data, m = mask.Data;
        for (int i = 0; i < d.Length; i++)
        {
            float t = m[i];
            d[i] = (d[i] * (1f - t)) + (o[i] * t);
        }
        return this;
    }

    /// <summary>
    /// Replaces every value by the smaller of it and <paramref name="value"/> (an upper limit).
    /// NaN values stay NaN.
    /// </summary>
    /// <param name="value">The upper limit.</param>
    /// <returns>This plane.</returns>
    public DrawingPlane Min(float value)
    {
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { if (d[i] > value) { d[i] = value; } }
        return this;
    }

    /// <summary>
    /// Replaces every value by the smaller of it and the matching value of another plane.
    /// </summary>
    /// <param name="other">The other plane (same size).</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlane Min(DrawingPlane other)
    {
        RequireSameSize(other, nameof(other));
        float[] d = Data, o = other.Data;
        for (int i = 0; i < d.Length; i++) { if (d[i] > o[i]) { d[i] = o[i]; } }
        return this;
    }

    /// <summary>
    /// Replaces every value by the larger of it and <paramref name="value"/> (a lower limit).
    /// NaN values stay NaN.
    /// </summary>
    /// <param name="value">The lower limit.</param>
    /// <returns>This plane.</returns>
    public DrawingPlane Max(float value)
    {
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { if (d[i] < value) { d[i] = value; } }
        return this;
    }

    /// <summary>
    /// Replaces every value by the larger of it and the matching value of another plane.
    /// </summary>
    /// <param name="other">The other plane (same size).</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    public DrawingPlane Max(DrawingPlane other)
    {
        RequireSameSize(other, nameof(other));
        float[] d = Data, o = other.Data;
        for (int i = 0; i < d.Length; i++) { if (d[i] < o[i]) { d[i] = o[i]; } }
        return this;
    }

    /// <summary>
    /// Limits every value to the range <paramref name="min"/> .. <paramref name="max"/>. NaN
    /// values become <paramref name="min"/>.
    /// </summary>
    /// <param name="min">The lowest allowed value.</param>
    /// <param name="max">The highest allowed value.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    public DrawingPlane Clamp(float min, float max)
    {
        if (min > max) { throw new ArgumentException("min must not be greater than max.", nameof(min)); }
        float[] d = Data;
        for (int i = 0; i < d.Length; i++)
        {
            float v = d[i];
            d[i] = !(v > min) ? min : (v > max ? max : v);
        }
        return this;
    }

    /// <summary>
    /// Replaces every value by a function of itself - any per-value curve.
    /// </summary>
    /// <param name="function">Receives a value and returns its replacement.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public DrawingPlane Apply(Func<float, float> function)
    {
        if (function == null) { throw new ArgumentNullException(nameof(function)); }
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { d[i] = function(d[i]); }
        return this;
    }

    //---------------------------------------------------------------------------------------------
    //Tone, gamma, vignette

    /// <summary>
    /// Applies the soft-shoulder tone curve <c>v = v / (1 + k * v) * gain</c> to every value, in
    /// float, in that order. It compresses bright, open-ended values (lights past 1.0) back towards
    /// the displayable range while leaving dark values almost unchanged. With <paramref name="k"/> = 0
    /// it is a plain gain. The curve is meant for values of 0 and above; a value of -1/k divides by zero.
    /// </summary>
    /// <param name="k">The shoulder strength (e.g. 0.15).</param>
    /// <param name="gain">The final multiplier (e.g. 1.08).</param>
    /// <returns>This plane.</returns>
    public DrawingPlane ApplyToneCurve(float k, float gain)
    {
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { d[i] = ToneCurveValue(d[i], k, gain); }
        return this;
    }

    /// <summary>
    /// Raises every positive value to a power: <c>v = MathF.Pow(v, exponent)</c>; values at or
    /// below 0 (and NaN) are left unchanged. An exponent below 1 brightens mid-tones (1 / 2.2
    /// encodes linear light for display), above 1 darkens them.
    /// </summary>
    /// <param name="exponent">The power; must be greater than 0.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="exponent"/> is not greater than 0.</exception>
    public DrawingPlane ApplyGamma(float exponent)
    {
        if (!(exponent > 0f) || float.IsInfinity(exponent)) { throw new ArgumentOutOfRangeException(nameof(exponent)); }
        float[] d = Data;
        for (int i = 0; i < d.Length; i++)
        {
            float v = d[i];
            if (v > 0f) { d[i] = MathF.Pow(v, exponent); }
        }
        return this;
    }

    /// <summary>
    /// Multiplies every value by the vignette factor of
    /// <see cref="CreateVignette(int, int, double, double, double, double, double, double)"/> (computed
    /// on the fly, identically).
    /// </summary>
    /// <param name="centerX">The vignette centre's x, in pixels (e.g. <c>Width / 2f</c>).</param>
    /// <param name="centerY">The vignette centre's y, in pixels.</param>
    /// <param name="radiusX">The horizontal distance, in pixels, at which the normalised distance reaches 1.</param>
    /// <param name="radiusY">The vertical distance, in pixels, at which the normalised distance reaches 1.</param>
    /// <param name="strength">How much the factor falls per unit of squared normalised distance.</param>
    /// <param name="minimum">The lowest factor (the corners never go darker than this).</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a radius is not greater than 0.</exception>
    public DrawingPlane ApplyVignette(double centerX, double centerY, double radiusX, double radiusY, double strength, double minimum = 0.0)
    {
        ValidateVignette(radiusX, radiusY);
        float[] d = Data;
        int width = Width;
        PlaneKernels.ForRows(Height, Length, y =>
        {
            int row = y * width;
            double dy = VignetteAxis(y, centerY, radiusY);
            for (int x = 0; x < width; x++)
            {
                d[row + x] *= VignetteFactor(VignetteAxis(x, centerX, radiusX), dy, strength, minimum);
            }
        });
        return this;
    }

    /// <summary>
    /// Creates a vignette factor plane: for each pixel,
    /// <c>dx = (x - centerX) / radiusX</c>, <c>dy = (y - centerY) / radiusY</c> and
    /// <c>factor = clamp(1 - strength * (dx * dx + dy * dy), minimum, 1)</c>, computed in double
    /// and stored as float. x and y are the pixel's integer column and row; every parameter is a
    /// double so no precision is lost before the double arithmetic. Multiply an image or a
    /// plane by it to darken towards the edges.
    /// </summary>
    /// <param name="width">The plane width in pixels.</param>
    /// <param name="height">The plane height in pixels.</param>
    /// <param name="centerX">The vignette centre's x, in pixels.</param>
    /// <param name="centerY">The vignette centre's y, in pixels.</param>
    /// <param name="radiusX">The horizontal distance, in pixels, at which the normalised distance reaches 1.</param>
    /// <param name="radiusY">The vertical distance, in pixels, at which the normalised distance reaches 1.</param>
    /// <param name="strength">How much the factor falls per unit of squared normalised distance.</param>
    /// <param name="minimum">The lowest factor.</param>
    /// <returns>A new plane of factors in <paramref name="minimum"/> .. 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1 or a radius is not greater than 0.</exception>
    public static DrawingPlane CreateVignette(int width, int height, double centerX, double centerY, double radiusX, double radiusY, double strength, double minimum = 0.0)
        => new DrawingPlane(width, height, 1f).ApplyVignette(centerX, centerY, radiusX, radiusY, strength, minimum);

    //---------------------------------------------------------------------------------------------
    //Filters

    /// <summary>
    /// Blurs the plane with a Gaussian, in place. Kernel: radius <c>(int)(4 * sigma + 0.5)</c>;
    /// each weight <c>exp(-i² / (2σ²))</c>, their sum, and each weight divided by that sum are all
    /// computed in double; only the normalised weight is rounded, once, to float.
    /// The blur is separable - a horizontal pass, then a vertical pass - and each output value sums
    /// <c>weight * sample</c> from -radius to +radius starting at 0. Edges are MIRRORED including
    /// the edge pixel (index -1 reads 0, -2 reads 1; width reads width - 1), so a flat plane stays
    /// flat right up to its borders. A sigma of 0 or less does nothing.
    /// </summary>
    /// <param name="sigma">The standard deviation in pixels.</param>
    /// <returns>This plane.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sigma"/> is NaN or infinite.</exception>
    public DrawingPlane GaussianBlur(float sigma)
    {
        ValidateSigma(sigma);
        PlaneKernels.Blur(Data, Data, Width, Height, sigma);
        return this;
    }

    /// <summary>
    /// Returns a resized copy of the plane (this plane is unchanged). See
    /// <see cref="DrawingPlaneResampling"/> for each mode's exact sampling grid. Bilinear values are
    /// <c>p00·w00 + p10·w10 + p01·w01 + p11·w11</c> summed in that order, with
    /// <c>w00 = (1 - fx)(1 - fy)</c>, <c>w10 = fx(1 - fy)</c>, <c>w01 = (1 - fx)fy</c>, <c>w11 = fx·fy</c>.
    /// Bilinear does not average away detail when shrinking a lot - use
    /// <see cref="Downsampled"/> first for that.
    /// </summary>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    /// <param name="resampling">How source values are chosen.</param>
    /// <returns>A new plane of the requested size.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1 or the mode is unknown.</exception>
    public DrawingPlane Resized(int width, int height, DrawingPlaneResampling resampling = DrawingPlaneResampling.Bilinear)
    {
        var result = new DrawingPlane(width, height);
        ResizeInto(Data, Width, Height, result.Data, width, height, resampling);
        return result;
    }

    /// <summary>
    /// Returns a copy shrunk by a whole factor with a box filter (this plane is unchanged). The new
    /// size is <c>ceiling(Width / factor) x ceiling(Height / factor)</c>; each new pixel is the mean
    /// of the factor x factor block it covers (a partial block at the right or bottom edge averages
    /// only the pixels it has). Each block row is summed left to right, row sums are added top to
    /// bottom, and the total is divided by the pixel count as a float.
    /// </summary>
    /// <param name="factor">The shrink factor, 1 or more (1 returns a copy).</param>
    /// <returns>A new, smaller plane.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="factor"/> is less than 1.</exception>
    public DrawingPlane Downsampled(int factor)
    {
        if (factor < 1) { throw new ArgumentOutOfRangeException(nameof(factor)); }
        int width = ((Width - 1) / factor) + 1;
        int height = ((Height - 1) / factor) + 1;
        var result = new DrawingPlane(width, height);
        PlaneKernels.Downsample(Data, Width, Height, factor, result.Data, width, height);
        return result;
    }

    /// <summary>
    /// Builds a bloom (glow) plane from this plane of light values, which is unchanged. Steps:
    /// (1) <see cref="Downsampled"/> by <paramref name="downsampleFactor"/>;
    /// (2) for each (Sigma, Weight) pair in order, blur a copy of the small plane with
    /// <see cref="GaussianBlur(float)"/> and accumulate <c>sum = sum + blurred * Weight</c>
    /// (sum starts at 0);
    /// (3) multiply by <paramref name="gain"/>;
    /// (4) <see cref="Resized"/> to the output size.
    /// Sigmas are in SMALL-plane pixels. Add the result to the scene (<see cref="Add(DrawingPlane, float)"/>)
    /// to make the lights glow.
    /// </summary>
    /// <param name="downsampleFactor">The box-downsample factor before blurring (1 = none; 4 is typical).</param>
    /// <param name="blurs">The blurs to sum, e.g. a tight and a wide one: <c>new[] { (3f, 0.8f), (14f, 0.9f) }</c>.</param>
    /// <param name="gain">The factor applied to the summed glow.</param>
    /// <param name="outputSize">The size of the returned plane; null means this plane's size.</param>
    /// <param name="upsampling">How the small glow is enlarged again.</param>
    /// <returns>A new plane holding only the glow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blurs"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="blurs"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor is less than 1 or a sigma is NaN or infinite.</exception>
    public DrawingPlane CreateBloom(int downsampleFactor, IReadOnlyList<(float Sigma, float Weight)> blurs, float gain = 1f,
        Size? outputSize = null, DrawingPlaneResampling upsampling = DrawingPlaneResampling.Bilinear)
    {
        ValidateBlurs(blurs);
        Size size = outputSize ?? Size;
        ValidateSize(size.Width, size.Height);
        DrawingPlane small = Downsampled(downsampleFactor);
        var sum = new DrawingPlane(small.Width, small.Height);
        var scratch = new DrawingPlane(small.Width, small.Height);
        for (int i = 0; i < blurs.Count; i++)
        {
            small.CopyTo(scratch);
            scratch.GaussianBlur(blurs[i].Sigma);
            sum.Add(scratch, blurs[i].Weight);
        }
        sum.Multiply(gain);
        return sum.Resized(size.Width, size.Height, upsampling);
    }

    //---------------------------------------------------------------------------------------------
    //8-bit conversion

    /// <summary>
    /// Writes the plane as one byte per pixel (row-major). Rounding rule: NaN and values at or
    /// below 0 give 0, values at or above 1 give 255, anything else gives
    /// <c>(byte)(value * 255 + 0.5)</c> (round half up).
    /// </summary>
    /// <param name="destination">At least <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    public void ToBytes(Span<byte> destination)
    {
        if (destination.Length < Length) { throw new ArgumentException("The destination is too small.", nameof(destination)); }
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { destination[i] = PlaneKernels.UnitToByte(d[i]); }
    }

    /// <summary>
    /// Returns the plane as one byte per pixel, with the rounding rule of <see cref="ToBytes(Span{byte})"/>.
    /// </summary>
    /// <returns>A new array of <see cref="Length"/> bytes.</returns>
    public byte[] ToBytes()
    {
        var bytes = new byte[Length];
        ToBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Overwrites the plane from one byte per pixel; each value becomes <c>byte / 255f</c>, which
    /// <see cref="ToBytes(Span{byte})"/> turns back into the same byte - the in-place companion of
    /// <see cref="FromBytes(ReadOnlySpan{byte}, int, int)"/>, for reusing one plane every frame.
    /// </summary>
    /// <param name="bytes">At least <see cref="Length"/> bytes, row-major.</param>
    /// <exception cref="ArgumentException">Thrown when there are too few bytes.</exception>
    public void CopyFromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Length) { throw new ArgumentException("There are fewer bytes than pixels.", nameof(bytes)); }
        float[] d = Data;
        for (int i = 0; i < d.Length; i++) { d[i] = PlaneKernels.ByteToUnit(bytes[i]); }
    }

    /// <summary>
    /// Creates a plane from one byte per pixel; each value becomes <c>byte / 255f</c>, which
    /// <see cref="ToBytes(Span{byte})"/> turns back into the same byte.
    /// </summary>
    /// <param name="bytes">At least <c>width * height</c> bytes, row-major.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>A new plane.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when there are too few bytes.</exception>
    public static DrawingPlane FromBytes(ReadOnlySpan<byte> bytes, int width, int height)
    {
        var plane = new DrawingPlane(width, height);
        if (bytes.Length < plane.Length) { throw new ArgumentException("There are fewer bytes than pixels.", nameof(bytes)); }
        for (int i = 0; i < plane.Data.Length; i++) { plane.Data[i] = PlaneKernels.ByteToUnit(bytes[i]); }
        return plane;
    }

    //---------------------------------------------------------------------------------------------
    //Helpers shared with DrawingPlaneImage

    internal static void ValidateSize(int width, int height)
    {
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height)); }
        if ((long)width * height > Array.MaxLength) { throw new ArgumentOutOfRangeException(nameof(height)); }
    }

    internal static void ValidateSigma(float sigma)
    {
        if (float.IsNaN(sigma) || float.IsInfinity(sigma)) { throw new ArgumentOutOfRangeException(nameof(sigma)); }
    }

    internal static void ValidateBlurs(IReadOnlyList<(float Sigma, float Weight)> blurs)
    {
        if (blurs == null) { throw new ArgumentNullException(nameof(blurs)); }
        if (blurs.Count == 0) { throw new ArgumentException("At least one blur is required.", nameof(blurs)); }
        for (int i = 0; i < blurs.Count; i++) { ValidateSigma(blurs[i].Sigma); }
    }

    internal static void ValidateVignette(double radiusX, double radiusY)
    {
        if (!(radiusX > 0.0)) { throw new ArgumentOutOfRangeException(nameof(radiusX)); }
        if (!(radiusY > 0.0)) { throw new ArgumentOutOfRangeException(nameof(radiusY)); }
    }

    internal static double VignetteAxis(int position, double center, double radius) => (position - center) / radius;

    internal static float VignetteFactor(double dx, double dy, double strength, double minimum)
    {
        double factor = 1.0 - (strength * ((dx * dx) + (dy * dy)));
        if (factor > 1.0) { factor = 1.0; }
        if (factor < minimum) { factor = minimum; }
        return (float)factor;
    }

    internal static float ToneCurveValue(float value, float k, float gain) => value / (1f + (k * value)) * gain;

    internal static void ResizeInto(float[] source, int sourceWidth, int sourceHeight, float[] destination, int width, int height,
        DrawingPlaneResampling resampling)
    {
        switch (resampling)
        {
            case DrawingPlaneResampling.Nearest:
                PlaneKernels.ResizeNearest(source, sourceWidth, sourceHeight, destination, width, height);
                break;
            case DrawingPlaneResampling.Bilinear:
                PlaneKernels.ResizeBilinear(source, sourceWidth, sourceHeight, destination, width, height, false);
                break;
            case DrawingPlaneResampling.BilinearAlignedCorners:
                PlaneKernels.ResizeBilinear(source, sourceWidth, sourceHeight, destination, width, height, true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resampling));
        }
    }

    internal bool Clip(Rectangle area, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Math.Max(area.X, 0);
        y0 = Math.Max(area.Y, 0);
        x1 = (int)Math.Min((long)area.X + Math.Max(area.Width, 0), Width);
        y1 = (int)Math.Min((long)area.Y + Math.Max(area.Height, 0), Height);
        return x0 < x1 && y0 < y1;
    }

    internal void RequireSameSize(DrawingPlane other, string parameterName)
    {
        if (other == null) { throw new ArgumentNullException(parameterName); }
        if (other.Width != Width || other.Height != Height)
        {
            throw new ArgumentException("The planes must be the same size.", parameterName);
        }
    }

    private void CheckPosition(int x, int y)
    {
        if (x < 0 || x >= Width) { throw new ArgumentOutOfRangeException(nameof(x)); }
        if (y < 0 || y >= Height) { throw new ArgumentOutOfRangeException(nameof(y)); }
    }
}
