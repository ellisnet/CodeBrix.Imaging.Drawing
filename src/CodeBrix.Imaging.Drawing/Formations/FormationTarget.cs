namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// One spot of a formation's shape for one object to come to rest on, with its turn to be filled
/// (made by <see cref="FormationTargets.Fill(FormationArea, FormationTargetOptions)"/>).
/// </summary>
public readonly struct FormationTarget
{
    /// <summary>Creates a target spot.</summary>
    /// <param name="position">The spot, calibrated units.</param>
    /// <param name="key">Its arrival key: smaller arrives earlier (in the order's own units).</param>
    /// <param name="clearance">Its distance from the shape's edge, calibrated units.</param>
    public FormationTarget(PointF position, float key, float clearance)
    {
        Position = position;
        Key = key;
        Clearance = clearance;
    }

    /// <summary>The spot, calibrated units.</summary>
    public PointF Position { get; }

    /// <summary>The arrival key given by the <see cref="IArrivalOrder"/>: smaller arrives earlier.</summary>
    public float Key { get; }

    /// <summary>The spot's distance from the shape's edge (how deep inside it lies), calibrated units.</summary>
    public float Clearance { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Position} key {Key} clearance {Clearance}";
}
