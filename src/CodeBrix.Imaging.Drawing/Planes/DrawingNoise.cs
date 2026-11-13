using System;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// Seeded coherent noise in 2D and 3D (the third coordinate is usually time, for noise that
/// evolves smoothly from frame to frame), plus fractal sums of it and plane fills. Every value is
/// a pure function of its coordinates and seed, computed with this library's own documented
/// arithmetic, so it never changes between runs, .NET versions or the two drawing packages.
/// <para>
/// LATTICE. Space is divided into unit cells by <c>floor</c> of each coordinate. Each lattice
/// point gets a 64-bit hash <c>h = Mix(Mix(Mix(seed) + ix) + iy)</c> (and <c>Mix(h + iz)</c> in 3D),
/// where Mix is the SplitMix64 finaliser of <see cref="DrawingRandom"/>.
/// Value noise uses <c>(h &gt;&gt; 11) * 2^-53 * 2 - 1</c> (in -1..1) at each point. Gradient noise uses
/// one of 8 unit directions at 45 degree steps in 2D (<c>h &gt;&gt; 61</c>) or one of the 12 cube-edge
/// directions of improved Perlin noise in 3D (<c>((h &gt;&gt; 32) * 12) &gt;&gt; 32</c>), dotted with
/// the offset from that point.
/// </para>
/// <para>
/// BLENDING. Corner values are blended with the quintic fade <c>6t^5 - 15t^4 + 10t^3</c> and
/// <c>a + f * (b - a)</c>, x first, then y, then z. Gradient noise is then scaled so its
/// theoretical bound becomes 1 (by sqrt 2 in 2D, by 1 / sqrt 1.5 in 3D). Every result is clamped
/// to -1..1.
/// </para>
/// </summary>
public static class DrawingNoise
{
    private const double Diagonal = 0.70710678118654752440;
    private const double Scale2D = 1.41421356237309504880;
    private const double Scale3D = 0.81649658092772603273;

    private static readonly double[] Gradients2D =
    {
        1, 0, Diagonal, Diagonal, 0, 1, -Diagonal, Diagonal,
        -1, 0, -Diagonal, -Diagonal, 0, -1, Diagonal, -Diagonal,
    };

    private static readonly double[] Gradients3D =
    {
        1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1, 0,
        1, 0, 1, -1, 0, 1, 1, 0, -1, -1, 0, -1,
        0, 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1,
    };

    /// <summary>
    /// 2D value noise at a point.
    /// </summary>
    /// <param name="x">The x coordinate (one lattice cell per unit).</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="seed">The seed; different seeds give unrelated noise.</param>
    /// <returns>A value in -1..1.</returns>
    public static double Value(double x, double y, long seed)
    {
        long ix = FloorToLong(x), iy = FloorToLong(y);
        double fx = x - ix, fy = y - iy;
        double u = Fade(fx), v = Fade(fy);
        double a = Lerp(LatticeValue(seed, ix, iy), LatticeValue(seed, ix + 1, iy), u);
        double b = Lerp(LatticeValue(seed, ix, iy + 1), LatticeValue(seed, ix + 1, iy + 1), u);
        return ClampUnit(Lerp(a, b, v));
    }

    /// <summary>
    /// 3D value noise at a point - use z as time for 2D noise that changes smoothly.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="z">The z coordinate (often time multiplied by a speed).</param>
    /// <param name="seed">The seed.</param>
    /// <returns>A value in -1..1.</returns>
    public static double Value(double x, double y, double z, long seed)
    {
        long ix = FloorToLong(x), iy = FloorToLong(y), iz = FloorToLong(z);
        double u = Fade(x - ix), v = Fade(y - iy), w = Fade(z - iz);
        double c00 = Lerp(LatticeValue(seed, ix, iy, iz), LatticeValue(seed, ix + 1, iy, iz), u);
        double c10 = Lerp(LatticeValue(seed, ix, iy + 1, iz), LatticeValue(seed, ix + 1, iy + 1, iz), u);
        double c01 = Lerp(LatticeValue(seed, ix, iy, iz + 1), LatticeValue(seed, ix + 1, iy, iz + 1), u);
        double c11 = Lerp(LatticeValue(seed, ix, iy + 1, iz + 1), LatticeValue(seed, ix + 1, iy + 1, iz + 1), u);
        return ClampUnit(Lerp(Lerp(c00, c10, v), Lerp(c01, c11, v), w));
    }

    /// <summary>
    /// 2D gradient (Perlin-style) noise at a point; exactly 0 at whole-number coordinates.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>A value in -1..1.</returns>
    public static double Gradient(double x, double y, long seed)
    {
        long ix = FloorToLong(x), iy = FloorToLong(y);
        double fx = x - ix, fy = y - iy;
        double u = Fade(fx), v = Fade(fy);
        double a = Lerp(Dot2(seed, ix, iy, fx, fy), Dot2(seed, ix + 1, iy, fx - 1, fy), u);
        double b = Lerp(Dot2(seed, ix, iy + 1, fx, fy - 1), Dot2(seed, ix + 1, iy + 1, fx - 1, fy - 1), u);
        return ClampUnit(Lerp(a, b, v) * Scale2D);
    }

    /// <summary>
    /// 3D gradient (improved Perlin-style) noise at a point - use z as time for evolving 2D noise.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="z">The z coordinate (often time multiplied by a speed).</param>
    /// <param name="seed">The seed.</param>
    /// <returns>A value in -1..1.</returns>
    public static double Gradient(double x, double y, double z, long seed)
    {
        long ix = FloorToLong(x), iy = FloorToLong(y), iz = FloorToLong(z);
        double fx = x - ix, fy = y - iy, fz = z - iz;
        double u = Fade(fx), v = Fade(fy), w = Fade(fz);
        double c00 = Lerp(Dot3(seed, ix, iy, iz, fx, fy, fz), Dot3(seed, ix + 1, iy, iz, fx - 1, fy, fz), u);
        double c10 = Lerp(Dot3(seed, ix, iy + 1, iz, fx, fy - 1, fz), Dot3(seed, ix + 1, iy + 1, iz, fx - 1, fy - 1, fz), u);
        double c01 = Lerp(Dot3(seed, ix, iy, iz + 1, fx, fy, fz - 1), Dot3(seed, ix + 1, iy, iz + 1, fx - 1, fy, fz - 1), u);
        double c11 = Lerp(Dot3(seed, ix, iy + 1, iz + 1, fx, fy - 1, fz - 1), Dot3(seed, ix + 1, iy + 1, iz + 1, fx - 1, fy - 1, fz - 1), u);
        return ClampUnit(Lerp(Lerp(c00, c10, v), Lerp(c01, c11, v), w) * Scale3D);
    }

    /// <summary>
    /// 2D noise of either kind at a point.
    /// </summary>
    /// <param name="kind">Value or gradient noise.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>A value in -1..1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is unknown.</exception>
    public static double Sample(DrawingNoiseKind kind, double x, double y, long seed)
    {
        switch (kind)
        {
            case DrawingNoiseKind.Value: return Value(x, y, seed);
            case DrawingNoiseKind.Gradient: return Gradient(x, y, seed);
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>
    /// 3D noise of either kind at a point.
    /// </summary>
    /// <param name="kind">Value or gradient noise.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="z">The z coordinate.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>A value in -1..1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is unknown.</exception>
    public static double Sample(DrawingNoiseKind kind, double x, double y, double z, long seed)
    {
        switch (kind)
        {
            case DrawingNoiseKind.Value: return Value(x, y, z, seed);
            case DrawingNoiseKind.Gradient: return Gradient(x, y, z, seed);
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>
    /// 2D fractal noise (fBm): octave <c>o</c> (from 0) samples the noise at
    /// <c>(x, y) * lacunarity^o</c> with seed <c>seed + o</c> and amplitude <c>gain^o</c>; the
    /// amplitudes' sum divides the total, so the result stays in -1..1. One octave equals
    /// <see cref="Sample(DrawingNoiseKind, double, double, long)"/> exactly.
    /// </summary>
    /// <param name="kind">Value or gradient noise.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="seed">The seed of the first octave.</param>
    /// <param name="octaves">The number of octaves, 1 to 30.</param>
    /// <param name="lacunarity">The frequency multiplier from one octave to the next (2 is usual).</param>
    /// <param name="gain">The amplitude multiplier from one octave to the next (0.5 is usual).</param>
    /// <returns>A value in -1..1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is unknown or <paramref name="octaves"/> is outside 1..30.</exception>
    public static double Fractal(DrawingNoiseKind kind, double x, double y, long seed, int octaves = 4, double lacunarity = 2.0, double gain = 0.5)
    {
        ValidateOctaves(octaves);
        double total = 0, norm = 0, amplitude = 1, frequency = 1;
        for (int o = 0; o < octaves; o++)
        {
            total += amplitude * Sample(kind, x * frequency, y * frequency, unchecked(seed + o));
            norm += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }
        return norm == 0 ? 0 : ClampUnit(total / norm);
    }

    /// <summary>
    /// 3D fractal noise (fBm) with the octave rule of
    /// <see cref="Fractal(DrawingNoiseKind, double, double, long, int, double, double)"/>; all three
    /// coordinates are multiplied by the octave frequency.
    /// </summary>
    /// <param name="kind">Value or gradient noise.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="z">The z coordinate (often time).</param>
    /// <param name="seed">The seed of the first octave.</param>
    /// <param name="octaves">The number of octaves, 1 to 30.</param>
    /// <param name="lacunarity">The frequency multiplier per octave.</param>
    /// <param name="gain">The amplitude multiplier per octave.</param>
    /// <returns>A value in -1..1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is unknown or <paramref name="octaves"/> is outside 1..30.</exception>
    public static double Fractal(DrawingNoiseKind kind, double x, double y, double z, long seed, int octaves = 4, double lacunarity = 2.0, double gain = 0.5)
    {
        ValidateOctaves(octaves);
        double total = 0, norm = 0, amplitude = 1, frequency = 1;
        for (int o = 0; o < octaves; o++)
        {
            total += amplitude * Sample(kind, x * frequency, y * frequency, z * frequency, unchecked(seed + o));
            norm += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }
        return norm == 0 ? 0 : ClampUnit(total / norm);
    }

    /// <summary>
    /// Fills a plane with (fractal) noise, rows shared out across threads (each value is a pure
    /// function of its position, so the result never depends on the thread count). Pixel (x, y)
    /// samples <c>((x + 0.5 + offsetX) * frequency, (y + 0.5 + offsetY) * frequency)</c> - the pixel
    /// centre - and, when <paramref name="z"/> is given, 3D noise at that z as is (pass a time times a speed
    /// for noise that evolves).
    /// Values are in -1..1; map them with <see cref="DrawingPlane.Multiply(float)"/> and
    /// <see cref="DrawingPlane.Add(float)"/> (e.g. * 0.5 + 0.5 for 0..1).
    /// </summary>
    /// <param name="plane">The plane to overwrite.</param>
    /// <param name="kind">Value or gradient noise.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="frequency">Lattice cells per pixel (0.01 gives features about 100 pixels across).</param>
    /// <param name="z">The third coordinate for evolving noise (often time multiplied by a speed), or null for 2D noise.</param>
    /// <param name="octaves">The number of fractal octaves (1 = plain noise).</param>
    /// <param name="lacunarity">The frequency multiplier per octave.</param>
    /// <param name="gain">The amplitude multiplier per octave.</param>
    /// <param name="offsetX">Shifts the pattern, in pixels (scrolling).</param>
    /// <param name="offsetY">Shifts the pattern vertically, in pixels.</param>
    /// <returns>The same plane.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plane"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is unknown or <paramref name="octaves"/> is outside 1..30.</exception>
    public static DrawingPlane Fill(DrawingPlane plane, DrawingNoiseKind kind, long seed, double frequency, double? z = null,
        int octaves = 1, double lacunarity = 2.0, double gain = 0.5, double offsetX = 0.0, double offsetY = 0.0)
    {
        if (plane == null) { throw new ArgumentNullException(nameof(plane)); }
        if (kind != DrawingNoiseKind.Value && kind != DrawingNoiseKind.Gradient) { throw new ArgumentOutOfRangeException(nameof(kind)); }
        ValidateOctaves(octaves);
        float[] data = plane.Data;
        int width = plane.Width;
        PlaneKernels.ForRows(plane.Height, plane.Length, y =>
        {
            double py = (y + 0.5 + offsetY) * frequency;
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                double px = (x + 0.5 + offsetX) * frequency;
                data[row + x] = (float)(z.HasValue
                    ? Fractal(kind, px, py, z.Value, seed, octaves, lacunarity, gain)
                    : Fractal(kind, px, py, seed, octaves, lacunarity, gain));
            }
        });
        return plane;
    }

    //---------------------------------------------------------------------------------------------

    private static void ValidateOctaves(int octaves)
    {
        if (octaves < 1 || octaves > 30) { throw new ArgumentOutOfRangeException(nameof(octaves)); }
    }

    private static long FloorToLong(double value) => (long)Math.Floor(value);

    private static double Fade(double t) => t * t * t * ((t * ((t * 6) - 15)) + 10);

    private static double Lerp(double a, double b, double t) => a + (t * (b - a));

    private static double ClampUnit(double value) => value < -1 ? -1 : (value > 1 ? 1 : value);

    private static ulong Hash(long seed, long ix, long iy)
        => unchecked(DrawingRandom.Mix(DrawingRandom.Mix(DrawingRandom.Mix((ulong)seed) + (ulong)ix) + (ulong)iy));

    private static ulong Hash(long seed, long ix, long iy, long iz) => unchecked(DrawingRandom.Mix(Hash(seed, ix, iy) + (ulong)iz));

    private static double ToSigned(ulong h) => ((h >> 11) * (1.0 / 9007199254740992.0) * 2.0) - 1.0;

    private static double LatticeValue(long seed, long ix, long iy) => ToSigned(Hash(seed, ix, iy));

    private static double LatticeValue(long seed, long ix, long iy, long iz) => ToSigned(Hash(seed, ix, iy, iz));

    private static double Dot2(long seed, long ix, long iy, double dx, double dy)
    {
        int g = (int)(Hash(seed, ix, iy) >> 61) * 2;
        return (Gradients2D[g] * dx) + (Gradients2D[g + 1] * dy);
    }

    private static double Dot3(long seed, long ix, long iy, long iz, double dx, double dy, double dz)
    {
        int g = (int)((((Hash(seed, ix, iy, iz) >> 32) * 12UL) >> 32) * 3UL);
        return (Gradients3D[g] * dx) + (Gradients3D[g + 1] * dy) + (Gradients3D[g + 2] * dz);
    }
}
