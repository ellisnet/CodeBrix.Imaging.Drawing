using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// One object of a formation: when it arrives, and the way it travels there - or no way at all,
/// for one that simply appears at its arrival (tucked under another, say). Turned into session
/// elements by <see cref="CodeBrix.Imaging.Drawing.Models.DrawingAnimations.BindFormation"/>.
/// </summary>
public sealed class FormationMember
{
    /// <summary>Creates a member that travels: it arrives at its path's arrival time.</summary>
    /// <param name="path">Its way to its place.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    public FormationMember(IArrivalPath path)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        ArrivalTime = path.ArrivalTime;
    }

    /// <summary>Creates a member that does not travel: it appears, at rest, at its arrival time.</summary>
    /// <param name="arrivalTime">When it appears.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="arrivalTime"/> is not finite.</exception>
    public FormationMember(double arrivalTime)
    {
        if (!Double.IsFinite(arrivalTime)) { throw new ArgumentOutOfRangeException(nameof(arrivalTime)); }
        ArrivalTime = arrivalTime;
    }

    /// <summary>Its way to its place; <c>null</c> for a member that just appears.</summary>
    public IArrivalPath Path { get; }

    /// <summary>When it arrives (from then on it is at rest), seconds.</summary>
    public double ArrivalTime { get; }

    /// <summary>When it sets off (its path's start); its arrival time for a member that just appears.</summary>
    public double StartTime => Path?.StartTime ?? ArrivalTime;

    /// <summary>Indicates whether it is on its way at a time (<see cref="StartTime"/> &lt;= t &lt; <see cref="ArrivalTime"/>).</summary>
    /// <param name="seconds">The time.</param>
    /// <returns><c>true</c> while it travels.</returns>
    public bool IsTravellingAt(double seconds) => Path != null && seconds >= Path.StartTime && seconds < ArrivalTime;

    /// <summary>
    /// The simplest formation: one member per target spot, the k-th spot arriving at the k-th
    /// time, each with the way <paramref name="createPath"/> makes for it (the spot's resting pose
    /// is for you to choose - the spot's position, a rotation, a scale).
    /// </summary>
    /// <param name="targets">The spots, in arrival order (as <see cref="FormationTargets"/> returns them).</param>
    /// <param name="arrivalTimes">One arrival time per spot (as <see cref="ArrivalSchedule.GetTimes"/> returns them).</param>
    /// <param name="createPath">Makes the way for spot k from the spot, its arrival time and k.</param>
    /// <returns>The members, in the same order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null, or the function returns null.</exception>
    /// <exception cref="ArgumentException">Thrown when the counts differ.</exception>
    public static List<FormationMember> FromTargets(IReadOnlyList<FormationTarget> targets, IReadOnlyList<double> arrivalTimes,
        Func<FormationTarget, double, int, IArrivalPath> createPath)
    {
        if (targets == null) { throw new ArgumentNullException(nameof(targets)); }
        if (arrivalTimes == null) { throw new ArgumentNullException(nameof(arrivalTimes)); }
        if (createPath == null) { throw new ArgumentNullException(nameof(createPath)); }
        if (targets.Count != arrivalTimes.Count) { throw new ArgumentException("There must be one arrival time per target.", nameof(arrivalTimes)); }
        var members = new List<FormationMember>(targets.Count);
        for (int k = 0; k < targets.Count; k++)
        {
            members.Add(new FormationMember(createPath(targets[k], arrivalTimes[k], k)
                ?? throw new ArgumentNullException(nameof(createPath), "The path function returned null.")));
        }
        return members;
    }
}
