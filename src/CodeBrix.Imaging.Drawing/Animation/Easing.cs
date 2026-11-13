using System;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// The conventional easing curves for animation: each maps a progress value
/// <c>t</c> in 0..1 to an eased value that starts at 0 (t = 0) and ends at 1 (t = 1).
/// Inputs outside 0..1 are clamped and NaN is treated as 0, so a caller can pass a raw
/// <c>(now - start) / duration</c> without guarding it. Outputs stay in 0..1 except for the
/// Back curves (which overshoot by design) and the Elastic curves (which oscillate about
/// their end). The formulas are the widely used set popularised by Robert Penner's easing
/// equations and the CSS easing functions; they are pure functions, so the same input gives
/// the same output in both packages. Combine with <see cref="Lerp(double, double, double)"/>
/// to ease any value: <c>Easing.Lerp(from, to, Easing.CubicOut(t))</c>.
/// </summary>
public static class Easing
{
    /// <summary>The default overshoot of the Back curves (about a 10% overshoot).</summary>
    public const double DefaultBackOvershoot = 1.70158;

    private const double ElasticPeriodIn = (2 * Math.PI) / 3;
    private const double ElasticPeriodInOut = (2 * Math.PI) / 4.5;

    /// <summary>Clamps a progress value to 0..1, treating NaN as 0.</summary>
    /// <param name="t">The progress value.</param>
    /// <returns>The clamped value.</returns>
    public static double Clamp01(double t) => Double.IsNaN(t) ? 0 : Math.Clamp(t, 0, 1);

    /// <summary>Interpolates linearly from <paramref name="from"/> to <paramref name="to"/> (not clamped).</summary>
    /// <param name="from">The value at t = 0.</param>
    /// <param name="to">The value at t = 1.</param>
    /// <param name="t">The (usually eased) progress; values outside 0..1 extrapolate.</param>
    /// <returns><c>from + (to - from) * t</c>.</returns>
    public static double Lerp(double from, double to, double t) => from + ((to - from) * t);

    /// <summary>Interpolates linearly between two single-precision values (not clamped).</summary>
    /// <param name="from">The value at t = 0.</param>
    /// <param name="to">The value at t = 1.</param>
    /// <param name="t">The (usually eased) progress; values outside 0..1 extrapolate.</param>
    /// <returns><c>from + (to - from) * t</c>.</returns>
    public static float Lerp(float from, float to, double t) => (float)(from + ((to - from) * t));

    /// <summary>No easing: returns the clamped progress unchanged.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double Linear(double t) => Clamp01(t);

    /// <summary>The Hermite smooth step, <c>t² (3 - 2t)</c>: gentle at both ends, flat slope at 0 and 1.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double SmoothStep(double t)
    {
        t = Clamp01(t);
        return t * t * (3 - (2 * t));
    }

    /// <summary>
    /// The smooth step between two edges (the GLSL <c>smoothstep</c>): 0 at or below
    /// <paramref name="edge0"/>, 1 at or above <paramref name="edge1"/>, and
    /// <see cref="SmoothStep(double)"/> of the position between them.
    /// </summary>
    /// <param name="edge0">The input at which the result is 0.</param>
    /// <param name="edge1">The input at which the result is 1; must differ from <paramref name="edge0"/>.</param>
    /// <param name="x">The input.</param>
    /// <returns>The eased value, 0..1.</returns>
    /// <exception cref="ArgumentException">Thrown when the two edges are equal.</exception>
    public static double SmoothStep(double edge0, double edge1, double x)
    {
        if (edge0 == edge1) { throw new ArgumentException("The two edges must differ.", nameof(edge1)); }
        return SmoothStep((x - edge0) / (edge1 - edge0));
    }

    /// <summary>Ken Perlin's smoother step, <c>t³ (t (6t - 15) + 10)</c>: flat first and second derivatives at both ends.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double SmootherStep(double t)
    {
        t = Clamp01(t);
        return t * t * t * ((t * ((6 * t) - 15)) + 10);
    }

    /// <summary>Quadratic ease-in, <c>t²</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuadIn(double t) => PowerIn(t, 2);

    /// <summary>Quadratic ease-out, <c>1 - (1 - t)²</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuadOut(double t) => PowerOut(t, 2);

    /// <summary>Quadratic ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuadInOut(double t) => PowerInOut(t, 2);

    /// <summary>Cubic ease-in, <c>t³</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double CubicIn(double t) => PowerIn(t, 3);

    /// <summary>Cubic ease-out, <c>1 - (1 - t)³</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double CubicOut(double t) => PowerOut(t, 3);

    /// <summary>Cubic ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double CubicInOut(double t) => PowerInOut(t, 3);

    /// <summary>Quartic ease-in, <c>t⁴</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuartIn(double t) => PowerIn(t, 4);

    /// <summary>Quartic ease-out, <c>1 - (1 - t)⁴</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuartOut(double t) => PowerOut(t, 4);

    /// <summary>Quartic ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuartInOut(double t) => PowerInOut(t, 4);

    /// <summary>Quintic ease-in, <c>t⁵</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuintIn(double t) => PowerIn(t, 5);

    /// <summary>Quintic ease-out, <c>1 - (1 - t)⁵</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuintOut(double t) => PowerOut(t, 5);

    /// <summary>Quintic ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double QuintInOut(double t) => PowerInOut(t, 5);

    /// <summary>Sinusoidal ease-in, <c>1 - cos(t π / 2)</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double SineIn(double t) => 1 - Math.Cos(Clamp01(t) * Math.PI / 2);

    /// <summary>Sinusoidal ease-out, <c>sin(t π / 2)</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double SineOut(double t) => Math.Sin(Clamp01(t) * Math.PI / 2);

    /// <summary>Sinusoidal ease-in-out, <c>(1 - cos(t π)) / 2</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double SineInOut(double t) => (1 - Math.Cos(Clamp01(t) * Math.PI)) / 2;

    /// <summary>Exponential ease-in, <c>2^(10t - 10)</c> (exactly 0 at t = 0).</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double ExpoIn(double t)
    {
        t = Clamp01(t);
        return t == 0 ? 0 : Math.Pow(2, (10 * t) - 10);
    }

    /// <summary>Exponential ease-out, <c>1 - 2^(-10t)</c> (exactly 1 at t = 1).</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double ExpoOut(double t)
    {
        t = Clamp01(t);
        return t == 1 ? 1 : 1 - Math.Pow(2, -10 * t);
    }

    /// <summary>Exponential ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double ExpoInOut(double t)
    {
        t = Clamp01(t);
        if (t == 0 || t == 1) { return t; }
        return t < 0.5
            ? Math.Pow(2, (20 * t) - 10) / 2
            : (2 - Math.Pow(2, (-20 * t) + 10)) / 2;
    }

    /// <summary>Circular ease-in, <c>1 - √(1 - t²)</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double CircIn(double t)
    {
        t = Clamp01(t);
        return 1 - Math.Sqrt(1 - (t * t));
    }

    /// <summary>Circular ease-out, <c>√(1 - (t - 1)²)</c>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double CircOut(double t)
    {
        t = Clamp01(t) - 1;
        return Math.Sqrt(1 - (t * t));
    }

    /// <summary>Circular ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double CircInOut(double t)
    {
        t = Clamp01(t);
        if (t < 0.5)
        {
            double u = 2 * t;
            return (1 - Math.Sqrt(1 - (u * u))) / 2;
        }
        double v = (-2 * t) + 2;
        return (Math.Sqrt(1 - (v * v)) + 1) / 2;
    }

    /// <summary>Back ease-in with the default overshoot: dips below 0 before rising to 1.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double BackIn(double t) => BackIn(t, DefaultBackOvershoot);

    /// <summary>Back ease-in, <c>(s + 1) t³ - s t²</c>: dips below 0 before rising to 1.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <param name="overshoot">How far it dips; <see cref="DefaultBackOvershoot"/> is about 10%, 0 gives a plain cubic.</param>
    /// <returns>The eased value.</returns>
    public static double BackIn(double t, double overshoot)
    {
        t = Clamp01(t);
        return ((overshoot + 1) * t * t * t) - (overshoot * t * t);
    }

    /// <summary>Back ease-out with the default overshoot: passes 1 before settling back to it.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double BackOut(double t) => BackOut(t, DefaultBackOvershoot);

    /// <summary>Back ease-out, <c>1 + (s + 1)(t - 1)³ + s (t - 1)²</c>: passes 1 before settling back to it.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <param name="overshoot">How far it overshoots; <see cref="DefaultBackOvershoot"/> is about 10%.</param>
    /// <returns>The eased value.</returns>
    public static double BackOut(double t, double overshoot)
    {
        double u = Clamp01(t) - 1;
        return 1 + ((overshoot + 1) * u * u * u) + (overshoot * u * u);
    }

    /// <summary>Back ease-in-out with the default overshoot.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double BackInOut(double t) => BackInOut(t, DefaultBackOvershoot);

    /// <summary>
    /// Back ease-in for the first half and back ease-out for the second; the overshoot is
    /// scaled by 1.525 as in the conventional formula so each half overshoots by about the
    /// same proportion as the one-sided curves.
    /// </summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <param name="overshoot">The overshoot before scaling; <see cref="DefaultBackOvershoot"/> by default.</param>
    /// <returns>The eased value.</returns>
    public static double BackInOut(double t, double overshoot)
    {
        t = Clamp01(t);
        double s = overshoot * 1.525;
        if (t < 0.5)
        {
            double u = 2 * t;
            return (u * u * (((s + 1) * u) - s)) / 2;
        }
        double v = (2 * t) - 2;
        return ((v * v * (((s + 1) * v) + s)) + 2) / 2;
    }

    /// <summary>Elastic ease-in: a spring oscillation that grows into the start of the motion.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value (dips slightly below 0 on the way).</returns>
    public static double ElasticIn(double t)
    {
        t = Clamp01(t);
        if (t == 0 || t == 1) { return t; }
        return -Math.Pow(2, (10 * t) - 10) * Math.Sin(((10 * t) - 10.75) * ElasticPeriodIn);
    }

    /// <summary>Elastic ease-out: overshoots 1 and rings down to it like a released spring.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value (rises slightly above 1 on the way).</returns>
    public static double ElasticOut(double t)
    {
        t = Clamp01(t);
        if (t == 0 || t == 1) { return t; }
        return (Math.Pow(2, -10 * t) * Math.Sin(((10 * t) - 0.75) * ElasticPeriodIn)) + 1;
    }

    /// <summary>Elastic ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double ElasticInOut(double t)
    {
        t = Clamp01(t);
        if (t == 0 || t == 1) { return t; }
        double wave = Math.Sin(((20 * t) - 11.125) * ElasticPeriodInOut);
        return t < 0.5
            ? -(Math.Pow(2, (20 * t) - 10) * wave) / 2
            : ((Math.Pow(2, (-20 * t) + 10) * wave) / 2) + 1;
    }

    /// <summary>Bounce ease-in: the mirror image of <see cref="BounceOut(double)"/>.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double BounceIn(double t) => 1 - BounceOut(1 - Clamp01(t));

    /// <summary>Bounce ease-out: falls onto 1 and bounces to rest with three shrinking bounces.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double BounceOut(double t)
    {
        const double n = 7.5625;
        const double d = 2.75;
        t = Clamp01(t);
        if (t < 1 / d) { return n * t * t; }
        if (t < 2 / d)
        {
            t -= 1.5 / d;
            return (n * t * t) + 0.75;
        }
        if (t < 2.5 / d)
        {
            t -= 2.25 / d;
            return (n * t * t) + 0.9375;
        }
        t -= 2.625 / d;
        return (n * t * t) + 0.984375;
    }

    /// <summary>Bounce ease-in for the first half, ease-out for the second.</summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double BounceInOut(double t)
    {
        t = Clamp01(t);
        return t < 0.5
            ? (1 - BounceOut(1 - (2 * t))) / 2
            : (1 + BounceOut((2 * t) - 1)) / 2;
    }

    /// <summary>
    /// A staircase of equal steps - the CSS <c>steps(count, position)</c> timing function:
    /// the output moves in <paramref name="count"/> jumps instead of continuously (a ticking
    /// clock hand, a frame-by-frame sprite).
    /// </summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <param name="count">The number of intervals; at least 1 (at least 2 for <see cref="StepPosition.None"/>).</param>
    /// <param name="position">Where in each interval the jump happens; <see cref="StepPosition.End"/> by default.</param>
    /// <returns>The stepped value, one of a fixed set of levels from 0 to 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is too small for <paramref name="position"/>, or
    /// <paramref name="position"/> is not a defined value.
    /// </exception>
    public static double Steps(double t, int count, StepPosition position = StepPosition.End)
    {
        int jumps = position switch
        {
            StepPosition.End => count,
            StepPosition.Start => count,
            StepPosition.None => count - 1,
            StepPosition.Both => count + 1,
            _ => throw new ArgumentOutOfRangeException(nameof(position)),
        };
        if (count < 1 || jumps < 1) { throw new ArgumentOutOfRangeException(nameof(count)); }

        t = Clamp01(t);
        int step = (int)Math.Floor(t * count);
        if (position == StepPosition.Start || position == StepPosition.Both) { step++; }
        step = Math.Clamp(step, 0, jumps);
        return step / (double)jumps;
    }

    /// <summary>
    /// Evaluates the CSS <c>cubic-bezier(x1, y1, x2, y2)</c> timing function at
    /// <paramref name="t"/>. For a curve used many times, create a
    /// <see cref="CubicBezierEasing"/> once instead.
    /// </summary>
    /// <param name="t">The progress, 0..1.</param>
    /// <param name="x1">The first control point's x, 0..1.</param>
    /// <param name="y1">The first control point's y (may lie outside 0..1 for overshoot).</param>
    /// <param name="x2">The second control point's x, 0..1.</param>
    /// <param name="y2">The second control point's y (may lie outside 0..1 for overshoot).</param>
    /// <returns>The eased value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an x is outside 0..1 or any value is not finite.</exception>
    public static double CubicBezier(double t, double x1, double y1, double x2, double y2)
        => new CubicBezierEasing(x1, y1, x2, y2).Evaluate(t);

    /// <summary>Evaluates a named easing curve.</summary>
    /// <param name="kind">The curve.</param>
    /// <param name="t">The progress, 0..1.</param>
    /// <returns>The eased value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind"/> is not a defined value.</exception>
    public static double Evaluate(EasingKind kind, double t) => GetFunction(kind)(t);

    /// <summary>
    /// Returns a named easing curve as a function - handy for storing the curve an animated
    /// property uses, or for a list of curves to choose from.
    /// </summary>
    /// <param name="kind">The curve.</param>
    /// <returns>The curve's function (the same instance on every call for a given kind).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind"/> is not a defined value.</exception>
    public static Func<double, double> GetFunction(EasingKind kind) => kind switch
    {
        EasingKind.Linear => LinearFunction,
        EasingKind.SmoothStep => SmoothStepFunction,
        EasingKind.SmootherStep => SmootherStepFunction,
        EasingKind.QuadIn => QuadInFunction,
        EasingKind.QuadOut => QuadOutFunction,
        EasingKind.QuadInOut => QuadInOutFunction,
        EasingKind.CubicIn => CubicInFunction,
        EasingKind.CubicOut => CubicOutFunction,
        EasingKind.CubicInOut => CubicInOutFunction,
        EasingKind.QuartIn => QuartInFunction,
        EasingKind.QuartOut => QuartOutFunction,
        EasingKind.QuartInOut => QuartInOutFunction,
        EasingKind.QuintIn => QuintInFunction,
        EasingKind.QuintOut => QuintOutFunction,
        EasingKind.QuintInOut => QuintInOutFunction,
        EasingKind.SineIn => SineInFunction,
        EasingKind.SineOut => SineOutFunction,
        EasingKind.SineInOut => SineInOutFunction,
        EasingKind.ExpoIn => ExpoInFunction,
        EasingKind.ExpoOut => ExpoOutFunction,
        EasingKind.ExpoInOut => ExpoInOutFunction,
        EasingKind.CircIn => CircInFunction,
        EasingKind.CircOut => CircOutFunction,
        EasingKind.CircInOut => CircInOutFunction,
        EasingKind.BackIn => BackInFunction,
        EasingKind.BackOut => BackOutFunction,
        EasingKind.BackInOut => BackInOutFunction,
        EasingKind.ElasticIn => ElasticInFunction,
        EasingKind.ElasticOut => ElasticOutFunction,
        EasingKind.ElasticInOut => ElasticInOutFunction,
        EasingKind.BounceIn => BounceInFunction,
        EasingKind.BounceOut => BounceOutFunction,
        EasingKind.BounceInOut => BounceInOutFunction,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static double PowerIn(double t, int power) => Math.Pow(Clamp01(t), power);

    private static double PowerOut(double t, int power) => 1 - Math.Pow(1 - Clamp01(t), power);

    private static double PowerInOut(double t, int power)
    {
        t = Clamp01(t);
        return t < 0.5
            ? Math.Pow(2, power - 1) * Math.Pow(t, power)
            : 1 - (Math.Pow((-2 * t) + 2, power) / 2);
    }

    //One cached delegate per curve, so GetFunction never allocates
    private static readonly Func<double, double> LinearFunction = Linear;
    private static readonly Func<double, double> SmoothStepFunction = SmoothStep;
    private static readonly Func<double, double> SmootherStepFunction = SmootherStep;
    private static readonly Func<double, double> QuadInFunction = QuadIn;
    private static readonly Func<double, double> QuadOutFunction = QuadOut;
    private static readonly Func<double, double> QuadInOutFunction = QuadInOut;
    private static readonly Func<double, double> CubicInFunction = CubicIn;
    private static readonly Func<double, double> CubicOutFunction = CubicOut;
    private static readonly Func<double, double> CubicInOutFunction = CubicInOut;
    private static readonly Func<double, double> QuartInFunction = QuartIn;
    private static readonly Func<double, double> QuartOutFunction = QuartOut;
    private static readonly Func<double, double> QuartInOutFunction = QuartInOut;
    private static readonly Func<double, double> QuintInFunction = QuintIn;
    private static readonly Func<double, double> QuintOutFunction = QuintOut;
    private static readonly Func<double, double> QuintInOutFunction = QuintInOut;
    private static readonly Func<double, double> SineInFunction = SineIn;
    private static readonly Func<double, double> SineOutFunction = SineOut;
    private static readonly Func<double, double> SineInOutFunction = SineInOut;
    private static readonly Func<double, double> ExpoInFunction = ExpoIn;
    private static readonly Func<double, double> ExpoOutFunction = ExpoOut;
    private static readonly Func<double, double> ExpoInOutFunction = ExpoInOut;
    private static readonly Func<double, double> CircInFunction = CircIn;
    private static readonly Func<double, double> CircOutFunction = CircOut;
    private static readonly Func<double, double> CircInOutFunction = CircInOut;
    private static readonly Func<double, double> BackInFunction = BackIn;
    private static readonly Func<double, double> BackOutFunction = BackOut;
    private static readonly Func<double, double> BackInOutFunction = BackInOut;
    private static readonly Func<double, double> ElasticInFunction = ElasticIn;
    private static readonly Func<double, double> ElasticOutFunction = ElasticOut;
    private static readonly Func<double, double> ElasticInOutFunction = ElasticInOut;
    private static readonly Func<double, double> BounceInFunction = BounceIn;
    private static readonly Func<double, double> BounceOutFunction = BounceOut;
    private static readonly Func<double, double> BounceInOutFunction = BounceInOut;
}
