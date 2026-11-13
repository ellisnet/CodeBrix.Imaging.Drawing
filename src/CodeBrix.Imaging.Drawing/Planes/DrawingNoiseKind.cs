namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// The kinds of coherent noise <see cref="DrawingNoise"/> produces.
/// </summary>
public enum DrawingNoiseKind
{
    /// <summary>
    /// Value noise: a random value at every whole-number lattice point, blended smoothly between
    /// them. Blobby and soft; good for clouds, mottling and slow flicker.
    /// </summary>
    Value = 0,

    /// <summary>
    /// Gradient (Perlin-style) noise: a random direction at every lattice point, which gives
    /// finer, more natural-looking detail than value noise and exactly 0 at the lattice points.
    /// </summary>
    Gradient = 1,
}
