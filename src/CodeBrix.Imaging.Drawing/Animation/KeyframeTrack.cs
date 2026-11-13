using System;
using System.Collections;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// A keyframe track: values at moments in time, and the value at ANY moment in between -
/// eased segment by segment, held for step keys, and optionally looped or ping-ponged. The
/// concrete tracks are <see cref="FloatTrack"/>, <see cref="PointTrack"/> and
/// <see cref="ColorTrack"/>; derive from this class to animate a type of your own.
/// <para>
/// EVALUATION at time t (after looping maps t into the track's span): before the first key,
/// the first value; at or after the last key, the last value; otherwise, in the segment from
/// key a (the last key at or before t) to key b (the first key after t), the progress
/// <c>u = (t - a.Time) / (b.Time - a.Time)</c> is eased by key b's curve and the value is
/// <c>Interpolate(a.Value, b.Value, eased)</c> - or a.Value when b is a hold key. Keys that
/// share a time make a jump: from that time on, the later-added key's value holds.
/// </para>
/// Keys are kept in time order (a key added at an existing time goes after the keys already
/// there). Reading (<see cref="Evaluate(double)"/>) is thread-safe and lock-free; adding keys
/// while another thread evaluates is safe too (the reader sees the keys before or after the
/// change). The class implements <see cref="IEnumerable{T}"/> so a track can be written with
/// a collection initializer: <c>new FloatTrack { { 0, 0f }, { 2, 1f, EasingKind.CubicOut } }</c>.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public abstract class KeyframeTrack<T> : IEnumerable<Keyframe<T>>
{
    private readonly object _keysLocker = new object();
    private Keyframe<T>[] _keys = Array.Empty<Keyframe<T>>();

    /// <summary>Creates an empty track.</summary>
    protected KeyframeTrack()
    {
    }

    /// <summary>The number of keys.</summary>
    public int Count => _keys.Length;

    /// <summary>What the track does outside its span; <see cref="TrackLoopMode.Hold"/> by default.</summary>
    public TrackLoopMode LoopMode { get; set; }

    /// <summary>The first key's time; 0 when the track is empty.</summary>
    public double StartTime
    {
        get
        {
            Keyframe<T>[] keys = _keys;
            return keys.Length > 0 ? keys[0].Time : 0;
        }
    }

    /// <summary>The last key's time; 0 when the track is empty.</summary>
    public double EndTime
    {
        get
        {
            Keyframe<T>[] keys = _keys;
            return keys.Length > 0 ? keys[keys.Length - 1].Time : 0;
        }
    }

    /// <summary>The span from the first key to the last, seconds (the length of one loop).</summary>
    public double Duration => EndTime - StartTime;

    /// <summary>Adds a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>This track, so calls can be chained.</returns>
    public KeyframeTrack<T> Add(Keyframe<T> key)
    {
        lock (_keysLocker)
        {
            Keyframe<T>[] keys = _keys;
            int index = keys.Length;
            while (index > 0 && keys[index - 1].Time > key.Time) { index--; }
            var updated = new Keyframe<T>[keys.Length + 1];
            Array.Copy(keys, 0, updated, 0, index);
            updated[index] = key;
            Array.Copy(keys, index, updated, index + 1, keys.Length - index);
            _keys = updated;
        }
        return this;
    }

    /// <summary>Adds a key reached along a named curve.</summary>
    /// <param name="time">The key's time, seconds.</param>
    /// <param name="value">The value at that time.</param>
    /// <param name="easing">The curve of the segment arriving at this key; <see cref="EasingKind.Linear"/> by default.</param>
    /// <returns>This track, so calls can be chained.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite.</exception>
    public KeyframeTrack<T> Add(double time, T value, EasingKind easing = EasingKind.Linear)
        => Add(new Keyframe<T>(time, value, easing));

    /// <summary>Adds a key reached along a curve of the caller's own.</summary>
    /// <param name="time">The key's time, seconds.</param>
    /// <param name="value">The value at that time.</param>
    /// <param name="curve">Maps the segment's linear progress 0..1 to eased progress.</param>
    /// <returns>This track, so calls can be chained.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> is null.</exception>
    public KeyframeTrack<T> Add(double time, T value, Func<double, double> curve)
        => Add(new Keyframe<T>(time, value, curve));

    /// <summary>Adds a hold (step) key: the previous value holds until <paramref name="time"/>, then the track jumps.</summary>
    /// <param name="time">The key's time, seconds.</param>
    /// <param name="value">The value from that time on.</param>
    /// <returns>This track, so calls can be chained.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite.</exception>
    public KeyframeTrack<T> AddHold(double time, T value) => Add(Keyframe<T>.Hold(time, value));

    /// <summary>Removes every key.</summary>
    public void Clear()
    {
        lock (_keysLocker) { _keys = Array.Empty<Keyframe<T>>(); }
    }

    /// <summary>Returns a snapshot copy of the keys, in time order.</summary>
    /// <returns>A new array holding the keys.</returns>
    public Keyframe<T>[] GetKeyframes() => (Keyframe<T>[])_keys.Clone();

    /// <summary>
    /// The track's value at a time (see the class remarks for the exact rule). NaN counts as
    /// the track's start time.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the track has no keys.</exception>
    public T Evaluate(double seconds)
    {
        Keyframe<T>[] keys = _keys;
        if (keys.Length < 1) { throw new InvalidOperationException("The track has no keys."); }
        double start = keys[0].Time;
        double end = keys[keys.Length - 1].Time;
        double t = Double.IsNaN(seconds) ? start : MapTime(seconds, start, end);

        if (t < start) { return keys[0].Value; }
        if (t >= end) { return keys[keys.Length - 1].Value; }

        //The last key at or before t starts the segment; its successor lies strictly after t
        int low = 0;
        int high = keys.Length - 1;
        while (high - low > 1)
        {
            int middle = (low + high) / 2;
            if (keys[middle].Time <= t) { low = middle; } else { high = middle; }
        }
        Keyframe<T> a = keys[low];
        Keyframe<T> b = keys[high];
        if (b.IsHold) { return a.Value; }
        double u = (t - a.Time) / (b.Time - a.Time);
        return Interpolate(a.Value, b.Value, b.Ease(u));
    }

    //The value at a time of the track whose every key value is passed through map first (same
    //  times, same easing, same holds, same loop): exactly what a track built key by key with
    //  Keyframe.WithValue(map(value)) would give, without building it
    internal T EvaluateMapped(double seconds, Func<T, T> map)
    {
        Keyframe<T>[] keys = _keys;
        if (keys.Length < 1) { throw new InvalidOperationException("The track has no keys."); }
        double start = keys[0].Time;
        double end = keys[keys.Length - 1].Time;
        double t = Double.IsNaN(seconds) ? start : MapTime(seconds, start, end);

        if (t < start) { return map(keys[0].Value); }
        if (t >= end) { return map(keys[keys.Length - 1].Value); }

        int low = 0;
        int high = keys.Length - 1;
        while (high - low > 1)
        {
            int middle = (low + high) / 2;
            if (keys[middle].Time <= t) { low = middle; } else { high = middle; }
        }
        Keyframe<T> a = keys[low];
        Keyframe<T> b = keys[high];
        if (b.IsHold) { return map(a.Value); }
        double u = (t - a.Time) / (b.Time - a.Time);
        return Interpolate(map(a.Value), map(b.Value), b.Ease(u));
    }

    /// <summary>
    /// Interpolates between two values: <paramref name="fraction"/> 0 gives
    /// <paramref name="from"/> and 1 gives <paramref name="to"/>. The fraction is already eased
    /// and may leave 0..1 for overshooting curves.
    /// </summary>
    /// <param name="from">The value at the segment's start.</param>
    /// <param name="to">The value at the segment's end.</param>
    /// <param name="fraction">The eased progress through the segment.</param>
    /// <returns>The value in between.</returns>
    protected abstract T Interpolate(T from, T to, double fraction);

    /// <inheritdoc />
    public IEnumerator<Keyframe<T>> GetEnumerator() => ((IEnumerable<Keyframe<T>>)_keys).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    //Maps a time into the span [start, end] according to the loop mode (no change for Hold)
    private double MapTime(double seconds, double start, double end)
    {
        double span = end - start;
        if (LoopMode == TrackLoopMode.Hold || !(span > 0) || Double.IsInfinity(seconds)) { return seconds; }
        if (LoopMode == TrackLoopMode.Loop)
        {
            double phase = (seconds - start) % span;
            if (phase < 0) { phase += span; }
            return start + phase;
        }

        double doubled = (seconds - start) % (2 * span);
        if (doubled < 0) { doubled += 2 * span; }
        return start + (doubled <= span ? doubled : (2 * span) - doubled);
    }
}
