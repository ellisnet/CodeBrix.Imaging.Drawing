using System;
using System.Collections.Generic;
using System.Linq;
using AutumnAviana.Animation;
using AutumnAviana.Leaves;
using CodeBrix.Imaging.Drawing.Formations;
using CodeBrix.Imaging.Drawing.Planes;

namespace AutumnAviana.Piles;

/// <summary>One leaf of the piles (or one stray on the ground): how it looks, where it rests and how it gets there.</summary>
internal sealed class SettledLeaf
{
    /// <summary>How it looks.</summary>
    public LeafLook Look { get; init; }

    /// <summary>Its resting pose.</summary>
    public FormationPose Rest { get; init; }

    /// <summary>How deep inside its heap it rests (its distance from the letter's edge); 0 for a stray on the ground.</summary>
    public float Depth { get; init; }

    /// <summary>When it comes to rest (it exists on the piles from then on).</summary>
    public double LandTime { get; init; }

    /// <summary>
    /// Its fall, or <c>null</c> for a leaf that arrives tucked under another one (it appears beneath the falling
    /// leaf it came with, at the moment that leaf lands - see <see cref="PilePlanner" />).
    /// </summary>
    public IArrivalPath Fall { get; init; }
}

/// <summary>
/// Plans the piles - which leaves settle where, and when - from the library's formation tools:
/// <list type="bullet">
///   <item>SPOTS (<see cref="FormationTargets" /> with a <see cref="HeapArrivalOrder" />): each letter's area is
///   covered by resting spots, and the spots are filled the way a heap grows - bottom up, always resting on what is
///   already there, rounded and lumpy on top. All the letters grow together by height, so early on there are only
///   low heaps at their feet (leaves gathering) and the word emerges only as they rise.</item>
///   <item>POSES: every spot holds one leaf at a random angle, tilted (narrowed) by a random amount, so the heaps
///   show leaves lying every which way, edges sticking out past the letter's outline like a real heap's ragged
///   edge.</item>
///   <item>SCHEDULE (<see cref="ArrivalSchedule" />): landings start lazily, are thickest in the middle and taper
///   away to the last leaf, which settles exactly at the end of the "fall" cue. To keep the air from becoming a
///   blizzard, about one falling leaf in three brings a second leaf with it: that leaf appears tucked under its
///   companion's landing spot as the companion lands. A few dozen strays fall to the ground between and beside the
///   letters (some that missed).</item>
///   <item>FALLS (<see cref="SwayingFallArrival" />, carried by <see cref="Wind" />): each falling leaf's path is worked
///   backwards from its resting pose and landing time.</item>
/// </list>
/// </summary>
internal static class PilePlanner
{
    private const long Seed = 20251112;
    private const int StrayCount = 44;

    /// <summary>Every settled leaf, in drawing (landing) order.</summary>
    public static readonly IReadOnlyList<SettledLeaf> Leaves = Plan();

    private sealed class Spot
    {
        public FormationTarget Target;
        public int Letter;
        public bool Taken;
    }

    private static IReadOnlyList<SettledLeaf> Plan()
    {
        List<Spot> spots = new List<Spot>();
        for (int i = 0; i < PileLetters.Letters.Count; i++)
        {
            FormationTargetOptions options = new FormationTargetOptions
            {
                Spacing = 16.5f,
                Jitter = 0.33f,
                EdgeClearance = 6f,
                Seed = Seed + 10 + i,
                Order = new HeapArrivalOrder(Stage.PileGround),
            };

            foreach (FormationTarget target in FormationTargets.Fill(PileLetters.Letters[i].Region, options))
            {
                spots.Add(new Spot { Target = target, Letter = i });
            }
        }

        // All letters together, by their climb (the letters share one height, so keys compare across letters).
        spots.Sort((a, b) => a.Target.Key.CompareTo(b.Target.Key));

        // Group the spots: a leader that falls, and up to two tucked leaves beside it (level or lower) that come
        // with it and appear beneath it as it lands.
        DrawingRandom pairing = new DrawingRandom(Seed + 1);
        List<(Spot Leader, List<Spot> Tucked)> events = new List<(Spot, List<Spot>)>();
        for (int i = 0; i < spots.Count; i++)
        {
            Spot leader = spots[i];
            if (leader.Taken) continue;
            leader.Taken = true;
            List<Spot> tucked = new List<Spot>();
            double draw = pairing.NextDouble();
            int wanted = draw < 0.52 ? 2 : draw < 0.9 ? 1 : 0;
            for (int n = 0; n < wanted; n++)
            {
                Spot best = null;
                float bestDistance = 36f * 36f;
                for (int j = Math.Max(0, i - 80); j < Math.Min(spots.Count, i + 40); j++)
                {
                    Spot s = spots[j];
                    if (s.Taken || s.Letter != leader.Letter || s.Target.Key > leader.Target.Key + 30) continue;
                    float dx = s.Target.Position.X - leader.Target.Position.X;
                    float dy = s.Target.Position.Y - leader.Target.Position.Y;
                    float d = (dx * dx) + (dy * dy);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = s;
                    }
                }

                if (best == null) break;
                best.Taken = true;
                tucked.Add(best);
            }

            events.Add((leader, tucked));
        }

        // Landing times for every falling leaf (pile leaders and strays together).
        int total = events.Count + StrayCount;
        ArrivalRateProfile rate = new ArrivalRateProfile { RampUntil = 0.34, TaperFrom = 0.58, StartRate = 0.1 };
        double[] times = new ArrivalSchedule(Timeline.Fall.StartTime + 5.2, Timeline.Fall.EndTime, rate).GetTimes(total);
        DrawingRandom strayPick = new DrawingRandom(Seed + 2);
        HashSet<int> strays = new HashSet<int>();
        while (strays.Count < StrayCount)
        {
            // Strays come throughout, a little more often early on (before the heaps take shape); never last.
            double u = Math.Pow(strayPick.NextDouble(), 1.35);
            strays.Add(Math.Clamp((int)(u * (total - 2)), 0, total - 2));
        }

        List<SettledLeaf> leaves = new List<SettledLeaf>();
        int nextEvent = 0;
        int strayNumber = 0;
        for (int k = 0; k < total; k++)
        {
            DrawingRandom random = DrawingRandom.ForKey(Seed, k);
            if (strays.Contains(k))
            {
                leaves.Add(Stray(random, times[k], strayNumber++));
                continue;
            }

            (Spot leader, List<Spot> tucked) = events[nextEvent++];
            SettledLeaf falling = PileLeaf(random, leader, times[k], falls: true);
            for (int n = 0; n < tucked.Count; n++)
            {
                leaves.Add(PileLeaf(DrawingRandom.ForKey(Seed + 3 + n, k), tucked[n], falling.LandTime, falls: false));
            }

            leaves.Add(falling);
        }

        // Landing order is drawing order (a guarded early leaf may have moved later); the sort is stable, so tucked
        // leaves stay just before the leaf they came with.
        return leaves.OrderBy(leaf => leaf.LandTime).ToList();
    }

    /// <summary>
    /// A fall to a resting pose; if it would have to leave the top edge before the "fall" cue starts (the screen
    /// is white until then), it lands correspondingly later instead.
    /// </summary>
    private static SwayingFallArrival FallTo(DrawingRandom random, FormationPose rest, double landTime, float length)
    {
        long key = random.NextInt(int.MaxValue);
        SwayingFallArrival fall = new SwayingFallArrival(DrawingRandom.ForKey(Seed + 7, key), rest, landTime, -40 - length, Wind.Offset);
        for (int i = 0; i < 4 && fall.StartTime < Timeline.Fall.StartTime; i++)
        {
            landTime += Timeline.Fall.StartTime - fall.StartTime + 0.02;
            fall = new SwayingFallArrival(DrawingRandom.ForKey(Seed + 7, key), rest, landTime, -40 - length, Wind.Offset);
        }

        return fall;
    }

    /// <summary>
    /// A leaf of a pile, posed the way leaves lie in a heap seen from the side: along the heap's surface near its
    /// edge, and inside it mostly lying flat (so seen nearly edge-on, in layers) with some at every angle. Lower
    /// leaves are a little darker - the bottom of a heap is in its own shade.
    /// </summary>
    private static SettledLeaf PileLeaf(DrawingRandom random, Spot spot, double landTime, bool falls)
    {
        FormationTarget target = spot.Target;
        FormationArea region = PileLetters.Letters[spot.Letter].Region;
        float x = target.Position.X;
        float y = target.Position.Y;
        float height = Stage.PileGround - y;
        float heightFraction = Math.Clamp(height / Stage.PileLetterHeight, 0, 1);
        float shade = (float)(random.Uniform(0.9, 1.06) * (0.8 + (0.2 * MathF.Pow(heightFraction, 0.6f))));
        LeafLook look = LeafLook.Create(random, 30, 51, shade);

        float angle;
        float tilt;
        double kind = random.NextDouble();
        if (target.Clearance < 13 && kind < 0.75)
        {
            // On the surface: lying along it (the leaf's length follows the edge), tilted towards the viewer.
            float nx = region.DistanceToEdge(x + 2, y) - region.DistanceToEdge(x - 2, y);
            float ny = region.DistanceToEdge(x, y + 2) - region.DistanceToEdge(x, y - 2);
            float along = MathF.Atan2(nx, -ny) * 180f / MathF.PI;      // the edge's direction
            angle = along + 90 + (float)random.Uniform(-28, 28) + (random.NextDouble() < 0.5 ? 180 : 0);
            tilt = (float)random.Uniform(0.38, 0.85);
        }
        else if (kind < 0.58)
        {
            // Lying flat inside the heap: seen nearly edge-on, length across.
            angle = (random.NextDouble() < 0.5 ? 90 : -90) + (float)random.Uniform(-32, 32);
            tilt = (float)random.Uniform(0.26, 0.6);
        }
        else
        {
            angle = (float)random.Uniform(0, 360);
            tilt = (float)random.Uniform(0.45, 1.0);
        }

        FormationPose rest = new FormationPose(x, y, angle, tilt, (float)random.Uniform(0.82, 1.0));
        SwayingFallArrival fall = falls ? FallTo(random, rest, landTime, look.Length) : null;
        return new SettledLeaf
        {
            Look = look,
            Rest = rest,
            Depth = target.Clearance,
            LandTime = fall?.ArrivalTime ?? landTime,
            Fall = fall,
        };
    }

    /// <summary>A leaf that lands on the ground, lying nearly flat (seen edge-on), near a letter's foot or between letters.</summary>
    private static SettledLeaf Stray(DrawingRandom random, double landTime, int index)
    {
        LeafLook look = LeafLook.Create(random, 30, 50);
        IReadOnlyList<PileLetter> letters = PileLetters.Letters;
        float x;
        if (random.NextDouble() < 0.7)
        {
            PileLetter letter = letters[index % letters.Count];
            float foot = letter.Feet[random.NextInt(letter.Feet.Count)];
            x = foot + ((float)random.Uniform(55, 120) * (random.NextDouble() < 0.5 ? -1 : 1));
        }
        else
        {
            float left = letters[0].Region.Bounds.Left;
            float right = letters[letters.Count - 1].Region.Bounds.Right;
            x = (float)random.Uniform(left - 160, right + 160);
        }

        // Lying on the ground, mostly flat (edge-on), some a little curled up; the nearer ones sit lower (the ground
        // is seen slightly from above).
        float flat = (float)random.Uniform(0.22, 0.62);
        float angle = (float)((random.NextDouble() < 0.5 ? 90 : -90) + random.Uniform(-24, 24));
        float lift = look.Length * 0.3f * flat;
        float nearer = (float)random.Uniform(-4, 16);
        FormationPose rest = new FormationPose(x, Stage.PileGround - lift + nearer, angle, flat, (float)random.Uniform(0.85, 1.0));
        SwayingFallArrival fall = FallTo(random, rest, landTime, look.Length);
        return new SettledLeaf
        {
            Look = look,
            Rest = rest,
            LandTime = fall.ArrivalTime,
            Fall = fall,
        };
    }
}
