using System;

namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// WHEN a <see cref="SafeAreaGuide"/> applies, for guides that matter only part of the time
/// - for example a YouTube end screen, which can cover the final 20 seconds. A guide
/// without a time rule applies at every moment. A session's check and overlays always know
/// the moment - it is the session's (or renderer's) <c>Time</c> unless the overlay's own
/// <c>Time</c> is set. The guide's own methods (<see cref="SafeAreaGuide.GetUnsafeZones"/>,
/// <see cref="SafeAreaGuide.GetSafeRectangle"/>, <see cref="AppliesAt"/>) have no clock: given no
/// time, they treat the moment as unknown and count every rule as applying, so nothing is
/// missed. A <see cref="FinalSeconds(double)"/> rule also applies whenever the video's duration
/// is unknown.
/// </summary>
public sealed class SafeAreaTimeRule
{
    private readonly bool _isFinal;

    private SafeAreaTimeRule(bool isFinal, double start, double end, double duration)
    {
        _isFinal = isFinal;
        StartTime = start;
        EndTime = end;
        Duration = duration;
    }

    /// <summary>
    /// For a <see cref="Between(double, double)"/> rule, the window's start time, seconds from
    /// the start of the video; 0 for a final-seconds rule (its window is placed by the video's
    /// duration - see <see cref="Duration"/>).
    /// </summary>
    public double StartTime { get; }

    /// <summary>For a <see cref="Between(double, double)"/> rule, the window's end time; 0 for a final-seconds rule.</summary>
    public double EndTime { get; }

    /// <summary>
    /// How long the window lasts, seconds: the number of final seconds for a
    /// <see cref="FinalSeconds(double)"/> rule, <c>EndTime - StartTime</c> for a
    /// <see cref="Between(double, double)"/> rule.
    /// </summary>
    public double Duration { get; }

    /// <summary>Indicates whether this rule counts back from the end of the video.</summary>
    public bool IsFinalSeconds => _isFinal;

    /// <summary>
    /// A rule that applies during the final <paramref name="seconds"/> of a video (from
    /// duration - seconds to the end, inclusive).
    /// </summary>
    /// <param name="seconds">How many final seconds; must be positive.</param>
    /// <returns>The rule.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seconds"/> is not positive and finite.</exception>
    public static SafeAreaTimeRule FinalSeconds(double seconds)
    {
        if (!(seconds > 0) || Double.IsInfinity(seconds)) { throw new ArgumentOutOfRangeException(nameof(seconds)); }
        return new SafeAreaTimeRule(true, 0, 0, seconds);
    }

    /// <summary>
    /// A rule that applies from <paramref name="startTime"/> to <paramref name="endTime"/>, both inclusive.
    /// </summary>
    /// <param name="startTime">The window's start time, seconds from the start of the video.</param>
    /// <param name="endTime">The window's end time; not before the start.</param>
    /// <returns>The rule.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite or the end is before the start.</exception>
    public static SafeAreaTimeRule Between(double startTime, double endTime)
    {
        if (!Double.IsFinite(startTime)) { throw new ArgumentOutOfRangeException(nameof(startTime)); }
        if (!Double.IsFinite(endTime) || endTime < startTime) { throw new ArgumentOutOfRangeException(nameof(endTime)); }
        return new SafeAreaTimeRule(false, startTime, endTime, endTime - startTime);
    }

    /// <summary>
    /// Indicates whether the rule applies at a moment of a video.
    /// </summary>
    /// <param name="seconds">The moment, seconds from the start; or <c>null</c> when unknown (the rule then applies).</param>
    /// <param name="duration">The video's length; or <c>null</c> when unknown (a final-seconds rule then applies).</param>
    /// <returns><c>true</c> when the rule applies.</returns>
    public bool AppliesAt(double? seconds, double? duration)
    {
        if (!seconds.HasValue) { return true; }
        if (_isFinal)
        {
            return !duration.HasValue || seconds.Value >= duration.Value - Duration;
        }
        return seconds.Value >= StartTime && seconds.Value <= EndTime;
    }

    /// <inheritdoc />
    public override string ToString()
        => _isFinal
            ? FormattableString.Invariant($"final {Duration} s")
            : FormattableString.Invariant($"{StartTime} s to {EndTime} s");
}
