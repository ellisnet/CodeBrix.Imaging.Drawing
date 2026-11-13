using System;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// A seeded random source whose sequence is defined by this library's own documented algorithm
/// (not <see cref="Random"/>), so the same seed gives the same numbers in every .NET version, on
/// every machine and in both drawing packages - stroke layouts, star fields and particle systems
/// never change from run to run.
/// <para>
/// ALGORITHM. The 64-bit seed is expanded into the four 64-bit words of the state with
/// SplitMix64 (<c>z += 0x9E3779B97F4A7C15; z = (z ^ (z &gt;&gt; 30)) * 0xBF58476D1CE4E5B9;
/// z = (z ^ (z &gt;&gt; 27)) * 0x94D049BB133111EB; z ^= z &gt;&gt; 31</c>), and each draw is one step of
/// xoshiro256** (Blackman and Vigna): <c>result = rotl(s1 * 5, 7) * 9</c>, then
/// <c>t = s1 &lt;&lt; 17; s2 ^= s0; s3 ^= s1; s1 ^= s2; s0 ^= s3; s2 ^= t; s3 = rotl(s3, 45)</c>.
/// </para>
/// <para>
/// DERIVED VALUES. <see cref="NextDouble"/> = <c>(next &gt;&gt; 11) * 2^-53</c> (0 inclusive, 1
/// exclusive); <see cref="NextSingle"/> = <c>(next &gt;&gt; 40) * 2^-24</c>;
/// <see cref="Uniform(double, double)"/> = <c>low + (high - low) * NextDouble()</c>;
/// <see cref="NextInt(int)"/> uses Lemire's multiply-and-shift on the top 32 bits with rejection,
/// so it is exactly uniform; <see cref="Normal(double, double)"/> is the Box-Muller cosine branch
/// <c>mean + deviation * sqrt(-2 ln(1 - u1)) * cos(2 pi u2)</c> using two NextDouble draws.
/// The integer sequence (and therefore every value except Normal) is bit-exact everywhere; Normal
/// also goes through <see cref="Math.Log(double)"/> and <see cref="Math.Cos(double)"/>, which are
/// identical within one runtime and machine.
/// </para>
/// A source is not safe to use from two threads at once; give each thread its own (see
/// <see cref="ForKey(long, long)"/>).
/// </summary>
public sealed class DrawingRandom
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>
    /// Creates a source from a seed. Equal seeds give equal sequences.
    /// </summary>
    /// <param name="seed">The seed; any value, including 0 and negative values.</param>
    public DrawingRandom(long seed)
    {
        Seed = seed;
        ulong z = unchecked((ulong)seed);
        _s0 = SplitMix(ref z);
        _s1 = SplitMix(ref z);
        _s2 = SplitMix(ref z);
        _s3 = SplitMix(ref z);
    }

    /// <summary>The seed this source was created from.</summary>
    public long Seed { get; }

    /// <summary>
    /// Creates an independent source for one item of a larger whole - a particle, a stroke, a
    /// star - from a master seed and the item's key. The derived seed is
    /// <c>Mix(Mix(seed) + key)</c>, where Mix is the SplitMix64 finaliser, so the item's numbers
    /// never depend on how many other items were drawn first.
    /// </summary>
    /// <param name="seed">The master seed.</param>
    /// <param name="key">The item's key (its index, an id, ...).</param>
    /// <returns>A new source.</returns>
    public static DrawingRandom ForKey(long seed, long key) => new DrawingRandom(unchecked((long)Hash(seed, key)));

    /// <summary>
    /// The next raw 64-bit value of the sequence.
    /// </summary>
    /// <returns>A value spread evenly over the whole unsigned 64-bit range.</returns>
    public ulong NextUInt64()
    {
        unchecked
        {
            ulong result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);
            return result;
        }
    }

    /// <summary>
    /// A double in 0 (inclusive) .. 1 (exclusive), with 53 random bits.
    /// </summary>
    /// <returns>The value.</returns>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>
    /// A float in 0 (inclusive) .. 1 (exclusive), with 24 random bits.
    /// </summary>
    /// <returns>The value.</returns>
    public float NextSingle() => (NextUInt64() >> 40) * (1f / 16777216f);

    /// <summary>
    /// A double spread evenly over a range: <c>low + (high - low) * NextDouble()</c>.
    /// </summary>
    /// <param name="low">The range's start (inclusive).</param>
    /// <param name="high">The range's end (exclusive, unless equal to <paramref name="low"/>).</param>
    /// <returns>The value.</returns>
    public double Uniform(double low, double high) => low + ((high - low) * NextDouble());

    /// <summary>
    /// A normally distributed double (Box-Muller, cosine branch; one value per call, two draws).
    /// </summary>
    /// <param name="mean">The mean.</param>
    /// <param name="deviation">The standard deviation.</param>
    /// <returns>The value.</returns>
    public double Normal(double mean = 0.0, double deviation = 1.0)
    {
        double u = 1.0 - NextDouble();
        double v = NextDouble();
        return mean + (deviation * Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v));
    }

    /// <summary>
    /// A whole number in 0 .. <paramref name="maxExclusive"/> - 1, exactly uniform (Lemire's method
    /// on the top 32 bits of each draw, rejecting the few draws that would bias the result).
    /// </summary>
    /// <param name="maxExclusive">The exclusive upper limit; must be at least 1.</param>
    /// <returns>The number.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxExclusive"/> is less than 1.</exception>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive < 1) { throw new ArgumentOutOfRangeException(nameof(maxExclusive)); }
        uint range = (uint)maxExclusive;
        unchecked
        {
            ulong m = (ulong)(uint)(NextUInt64() >> 32) * range;
            uint low = (uint)m;
            if (low < range)
            {
                uint threshold = (0u - range) % range;
                while (low < threshold)
                {
                    m = (ulong)(uint)(NextUInt64() >> 32) * range;
                    low = (uint)m;
                }
            }
            return (int)(m >> 32);
        }
    }

    /// <summary>
    /// A whole number in <paramref name="minInclusive"/> .. <paramref name="maxExclusive"/> - 1,
    /// exactly uniform.
    /// </summary>
    /// <param name="minInclusive">The lowest possible value.</param>
    /// <param name="maxExclusive">The exclusive upper limit; must be greater than <paramref name="minInclusive"/>.</param>
    /// <returns>The number.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is empty or wider than <see cref="int.MaxValue"/>.</exception>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        long span = (long)maxExclusive - minInclusive;
        if (span < 1 || span > int.MaxValue) { throw new ArgumentOutOfRangeException(nameof(maxExclusive)); }
        return minInclusive + NextInt((int)span);
    }

    //---------------------------------------------------------------------------------------------

    //The SplitMix64 finaliser
    internal static ulong Mix(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    internal static ulong Hash(long seed, long key) => unchecked(Mix(Mix((ulong)seed) + (ulong)key));

    private static ulong SplitMix(ref ulong z)
    {
        unchecked
        {
            z += 0x9E3779B97F4A7C15UL;
            return Mix(z);
        }
    }

    private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
}
