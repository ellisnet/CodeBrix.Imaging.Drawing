using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>Spots arrive in a seeded random order (keys 0..1 from a <see cref="DrawingRandom"/> seeded from the fill's seed).</summary>
public sealed class RandomArrivalOrder : IArrivalOrder
{
    private DrawingRandom _random;

    /// <summary>Creates the order.</summary>
    public RandomArrivalOrder()
    {
    }

    /// <inheritdoc />
    public void Prepare(FormationArea area, long seed) => _random = new DrawingRandom(seed ^ 0x5DEECE66DL);

    /// <inheritdoc />
    public float GetKey(float x, float y, float clearance) => _random.NextSingle();
}
