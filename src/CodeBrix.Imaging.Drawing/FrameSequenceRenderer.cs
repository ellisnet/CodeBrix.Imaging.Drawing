using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// Receives one rendered frame from a <see cref="FrameSequenceRenderer"/>.
/// </summary>
/// <param name="frame">The frame number on the timeline.</param>
/// <param name="seconds">The moment the frame shows.</param>
/// <param name="rgba">
/// The frame: straight-alpha RGBA bytes, rows top to bottom, stride width x 4 (the
/// <see cref="DrawingSession.ExportPixels(Span{byte}, Size, bool)"/> layout). Valid only during
/// the call - copy it (or write it to an encoder) before returning.
/// </param>
public delegate void FrameRenderedCallback(int frame, double seconds, ReadOnlySpan<byte> rgba);

/// <summary>
/// Renders a run of frames of a timeline-driven scene - frames N..M, or a time range - as raw
/// RGBA, in order, optionally on several threads at once. Each frame is made by setting a
/// session's <see cref="DrawingSession.Time"/> to the frame's moment (which applies the
/// session's animations, animated shapes and time windows), calling <see cref="PrepareFrame"/>
/// when set, and exporting the frame with <see cref="DrawingSession.ExportPixels(Span{byte}, Size, bool)"/>.
/// <para>
/// PARALLEL RENDERING (<see cref="MaxDegreeOfParallelism"/> above 1): each worker thread gets
/// its OWN session from the factory - a session is never shared between threads - and the
/// callback still receives the frames one at a time, in frame order, on the calling thread.
/// For the output to be identical whatever the thread count, write the scene so that the
/// frame at a time depends only on that time: build everything in the factory (one complete,
/// independent scene per call - no element, group, animated shape or camera shared between
/// sessions), drive change only through the session's Time (bindings, animated shapes,
/// time windows) and <see cref="PrepareFrame"/>, compute every value from the time instead
/// of accumulating it from frame to frame, and only READ shared objects (tracks, timelines,
/// fills, font registries) while rendering. Exports never use the live-render caches, so a
/// worker's history never shows in its frames.
/// </para>
/// </summary>
public sealed class FrameSequenceRenderer
{
    private readonly Func<DrawingSession> _sessionFactory;
    private readonly object _keptLocker = new object();
    private readonly List<DrawingSession> _kept = new List<DrawingSession>();
    private int _maxDegreeOfParallelism = 1;

    /// <summary>
    /// Creates a renderer.
    /// </summary>
    /// <param name="timeline">The timeline: its frame rate turns frame numbers into times.</param>
    /// <param name="sessionFactory">Builds one complete scene (a session) - called once per worker, on the calling thread, at the start of each render (only for workers no kept session serves, with <see cref="KeepSessions"/>).</param>
    /// <param name="frameSize">The pixel size of the frames.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeline"/> or <paramref name="sessionFactory"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension of <paramref name="frameSize"/> is less than 1.</exception>
    public FrameSequenceRenderer(AnimationTimeline timeline, Func<DrawingSession> sessionFactory, Size frameSize)
    {
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        if (frameSize.Width < 1 || frameSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(frameSize)); }
        FrameSize = frameSize;
    }

    /// <summary>The timeline whose frame rate turns frame numbers into times.</summary>
    public AnimationTimeline Timeline { get; }

    /// <summary>The pixel size of the frames.</summary>
    public Size FrameSize { get; }

    /// <summary>The number of bytes in one frame (width x height x 4).</summary>
    public int FrameBytes => checked(FrameSize.Width * FrameSize.Height * 4);

    /// <summary>Whether frames include the session's background; <c>true</c> by default (<c>false</c> renders over transparency).</summary>
    public bool IncludeBackground { get; set; } = true;

    /// <summary>
    /// How many frames may render at once, each on its own thread with its own session; 1 (the
    /// default) renders on the calling thread.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
    public int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        set
        {
            if (value < 1) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _maxDegreeOfParallelism = value;
        }
    }

    /// <summary>
    /// Optional per-frame work run after the session's time is set and before the frame is
    /// exported - for scene code that is not expressed as bindings (it receives the session
    /// and the frame's time). Runs on the worker's thread; it must touch only that session.
    /// </summary>
    public Action<DrawingSession, double> PrepareFrame { get; set; }

    /// <summary>
    /// Whether the sessions the factory made are disposed when a render finishes; <c>true</c>
    /// by default. Set <c>false</c> when the factory hands out sessions you keep using.
    /// </summary>
    public bool DisposeSessions { get; set; } = true;

    /// <summary>
    /// Whether the sessions the factory made are KEPT for the next render instead of being
    /// released when a render finishes; <c>false</c> by default (a render builds its sessions and
    /// lets them go, as always). Set it when the same renderer renders many short runs - a
    /// look-ahead cache, a scrubbing preview - and building a scene costs more than its frames: the
    /// factory is then called only when a render needs more workers than are kept, and the kept
    /// sessions are disposed (when <see cref="DisposeSessions"/> is set) by
    /// <see cref="ReleaseSessions"/>. Renders running at the same time never share a session.
    /// Because exports never use the live caches, a kept session's frames are exactly a fresh
    /// session's.
    /// </summary>
    public bool KeepSessions { get; set; }

    /// <summary>The number of sessions kept for the next render (see <see cref="KeepSessions"/>).</summary>
    public int KeptSessionCount
    {
        get
        {
            lock (_keptLocker) { return _kept.Count; }
        }
    }

    /// <summary>
    /// Lets go of the kept sessions (see <see cref="KeepSessions"/>), disposing them when
    /// <see cref="DisposeSessions"/> is set. Sessions in use by a render still running are kept by
    /// that render and released at its end as its settings say.
    /// </summary>
    public void ReleaseSessions()
    {
        DrawingSession[] sessions;
        lock (_keptLocker)
        {
            sessions = _kept.ToArray();
            _kept.Clear();
        }
        if (DisposeSessions)
        {
            foreach (DrawingSession session in sessions) { session.Dispose(); }
        }
    }

    /// <summary>
    /// Renders frames <paramref name="firstFrame"/> to <paramref name="lastFrame"/> (inclusive)
    /// and hands each to <paramref name="onFrame"/>, in frame order, on the calling thread.
    /// </summary>
    /// <param name="firstFrame">The first frame; 0 or more.</param>
    /// <param name="lastFrame">The last frame; at least <paramref name="firstFrame"/> (frames past the timeline's end are allowed).</param>
    /// <param name="onFrame">Receives each frame.</param>
    /// <param name="cancellationToken">Stops the render; the call then throws <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onFrame"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame range is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is cancelled.</exception>
    public void Render(int firstFrame, int lastFrame, FrameRenderedCallback onFrame, CancellationToken cancellationToken = default)
    {
        if (onFrame == null) { throw new ArgumentNullException(nameof(onFrame)); }
        if (firstFrame < 0) { throw new ArgumentOutOfRangeException(nameof(firstFrame)); }
        if (lastFrame < firstFrame) { throw new ArgumentOutOfRangeException(nameof(lastFrame)); }

        int count = lastFrame - firstFrame + 1;
        int workers = Math.Min(_maxDegreeOfParallelism, count);
        int window = workers == 1 ? 1 : workers * 2;
        int frameBytes = FrameBytes;
        var slots = new byte[window][];
        for (int i = 0; i < window; i++) { slots[i] = new byte[frameBytes]; }

        Run(firstFrame, count, workers, window, k => slots[k % window],
            k => onFrame(firstFrame + k, Timeline.TimeOfFrame(firstFrame + k), new ReadOnlySpan<byte>(slots[k % window], 0, frameBytes)),
            cancellationToken);
    }

    /// <summary>
    /// Renders every frame whose moment lies in [<paramref name="startTime"/>,
    /// <paramref name="endTime"/>) - clamped to the timeline - and hands each to
    /// <paramref name="onFrame"/> in order. Nothing is rendered when no frame falls inside.
    /// </summary>
    /// <param name="startTime">The start of the range (inclusive).</param>
    /// <param name="endTime">The end of the range (exclusive).</param>
    /// <param name="onFrame">Receives each frame.</param>
    /// <param name="cancellationToken">Stops the render; the call then throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>The number of frames rendered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onFrame"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is NaN.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is cancelled.</exception>
    public int RenderTimeRange(double startTime, double endTime, FrameRenderedCallback onFrame, CancellationToken cancellationToken = default)
    {
        if (onFrame == null) { throw new ArgumentNullException(nameof(onFrame)); }
        if (Double.IsNaN(startTime)) { throw new ArgumentOutOfRangeException(nameof(startTime)); }
        if (Double.IsNaN(endTime)) { throw new ArgumentOutOfRangeException(nameof(endTime)); }

        double fps = Timeline.FramesPerSecond;
        double first = Math.Max(0, Math.Ceiling((startTime * fps) - 1e-6));
        double last = Math.Min(Timeline.FrameCount - 1, Math.Ceiling((endTime * fps) - 1e-6) - 1);
        if (last < first) { return 0; }
        Render((int)first, (int)last, onFrame, cancellationToken);
        return (int)(last - first) + 1;
    }

    /// <summary>
    /// Renders consecutive frames straight into caller buffers: frame
    /// <c>firstFrame + i</c> into <c>destinations[i]</c>. With several threads the buffers
    /// fill in any order; the call returns when all are done.
    /// </summary>
    /// <param name="firstFrame">The first frame; 0 or more.</param>
    /// <param name="destinations">One buffer per frame, each at least <see cref="FrameBytes"/> long.</param>
    /// <param name="cancellationToken">Stops the render; the call then throws <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destinations"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="firstFrame"/> is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when a buffer is null or too small.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is cancelled.</exception>
    public void Render(int firstFrame, IReadOnlyList<byte[]> destinations, CancellationToken cancellationToken = default)
    {
        if (destinations == null) { throw new ArgumentNullException(nameof(destinations)); }
        if (firstFrame < 0) { throw new ArgumentOutOfRangeException(nameof(firstFrame)); }
        int frameBytes = FrameBytes;
        foreach (byte[] destination in destinations)
        {
            if (destination == null || destination.Length < frameBytes)
            {
                throw new ArgumentException($"Every destination must hold at least {frameBytes} bytes.", nameof(destinations));
            }
        }
        if (destinations.Count < 1) { return; }

        int workers = Math.Min(_maxDegreeOfParallelism, destinations.Count);
        Run(firstFrame, destinations.Count, workers, destinations.Count, k => destinations[k], null, cancellationToken);
    }

    //Renders count frames from first. bufferFor(k) is frame k's buffer (k relative to first);
    //  deliver(k), when given, is called in order on this thread once frame k is ready, and
    //  frame k + window may not start before frame k has been delivered
    private void Run(int first, int count, int workers, int window, Func<int, byte[]> bufferFor, Action<int> deliver,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sessions = new DrawingSession[workers];
        bool keep = KeepSessions;
        try
        {
            for (int w = 0; w < workers; w++) { sessions[w] = TakeSession() ?? throw new InvalidOperationException("The session factory returned null."); }

            if (workers == 1)
            {
                for (int k = 0; k < count; k++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RenderInto(sessions[0], first + k, bufferFor(k));
                    deliver?.Invoke(k);
                }
                return;
            }

            RunParallel(sessions, first, count, window, bufferFor, deliver, cancellationToken);
        }
        finally
        {
            if (keep && KeepSessions)
            {
                lock (_keptLocker)
                {
                    foreach (DrawingSession session in sessions)
                    {
                        if (session != null) { _kept.Add(session); }
                    }
                }
            }
            else if (DisposeSessions)
            {
                foreach (DrawingSession session in sessions) { session?.Dispose(); }
            }
        }
    }

    //A kept session when there is one, else a new one from the factory
    private DrawingSession TakeSession()
    {
        lock (_keptLocker)
        {
            if (_kept.Count > 0)
            {
                DrawingSession session = _kept[_kept.Count - 1];
                _kept.RemoveAt(_kept.Count - 1);
                return session;
            }
        }
        return _sessionFactory();
    }

    private void RunParallel(DrawingSession[] sessions, int first, int count, int window, Func<int, byte[]> bufferFor,
        Action<int> deliver, CancellationToken cancellationToken)
    {
        var gate = new object();
        var readyFrame = new int[window];
        for (int i = 0; i < window; i++) { readyFrame[i] = -1; }
        int nextToClaim = 0;
        int delivered = 0;
        int finished = 0;
        bool stop = false;
        Exception failure = null;
        bool ordered = deliver != null;

        void Worker(object state)
        {
            DrawingSession session = (DrawingSession)state;
            while (true)
            {
                int k;
                lock (gate)
                {
                    while (true)
                    {
                        if (stop || failure != null || nextToClaim >= count) { return; }
                        if (!ordered || nextToClaim - delivered < window) { break; }
                        Monitor.Wait(gate);
                    }
                    k = nextToClaim++;
                }

                try
                {
                    RenderInto(session, first + k, bufferFor(k));
                }
                catch (Exception e)
                {
                    lock (gate)
                    {
                        failure ??= e;
                        Monitor.PulseAll(gate);
                    }
                    return;
                }

                lock (gate)
                {
                    readyFrame[k % window] = k;
                    finished++;
                    Monitor.PulseAll(gate);
                }
            }
        }

        var threads = new Thread[sessions.Length];
        using (cancellationToken.Register(() =>
        {
            lock (gate) { Monitor.PulseAll(gate); }
        }))
        {
            try
            {
                for (int w = 0; w < sessions.Length; w++)
                {
                    threads[w] = new Thread(Worker) { IsBackground = true, Name = "FrameSequenceRenderer " + w };
                    threads[w].Start(sessions[w]);
                }

                if (ordered)
                {
                    for (int k = 0; k < count; k++)
                    {
                        lock (gate)
                        {
                            while (readyFrame[k % window] != k && failure == null && !cancellationToken.IsCancellationRequested)
                            {
                                Monitor.Wait(gate);
                            }
                            if (failure != null || cancellationToken.IsCancellationRequested) { break; }
                        }

                        deliver(k);

                        lock (gate)
                        {
                            delivered = k + 1;
                            Monitor.PulseAll(gate);
                        }
                    }
                }
                else
                {
                    lock (gate)
                    {
                        while (finished < count && failure == null && !cancellationToken.IsCancellationRequested)
                        {
                            Monitor.Wait(gate);
                        }
                    }
                }
            }
            finally
            {
                lock (gate)
                {
                    stop = true;
                    Monitor.PulseAll(gate);
                }
                foreach (Thread thread in threads) { thread?.Join(); }
            }
        }

        if (failure != null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void RenderInto(DrawingSession session, int frame, byte[] buffer)
    {
        double seconds = Timeline.TimeOfFrame(frame);
        session.Time = seconds;
        PrepareFrame?.Invoke(session, seconds);
        session.ExportPixels(new Span<byte>(buffer, 0, FrameBytes), FrameSize, IncludeBackground);
    }
}
