using System;
using System.Collections.Generic;
using System.IO;
using AutumnAviana.Animation;
using AutumnAviana.Lettering;
using AutumnAviana.Piles;
using AutumnAviana.Raccoons;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Formations;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AutumnAviana.Scene;

/// <summary>
/// The whole animation as one CodeBrix.Imaging.Drawing.NoSkia session, built once; showing a moment is just
/// <c>Session.Time = seconds</c>. Layers, bottom to top:
/// <list type="bullet">
///   <item><b>Ground</b> - soft contact shadows under the piles (static; the layer fades in as the heaps grow).</item>
///   <item><b>Piles</b> - every settled leaf (with the shade leaf behind it inside a heap), each a time-keyed
///   element that exists from its landing time, added in landing order (the library's formation binding does
///   that): playing forward, the layer's cache draws only the leaves that have just landed.</item>
///   <item><b>Line two</b> - the rolling letters of "@ Øasys" and their shadows.</item>
///   <item><b>Raccoons</b> - the three articulated raccoons and their shadows.</item>
///   <item><b>Air</b> - the falling leaves: the two shapes of each (blade and linework) exist only while it falls,
///   and the formation binding poses every leaf in the air from the time alone.</item>
/// </list>
/// The camera (the library camera, its pan bound to a track) moves down during the "pan" cue. Layers with nothing
/// to show are hidden (their visibility bound to on/off tracks), so the live view neither redraws nor composites
/// them.
/// </summary>
internal sealed class AutumnScene : IDisposable
{
    /// <summary>The path of the bundled font.</summary>
    public static readonly string FontPath = Path.Combine(AppContext.BaseDirectory, "assets", "Fonts", "Roboto-Regular.ttf");

    private readonly List<RaccoonActor> actors = new List<RaccoonActor>();

    /// <summary>Builds the scene (at time 0).</summary>
    public AutumnScene()
    {
        Session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = Stage.FrameSize,
            LayerOpacity = 255,
            ActiveStrokeOpacity = 255,
            BackgroundFillColor = Color.White,
            SurfaceClearColor = Color.White,
        });
        Session.Fonts.RegisterFont(FontPath);

        DrawingLayer ground = Session.AddLayer("Ground", Color.Black);
        BuildGround(ground);
        DrawingLayer piles = Session.AddLayer("Piles", Color.Black);
        DrawingLayer lineTwo = Session.AddLayer("Line two", Color.Black);
        Letters = new RollingLetters(Session, lineTwo);
        for (int i = 0; i < RollingLetters.Pairs.Length; i++)
        {
            Letters.BindPair(Session.Animations, RollingLetters.Pairs[i], Timeline.Raccoons[i].Push);
        }

        Letters.AddLetters();

        // Three raccoons, each its own: the first (rolling the last two letters) a cool grey; the second a good deal
        // smaller, a youngster with a warmer, paler, sandy coat and softer, browner rings and mask; the third (with
        // the big "@" and "Ø") the largest - and the one that runs off at the end.
        DrawingLayer raccoons = Session.AddLayer("Raccoons", Color.Black);
        RaccoonLook[] looks =
        {
            new RaccoonLook(0.84f, Color.FromRgb(124, 120, 116), -0.4f, Seed: 1),
            new RaccoonLook(0.66f, Color.FromRgb(138, 122, 104), 0.9f, Pale: 0.3f, Bands: 0.5f, Seed: 2),
            new RaccoonLook(0.92f, Color.FromRgb(128, 120, 112), 0.2f, Seed: 3),
        };
        for (int i = 0; i < looks.Length; i++)
        {
            RaccoonActor actor = new RaccoonActor(looks[i], Timeline.Raccoons[i], Letters.Letters[RollingLetters.Pairs[i][0]], i, gallops: i == looks.Length - 1);
            actors.Add(actor);
            actor.Bind(Session.Animations);
            raccoons.AddShape(actor.Rig.Root);
        }

        // The leaves as a library formation: each settled leaf (with its shade leaf or ground shadow) on the piles
        // layer from its landing on, added in landing order; each falling leaf's two shapes (blade and linework, drawn
        // centred on the origin) on the air layer while it falls, posed from its fall by one binding.
        DrawingLayer air = Session.AddLayer("Air", Color.Black);
        IReadOnlyList<SettledLeaf> leaves = PilePlanner.Leaves;
        List<FormationMember> members = new List<FormationMember>(leaves.Count);
        foreach (SettledLeaf leaf in leaves) members.Add(leaf.Fall != null ? new FormationMember(leaf.Fall) : new FormationMember(leaf.LandTime));
        LeafFormation = Session.Animations.BindFormation(members, piles, air, i => SettledShape(leaves[i]), i => FallingParts(leaves[i]));

        // Layers with nothing to show are hidden: the second line and the raccoons until the camera starts to move
        // (the first letters are already rolling then, but still below and left of the frame), the air once the last
        // leaf has landed. A hidden layer is simply left out, so the picture is the same; the live view just skips
        // those layers entirely.
        Session.Animations.Bind(lineTwo, AnimatedProperty.IsVisible, new BoolTrack { { 0, false }, { Timeline.Pan.StartTime, true } });
        Session.Animations.Bind(raccoons, AnimatedProperty.IsVisible, new BoolTrack { { 0, false }, { Timeline.Pan.StartTime, true } });
        Session.Animations.Bind(air, AnimatedProperty.IsVisible, new BoolTrack { { 0, true }, { Timeline.Fall.EndTime, false } });

        Session.Camera = new DrawingCamera();
        Session.Animations.Bind(Session.Camera, AnimatedProperty.Position, new PointTrack
        {
            { Timeline.Pan.StartTime, new PointF(0, 0) },
            { Timeline.Pan.EndTime, new PointF(0, Stage.PanDistance), EasingKind.SineInOut },
        });

        Show(0);
    }

    /// <summary>The raccoons.</summary>
    public IReadOnlyList<RaccoonActor> Raccoons => actors;

    /// <summary>The rolling letters of the second line.</summary>
    public RollingLetters Letters { get; }

    /// <summary>The drawing session holding the scene.</summary>
    public DrawingSession Session { get; }

    /// <summary>The leaves as the library's formation elements (settled and falling).</summary>
    public FormationElements LeafFormation { get; }

    /// <summary>The number of leaves in the air at a time.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The count.</returns>
    public int AirborneAt(double seconds) => LeafFormation.GetTravellingCount(seconds);

    /// <summary>The number of leaves that fall visibly.</summary>
    public int FallingCount
    {
        get
        {
            int count = 0;
            foreach (FormationMember member in LeafFormation.Members)
            {
                if (member.Path != null) count++;
            }

            return count;
        }
    }

    /// <summary>The most leaves in the air at once (sampled every frame).</summary>
    /// <returns>The count.</returns>
    public int MostAirborne()
    {
        int most = 0;
        for (int f = 0; f < Timeline.FrameCount; f++) most = Math.Max(most, AirborneAt(Timeline.Clock.TimeOfFrame(f)));
        return most;
    }

    /// <summary>Puts the scene at an animation time.</summary>
    /// <param name="seconds">The time, clamped to the animation.</param>
    public void Show(double seconds) => Session.Time = Timeline.Clock.Clamp(seconds);

    /// <summary>Writes the current moment as raw RGBA (straight alpha) at a size.</summary>
    /// <param name="rgba">The destination, width x height x 4 bytes.</param>
    /// <param name="size">The output size.</param>
    /// <param name="includeBackground">With the white background (as shown); <c>false</c>: on transparency.</param>
    public void WriteFrame(Span<byte> rgba, Size size, bool includeBackground = true) => Session.ExportPixels(rgba, size, includeBackground);

    /// <summary>Releases the session.</summary>
    public void Dispose() => Session.Dispose();

    /// <summary>
    /// A leaf at rest. Inside a heap (far enough from its edge), the leaf comes with a "shade leaf" behind it: its
    /// own blade, turned and a little larger, in a deep shaded colour - another leaf further back in the heap. Where
    /// many overlap, the inside of the heap darkens and the gaps between leaves read as depth rather than holes,
    /// while the heap's edge stays bright and ragged. On the ground, the leaf lies over a flat contact shadow.
    /// It exists from the moment the leaf lands (the formation binding sets that).
    /// </summary>
    private static DrawingGroup SettledShape(SettledLeaf leaf)
    {
        float length = leaf.Look.Length;
        DrawingGroup group = new DrawingGroup();
        if (leaf.Depth > 10)
        {
            PathShape shade = leaf.Look.CreateShadeShape();
            FormationPose rest = leaf.Rest;
            float grow = leaf.Depth > 17 ? 1.3f : 1.05f;
            shade.Transform = new DrawingTransform(new PointF(rest.X, rest.Y + (length * 0.1f)), rest.Rotation + 47, Math.Max(0.55f, rest.ScaleX) * grow, grow);
            group.Add(shade);
        }
        else if (leaf.Depth <= 0)
        {
            group.Add(new PathShape(PathGeometry.Ellipse(new PointF(leaf.Rest.X, leaf.Rest.Y + (length * 0.08f)), length * 0.42f, 3.2f),
                Color.FromRgba(80, 50, 30, 46)));
        }

        DrawingGroup shape = leaf.Look.CreateShape();
        shape.Transform = leaf.Rest.ToTransform();
        group.Add(shape);
        return group;
    }

    /// <summary>A falling leaf's two shapes (blade and linework), drawn centred on the origin - posed by the formation.</summary>
    private static DrawingElement[] FallingParts(SettledLeaf leaf)
    {
        DrawingGroup falling = leaf.Look.CreateShape();
        DrawingElement[] parts = falling.GetChildren();
        falling.Clear();
        return parts;
    }

    /// <summary>
    /// Soft contact shadows on the ground under each letter: a radial gradient (dark centre to nothing) on a disc,
    /// squashed flat by its transform. They grow in with the heaps: the layer's opacity is bound to a track.
    /// </summary>
    private void BuildGround(DrawingLayer ground)
    {
        foreach (PileLetter letter in PileLetters.Letters)
        {
            foreach (float foot in letter.Feet)
            {
                float radius = letter.Feet.Count == 1 ? 120 : 92;
                CircleShape disc = new CircleShape(0, 0, radius, new RadialGradientFill(new PointF(0, 0), radius, new[]
                {
                    new ColorStop(0f, Color.FromRgba(90, 52, 30, 70)),
                    new ColorStop(0.6f, Color.FromRgba(90, 52, 30, 26)),
                    new ColorStop(1f, Color.FromRgba(90, 52, 30, 0)),
                }));
                disc.Transform = new DrawingTransform(new PointF(foot, Stage.PileGround + 4), 0, 1, 0.09f);
                ground.AddShape(disc);
            }
        }

        // The shadows grow in with the heaps: the whole layer fades in (a layer's opacity is applied when the layers
        // are composited, so the fade costs no redrawing).
        double fall = Timeline.Fall.Duration;
        Session.Animations.Bind(ground, AnimatedProperty.Opacity, new FloatTrack
        {
            { Timeline.Fall.StartTime + (0.2 * fall), 0f },
            { Timeline.Fall.StartTime + (0.6 * fall), 1f, EasingKind.SineInOut },
        });
    }
}
