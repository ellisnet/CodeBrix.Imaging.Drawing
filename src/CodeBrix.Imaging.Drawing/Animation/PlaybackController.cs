using System;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// The play / pause / seek state of an animation being shown live - a small state machine with
/// no clock, window or thread of its own. The host feeds it the real time that passed since
/// the last frame (<see cref="Advance(double)"/>), reads <see cref="Time"/>, and shows that
/// moment; keys and buttons call <see cref="Toggle"/>, <see cref="Seek(double)"/>,
/// <see cref="SeekBy(double)"/>, <see cref="Step(int)"/> and <see cref="Restart"/>.
/// <para>
/// A slow frame PAUSES the animation rather than skipping it: each advance moves at most
/// <see cref="MaxStep"/> of real time (times <see cref="Speed"/>). Without looping,
/// playback stops at the end (or at 0 when playing backwards) and <see cref="Advance(double)"/>
/// reports it; <see cref="Play"/> or <see cref="Toggle"/> from there starts again from the
/// beginning. Not thread-safe: drive it from one thread (the UI thread).
/// </para>
/// </summary>
public sealed class PlaybackController
{
    /// <summary>The default cap on the real time one <see cref="Advance(double)"/> may cover, seconds.</summary>
    public const double DefaultMaxStep = 0.1;

    private double _speed = 1.0;
    private double _maxStep = DefaultMaxStep;

    /// <summary>
    /// Creates a controller for an animation of the given length, paused at time 0.
    /// </summary>
    /// <param name="duration">The animation's length, seconds; positive.</param>
    /// <param name="framesPerSecond">The frame rate that <see cref="Step(int)"/> and <see cref="Frame"/> use; positive (30 by default).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either value is not finite and positive.</exception>
    public PlaybackController(double duration, double framesPerSecond = 30)
        : this(new AnimationTimeline(duration, framesPerSecond))
    {
    }

    /// <summary>
    /// Creates a controller for a timeline (its length and frame rate), paused at time 0.
    /// </summary>
    /// <param name="timeline">The timeline.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeline"/> is null.</exception>
    public PlaybackController(AnimationTimeline timeline)
    {
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
    }

    /// <summary>The timeline whose length and frame rate the controller uses.</summary>
    public AnimationTimeline Timeline { get; }

    /// <summary>The animation's length, seconds.</summary>
    public double Duration => Timeline.Duration;

    /// <summary>The current animation time, seconds: always within 0..<see cref="Duration"/>.</summary>
    public double Time { get; private set; }

    /// <summary>The frame that shows the current time (see <see cref="AnimationTimeline.FrameAt(double)"/>).</summary>
    public int Frame => Timeline.FrameAt(Time);

    /// <summary>Indicates whether time moves on each <see cref="Advance(double)"/>.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Indicates whether playback wraps around at the end (and at 0 when playing backwards)
    /// instead of stopping; <c>false</c> by default.
    /// </summary>
    public bool IsLooping { get; set; }

    /// <summary>
    /// How fast animation time moves against real time: 1 (the default) is real time, 0.5
    /// half speed, negative values play backwards, 0 freezes the picture while "playing".
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not finite.</exception>
    public double Speed
    {
        get => _speed;
        set
        {
            if (!Double.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _speed = value;
        }
    }

    /// <summary>
    /// The most real time one <see cref="Advance(double)"/> may cover, seconds
    /// (<see cref="DefaultMaxStep"/> by default): a longer gap - a slow frame, a window
    /// being dragged - pauses the animation instead of skipping ahead.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not finite and positive.</exception>
    public double MaxStep
    {
        get => _maxStep;
        set
        {
            if (!Double.IsFinite(value) || value <= 0) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _maxStep = value;
        }
    }

    /// <summary>Indicates whether the current time is the end of the animation.</summary>
    public bool IsAtEnd => Time >= Duration;

    /// <summary>
    /// Starts (or keeps) playing. When playback has stopped at the end (or at 0, playing
    /// backwards) and is not looping, it starts again from the beginning (or the end).
    /// </summary>
    public void Play()
    {
        if (!IsLooping)
        {
            if (_speed >= 0 && IsAtEnd) { Time = 0; }
            else if (_speed < 0 && Time <= 0) { Time = Duration; }
        }
        IsPlaying = true;
    }

    /// <summary>Stops time moving; the current time is kept.</summary>
    public void Pause() => IsPlaying = false;

    /// <summary>Pauses when playing, otherwise plays (see <see cref="Play"/>) - the usual Space-bar action.</summary>
    public void Toggle()
    {
        if (IsPlaying) { Pause(); } else { Play(); }
    }

    /// <summary>Jumps to a time (clamped to the animation; NaN goes to 0); playing or paused stays as it was.</summary>
    /// <param name="seconds">The time to show.</param>
    public void Seek(double seconds) => Time = Timeline.Clamp(seconds);

    /// <summary>Jumps forward (positive) or back (negative) by an amount of animation time, clamped to the animation.</summary>
    /// <param name="delta">The amount, seconds.</param>
    public void SeekBy(double delta) => Seek(Time + delta);

    /// <summary>
    /// Pauses and moves by whole frames: to the start of the frame <paramref name="frames"/>
    /// away from the current one (negative steps back), clamped to the animation.
    /// </summary>
    /// <param name="frames">How many frames to move; 1 by default.</param>
    public void Step(int frames = 1)
    {
        Pause();
        long target = Math.Clamp((long)Frame + frames, 0, Timeline.FrameCount - 1);
        Time = Timeline.Clamp(target / Timeline.FramesPerSecond);
    }

    /// <summary>Goes back to the beginning and plays.</summary>
    public void Restart()
    {
        Time = 0;
        IsPlaying = true;
    }

    /// <summary>
    /// Moves time on by the real time that passed since the last call (capped at
    /// <see cref="MaxStep"/>, then scaled by <see cref="Speed"/>) when playing. Without
    /// looping, time stops at the end (or at 0 backwards) and playback pauses there; with
    /// looping it wraps around.
    /// </summary>
    /// <param name="elapsed">The real time since the previous call, seconds (negative or NaN counts as 0).</param>
    /// <returns>
    /// <c>true</c> when this call reached the end of the animation in the direction of play
    /// (stopping there, or wrapping when looping); <c>false</c> otherwise.
    /// </returns>
    public bool Advance(double elapsed)
    {
        if (!IsPlaying || !(elapsed > 0) || _speed == 0) { return false; }
        double step = Math.Min(elapsed, _maxStep) * _speed;
        double duration = Duration;
        double next = Time + step;

        if (next >= duration)
        {
            if (IsLooping)
            {
                Time = next % duration;
                return true;
            }
            bool arrived = Time < duration;
            Time = duration;
            IsPlaying = false;
            return arrived;
        }

        if (next <= 0 && _speed < 0)
        {
            if (IsLooping)
            {
                double wrapped = next % duration;
                Time = wrapped < 0 ? wrapped + duration : wrapped;
                return true;
            }
            bool arrived = Time > 0;
            Time = 0;
            IsPlaying = false;
            return arrived;
        }

        Time = next;
        return false;
    }
}
