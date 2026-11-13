using System;
using System.Collections.Generic;
using System.Numerics;
using AutumnAviana.Animation;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AutumnAviana.Lettering;

/// <summary>One rolling letter of the second line.</summary>
internal sealed class RollingLetter
{
    /// <summary>The character.</summary>
    public string Text { get; init; }

    /// <summary>The letter as drawn (its glyph and, for the outlined letters, the red rim under it), in its final place.</summary>
    public DrawingGroup Piece { get; init; }

    /// <summary>Its hub (the point it rolls about) in its final place.</summary>
    public PointF FinalHub { get; init; }

    /// <summary>The rolling radius: from the hub down to the baseline it rolls on.</summary>
    public float Radius { get; init; }

    /// <summary>The farthest its ink reaches from the hub (the circle it sweeps when turning).</summary>
    public float Reach { get; init; }

    /// <summary>Half its upright ink width.</summary>
    public float HalfWidth { get; init; }

    /// <summary>The straight path its hub rolls along, once bound.</summary>
    public PathGeometry Path { get; set; }

    /// <summary>Its progress along <see cref="Path" /> (a fraction of the length) over time, once bound.</summary>
    public FloatTrack Progress { get; set; }

    /// <summary>Where its hub is at a time.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The hub's position.</returns>
    public PointF HubAt(double seconds) => Path.GetPointAt(Math.Clamp(Progress.Evaluate(seconds), 0f, 1f)).Position;
}

/// <summary>
/// The second line, "@ Øasys", as rolling letters. The line is laid out once with the library's text engine
/// (kerned Roboto) and centred under the piles; each letter is its glyph outline from the layout: the "@" filled
/// solid reddish orange, the others with a yellow-orange-red gradient over a thin red rim (the glyph's area grown by
/// <see cref="RimWidth" />, made once - the moving letters are redrawn every frame, and a pre-made rim is a plain
/// fill, where an outside stroke would be rebuilt each time). Each pair of letters is rolled in by one raccoon
/// (see <see cref="Pairs" />):
/// both roll along straight paths at the baseline, the rear one pushed, the front one rolling just ahead of it, the
/// gap between them closing as they slow to rest upright in their laid-out places, with a small rock at the end.
/// </summary>
internal sealed class RollingLetters
{
    /// <summary>The line.</summary>
    public const string Text = "@ Øasys";

    /// <summary>The solid colour of the "@": a reddish orange.</summary>
    private static readonly Color AtColor = Color.FromRgb(234, 78, 28);

    /// <summary>The colour of the thin rim around the other letters.</summary>
    private static readonly Color RimColor = Color.FromRgb(196, 22, 26);

    /// <summary>The rim's width outside the glyphs, scene units.</summary>
    private const float RimWidth = 2.6f;

    /// <summary>How far a letter rolls past its place before rocking back, scene units.</summary>
    private const float Overshoot = 7f;

    /// <summary>
    /// Which letters each raccoon rolls, by cluster index (rear letter first): raccoon 1 the last two letters
    /// (they travel farthest), raccoon 2 the middle two, raccoon 3 the "@" and the "Ø".
    /// </summary>
    public static readonly int[][] Pairs = { new[] { 5, 6 }, new[] { 3, 4 }, new[] { 0, 2 } };

    private readonly List<RollingLetter> letters = new List<RollingLetter>();

    /// <summary>Lays out the line and puts its letters and their shadows on a layer, in their final places.</summary>
    /// <param name="session">The session (its fonts are used).</param>
    /// <param name="layer">The layer.</param>
    public RollingLetters(DrawingSession session, DrawingLayer layer)
    {
        TextLayoutResult layout = session.LayoutText(Text, Stage.LineTwoSize);
        RectangleF ink = layout.InkBounds ?? layout.ContentBounds;
        float x = (Stage.Width / 2f) - (ink.Left + (ink.Width / 2f));
        float y = Stage.LineTwoBaseline - layout.Lines[0].Baseline;
        Baseline = Stage.LineTwoBaseline;
        XHeight = Stage.LineTwoSize * 0.528f;

        // Fill colours run top to bottom of the letters: yellow, orange, red.
        float top = Baseline - (Stage.LineTwoSize * 0.74f);
        LinearGradientFill fall = new LinearGradientFill(new PointF(0, top), new PointF(0, Baseline + (Stage.LineTwoSize * 0.2f)), new[]
        {
            new ColorStop(0f, Color.FromRgb(252, 204, 52)),
            new ColorStop(0.5f, Color.FromRgb(244, 128, 30)),
            new ColorStop(1f, Color.FromRgb(204, 40, 30)),
        });
        Matrix3x2 place = Matrix3x2.CreateTranslation(x, y);
        for (int i = 0; i < layout.ClusterCount; i++)
        {
            TextCluster cluster = layout.Clusters[i];
            if (!cluster.HasInk)
            {
                letters.Add(null);
                continue;
            }

            RectangleF glyph = cluster.InkBounds ?? cluster.Bounds;
            glyph = new RectangleF(glyph.X + x, glyph.Y + y, glyph.Width, glyph.Height);
            bool lowercase = char.IsLower(cluster.Text[0]);
            float hubY = lowercase ? Baseline - (XHeight / 2f) : glyph.Top + (glyph.Height / 2f);
            float radius = lowercase ? XHeight / 2f : MathF.Max(glyph.Bottom, Baseline) - hubY;
            PointF hub = new PointF(glyph.Left + (glyph.Width / 2f), hubY);
            float reach = 0;
            PointF[] corners =
            {
                new PointF(glyph.Left, glyph.Top), new PointF(glyph.Right, glyph.Top),
                new PointF(glyph.Left, glyph.Bottom), new PointF(glyph.Right, glyph.Bottom),
            };
            foreach (PointF corner in corners)
            {
                reach = MathF.Max(reach, MathF.Sqrt(((corner.X - hub.X) * (corner.X - hub.X)) + ((corner.Y - hub.Y) * (corner.Y - hub.Y))));
            }

            // The glyph as the font shapes it (the layout's own outline), in place; the outlined letters get a thin red
            // rim made once by growing the glyph's area (drawn under the fill, so it shows only outside the glyph).
            PathGeometry outline = cluster.GetOutline().Transformed(place);
            DrawingGroup piece = new DrawingGroup();
            if (cluster.Text == "@")
            {
                piece.Add(new PathShape(outline, AtColor));
            }
            else
            {
                piece.Add(new PathShape(outline.Inflate(RimWidth), RimColor));
                piece.Add(new PathShape(outline, fall));
            }

            letters.Add(new RollingLetter
            {
                Text = cluster.Text,
                Piece = piece,
                FinalHub = hub,
                Radius = radius,
                Reach = reach,
                HalfWidth = glyph.Width / 2f,
            });
        }

        Layer = layer;
    }

    /// <summary>The letters by cluster index (<c>null</c> for the space).</summary>
    public IReadOnlyList<RollingLetter> Letters => letters;

    /// <summary>The baseline the letters stand and roll on.</summary>
    public float Baseline { get; }

    /// <summary>The x-height of the line.</summary>
    public float XHeight { get; }

    /// <summary>The layer the letters are on.</summary>
    public DrawingLayer Layer { get; }

    /// <summary>
    /// Binds one pair's roll. Both letters roll in from off the left edge along the baseline at the same steady
    /// speed, far enough apart that they can never touch however they are turned; then the front letter slows and
    /// comes to rest first, and the rear one - still pushed - rolls up to it and stops in its place at the cue's end.
    /// In that last approach the gap between them is the final gap plus however far the rear letter still has to
    /// roll, and the further it has to roll the more it is still turned, so a turned letter is always well clear.
    /// Each letter slows from the steady speed, overshoots its place by a few units and rocks back upright. Soft
    /// ground shadows travel under both.
    /// </summary>
    /// <param name="animations">The session's animations.</param>
    /// <param name="pair">The pair's cluster indices, rear letter first.</param>
    /// <param name="push">The pushing cue.</param>
    public void BindPair(DrawingAnimations animations, int[] pair, TimelineCue push)
    {
        RollingLetter rear = letters[pair[0]];
        RollingLetter front = letters[pair[1]];
        float finalGap = front.FinalHub.X - rear.FinalHub.X;
        float clear = MathF.Max(finalGap, rear.Reach + front.Reach + 10);

        // The rear letter sets the pace: it starts far enough left that the front one starts off screen too.
        float rearStart = -front.Reach - 60 - clear;
        double rearEnd = push.EndTime;
        float speed = SteadySpeed(rear.FinalHub.X - rearStart, push.StartTime, rearEnd);

        // The front letter keeps the same steady speed and stops a little earlier - just early enough that, while
        // they travel together, they are clear of each other.
        double early = 0.2;
        float frontStart = StartFor(front.FinalHub.X, speed, push.StartTime, rearEnd - early);
        while (frontStart - rearStart < clear && early < 2.5)
        {
            early += 0.05;
            frontStart = StartFor(front.FinalHub.X, speed, push.StartTime, rearEnd - early);
        }

        Bind(animations, rear, rearStart, push.StartTime, rearEnd);
        Bind(animations, front, frontStart, push.StartTime, rearEnd - early);
    }

    private const double Slowing = 1.2;
    private const double Rock = 0.32;

    /// <summary>
    /// The steady speed of a roll of a distance between two times: steady, then slowing to a stop over
    /// <see cref="Slowing" /> seconds (covering half the distance it would have covered at the steady speed), then
    /// the overshoot and rock.
    /// </summary>
    private static float SteadySpeed(float distance, double start, double end)
    {
        double steady = end - Rock - Slowing - start;
        return (float)((distance + Overshoot) / (steady + (Slowing / 2)));
    }

    /// <summary>Where a letter must start to arrive at a place at a time, rolling at a steady speed (see <see cref="SteadySpeed" />).</summary>
    private static float StartFor(float place, float speed, double start, double end)
    {
        double steady = end - Rock - Slowing - start;
        return place + Overshoot - (float)(speed * (steady + (Slowing / 2)));
    }

    private void Bind(DrawingAnimations animations, RollingLetter letter, float startX, double startTime, double endTime)
    {
        float distance = letter.FinalHub.X - startX;
        float pathLength = distance + Overshoot;
        PathGeometry path = PathGeometry.Line(new PointF(startX, letter.FinalHub.Y), new PointF(letter.FinalHub.X + Overshoot, letter.FinalHub.Y));
        float rest = distance / pathLength;

        // Steady, then slowing; the slowing segment starts at the steady speed (QuadOut's initial slope is 2).
        double steady = endTime - Slowing - Rock - startTime;
        float atSlowing = (float)(2 * steady / (Slowing + (2 * steady)));
        FloatTrack progress = new FloatTrack
        {
            { startTime, 0f },
            { startTime + steady, atSlowing },
            { endTime - Rock, 1f, EasingKind.QuadOut },
            { endTime, rest, EasingKind.SineInOut },
        };

        // The roll: the library rolls the letter along its path like a wheel of its radius (its hub, the transform's
        // anchor, rides the path; the turn follows the distance travelled), upright where it comes to rest.
        letter.Path = path;
        letter.Progress = progress;
        letter.Piece.Transform = letter.Piece.Transform.WithAnchor(letter.FinalHub);
        animations.BindRollAlongPath(letter.Piece, path, progress, letter.Radius, rest);

        // A soft shadow on the ground under the letter, travelling with it (the same progress along a path at the baseline).
        float shadowWidth = MathF.Max(letter.HalfWidth, letter.Radius) * 1.15f;
        CircleShape shadow = new CircleShape(0, 0, shadowWidth, new RadialGradientFill(new PointF(0, 0), shadowWidth, new[]
        {
            new ColorStop(0f, Color.FromRgba(70, 46, 30, 64)),
            new ColorStop(0.55f, Color.FromRgba(70, 46, 30, 26)),
            new ColorStop(1f, Color.FromRgba(70, 46, 30, 0)),
        }));
        shadow.Transform = new DrawingTransform(new PointF(0, 0), 0, 1, 0.1f);
        PathGeometry ground = PathGeometry.Line(new PointF(startX, Baseline + 3), new PointF(letter.FinalHub.X + Overshoot, Baseline + 3));
        animations.BindAlongPath(shadow, ground, progress);
        Layer.AddShape(shadow);
    }

    /// <summary>Adds the letters to the layer (after every shadow, so no shadow falls over a letter).</summary>
    public void AddLetters()
    {
        foreach (RollingLetter letter in letters)
        {
            if (letter != null) Layer.AddShape(letter.Piece);
        }
    }
}
