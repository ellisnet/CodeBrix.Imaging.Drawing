using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;

namespace AutumnAviana.Leaves;

/// <summary>The kinds of leaf in the animation, each with its own silhouette, veins and colours.</summary>
internal enum LeafKind
{
    /// <summary>A five-lobed maple leaf with toothed lobes.</summary>
    Maple,

    /// <summary>An elongated oak leaf with deep, rounded lobes.</summary>
    Oak,

    /// <summary>An oval, finely toothed elm leaf with a lopsided base.</summary>
    Elm,

    /// <summary>A small, triangular-oval birch leaf with a doubly toothed edge.</summary>
    Birch,
}

/// <summary>
/// One leaf silhouette at unit length, before it is curled, sized and coloured: the blade outline (a closed
/// polygon), the vein lines as polylines (petiole first), and the point the veins radiate from. Coordinates: the tip
/// points up (-y), the leaf spans y = -0.5 (tip) .. +0.5 (end of the stalk), centred on x = 0.
/// </summary>
internal sealed class LeafTemplate
{
    /// <summary>Creates a template.</summary>
    /// <param name="kind">The kind of leaf.</param>
    /// <param name="outline">The blade outline, one closed contour.</param>
    /// <param name="veins">The veins (the stalk first), each an open polyline.</param>
    /// <param name="hub">The point the main veins leave from (the top of the stalk).</param>
    /// <param name="linework">The rim and the veins as filled areas (closed rings), or <c>null</c> before they are made.</param>
    public LeafTemplate(LeafKind kind, IReadOnlyList<PointF> outline, IReadOnlyList<PointF[]> veins, PointF hub, IReadOnlyList<PointF[]> linework = null)
    {
        Kind = kind;
        Outline = outline;
        Veins = veins;
        Hub = hub;
        Linework = linework;
    }

    /// <summary>The kind of leaf.</summary>
    public LeafKind Kind { get; }

    /// <summary>The blade outline, one closed contour of points.</summary>
    public IReadOnlyList<PointF> Outline { get; }

    /// <summary>The veins (the stalk first), each an open polyline.</summary>
    public IReadOnlyList<PointF[]> Veins { get; }

    /// <summary>The top of the stalk, where the main veins start.</summary>
    public PointF Hub { get; }

    /// <summary>
    /// The rim and the veins, already turned from strokes into filled areas (closed rings, non-zero fill), so a leaf
    /// is drawn as two fills and never needs stroking per frame.
    /// </summary>
    public IReadOnlyList<PointF[]> Linework { get; }
}

/// <summary>
/// Builds the leaf silhouettes once: several variants of each kind, made by uniting simple lobe shapes with the
/// library's path combining (<see cref="PathGeometry.CombineAll" />), so the sinuses between the lobes come out
/// as real notches in one clean outline.
/// </summary>
internal static class LeafTemplates
{
    /// <summary>Variants built per kind.</summary>
    public const int VariantsPerKind = 6;

    /// <summary>The leaf length the rim and vein widths are worked out for, pixels.</summary>
    public const float NominalLength = 42f;

    private static readonly Dictionary<LeafKind, LeafTemplate[]> Built = Build();

    /// <summary>A template of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="variant">Any number; it is wrapped to the variants built.</param>
    /// <returns>The template.</returns>
    public static LeafTemplate Get(LeafKind kind, int variant) => Built[kind][((variant % VariantsPerKind) + VariantsPerKind) % VariantsPerKind];

    private static Dictionary<LeafKind, LeafTemplate[]> Build()
    {
        Dictionary<LeafKind, LeafTemplate[]> all = new Dictionary<LeafKind, LeafTemplate[]>();
        foreach (LeafKind kind in Enum.GetValues<LeafKind>())
        {
            LeafTemplate[] variants = new LeafTemplate[VariantsPerKind];
            for (int v = 0; v < VariantsPerKind; v++)
            {
                DrawingRandom random = DrawingRandom.ForKey(4207, ((int)kind * 100) + v);
                variants[v] = kind switch
                {
                    LeafKind.Maple => Maple(random),
                    LeafKind.Oak => Oak(random),
                    LeafKind.Elm => Elm(random),
                    _ => Birch(random),
                };
            }

            all[kind] = variants;
        }

        return all;
    }

    private static float R(DrawingRandom random, double low, double high) => (float)random.Uniform(low, high);

    /// <summary>
    /// A maple: five lobes radiating from the top of the stalk (a long middle lobe, two broad upper side lobes and
    /// two small lower ones), each a pointed blade with a tooth or two on its flanks, united with a small hub.
    /// </summary>
    private static LeafTemplate Maple(DrawingRandom random)
    {
        PointF hub = new PointF(0, 0.2f);
        (float Angle, float Length, float Width)[] lobes =
        {
            (-90 + R(random, -4, 4), R(random, 0.66f, 0.72f), R(random, 0.30f, 0.36f)),
            (-90 - R(random, 48, 56), R(random, 0.56f, 0.62f), R(random, 0.28f, 0.33f)),
            (-90 + R(random, 48, 56), R(random, 0.56f, 0.62f), R(random, 0.28f, 0.33f)),
            (-90 - R(random, 104, 116), R(random, 0.30f, 0.36f), R(random, 0.20f, 0.24f)),
            (-90 + R(random, 104, 116), R(random, 0.30f, 0.36f), R(random, 0.20f, 0.24f)),
        };

        List<PathGeometry> parts = new List<PathGeometry> { PathGeometry.Circle(hub, 0.17f) };
        List<PointF[]> veins = new List<PointF[]> { new[] { new PointF(0, 0.5f), new PointF(0.01f, 0.32f), hub } };
        foreach ((float angle, float length, float width) in lobes)
        {
            parts.Add(PathGeometry.FromPoints(Lobe(hub, angle, length, width, teeth: 2, random), closed: true));
            veins.Add(Ray(hub, angle, length * 0.9f));
        }

        PathGeometry blade = PathGeometry.CombineAll(parts, PathBooleanOperation.Union, 0.002f);
        return Normalize(new LeafTemplate(LeafKind.Maple, LargestContour(blade), veins, hub));
    }

    /// <summary>
    /// An oak: a narrow core along the midrib with finger-like rounded lobes (ellipses angled towards the tip) set
    /// alternately along both sides, and a rounded tip lobe; deep sinuses between them.
    /// </summary>
    private static LeafTemplate Oak(DrawingRandom random)
    {
        float top = -0.5f;
        float bottom = 0.3f;
        List<PathGeometry> parts = new List<PathGeometry>
        {
            PathGeometry.Ellipse(new PointF(0, (top + bottom) / 2), 0.06f, (bottom - top) / 2),
        };

        List<PointF[]> veins = new List<PointF[]> { new[] { new PointF(0, 0.5f), new PointF(0, bottom), new PointF(0, top + 0.04f) } };
        const int pairs = 4;
        for (int i = 0; i < pairs; i++)
        {
            float s = (i + 0.4f) / pairs;                       // 0 near the base .. 1 near the tip
            float y = bottom - 0.04f - ((bottom - top - 0.2f) * s);
            float reach = 0.1f + (0.13f * MathF.Sin(MathF.PI * MathF.Pow(s, 0.75f)));
            for (int side = -1; side <= 1; side += 2)
            {
                float yy = y + (side * 0.035f) + R(random, -0.015f, 0.015f);
                float length = reach * R(random, 0.9f, 1.12f);
                float angle = side * R(random, 22, 34);         // lobes lean towards the tip
                float a = angle * MathF.PI / 180f;
                PointF tip = new PointF(side * length * MathF.Cos(a), yy - (length * MathF.Abs(MathF.Sin(a))));
                PointF centre = new PointF(tip.X * 0.55f, yy + ((tip.Y - yy) * 0.55f));
                float half = length * 0.55f;
                PathGeometry lobe = PathGeometry.Ellipse(centre, half, half * R(random, 0.5f, 0.62f))
                    .Transformed(System.Numerics.Matrix3x2.CreateRotation(-side * MathF.Abs(a), new System.Numerics.Vector2(centre.X, centre.Y)));
                parts.Add(lobe);
                veins.Add(new[] { new PointF(0, yy + 0.02f), new PointF(tip.X * 0.82f, yy + ((tip.Y - yy) * 0.82f)) });
            }
        }

        parts.Add(PathGeometry.Ellipse(new PointF(0, top + 0.08f), 0.075f, 0.09f));
        PathGeometry blade = PathGeometry.CombineAll(parts, PathBooleanOperation.Union, 0.002f);
        return Normalize(new LeafTemplate(LeafKind.Oak, LargestContour(blade), veins, new PointF(0, bottom)));
    }

    /// <summary>An elm: an oval blade with a pointed tip, a lopsided base and a finely toothed edge; pinnate veins.</summary>
    private static LeafTemplate Elm(DrawingRandom random)
    {
        float width = R(random, 0.27f, 0.32f);
        float lopsided = R(random, 0.05f, 0.12f);
        return Pinnate(LeafKind.Elm, random, width, widest: 0.42f, teeth: 15, toothDepth: 0.018f, lopsided, veinPairs: 7);
    }

    /// <summary>A birch: a triangular oval (widest low down) with a long point and a doubly toothed edge.</summary>
    private static LeafTemplate Birch(DrawingRandom random)
    {
        float width = R(random, 0.30f, 0.36f);
        return Pinnate(LeafKind.Birch, random, width, widest: 0.3f, teeth: 11, toothDepth: 0.026f, lopsided: 0.02f, veinPairs: 6);
    }

    private static LeafTemplate Pinnate(LeafKind kind, DrawingRandom random, float width, float widest, int teeth,
        float toothDepth, float lopsided, int veinPairs)
    {
        const float baseY = 0.3f;
        const float tipY = -0.5f;
        const int steps = 60;
        List<PointF> right = new List<PointF>();
        List<PointF> left = new List<PointF>();
        for (int i = 0; i <= steps; i++)
        {
            float s = i / (float)steps;                         // 0 at the base, 1 at the tip
            float y = baseY + ((tipY - baseY) * s);
            float half = width * Profile(s, widest);
            float saw = toothDepth * Saw(s * teeth) * MathF.Min(1, s * 6) * MathF.Min(1, (1 - s) * 8);
            if (kind == LeafKind.Birch) saw += 0.4f * toothDepth * Saw(s * teeth * 2.5f) * MathF.Min(1, (1 - s) * 8);
            float lean = lopsided * MathF.Max(0, 1 - (s * 4));
            right.Add(new PointF(half + saw + (lean * 0.3f), y + (lean * 0.15f)));
            left.Add(new PointF(-(half + saw) * (1 - (lean * 0.6f)), y - (lean * 0.25f)));
        }

        List<PointF> outline = new List<PointF>(right);
        for (int i = left.Count - 2; i >= 1; i--) outline.Add(left[i]);

        List<PointF[]> veins = new List<PointF[]> { new[] { new PointF(0, 0.5f), new PointF(0, baseY), new PointF(0, tipY + 0.06f) } };
        for (int i = 0; i < veinPairs; i++)
        {
            float s = 0.1f + (0.75f * i / (veinPairs - 1f));
            float y = baseY + ((tipY - baseY) * s);
            float reach = width * Profile(Math.Min(1, s + 0.1f), widest) * 0.82f;
            float rise = 0.11f + (0.04f * R(random, -1, 1));
            veins.Add(new[] { new PointF(0, y), new PointF(reach, y - rise) });
            veins.Add(new[] { new PointF(0, y - 0.015f), new PointF(-reach, y - rise - 0.015f) });
        }

        return Normalize(new LeafTemplate(kind, outline, veins, new PointF(0, baseY)));
    }

    /// <summary>The half-width profile of a pinnate blade: 0 at both ends, 1 at <paramref name="widest" />.</summary>
    private static float Profile(float s, float widest)
    {
        float u = s < widest ? s / widest * 0.5f : 0.5f + ((s - widest) / (1 - widest) * 0.5f);
        float shape = MathF.Max(0, MathF.Sin(MathF.PI * u));
        return s < widest ? MathF.Pow(shape, 0.6f) : MathF.Pow(shape, 1.25f);
    }

    /// <summary>A saw-tooth wave in 0..1 that rises slowly and drops quickly (leaf teeth point towards the tip).</summary>
    private static float Saw(float x)
    {
        float f = x - MathF.Floor(x);
        return f < 0.75f ? f / 0.75f : (1 - f) / 0.25f;
    }

    /// <summary>One maple lobe: a pointed blade from the hub along an angle, with teeth on its flanks.</summary>
    private static PointF[] Lobe(PointF hub, float angleDegrees, float length, float width, int teeth, DrawingRandom random)
    {
        float a = angleDegrees * MathF.PI / 180f;
        PointF along = new PointF(MathF.Cos(a), MathF.Sin(a));
        PointF across = new PointF(-along.Y, along.X);
        List<PointF> right = new List<PointF>();
        List<PointF> left = new List<PointF>();
        const int steps = 24;
        for (int i = 0; i <= steps; i++)
        {
            float s = i / (float)steps;
            float half = width * 0.5f * MathF.Pow(MathF.Sin(MathF.PI * MathF.Min(1, s * 0.62f + 0.04f)), 1.1f) * MathF.Pow(1 - s, 0.55f);
            float tooth = 0;
            for (int k = 0; k < teeth; k++)
            {
                float at = 0.48f + (0.2f * k) + R(random, -0.02f, 0.02f);
                float d = (s - at) / 0.07f;
                if (d > -1 && d < 1) tooth = MathF.Max(tooth, width * 0.22f * (1 - MathF.Abs(d)) * (d < 0 ? 1 : 0.5f));
            }

            float h = half + tooth;
            PointF p = new PointF(hub.X + (along.X * length * s), hub.Y + (along.Y * length * s));
            right.Add(new PointF(p.X + (across.X * h), p.Y + (across.Y * h)));
            left.Add(new PointF(p.X - (across.X * h), p.Y - (across.Y * h)));
        }

        List<PointF> points = new List<PointF>(right);
        for (int i = left.Count - 2; i >= 1; i--) points.Add(left[i]);
        return points.ToArray();
    }

    private static PointF[] Ray(PointF from, float angleDegrees, float length)
    {
        float a = angleDegrees * MathF.PI / 180f;
        PointF mid = new PointF(from.X + (MathF.Cos(a) * length * 0.5f), from.Y + (MathF.Sin(a) * length * 0.5f));
        PointF end = new PointF(from.X + (MathF.Cos(a) * length), from.Y + (MathF.Sin(a) * length));
        return new[] { from, mid, end };
    }

    private static IReadOnlyList<PointF> LargestContour(PathGeometry geometry)
    {
        IReadOnlyList<PathPolyline> lines = geometry.Flatten(0.002f);
        PathPolyline best = lines[0];
        foreach (PathPolyline line in lines)
        {
            if (line.Count > best.Count) best = line;
        }

        return best.GetPoints();
    }

    /// <summary>Scales a template so its blade and stalk span exactly y = -0.5 .. +0.5.</summary>
    private static LeafTemplate Normalize(LeafTemplate template)
    {
        float minY = float.MaxValue;
        foreach (PointF p in template.Outline) minY = MathF.Min(minY, p.Y);
        const float maxY = 0.5f;                            // the end of the stalk
        float scale = 1f / (maxY - minY);
        float shift = -0.5f - (minY * scale);
        PointF Map(PointF p) => new PointF(p.X * scale, (p.Y * scale) + shift);
        List<PointF> outline = new List<PointF>();
        foreach (PointF p in template.Outline) outline.Add(Map(p));
        List<PointF[]> veins = new List<PointF[]>();
        foreach (PointF[] vein in template.Veins) veins.Add(Array.ConvertAll(vein, Map));
        return new LeafTemplate(template.Kind, outline, veins, Map(template.Hub), Linework(outline, veins));
    }

    /// <summary>
    /// The rim and the veins as filled areas: each stroked once with the library's stroke-to-outline, and the two
    /// united. Worked at 100 times the unit size (finer than the combining grid needs), for a leaf of
    /// <see cref="NominalLength" /> pixels: the rim is 1.1 px wide, the veins 1.2 px.
    /// </summary>
    private static IReadOnlyList<PointF[]> Linework(List<PointF> outline, List<PointF[]> veins)
    {
        const float scale = 100f;
        float unit = scale / NominalLength;
        PointF Up(PointF p) => new PointF(p.X * scale, p.Y * scale);
        PathGeometry rim = PathGeometry.FromPoints(outline.ConvertAll(Up), closed: true)
            .ToStrokeOutline(new PathStrokeStyle(1.1f * unit, PathStrokeCap.Round, PathStrokeJoin.Round), 0.05f);
        PathGeometryBuilder lines = new PathGeometryBuilder();
        foreach (PointF[] vein in veins)
        {
            lines.MoveTo(Up(vein[0]));
            for (int i = 1; i < vein.Length; i++) lines.LineTo(Up(vein[i]));
        }

        PathGeometry ribs = lines.Build().ToStrokeOutline(new PathStrokeStyle(1.2f * unit, PathStrokeCap.Round, PathStrokeJoin.Round), 0.05f);
        List<PointF[]> rings = new List<PointF[]>();
        foreach (PathPolyline ring in rim.Union(ribs, 0.05f).Flatten(0.05f))
        {
            rings.Add(Array.ConvertAll(ring.GetPoints(), p => new PointF(p.X / scale, p.Y / scale)));
        }

        return rings;
    }
}
