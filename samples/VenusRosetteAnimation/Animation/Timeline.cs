using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Animation;

namespace VenusRosetteAnimation.Animation;

/// <summary>
/// The animation's clock and the orbital model behind it. Everything shown is a pure function of the animation
/// time, so the live window and the rendered frames always agree.
/// <para>
/// TIMING (21.0 s, 630 frames at 30 fps) lives in <see cref="Clock" />, the library timeline, as three cues: "intro"
/// (the fade-in over the first <see cref="IntroSeconds" />), "draw" (the eight years, drawn at a steady rate until
/// <see cref="DrawEndSeconds" />, when the last thread lands) and "settle" (the thread being drawn fades away); the
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

    /// <summary>The fade of the thread being drawn, once the last thread has landed, seconds.</summary>
    public const double SettleSeconds = 0.4;

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

    /// <summary>
    /// The animation's clock: 21 s at 30 fps, with the cues "intro" (eased, 0 .. <see cref="IntroSeconds" />),
    /// "draw" (linear, <see cref="IntroSeconds" /> .. <see cref="DrawEndSeconds" />) and "settle" (eased, the
    /// <see cref="SettleSeconds" /> after <see cref="DrawEndSeconds" />).
    /// </summary>
    public static readonly AnimationTimeline Clock = CreateClock();

    /// <summary>The fade-in cue.</summary>
    public static readonly TimelineCue Intro = Clock.GetCue("intro");

    /// <summary>The eight years being drawn.</summary>
    public static readonly TimelineCue Draw = Clock.GetCue("draw");

    /// <summary>The fade of the thread being drawn, after the last one lands.</summary>
    public static readonly TimelineCue Settle = Clock.GetCue("settle");

    /// <summary>The number of frames in the rendered video.</summary>
    public static int FrameCount => Clock.FrameCount;

    /// <summary>The simulated day shown at an animation time (0 during the intro, <see cref="TotalDays" /> after the draw).</summary>
    /// <param name="seconds">The animation time.</param>
    /// <returns>The day.</returns>
    public static double DayAt(double seconds) => Draw.LinearProgress(seconds) * TotalDays;

    /// <summary>
    /// The first animation time at which <see cref="DayAt" /> reaches a day (to within the thread schedule's
    /// 1e-9-day tolerance) - the moment a thread or ring of that day appears. Found exactly, double by double, so an
    /// element keyed to it appears on precisely the frames where the day has come.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The time; <c>null</c> when the day has come from the start, <see cref="double.PositiveInfinity" /> when it never comes.</returns>
    public static double? FirstTimeAt(double day)
    {
        bool Reached(double seconds) => DayAt(seconds) >= day - 1e-9;
        if (Reached(0)) return null;
        if (!Reached(TotalSeconds)) return double.PositiveInfinity;

        // DayAt only ever grows with time, so step from an estimate to the exact first time that reaches the day.
        double seconds = Math.Clamp(IntroSeconds + (day / TotalDays * (DrawEndSeconds - IntroSeconds)), 0, TotalSeconds);
        while (Reached(seconds)) seconds = Math.BitDecrement(seconds);
        while (!Reached(seconds)) seconds = Math.BitIncrement(seconds);
        return seconds;
    }

    private static AnimationTimeline CreateClock()
    {
        AnimationTimeline clock = new AnimationTimeline(TotalSeconds, Fps);
        clock.AddCue("intro", 0, IntroSeconds, EasingKind.SmoothStep);
        clock.AddCue("draw", IntroSeconds, DrawEndSeconds);
        clock.AddCue("settle", DrawEndSeconds, DrawEndSeconds + SettleSeconds, EasingKind.SmoothStep);
        return clock;
    }

    /// <summary>The number of threads drawn by a given day (the first one at day 0).</summary>
    /// <param name="day">The day.</param>
    /// <returns>The count.</returns>
    public static int ThreadsBy(double day) => Math.Min(TotalThreads, (int)Math.Floor((day / ThreadEveryDays) + 1e-9) + 1);

    /// <summary>
    /// The thread colour across the eight years: rose, through copper, to violet (position 0 at the start, 1 at
    /// the end; the alpha is given separately with <see cref="ColorRamp.At(double, byte)" />).
    /// </summary>
    public static readonly ColorRamp ThreadRamp = new ColorRamp(
        Color.FromRgb(246, 112, 168), Color.FromRgb(232, 146, 92), Color.FromRgb(156, 122, 248));

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
