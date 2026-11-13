using System;
using System.Collections.Generic;
using System.Numerics;
using AutumnAviana.Animation;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Formations;
using CodeBrix.Imaging.Drawing.Paths;

namespace AutumnAviana.Piles;

/// <summary>One letter of the word the piles spell: its area (where its leaves settle) and its footprint.</summary>
internal sealed class PileLetter
{
    /// <summary>Creates a letter.</summary>
    /// <param name="character">The letter.</param>
    /// <param name="area">Its area in scene units, standing on <see cref="Stage.PileGround" />.</param>
    /// <param name="feet">The x positions where it meets the ground (one per leg or stem).</param>
    public PileLetter(char character, PathGeometry area, IReadOnlyList<float> feet)
    {
        Character = character;
        Area = area;
        Feet = feet;
        Region = FormationArea.FromGeometry(area, PileLetters.Tolerance);
    }

    /// <summary>The letter.</summary>
    public char Character { get; }

    /// <summary>Its area, scene units.</summary>
    public PathGeometry Area { get; }

    /// <summary>Its area for the formation tools' inside and edge-distance tests (flattened once).</summary>
    public FormationArea Region { get; }

    /// <summary>Where its legs or stems meet the ground, scene x.</summary>
    public IReadOnlyList<float> Feet { get; }
}

/// <summary>
/// The word AVIANA as heavy, hand-built letter areas that heaps of leaves can hold: thick strokes with round
/// caps and joins (so every top is domed like the top of a heap), cut off flat where they run into the ground, a
/// low crossbar and an open counter in each A, and a heap flaring out at the foot of every leg (piles are wider at
/// the bottom). Built with the library's stroke-to-outline and path combining, and spaced so the nearest points of
/// neighbouring letters are a fixed gap apart (optical spacing for the diagonals).
/// </summary>
internal static class PileLetters
{
    /// <summary>The word.</summary>
    public const string Word = "AVIANA";

    /// <summary>The clear gap between neighbouring letters' areas, scene units.</summary>
    private const float Gap = 20f;

    /// <summary>How closely the letters' curves are followed by the area queries, scene units.</summary>
    public const float Tolerance = 0.5f;

    /// <summary>The letters, left to right, in scene units.</summary>
    public static readonly IReadOnlyList<PileLetter> Letters = Build();

    private static IReadOnlyList<PileLetter> Build()
    {
        List<(char Character, PathGeometry Local, float[] Feet)> local = new List<(char, PathGeometry, float[])>();
        foreach (char c in Word)
        {
            local.Add(c switch
            {
                'A' => A(),
                'V' => V(),
                'I' => I(),
                _ => N(),
            });
        }

        // Space the letters by their nearest points (the library's area-to-area distance, the right letter taken
        // moved along), then centre the word.
        float[] offsets = new float[local.Count];
        for (int i = 1; i < local.Count; i++)
        {
            PathGeometry left = local[i - 1].Local.Transformed(Matrix3x2.CreateTranslation(offsets[i - 1], 0));
            PathGeometry right = local[i].Local;
            float lo = offsets[i - 1];
            float hi = offsets[i - 1] + 600;
            for (int k = 0; k < 40; k++)
            {
                float mid = (lo + hi) / 2;
                if (left.Distance(right, new PointF(mid, 0), Tolerance) < Gap) lo = mid;
                else hi = mid;
            }

            offsets[i] = hi;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        for (int i = 0; i < local.Count; i++)
        {
            RectangleF b = local[i].Local.GetBounds();
            minX = MathF.Min(minX, b.Left + offsets[i]);
            maxX = MathF.Max(maxX, b.Right + offsets[i]);
        }

        float shift = (Stage.Width / 2f) - ((minX + maxX) / 2f);
        List<PileLetter> letters = new List<PileLetter>();
        for (int i = 0; i < local.Count; i++)
        {
            float dx = offsets[i] + shift;
            PathGeometry area = local[i].Local.Transformed(Matrix3x2.CreateTranslation(dx, Stage.PileGround));
            letters.Add(new PileLetter(local[i].Character, area, Array.ConvertAll(local[i].Feet, f => f + dx)));
        }

        return letters;
    }

    private const float H = Stage.PileLetterHeight;

    private static PathGeometry Poly(params float[] xy)
    {
        PointF[] points = new PointF[xy.Length / 2];
        for (int i = 0; i < points.Length; i++) points[i] = new PointF(xy[2 * i], xy[(2 * i) + 1]);
        return PathGeometry.FromPoints(points, closed: true);
    }

    /// <summary>A low heap at the foot of a leg: the top half of an ellipse standing on the ground.</summary>
    private static PathGeometry Heap(float centerX, float halfWidth, float height)
    {
        List<PointF> points = new List<PointF>();
        const int steps = 20;
        for (int i = 0; i <= steps; i++)
        {
            float a = MathF.PI * i / steps;
            points.Add(new PointF(centerX - (MathF.Cos(a) * halfWidth), -MathF.Sin(a) * height));
        }

        return PathGeometry.FromPoints(points, closed: true);
    }

    private static PathGeometry Union(params PathGeometry[] parts) => PathGeometry.CombineAll(parts, PathBooleanOperation.Union);

    /// <summary>A thick stroke along a polyline with round caps and joins - the domed top of a heap.</summary>
    private static PathGeometry Stroke(float width, params float[] xy)
    {
        PointF[] points = new PointF[xy.Length / 2];
        for (int i = 0; i < points.Length; i++) points[i] = new PointF(xy[2 * i], xy[(2 * i) + 1]);
        return PathGeometry.FromPoints(points, closed: false)
            .ToStrokeOutline(new PathStrokeStyle(width, PathStrokeCap.Round, PathStrokeJoin.Round));
    }

    /// <summary>Unites parts and cuts them off flat at the ground (y = 0): a heap stands on the ground.</summary>
    private static PathGeometry Grounded(params PathGeometry[] parts)
        => Union(parts).Intersect(PathGeometry.Rectangle(new RectangleF(-400, -1000, 1400, 1000)));

    /// <summary>
    /// A: two thick legs (thick round-capped strokes whose feet run into the ground) leaning together to a short,
    /// domed top, a low crossbar, and a counter kept open between them; a heap flares out at each foot.
    /// </summary>
    private static (char, PathGeometry, float[]) A()
    {
        const float w = 270;
        const float leg = 74;
        float topY = -(H - (leg / 2));
        float barY = -(H * 0.2f);
        PathGeometry area = Grounded(
            Stroke(leg, 40, 40, (w / 2) - 26, topY, (w / 2) + 26, topY, w - 40, 40),
            Stroke(38, 58, barY, w - 58, barY),
            Heap(38, (leg / 2) + 24, 46),
            Heap(w - 38, (leg / 2) + 24, 46));
        return ('A', area, new[] { 40f, w - 40f });
    }

    /// <summary>V: two thick arms from domed tops down to a shared base in the ground, with a heap around the base.</summary>
    private static (char, PathGeometry, float[]) V()
    {
        const float w = 262;
        const float arm = 76;
        float topY = -(H - (arm / 2));
        PathGeometry area = Grounded(
            Stroke(arm, arm / 2, topY, w / 2, 26, w - (arm / 2), topY),
            Heap(w / 2, 80, 50));
        return ('V', area, new[] { w / 2 });
    }

    /// <summary>I: one thick column with a domed top, on a heap.</summary>
    private static (char, PathGeometry, float[]) I()
    {
        const float w = 84;
        PathGeometry area = Grounded(Stroke(w, w / 2, 40, w / 2, -(H - (w / 2))), Heap(w / 2, (w / 2) + 26, 46));
        return ('I', area, new[] { w / 2 });
    }

    /// <summary>N: two thick stems with domed tops joined by a thick diagonal from the top left to the bottom right.</summary>
    private static (char, PathGeometry, float[]) N()
    {
        const float w = 262;
        const float stem = 74;
        float topY = -(H - (stem / 2));
        PathGeometry area = Grounded(
            Stroke(stem, stem / 2, 40, stem / 2, topY, w - (stem / 2), 40),
            Stroke(stem, w - (stem / 2), 40, w - (stem / 2), topY),
            Heap(stem / 2, (stem / 2) + 24, 46),
            Heap(w - (stem / 2), (stem / 2) + 24, 46));
        return ('N', area, new[] { stem / 2, w - (stem / 2) });
    }
}
