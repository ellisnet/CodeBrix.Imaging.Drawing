using System;
using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// Spots arrive in a sweep across the shape: the key is how far a spot lies along a direction
/// (0 degrees = left to right, 90 = top to bottom, 270 = bottom to top; clockwise on screen),
/// plus an optional seeded random spread so the front is ragged rather than ruled.
/// </summary>
public sealed class DirectionalArrivalOrder : IArrivalOrder
{
    private readonly double _cos;
    private readonly double _sin;
    private DrawingRandom _random;

    /// <summary>Creates the order.</summary>
    /// <param name="angle">The direction of the sweep, degrees clockwise from +x.</param>
    /// <param name="scatter">A per-spot random spread of the keys, calibrated units (0 = a straight front).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite or the scatter is negative.</exception>
    public DirectionalArrivalOrder(float angle, float scatter = 0f)
    {
        if (!Single.IsFinite(angle)) { throw new ArgumentOutOfRangeException(nameof(angle)); }
        if (!Single.IsFinite(scatter) || scatter < 0f) { throw new ArgumentOutOfRangeException(nameof(scatter)); }
        Angle = angle;
        Scatter = scatter;
        double radians = angle * (Math.PI / 180.0);
        _cos = Math.Cos(radians);
        _sin = Math.Sin(radians);
    }

    /// <summary>The direction of the sweep, degrees.</summary>
    public float Angle { get; }

    /// <summary>The per-spot random spread of the keys, calibrated units.</summary>
    public float Scatter { get; }

    /// <inheritdoc />
    public void Prepare(FormationArea area, long seed) => _random = new DrawingRandom(seed ^ 0x1B873593L);

    /// <inheritdoc />
    public float GetKey(float x, float y, float clearance)
    {
        double key = (x * _cos) + (y * _sin);
        if (Scatter > 0f) { key += _random.Uniform(-Scatter, Scatter); }
        return (float)key;
    }
}
