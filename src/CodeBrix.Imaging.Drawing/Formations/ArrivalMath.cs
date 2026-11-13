using System;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing.Formations;

//Shared checks and the eased fraction of the built-in arrival styles
internal static class ArrivalMath
{
    public static void CheckTimes(double startTime, double arrivalTime)
    {
        if (!Double.IsFinite(startTime)) { throw new ArgumentOutOfRangeException(nameof(startTime)); }
        if (!Double.IsFinite(arrivalTime) || arrivalTime <= startTime) { throw new ArgumentOutOfRangeException(nameof(arrivalTime), "The arrival must be finite and after the start."); }
    }

    public static void CheckPose(FormationPose pose, string name)
    {
        if (!Single.IsFinite(pose.X) || !Single.IsFinite(pose.Y) || !Single.IsFinite(pose.Rotation)
            || !Single.IsFinite(pose.ScaleX) || !Single.IsFinite(pose.ScaleY))
        {
            throw new ArgumentOutOfRangeException(name, "Every value of the pose must be finite.");
        }
    }

    //Linear progress from the start to the arrival, clamped to 0..1, then eased
    public static double Eased(double seconds, double startTime, double arrivalTime, EasingKind easing)
    {
        double u = Math.Clamp((seconds - startTime) / (arrivalTime - startTime), 0.0, 1.0);
        return Easing.Evaluate(easing, u);
    }

    public static float Lerp(float from, float to, double fraction) => (float)(from + ((to - (double)from) * fraction));
}
