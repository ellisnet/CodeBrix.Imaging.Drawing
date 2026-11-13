using System;

namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// One key of a <see cref="KeyframeTrack{T}"/>: a value at a time, and how the track arrives
/// at it from the previous key. The easing (or custom curve) shapes the segment that ENDS at
/// this key - "get to this value by this time, like this" - and a hold key keeps the previous
/// value until its time and then jumps (a step). The first key's easing is never used.
/// </summary>
/// <typeparam name="T">The value type (float, PointF, Color, or any type a track can interpolate).</typeparam>
public readonly struct Keyframe<T>
{
    private readonly Func<double, double> _curve;

    /// <summary>The key's time, seconds.</summary>
    public double Time { get; }

    /// <summary>The value at the key's time.</summary>
    public T Value { get; }

    /// <summary>The named curve of the segment arriving at this key (unused when <see cref="HasCustomCurve"/> or <see cref="IsHold"/>).</summary>
    public EasingKind Easing { get; }

    /// <summary>Indicates whether the segment arriving at this key is shaped by a caller-supplied curve.</summary>
    public bool HasCustomCurve => _curve != null;

    /// <summary>
    /// The caller-supplied curve that shapes the segment arriving at this key (it maps the
    /// segment's linear progress 0..1 to eased progress); <c>null</c> when the key uses the named
    /// <see cref="Easing"/>. Read it to derive another track from this one key by key - or use
    /// <see cref="WithValue(T)"/>, which keeps the curve for you.
    /// </summary>
    public Func<double, double> Curve => _curve;

    /// <summary>
    /// Indicates a hold (step) key: the track keeps the PREVIOUS key's value until this key's
    /// time, then takes this key's value.
    /// </summary>
    public bool IsHold { get; }

    /// <summary>
    /// Creates a key reached along a named curve.
    /// </summary>
    /// <param name="time">The key's time, seconds.</param>
    /// <param name="value">The value at that time.</param>
    /// <param name="easing">The curve of the segment arriving at this key; <see cref="EasingKind.Linear"/> by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite.</exception>
    public Keyframe(double time, T value, EasingKind easing = EasingKind.Linear)
    {
        if (!Double.IsFinite(time)) { throw new ArgumentOutOfRangeException(nameof(time)); }
        Time = time;
        Value = value;
        Easing = easing;
        IsHold = false;
        _curve = null;
    }

    /// <summary>
    /// Creates a key reached along a curve of the caller's own (for example a
    /// <see cref="CubicBezierEasing"/>'s <c>Evaluate</c> method).
    /// </summary>
    /// <param name="time">The key's time, seconds.</param>
    /// <param name="value">The value at that time.</param>
    /// <param name="curve">Maps the segment's linear progress 0..1 to eased progress.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> is null.</exception>
    public Keyframe(double time, T value, Func<double, double> curve)
        : this(time, value, EasingKind.Linear)
    {
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));
    }

    private Keyframe(double time, T value, bool hold)
        : this(time, value, EasingKind.Linear)
    {
        IsHold = hold;
    }

    /// <summary>
    /// Creates a hold (step) key: the previous value holds until <paramref name="time"/>, then
    /// the track jumps to <paramref name="value"/>.
    /// </summary>
    /// <param name="time">The key's time, seconds.</param>
    /// <param name="value">The value from that time on.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite.</exception>
    public static Keyframe<T> Hold(double time, T value) => new Keyframe<T>(time, value, hold: true);

    /// <summary>
    /// The same key - same time, same easing or custom <see cref="Curve"/>, same hold - with
    /// another value: what a track derived from another one key by key (a mirrored or scaled copy)
    /// is built from, so that its segments are eased exactly like the original's.
    /// </summary>
    /// <param name="value">The new value.</param>
    /// <returns>The key with that value.</returns>
    public Keyframe<T> WithValue(T value)
    {
        if (IsHold) { return Hold(Time, value); }
        return _curve != null ? new Keyframe<T>(Time, value, _curve) : new Keyframe<T>(Time, value, Easing);
    }

    //The eased progress through the segment that ends at this key
    internal double Ease(double fraction)
        => _curve != null ? _curve(fraction) : Animation.Easing.Evaluate(Easing, fraction);

    /// <inheritdoc />
    public override string ToString() => IsHold ? $"{Time}: {Value} (hold)" : $"{Time}: {Value} ({Easing})";
}
