using System;
using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// The "swaying fall" arrival: an object drifts DOWN to its target the way a leaf, a feather or a
/// scrap of paper falls, worked backwards from where and when it arrives - so it ends exactly in
/// its target pose and every moment is a pure function of the time.
/// <para>
/// The model is a pendulum fall: the object rocks from side to side (<see cref="Sway"/> each way
/// over <see cref="SwingPeriod"/> seconds), sinking fastest through the bottom of each swing and
/// slowest at its ends (<see cref="Bob"/>), tilting with the swing (<see cref="Rock"/>); about one
/// in three tumbles end over end instead (<see cref="Spin"/>), and all of them flip slowly about
/// their long axis (the width scale swings through the back, never thinner than 0.16). An optional
/// air movement (the integral of a wind speed) carries each object by its own <see cref="Drift"/>.
/// It arrives at the END of a swing (where it is slowest), its sinking eases off over its last
/// half second, and just before it settles it flutters - a quick, small rocking that dies away.
/// </para>
/// <para>
/// Every characteristic is drawn from the object's own random stream in this order: sink speed
/// 185..250 units/s, sway 16..74, swing period 1.5..2.8 s, bob 0.3..0.62, rock 14..38 degrees
/// (either way), tumble (32%: 100..250 degrees/s either way), flip rate 0.7..2.3 rad/s and phase,
/// drift 0.6..1.3, flutter 6..15 degrees at 26..40 rad/s, then which end of the swing it lands at.
/// The start time is where the fall passes <c>startY</c> on the way back from the arrival (found by
/// bisection, from half a second to 40 s before it).
/// </para>
/// </summary>
public sealed class SwayingFallArrival : IArrivalPath
{
    private const double SettleTime = 1.1;
    private const double SoftLanding = 0.42;
    private readonly Func<double, double> _airOffset;

    /// <summary>Builds a fall that ends in a target pose.</summary>
    /// <param name="random">The object's own random stream (its swing, tumble, flip and drift are drawn from it).</param>
    /// <param name="target">The pose it comes to rest in.</param>
    /// <param name="arrivalTime">When it comes to rest.</param>
    /// <param name="startY">The height it starts from (normally just above the top edge), calibrated units.</param>
    /// <param name="airOffset">How far the air has moved sideways by a time (the integral of the wind speed), or <c>null</c> for still air.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite.</exception>
    public SwayingFallArrival(DrawingRandom random, FormationPose target, double arrivalTime, float startY, Func<double, double> airOffset = null)
    {
        if (random == null) { throw new ArgumentNullException(nameof(random)); }
        ArrivalMath.CheckPose(target, nameof(target));
        if (!Double.IsFinite(arrivalTime)) { throw new ArgumentOutOfRangeException(nameof(arrivalTime)); }
        if (!Single.IsFinite(startY)) { throw new ArgumentOutOfRangeException(nameof(startY)); }
        _airOffset = airOffset;
        Target = target;
        ArrivalTime = arrivalTime;
        SinkSpeed = random.Uniform(185, 250);
        Sway = random.Uniform(16, 74);
        SwingPeriod = random.Uniform(1.5, 2.8);
        Bob = random.Uniform(0.3, 0.62);
        Rock = random.Uniform(14, 38) * (random.NextDouble() < 0.5 ? -1 : 1);
        Spin = random.NextDouble() < 0.32 ? random.Uniform(100, 250) * (random.NextDouble() < 0.5 ? -1 : 1) : 0;
        FlipRate = random.Uniform(0.7, 2.3);
        FlipPhase = random.Uniform(0, Math.PI * 2);
        Drift = random.Uniform(0.6, 1.3);
        Flutter = random.Uniform(6, 15);
        FlutterRate = random.Uniform(26, 40);

        //Arrive at the end of a swing: the swing's phase there is +-90 degrees
        double omega = 2 * Math.PI / SwingPeriod;
        Phase = ((random.NextDouble() < 0.5 ? 0.5 : -0.5) * Math.PI) - (omega * arrivalTime);

        //How long it falls: from startY down to the target (the height only ever grows going back in time)
        double lo = 0.5;
        double hi = 40;
        for (int i = 0; i < 50; i++)
        {
            double mid = (lo + hi) / 2;
            if (GetPoseAt(arrivalTime - mid).Y > startY) { lo = mid; }
            else { hi = mid; }
        }

        StartTime = arrivalTime - hi;
    }

    /// <inheritdoc />
    public FormationPose Target { get; }

    /// <inheritdoc />
    public double ArrivalTime { get; }

    /// <inheritdoc />
    public double StartTime { get; }

    /// <summary>The steady sinking speed, units per second.</summary>
    public double SinkSpeed { get; }

    /// <summary>The swing's reach to each side, units.</summary>
    public double Sway { get; }

    /// <summary>The time of one full swing there and back, seconds.</summary>
    public double SwingPeriod { get; }

    /// <summary>How much the sinking speed rises and falls with the swing (0..1).</summary>
    public double Bob { get; }

    /// <summary>How far the object tilts at the bottom of a swing, degrees.</summary>
    public double Rock { get; }

    /// <summary>A tumbling object's turning speed, degrees per second (0 = it rocks instead).</summary>
    public double Spin { get; }

    /// <summary>How fast it flips about its long axis, radians per second.</summary>
    public double FlipRate { get; }

    /// <summary>The flip's starting phase, radians.</summary>
    public double FlipPhase { get; }

    /// <summary>How strongly the air carries it.</summary>
    public double Drift { get; }

    /// <summary>The flutter before settling: its size, degrees.</summary>
    public double Flutter { get; }

    /// <summary>The flutter's speed, radians per second.</summary>
    public double FlutterRate { get; }

    /// <summary>The swing's phase offset, radians.</summary>
    public double Phase { get; }

    /// <inheritdoc />
    public FormationPose GetPoseAt(double seconds)
    {
        if (seconds >= ArrivalTime) { return Target; }
        double toGo = Math.Max(0, ArrivalTime - seconds);      //seconds until it rests
        double ease = Smooth(toGo / SettleTime);              //0 at rest .. 1 while falling freely
        double omega = 2 * Math.PI / SwingPeriod;
        double swing = (omega * seconds) + Phase;
        double swingAtRest = (omega * ArrivalTime) + Phase;

        double air = _airOffset == null ? 0 : _airOffset(seconds) - _airOffset(ArrivalTime);
        double x = Target.X + (Sway * (Math.Sin(swing) - Math.Sin(swingAtRest))) + (Drift * air * ease);

        double sink = toGo - (SoftLanding * (1 - Math.Exp(-toGo / SoftLanding)));
        double bob = SinkSpeed * Bob * (Math.Sin(2 * swing) - Math.Sin(2 * swingAtRest)) / (2 * omega);
        double y = Target.Y - (SinkSpeed * sink) + (bob * ease);

        double flutterWindow = toGo / 0.55 * Math.Exp(1 - (toGo / 0.55));
        double angle = Target.Rotation
            + (Rock * Math.Cos(swing) * ease)
            + (Spin * (seconds - ArrivalTime) * ease)
            + (Flutter * Math.Sin(FlutterRate * toGo) * flutterWindow);

        double flip = Math.Cos((FlipRate * seconds) + FlipPhase);
        flip = Math.Sign(flip) * Math.Max(0.16, Math.Abs(flip));
        double scaleX = Target.ScaleX + ((flip - Target.ScaleX) * Smooth(toGo / 1.4));
        double scaleY = Target.ScaleY + ((0.92 + (0.08 * Math.Sin(swing)) - Target.ScaleY) * ease);
        return new FormationPose((float)x, (float)y, (float)angle, (float)scaleX, (float)scaleY);
    }

    private static double Smooth(double u)
    {
        u = Math.Clamp(u, 0, 1);
        return u * u * (3 - (2 * u));
    }
}
