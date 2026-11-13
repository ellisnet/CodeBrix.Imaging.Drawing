using System;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// The settings of one particle emitter in a <see cref="DrawingParticleSystem"/>: where and when
/// particles are born, how they move and how they look over their life. Positions and sizes are
/// in the units the particles are drawn in - "pixels" below: pixels of a plane you paint yourself,
/// or calibrated drawing units when a drawing session draws them (DrawParticles / ParticlesShape) -
/// times seconds (double), angles DEGREES clockwise on screen from +x, with y pointing DOWN
/// (screen space): a direction of 0 moves right, 90 moves down, -90 moves up - the unit and sense of
/// a drawing transform's rotation and of a path point's angle.
/// <para>
/// Every random choice (lifetime, launch angle, speed, spawn offset, size) is drawn per particle
/// from <see cref="DrawingRandom.ForKey(long, long)"/>, so changing one emitter or querying a
/// different time never changes any other particle. Do not change an emitter's settings while a
/// query is running on another thread.
/// </para>
/// </summary>
public sealed class DrawingParticleEmitter
{
    private float _rate;
    private int _burst;
    private double _startTime;
    private double _endTime = double.PositiveInfinity;
    private double _lifetime = 1.0;
    private double _lifetimeVariation;
    private float _spawnRadius;
    private float _speedVariation;
    private float _sizeVariation;

    /// <summary>The emitter's x position, in pixels.</summary>
    public float X { get; set; }

    /// <summary>The emitter's y position, in pixels.</summary>
    public float Y { get; set; }

    /// <summary>
    /// Particles start at a uniformly random point within a disc of this radius around
    /// (<see cref="X"/>, <see cref="Y"/>); 0 (the default) starts them all at the emitter.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative, NaN or infinite value.</exception>
    public float SpawnRadius
    {
        get => _spawnRadius;
        set => _spawnRadius = RequireNonNegative(value, nameof(SpawnRadius));
    }

    /// <summary>
    /// Continuous emission rate, particles per second, from <see cref="StartTime"/> until
    /// <see cref="EndTime"/>: particle n (counting from 0) is born at <c>StartTime + n / Rate</c>.
    /// 0 (the default) emits only the burst.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative, NaN or infinite value.</exception>
    public float Rate
    {
        get => _rate;
        set => _rate = RequireNonNegative(value, nameof(Rate));
    }

    /// <summary>The number of particles born together at <see cref="StartTime"/> (0 by default).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
    public int Burst
    {
        get => _burst;
        set
        {
            if (value < 0) { throw new ArgumentOutOfRangeException(nameof(Burst)); }
            _burst = value;
        }
    }

    /// <summary>When emission starts, in seconds (0 by default).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or infinity.</exception>
    public double StartTime
    {
        get => _startTime;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) { throw new ArgumentOutOfRangeException(nameof(StartTime)); }
            _startTime = value;
        }
    }

    /// <summary>
    /// When continuous emission stops, in seconds (exclusive; particles already born live on).
    /// Positive infinity, the default, never stops.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or negative infinity.</exception>
    public double EndTime
    {
        get => _endTime;
        set
        {
            if (double.IsNaN(value) || double.IsNegativeInfinity(value)) { throw new ArgumentOutOfRangeException(nameof(EndTime)); }
            _endTime = value;
        }
    }

    /// <summary>The mean particle lifetime, in seconds (1 by default).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not greater than 0, or infinite.</exception>
    public double Lifetime
    {
        get => _lifetime;
        set
        {
            if (!(value > 0.0) || double.IsInfinity(value)) { throw new ArgumentOutOfRangeException(nameof(Lifetime)); }
            _lifetime = value;
        }
    }

    /// <summary>
    /// Each particle's lifetime is <c>Lifetime + uniform(-LifetimeVariation, +LifetimeVariation)</c>,
    /// never less than 1% of <see cref="Lifetime"/> (0 by default).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative, NaN or infinite value.</exception>
    public double LifetimeVariation
    {
        get => _lifetimeVariation;
        set
        {
            if (!(value >= 0.0) || double.IsInfinity(value)) { throw new ArgumentOutOfRangeException(nameof(LifetimeVariation)); }
            _lifetimeVariation = value;
        }
    }

    /// <summary>The mean launch direction, in degrees clockwise on screen from +x (0 = right, 90 = down, -90 = up).</summary>
    public float Direction { get; set; }

    /// <summary>
    /// The full width of the launch cone, in degrees: each particle launches at
    /// <c>Direction + uniform(-Spread / 2, +Spread / 2)</c>. 360 launches in every direction.
    /// </summary>
    public float Spread { get; set; }

    /// <summary>The mean launch speed, in pixels per second.</summary>
    public float Speed { get; set; }

    /// <summary>Each particle's speed is <c>Speed + uniform(-SpeedVariation, +SpeedVariation)</c> (0 by default).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative, NaN or infinite value.</exception>
    public float SpeedVariation
    {
        get => _speedVariation;
        set => _speedVariation = RequireNonNegative(value, nameof(SpeedVariation));
    }

    /// <summary>Horizontal acceleration, pixels per second squared (gravity, wind gusts).</summary>
    public float GravityX { get; set; }

    /// <summary>Vertical acceleration, pixels per second squared (positive pulls DOWN).</summary>
    public float GravityY { get; set; }

    /// <summary>A constant horizontal velocity added to every particle, pixels per second (steady wind, drift).</summary>
    public float DriftX { get; set; }

    /// <summary>A constant vertical velocity added to every particle, pixels per second.</summary>
    public float DriftY { get; set; }

    /// <summary>The size (diameter) at birth, in pixels (4 by default).</summary>
    public float StartSize { get; set; } = 4f;

    /// <summary>The size at the end of life, in pixels (4 by default); sizes blend linearly over life.</summary>
    public float EndSize { get; set; } = 4f;

    /// <summary>
    /// Each particle's two sizes are multiplied by <c>1 + uniform(-SizeVariation, +SizeVariation)</c>
    /// (0 by default; 0.25 means up to 25% larger or smaller).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative, NaN or infinite value.</exception>
    public float SizeVariation
    {
        get => _sizeVariation;
        set => _sizeVariation = RequireNonNegative(value, nameof(SizeVariation));
    }

    /// <summary>The opacity at birth, 0..1 (1 by default).</summary>
    public float StartOpacity { get; set; } = 1f;

    /// <summary>The opacity at the end of life, 0..1 (0 by default); opacity blends linearly over life.</summary>
    public float EndOpacity { get; set; }

    /// <summary>The colour at birth (white by default).</summary>
    public Color StartColor { get; set; } = Color.White;

    /// <summary>
    /// The colour at the end of life (white by default). Colours blend linearly over life on their
    /// 8-bit channels (straight alpha), rounded half up.
    /// </summary>
    public Color EndColor { get; set; } = Color.White;

    /// <summary>
    /// Makes an independent copy of these settings.
    /// </summary>
    /// <returns>A new emitter with the same settings.</returns>
    public DrawingParticleEmitter Clone() => (DrawingParticleEmitter)MemberwiseClone();

    private static float RequireNonNegative(float value, string name)
    {
        if (!(value >= 0f) || float.IsInfinity(value)) { throw new ArgumentOutOfRangeException(name); }
        return value;
    }
}
