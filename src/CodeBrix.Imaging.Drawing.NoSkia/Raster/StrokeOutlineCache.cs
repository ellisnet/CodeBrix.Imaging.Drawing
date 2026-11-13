using System;
using System.Collections.Generic;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

//Remembers recent stroke OUTLINES (the polygons StrokeBuilder makes, in the path's own
//  coordinates, before the canvas matrix is applied), so a stroked path drawn again with the same
//  geometry and stroke - only its transform changed, or nothing at all - is filled from the kept
//  outline instead of being stroked again. The outline depends ONLY on the key: the path's verbs
//  and points (compared value by value), the stroke width, cap, join and miter limit, the dash
//  intervals and phase, and the canvas matrix's largest scale (which sets the flattening and arc
//  tolerances). A hit returns the very polygons a miss would build, so the pixels never change -
//  the cache only skips work. Bounded (cleared when it holds too many points); thread-safe.
internal static class StrokeOutlineCache
{
    private const int MaxPoints = 1 << 21;
    private static readonly object Locker = new object();
    private static readonly Dictionary<long, Entry> Entries = new Dictionary<long, Entry>();
    private static int _points;

    //A switch for tests (to prove a cached and an uncached draw give the same pixels)
    internal static bool Enabled { get; set; } = true;

    internal static int Count
    {
        get
        {
            lock (Locker) { return Entries.Count; }
        }
    }

    internal static void Clear()
    {
        lock (Locker)
        {
            Entries.Clear();
            _points = 0;
        }
    }

    //The outline for a path and stroke, built by `build` on a miss; the returned arrays are shared
    //  and must not be changed
    public static Vector2[][] GetOrBuild(DrawingPath path, DrawingPaint paint, float maxScale, Func<List<List<Vector2>>> build)
    {
        if (!Enabled) { return ToArrays(build()); }
        IReadOnlyList<DrawingPathVerb> verbs = path.Verbs;
        IReadOnlyList<DrawingPoint> points = path.Points;
        float[] intervals = paint.PathEffect?.Intervals;
        float phase = paint.PathEffect?.Phase ?? 0f;
        long hash = Hash(verbs, points, paint, intervals, phase, maxScale);
        lock (Locker)
        {
            if (Entries.TryGetValue(hash, out Entry entry) && entry.Matches(verbs, points, paint, intervals, phase, maxScale))
            {
                return entry.Outline;
            }
        }

        Vector2[][] outline = ToArrays(build());
        var added = new Entry(verbs, points, paint, intervals, phase, maxScale, outline);
        lock (Locker)
        {
            if (_points + added.Size > MaxPoints)
            {
                Entries.Clear();
                _points = 0;
            }
            if (Entries.TryGetValue(hash, out Entry old)) { _points -= old.Size; }
            Entries[hash] = added;
            _points += added.Size;
        }
        return outline;
    }

    private static Vector2[][] ToArrays(List<List<Vector2>> outline)
    {
        var arrays = new Vector2[outline.Count][];
        for (int i = 0; i < outline.Count; i++) { arrays[i] = outline[i].ToArray(); }
        return arrays;
    }

    private static long Hash(IReadOnlyList<DrawingPathVerb> verbs, IReadOnlyList<DrawingPoint> points, DrawingPaint paint,
        float[] intervals, float phase, float maxScale)
    {
        ulong hash = 0x9E3779B97F4A7C15UL;
        hash = Mix(hash, (ulong)verbs.Count);
        for (int i = 0; i < verbs.Count; i++) { hash = Mix(hash, (ulong)verbs[i]); }
        for (int i = 0; i < points.Count; i++)
        {
            hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(points[i].X));
            hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(points[i].Y));
        }
        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(paint.StrokeWidth));
        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(paint.StrokeMiter));
        hash = Mix(hash, ((ulong)paint.StrokeCap << 8) | (ulong)paint.StrokeJoin);
        hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(maxScale));
        if (intervals != null)
        {
            foreach (float interval in intervals) { hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(interval)); }
            hash = Mix(hash, (uint)BitConverter.SingleToInt32Bits(phase));
        }
        return unchecked((long)hash);
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        ulong z = unchecked(hash + value + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    private static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    private sealed class Entry
    {
        private readonly DrawingPathVerb[] _verbs;
        private readonly DrawingPoint[] _points;
        private readonly float _width;
        private readonly float _miter;
        private readonly DrawingStrokeCap _cap;
        private readonly DrawingStrokeJoin _join;
        private readonly float[] _intervals;
        private readonly float _phase;
        private readonly float _maxScale;

        public Entry(IReadOnlyList<DrawingPathVerb> verbs, IReadOnlyList<DrawingPoint> points, DrawingPaint paint, float[] intervals,
            float phase, float maxScale, Vector2[][] outline)
        {
            _verbs = new DrawingPathVerb[verbs.Count];
            for (int i = 0; i < _verbs.Length; i++) { _verbs[i] = verbs[i]; }
            _points = new DrawingPoint[points.Count];
            for (int i = 0; i < _points.Length; i++) { _points[i] = points[i]; }
            _width = paint.StrokeWidth;
            _miter = paint.StrokeMiter;
            _cap = paint.StrokeCap;
            _join = paint.StrokeJoin;
            _intervals = intervals == null ? null : (float[])intervals.Clone();
            _phase = phase;
            _maxScale = maxScale;
            Outline = outline;
            int size = _points.Length;
            foreach (Vector2[] polygon in outline) { size += polygon.Length; }
            Size = size;
        }

        public Vector2[][] Outline { get; }

        public int Size { get; }

        public bool Matches(IReadOnlyList<DrawingPathVerb> verbs, IReadOnlyList<DrawingPoint> points, DrawingPaint paint, float[] intervals,
            float phase, float maxScale)
        {
            if (verbs.Count != _verbs.Length || points.Count != _points.Length) { return false; }
            if (!Same(paint.StrokeWidth, _width) || !Same(paint.StrokeMiter, _miter) || paint.StrokeCap != _cap || paint.StrokeJoin != _join
                || !Same(maxScale, _maxScale))
            {
                return false;
            }
            if ((intervals == null) != (_intervals == null)) { return false; }
            if (intervals != null)
            {
                if (intervals.Length != _intervals.Length || !Same(phase, _phase)) { return false; }
                for (int i = 0; i < intervals.Length; i++)
                {
                    if (!Same(intervals[i], _intervals[i])) { return false; }
                }
            }
            for (int i = 0; i < _verbs.Length; i++)
            {
                if (verbs[i] != _verbs[i]) { return false; }
            }
            for (int i = 0; i < _points.Length; i++)
            {
                if (!Same(points[i].X, _points[i].X) || !Same(points[i].Y, _points[i].Y)) { return false; }
            }
            return true;
        }
    }
}
