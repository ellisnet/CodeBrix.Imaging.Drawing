using System;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// Schedules the arrivals of a formation so it completes EXACTLY at a given time: <c>count</c>
/// arrivals between a first and a last time, spaced by a rate that follows a profile (a ramp, a
/// hold and a taper - <see cref="ArrivalRateProfile"/> - or a function of your own). Arrival k is
/// at the k-th of count evenly spaced quantiles of the rate: the rate is summed over 4000 equal
/// steps (each step's rate taken at its middle), and each quantile is found in that table by
/// linear interpolation. The first arrival is at <see cref="FirstTime"/>, the last at
/// <see cref="LastTime"/> exactly. Shared managed arithmetic - identical in both packages.
/// </summary>
public sealed class ArrivalSchedule
{
    private const int Steps = 4000;
    private readonly double[] _cumulative = new double[Steps + 1];

    /// <summary>Creates a schedule following a ramp-hold-taper profile.</summary>
    /// <param name="firstTime">The first arrival.</param>
    /// <param name="lastTime">The last arrival; at or after the first.</param>
    /// <param name="profile">The rate's shape; or <c>null</c> for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite, the last is before the first, or the profile is out of range.</exception>
    public ArrivalSchedule(double firstTime, double lastTime, ArrivalRateProfile profile = null)
        : this(firstTime, lastTime, Checked(profile ?? new ArrivalRateProfile()).GetRate)
    {
    }

    /// <summary>Creates a schedule following a rate function of your own.</summary>
    /// <param name="firstTime">The first arrival.</param>
    /// <param name="lastTime">The last arrival; at or after the first.</param>
    /// <param name="rate">The relative arrival rate at a fraction 0..1 of the span (0 or more; not all 0).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite or the last is before the first.</exception>
    /// <exception cref="ArgumentException">Thrown when the rate is negative or not finite somewhere, or 0 everywhere.</exception>
    public ArrivalSchedule(double firstTime, double lastTime, Func<double, double> rate)
    {
        if (!Double.IsFinite(firstTime)) { throw new ArgumentOutOfRangeException(nameof(firstTime)); }
        if (!Double.IsFinite(lastTime) || lastTime < firstTime) { throw new ArgumentOutOfRangeException(nameof(lastTime)); }
        if (rate == null) { throw new ArgumentNullException(nameof(rate)); }
        FirstTime = firstTime;
        LastTime = lastTime;
        for (int i = 1; i <= Steps; i++)
        {
            double u = (i - 0.5) / Steps;
            double r = rate(u);
            if (!Double.IsFinite(r) || r < 0) { throw new ArgumentException("The rate must be finite and not negative.", nameof(rate)); }
            _cumulative[i] = _cumulative[i - 1] + r;
        }
        if (!(_cumulative[Steps] > 0)) { throw new ArgumentException("The rate is 0 everywhere.", nameof(rate)); }
    }

    /// <summary>The first arrival, seconds.</summary>
    public double FirstTime { get; }

    /// <summary>The last arrival, seconds.</summary>
    public double LastTime { get; }

    /// <summary>The arrival times of a number of objects, in order: the first at <see cref="FirstTime"/>, the last at <see cref="LastTime"/>.</summary>
    /// <param name="count">The number of objects (0 or more).</param>
    /// <returns>The times.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public double[] GetTimes(int count)
    {
        if (count < 0) { throw new ArgumentOutOfRangeException(nameof(count)); }
        var result = new double[count];
        int j = 0;
        for (int k = 0; k < count; k++)
        {
            double target = _cumulative[Steps] * k / Math.Max(1, count - 1);
            while (j < Steps && _cumulative[j + 1] < target) { j++; }
            double span = _cumulative[Math.Min(Steps, j + 1)] - _cumulative[j];
            double f = span <= 0 ? 0 : (target - _cumulative[j]) / span;
            double u = Math.Clamp((j + f) / Steps, 0, 1);
            result[k] = FirstTime + ((LastTime - FirstTime) * u);
        }

        if (count > 0) { result[count - 1] = LastTime; }
        return result;
    }

    private static ArrivalRateProfile Checked(ArrivalRateProfile profile)
    {
        profile.Validate();
        return profile;
    }
}
