namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// Decides the order a formation's spots are filled in, by giving each spot a KEY (smaller =
/// earlier). Built in: <see cref="RandomArrivalOrder"/>, <see cref="CenterOutArrivalOrder"/>,
/// <see cref="DirectionalArrivalOrder"/>, <see cref="HeapArrivalOrder"/> (bottom up, the way a
/// heap grows) and <see cref="CustomArrivalOrder"/> (a function of your own); implement it for
/// anything else. An order keeps what <see cref="Prepare"/> worked out, so use one instance for one
/// fill at a time.
/// </summary>
public interface IArrivalOrder
{
    /// <summary>Called once per fill, before any <see cref="GetKey"/>.</summary>
    /// <param name="area">The shape being filled.</param>
    /// <param name="seed">The fill's seed (for an order's own randomness).</param>
    void Prepare(FormationArea area, long seed);

    /// <summary>A spot's key.</summary>
    /// <param name="x">The spot's x, calibrated units.</param>
    /// <param name="y">The spot's y, calibrated units.</param>
    /// <param name="clearance">Its distance from the shape's edge.</param>
    /// <returns>The key (smaller arrives earlier; infinity or NaN = leave this spot empty).</returns>
    float GetKey(float x, float y, float clearance);
}
