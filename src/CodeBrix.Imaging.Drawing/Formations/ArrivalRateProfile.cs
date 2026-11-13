using System;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// The shape of an <see cref="ArrivalSchedule"/>'s arrival rate over its span (u = 0 at the first
/// arrival, 1 at the last): a RAMP from <see cref="StartRate"/> up to 1 until
/// <see cref="RampUntil"/> (<c>StartRate + (1 - StartRate) x (u / RampUntil)^RampExponent</c>), a
/// HOLD at 1, and from <see cref="TaperFrom"/> a TAPER down towards <see cref="EndRate"/>
/// (<c>(1 - (u - TaperFrom) / (1 - TaperFrom))^TaperExponent + EndRate</c>) - a lazy start, a busy
/// middle and a long tail, so a formation completes without an abrupt stop. The rate is relative
/// (only its shape matters).
/// </summary>
public sealed class ArrivalRateProfile
{
    /// <summary>The fraction of the span over which the rate builds up (0..1); 0.3 by default.</summary>
    public double RampUntil { get; set; } = 0.3;

    /// <summary>The fraction of the span from which the rate tapers away (RampUntil..1); 0.72 by default.</summary>
    public double TaperFrom { get; set; } = 0.72;

    /// <summary>The rate at the very start, relative to the held rate of 1; 0.06 by default.</summary>
    public double StartRate { get; set; } = 0.06;

    /// <summary>The power of the ramp's curve (above 1: slow, then quickening); 1.6 by default.</summary>
    public double RampExponent { get; set; } = 1.6;

    /// <summary>The power of the taper's curve (above 1: a long thin tail); 2.1 by default.</summary>
    public double TaperExponent { get; set; } = 2.1;

    /// <summary>The rate the taper ends at (a trickle, so the last arrivals are not infinitely far apart); 0.012 by default.</summary>
    public double EndRate { get; set; } = 0.012;

    /// <summary>The relative arrival rate at a fraction of the span (see the class remarks for the rule).</summary>
    /// <param name="fraction">The fraction of the span, 0..1.</param>
    /// <returns>The rate.</returns>
    public double GetRate(double fraction)
    {
        double u = fraction;
        return u < RampUntil ? StartRate + ((1 - StartRate) * Math.Pow(u / RampUntil, RampExponent))
            : u < TaperFrom ? 1
            : Math.Pow(1 - ((u - TaperFrom) / (1 - TaperFrom)), TaperExponent) + EndRate;
    }

    internal void Validate()
    {
        if (!Double.IsFinite(RampUntil) || RampUntil < 0 || RampUntil > 1) { throw new ArgumentOutOfRangeException(nameof(RampUntil)); }
        if (!Double.IsFinite(TaperFrom) || TaperFrom < RampUntil || TaperFrom >= 1) { throw new ArgumentOutOfRangeException(nameof(TaperFrom)); }
        if (!Double.IsFinite(StartRate) || StartRate < 0) { throw new ArgumentOutOfRangeException(nameof(StartRate)); }
        if (!Double.IsFinite(RampExponent) || RampExponent <= 0) { throw new ArgumentOutOfRangeException(nameof(RampExponent)); }
        if (!Double.IsFinite(TaperExponent) || TaperExponent <= 0) { throw new ArgumentOutOfRangeException(nameof(TaperExponent)); }
        if (!Double.IsFinite(EndRate) || EndRate < 0) { throw new ArgumentOutOfRangeException(nameof(EndRate)); }
    }
}
