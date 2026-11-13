using System;
using System.Collections.Generic;
using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// A deterministic particle system as DATA: a seed and a list of
/// <see cref="DrawingParticleEmitter"/> settings, from which <see cref="GetParticles(double)"/>
/// returns the live particles at any moment as a PURE FUNCTION of the time and the seed - no
/// state is stepped from frame to frame, so frames can be rendered in any order, skipped,
/// repeated or rendered in parallel and always agree. Nothing is drawn; draw the returned
/// positions, sizes, opacities and colours however you like.
/// <para>
/// THE RULE. Emitter e's particle k is born at <c>StartTime</c> for the burst (k = 0 .. Burst - 1)
/// or at <c>StartTime + n / Rate</c> for continuous particle n (k = Burst + n, only while that is
/// before <c>EndTime</c>). Its random choices come from
/// <c>DrawingRandom.ForKey(Mix(Mix(seed) + e), k)</c>, drawn in this fixed order: lifetime
/// variation, launch angle, speed variation, spawn angle, spawn radius (<c>SpawnRadius * sqrt(u)</c>,
/// uniform over the disc), size variation. It is alive at time t when
/// <c>birth &lt;= t &lt; birth + lifetime</c>; with <c>age = t - birth</c> and launch velocity v
/// (speed along the launch angle - <c>Direction + uniform(-Spread / 2, +Spread / 2)</c> degrees,
/// turned into radians by <c>x (pi / 180)</c> in double - plus drift), its centre is
/// <c>emitter + spawnOffset + v * age + gravity * age² / 2</c>. Size, opacity and colour blend
/// linearly from start to end values by <c>age / lifetime</c>. All arithmetic is double precision
/// (positions, sizes and opacities are then stored as float); the trigonometry goes through
/// <see cref="Math.Cos(double)"/> and <see cref="Math.Sin(double)"/>, identical within one runtime
/// and machine and in both drawing packages.
/// </para>
/// <para>
/// Results are ordered by emitter, then by birth number. Querying is thread-safe as long as no
/// emitter is added or changed meanwhile.
/// </para>
/// </summary>
public sealed class DrawingParticleSystem
{
    private readonly List<DrawingParticleEmitter> _emitters = new List<DrawingParticleEmitter>();

    /// <summary>
    /// Creates an empty particle system.
    /// </summary>
    /// <param name="seed">The seed every particle's random choices derive from.</param>
    public DrawingParticleSystem(long seed)
    {
        Seed = seed;
    }

    /// <summary>The seed every particle's random choices derive from.</summary>
    public long Seed { get; }

    /// <summary>The emitters, in the order they were added (the order of <see cref="DrawingParticle.EmitterIndex"/>).</summary>
    public IReadOnlyList<DrawingParticleEmitter> Emitters => _emitters;

    /// <summary>
    /// Adds an emitter. The system keeps the instance (it does not copy it), so later changes to
    /// its settings show in later queries.
    /// </summary>
    /// <param name="emitter">The emitter settings.</param>
    /// <returns>The same emitter, for fluent set-up.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="emitter"/> is null.</exception>
    public DrawingParticleEmitter AddEmitter(DrawingParticleEmitter emitter)
    {
        if (emitter == null) { throw new ArgumentNullException(nameof(emitter)); }
        _emitters.Add(emitter);
        return emitter;
    }

    /// <summary>
    /// Returns every particle alive at a moment.
    /// </summary>
    /// <param name="seconds">The moment, in seconds.</param>
    /// <returns>A new list of the live particles, by emitter then birth number.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seconds"/> is NaN or infinite.</exception>
    public IReadOnlyList<DrawingParticle> GetParticles(double seconds)
    {
        var results = new List<DrawingParticle>();
        GetParticles(seconds, results);
        return results;
    }

    /// <summary>
    /// Fills a list with every particle alive at a moment (the list is cleared first), so a
    /// renderer can reuse one list for every frame.
    /// </summary>
    /// <param name="seconds">The moment, in seconds.</param>
    /// <param name="results">The list to fill.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="results"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seconds"/> is NaN or infinite.</exception>
    public void GetParticles(double seconds, List<DrawingParticle> results)
    {
        if (results == null) { throw new ArgumentNullException(nameof(results)); }
        if (double.IsNaN(seconds) || double.IsInfinity(seconds)) { throw new ArgumentOutOfRangeException(nameof(seconds)); }
        double time = seconds;
        results.Clear();
        for (int e = 0; e < _emitters.Count; e++)
        {
            DrawingParticleEmitter emitter = _emitters[e];
            long emitterSeed = unchecked((long)DrawingRandom.Hash(Seed, e));
            if (time < emitter.StartTime) { continue; }

            for (int k = 0; k < emitter.Burst; k++)
            {
                TryAdd(results, emitter, e, emitterSeed, k, emitter.StartTime, time);
            }

            double rate = emitter.Rate;
            if (!(rate > 0)) { continue; }
            double maxLife = emitter.Lifetime + emitter.LifetimeVariation;
            double end = Math.Min(time, emitter.EndTime);
            long last = (long)Math.Floor((end - emitter.StartTime) * rate);
            long first = Math.Max(0L, (long)Math.Floor((time - maxLife - emitter.StartTime) * rate) - 1L);
            for (long n = first; n <= last; n++)
            {
                double birth = emitter.StartTime + (n / rate);
                if (birth > time || birth >= emitter.EndTime) { continue; }
                TryAdd(results, emitter, e, emitterSeed, emitter.Burst + n, birth, time);
            }
        }
    }

    private static void TryAdd(List<DrawingParticle> results, DrawingParticleEmitter emitter, int emitterIndex,
        long emitterSeed, long index, double birth, double time)
    {
        var random = DrawingRandom.ForKey(emitterSeed, index);
        double lifetime = emitter.Lifetime + random.Uniform(-emitter.LifetimeVariation, emitter.LifetimeVariation);
        double shortest = emitter.Lifetime * 0.01;
        if (lifetime < shortest) { lifetime = shortest; }
        double angle = (emitter.Direction + random.Uniform(-emitter.Spread / 2.0, emitter.Spread / 2.0)) * (Math.PI / 180.0);
        double speed = emitter.Speed + random.Uniform(-emitter.SpeedVariation, emitter.SpeedVariation);
        double spawnAngle = random.Uniform(0.0, 2.0 * Math.PI);
        double spawnRadius = emitter.SpawnRadius * Math.Sqrt(random.NextDouble());
        double sizeFactor = 1.0 + random.Uniform(-emitter.SizeVariation, emitter.SizeVariation);

        double age = time - birth;
        if (age < 0 || age >= lifetime) { return; }

        double vx = (speed * Math.Cos(angle)) + emitter.DriftX;
        double vy = (speed * Math.Sin(angle)) + emitter.DriftY;
        double x = emitter.X + (spawnRadius * Math.Cos(spawnAngle)) + (vx * age) + (0.5 * emitter.GravityX * age * age);
        double y = emitter.Y + (spawnRadius * Math.Sin(spawnAngle)) + (vy * age) + (0.5 * emitter.GravityY * age * age);
        double f = age / lifetime;
        double size = (emitter.StartSize + ((emitter.EndSize - (double)emitter.StartSize) * f)) * sizeFactor;
        double opacity = emitter.StartOpacity + ((emitter.EndOpacity - (double)emitter.StartOpacity) * f);

        results.Add(new DrawingParticle(emitterIndex, index, birth, age, lifetime, (float)x, (float)y, (float)size, (float)opacity,
            BlendColor(emitter.StartColor, emitter.EndColor, f)));
    }

    private static Color BlendColor(Color start, Color end, double f)
    {
        Rgba32 a = start.ToRgba32();
        Rgba32 b = end.ToRgba32();
        return Color.FromRgba(Channel(a.R, b.R, f), Channel(a.G, b.G, f), Channel(a.B, b.B, f), Channel(a.A, b.A, f));
    }

    private static byte Channel(byte a, byte b, double f)
    {
        double v = a + ((b - a) * f);
        if (!(v > 0)) { return 0; }
        return v >= 255 ? (byte)255 : (byte)Math.Floor(v + 0.5);
    }
}
