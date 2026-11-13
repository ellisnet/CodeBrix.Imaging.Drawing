using System;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// Spots arrive in an order of your own: a function gives each spot its key from its position and
/// its distance from the edge (smaller = earlier; infinity = leave the spot empty) - for example
/// <c>new CustomArrivalOrder((x, y, clearance) =&gt; -clearance)</c> fills the deepest spots first.
/// </summary>
public sealed class CustomArrivalOrder : IArrivalOrder
{
    private readonly Func<float, float, float, float> _key;

    /// <summary>Creates the order.</summary>
    /// <param name="key">Returns a spot's key from its x, y and clearance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    public CustomArrivalOrder(Func<float, float, float, float> key)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <inheritdoc />
    public void Prepare(FormationArea area, long seed)
    {
    }

    /// <inheritdoc />
    public float GetKey(float x, float y, float clearance) => _key(x, y, clearance);
}
