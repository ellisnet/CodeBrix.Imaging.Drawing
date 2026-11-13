namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// One object's way to its place in a formation, as a PURE FUNCTION OF TIME: an arrival "style".
/// It sets off at <see cref="StartTime"/> and must end EXACTLY in <see cref="Target"/> at
/// <see cref="ArrivalTime"/>, so the travelling object can be swapped for a resting one at that
/// moment without a jump. Built in: <see cref="SwayingFallArrival"/> (a leaf's fall),
/// <see cref="StraightArrival"/>, <see cref="CurvedArrival"/> (a drift in along a bend) and
/// <see cref="SpiralArrival"/>; implement it for any other style.
/// </summary>
public interface IArrivalPath
{
    /// <summary>The pose it arrives in.</summary>
    FormationPose Target { get; }

    /// <summary>When it sets off, seconds.</summary>
    double StartTime { get; }

    /// <summary>When it arrives (and is at rest), seconds.</summary>
    double ArrivalTime { get; }

    /// <summary>
    /// The pose at a time - meaningful between <see cref="StartTime"/> and
    /// <see cref="ArrivalTime"/>; at or after the arrival it is <see cref="Target"/>.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The pose.</returns>
    FormationPose GetPoseAt(double seconds);
}
