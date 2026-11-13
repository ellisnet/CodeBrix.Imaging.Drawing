using System;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// A drift in along a bend: from a starting pose to the target along a quadratic curve whose
/// control point lies off the straight line's middle by <see cref="Bend"/> times the distance
/// (positive bends to the right of the direction of travel, negative to the left), position,
/// rotation and scales eased along one curve (<see cref="EasingKind.CubicOut"/> by default).
/// </summary>
public sealed class CurvedArrival : IArrivalPath
{
    private readonly double _controlX;
    private readonly double _controlY;

    /// <summary>Creates the arrival.</summary>
    /// <param name="start">The pose it sets off in.</param>
    /// <param name="target">The pose it arrives in.</param>
    /// <param name="startTime">When it sets off.</param>
    /// <param name="arrivalTime">When it arrives; after <paramref name="startTime"/>.</param>
    /// <param name="bend">How far the curve bows out, as a fraction of the distance (0 = straight); 0.35 by default.</param>
    /// <param name="easing">The curve of the whole way; <see cref="EasingKind.CubicOut"/> by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite or the arrival is not after the start.</exception>
    public CurvedArrival(FormationPose start, FormationPose target, double startTime, double arrivalTime, float bend = 0.35f,
        EasingKind easing = EasingKind.CubicOut)
    {
        ArrivalMath.CheckPose(start, nameof(start));
        ArrivalMath.CheckPose(target, nameof(target));
        ArrivalMath.CheckTimes(startTime, arrivalTime);
        if (!Single.IsFinite(bend)) { throw new ArgumentOutOfRangeException(nameof(bend)); }
        if (!Enum.IsDefined(easing)) { throw new ArgumentOutOfRangeException(nameof(easing)); }
        Start = start;
        Target = target;
        StartTime = startTime;
        ArrivalTime = arrivalTime;
        Bend = bend;
        Easing = easing;
        double dx = target.X - (double)start.X;
        double dy = target.Y - (double)start.Y;
        //The right of the direction of travel, on screen (y down), is (-dy, dx)
        _controlX = ((start.X + (double)target.X) / 2.0) - (dy * bend);
        _controlY = ((start.Y + (double)target.Y) / 2.0) + (dx * bend);
    }

    /// <summary>The pose it sets off in.</summary>
    public FormationPose Start { get; }

    /// <inheritdoc />
    public FormationPose Target { get; }

    /// <inheritdoc />
    public double StartTime { get; }

    /// <inheritdoc />
    public double ArrivalTime { get; }

    /// <summary>How far the curve bows out, as a fraction of the distance.</summary>
    public float Bend { get; }

    /// <summary>The curve of the whole way.</summary>
    public EasingKind Easing { get; }

    /// <inheritdoc />
    public FormationPose GetPoseAt(double seconds)
    {
        if (seconds >= ArrivalTime) { return Target; }
        double e = ArrivalMath.Eased(seconds, StartTime, ArrivalTime, Easing);
        double a = (1 - e) * (1 - e);
        double b = 2 * (1 - e) * e;
        double c = e * e;
        double x = (a * Start.X) + (b * _controlX) + (c * Target.X);
        double y = (a * Start.Y) + (b * _controlY) + (c * Target.Y);
        return new FormationPose((float)x, (float)y, ArrivalMath.Lerp(Start.Rotation, Target.Rotation, e),
            ArrivalMath.Lerp(Start.ScaleX, Target.ScaleX, e), ArrivalMath.Lerp(Start.ScaleY, Target.ScaleY, e));
    }
}
