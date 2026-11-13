using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Painting;

/// <summary>
/// Paints one keyframe of a <see cref="KeyframePlayer"/> into the buffer it is given.
/// </summary>
/// <param name="keyframe">The keyframe's number (0 at time 0, 1 one interval later, and so on; negative before 0).</param>
/// <param name="seconds">The keyframe's time: <c>keyframe x interval</c>.</param>
/// <param name="rgba">The frame to fill completely: straight-alpha RGBA, rows top-down, stride width x 4.</param>
public delegate void KeyframePaintCallback(int keyframe, double seconds, Span<byte> rgba);

/// <summary>
/// KEYFRAME PLAYBACK for animations too costly to paint every frame - a painterly scene painted
/// every few seconds, the frames between dissolving from one painting to the next. Keyframe k
/// is painted at time <c>k x Interval</c> by the caller's function; the frame at time t
/// is <see cref="Transition"/> (by default <see cref="DrawingTransition.Dissolve"/>) from
/// keyframe <c>k = floor(t / interval)</c> to keyframe k + 1 at fraction
/// <c>u = t / interval - k</c> (exactly keyframe k when u is 0). While keyframes k and k + 1 are
/// on screen, k + 2 is painted ahead in the background, so steady playback never waits; after a
/// seek the first frame waits for its two keyframes, and the result is always exactly what
/// painting them on the spot would give.
/// <para>
/// THREADS: the paint function runs on ONE background worker, one keyframe at a time (never
/// twice at once - it need not be thread-safe, but it must not touch state the caller changes
/// meanwhile), nearest-needed keyframe first; keyframes no longer needed after a seek are
/// dropped before they start. Every member of the player is thread-safe; RenderFrame blocks
/// until its keyframes exist. At most <see cref="CacheCapacity"/> painted keyframes are kept.
/// A paint function that throws makes the RenderFrame that needed it throw the same exception;
/// that keyframe is painted afresh when asked for again. <see cref="Dispose"/> drops queued
/// paintings and waits for the one in progress. Never call the player from inside the paint
/// function.
/// </para>
/// </summary>
public sealed class KeyframePlayer : IDisposable
{
    /// <summary>The default <see cref="CacheCapacity"/>: the two keyframes on screen, the one painted ahead, and one spare.</summary>
    public const int DefaultCacheCapacity = 4;

    private readonly KeyframeCache<byte[]> _cache;
    private int _disposed;

    /// <summary>
    /// Creates a player.
    /// </summary>
    /// <param name="width">The frame width, pixels.</param>
    /// <param name="height">The frame height, pixels.</param>
    /// <param name="interval">Seconds between keyframes; positive.</param>
    /// <param name="paint">Paints a keyframe into the buffer given.</param>
    /// <param name="transition">How frames between keyframes are made; <c>null</c> (the default) for a dissolve.</param>
    /// <param name="cacheCapacity">How many painted keyframes are kept; at least 3.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is less than 1, the interval is not positive and finite, or the capacity is below 3.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="paint"/> is null.</exception>
    public KeyframePlayer(int width, int height, double interval, KeyframePaintCallback paint,
        DrawingTransition transition = null, int cacheCapacity = DefaultCacheCapacity)
    {
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height)); }
        if (!double.IsFinite(interval) || interval <= 0) { throw new ArgumentOutOfRangeException(nameof(interval)); }
        if (paint == null) { throw new ArgumentNullException(nameof(paint)); }
        if (cacheCapacity < 3) { throw new ArgumentOutOfRangeException(nameof(cacheCapacity)); }
        Width = width;
        Height = height;
        FrameBytes = checked(width * height * 4);
        Interval = interval;
        Transition = transition ?? DrawingTransition.Dissolve();
        CacheCapacity = cacheCapacity;
        _cache = new KeyframeCache<byte[]>(() => new byte[FrameBytes],
            (key, frame) => paint(key, key * interval, frame), cacheCapacity);
    }

    /// <summary>The frame width, pixels.</summary>
    public int Width { get; }

    /// <summary>The frame height, pixels.</summary>
    public int Height { get; }

    /// <summary>The size of one frame, bytes (width x height x 4).</summary>
    public int FrameBytes { get; }

    /// <summary>Seconds between keyframes.</summary>
    public double Interval { get; }

    /// <summary>How the frames between two keyframes are made.</summary>
    public DrawingTransition Transition { get; }

    /// <summary>How many painted keyframes are kept at most.</summary>
    public int CacheCapacity { get; }

    /// <summary>How many times the paint function has been called so far.</summary>
    public long PaintCount => _cache.PaintCount;

    /// <summary>How many keyframes are cached or queued right now.</summary>
    public int CachedKeyframeCount => _cache.CachedCount;

    /// <summary>The keyframe a time starts from: <c>floor(seconds / interval)</c>.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The keyframe's number.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is not finite or too far from 0 for an int keyframe number.</exception>
    public int KeyframeAt(double seconds) => KeyframeTiming.KeyframeAt(seconds, Interval, out _);

    /// <summary>
    /// Writes the frame at a time: the transition from its keyframe to the next at the time's
    /// fraction of the interval. Blocks until both keyframes exist, and asks for the one after
    /// them to be painted ahead.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <param name="destination">Receives the frame (straight-alpha RGBA, <see cref="FrameBytes"/> bytes); extra bytes are untouched.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is shorter than <see cref="FrameBytes"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is not finite or too far from 0.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the player has been disposed.</exception>
    public void RenderFrame(double seconds, Span<byte> destination)
    {
        if (destination.Length < FrameBytes) { throw new ArgumentException("The destination is shorter than one frame.", nameof(destination)); }
        int k = KeyframeTiming.KeyframeAt(seconds, Interval, out float fraction);
        while (true)
        {
            ThrowIfDisposed();
            Task<byte[]> first = _cache.Request(k, k);
            Task<byte[]> second = _cache.Request(k + 1, k);
            _cache.Request(k + 2, k);
            if (!KeyframeTiming.TryWait(first) || (fraction > 0f && !KeyframeTiming.TryWait(second))) { continue; }
            if (fraction <= 0f)
            {
                first.Result.AsSpan(0, FrameBytes).CopyTo(destination);
            }
            else
            {
                Blend(first.Result, second.Result, destination.Slice(0, FrameBytes), fraction);
            }
            return;
        }
    }

    //The transition between two keyframes. A dissolve or a fade through a colour treats every pixel on
    //  its own, so it runs on bands of rows in parallel (into a pooled buffer, then copied out - a span
    //  cannot cross threads); each pixel's arithmetic is exactly the single-threaded one, so the bytes
    //  are the same. Wipes, slides and pushes depend on positions and run on the whole frame
    private void Blend(byte[] from, byte[] to, Span<byte> destination, float fraction)
    {
        DrawingTransitionKind kind = Transition.Kind;
        if ((kind != DrawingTransitionKind.Dissolve && kind != DrawingTransitionKind.FadeThroughColor) || Height < 2
            || FrameBytes < PlaneKernels.ParallelThreshold * 4)
        {
            Transition.Apply(from, to, destination, Width, Height, fraction);
            return;
        }
        byte[] buffer = ArrayPool<byte>.Shared.Rent(FrameBytes);
        try
        {
            int bands = Math.Min(Height, Math.Max(1, Environment.ProcessorCount));
            int rowBytes = Width * 4;
            Parallel.For(0, bands, band =>
            {
                int top = (int)((long)Height * band / bands), bottom = (int)((long)Height * (band + 1) / bands);
                int offset = top * rowBytes, length = (bottom - top) * rowBytes;
                Transition.Apply(new ReadOnlySpan<byte>(from, offset, length), new ReadOnlySpan<byte>(to, offset, length),
                    new Span<byte>(buffer, offset, length), Width, bottom - top, fraction);
            });
            new ReadOnlySpan<byte>(buffer, 0, FrameBytes).CopyTo(destination);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Asks for the keyframes a time needs (and the one after them) to be painted, without
    /// waiting - call it ahead of <see cref="RenderFrame"/> to overlap painting with other work.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is not finite or too far from 0.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the player has been disposed.</exception>
    public void Prefetch(double seconds)
    {
        ThrowIfDisposed();
        int k = KeyframeTiming.KeyframeAt(seconds, Interval, out _);
        _cache.Request(k, k);
        _cache.Request(k + 1, k);
        _cache.Request(k + 2, k);
    }

    /// <summary>Indicates whether <see cref="Dispose"/> has been called (the player cannot be used afterwards).</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Drops the queued paintings and waits for the one in progress; the player cannot be used afterwards.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) { return; }
        _cache.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed != 0) { throw new ObjectDisposedException(nameof(KeyframePlayer)); }
    }
}

//The timing rule and the waiting both players share
internal static class KeyframeTiming
{
    internal static int KeyframeAt(double seconds, double interval, out float fraction)
    {
        if (!double.IsFinite(seconds)) { throw new ArgumentOutOfRangeException(nameof(seconds)); }
        double position = seconds / interval;
        double floor = Math.Floor(position);
        if (floor < int.MinValue + 2.0 || floor > int.MaxValue - 3.0) { throw new ArgumentOutOfRangeException(nameof(seconds)); }
        fraction = (float)(position - floor);
        return (int)floor;
    }

    //Waits for a keyframe; false when its painting was dropped (ask again), the paint function's
    //  own exception when it failed
    internal static bool TryWait<TFrame>(Task<TFrame> task)
    {
        try
        {
            task.Wait();
        }
        catch (AggregateException)
        {
            //Inspected below
        }
        if (task.IsCanceled) { return false; }
        task.GetAwaiter().GetResult();
        return true;
    }
}
