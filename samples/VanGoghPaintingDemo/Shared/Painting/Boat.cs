using System;
using System.Collections.Generic;
using CodeBrix.Imaging;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// The Boat of Heaven: its outline in the boat's own units (1 unit is 1000 frame pixels at full size, the
/// waterline is y = 0, up is negative) and where it is at any time.
/// </summary>
internal static class Boat
{
    /// <summary>The hull.</summary>
    public static readonly (double X, double Y)[] Hull = BuildHull();

    /// <summary>The curved horn rising from the stern.</summary>
    public static readonly (double X, double Y)[] Horn = BuildHorn();

    /// <summary>The shrine amidships.</summary>
    public static readonly (double X, double Y)[] Shrine =
    {
        (0.50, -0.13), (0.50, -0.30), (0.56, -0.345), (0.63, -0.355), (0.70, -0.345), (0.76, -0.30), (0.76, -0.13),
    };

    /// <summary>The shrine's glowing doorway.</summary>
    public static readonly (double X, double Y)[] Door =
    {
        (0.585, -0.14), (0.585, -0.27), (0.63, -0.29), (0.675, -0.27), (0.675, -0.14),
    };

    /// <summary>The two torch poles.</summary>
    public static readonly (double X, double Y)[][] Poles =
    {
        new[] { (0.335, -0.12), (0.345, -0.12), (0.345, -0.47), (0.335, -0.47) },
        new[] { (1.015, -0.15), (1.025, -0.15), (1.025, -0.45), (1.015, -0.45) },
    };

    /// <summary>Where the two torch flames sit.</summary>
    public static readonly (double X, double Y)[] Flames = { (0.34, -0.47), (1.02, -0.45) };

    /// <summary>The centre of the shrine's glow.</summary>
    public static readonly (double X, double Y) Glow = (0.63, -0.22);

    /// <summary>Where the boat is at a time.</summary>
    /// <param name="seconds">The animation time.</param>
    /// <returns>The boat's position, size, tilt and visibility.</returns>
    public static BoatState At(double seconds)
    {
        double u = 1 - Math.Pow(1 - Timeline.Approach.LinearProgress(seconds), 1.6); // slows as it nears the quay
        double distance = 0.035 + ((1 - 0.035) * u);
        double waterline = Timeline.Horizon + (distance * (1500 - Timeline.Horizon));
        double scale = (waterline - Timeline.Horizon) / (1380 - Timeline.Horizon);
        double x = 760 + ((330 - 760) * Math.Pow(u, 1.3));
        double bob = Math.Sin(seconds * 0.8) * 5 * scale;
        double tilt = 0.010 * Math.Sin((seconds * 0.8) + 0.7);
        return new BoatState(x, waterline + bob, scale, tilt, Timeline.BoatAppears.Progress(seconds));
    }

    private static (double X, double Y)[] BuildHull()
    {
        List<(double, double)> hull = new List<(double, double)> { (-0.03, -0.06), (-0.02, -0.11) };
        for (int i = 0; i < 30; i++)
        {
            double x = 1.45 * i / 29.0;
            double sheer = (x - 0.72) / 0.72;
            double bow = Math.Clamp((x - 1.25) / 0.2, 0, 1);
            hull.Add((x, -0.10 - (0.06 * sheer * sheer) - (0.12 * bow * bow)));
        }

        hull.AddRange(new[] { (1.52, -0.30), (1.5, -0.2), (1.45, 0.0), (0.0, 0.0) });
        return hull.ToArray();
    }

    private static (double X, double Y)[] BuildHorn()
    {
        (double X, double Y)[] spine =
        {
            (0.00, -0.06), (-0.035, -0.2), (-0.03, -0.34), (0.01, -0.47), (0.08, -0.56), (0.155, -0.585), (0.205, -0.555), (0.215, -0.51),
        };
        int n = spine.Length;
        (double X, double Y)[] horn = new (double, double)[2 * n];
        for (int i = 0; i < n; i++)
        {
            (double X, double Y) before = spine[Math.Max(i - 1, 0)];
            (double X, double Y) after = spine[Math.Min(i + 1, n - 1)];
            double tangentX = after.X - before.X;
            double tangentY = after.Y - before.Y;
            double length = Math.Sqrt((tangentX * tangentX) + (tangentY * tangentY));
            double normalX = -tangentY / length;
            double normalY = tangentX / length;
            double half = (0.075 + ((0.014 - 0.075) * i / (n - 1.0))) / 2;
            horn[i] = (spine[i].X + (normalX * half), spine[i].Y + (normalY * half));
            horn[(2 * n) - 1 - i] = (spine[i].X - (normalX * half), spine[i].Y - (normalY * half));
        }

        return horn;
    }
}

/// <summary>Where the boat is at one moment.</summary>
/// <param name="X">The stern's frame x.</param>
/// <param name="Waterline">The waterline's frame y (with the bob).</param>
/// <param name="Scale">The boat's size: 1 when docked at full size.</param>
/// <param name="Tilt">Its roll, radians.</param>
/// <param name="Visibility">0 while still hidden in the dark, 1 once fully seen.</param>
internal readonly record struct BoatState(double X, double Waterline, double Scale, double Tilt, double Visibility)
{
    /// <summary>A point of the boat, in frame coordinates.</summary>
    /// <param name="point">The point, boat units.</param>
    /// <returns>The frame position.</returns>
    public (double X, double Y) ToFrame((double X, double Y) point)
    {
        double c = Math.Cos(Tilt);
        double s = Math.Sin(Tilt);
        double x = point.X * 1000 * Scale;
        double y = point.Y * 1000 * Scale;
        return (X + (x * c) - (y * s), Waterline + (x * s) + (y * c));
    }

    /// <summary>An outline of the boat, in frame coordinates.</summary>
    /// <param name="outline">The outline, boat units.</param>
    /// <returns>The frame polygon.</returns>
    public PointF[] ToFrame((double X, double Y)[] outline)
    {
        PointF[] polygon = new PointF[outline.Length];
        for (int i = 0; i < outline.Length; i++)
        {
            (double x, double y) = ToFrame(outline[i]);
            polygon[i] = new PointF((float)x, (float)y);
        }

        return polygon;
    }
}
