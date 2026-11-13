using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.Imaging.Drawing.Painting;

//The engine behind KeyframePlayer and PlaneKeyframePlayer: keyframes painted on ONE background
//  worker (so the caller's paint function is never entered twice at once), nearest-needed first,
//  held in a bounded cache. A request returns a task for its keyframe's finished frame; frames are
//  never reused or written after they are finished, so a reader may keep one after it has been
//  dropped from the cache. Every member is thread-safe.
internal sealed class KeyframeCache<TFrame> where TFrame : class
{
    private sealed class Entry
    {
        public int Key;
        public bool Started;
        public readonly TaskCompletionSource<TFrame> Done = new TaskCompletionSource<TFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly Func<TFrame> _allocate;
    private readonly Action<int, TFrame> _paint;
    private readonly int _capacity;
    private readonly object _locker = new object();
    private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
    private readonly List<Entry> _pending = new List<Entry>();
    private Task _worker = Task.CompletedTask;
    private bool _working;
    private bool _disposed;
    private int _focus;
    private long _paintCount;

    internal KeyframeCache(Func<TFrame> allocate, Action<int, TFrame> paint, int capacity)
    {
        _allocate = allocate;
        _paint = paint;
        _capacity = capacity;
    }

    internal long PaintCount => Interlocked.Read(ref _paintCount);

    internal int CachedCount
    {
        get { lock (_locker) { return _entries.Count; } }
    }

    //Returns the keyframe's task, starting its painting when it is neither cached nor queued;
    //  focus is the keyframe the caller is showing now (it decides what is kept and what first)
    internal Task<TFrame> Request(int key, int focus)
    {
        lock (_locker)
        {
            if (_disposed) { throw new ObjectDisposedException(GetType().Name); }
            _focus = focus;
            if (!_entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry { Key = key };
                _entries.Add(key, entry);
                _pending.Add(entry);
                Evict();
                if (!_working)
                {
                    _working = true;
                    _worker = Task.Run(Work);
                }
            }
            return entry.Done.Task;
        }
    }

    //Waits for any painting in progress, drops everything queued, and refuses new requests
    internal void Dispose()
    {
        Task worker;
        lock (_locker)
        {
            if (_disposed) { return; }
            _disposed = true;
            foreach (Entry entry in _pending)
            {
                _entries.Remove(entry.Key);
                entry.Done.TrySetCanceled();
            }
            _pending.Clear();
            worker = _worker;
        }
        try
        {
            worker.Wait();
        }
        catch (AggregateException)
        {
            //A failed painting was reported to whoever asked for it
        }
    }

    //Keeps at most _capacity entries: drops the ones farthest from the focus (never one being
    //  painted, never the three the focus needs: focus, focus + 1 and focus + 2)
    private void Evict()
    {
        while (_entries.Count > _capacity)
        {
            Entry victim = null;
            long worst = -1;
            foreach (Entry entry in _entries.Values)
            {
                if (entry.Started && !entry.Done.Task.IsCompleted) { continue; }
                if (entry.Key >= _focus && entry.Key <= _focus + 2) { continue; }
                long distance = Distance(entry.Key);
                if (distance > worst || (distance == worst && entry.Key > victim.Key))
                {
                    worst = distance;
                    victim = entry;
                }
            }
            if (victim == null) { return; }
            _entries.Remove(victim.Key);
            if (_pending.Remove(victim)) { victim.Done.TrySetCanceled(); }
        }
    }

    //How far a keyframe is from what is needed: the ones ahead of the focus first, in order,
    //  then the ones behind it
    private long Distance(int key) => key >= _focus ? (long)key - _focus : ((long)_focus - key) + int.MaxValue;

    private void Work()
    {
        while (true)
        {
            Entry entry;
            lock (_locker)
            {
                if (_disposed || _pending.Count == 0)
                {
                    _working = false;
                    return;
                }
                int best = 0;
                for (int i = 1; i < _pending.Count; i++)
                {
                    if (Distance(_pending[i].Key) < Distance(_pending[best].Key)) { best = i; }
                }
                entry = _pending[best];
                _pending.RemoveAt(best);
                entry.Started = true;
            }

            try
            {
                TFrame frame = _allocate();
                Interlocked.Increment(ref _paintCount);
                _paint(entry.Key, frame);
                entry.Done.TrySetResult(frame);
            }
            catch (Exception e)
            {
                //Forget the failure so that a later request paints the keyframe again
                lock (_locker)
                {
                    if (_entries.TryGetValue(entry.Key, out Entry current) && current == entry) { _entries.Remove(entry.Key); }
                }
                entry.Done.TrySetException(e);
            }

            lock (_locker) { Evict(); }
        }
    }
}
