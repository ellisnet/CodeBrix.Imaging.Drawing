using System;
using System.Collections.Generic;
using System.Threading;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// Renders frames AHEAD of a moving play head on background threads into a bounded buffer, for a
/// live view that cannot draw some stretch of an animation in real time (a camera move that
/// redraws every layer, a busy export-quality frame): tell it where the play head is
/// (<see cref="SetPlayHead"/>), and ask for the frame on screen (<see cref="TryGetFrame"/>) - when
/// it is ready, copy it; when not, draw live as usual.
/// <para>
/// HOW: <see cref="WorkerCount"/> background threads (below normal priority) each build ONE
/// session from the factory - on their own thread, the first time they have work - and keep it
/// for the cache's whole life, so a scene is built once per worker, not once per request. The
/// frames wanted are the first <see cref="Capacity"/> frames at or after the play head that
/// <see cref="ShouldRender"/> accepts (every frame when it is <c>null</c>); workers render the
/// nearest missing one first. Moving the play head re-aims at once: frames that left the window
/// are dropped (a frame already being drawn is finished and thrown away - an export cannot be
/// interrupted), and up to <see cref="KeepBehind"/> frames just behind the head stay. So at most
/// <see cref="Capacity"/> + <see cref="KeepBehind"/> frames are held, plus one per worker in flight.
/// </para>
/// <para>
/// THE FRAMES are exactly what <see cref="FrameSequenceRenderer"/> makes: the session's Time set to
/// the frame's moment, <see cref="PrepareFrame"/>, then
/// <see cref="DrawingSession.ExportPixels(Span{byte}, Size, bool)"/> (straight-alpha RGBA, stride
/// width x 4). Write the scene as for parallel rendering (see <see cref="FrameSequenceRenderer"/>):
/// one complete, independent scene per factory call, every frame a function of its time alone -
/// then a cached frame and a live one of the same moment are the same picture.
/// </para>
/// <para>
/// THREADS: <see cref="SetPlayHead"/>, <see cref="TryGetFrame"/>, <see cref="Contains"/> and the
/// counters may be called from any thread (typically the UI thread) at any time;
/// <see cref="ShouldRender"/> is called on any thread and must be a pure, cheap function of the
/// frame number. Set the properties before the first <see cref="SetPlayHead"/>. A worker's exception
/// stops the rendering and is kept in <see cref="Failure"/> (frames already made stay available).
/// Dispose stops the workers, waits for them, and disposes their sessions when
/// <see cref="DisposeSessions"/> is set.
/// </para>
/// </summary>
public sealed class FrameCache : IDisposable
{
    /// <summary>The number of frames a cache holds ahead of the play head unless told otherwise.</summary>
    public const int DefaultCapacity = 60;

    private readonly Func<DrawingSession> _sessionFactory;
    private readonly object _gate = new object();
    private readonly Dictionary<int, byte[]> _frames = new Dictionary<int, byte[]>();
    private readonly HashSet<int> _inFlight = new HashSet<int>();
    private readonly Stack<byte[]> _spare = new Stack<byte[]>();
    private Thread[] _workers;
    private int _head = -1;
    private int _windowEnd = -1;
    private bool _stopping;
    private int _keepBehind = 2;

    /// <summary>Creates a cache (no work starts before the first <see cref="SetPlayHead"/>).</summary>
    /// <param name="timeline">The timeline: its frame rate turns frame numbers into times, its frame count bounds the frames.</param>
    /// <param name="sessionFactory">Builds one complete scene (a session) - called once per worker, on that worker's thread.</param>
    /// <param name="frameSize">The pixel size of the frames.</param>
    /// <param name="capacity">How many frames ahead of the play head to hold (1 or more).</param>
    /// <param name="workerCount">How many frames are rendered at once, each worker with its own session (1 or more).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeline"/> or <paramref name="sessionFactory"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a size dimension, the capacity or the worker count is less than 1.</exception>
    public FrameCache(AnimationTimeline timeline, Func<DrawingSession> sessionFactory, Size frameSize, int capacity = DefaultCapacity, int workerCount = 1)
    {
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        if (frameSize.Width < 1 || frameSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(frameSize)); }
        if (capacity < 1) { throw new ArgumentOutOfRangeException(nameof(capacity)); }
        if (workerCount < 1) { throw new ArgumentOutOfRangeException(nameof(workerCount)); }
        FrameSize = frameSize;
        Capacity = capacity;
        WorkerCount = workerCount;
    }

    /// <summary>The timeline whose frame rate turns frame numbers into times.</summary>
    public AnimationTimeline Timeline { get; }

    /// <summary>The pixel size of the frames.</summary>
    public Size FrameSize { get; }

    /// <summary>The number of bytes in one frame (width x height x 4).</summary>
    public int FrameBytes => checked(FrameSize.Width * FrameSize.Height * 4);

    /// <summary>How many frames ahead of the play head the cache holds.</summary>
    public int Capacity { get; }

    /// <summary>How many frames are rendered at once (one session each).</summary>
    public int WorkerCount { get; }

    /// <summary>
    /// How many frames just behind the play head are kept once rendered (so a frame shown a moment
    /// ago - or a small step back - is still there); 2 by default, 0 or more.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 0.</exception>
    public int KeepBehind
    {
        get => _keepBehind;
        set
        {
            if (value < 0) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _keepBehind = value;
        }
    }

    /// <summary>
    /// Which frames are worth rendering ahead (the stretches that cannot be drawn live); <c>null</c>
    /// (the default) for every frame. Called on any thread: keep it a pure, cheap function.
    /// </summary>
    public Func<int, bool> ShouldRender { get; set; }

    /// <summary>Whether frames include the session's background; <c>true</c> by default (<c>false</c> renders over transparency).</summary>
    public bool IncludeBackground { get; set; } = true;

    /// <summary>Optional per-frame work after the session's time is set and before the frame is exported (on the worker's thread, touching only its session).</summary>
    public Action<DrawingSession, double> PrepareFrame { get; set; }

    /// <summary>Whether the workers' sessions are disposed when the cache is; <c>true</c> by default.</summary>
    public bool DisposeSessions { get; set; } = true;

    /// <summary>The play head (the frame being shown); -1 before the first <see cref="SetPlayHead"/>.</summary>
    public int PlayHead
    {
        get
        {
            lock (_gate) { return _head; }
        }
    }

    /// <summary>The number of frames ready.</summary>
    public int Count
    {
        get
        {
            lock (_gate) { return _frames.Count; }
        }
    }

    /// <summary>The first exception a worker met, which stopped the rendering; <c>null</c> while all is well.</summary>
    public Exception Failure { get; private set; }

    /// <summary>
    /// Tells the cache where the play head is: the frames from there on (the first
    /// <see cref="Capacity"/> that <see cref="ShouldRender"/> accepts) are rendered nearest first,
    /// and frames that are no longer wanted are dropped. Starts the workers the first time. Cheap -
    /// call it every displayed frame.
    /// </summary>
    /// <param name="frame">The frame being shown (clamped to the timeline).</param>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public void SetPlayHead(int frame)
    {
        frame = Math.Clamp(frame, 0, Timeline.FrameCount - 1);
        lock (_gate)
        {
            if (_stopping) { throw new ObjectDisposedException(nameof(FrameCache)); }
            if (_workers == null) { StartWorkers(); }
            if (frame == _head) { return; }
            _head = frame;
            _windowEnd = FindWindowEnd(frame);
            Evict();
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>Indicates whether a frame is ready.</summary>
    /// <param name="frame">The frame number.</param>
    /// <returns><c>true</c> when <see cref="TryGetFrame"/> would succeed.</returns>
    public bool Contains(int frame)
    {
        lock (_gate) { return _frames.ContainsKey(frame); }
    }

    /// <summary>Copies a ready frame into a buffer.</summary>
    /// <param name="frame">The frame number.</param>
    /// <param name="destination">Where to copy it: at least <see cref="FrameBytes"/> bytes (straight-alpha RGBA, stride width x 4).</param>
    /// <returns><c>true</c> when the frame was ready and copied; <c>false</c> (destination untouched) when it was not.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small.</exception>
    public bool TryGetFrame(int frame, Span<byte> destination)
    {
        int bytes = FrameBytes;
        if (destination.Length < bytes)
        {
            throw new ArgumentException($"The destination holds {destination.Length} bytes but a frame needs {bytes}.", nameof(destination));
        }
        lock (_gate)
        {
            if (!_frames.TryGetValue(frame, out byte[] rgba)) { return false; }
            new ReadOnlySpan<byte>(rgba, 0, bytes).CopyTo(destination);
            return true;
        }
    }

    /// <summary>Stops the workers, waits for them to finish the frame each is drawing, and lets go of the frames (and of the sessions when <see cref="DisposeSessions"/> is set).</summary>
    public void Dispose()
    {
        Thread[] workers;
        lock (_gate)
        {
            if (_stopping) { return; }
            _stopping = true;
            workers = _workers;
            Monitor.PulseAll(_gate);
        }
        if (workers != null)
        {
            foreach (Thread worker in workers) { worker.Join(); }
        }
        lock (_gate)
        {
            _frames.Clear();
            _spare.Clear();
        }
    }

    private void StartWorkers()
    {
        _workers = new Thread[WorkerCount];
        for (int w = 0; w < WorkerCount; w++)
        {
            _workers[w] = new Thread(Work) { IsBackground = true, Name = "FrameCache " + w, Priority = ThreadPriority.BelowNormal };
            _workers[w].Start();
        }
    }

    private void Work()
    {
        DrawingSession session = null;
        try
        {
            while (true)
            {
                int frame;
                byte[] buffer;
                lock (_gate)
                {
                    while (true)
                    {
                        if (_stopping || Failure != null) { return; }
                        frame = NextWanted();
                        if (frame >= 0) { break; }
                        Monitor.Wait(_gate);
                    }
                    _inFlight.Add(frame);
                    buffer = _spare.Count > 0 ? _spare.Pop() : null;
                }

                try
                {
                    buffer ??= new byte[FrameBytes];
                    session ??= _sessionFactory() ?? throw new InvalidOperationException("The session factory returned null.");
                    double seconds = Timeline.TimeOfFrame(frame);
                    session.Time = seconds;
                    PrepareFrame?.Invoke(session, seconds);
                    session.ExportPixels(new Span<byte>(buffer, 0, FrameBytes), FrameSize, IncludeBackground);
                }
                catch (Exception e)
                {
                    lock (_gate)
                    {
                        _inFlight.Remove(frame);
                        Failure ??= e;
                        Monitor.PulseAll(_gate);
                    }
                    return;
                }

                lock (_gate)
                {
                    _inFlight.Remove(frame);
                    if (!_stopping && IsInWindow(frame) && !_frames.ContainsKey(frame)) { _frames[frame] = buffer; }
                    else if (_spare.Count < WorkerCount) { _spare.Push(buffer); }
                    Monitor.PulseAll(_gate);
                }
            }
        }
        finally
        {
            if (session != null && DisposeSessions) { session.Dispose(); }
        }
    }

    //The nearest wanted frame at or after the head that is neither ready nor being drawn; -1 when none
    private int NextWanted()
    {
        if (_head < 0) { return -1; }
        for (int f = _head; f <= _windowEnd; f++)
        {
            if (_frames.ContainsKey(f) || _inFlight.Contains(f) || !Wanted(f)) { continue; }
            return f;
        }
        return -1;
    }

    //The last frame of the window: the Capacity-th wanted frame at or after the head (or the last frame)
    private int FindWindowEnd(int head)
    {
        int last = Timeline.FrameCount - 1;
        int counted = 0;
        for (int f = head; f <= last; f++)
        {
            if (!Wanted(f)) { continue; }
            if (++counted == Capacity) { return f; }
        }
        return last;
    }

    private bool IsInWindow(int frame) => frame >= _head && frame <= _windowEnd && Wanted(frame);

    private bool Wanted(int frame) => ShouldRender == null || ShouldRender(frame);

    //Drops the frames that left the window, keeping up to KeepBehind frames just behind the head
    private void Evict()
    {
        List<int> drop = null;
        foreach (int f in _frames.Keys)
        {
            bool behind = f < _head && f >= _head - _keepBehind;
            if (behind || IsInWindow(f)) { continue; }
            (drop ??= new List<int>()).Add(f);
        }
        if (drop == null) { return; }
        foreach (int f in drop)
        {
            _spare.Push(_frames[f]);
            _frames.Remove(f);
        }
        while (_spare.Count > WorkerCount) { _spare.Pop(); }
    }
}
