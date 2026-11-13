using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Painting;

/// <summary>
/// KEYFRAME PLAYBACK in float images: exactly <see cref="KeyframePlayer"/>, but each keyframe is
/// painted into a <see cref="DrawingPlaneImage"/> (straight alpha, open-ended values) and the
/// frames between are made with the float overloads of <see cref="DrawingTransition"/> - so the
/// dissolve happens before any tone curve or 8-bit conversion. The same threading, caching,
/// seeking, failure and disposal rules apply. The paint function receives a fresh, transparent
/// black image of the player's size for every keyframe and must not keep it.
/// </summary>
public sealed class PlaneKeyframePlayer : IDisposable
{
    private readonly KeyframeCache<DrawingPlaneImage> _cache;
    private int _disposed;

    /// <summary>
    /// Creates a player.
    /// </summary>
    /// <param name="width">The frame width, pixels.</param>
    /// <param name="height">The frame height, pixels.</param>
    /// <param name="interval">Seconds between keyframes; positive.</param>
    /// <param name="paint">Paints keyframe (number, time) into the image given.</param>
    /// <param name="transition">How frames between keyframes are made; <c>null</c> (the default) for a dissolve.</param>
    /// <param name="cacheCapacity">How many painted keyframes are kept; at least 3.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is less than 1, the interval is not positive and finite, or the capacity is below 3.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="paint"/> is null.</exception>
    public PlaneKeyframePlayer(int width, int height, double interval, Action<int, double, DrawingPlaneImage> paint,
        DrawingTransition transition = null, int cacheCapacity = KeyframePlayer.DefaultCacheCapacity)
    {
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height)); }
        if (!double.IsFinite(interval) || interval <= 0) { throw new ArgumentOutOfRangeException(nameof(interval)); }
        if (paint == null) { throw new ArgumentNullException(nameof(paint)); }
        if (cacheCapacity < 3) { throw new ArgumentOutOfRangeException(nameof(cacheCapacity)); }
        Width = width;
        Height = height;
        Interval = interval;
        Transition = transition ?? DrawingTransition.Dissolve();
        CacheCapacity = cacheCapacity;
        _cache = new KeyframeCache<DrawingPlaneImage>(() => new DrawingPlaneImage(width, height),
            (key, frame) => paint(key, key * interval, frame), cacheCapacity);
    }

    /// <summary>The frame width, pixels.</summary>
    public int Width { get; }

    /// <summary>The frame height, pixels.</summary>
    public int Height { get; }

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is not finite or too far from 0.</exception>
    public int KeyframeAt(double seconds) => KeyframeTiming.KeyframeAt(seconds, Interval, out _);

    /// <summary>
    /// Writes the frame at a time into an image of the player's size (every plane overwritten):
    /// the transition from its keyframe to the next at the time's fraction of the interval.
    /// Blocks until both keyframes exist, and asks for the one after them to be painted ahead.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <param name="destination">An image of the player's size.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination's size differs from the player's.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is not finite or too far from 0.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the player has been disposed.</exception>
    public void RenderFrame(double seconds, DrawingPlaneImage destination)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        if (destination.Width != Width || destination.Height != Height) { throw new ArgumentException("The image must be the player's size.", nameof(destination)); }
        int k = KeyframeTiming.KeyframeAt(seconds, Interval, out float fraction);
        while (true)
        {
            ThrowIfDisposed();
            Task<DrawingPlaneImage> first = _cache.Request(k, k);
            Task<DrawingPlaneImage> second = _cache.Request(k + 1, k);
            _cache.Request(k + 2, k);
            if (!KeyframeTiming.TryWait(first) || (fraction > 0f && !KeyframeTiming.TryWait(second))) { continue; }
            if (fraction <= 0f)
            {
                first.Result.CopyTo(destination);
            }
            else
            {
                Transition.Apply(first.Result, second.Result, destination, fraction);
            }
            return;
        }
    }

    /// <summary>
    /// Asks for the keyframes a time needs (and the one after them) to be painted, without waiting.
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
        if (_disposed != 0) { throw new ObjectDisposedException(nameof(PlaneKeyframePlayer)); }
    }
}
