using System;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// One live particle at one moment, as returned by <see cref="DrawingParticleSystem.GetParticles(double)"/>
/// - pure data, nothing drawn. Positions and sizes are in the same units the emitters use ("pixels":
/// a plane's pixels, or calibrated drawing units when a drawing session draws the particles).
/// </summary>
public readonly struct DrawingParticle : IEquatable<DrawingParticle>
{
    /// <summary>
    /// Creates a particle snapshot.
    /// </summary>
    /// <param name="emitterIndex">The index of the emitter in the system.</param>
    /// <param name="index">The particle's birth number within its emitter.</param>
    /// <param name="birthTime">When it was born, in seconds.</param>
    /// <param name="age">How long it has lived at the queried time, in seconds.</param>
    /// <param name="lifetime">How long it lives in total, in seconds.</param>
    /// <param name="x">The centre's x, in pixels.</param>
    /// <param name="y">The centre's y, in pixels.</param>
    /// <param name="size">The size (diameter), in pixels.</param>
    /// <param name="opacity">The opacity, 0..1.</param>
    /// <param name="color">The colour (straight alpha; its own alpha is separate from <paramref name="opacity"/>).</param>
    public DrawingParticle(int emitterIndex, long index, double birthTime, double age, double lifetime,
        float x, float y, float size, float opacity, Color color)
    {
        EmitterIndex = emitterIndex;
        Index = index;
        BirthTime = birthTime;
        Age = age;
        Lifetime = lifetime;
        X = x;
        Y = y;
        Size = size;
        Opacity = opacity;
        Color = color;
    }

    /// <summary>The index of the emitter in <see cref="DrawingParticleSystem.Emitters"/>.</summary>
    public int EmitterIndex { get; }

    /// <summary>
    /// The particle's birth number within its emitter: burst particles are 0 .. Burst - 1, then
    /// continuously emitted particles follow in birth order. Stable across queries, so it can key
    /// per-particle extras (a sprite, a rotation).
    /// </summary>
    public long Index { get; }

    /// <summary>When the particle was born, in seconds.</summary>
    public double BirthTime { get; }

    /// <summary>How long the particle has lived at the queried time, in seconds.</summary>
    public double Age { get; }

    /// <summary>How long the particle lives in total, in seconds.</summary>
    public double Lifetime { get; }

    /// <summary>How far through its life the particle is: <c>Age / Lifetime</c>, 0 (just born) .. 1 (about to die).</summary>
    public double LifeFraction => Lifetime > 0 ? Age / Lifetime : 0;

    /// <summary>The centre's x, in pixels.</summary>
    public float X { get; }

    /// <summary>The centre's y, in pixels.</summary>
    public float Y { get; }

    /// <summary>The size (diameter), in pixels.</summary>
    public float Size { get; }

    /// <summary>The opacity, 0..1 - multiply the colour's alpha by it when drawing.</summary>
    public float Opacity { get; }

    /// <summary>The colour at this point of the particle's life.</summary>
    public Color Color { get; }

    /// <summary>Compares every field.</summary>
    /// <param name="other">The other particle.</param>
    /// <returns><c>true</c> when every field is equal.</returns>
    public bool Equals(DrawingParticle other)
        => EmitterIndex == other.EmitterIndex && Index == other.Index && BirthTime.Equals(other.BirthTime)
            && Age.Equals(other.Age) && Lifetime.Equals(other.Lifetime) && X.Equals(other.X) && Y.Equals(other.Y)
            && Size.Equals(other.Size) && Opacity.Equals(other.Opacity) && Color.Equals(other.Color);

    /// <summary>Compares every field with another boxed particle.</summary>
    /// <param name="obj">The other object.</param>
    /// <returns><c>true</c> when it is a particle with equal fields.</returns>
    public override bool Equals(object obj) => obj is DrawingParticle other && Equals(other);

    /// <summary>A hash of the particle's identity and position.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(EmitterIndex, Index, Age, X, Y, Size, Opacity, Color);

    /// <summary>Whether two particles are equal in every field.</summary>
    /// <param name="left">The first particle.</param>
    /// <param name="right">The second particle.</param>
    /// <returns><c>true</c> when equal.</returns>
    public static bool operator ==(DrawingParticle left, DrawingParticle right) => left.Equals(right);

    /// <summary>Whether two particles differ in any field.</summary>
    /// <param name="left">The first particle.</param>
    /// <param name="right">The second particle.</param>
    /// <returns><c>true</c> when they differ.</returns>
    public static bool operator !=(DrawingParticle left, DrawingParticle right) => !left.Equals(right);

    /// <summary>A short description for debugging.</summary>
    /// <returns>The emitter, index, position, size and opacity.</returns>
    public override string ToString() => $"Particle {EmitterIndex}/{Index} at ({X}, {Y}) size {Size} opacity {Opacity}";
}
