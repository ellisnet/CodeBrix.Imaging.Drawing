using System;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// The arithmetic behind every path measurement, in double precision and in a fixed order
/// of operations, so the two packages (which compile this same source) agree to the last
/// bit. Only +, -, *, /, Math.Sqrt, Math.Floor and Math.Ceiling are used for measuring,
/// trimming, dashing, flattening and morphing - all IEEE-exact - so results also agree
/// across machines. (Arc construction uses Math.Sin/Math.Cos/Math.Atan2 once, when a path is
/// built; measurement never calls them.)
/// </summary>
internal static class PathMath
{
    //The chord tolerance, calibrated units, of the tables that measure lengths and find points
    internal const double MeasureTolerance = 0.01;

    //Upper bound on the pieces one curve is divided into (keeps degenerate input bounded)
    internal const int MaxSubdivisions = 4096;

    //A segment as doubles: p0, c1, c2, p3 (a line keeps its controls on its ends; a
    //  quadratic keeps its one control in c1)
    internal readonly struct Seg
    {
        public readonly PathSegmentKind Kind;
        public readonly double X0, Y0, X1, Y1, X2, Y2, X3, Y3;

        public Seg(PathSegmentKind kind, double x0, double y0, double x1, double y1,
            double x2, double y2, double x3, double y3)
        {
            Kind = kind;
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; X3 = x3; Y3 = y3;
        }

        public static Seg From(PathSegment s)
            => new Seg(s.Kind, s.Start.X, s.Start.Y, s.Control1.X, s.Control1.Y,
                s.Control2.X, s.Control2.Y, s.End.X, s.End.Y);

        public static Seg Line(double x0, double y0, double x3, double y3)
            => new Seg(PathSegmentKind.Line, x0, y0, x0, y0, x3, y3, x3, y3);

        public PathSegment ToSegment()
            => new PathSegment(Kind, P(X0, Y0), P(X1, Y1), P(X2, Y2), P(X3, Y3));
    }

    internal static PointF P(double x, double y) => new PointF((float)x, (float)y);

    //Wang's formula: the number of uniform parameter steps that keeps the chords of a Bezier
    //  within the tolerance of the curve
    internal static int Subdivisions(in Seg s, double tolerance)
    {
        if (s.Kind == PathSegmentKind.Line) { return 1; }
        double m;
        double factor;
        if (s.Kind == PathSegmentKind.Quadratic)
        {
            m = Length(s.X0 - (2.0 * s.X1) + s.X3, s.Y0 - (2.0 * s.Y1) + s.Y3);
            factor = 0.25;
        }
        else
        {
            double m1 = Length(s.X0 - (2.0 * s.X1) + s.X2, s.Y0 - (2.0 * s.Y1) + s.Y2);
            double m2 = Length(s.X1 - (2.0 * s.X2) + s.X3, s.Y1 - (2.0 * s.Y2) + s.Y3);
            m = Math.Max(m1, m2);
            factor = 0.75;
        }
        double n = Math.Ceiling(Math.Sqrt(factor * m / tolerance));
        if (Double.IsNaN(n) || n < 1.0) { return 1; }
        return n > MaxSubdivisions ? MaxSubdivisions : (int)n;
    }

    internal static double Length(double dx, double dy) => Math.Sqrt((dx * dx) + (dy * dy));

    //The point at parameter t
    internal static void Evaluate(in Seg s, double t, out double x, out double y)
    {
        switch (s.Kind)
        {
            case PathSegmentKind.Line:
                x = s.X0 + ((s.X3 - s.X0) * t);
                y = s.Y0 + ((s.Y3 - s.Y0) * t);
                return;
            case PathSegmentKind.Quadratic:
            {
                double u = 1.0 - t;
                double a = u * u, b = 2.0 * u * t, c = t * t;
                x = (a * s.X0) + (b * s.X1) + (c * s.X3);
                y = (a * s.Y0) + (b * s.Y1) + (c * s.Y3);
                return;
            }
            default:
            {
                double u = 1.0 - t;
                double a = u * u * u, b = 3.0 * u * u * t, c = 3.0 * u * t * t, d = t * t * t;
                x = (a * s.X0) + (b * s.X1) + (c * s.X2) + (d * s.X3);
                y = (a * s.Y0) + (b * s.Y1) + (c * s.Y2) + (d * s.Y3);
                return;
            }
        }
    }

    //The derivative at parameter t (not normalised)
    internal static void Derivative(in Seg s, double t, out double dx, out double dy)
    {
        switch (s.Kind)
        {
            case PathSegmentKind.Line:
                dx = s.X3 - s.X0;
                dy = s.Y3 - s.Y0;
                return;
            case PathSegmentKind.Quadratic:
            {
                double u = 1.0 - t;
                dx = 2.0 * ((u * (s.X1 - s.X0)) + (t * (s.X3 - s.X1)));
                dy = 2.0 * ((u * (s.Y1 - s.Y0)) + (t * (s.Y3 - s.Y1)));
                return;
            }
            default:
            {
                double u = 1.0 - t;
                double a = u * u, b = 2.0 * u * t, c = t * t;
                dx = 3.0 * ((a * (s.X1 - s.X0)) + (b * (s.X2 - s.X1)) + (c * (s.X3 - s.X2)));
                dy = 3.0 * ((a * (s.Y1 - s.Y0)) + (b * (s.Y2 - s.Y1)) + (c * (s.Y3 - s.Y2)));
                return;
            }
        }
    }

    //The part of a segment between parameters t0 and t1 (0 <= t0 <= t1 <= 1), by de Casteljau
    internal static Seg Sub(in Seg s, double t0, double t1)
    {
        if (t0 <= 0.0 && t1 >= 1.0) { return s; }
        if (s.Kind == PathSegmentKind.Line)
        {
            Evaluate(s, t0, out double ax, out double ay);
            Evaluate(s, t1, out double bx, out double by);
            return Seg.Line(ax, ay, bx, by);
        }

        //Keep [0, t1], then the part of that from t0 / t1
        Seg left = t1 >= 1.0 ? s : SplitLeft(s, t1);
        if (t0 <= 0.0) { return left; }
        double u = t1 <= 0.0 ? 0.0 : t0 / t1;
        return SplitRight(left, u);
    }

    private static Seg SplitLeft(in Seg s, double t)
    {
        if (s.Kind == PathSegmentKind.Quadratic)
        {
            double ax = Mix(s.X0, s.X1, t), ay = Mix(s.Y0, s.Y1, t);
            double bx = Mix(s.X1, s.X3, t), by = Mix(s.Y1, s.Y3, t);
            double cx = Mix(ax, bx, t), cy = Mix(ay, by, t);
            return new Seg(PathSegmentKind.Quadratic, s.X0, s.Y0, ax, ay, ax, ay, cx, cy);
        }
        double abx = Mix(s.X0, s.X1, t), aby = Mix(s.Y0, s.Y1, t);
        double bcx = Mix(s.X1, s.X2, t), bcy = Mix(s.Y1, s.Y2, t);
        double cdx = Mix(s.X2, s.X3, t), cdy = Mix(s.Y2, s.Y3, t);
        double abcx = Mix(abx, bcx, t), abcy = Mix(aby, bcy, t);
        double bcdx = Mix(bcx, cdx, t), bcdy = Mix(bcy, cdy, t);
        double px = Mix(abcx, bcdx, t), py = Mix(abcy, bcdy, t);
        return new Seg(PathSegmentKind.Cubic, s.X0, s.Y0, abx, aby, abcx, abcy, px, py);
    }

    private static Seg SplitRight(in Seg s, double t)
    {
        if (s.Kind == PathSegmentKind.Quadratic)
        {
            double ax = Mix(s.X0, s.X1, t), ay = Mix(s.Y0, s.Y1, t);
            double bx = Mix(s.X1, s.X3, t), by = Mix(s.Y1, s.Y3, t);
            double cx = Mix(ax, bx, t), cy = Mix(ay, by, t);
            return new Seg(PathSegmentKind.Quadratic, cx, cy, bx, by, bx, by, s.X3, s.Y3);
        }
        double abx = Mix(s.X0, s.X1, t), aby = Mix(s.Y0, s.Y1, t);
        double bcx = Mix(s.X1, s.X2, t), bcy = Mix(s.Y1, s.Y2, t);
        double cdx = Mix(s.X2, s.X3, t), cdy = Mix(s.Y2, s.Y3, t);
        double abcx = Mix(abx, bcx, t), abcy = Mix(aby, bcy, t);
        double bcdx = Mix(bcx, cdx, t), bcdy = Mix(bcy, cdy, t);
        double px = Mix(abcx, bcdx, t), py = Mix(abcy, bcdy, t);
        return new Seg(PathSegmentKind.Cubic, px, py, bcdx, bcdy, cdx, cdy, s.X3, s.Y3);
    }

    internal static double Mix(double a, double b, double t) => a + ((b - a) * t);

    //Extends a box (minX, minY, maxX, maxY) by a segment's exact extent: its end points and
    //  the points where its derivative is zero on either axis
    internal static void ExtendBounds(in Seg s, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        Include(s.X3, s.Y3, ref minX, ref minY, ref maxX, ref maxY);
        if (s.Kind == PathSegmentKind.Line) { return; }

        Span<double> roots = stackalloc double[4];
        int count = 0;
        if (s.Kind == PathSegmentKind.Quadratic)
        {
            count = QuadraticExtremum(s.X0, s.X1, s.X3, roots, count);
            count = QuadraticExtremum(s.Y0, s.Y1, s.Y3, roots, count);
        }
        else
        {
            count = CubicExtrema(s.X0, s.X1, s.X2, s.X3, roots, count);
            count = CubicExtrema(s.Y0, s.Y1, s.Y2, s.Y3, roots, count);
        }
        for (int i = 0; i < count; i++)
        {
            Evaluate(s, roots[i], out double x, out double y);
            Include(x, y, ref minX, ref minY, ref maxX, ref maxY);
        }
    }

    internal static void Include(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        if (x < minX) { minX = x; }
        if (y < minY) { minY = y; }
        if (x > maxX) { maxX = x; }
        if (y > maxY) { maxY = y; }
    }

    private static int QuadraticExtremum(double p0, double p1, double p2, Span<double> roots, int count)
    {
        double denominator = p0 - (2.0 * p1) + p2;
        if (denominator == 0.0) { return count; }
        double t = (p0 - p1) / denominator;
        if (t > 0.0 && t < 1.0) { roots[count++] = t; }
        return count;
    }

    private static int CubicExtrema(double p0, double p1, double p2, double p3, Span<double> roots, int count)
    {
        //B'(t) / 3 = a t^2 + b t + c
        double d0 = p1 - p0, d1 = p2 - p1, d2 = p3 - p2;
        double a = d0 - (2.0 * d1) + d2;
        double b = 2.0 * (d1 - d0);
        double c = d0;
        if (Math.Abs(a) < 1e-12)
        {
            if (b != 0.0)
            {
                double t = -c / b;
                if (t > 0.0 && t < 1.0) { roots[count++] = t; }
            }
            return count;
        }
        double discriminant = (b * b) - (4.0 * a * c);
        if (discriminant < 0.0) { return count; }
        double root = Math.Sqrt(discriminant);
        double t1 = (-b + root) / (2.0 * a);
        double t2 = (-b - root) / (2.0 * a);
        if (t1 > 0.0 && t1 < 1.0) { roots[count++] = t1; }
        if (t2 > 0.0 && t2 < 1.0) { roots[count++] = t2; }
        return count;
    }
}
