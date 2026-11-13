using System;

namespace AutumnAviana.Leaves;

/// <summary>
/// The air the leaves fall through: a faint steady drift to the right plus a few breaths of wind, each a smooth
/// rise and fall of sideways speed lasting a couple of seconds. <see cref="Offset" /> is the integral of the
/// speed - how far the air has carried something since the start - tabulated once, so a leaf's drift is a pure
/// function of the time.
/// </summary>
internal static class Wind
{
    private const double Step = 1.0 / 240;
    private const double Start = -20;
    private const double End = 70;

    /// <summary>The breaths: centre time (s), sideways speed at the peak (px/s, + = right), half-length (s).</summary>
    private static readonly (double Center, double Speed, double HalfWidth)[] Breaths =
    {
        (7.5, 46, 1.6),
        (13.8, -34, 1.4),
        (19.6, 58, 1.9),
        (25.2, -28, 1.3),
    };

    private static readonly double[] Table = Integrate();

    /// <summary>The air's sideways speed at a time, px/s.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The speed.</returns>
    public static double Speed(double seconds)
    {
        double speed = 7;
        foreach ((double center, double peak, double half) in Breaths)
        {
            double u = (seconds - center) / half;
            if (u > -1 && u < 1)
            {
                double c = Math.Cos(u * Math.PI / 2);
                speed += peak * c * c;
            }
        }

        return speed;
    }

    /// <summary>How far the air has moved sideways by a time (px), from a fixed origin.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The offset.</returns>
    public static double Offset(double seconds)
    {
        double f = (Math.Clamp(seconds, Start, End - Step) - Start) / Step;
        int i = (int)Math.Floor(f);
        double t = f - i;
        return Table[i] + ((Table[i + 1] - Table[i]) * t);
    }

    private static double[] Integrate()
    {
        int n = (int)Math.Ceiling((End - Start) / Step) + 2;
        double[] table = new double[n];
        for (int i = 1; i < n; i++)
        {
            double a = Start + ((i - 1) * Step);
            table[i] = table[i - 1] + ((Speed(a) + Speed(a + Step)) * 0.5 * Step);
        }

        return table;
    }
}
