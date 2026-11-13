using System;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// The CSS <c>cubic-bezier(x1, y1, x2, y2)</c> timing function: a cubic Bezier curve from
/// (0, 0) to (1, 1) with two control points, read as "eased value (y) at progress (x)". The
/// control points' x values must lie in 0..1 (so the curve is a function of x); their y
/// values may lie outside 0..1 to overshoot. Immutable; create one and call
/// <see cref="Evaluate(double)"/> as often as needed (its method group converts to
/// <c>Func&lt;double, double&gt;</c>).
/// </summary>
public sealed class CubicBezierEasing
{
    private const int NewtonIterations = 8;
    private const double Epsilon = 1e-7;

    /// <summary>The CSS <c>ease</c> keyword, <c>cubic-bezier(0.25, 0.1, 0.25, 1)</c>.</summary>
    public static readonly CubicBezierEasing Ease = new CubicBezierEasing(0.25, 0.1, 0.25, 1);

    /// <summary>The CSS <c>ease-in</c> keyword, <c>cubic-bezier(0.42, 0, 1, 1)</c>.</summary>
    public static readonly CubicBezierEasing EaseIn = new CubicBezierEasing(0.42, 0, 1, 1);

    /// <summary>The CSS <c>ease-out</c> keyword, <c>cubic-bezier(0, 0, 0.58, 1)</c>.</summary>
    public static readonly CubicBezierEasing EaseOut = new CubicBezierEasing(0, 0, 0.58, 1);

    /// <summary>The CSS <c>ease-in-out</c> keyword, <c>cubic-bezier(0.42, 0, 0.58, 1)</c>.</summary>
    public static readonly CubicBezierEasing EaseInOut = new CubicBezierEasing(0.42, 0, 0.58, 1);

    /// <summary>The first control point's x, 0..1.</summary>
    public double X1 { get; }

    /// <summary>The first control point's y.</summary>
    public double Y1 { get; }

    /// <summary>The second control point's x, 0..1.</summary>
    public double X2 { get; }

    /// <summary>The second control point's y.</summary>
    public double Y2 { get; }

    /// <summary>
    /// Creates the curve <c>cubic-bezier(x1, y1, x2, y2)</c>.
    /// </summary>
    /// <param name="x1">The first control point's x, 0..1.</param>
    /// <param name="y1">The first control point's y.</param>
    /// <param name="x2">The second control point's x, 0..1.</param>
    /// <param name="y2">The second control point's y.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an x is outside 0..1 or any value is not finite.</exception>
    public CubicBezierEasing(double x1, double y1, double x2, double y2)
    {
        if (!Double.IsFinite(x1) || x1 < 0 || x1 > 1) { throw new ArgumentOutOfRangeException(nameof(x1)); }
        if (!Double.IsFinite(x2) || x2 < 0 || x2 > 1) { throw new ArgumentOutOfRangeException(nameof(x2)); }
        if (!Double.IsFinite(y1)) { throw new ArgumentOutOfRangeException(nameof(y1)); }
        if (!Double.IsFinite(y2)) { throw new ArgumentOutOfRangeException(nameof(y2)); }
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    /// <summary>
    /// Evaluates the curve: finds the curve parameter whose x equals <paramref name="t"/>
    /// (Newton's method, falling back to bisection) and returns the y there.
    /// </summary>
    /// <param name="t">The progress, 0..1 (clamped; NaN is treated as 0).</param>
    /// <returns>The eased value; exactly 0 at t = 0 and exactly 1 at t = 1.</returns>
    public double Evaluate(double t)
    {
        t = Easing.Clamp01(t);
        if (t == 0 || t == 1) { return t; }
        return SampleY(SolveForX(t));
    }

    /// <inheritdoc />
    public override string ToString() => FormattableString.Invariant($"cubic-bezier({X1}, {Y1}, {X2}, {Y2})");

    //B(s) = 3(1-s)^2 s P1 + 3(1-s) s^2 P2 + s^3, written in polynomial form
    private static double Sample(double s, double p1, double p2)
    {
        double c = 3 * p1;
        double b = (3 * (p2 - p1)) - c;
        double a = 1 - c - b;
        return ((((a * s) + b) * s) + c) * s;
    }

    private static double SampleDerivative(double s, double p1, double p2)
    {
        double c = 3 * p1;
        double b = (3 * (p2 - p1)) - c;
        double a = 1 - c - b;
        return (((3 * a * s) + (2 * b)) * s) + c;
    }

    private double SampleY(double s) => Sample(s, Y1, Y2);

    private double SolveForX(double x)
    {
        double s = x;
        for (int i = 0; i < NewtonIterations; i++)
        {
            double error = Sample(s, X1, X2) - x;
            if (Math.Abs(error) < Epsilon) { return s; }
            double slope = SampleDerivative(s, X1, X2);
            if (Math.Abs(slope) < 1e-6) { break; }
            s -= error / slope;
        }

        //Bisection: x(s) is monotonic on 0..1 because both control x values lie in 0..1
        double low = 0;
        double high = 1;
        s = x;
        while (high - low > Epsilon)
        {
            double value = Sample(s, X1, X2);
            if (Math.Abs(value - x) < Epsilon) { return s; }
            if (value < x) { low = s; } else { high = s; }
            s = (low + high) / 2;
        }
        return s;
    }
}
