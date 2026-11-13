using System;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// Spots arrive from the shape's centre outwards (or, reversed, from the outside in): the key is
/// the distance from the centre of the shape's bounds.
/// </summary>
public sealed class CenterOutArrivalOrder : IArrivalOrder
{
    private PointF _center;

    /// <summary>Creates the order.</summary>
    /// <param name="reverse"><c>true</c> to fill from the outside in.</param>
    public CenterOutArrivalOrder(bool reverse = false)
    {
        IsReversed = reverse;
    }

    /// <summary>Indicates whether the shape fills from the outside in.</summary>
    public bool IsReversed { get; }

    /// <inheritdoc />
    public void Prepare(FormationArea area, long seed)
        => _center = new PointF(area.Bounds.Left + (area.Bounds.Width / 2), area.Bounds.Top + (area.Bounds.Height / 2));

    /// <inheritdoc />
    public float GetKey(float x, float y, float clearance)
    {
        float distance = MathF.Sqrt(((x - _center.X) * (x - _center.X)) + ((y - _center.Y) * (y - _center.Y)));
        return IsReversed ? -distance : distance;
    }
}
