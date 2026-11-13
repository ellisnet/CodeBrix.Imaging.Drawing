using System;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// A spiral in: the object circles its target, the circle shrinking to nothing as it arrives -
/// it starts <see cref="StartRadius"/> away at <see cref="StartAngle"/> and goes round
/// <see cref="Turns"/> times (positive clockwise on screen), radius and angle eased along one curve
/// (<see cref="EasingKind.CubicOut"/> by default). Its own rotation turns with the spiral
/// (<see cref="SpinsWithPath"/>) or stays at the target's, and its scale goes from
/// <see cref="StartScale"/> to the target's.
/// </summary>
public sealed class SpiralArrival : IArrivalPath
{
    /// <summary>Creates the arrival.</summary>
    /// <param name="target">The pose it arrives in.</param>
    /// <param name="startTime">When it sets off.</param>
    /// <param name="arrivalTime">When it arrives; after <paramref name="startTime"/>.</param>
    /// <param name="startRadius">How far from the target it starts, calibrated units.</param>
    /// <param name="startAngle">Where on the circle it starts, degrees clockwise from +x.</param>
    /// <param name="turns">How many times it goes round (negative = counter-clockwise); 1.5 by default.</param>
    /// <param name="startScale">Its scale at the start (multiplying the target's scales); 1 by default.</param>
    /// <param name="spinsWithPath"><c>true</c> to turn with the spiral as it goes round.</param>
    /// <param name="easing">The curve of the whole way; <see cref="EasingKind.CubicOut"/> by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite, the radius is negative, or the arrival is not after the start.</exception>
    public SpiralArrival(FormationPose target, double startTime, double arrivalTime, float startRadius, float startAngle = 0f,
        float turns = 1.5f, float startScale = 1f, bool spinsWithPath = false, EasingKind easing = EasingKind.CubicOut)
    {
        ArrivalMath.CheckPose(target, nameof(target));
        ArrivalMath.CheckTimes(startTime, arrivalTime);
        if (!Single.IsFinite(startRadius) || startRadius < 0f) { throw new ArgumentOutOfRangeException(nameof(startRadius)); }
        if (!Single.IsFinite(startAngle)) { throw new ArgumentOutOfRangeException(nameof(startAngle)); }
        if (!Single.IsFinite(turns)) { throw new ArgumentOutOfRangeException(nameof(turns)); }
        if (!Single.IsFinite(startScale)) { throw new ArgumentOutOfRangeException(nameof(startScale)); }
        if (!Enum.IsDefined(easing)) { throw new ArgumentOutOfRangeException(nameof(easing)); }
        Target = target;
        StartTime = startTime;
        ArrivalTime = arrivalTime;
        StartRadius = startRadius;
        StartAngle = startAngle;
        Turns = turns;
        StartScale = startScale;
        SpinsWithPath = spinsWithPath;
        Easing = easing;
    }

    /// <inheritdoc />
    public FormationPose Target { get; }

    /// <inheritdoc />
    public double StartTime { get; }

    /// <inheritdoc />
    public double ArrivalTime { get; }

    /// <summary>How far from the target it starts, calibrated units.</summary>
    public float StartRadius { get; }

    /// <summary>Where on the circle it starts, degrees clockwise from +x.</summary>
    public float StartAngle { get; }

    /// <summary>How many times it goes round (negative = counter-clockwise).</summary>
    public float Turns { get; }

    /// <summary>Its scale at the start, multiplying the target's scales.</summary>
    public float StartScale { get; }

    /// <summary>Indicates whether it turns with the spiral as it goes round.</summary>
    public bool SpinsWithPath { get; }

    /// <summary>The curve of the whole way.</summary>
    public EasingKind Easing { get; }

    /// <inheritdoc />
    public FormationPose GetPoseAt(double seconds)
    {
        if (seconds >= ArrivalTime) { return Target; }
        double e = ArrivalMath.Eased(seconds, StartTime, ArrivalTime, Easing);
        double left = 1 - e;
        double angleDegrees = StartAngle + (Turns * 360.0 * e);
        double radians = angleDegrees * (Math.PI / 180.0);
        double radius = StartRadius * left;
        double x = Target.X + (radius * Math.Cos(radians));
        double y = Target.Y + (radius * Math.Sin(radians));
        double rotation = Target.Rotation - (SpinsWithPath ? Turns * 360.0 * left : 0.0);
        double scale = StartScale + ((1 - (double)StartScale) * e);
        return new FormationPose((float)x, (float)y, (float)rotation, (float)(Target.ScaleX * scale), (float)(Target.ScaleY * scale));
    }
}
