using System;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// The plainest arrival: straight from a starting pose to the target, every value (position,
/// rotation, scales) eased along one curve - <see cref="EasingKind.CubicOut"/> by default, quick
/// at first and settling gently.
/// </summary>
public sealed class StraightArrival : IArrivalPath
{
    /// <summary>Creates the arrival.</summary>
    /// <param name="start">The pose it sets off in.</param>
    /// <param name="target">The pose it arrives in.</param>
    /// <param name="startTime">When it sets off.</param>
    /// <param name="arrivalTime">When it arrives; after <paramref name="startTime"/>.</param>
    /// <param name="easing">The curve of the whole way; <see cref="EasingKind.CubicOut"/> by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite or the arrival is not after the start.</exception>
    public StraightArrival(FormationPose start, FormationPose target, double startTime, double arrivalTime, EasingKind easing = EasingKind.CubicOut)
    {
        ArrivalMath.CheckPose(start, nameof(start));
        ArrivalMath.CheckPose(target, nameof(target));
        ArrivalMath.CheckTimes(startTime, arrivalTime);
        if (!Enum.IsDefined(easing)) { throw new ArgumentOutOfRangeException(nameof(easing)); }
        Start = start;
        Target = target;
        StartTime = startTime;
        ArrivalTime = arrivalTime;
        Easing = easing;
    }

    /// <summary>The pose it sets off in.</summary>
    public FormationPose Start { get; }

    /// <inheritdoc />
    public FormationPose Target { get; }

    /// <inheritdoc />
    public double StartTime { get; }

    /// <inheritdoc />
    public double ArrivalTime { get; }

    /// <summary>The curve of the whole way.</summary>
    public EasingKind Easing { get; }

    /// <inheritdoc />
    public FormationPose GetPoseAt(double seconds)
    {
        if (seconds >= ArrivalTime) { return Target; }
        double e = ArrivalMath.Eased(seconds, StartTime, ArrivalTime, Easing);
        return new FormationPose(ArrivalMath.Lerp(Start.X, Target.X, e), ArrivalMath.Lerp(Start.Y, Target.Y, e),
            ArrivalMath.Lerp(Start.Rotation, Target.Rotation, e), ArrivalMath.Lerp(Start.ScaleX, Target.ScaleX, e),
            ArrivalMath.Lerp(Start.ScaleY, Target.ScaleY, e));
    }
}
