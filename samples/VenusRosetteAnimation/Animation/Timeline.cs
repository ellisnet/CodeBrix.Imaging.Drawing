using System;
using CodeBrix.Imaging;

namespace VenusRosetteAnimation.Animation;

/// <summary>
/// The animation's clock and the orbital model behind it. Everything shown is a pure function of the animation
/// time, so the live window and the rendered frames always agree.
/// <para>
/// TIMING (21.0 s, 630 frames at 30 fps): the scene fades in over the first <see cref="IntroSeconds" />; the eight
/// years are then drawn at a steady rate until <see cref="DrawEndSeconds" />, when the last thread lands; the
/// finished rosette holds for the remaining frames.
/// </para>
/// <para>
/// MODEL: Earth and Venus move on circular, coplanar orbits around the Sun (radii 1 and 0.7233 AU, periods
/// 365.256 and 224.701 days), both starting at the top of the picture (an inferior conjunction) and moving
/// counter-clockwise. Every <see cref="ThreadEveryDays" /> days a straight thread joins the two planets.
/// </para>
/// </summary>
internal static class Timeline
{
    /// <summary>The animation's length, seconds.</summary>
    public const double TotalSeconds = 21.0;

    /// <summary>Frames per second of the rendered video.</summary>
    public const int Fps = 30;

    /// <summary>The number of frames in the rendered video.</summary>
    public const int FrameCount = (int)(TotalSeconds * Fps);

    /// <summary>The fade-in from black, seconds (the clock of the eight years starts when it ends).</summary>
    public const double IntroSeconds = 1.0;

    /// <summary>The moment the last thread lands and the eight years are complete, seconds.</summary>
    public const double DrawEndSeconds = 20.5;

    /// <summary>Earth's orbital period, days.</summary>
    public const double EarthYearDays = 365.256;

    /// <summary>Venus' orbital period, days.</summary>
    public const double VenusYearDays = 224.701;

    /// <summary>Venus' orbital radius, in Earth orbital radii.</summary>
    public const double VenusOrbitRatio = 0.7233;

    /// <summary>Eight Earth years, days: almost exactly thirteen Venus years.</summary>
    public const double TotalDays = 8 * EarthYearDays;

    /// <summary>Days between threads.</summary>
    public const double ThreadEveryDays = 3;

    /// <summary>The days between inferior conjunctions (Venus passing between the Sun and Earth).</summary>
    public static readonly double SynodicDays = 1 / ((1 / VenusYearDays) - (1 / EarthYearDays));

    /// <summary>The number of threads in the finished rosette.</summary>
    public static readonly int TotalThreads = (int)Math.Floor(TotalDays / ThreadEveryDays) + 1;

    private const double StartAngle = Math.PI / 2;

    /// <summary>The simulated day shown at an animation time (0 during the intro, <see cref="TotalDays" /> after the draw).</summary>
    /// <param name="seconds">The animation time.</param>
    /// <returns>The day.</returns>
    public static double DayAt(double seconds)
    {
        double f = (seconds - IntroSeconds) / (DrawEndSeconds - IntroSeconds);
        return Math.Clamp(f, 0, 1) * TotalDays;
    }

    /// <summary>The number of threads drawn by a given day (the first one at day 0).</summary>
    /// <param name="day">The day.</param>
    /// <returns>The count.</returns>
    public static int ThreadsBy(double day) => Math.Min(TotalThreads, (int)Math.Floor((day / ThreadEveryDays) + 1e-9) + 1);

    /// <summary>
    /// The thread colour at a fraction of the eight years: rose, through copper, to violet.
    /// </summary>
    /// <param name="fraction">0 at the start, 1 at the end.</param>
    /// <param name="alpha">The colour's alpha.</param>
    /// <returns>The colour.</returns>
    public static Color ThreadColor(double fraction, byte alpha)
    {
        (float r, float g, float b)[] stops = { (246, 112, 168), (232, 146, 92), (156, 122, 248) };
        double f = Math.Clamp(fraction, 0, 1) * (stops.Length - 1);
        int i = Math.Min((int)f, stops.Length - 2);
        float t = (float)(f - i);
        (float r0, float g0, float b0) = stops[i];
        (float r1, float g1, float b1) = stops[i + 1];
        return Color.FromRgba(
            (byte)Math.Round(r0 + ((r1 - r0) * t)),
            (byte)Math.Round(g0 + ((g1 - g0) * t)),
            (byte)Math.Round(b0 + ((b1 - b0) * t)),
            alpha);
    }

    /// <summary>The smooth 0..1 ramp used for fades.</summary>
    /// <param name="x">The input, clamped to 0..1.</param>
    /// <returns>The eased value.</returns>
    public static double SmoothStep(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - (2 * x));
    }

    /// <summary>A planet's position on a given day, frame coordinates (counter-clockwise, starting at the top).</summary>
    /// <param name="sun">The Sun, frame coordinates.</param>
    /// <param name="radius">The orbit's radius, pixels.</param>
    /// <param name="periodDays">The orbital period, days.</param>
    /// <param name="day">The day.</param>
    /// <returns>The position.</returns>
    public static PointF OnOrbit(PointF sun, float radius, double periodDays, double day)
    {
        double angle = StartAngle + (2 * Math.PI * day / periodDays);
        return new PointF(sun.X + (radius * (float)Math.Cos(angle)), sun.Y - (radius * (float)Math.Sin(angle)));
    }
}
