using System;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// A named stretch of an <see cref="AnimationTimeline"/> - "intro", "draw", "fade out" - or,
/// when <see cref="StartTime"/> equals <see cref="EndTime"/>, a named instant (a marker). A cue turns
/// any time into a <see cref="Progress(double)"/> of 0..1 through it, eased by its own curve,
/// which is what most animated values are a function of. Immutable and thread-safe.
/// </summary>
public sealed class TimelineCue
{
    private readonly Func<double, double> _curve;

    /// <summary>The cue's name, unique within its timeline (compared case-sensitively).</summary>
    public string Name { get; }

    /// <summary>When the cue starts, seconds.</summary>
    public double StartTime { get; }

    /// <summary>When the cue ends, seconds; equal to <see cref="StartTime"/> for a marker.</summary>
    public double EndTime { get; }

    /// <summary>The cue's length, seconds (<c>EndTime - StartTime</c>; 0 for a marker).</summary>
    public double Duration => EndTime - StartTime;

    /// <summary>Indicates whether the cue is a single instant rather than a stretch of time.</summary>
    public bool IsMarker => EndTime == StartTime;

    /// <summary>
    /// The named curve that shapes <see cref="Progress(double)"/>; ignored when the cue was
    /// created with a custom curve (see <see cref="HasCustomCurve"/>).
    /// </summary>
    public EasingKind Easing { get; }

    /// <summary>Indicates whether the cue eases with a caller-supplied curve instead of <see cref="Easing"/>.</summary>
    public bool HasCustomCurve => _curve != null;

    /// <summary>
    /// Creates a cue eased by a named curve.
    /// </summary>
    /// <param name="name">The cue's name.</param>
    /// <param name="startTime">When it starts, seconds.</param>
    /// <param name="endTime">When it ends, seconds; at least <paramref name="startTime"/> (equal for a marker).</param>
    /// <param name="easing">The curve that shapes its progress; <see cref="EasingKind.Linear"/> by default.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite or <paramref name="endTime"/> is before <paramref name="startTime"/>.</exception>
    public TimelineCue(string name, double startTime, double endTime, EasingKind easing = EasingKind.Linear)
    {
        if (String.IsNullOrWhiteSpace(name)) { throw new ArgumentException("A cue name is required.", nameof(name)); }
        if (!Double.IsFinite(startTime)) { throw new ArgumentOutOfRangeException(nameof(startTime)); }
        if (!Double.IsFinite(endTime) || endTime < startTime) { throw new ArgumentOutOfRangeException(nameof(endTime)); }
        Name = name.Trim();
        StartTime = startTime;
        EndTime = endTime;
        Easing = easing;
    }

    /// <summary>
    /// Creates a cue eased by a curve of the caller's own - for example a
    /// <see cref="CubicBezierEasing"/>'s <c>Evaluate</c> method.
    /// </summary>
    /// <param name="name">The cue's name.</param>
    /// <param name="startTime">When it starts, seconds.</param>
    /// <param name="endTime">When it ends, seconds; at least <paramref name="startTime"/>.</param>
    /// <param name="curve">Maps linear progress 0..1 to eased progress.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite or <paramref name="endTime"/> is before <paramref name="startTime"/>.</exception>
    public TimelineCue(string name, double startTime, double endTime, Func<double, double> curve)
        : this(name, startTime, endTime, EasingKind.Linear)
    {
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));
    }

    /// <summary>
    /// Indicates whether the cue is active at a time - whether the time lies inside it:
    /// <c>StartTime &lt;= seconds &lt; EndTime</c> (the same test as a caption cue's
    /// <c>IsActiveAt</c> and an element's <c>IsVisibleAt</c>). A marker contains no time (see <see cref="AnimationTimeline.GetActiveCues(double)"/> for
    /// how markers are reported).
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns><c>true</c> when the time is inside the cue.</returns>
    public bool IsActiveAt(double seconds) => seconds >= StartTime && seconds < EndTime;

    /// <summary>
    /// How far through the cue a time is, linearly: 0 at or before <see cref="StartTime"/>, 1 at
    /// or after <see cref="EndTime"/>, <c>(seconds - StartTime) / (EndTime - StartTime)</c> in between. A
    /// marker gives 0 before its time and 1 from it on. NaN counts as 0.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The linear progress, 0..1.</returns>
    public double LinearProgress(double seconds)
    {
        if (Double.IsNaN(seconds) || seconds <= StartTime) { return IsMarker && seconds >= StartTime ? 1.0 : 0.0; }
        if (seconds >= EndTime) { return 1.0; }
        return (seconds - StartTime) / (EndTime - StartTime);
    }

    /// <summary>
    /// How far through the cue a time is, eased by the cue's curve: the
    /// <see cref="LinearProgress(double)"/> passed through <see cref="Easing"/> (or the custom
    /// curve). 0 before the cue, 1 after it; curves that overshoot (Back, Elastic) may leave
    /// 0..1 inside it.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The eased progress.</returns>
    public double Progress(double seconds)
    {
        double linear = LinearProgress(seconds);
        return _curve != null ? _curve(linear) : Animation.Easing.Evaluate(Easing, linear);
    }

    /// <summary>The time relative to the cue's start (negative before it starts; not clamped).</summary>
    /// <param name="seconds">The time.</param>
    /// <returns><c>seconds - StartTime</c>.</returns>
    public double LocalTime(double seconds) => seconds - StartTime;

    /// <inheritdoc />
    public override string ToString() => IsMarker ? $"{Name} @ {StartTime}" : $"{Name} [{StartTime}, {EndTime})";
}
