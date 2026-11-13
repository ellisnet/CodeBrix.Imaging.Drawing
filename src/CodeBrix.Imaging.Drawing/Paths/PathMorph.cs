using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// An in-between of two paths: <see cref="GetPath(float)"/> gives the shape a fraction of the
/// way from <see cref="From"/> to <see cref="To"/>. Draw it with a
/// <see cref="CodeBrix.Imaging.Drawing.Shapes.PathShape"/> made from the morph and animate the
/// shape's <c>MorphFraction</c>. Immutable and thread-safe; the point correspondence is worked out
/// once, at construction, in shared managed arithmetic (identical in both packages).
/// <para>
/// THE CORRESPONDENCE RULE. Contours pair up in order (the first with the first, and so on);
/// when one path has more contours, each extra one pairs with a single point at its own centre,
/// so it grows out of (or shrinks into) that point. Each pair is resampled to the same number of
/// points spaced evenly by length (<see cref="SampleCount"/> per pair: the constructor's value,
/// or by default one sample every 2 units of the longer contour, at least 16 and at most 4096).
/// Two closed contours are matched as loops: the second is reversed when the two run opposite
/// ways round, then turned to the starting sample that brings the two closest (least sum of
/// squared distances; the lowest such turn on a tie). Two open contours are matched end to end,
/// the second reversed when that is closer. An open contour paired with a closed one treats the
/// closed one as a loop cut at its best starting sample and run back to it (whichever way round
/// is closer), and the in-between is open. Each in-between point is a + (b - a) x fraction; a pair is closed when both its
/// contours are. Fraction 0 returns <see cref="From"/> itself and 1 returns <see cref="To"/>
/// itself (the exact originals); between them the shape is made of straight segments through
/// the samples, and its fill rule is From's below one half and To's from one half.
/// </para>
/// </summary>
public sealed class PathMorph
{
    private const double DefaultSampleSpacing = 2.0;
    private const int MinimumSamples = 16;
    private const int MaximumSamples = 4096;

    private readonly Pair[] _pairs;

    /// <summary>
    /// Works out the correspondence between two paths.
    /// </summary>
    /// <param name="from">The shape at fraction 0.</param>
    /// <param name="to">The shape at fraction 1.</param>
    /// <param name="samplesPerContour">How many points each contour pair is resampled to; 0 (the default) chooses from the lengths (see the type's remarks); otherwise 2 to 65536.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="from"/> or <paramref name="to"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="samplesPerContour"/> is negative, 1, or above 65536.</exception>
    public PathMorph(PathGeometry from, PathGeometry to, int samplesPerContour = 0)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        if (samplesPerContour < 0 || samplesPerContour == 1 || samplesPerContour > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(samplesPerContour));
        }
        SampleCount = samplesPerContour;

        int count = Math.Max(from.Contours.Count, to.Contours.Count);
        _pairs = new Pair[count];
        for (int i = 0; i < count; i++)
        {
            PathContour a = i < from.Contours.Count ? from.Contours[i] : null;
            PathContour b = i < to.Contours.Count ? to.Contours[i] : null;
            _pairs[i] = BuildPair(a, b, samplesPerContour);
        }
    }

    /// <summary>The shape at fraction 0.</summary>
    public PathGeometry From { get; }

    /// <summary>The shape at fraction 1.</summary>
    public PathGeometry To { get; }

    /// <summary>The samples per contour pair asked for at construction; 0 when chosen from the lengths.</summary>
    public int SampleCount { get; }

    /// <summary>The number of contour pairs (the larger of the two contour counts).</summary>
    public int PairCount => _pairs.Length;

    /// <summary>
    /// The shape at a fraction of the way from <see cref="From"/> to <see cref="To"/>; the
    /// fraction is clamped to 0..1 (NaN counts as 0).
    /// </summary>
    /// <param name="fraction">How far, 0..1.</param>
    /// <returns><see cref="From"/> at 0 or below, <see cref="To"/> at 1 or above, otherwise a new geometry of straight segments.</returns>
    public PathGeometry GetPath(float fraction)
    {
        if (Single.IsNaN(fraction) || fraction <= 0f) { return From; }
        if (fraction >= 1f) { return To; }
        double f = fraction;
        var contours = new List<PathContour>(_pairs.Length);
        foreach (Pair pair in _pairs)
        {
            int n = pair.A.Length / 2;
            if (n < 2) { continue; }
            var points = new PointF[n];
            for (int k = 0; k < n; k++)
            {
                double ax = pair.A[2 * k], ay = pair.A[(2 * k) + 1];
                double bx = pair.B[2 * k], by = pair.B[(2 * k) + 1];
                points[k] = PathMath.P(ax + ((bx - ax) * f), ay + ((by - ay) * f));
            }
            var segments = new PathSegment[pair.Closed ? n : n - 1];
            for (int k = 0; k + 1 < n; k++) { segments[k] = PathSegment.Line(points[k], points[k + 1]); }
            if (pair.Closed) { segments[n - 1] = PathSegment.Line(points[n - 1], points[0]); }
            contours.Add(new PathContour(points[0], segments, pair.Closed, true));
        }
        return new PathGeometry(contours.ToArray(), fraction < 0.5f ? From.FillRule : To.FillRule);
    }

    //Two matched sample lists (x, y interleaved, equal length) and whether the in-between closes
    private sealed class Pair
    {
        public double[] A;
        public double[] B;
        public bool Closed;
    }

    private static Pair BuildPair(PathContour a, PathContour b, int requested)
    {
        double lengthA = a?.LengthValue ?? 0.0, lengthB = b?.LengthValue ?? 0.0;
        int n = requested > 0 ? requested
            : (int)Math.Clamp(Math.Ceiling(Math.Max(lengthA, lengthB) / DefaultSampleSpacing), MinimumSamples, MaximumSamples);

        bool closedA = a?.IsClosed ?? (b?.IsClosed ?? false);
        bool closedB = b?.IsClosed ?? closedA;
        if (closedA && closedB)
        {
            double[] sa = a == null ? null : SampleLoop(a, n);
            double[] sb = b == null ? null : SampleLoop(b, n);
            sa ??= Collapse(sb);
            sb ??= Collapse(sa);
            if (a != null && b != null)
            {
                if (SignedArea(sa) * SignedArea(sb) < 0.0) { sb = ReverseLoop(sb); }
                sb = Rotate(sb, BestShift(sa, sb));
            }
            return new Pair { A = sa, B = sb, Closed = true };
        }

        if (!closedA && !closedB)
        {
            double[] sa = a == null ? null : SampleOpen(a, n);
            double[] sb = b == null ? null : SampleOpen(b, n);
            sa ??= Collapse(sb);
            sb ??= Collapse(sa);
            if (a != null && b != null)
            {
                double[] reversed = ReverseOpen(sb);
                if (SquaredDistance(sa, reversed, 0) < SquaredDistance(sa, sb, 0)) { sb = reversed; }
            }
            return new Pair { A = sa, B = sb, Closed = false };
        }

        //One open, one closed: the loop is cut at its best sample and run back to it
        PathContour open = closedA ? b : a;
        PathContour loop = closedA ? a : b;
        double[] openSamples = SampleOpen(open, n + 1);
        double[] forward = SampleLoop(loop, n);
        double[] backward = ReverseLoop(forward);
        double[] cut = null;
        double best = Double.MaxValue;
        foreach (double[] direction in new[] { forward, backward })
        {
            for (int s = 0; s < n; s++)
            {
                double[] candidate = OpenUp(direction, s);
                double d = SquaredDistance(openSamples, candidate, 0);
                if (d < best) { best = d; cut = candidate; }
            }
        }
        return closedA
            ? new Pair { A = cut, B = openSamples, Closed = false }
            : new Pair { A = openSamples, B = cut, Closed = false };
    }

    //n samples at distances L * k / n (k = 0..n-1) round a closed contour
    private static double[] SampleLoop(PathContour contour, int n)
    {
        PathContour.Measure measure = contour.GetMeasure();
        var samples = new double[2 * n];
        for (int k = 0; k < n; k++)
        {
            measure.PointAt(measure.Total * k / n, out double x, out double y, out _, out _);
            samples[2 * k] = x;
            samples[(2 * k) + 1] = y;
        }
        return samples;
    }

    //n samples at distances L * k / (n - 1) (k = 0..n-1) along an open contour, both ends included
    private static double[] SampleOpen(PathContour contour, int n)
    {
        PathContour.Measure measure = contour.GetMeasure();
        var samples = new double[2 * n];
        for (int k = 0; k < n; k++)
        {
            measure.PointAt(measure.Total * k / (n - 1), out double x, out double y, out _, out _);
            samples[2 * k] = x;
            samples[(2 * k) + 1] = y;
        }
        return samples;
    }

    //A partner for a missing contour: every sample at the other's mean point
    private static double[] Collapse(double[] samples)
    {
        int n = samples.Length / 2;
        double sx = 0.0, sy = 0.0;
        for (int k = 0; k < n; k++) { sx += samples[2 * k]; sy += samples[(2 * k) + 1]; }
        double cx = sx / n, cy = sy / n;
        var result = new double[samples.Length];
        for (int k = 0; k < n; k++) { result[2 * k] = cx; result[(2 * k) + 1] = cy; }
        return result;
    }

    //Twice the shoelace area: positive for clockwise travel on screen (y down)
    private static double SignedArea(double[] samples)
    {
        int n = samples.Length / 2;
        double area = 0.0;
        for (int k = 0; k < n; k++)
        {
            int j = (k + 1) % n;
            area += (samples[2 * k] * samples[(2 * j) + 1]) - (samples[2 * j] * samples[(2 * k) + 1]);
        }
        return area;
    }

    //The loop the other way round, keeping sample 0 first
    private static double[] ReverseLoop(double[] samples)
    {
        int n = samples.Length / 2;
        var result = new double[samples.Length];
        for (int k = 0; k < n; k++)
        {
            int from = (n - k) % n;
            result[2 * k] = samples[2 * from];
            result[(2 * k) + 1] = samples[(2 * from) + 1];
        }
        return result;
    }

    private static double[] ReverseOpen(double[] samples)
    {
        int n = samples.Length / 2;
        var result = new double[samples.Length];
        for (int k = 0; k < n; k++)
        {
            result[2 * k] = samples[2 * (n - 1 - k)];
            result[(2 * k) + 1] = samples[(2 * (n - 1 - k)) + 1];
        }
        return result;
    }

    //The loop starting at sample `shift`
    private static double[] Rotate(double[] samples, int shift)
    {
        if (shift == 0) { return samples; }
        int n = samples.Length / 2;
        var result = new double[samples.Length];
        for (int k = 0; k < n; k++)
        {
            int from = (k + shift) % n;
            result[2 * k] = samples[2 * from];
            result[(2 * k) + 1] = samples[(2 * from) + 1];
        }
        return result;
    }

    //The loop starting at sample `shift` and running back to it (n + 1 samples)
    private static double[] OpenUp(double[] loop, int shift)
    {
        int n = loop.Length / 2;
        var result = new double[2 * (n + 1)];
        for (int k = 0; k <= n; k++)
        {
            int from = (k + shift) % n;
            result[2 * k] = loop[2 * from];
            result[(2 * k) + 1] = loop[(2 * from) + 1];
        }
        return result;
    }

    private static int BestShift(double[] a, double[] b)
    {
        int n = a.Length / 2;
        int bestShift = 0;
        double best = Double.MaxValue;
        for (int s = 0; s < n; s++)
        {
            double d = SquaredDistance(a, b, s);
            if (d < best) { best = d; bestShift = s; }
        }
        return bestShift;
    }

    //Sum over k of |a[k] - b[(k + shift) mod n]|^2, in k order
    private static double SquaredDistance(double[] a, double[] b, int shift)
    {
        int n = a.Length / 2;
        double sum = 0.0;
        for (int k = 0; k < n; k++)
        {
            int j = (k + shift) % n;
            double dx = a[2 * k] - b[2 * j];
            double dy = a[(2 * k) + 1] - b[(2 * j) + 1];
            sum += (dx * dx) + (dy * dy);
        }
        return sum;
    }
}
