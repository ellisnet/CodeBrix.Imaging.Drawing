using System;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// How <see cref="FormationTargets.Fill(FormationArea, FormationTargetOptions)"/> covers a shape
/// with spots: their spacing (a triangular grid), how far each strays at random from its grid
/// point, how far they keep from the edge, the seed, and the order they fill in. The same shape
/// and options always give the same spots.
/// </summary>
public sealed class FormationTargetOptions
{
    /// <summary>The distance between neighbouring spots (rows of a triangular grid), calibrated units; 16 by default.</summary>
    public float Spacing { get; set; } = 16f;

    /// <summary>How far each spot is moved at random from its grid point, as a fraction of the spacing (0 = a perfect grid); 0.33 by default.</summary>
    public float Jitter { get; set; } = 0.33f;

    /// <summary>The least distance from a spot to the shape's edge, calibrated units (spots nearer are dropped); 4 by default.</summary>
    public float EdgeClearance { get; set; } = 4f;

    /// <summary>The seed of the jitter (and of the order's own randomness); 1 by default.</summary>
    public long Seed { get; set; } = 1;

    /// <summary>The arrival order; <c>null</c> (the default) for a <see cref="RandomArrivalOrder"/>.</summary>
    public IArrivalOrder Order { get; set; }

    internal void Validate()
    {
        if (!Single.IsFinite(Spacing) || Spacing <= 0f) { throw new ArgumentOutOfRangeException(nameof(Spacing)); }
        if (!Single.IsFinite(Jitter) || Jitter < 0f) { throw new ArgumentOutOfRangeException(nameof(Jitter)); }
        if (!Single.IsFinite(EdgeClearance)) { throw new ArgumentOutOfRangeException(nameof(EdgeClearance)); }
    }
}
