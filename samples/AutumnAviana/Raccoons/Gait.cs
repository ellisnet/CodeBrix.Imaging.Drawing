using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Planes;

namespace AutumnAviana.Raccoons;

/// <summary>
/// A gait's rhythm, driven by DISTANCE: how many stride cycles the body has gone through after travelling a
/// distance (its phase), and back. The stride may change with the distance (a run that lengthens its bounds as it
/// gathers speed), and a seeded, smooth wobble makes some strides a little longer and some a little shorter, so no
/// two steps are quite alike. Worked out once into a table; every lookup is a pure function of the distance.
/// </summary>
internal sealed class GaitClock
{
    private const float Step = 0.5f;
    private readonly float[] phases;
    private readonly float startRate;
    private readonly float endRate;

    /// <summary>Creates a gait clock.</summary>
    /// <param name="length">The distance it must cover (it carries on at the last stride beyond).</param>
    /// <param name="strideAt">The stride (the distance per cycle) at a distance.</param>
    /// <param name="variation">How much the strides vary, as a fraction (0 = every stride exactly as given).</param>
    /// <param name="seed">The seed of the variation.</param>
    public GaitClock(float length, Func<float, float> strideAt, float variation, long seed)
    {
        int n = Math.Max(2, (int)MathF.Ceiling(length / Step) + 1);
        phases = new float[n];
        double phase = 0;
        for (int i = 1; i < n; i++)
        {
            float middle = (i - 0.5f) * Step;
            float stride = strideAt(middle);
            double wobble = 1 + (variation * DrawingNoise.Value(middle / stride * 0.8, 0.37, seed));
            phase += Step / stride / wobble;
            phases[i] = (float)phase;
        }

        startRate = phases[1] / Step;
        endRate = (phases[n - 1] - phases[n - 2]) / Step;
    }

    /// <summary>The phase (cycles) after a distance (negative before the start, at the first stride).</summary>
    /// <param name="distance">The distance.</param>
    /// <returns>The phase.</returns>
    public float PhaseAt(float distance)
    {
        if (distance <= 0) return distance * startRate;
        float f = distance / Step;
        int i = (int)f;
        if (i >= phases.Length - 1) return phases[^1] + ((distance - ((phases.Length - 1) * Step)) * endRate);
        return phases[i] + ((phases[i + 1] - phases[i]) * (f - i));
    }

    /// <summary>The distance at which the phase is reached (the inverse of <see cref="PhaseAt" />).</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The distance.</returns>
    public float DistanceAt(float phase)
    {
        if (phase <= 0) return phase / startRate;
        if (phase >= phases[^1]) return ((phases.Length - 1) * Step) + ((phase - phases[^1]) / endRate);
        int lo = 0;
        int hi = phases.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (phases[mid] <= phase) lo = mid;
            else hi = mid;
        }

        return (lo + ((phase - phases[lo]) / (phases[hi] - phases[lo]))) * Step;
    }
}

/// <summary>
/// One foot's footfalls through a gait. A foot is on the ground ("stance") for the first <c>duty</c> of each of
/// its cycles and swings forward through the rest. Each footfall is PLANTED where it lands - fixed on the ground
/// until it lifts, however the body's speed or stride changes meanwhile - so a planted foot never slides; in the
/// swing it travels in an eased arc from where it lifted to where it lands next. Footfalls land a little more or
/// less far forward from step to step (seeded). A foot may also start standing on a given spot and lift from it at
/// a given phase (a gait that starts from a stand, such as a bound from standing).
/// </summary>
internal sealed class FootSteps
{
    private readonly GaitClock clock;
    private readonly float offset;
    private readonly float duty;
    private readonly float neutral;
    private readonly float reach;
    private readonly float lift;
    private readonly float jitter;
    private readonly long seed;
    private float? standX;
    private float standLift;

    /// <summary>Creates a foot's footfalls.</summary>
    /// <param name="clock">The gait's rhythm.</param>
    /// <param name="offset">This foot's phase offset: its stances start where the clock's phase plus this is whole.</param>
    /// <param name="duty">The fraction of each cycle on the ground.</param>
    /// <param name="neutral">The middle of its stance, relative to the body (raccoon units, + forward).</param>
    /// <param name="reach">How far ahead of <paramref name="neutral" /> it lands.</param>
    /// <param name="lift">How high it lifts mid-swing.</param>
    /// <param name="jitter">How much the landing spot varies, as a fraction of the reach.</param>
    /// <param name="seed">The seed of the variation.</param>
    public FootSteps(GaitClock clock, float offset, float duty, float neutral, float reach, float lift, float jitter, long seed)
    {
        this.clock = clock;
        this.offset = offset;
        this.duty = duty;
        this.neutral = neutral;
        this.reach = reach;
        this.lift = lift;
        this.jitter = jitter;
        this.seed = seed;
    }

    /// <summary>
    /// Starts the foot standing at a place (relative to the body at distance 0) until its own phase reaches
    /// <paramref name="liftPhase" />; from there it swings to its next regular footfall.
    /// </summary>
    /// <param name="x">Where it stands at the start, relative to the body.</param>
    /// <param name="liftPhase">The foot's phase (the clock's phase plus its offset) at which it lifts.</param>
    public void StandAt(float x, float liftPhase)
    {
        standX = x;
        standLift = liftPhase;
    }

    /// <summary>
    /// The foot's place relative to the body (x forward from the body's middle; y up as negative, 0 on the ground)
    /// after the body has travelled a distance, and how far through its swing it is (0 while it is planted).
    /// </summary>
    /// <param name="distance">The distance travelled.</param>
    /// <param name="swing">0 while planted; 0..1 through the swing.</param>
    /// <returns>Its place.</returns>
    public PointF At(float distance, out float swing)
    {
        float phase = clock.PhaseAt(distance) + offset;
        if (standX != null)
        {
            float stood = standX.Value;
            if (phase < standLift)
            {
                swing = 0;
                return new PointF(stood - distance, 0);
            }

            float next = MathF.Floor(standLift) + 1;
            if (phase < next)
            {
                float from = stood - clock.DistanceAt(standLift - offset);
                return Swinging(from, (int)next, (phase - standLift) / (next - standLift), out swing);
            }
        }

        int n = (int)MathF.Floor(phase);
        float p = phase - n;
        if (p < duty)
        {
            swing = 0;
            return new PointF(Planted(n) - distance, 0);
        }

        float lifted = Planted(n) - clock.DistanceAt(n + duty - offset);
        return Swinging(lifted, n + 1, (p - duty) / (1 - duty), out swing);
    }

    /// <summary>Where footfall <paramref name="n" /> lands, in distance along the ground.</summary>
    private float Planted(int n) => clock.DistanceAt(n - offset) + Landing(n);

    /// <summary>Where footfall <paramref name="n" /> lands relative to the body as it lands (the first ones exactly as set).</summary>
    private float Landing(int n) => neutral + (reach * (1 + Vary(n)));

    private float Vary(int n) => n <= 0 ? 0 : jitter * (float)((DrawingRandom.ForKey(seed, n).NextDouble() * 2) - 1);

    private PointF Swinging(float from, int landing, float u, out float swing)
    {
        swing = u;
        float to = Landing(landing);
        float eased = u * u * (3 - (2 * u));
        float height = lift * (1 + (0.5f * Vary(landing + 7919)));
        return new PointF(from + ((to - from) * eased), -height * MathF.Sin(MathF.PI * u));
    }
}

/// <summary>
/// A pace over a cue: how far (0..1 of the way) a body has gone at a time, from a speed profile scaled to arrive
/// exactly at the cue's end. The profile is integrated once into a table; lookups are pure functions of the time.
/// </summary>
internal sealed class Pace
{
    private const int Steps = 2400;
    private readonly double start;
    private readonly double duration;
    private readonly float[] travelled;

    /// <summary>Creates a pace.</summary>
    /// <param name="start">When it starts.</param>
    /// <param name="duration">How long it lasts.</param>
    /// <param name="speed">The relative speed at a time into it (any scale; never negative).</param>
    public Pace(double start, double duration, Func<double, double> speed)
    {
        this.start = start;
        this.duration = duration;
        travelled = new float[Steps + 1];
        double sum = 0;
        double[] raw = new double[Steps + 1];
        for (int i = 1; i <= Steps; i++)
        {
            sum += speed(duration * (i - 0.5) / Steps);
            raw[i] = sum;
        }

        for (int i = 0; i <= Steps; i++) travelled[i] = (float)(raw[i] / sum);
    }

    /// <summary>The fraction of the way at a time (0 before the start, 1 after the end).</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The fraction.</returns>
    public float At(double seconds)
    {
        double f = (seconds - start) / duration * Steps;
        if (f <= 0) return 0;
        if (f >= Steps) return 1;
        int i = (int)f;
        return travelled[i] + ((travelled[i + 1] - travelled[i]) * (float)(f - i));
    }
}

/// <summary>A two-bone limb solved for a target: the joint angles that put its end on the target.</summary>
internal static class LegIk
{
    /// <summary>
    /// Solves a two-bone limb from its root towards a target. Angles are screen directions in degrees (0 = right,
    /// 90 = down). Of the two ways the middle joint can bend, the one whose joint lies further in the
    /// <paramref name="bendTowards" /> direction is chosen (forward for a hind knee, back and down for an elbow).
    /// </summary>
    /// <param name="root">The limb's root (hip or shoulder).</param>
    /// <param name="target">Where its end should be (the target is pulled in when out of reach).</param>
    /// <param name="upper">The upper bone's length.</param>
    /// <param name="lower">The lower bone's length.</param>
    /// <param name="bendTowards">The direction the middle joint should point.</param>
    /// <param name="upperAngle">The upper bone's direction.</param>
    /// <param name="lowerAngle">The lower bone's direction.</param>
    public static void Solve(PointF root, PointF target, float upper, float lower, PointF bendTowards, out float upperAngle, out float lowerAngle)
    {
        float dx = target.X - root.X;
        float dy = target.Y - root.Y;
        float d = MathF.Sqrt((dx * dx) + (dy * dy));
        d = Math.Clamp(d, MathF.Abs(upper - lower) + 0.5f, upper + lower - 0.3f);
        float toTarget = MathF.Atan2(dy, dx);
        float cos = ((upper * upper) + (d * d) - (lower * lower)) / (2 * upper * d);
        float bend = MathF.Acos(Math.Clamp(cos, -1f, 1f));
        float a = toTarget + bend;
        float b = toTarget - bend;
        float scoreA = (MathF.Cos(a) * bendTowards.X) + (MathF.Sin(a) * bendTowards.Y);
        float scoreB = (MathF.Cos(b) * bendTowards.X) + (MathF.Sin(b) * bendTowards.Y);
        float a1 = scoreA >= scoreB ? a : b;
        PointF end = new PointF(root.X + (d * MathF.Cos(toTarget)), root.Y + (d * MathF.Sin(toTarget)));
        PointF joint = new PointF(root.X + (upper * MathF.Cos(a1)), root.Y + (upper * MathF.Sin(a1)));
        float a2 = MathF.Atan2(end.Y - joint.Y, end.X - joint.X);
        upperAngle = a1 * 180f / MathF.PI;
        lowerAngle = a2 * 180f / MathF.PI;
    }
}
