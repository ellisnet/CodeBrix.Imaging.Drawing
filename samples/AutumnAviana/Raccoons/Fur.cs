using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;

namespace AutumnAviana.Raccoons;

/// <summary>How a furry edge is cut: the size of its tufts, how deep between them, and the way the hair lies.</summary>
internal sealed class FurEdge
{
    /// <summary>The mean width of a tuft along the edge, raccoon units.</summary>
    public float Spacing { get; init; } = 8;

    /// <summary>How deep the notches between tufts are cut into the shape (the tips stay on its outline).</summary>
    public float Depth { get; init; } = 5;

    /// <summary>The way the hair lies: the tufts lean this way along the edge.</summary>
    public PointF Flow { get; init; } = new PointF(-1, 0);

    /// <summary>How strongly the tufts lean with the flow: 0 symmetric, 1 hooked.</summary>
    public float Lean { get; init; } = 0.7f;

    /// <summary>How much of the cut each place gets, 0..1 (<c>null</c>: all of it everywhere).</summary>
    public Func<PointF, float> Amount { get; init; }
}

/// <summary>
/// The raccoons' fur, as plain geometry made once when a raccoon is built: soft TUFTED EDGES (an outline broken
/// into locks of hair that lean the way the hair lies) and STRANDS (short tapered strokes of lighter and darker
/// hair over the coat). Nothing here grows a shape: every tuft's tip lies on the original outline and the notches
/// between tufts are cut INTO it, so a furry part covers no more than the smooth part it replaces. The tufts belong
/// to the geometry of the part they edge, so they move with it and never change from frame to frame.
/// </summary>
internal static class Fur
{
    /// <summary>The spacing of the points along a tufted edge, raccoon units.</summary>
    private const float SampleSpacing = 1.5f;

    /// <summary>
    /// A shape with its outline broken into tufts. Each closed contour is walked along its length and cut into
    /// tufts of seeded, slightly varied widths and depths; each tuft rises from a sharp notch to a soft tip that
    /// leans the way the hair flows (where the edge runs along the flow; tufts on an edge across the flow stand
    /// straight).
    /// </summary>
    /// <param name="shape">The smooth shape.</param>
    /// <param name="edge">How to cut it.</param>
    /// <param name="seed">The seed of the tufts' variation.</param>
    /// <returns>The tufted shape (straight-sided polygons).</returns>
    public static PathGeometry Tufted(PathGeometry shape, FurEdge edge, long seed)
    {
        PathGeometryBuilder builder = new PathGeometryBuilder();
        int contour = 0;
        foreach (PathPolyline line in shape.Flatten(0.2f))
        {
            PointF[] points = line.GetPoints();
            if (points.Length < 3) continue;
            builder.AddPolygon(TuftLoop(points, edge, DrawingRandom.ForKey(seed, contour++)), true);
        }

        return builder.Build();
    }

    /// <summary>
    /// Short tapered strands of hair scattered over a shape (clipped to it), each a slim curved spike from a
    /// rounded root to a fine tip, pointing the way the hair lies there.
    /// </summary>
    /// <param name="area">Where they may lie.</param>
    /// <param name="count">How many to place.</param>
    /// <param name="length">Their mean length.</param>
    /// <param name="width">Their mean width at the root.</param>
    /// <param name="flow">The way the hair lies at a place.</param>
    /// <param name="seed">The seed of their places and shapes.</param>
    /// <param name="density">How likely a strand is at a place, 0..1 (<c>null</c>: evenly everywhere).</param>
    /// <returns>All the strands as one geometry.</returns>
    public static PathGeometry Strands(PathGeometry area, int count, float length, float width, Func<PointF, PointF> flow, long seed,
        Func<PointF, float> density = null)
    {
        RectangleF box = area.GetBounds();
        DrawingRandom random = new DrawingRandom(seed);
        PathGeometryBuilder builder = new PathGeometryBuilder();
        int made = 0;
        for (int tries = 0; made < count && tries < count * 40; tries++)
        {
            PointF root = new PointF(box.Left + (random.NextSingle() * box.Width), box.Top + (random.NextSingle() * box.Height));
            float chance = random.NextSingle();
            if (density != null && chance > density(root)) continue;
            if (!area.Contains(root, 0.5f)) continue;

            PointF f = flow(root);
            float turn = (random.NextSingle() - 0.5f) * 0.5f;
            float angle = MathF.Atan2(f.Y, f.X) + turn;
            float l = length * (0.7f + (0.6f * random.NextSingle()));
            float w = width * (0.75f + (0.5f * random.NextSingle()));
            float bend = (random.NextSingle() - 0.5f) * 0.6f;
            builder.AddPolygon(Strand(root, angle, l, w, bend), true);
            made++;
        }

        return builder.Build().Intersect(area, 0.2f);
    }

    /// <summary>One strand: a slim curved spike from its root along a direction.</summary>
    private static PointF[] Strand(PointF root, float angle, float length, float width, float bend)
    {
        const int steps = 4;
        PointF dir = new PointF(MathF.Cos(angle), MathF.Sin(angle));
        PointF side = new PointF(-dir.Y, dir.X);
        PointF[] points = new PointF[(2 * steps) + 2];
        for (int i = 0; i <= steps; i++)
        {
            float v = i / (float)steps;
            float along = length * v;
            float curve = bend * length * v * v;
            float half = width * 0.5f * MathF.Pow(1 - v, 0.9f) * MathF.Min(1, 0.45f + (v * 4));
            PointF c = new PointF(root.X + (dir.X * along) + (side.X * curve), root.Y + (dir.Y * along) + (side.Y * curve));
            points[i] = new PointF(c.X + (side.X * half), c.Y + (side.Y * half));
            points[(2 * steps) + 1 - i] = new PointF(c.X - (side.X * half), c.Y - (side.Y * half));
        }

        return points;
    }

    /// <summary>One closed contour cut into tufts.</summary>
    private static PointF[] TuftLoop(PointF[] points, FurEdge edge, DrawingRandom random)
    {
        int n = points.Length;
        double[] at = new double[n + 1];
        double area = 0;
        for (int i = 0; i < n; i++)
        {
            PointF a = points[i];
            PointF b = points[(i + 1) % n];
            at[i + 1] = at[i] + Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        float total = (float)at[n];

        // The outward normal of a step along the contour: (ty, -tx) when it runs clockwise on screen (y down).
        float outward = area > 0 ? 1 : -1;

        // The tufts: seeded widths that add up to the whole length, each with its own depth and how short of the
        // outline its tip stops.
        int count = Math.Max(3, (int)MathF.Round(total / edge.Spacing));
        float[] bounds = new float[count + 1];
        float[] depth = new float[count];
        float[] shortfall = new float[count];
        for (int k = 0; k < count; k++)
        {
            bounds[k + 1] = bounds[k] + 0.65f + (0.7f * random.NextSingle());
            depth[k] = 0.6f + (0.7f * random.NextSingle());
            shortfall[k] = 0.3f * random.NextSingle() * random.NextSingle();
        }

        float stretch = total / bounds[count];
        for (int k = 0; k <= count; k++) bounds[k] *= stretch;

        float flowLength = MathF.Sqrt((edge.Flow.X * edge.Flow.X) + (edge.Flow.Y * edge.Flow.Y));
        PointF flow = new PointF(edge.Flow.X / flowLength, edge.Flow.Y / flowLength);
        int samples = Math.Max(count * 5, (int)MathF.Ceiling(total / SampleSpacing));
        List<PointF> result = new List<PointF>(samples);
        int tuft = 0;
        int segment = 0;
        for (int j = 0; j < samples; j++)
        {
            float s = total * j / samples;
            while (tuft < count - 1 && s >= bounds[tuft + 1]) tuft++;
            PointF p = PointAt(points, at, ref segment, s);
            int probe = 0;
            PointF ahead = PointAt(points, at, ref probe, Wrap(s + 1.2f, total));
            probe = 0;
            PointF behind = PointAt(points, at, ref probe, Wrap(s - 1.2f, total));
            float tx = ahead.X - behind.X;
            float ty = ahead.Y - behind.Y;
            float tl = MathF.Max(1e-4f, MathF.Sqrt((tx * tx) + (ty * ty)));
            tx /= tl;
            ty /= tl;
            PointF normal = new PointF(outward * ty, -outward * tx);

            float amount = edge.Amount?.Invoke(p) ?? 1f;
            float cut = edge.Depth * depth[tuft] * amount;
            if (cut <= 0.01f)
            {
                result.Add(p);
                continue;
            }

            float u = (s - bounds[tuft]) / (bounds[tuft + 1] - bounds[tuft]);
            float along = (tx * flow.X) + (ty * flow.Y);
            float tip = 0.5f + (0.3f * edge.Lean * along);
            // Broad soft tops, narrow notches: 1 at the tip, falling slowly near it and steeply into the notches.
            float rise = u < tip ? 1 - MathF.Pow(1 - (u / tip), 2.3f) : 1 - MathF.Pow((u - tip) / (1 - tip), 2.0f);
            float inward = (cut * (1 - rise)) + (cut * shortfall[tuft] * rise);
            float lean = 0.45f * edge.Lean * along * cut * MathF.Sin(MathF.PI * u);
            result.Add(new PointF(p.X - (normal.X * inward) + (tx * lean), p.Y - (normal.Y * inward) + (ty * lean)));
        }

        return result.ToArray();
    }

    private static float Wrap(float s, float total) => s < 0 ? s + total : s >= total ? s - total : s;

    /// <summary>The point at a distance along a closed contour (the segment index is a hint, moved forward as needed).</summary>
    private static PointF PointAt(PointF[] points, double[] at, ref int segment, float s)
    {
        int n = points.Length;
        while (segment < n - 1 && s > at[segment + 1]) segment++;
        PointF a = points[segment];
        PointF b = points[(segment + 1) % n];
        double span = at[segment + 1] - at[segment];
        float f = span <= 0 ? 0 : (float)((s - at[segment]) / span);
        return new PointF(a.X + ((b.X - a.X) * f), a.Y + ((b.Y - a.Y) * f));
    }
}
