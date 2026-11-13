using System;
using System.Collections.Generic;
using System.Globalization;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// An animation's clock: its length, its frame rate, the conversion between time and frame
/// numbers, and a sheet of named <see cref="TimelineCue"/> values (stretches of time and
/// instants) whose eased progress drives the animated values. It holds no current time of its
/// own - every member is a pure function of the time passed in - so one timeline can serve a
/// live view, stills and a frame-by-frame render at once. Adding cues is thread-safe, and so
/// is everything that reads them.
/// <para>
/// FRAMES: frame <c>i</c> shows the moment <c>i / FramesPerSecond</c>; the timeline has
/// <see cref="FrameCount"/> frames, every one starting before <see cref="Duration"/>.
/// A time maps to the frame whose interval contains it (a time within a millionth of a frame
/// before a frame's start counts as that frame, so <c>FrameAt(TimeOfFrame(i))</c> is always
/// <c>i</c>).
/// </para>
/// </summary>
public sealed class AnimationTimeline
{
    //How close (in frames) a time may fall short of a frame boundary and still count as on it
    private const double FrameTolerance = 1e-6;

    private readonly object _cuesLocker = new object();
    private TimelineCue[] _cues = Array.Empty<TimelineCue>();

    /// <summary>The animation's length, seconds.</summary>
    public double Duration { get; }

    /// <summary>The frame rate of rendered frames, frames per second.</summary>
    public double FramesPerSecond { get; }

    /// <summary>The length of one frame, seconds (<c>1 / FramesPerSecond</c>).</summary>
    public double FrameDuration => 1.0 / FramesPerSecond;

    /// <summary>
    /// The number of frames: every frame whose start time lies before the end, so at least 1
    /// (for example 21 s at 30 fps is 630 frames, 0..629).
    /// </summary>
    public int FrameCount { get; }

    /// <summary>The cues, in the order they were added.</summary>
    public IReadOnlyList<TimelineCue> Cues => _cues;

    /// <summary>
    /// Creates a timeline.
    /// </summary>
    /// <param name="duration">The animation's length, seconds; positive.</param>
    /// <param name="framesPerSecond">The frame rate; positive (30 by default).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either value is not finite and positive.</exception>
    public AnimationTimeline(double duration, double framesPerSecond = 30)
    {
        if (!Double.IsFinite(duration) || duration <= 0) { throw new ArgumentOutOfRangeException(nameof(duration)); }
        if (!Double.IsFinite(framesPerSecond) || framesPerSecond <= 0) { throw new ArgumentOutOfRangeException(nameof(framesPerSecond)); }
        Duration = duration;
        FramesPerSecond = framesPerSecond;
        double frames = Math.Ceiling((duration * framesPerSecond) - FrameTolerance);
        FrameCount = (int)Math.Clamp(frames, 1, Int32.MaxValue);
    }

    /// <summary>The moment a frame shows: <c>frame / FramesPerSecond</c>.</summary>
    /// <param name="frame">The frame number; 0 or more (frames past the end are allowed and extrapolate).</param>
    /// <returns>The frame's time, seconds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="frame"/> is negative.</exception>
    public double TimeOfFrame(int frame)
    {
        if (frame < 0) { throw new ArgumentOutOfRangeException(nameof(frame)); }
        return frame / FramesPerSecond;
    }

    /// <summary>
    /// The frame that shows a time: the frame whose interval contains it, clamped to
    /// 0..<see cref="FrameCount"/> - 1. NaN gives frame 0.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The frame number.</returns>
    public int FrameAt(double seconds)
    {
        if (Double.IsNaN(seconds)) { return 0; }
        double frame = Math.Floor((seconds * FramesPerSecond) + FrameTolerance);
        return (int)Math.Clamp(frame, 0, FrameCount - 1);
    }

    /// <summary>Clamps a time to the timeline, 0..<see cref="Duration"/> (NaN gives 0).</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The clamped time.</returns>
    public double Clamp(double seconds) => Double.IsNaN(seconds) ? 0 : Math.Clamp(seconds, 0, Duration);

    /// <summary>
    /// Formats a time as a timecode, <c>HH:MM:SS:FF</c> - whole hours, minutes and seconds and
    /// the frame within the second - counting frames at the nominal (rounded) frame rate
    /// without dropping frame numbers. Negative times are shown from 0.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The timecode, for example <c>00:00:12:07</c>.</returns>
    public string FormatTimecode(double seconds)
    {
        double whole = Double.IsNaN(seconds) || seconds < 0 ? 0 : Math.Floor((seconds * FramesPerSecond) + FrameTolerance);
        long frames = (long)Math.Min(whole, Int64.MaxValue / 2);
        long nominal = Math.Max(1, (long)Math.Round(FramesPerSecond, MidpointRounding.AwayFromZero));
        long totalSeconds = frames / nominal;
        long frameInSecond = frames % nominal;
        int frameDigits = Math.Max(2, (nominal - 1).ToString(CultureInfo.InvariantCulture).Length);
        return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}:{3}",
            totalSeconds / 3600, (totalSeconds / 60) % 60, totalSeconds % 60,
            frameInSecond.ToString(CultureInfo.InvariantCulture).PadLeft(frameDigits, '0'));
    }

    /// <summary>
    /// Adds a cue - a named stretch of time whose eased progress drives animated values.
    /// </summary>
    /// <param name="name">The cue's name; unique within the timeline.</param>
    /// <param name="startTime">When it starts, seconds.</param>
    /// <param name="endTime">When it ends, seconds; at least <paramref name="startTime"/>.</param>
    /// <param name="easing">The curve that shapes its progress; <see cref="EasingKind.Linear"/> by default.</param>
    /// <returns>The new cue.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is missing or already used.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite or <paramref name="endTime"/> is before <paramref name="startTime"/>.</exception>
    public TimelineCue AddCue(string name, double startTime, double endTime, EasingKind easing = EasingKind.Linear)
        => AddCue(new TimelineCue(name, startTime, endTime, easing));

    /// <summary>
    /// Adds a cue eased by a curve of the caller's own.
    /// </summary>
    /// <param name="name">The cue's name; unique within the timeline.</param>
    /// <param name="startTime">When it starts, seconds.</param>
    /// <param name="endTime">When it ends, seconds; at least <paramref name="startTime"/>.</param>
    /// <param name="curve">Maps linear progress 0..1 to eased progress.</param>
    /// <returns>The new cue.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is missing or already used.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite or <paramref name="endTime"/> is before <paramref name="startTime"/>.</exception>
    public TimelineCue AddCue(string name, double startTime, double endTime, Func<double, double> curve)
        => AddCue(new TimelineCue(name, startTime, endTime, curve));

    /// <summary>
    /// Adds a marker - a named instant, for example the moment a sound lands.
    /// </summary>
    /// <param name="name">The marker's name; unique within the timeline.</param>
    /// <param name="seconds">Its time.</param>
    /// <returns>The new marker (a cue whose start and end times are equal).</returns>
    /// <exception cref="ArgumentException">Thrown when the name is missing or already used.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seconds"/> is not finite.</exception>
    public TimelineCue AddMarker(string name, double seconds) => AddCue(new TimelineCue(name, seconds, seconds));

    /// <summary>
    /// Adds an existing cue.
    /// </summary>
    /// <param name="cue">The cue.</param>
    /// <returns>The same cue.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cue"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when its name is already used.</exception>
    public TimelineCue AddCue(TimelineCue cue)
    {
        if (cue == null) { throw new ArgumentNullException(nameof(cue)); }
        lock (_cuesLocker)
        {
            foreach (TimelineCue existing in _cues)
            {
                if (String.Equals(existing.Name, cue.Name, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"A cue named '{cue.Name}' already exists.", nameof(cue));
                }
            }
            var cues = new TimelineCue[_cues.Length + 1];
            Array.Copy(_cues, cues, _cues.Length);
            cues[_cues.Length] = cue;
            _cues = cues;
        }
        return cue;
    }

    /// <summary>Finds a cue by its (case-sensitive) name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The cue; or <c>null</c> when there is none of that name.</returns>
    public TimelineCue GetCue(string name)
    {
        if (String.IsNullOrWhiteSpace(name)) { return null; }
        string trimmed = name.Trim();
        foreach (TimelineCue cue in _cues)
        {
            if (String.Equals(cue.Name, trimmed, StringComparison.Ordinal)) { return cue; }
        }
        return null;
    }

    /// <summary>
    /// The eased progress, 0..1, of a time through the named cue (see <see cref="TimelineCue.Progress(double)"/>).
    /// </summary>
    /// <param name="cueName">The cue's name.</param>
    /// <param name="seconds">The time.</param>
    /// <returns>The eased progress.</returns>
    /// <exception cref="ArgumentException">Thrown when there is no cue of that name.</exception>
    public double Progress(string cueName, double seconds)
        => (GetCue(cueName) ?? throw new ArgumentException($"There is no cue named '{cueName}'.", nameof(cueName))).Progress(seconds);

    /// <summary>
    /// The cues active at a time, in the order they were added: every stretch that contains
    /// the time (<see cref="TimelineCue.IsActiveAt(double)"/>), and every marker that falls in
    /// the same frame as the time.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The active cues; empty when none.</returns>
    public IReadOnlyList<TimelineCue> GetActiveCues(double seconds)
    {
        var active = new List<TimelineCue>();
        int frame = FrameAt(seconds);
        foreach (TimelineCue cue in _cues)
        {
            if (cue.IsMarker ? FrameAt(cue.StartTime) == frame : cue.IsActiveAt(seconds)) { active.Add(cue); }
        }
        return active;
    }
}
