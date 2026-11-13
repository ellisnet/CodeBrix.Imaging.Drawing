using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.PixelFormats;

namespace AutumnAviana.Raccoons;

/// <summary>How one raccoon looks: its size and coat.</summary>
/// <param name="Scale">Its size (1 = about 290 units nose to rump, 135 tall at the hips).</param>
/// <param name="Fur">The main grey-brown of its coat.</param>
/// <param name="Warmth">How brown (positive) or cool grey (negative) its coat leans.</param>
/// <param name="Pale">How far its coat is washed towards a pale buff (0 = not at all).</param>
/// <param name="Bands">The darkness of its tail rings and mask (1 = near black; less = softer, browner).</param>
/// <param name="Seed">The seed of its fur (tufts and strands), so each coat is its own.</param>
internal sealed record RaccoonLook(float Scale, Color Fur, float Warmth, float Pale = 0, float Bands = 1, long Seed = 1);

/// <summary>A limb's pose: where its end goes and how its paw is turned.</summary>
/// <param name="Target">Where the ankle or wrist goes, in the raccoon's own units (origin on the ground under its
/// middle, +x forward, +y down).</param>
/// <param name="PawAngle">The paw's direction, degrees (0 = flat, pointing forward).</param>
/// <param name="Planted">Whether the paw is meant to stay put (on the ground, or on a letter) - where it must
/// reach its target exactly; a swinging paw may hang short of a target out of reach.</param>
internal readonly record struct LimbPose(PointF Target, float PawAngle, bool Planted = true);

/// <summary>
/// A side-view raccoon facing right, built once from paths and gradient fills as nested library groups, and posed
/// each frame by setting the groups' transforms:
/// <list type="bullet">
///   <item>the ROOT group places it (translation, size);</item>
///   <item>the BODY group (the hunched torso with the dark back and pale belly, the ringed tail and the head)
///   pitches about the hip and bobs;</item>
///   <item>the TORSO bends along the spine (see <see cref="Bend" />): arched, its ends drop and the hips and
///   shoulders draw together; stretched, the back flattens and they reach apart. It is an animated shape that
///   redraws the torso bent by <see cref="FlexAt" />; the head and tail sit on mounts that follow the bent spine,
///   and the legs hang from the bent hip and shoulder;</item>
///   <item>the HEAD group turns about the neck; inside it the small rounded ears, the black bandit mask with the
///   white brow above it, the pale pointed snout with its dark nose and the dark stripe down its bridge, the white
///   cheek ruff, the eyes (each a group of its own, squashed to blink) and whiskers;</item>
///   <item>the TAIL is three nested segments, ringed dark and light with a dark tip, each turning about its root
///   so a sway travels down it;</item>
///   <item>each LEG is three nested groups - upper bone, lower bone, paw - turned by a two-bone solve so the paw
///   lands exactly on its target; the far legs are drawn behind the body in a deeper shade, the near legs in
///   front.</item>
/// </list>
/// The coat is FUR (see <see cref="Fur" />): the torso, haunches, shoulders, head, cheek ruff and tail have soft
/// tufted edges cut into their outlines - a darker underfur showing between the tufts of the coat over it - and
/// short strands of lighter and darker hair; the tail's rings have tufted edges too. The fur is part of each
/// part's geometry, made once, so it moves with the part and never shimmers; and it is cut INTO the old smooth
/// outlines, so no part is any bigger than before.
/// The body group, head group, eyes and tail segments are exposed so tracks can be bound to them.
/// </summary>
internal sealed class RaccoonRig
{
    /// <summary>The hip joint, in the raccoon's units.</summary>
    public static readonly PointF Hip = new PointF(-56, -80);

    /// <summary>The shoulder joint.</summary>
    public static readonly PointF Shoulder = new PointF(46, -70);

    /// <summary>The neck (the head turns about it).</summary>
    public static readonly PointF Neck = new PointF(80, -96);

    /// <summary>The root of the tail.</summary>
    public static readonly PointF TailRoot = new PointF(-104, -104);

    /// <summary>The thigh's length.</summary>
    public const float Thigh = 44;

    /// <summary>The shin's length (knee to ankle).</summary>
    public const float Shin = 42;

    /// <summary>The upper arm's length.</summary>
    public const float UpperArm = 39;

    /// <summary>The forearm's length (elbow to wrist).</summary>
    public const float Forearm = 39;

    /// <summary>The height of an ankle above the ground when the hind paw is flat.</summary>
    public const float AnkleHeight = 9;

    /// <summary>The height of a wrist above the ground when the front paw is flat.</summary>
    public const float WristHeight = 7;

    private const float TailSegment = 52;
    private const float FaceScale = 1.2f;

    // The spine's bend (see Bend): the line it bends along, its middle, and how far the ends drop at a full arch.
    private const float SpineY = -142;
    private const float SpineMiddle = -12;
    private const float SpineHalf = 100;
    private const float SpineDrop = 17;

    private readonly Limb nearHind;
    private readonly Limb farHind;
    private readonly Limb nearFront;
    private readonly Limb farFront;
    private readonly DrawingShape shadow;
    private readonly DrawingGroup headMount;
    private readonly DrawingGroup tailMount;
    private readonly List<BendablePart> torsoParts;
    private DrawingGroup torsoShown;
    private float torsoFlex;

    /// <summary>Builds the raccoon.</summary>
    /// <param name="look">Its size and coat.</param>
    public RaccoonRig(RaccoonLook look)
    {
        Look = look;
        Palette palette = new Palette(look);
        long seed = look.Seed * 1000;
        Root = new DrawingGroup();
        shadow = Shadow();
        Root.Add(shadow);

        farHind = new Limb(Thigh, Shin, 46, 24, 17, palette.Shade(palette.Leg, 0.72f), palette.Shade(palette.Paw, 0.8f), HindPaw(),
            palette.Shade(palette.Fur, 0.72f), palette, ThighFur, seed + 10);
        farFront = new Limb(UpperArm, Forearm, 32, 20, 15, palette.Shade(palette.Leg, 0.72f), palette.Shade(palette.Paw, 0.8f), FrontPaw(),
            palette.Shade(palette.Fur, 0.72f), palette, ShoulderFur, seed + 20);
        Root.Add(farHind.Group);
        Root.Add(farFront.Group);

        Body = new DrawingGroup { Transform = new DrawingTransform(new PointF(0, 0), 0, 1, 1, Hip) };
        tailMount = new DrawingGroup();
        TailFlick = new DrawingGroup { Transform = new DrawingTransform(new PointF(0, 0), 0, 1, 1, TailRoot) };
        Tail = BuildTail(palette, seed + 30, out DrawingGroup[] segments);
        TailSegments = segments;
        TailFlick.Add(Tail);
        tailMount.Add(TailFlick);
        Body.Add(tailMount);

        torsoParts = Torso(palette, seed + 40);
        torsoShown = BuildTorso(0);
        Body.Add(new AnimatedShape(TorsoAt));

        headMount = new DrawingGroup();
        Head = BuildHead(palette, seed + 50, out DrawingGroup[] eyes, out DrawingGroup face);
        Face = face;
        Eyes = eyes;
        headMount.Add(Head);
        Body.Add(headMount);
        Root.Add(Body);

        nearHind = new Limb(Thigh, Shin, 66, 26, 18, palette.Leg, palette.Paw, HindPaw(), palette.Fur, palette, ThighFur, seed + 60,
            haunch: new LinearGradientFill(new PointF(0, -34), new PointF(0, 40), palette.Shade(palette.Fur, 1.1f), palette.Shade(palette.Fur, 0.86f)));
        nearFront = new Limb(UpperArm, Forearm, 38, 22, 16, palette.Leg, palette.Paw, FrontPaw(), palette.Fur, palette, ShoulderFur, seed + 70);
        Root.Add(nearHind.Group);
        Root.Add(nearFront.Group);
    }

    /// <summary>How it looks.</summary>
    public RaccoonLook Look { get; }

    /// <summary>The whole raccoon: put this on a layer.</summary>
    public DrawingGroup Root { get; }

    /// <summary>The body (torso, tail, head): pitches about the hip.</summary>
    public DrawingGroup Body { get; }

    /// <summary>The head: turns about the neck.</summary>
    public DrawingGroup Head { get; }

    /// <summary>
    /// Everything of the head, inside <see cref="Head" />: it carries a small nod on top of the head's own turn
    /// (see <see cref="Pose" />), so the head can follow through after the body's steps.
    /// </summary>
    public DrawingGroup Face { get; }

    /// <summary>The eyes, each squashed vertically to blink (anchored at its centre).</summary>
    public IReadOnlyList<DrawingGroup> Eyes { get; }

    /// <summary>The whole tail, inside <see cref="TailFlick" />.</summary>
    public DrawingGroup Tail { get; }

    /// <summary>A group around the tail for a flick on top of its sway (turns about the tail's root).</summary>
    public DrawingGroup TailFlick { get; }

    /// <summary>The tail's three segments, root first (each turns about its own root).</summary>
    public IReadOnlyList<DrawingGroup> TailSegments { get; }

    /// <summary>
    /// The spine's bend at a time (see <see cref="Bend" />): +1 fully arched, -1 fully stretched, 0 at rest. The
    /// torso is redrawn from it whenever the time is set, so it must be a pure function of the time; <c>null</c>
    /// keeps the torso at rest.
    /// </summary>
    public Func<double, float> FlexAt { get; set; }

    /// <summary>
    /// The furthest any PLANTED paw's target has lain beyond its leg's reach since the rig was built, raccoon units.
    /// A planted paw whose target is out of reach is pulled short and would slip; 0 means every footfall was reached
    /// exactly.
    /// </summary>
    public float Overreach => MathF.Max(MathF.Max(nearHind.Overreach, farHind.Overreach), MathF.Max(nearFront.Overreach, farFront.Overreach));

    /// <summary>
    /// Poses the raccoon: where it stands, its body's bob, pitch and bend, and its four paws. Joint positions
    /// follow the body's pitch and bend; each leg is solved so its paw lands on its target.
    /// </summary>
    /// <param name="x">The scene x of the ground point under its middle.</param>
    /// <param name="ground">The scene y of the ground.</param>
    /// <param name="bob">The body's vertical offset (raccoon units, + = down).</param>
    /// <param name="pitch">The body's pitch, degrees (negative = front up).</param>
    /// <param name="flex">The spine's bend (see <see cref="Bend" />); must equal <see cref="FlexAt" /> for this time.</param>
    /// <param name="hindNear">The near hind paw.</param>
    /// <param name="hindFar">The far hind paw.</param>
    /// <param name="frontNear">The near front paw.</param>
    /// <param name="frontFar">The far front paw.</param>
    /// <param name="nod">The head's nod on top of its turn, degrees (+ = nose down).</param>
    public void Pose(float x, float ground, float bob, float pitch, float flex, LimbPose hindNear, LimbPose hindFar, LimbPose frontNear, LimbPose frontFar, float nod)
    {
        float s = Look.Scale;
        Face.Transform = new DrawingTransform(new PointF(0, 0), nod, FaceScale, FaceScale, Neck);
        Root.Transform = new DrawingTransform(new PointF(x, ground), 0, s, s);
        DrawingTransform body = new DrawingTransform(new PointF(0, bob), pitch, 1, 1, Hip);
        Body.Transform = body;
        headMount.Transform = Mount(Neck, flex, 1);
        tailMount.Transform = Mount(TailRoot, flex, 0.5f);

        // The shadow shrinks a little as the body rises off the ground (between bounds).
        float rise = Math.Clamp(-bob / 40f, -0.1f, 0.25f);
        shadow.Transform = new DrawingTransform(new PointF(4, 2), 0, 1 - rise, 0.075f * (1 - rise));

        PointF hip = body.TransformPoint(Bend(Hip, flex));
        PointF shoulder = body.TransformPoint(Bend(Shoulder, flex));
        nearHind.Solve(new PointF(hip.X + 3, hip.Y), hindNear, new PointF(1, 0.15f));
        farHind.Solve(new PointF(hip.X - 5, hip.Y - 2), hindFar, new PointF(1, 0.15f));
        nearFront.Solve(new PointF(shoulder.X + 2, shoulder.Y), frontNear, new PointF(-1, 0.45f));
        farFront.Solve(new PointF(shoulder.X - 6, shoulder.Y - 3), frontFar, new PointF(-1, 0.45f));
    }

    /// <summary>
    /// Where a point of the body (body units) goes when the spine bends: the spine, a line along the top of the
    /// back, curves like a bow about the middle of the back - arched (flex &gt; 0) its ends drop, stretched (flex
    /// &lt; 0) they lift - and every cross-section turns with the spine at its place, so below the spine an arch
    /// draws the hips and shoulders together and a stretch pulls them apart.
    /// </summary>
    /// <param name="p">The point at rest.</param>
    /// <param name="flex">The bend: +1 fully arched, -1 fully stretched.</param>
    /// <returns>The point bent.</returns>
    public static PointF Bend(PointF p, float flex)
    {
        if (flex == 0) return p;
        float u = (p.X - SpineMiddle) / SpineHalf;
        float drop = flex * SpineDrop * u * u;
        float turn = MathF.Atan(2 * flex * SpineDrop * u / SpineHalf);
        float below = p.Y - SpineY;
        return new PointF(p.X - (below * MathF.Sin(turn)), p.Y + drop + (below * (MathF.Cos(turn) - 1)));
    }

    /// <summary>The spine's slope where it passes a point's x when bent, degrees (clockwise positive).</summary>
    /// <param name="x">The x, body units.</param>
    /// <param name="flex">The bend.</param>
    /// <returns>The angle.</returns>
    public static float BendAngle(float x, float flex) => MathF.Atan(2 * flex * SpineDrop * ((x - SpineMiddle) / SpineHalf) / SpineHalf) * 180f / MathF.PI;

    /// <summary>The shoulder's place after a body pitch and bob (raccoon units, the spine at rest).</summary>
    /// <param name="bob">The bob.</param>
    /// <param name="pitch">The pitch, degrees.</param>
    /// <returns>The shoulder.</returns>
    public static PointF ShoulderAt(float bob, float pitch) => new DrawingTransform(new PointF(0, bob), pitch, 1, 1, Hip).TransformPoint(Shoulder);

    /// <summary>
    /// A mount that carries a part (head or tail) with the bent spine: moved with its joint and turned with the
    /// spine there - fully, or partly (the tail is held out against the bend, so it only half follows it).
    /// </summary>
    private static DrawingTransform Mount(PointF joint, float flex, float turn)
    {
        PointF moved = Bend(joint, flex);
        return new DrawingTransform(new PointF(moved.X - joint.X, moved.Y - joint.Y), turn * BendAngle(joint.X, flex), 1, 1, joint);
    }

    /// <summary>The torso at a time: rebuilt only when the bend has changed (the same instance means "unchanged").</summary>
    private DrawingShape TorsoAt(double seconds)
    {
        float flex = FlexAt?.Invoke(seconds) ?? 0f;
        if (flex != torsoFlex)
        {
            torsoFlex = flex;
            torsoShown = BuildTorso(flex);
        }

        return torsoShown;
    }

    private DrawingGroup BuildTorso(float flex)
    {
        DrawingGroup group = new DrawingGroup();
        foreach (BendablePart part in torsoParts) group.Add(part.Build(flex));
        return group;
    }

    /// <summary>A soft shadow on the ground under the raccoon (a radial gradient on a disc, squashed flat).</summary>
    private static DrawingShape Shadow()
    {
        const float r = 150;
        CircleShape disc = new CircleShape(0, 0, r, new RadialGradientFill(new PointF(0, 0), r, new[]
        {
            new ColorStop(0f, Color.FromRgba(60, 44, 34, 92)),
            new ColorStop(0.55f, Color.FromRgba(60, 44, 34, 40)),
            new ColorStop(1f, Color.FromRgba(60, 44, 34, 0)),
        }));
        disc.Transform = new DrawingTransform(new PointF(4, 2), 0, 1, 0.075f);
        return disc;
    }

    /// <summary>
    /// A furry part as layers: the UNDERFUR - the part's own outline cut into tufts, in a deep shade - and over it
    /// the COAT, the same outline drawn in a little and cut into tufts of its own, so the darker underfur shows
    /// between the coat's tufts as a soft, broken rim (where the old outline was a hard line). The underfur is
    /// only a band round the edge, as deep as the coat's notches can reach: the coat covers the rest, and the
    /// raccoons are redrawn every frame, so the band saves filling the whole part twice.
    /// </summary>
    private static List<(PathGeometry Area, Color? Color, ShapeFill Fill)> FurLayers(PathGeometry shape, FurEdge edge, Color underfur, Color? coat, ShapeFill coatFill, long seed, out PathGeometry coatArea)
    {
        PathGeometry under = Fur.Tufted(shape, edge, seed).Subtract(shape.Deflate((edge.Depth * 1.6f) + 2, PathStrokeJoin.Round, 4, 0.2f), 0.2f);
        coatArea = Fur.Tufted(shape.Deflate(edge.Depth * 0.32f, PathStrokeJoin.Round, 4, 0.2f), new FurEdge
        {
            Spacing = edge.Spacing * 0.9f,
            Depth = edge.Depth * 0.8f,
            Flow = edge.Flow,
            Lean = edge.Lean,
            Amount = edge.Amount,
        }, seed + 1);
        return new List<(PathGeometry, Color?, ShapeFill)> { (under, underfur, null), (coatArea, coat, coatFill) };
    }

    /// <summary>
    /// The torso: a hunched bean, highest over the hips (a raccoon's back slopes down to its shoulders), with a
    /// dark back fading through the grey-brown coat to a pale belly, edged with tufts all round (longest along
    /// the belly and the rump), and short strands of hair lying back and down the flank - darker along the back,
    /// lighter over the side.
    /// </summary>
    private static List<BendablePart> Torso(Palette palette, long seed)
    {
        PathGeometryBuilder b = new PathGeometryBuilder();
        b.MoveTo(new PointF(-110, -64));
        b.CubicTo(new PointF(-132, -100), new PointF(-110, -150), new PointF(-58, -154));
        b.CubicTo(new PointF(-14, -158), new PointF(28, -138), new PointF(58, -122));
        b.CubicTo(new PointF(76, -114), new PointF(90, -112), new PointF(98, -102));
        b.CubicTo(new PointF(108, -84), new PointF(102, -58), new PointF(80, -50));

        // The belly's lower edge: a little ragged, like fur.
        PointF from = new PointF(80, -50);
        PointF to = new PointF(-102, -50);
        const int tufts = 9;
        for (int i = 1; i <= tufts; i++)
        {
            float u = i / (float)tufts;
            float x = from.X + ((to.X - from.X) * u);
            float sag = 10 * MathF.Sin(MathF.PI * u);
            float y = from.Y + ((to.Y - from.Y) * u) + sag;
            b.QuadraticTo(new PointF(x + 10, y + 7), new PointF(x, y));
        }

        b.CubicTo(new PointF(-106, -52), new PointF(-110, -58), new PointF(-110, -64));
        b.Close();
        PathGeometry bean = b.Build();

        // The hair lies back towards the tail, and more steeply down the lower flank and belly.
        static PointF Flow(PointF p) => new PointF(-1, 0.3f + Math.Clamp((p.Y + 115) / 45f, 0, 1.6f));
        FurEdge edge = new FurEdge
        {
            Spacing = 10,
            Depth = 5.5f,
            Flow = new PointF(-1, 0.45f),
            Lean = 0.85f,
            Amount = p => p.X > 78 ? 0.45f : 1f,       // the chest, under the cheek ruff
        };

        List<BendablePart> parts = new List<BendablePart>();
        List<(PathGeometry Area, Color? Color, ShapeFill Fill)> layers = FurLayers(bean, edge, palette.Underfur, null, palette.BodyFill, seed, out PathGeometry coat);
        foreach ((PathGeometry area, Color? color, ShapeFill fill) in layers)
        {
            parts.Add(new BendablePart(area, color, fill));
        }

        // Strands: darker along the back and the lower flank, lighter over the side.
        PathGeometry dark = Fur.Strands(coat, 60, 15, 3f, Flow, seed + 2, p => p.Y < -128 ? 1f : p.Y > -72 ? 0.7f : 0.22f);
        PathGeometry light = Fur.Strands(coat, 60, 14, 2.8f, Flow, seed + 3, p => p.Y > -132 && p.Y < -76 ? 1f : 0.2f);
        parts.Add(new BendablePart(dark, palette.DarkStrand, null));
        parts.Add(new BendablePart(light, palette.LightStrand, null));
        return parts;
    }

    /// <summary>
    /// The tail: three segments, ringed - alternating dark and light bands, ending in a dark tip - each turning
    /// about its own root. The silhouette is the old bushy one (overlapping discs, thickest in the middle) with its
    /// edge cut into tufts that lean towards the tip; the rings' edges are tufted too, and each band carries
    /// strands of the other shade, lying along the tail and fanning out from its middle.
    /// </summary>
    private static DrawingGroup BuildTail(Palette palette, long seed, out DrawingGroup[] segments)
    {
        float[] widths = { 38, 50, 56, 54, 46, 26 };            // radius profile along the tail, root to tip
        float total = TailSegment * 3;
        float Radius(float along)
        {
            float f = Math.Clamp(along / total, 0, 1) * (widths.Length - 1);
            int i = Math.Min(widths.Length - 2, (int)f);
            float w = widths[i] + ((widths[i + 1] - widths[i]) * (f - i));
            return w * 0.5f;
        }

        // Dark bands along the tail (start, end): five rings, then the dark tip.
        (float Start, float End)[] rings = { (22, 36), (50, 64), (78, 92), (104, 118), (128, 140), (150, 175) };
        bool InRing(PointF p)
        {
            float along = TailRoot.X - p.X;
            foreach ((float rs, float re) in rings)
            {
                if (along >= rs && along <= re) return true;
            }

            return false;
        }

        segments = new DrawingGroup[3];
        for (int k = 0; k < 3; k++)
        {
            PointF root = new PointF(TailRoot.X - (k * TailSegment), TailRoot.Y);
            segments[k] = new DrawingGroup { Transform = new DrawingTransform(new PointF(0, 0), 0, 1, 1, root) };
        }

        // Tufts lean towards the tip, and shrink where the tail narrows to its end.
        FurEdge edge = new FurEdge
        {
            Spacing = 8.5f,
            Depth = 4.2f,
            Flow = new PointF(-1, 0),
            Lean = 0.9f,
            Amount = p => Math.Clamp(Radius(Math.Min(TailRoot.X - p.X, total)) / 14f, 0.2f, 1f),
        };
        PointF Flow(PointF p) => new PointF(-1, (p.Y - TailRoot.Y) / 34f);

        for (int k = 0; k < 3; k++)
        {
            // The segment further out is added first, so it draws first and this segment covers the joint.
            if (k < 2) segments[k].Add(segments[k + 1]);
            float start = k * TailSegment;
            float end = k == 2 ? total + 14 : start + TailSegment + 8;
            List<PathGeometry> discs = new List<PathGeometry>();
            for (float a = start; a <= end; a += 5)
            {
                float r = Radius(Math.Min(a, total));
                if (a > total) r *= 1 - ((a - total) / 16);
                if (r < 2) continue;
                float wobble = 1.5f * MathF.Sin(a * 0.9f);        // an uneven edge
                discs.Add(PathGeometry.Circle(new PointF(TailRoot.X - a, TailRoot.Y + wobble), r));
            }

            PathGeometry fluff = PathGeometry.CombineAll(discs, PathBooleanOperation.Union, 0.3f);
            DrawingGroup segment = segments[k];
            LinearGradientFill shading = new LinearGradientFill(new PointF(0, TailRoot.Y - 24), new PointF(0, TailRoot.Y + 24),
                palette.TailLight, palette.Shade(palette.TailLight, 0.78f));
            List<(PathGeometry Area, Color? Color, ShapeFill Fill)> layers = FurLayers(fluff, edge, palette.Underfur, null, shading, seed + (k * 10), out PathGeometry coat);
            foreach ((PathGeometry area, Color? color, ShapeFill fill) in layers)
            {
                segment.Add(fill != null ? new PathShape(area, fill) : new PathShape(area, color.Value));
            }

            List<PathGeometry> bands = new List<PathGeometry>();
            for (int r = 0; r < rings.Length; r++)
            {
                (float rs, float re) = rings[r];
                if (re < start || rs > end) continue;
                bands.Add(Band(rs, re, seed + 100 + r));
            }

            PathGeometry ringArea = bands.Count == 0 ? PathGeometry.Empty : PathGeometry.CombineAll(bands, PathBooleanOperation.Union, 0.2f).Intersect(coat, 0.2f);
            if (!ringArea.IsEmpty) segment.Add(new PathShape(ringArea, palette.TailDark));

            // Strands of the other shade over each band: pale hairs in the dark rings, dark ones in the light bands.
            PathGeometry pale = Fur.Strands(coat, 8, 12, 2.6f, Flow, seed + (k * 10) + 3, p => InRing(p) ? 1f : 0f);
            PathGeometry deep = Fur.Strands(coat, 10, 12, 2.6f, Flow, seed + (k * 10) + 4, p => InRing(p) ? 0f : 1f);
            if (!pale.IsEmpty) segment.Add(new PathShape(pale, palette.TailPaleStrand));
            if (!deep.IsEmpty) segment.Add(new PathShape(deep, palette.TailDarkStrand));
        }

        return segments[0];
    }

    /// <summary>
    /// One ring of the tail, from <paramref name="start" /> to <paramref name="end" /> along it: a band across the
    /// tail whose two edges are rows of soft tufts pointing towards the tip (the hair of each band lies over the
    /// next), bowed a little towards the tip so the ring reads as going round the tail.
    /// </summary>
    private static PathGeometry Band(float start, float end, long seed)
    {
        DrawingRandom random = new DrawingRandom(seed);
        const float half = 40;
        float tipX = TailRoot.X - end;
        float rootX = TailRoot.X - start;
        List<PointF> points = new List<PointF>();
        foreach ((float edgeX, bool down) in new[] { (tipX, true), (rootX, false) })
        {
            List<PointF> side = new List<PointF>();
            float y = -half;
            while (y < half)
            {
                float width = 7f + (4f * random.NextSingle());
                float reach = 2f + (3f * random.NextSingle());
                for (int i = 0; i < 8; i++)
                {
                    float u = i / 8f;
                    float yy = Math.Min(half, y + (width * u));
                    float point = 1 - MathF.Pow(MathF.Abs((2 * u) - 1), 1.8f);
                    float bow = 5f * (1 - ((yy / half) * (yy / half)));
                    side.Add(new PointF(edgeX - bow - (reach * point), TailRoot.Y + yy));
                }

                y += width;
            }

            side.Add(new PointF(edgeX, TailRoot.Y + half));
            if (!down) side.Reverse();
            points.AddRange(side);
        }

        return PathGeometry.FromPoints(points, closed: true);
    }

    /// <summary>
    /// The head (drawn around the neck point, facing right): far ear, skull, near ear, white cheek ruff, pale
    /// pointed snout with the dark stripe down its bridge, the black mask across the eyes with the white brow above,
    /// the eyes (blinkable groups), the dark nose, the mouth and whiskers. The back of the skull and the cheek ruff
    /// are tufted.
    /// </summary>
    private static DrawingGroup BuildHead(Palette palette, long seed, out DrawingGroup[] eyes, out DrawingGroup faceGroup)
    {
        PointF P(float x, float y) => new PointF(Neck.X + x, Neck.Y + y);
        DrawingGroup head = new DrawingGroup { Transform = new DrawingTransform(new PointF(0, 0), 0, 1, 1, Neck) };
        DrawingGroup face = new DrawingGroup { Transform = new DrawingTransform(new PointF(0, 0), 0, FaceScale, FaceScale, Neck) };
        head.Add(face);
        faceGroup = face;

        face.Add(Ear(P(10, -34), palette, far: true));

        // The skull: fur tufts round the back and under the jaw; the brow and snout end stay smooth.
        PathGeometry skull = PathGeometry.Ellipse(P(30, -14), 31, 27);
        FurEdge skullEdge = new FurEdge
        {
            Spacing = 6.5f,
            Depth = 4f,
            Flow = new PointF(-1, 0.6f),
            Lean = 0.8f,
            Amount = p => Math.Clamp((P(34, 0).X - p.X) / 14f, 0, 1),
        };
        foreach ((PathGeometry area, Color? color, ShapeFill fill) in FurLayers(skull, skullEdge, palette.Underfur, null,
            new LinearGradientFill(P(0, -42), P(0, 12), palette.Shade(palette.Fur, 0.82f), palette.Shade(palette.Fur, 1.12f)), seed, out _))
        {
            face.Add(fill != null ? new PathShape(area, fill) : new PathShape(area, color.Value));
        }

        face.Add(Ear(P(22, -36), palette, far: false));

        // The cheek ruff: white fur flaring back and down below the mask, ending in soft locks.
        PathGeometryBuilder ruff = new PathGeometryBuilder();
        ruff.MoveTo(P(46, -6));
        ruff.CubicTo(P(40, 2), P(26, 8), P(12, 6));
        ruff.LineTo(P(4, 12));
        ruff.LineTo(P(2, 3));
        ruff.LineTo(P(-6, 6));
        ruff.LineTo(P(-2, -4));
        ruff.CubicTo(P(6, -8), P(26, -10), P(46, -6));
        ruff.Close();
        FurEdge ruffEdge = new FurEdge { Spacing = 5f, Depth = 3.2f, Flow = new PointF(-1, 0.7f), Lean = 0.9f, Amount = p => p.Y > P(0, -6).Y ? 1f : 0.35f };
        foreach ((PathGeometry area, Color? color, ShapeFill fill) in FurLayers(ruff.Build(), ruffEdge, palette.Shade(palette.White, 0.74f), palette.White, null, seed + 5, out _))
        {
            face.Add(new PathShape(area, color.Value));
        }

        // The snout: pale, pointed, a little upturned at the nose.
        PathGeometryBuilder snout = new PathGeometryBuilder();
        snout.MoveTo(P(42, -24));
        snout.CubicTo(P(58, -24), P(76, -14), P(86, -8));
        snout.CubicTo(P(88, -4), P(86, 1), P(80, 2));
        snout.CubicTo(P(68, 6), P(52, 8), P(42, 4));
        snout.Close();
        face.Add(Outlined(snout.Build(), new PathStrokeStyle(1.4f), palette.Shade(palette.White, 0.62f), null,
            new LinearGradientFill(P(0, -24), P(0, 8), palette.White, palette.Shade(palette.White, 0.88f))));

        // The dark stripe down the bridge of the nose, from between the eyes to the nose.
        PathGeometryBuilder bridge = new PathGeometryBuilder();
        bridge.MoveTo(P(36, -30));
        bridge.CubicTo(P(52, -26), P(70, -16), P(84, -9));
        face.Add(Outlined(bridge.Build(), new PathStrokeStyle(4.5f, PathStrokeCap.Round), palette.Shade(palette.Mask, 1.6f)));

        // The white brow above the mask.
        PathGeometryBuilder brow = new PathGeometryBuilder();
        brow.MoveTo(P(8, -24));
        brow.CubicTo(P(18, -34), P(42, -36), P(58, -24));
        brow.CubicTo(P(44, -28), P(22, -27), P(8, -20));
        brow.Close();
        face.Add(new PathShape(brow.Build(), palette.White));

        // The bandit mask: a black band across the eye, widest round it, tapering back over the cheek (where it
        // breaks into short locks) and forward onto the snout.
        PathGeometryBuilder mask = new PathGeometryBuilder();
        mask.MoveTo(P(4, -12));
        mask.CubicTo(P(14, -26), P(40, -30), P(56, -20));
        mask.CubicTo(P(62, -15), P(62, -9), P(56, -7));
        mask.CubicTo(P(44, -2), P(30, -4), P(20, -2));
        mask.CubicTo(P(12, -1), P(4, -4), P(4, -12));
        mask.Close();
        FurEdge maskEdge = new FurEdge { Spacing = 5f, Depth = 2.4f, Flow = new PointF(-1, 0.3f), Lean = 0.8f, Amount = p => Math.Clamp((P(26, 0).X - p.X) / 10f, 0, 1) };
        face.Add(new PathShape(Fur.Tufted(mask.Build(), maskEdge, seed + 7), palette.Mask));

        // The eye: a glossy dark eye with a catch-light, inside the mask; a group so it can be squashed to blink.
        PointF eyeCentre = P(39, -16);
        DrawingGroup eye = new DrawingGroup { Transform = new DrawingTransform(new PointF(0, 0), 0, 1, 1, eyeCentre) };
        eye.Add(new CircleShape(eyeCentre.X, eyeCentre.Y, 5.4f, 1, Color.FromRgb(58, 40, 30), isFilled: true));
        eye.Add(new CircleShape(eyeCentre.X + 0.4f, eyeCentre.Y + 0.4f, 3.6f, 1, Color.FromRgb(8, 6, 6), isFilled: true));
        eye.Add(new CircleShape(eyeCentre.X + 1.6f, eyeCentre.Y - 1.6f, 1.5f, 1, Color.FromRgba(255, 255, 255, 235), isFilled: true));
        face.Add(eye);
        eyes = new[] { eye };

        // The nose, the mouth and the whiskers.
        face.Add(new PathShape(PathGeometry.Ellipse(P(85, -6), 6.5f, 5f), Color.FromRgb(24, 20, 20)));
        face.Add(new CircleShape(P(86.5f, -8).X, P(86.5f, -8).Y, 1.5f, 1, Color.FromRgba(255, 255, 255, 140), isFilled: true));
        PathGeometryBuilder mouth = new PathGeometryBuilder();
        mouth.MoveTo(P(80, 2));
        mouth.QuadraticTo(P(72, 6), P(62, 5));
        face.Add(Outlined(mouth.Build(), new PathStrokeStyle(1.4f, PathStrokeCap.Round), Color.FromRgb(70, 60, 56)));
        PathGeometryBuilder whiskers = new PathGeometryBuilder();
        foreach ((float dx, float dy) in new[] { (26f, -6f), (28f, 2f), (24f, 9f) })
        {
            whiskers.MoveTo(P(68, -2));
            whiskers.QuadraticTo(P(68 + (dx * 0.6f), -2 + (dy * 0.4f)), P(68 + dx, -2 + dy));
        }

        face.Add(Outlined(whiskers.Build(), new PathStrokeStyle(0.9f, PathStrokeCap.Round), Color.FromRgba(236, 232, 226, 200)));
        return head;
    }

    /// <summary>
    /// A shape with an outline, made ready to draw as plain fills: the outline is turned into an area ONCE, with the
    /// library's stroke-to-outline, and filled - the raccoon is redrawn every frame, and a pre-made outline costs
    /// a fill where a stroke would be rebuilt by the stroker each time. Without a fill colour or fill, just the
    /// outline.
    /// </summary>
    private static DrawingElement Outlined(PathGeometry geometry, PathStrokeStyle style, Color? stroke, Color? fillColor = null, ShapeFill fill = null)
    {
        PathShape line = new PathShape(geometry.ToStrokeOutline(style, 0.15f), 0, stroke, isFilled: true);
        if (fillColor == null && fill == null) return line;
        DrawingGroup group = new DrawingGroup();
        group.Add(fill != null ? new PathShape(geometry, fill) : new PathShape(geometry, 0, fillColor, isFilled: true));
        group.Add(line);
        return group;
    }

    /// <summary>A small rounded ear: grey with a pale rim and a dark inside.</summary>
    private static DrawingGroup Ear(PointF baseCentre, Palette palette, bool far)
    {
        PathGeometryBuilder outer = new PathGeometryBuilder();
        outer.MoveTo(new PointF(baseCentre.X - 11, baseCentre.Y + 4));
        outer.CubicTo(new PointF(baseCentre.X - 12, baseCentre.Y - 12), new PointF(baseCentre.X - 4, baseCentre.Y - 20), new PointF(baseCentre.X + 2, baseCentre.Y - 19));
        outer.CubicTo(new PointF(baseCentre.X + 9, baseCentre.Y - 17), new PointF(baseCentre.X + 12, baseCentre.Y - 6), new PointF(baseCentre.X + 11, baseCentre.Y + 4));
        outer.Close();
        PathGeometryBuilder inner = new PathGeometryBuilder();
        inner.MoveTo(new PointF(baseCentre.X - 6, baseCentre.Y + 2));
        inner.CubicTo(new PointF(baseCentre.X - 7, baseCentre.Y - 9), new PointF(baseCentre.X - 2, baseCentre.Y - 14), new PointF(baseCentre.X + 2, baseCentre.Y - 13));
        inner.CubicTo(new PointF(baseCentre.X + 6, baseCentre.Y - 11), new PointF(baseCentre.X + 7, baseCentre.Y - 4), new PointF(baseCentre.X + 6, baseCentre.Y + 2));
        inner.Close();
        float shade = far ? 0.7f : 1f;
        DrawingGroup ear = new DrawingGroup();
        ear.Add(Outlined(outer.Build(), new PathStrokeStyle(1.3f), palette.Outline, palette.Shade(palette.White, 0.9f * shade), null));
        ear.Add(new PathShape(inner.Build(), palette.Shade(palette.Mask, 1.5f * shade)));
        return ear;
    }

    /// <summary>A hind paw: a long, flat, dark sole from the heel forward to four short toes.</summary>
    private static PathGeometry HindPaw()
    {
        PathGeometryBuilder b = new PathGeometryBuilder();
        b.MoveTo(new PointF(-7, -4));
        b.CubicTo(new PointF(-8, 4), new PointF(-4, 9), new PointF(4, 9));
        b.LineTo(new PointF(30, 9));
        b.QuadraticTo(new PointF(36, 9), new PointF(36, 5));
        b.QuadraticTo(new PointF(37, 1), new PointF(32, 0));
        b.QuadraticTo(new PointF(30, -3), new PointF(26, -2));
        b.CubicTo(new PointF(16, -4), new PointF(6, -8), new PointF(-7, -4));
        b.Close();
        return b.Build();
    }

    /// <summary>A front paw: a small dark hand with long fingers spread forward.</summary>
    private static PathGeometry FrontPaw()
    {
        List<PathGeometry> parts = new List<PathGeometry> { PathGeometry.Ellipse(new PointF(5, 3), 9, 6) };
        foreach ((float angle, float length) in new[] { (-28f, 12f), (-10f, 15f), (6f, 15f), (22f, 13f), (40f, 9f) })
        {
            float a = angle * MathF.PI / 180f;
            PointF from = new PointF(9, 3);
            PointF to = new PointF(from.X + (MathF.Cos(a) * length), from.Y + (MathF.Sin(a) * length * 0.55f) + 2);
            parts.Add(PathGeometry.Line(from, to).ToStrokeOutline(new PathStrokeStyle(3.6f, PathStrokeCap.Round)));
        }

        return PathGeometry.CombineAll(parts, PathBooleanOperation.Union, 0.2f);
    }

    /// <summary>The fur of a thigh (in its bone's own coordinates, the bone hanging down +y): tufts all round the haunch, leaning down.</summary>
    private static readonly FurEdge ThighFur = new FurEdge { Spacing = 7.5f, Depth = 5f, Flow = new PointF(-0.3f, 1), Lean = 0.8f, Amount = p => Math.Clamp((34 - p.Y) / 16f, 0, 1) };

    /// <summary>The fur of an upper arm: short tufts round the shoulder only.</summary>
    private static readonly FurEdge ShoulderFur = new FurEdge { Spacing = 6.5f, Depth = 3.5f, Flow = new PointF(0.2f, 1), Lean = 0.7f, Amount = p => Math.Clamp((22 - p.Y) / 14f, 0, 1) };

    /// <summary>The raccoon's colours, from its look.</summary>
    private sealed class Palette
    {
        public Palette(RaccoonLook look)
        {
            Rgba32 f = look.Fur.ToPixel<Rgba32>();
            float w = look.Warmth;
            Color warmed = Color.FromRgb(Clamp(f.R + (10 * w)), f.G, Clamp(f.B - (10 * w)));
            Fur = Mix(warmed, Color.FromRgb(222, 206, 178), look.Pale);
            Back = Shade(Fur, 0.58f);
            Belly = Mix(Fur, Color.FromRgb(226, 220, 208), 0.62f);
            Leg = Shade(Fur, 0.62f);
            Paw = Color.FromRgb(42, 36, 34);
            Color black = Color.FromRgb(26, 23, 22);
            Mask = Mix(Shade(Fur, 0.42f), black, look.Bands);
            White = Color.FromRgb(238, 234, 226);
            TailLight = Mix(Fur, Color.FromRgb(214, 204, 186), 0.38f);
            TailDark = Mix(Shade(Fur, 0.5f), Color.FromRgb(46, 40, 38), look.Bands);
            Outline = Color.FromRgba(48, 40, 36, 150);
            Rgba32 under = Shade(Fur, 0.64f).ToPixel<Rgba32>();
            Underfur = Color.FromRgba(under.R, under.G, under.B, 190);
            Rgba32 light = Mix(Fur, Color.FromRgb(246, 240, 228), 0.45f).ToPixel<Rgba32>();
            LightStrand = Color.FromRgba(light.R, light.G, light.B, 120);
            Rgba32 dark = Shade(Fur, 0.5f).ToPixel<Rgba32>();
            DarkStrand = Color.FromRgba(dark.R, dark.G, dark.B, 105);
            Rgba32 pale = Mix(TailLight, Color.FromRgb(240, 234, 222), 0.3f).ToPixel<Rgba32>();
            TailPaleStrand = Color.FromRgba(pale.R, pale.G, pale.B, 80);
            Rgba32 deep = Mix(TailDark, TailLight, 0.25f).ToPixel<Rgba32>();
            TailDarkStrand = Color.FromRgba(deep.R, deep.G, deep.B, 105);
            BodyFill = new LinearGradientFill(new PointF(0, -156), new PointF(0, -44), new[]
            {
                new ColorStop(0f, Back),
                new ColorStop(0.42f, Fur),
                new ColorStop(0.78f, Mix(Fur, Belly, 0.55f)),
                new ColorStop(1f, Belly),
            });
        }

        public Color Fur { get; }

        public Color Back { get; }

        public Color Belly { get; }

        public Color Leg { get; }

        public Color Paw { get; }

        public Color Mask { get; }

        public Color White { get; }

        public Color TailLight { get; }

        public Color TailDark { get; }

        public Color Outline { get; }

        /// <summary>The underfur showing between the coat's tufts (and as the soft rim of every furry part).</summary>
        public Color Underfur { get; }

        public Color LightStrand { get; }

        public Color DarkStrand { get; }

        public Color TailPaleStrand { get; }

        public Color TailDarkStrand { get; }

        public LinearGradientFill BodyFill { get; }

        public Color Shade(Color c, float factor)
        {
            Rgba32 p = c.ToPixel<Rgba32>();
            return Color.FromRgba(Clamp(p.R * factor), Clamp(p.G * factor), Clamp(p.B * factor), p.A);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            Rgba32 p = a.ToPixel<Rgba32>();
            Rgba32 q = b.ToPixel<Rgba32>();
            return Color.FromRgb(Clamp(p.R + ((q.R - p.R) * t)), Clamp(p.G + ((q.G - p.G) * t)), Clamp(p.B + ((q.B - p.B) * t)));
        }

        private static byte Clamp(float v) => (byte)Math.Clamp(v + 0.5f, 0, 255);
    }

    /// <summary>
    /// A part of the torso that bends with the spine: its area as polygons at rest, refilled bent (every point moved
    /// by <see cref="Bend" />) - the same points in the same order every time, so its tufts and strands stay put on
    /// the body and only flex with it.
    /// </summary>
    private sealed class BendablePart
    {
        private readonly PointF[][] polygons;
        private readonly Color? color;
        private readonly ShapeFill fill;

        public BendablePart(PathGeometry area, Color? color, ShapeFill fill)
        {
            IReadOnlyList<PathPolyline> lines = area.Flatten(0.2f);
            polygons = new PointF[lines.Count][];
            for (int i = 0; i < lines.Count; i++) polygons[i] = lines[i].GetPoints();
            this.color = color;
            this.fill = fill;
        }

        public PathShape Build(float flex)
        {
            PathGeometryBuilder builder = new PathGeometryBuilder();
            PointF[] bent = null;
            foreach (PointF[] polygon in polygons)
            {
                if (bent == null || bent.Length != polygon.Length) bent = new PointF[polygon.Length];
                for (int i = 0; i < polygon.Length; i++) bent[i] = Bend(polygon[i], flex);
                builder.AddPolygon(bent, true);
            }

            PathGeometry geometry = builder.Build();
            return fill != null ? new PathShape(geometry, fill) : new PathShape(geometry, color.Value);
        }
    }

    /// <summary>
    /// One leg: an upper bone group (at the hip or shoulder), a lower bone group inside it (at the knee or elbow)
    /// and a paw group inside that (at the ankle or wrist). Each bone is a tapered, round-ended shape drawn down its
    /// group's +y axis; the paw is drawn forward along +x. The upper bone - the haunch or the shoulder - is furry.
    /// </summary>
    private sealed class Limb
    {
        private readonly float upper;
        private readonly float lower;
        private readonly DrawingGroup lowerGroup;
        private readonly DrawingGroup pawGroup;

        public Limb(float upperLength, float lowerLength, float topWidth, float jointWidth, float endWidth, Color legColor, Color pawColor,
            PathGeometry paw, Color upperColor, Palette palette, FurEdge fur, long seed, ShapeFill haunch = null)
        {
            upper = upperLength;
            lower = lowerLength;
            Group = new DrawingGroup();
            PathGeometry upperBone = Bone(upperLength, topWidth, jointWidth);
            Color outline = Color.FromRgba(40, 34, 30, 140);
            // The underfur of a haunch or shoulder is only a little darker than its coat: it lies over the body, where a
            // dark rim would read as a seam.
            Rgba32 u = palette.Shade(upperColor, 0.78f).ToPixel<Rgba32>();
            List<(PathGeometry Area, Color? Color, ShapeFill Fill)> layers = FurLayers(upperBone, fur, Color.FromRgba(u.R, u.G, u.B, 190),
                haunch != null ? null : upperColor, haunch, seed, out PathGeometry coat);
            foreach ((PathGeometry area, Color? color, ShapeFill fill) in layers)
            {
                Group.Add(fill != null ? new PathShape(area, fill) : new PathShape(area, color.Value));
            }

            if (haunch != null)
            {
                // Strands on the haunch, lying down and back.
                Group.Add(new PathShape(Fur.Strands(coat, 14, 12, 2.6f, p => new PointF(-0.35f, 1), seed + 3, p => p.Y < 30 ? 1f : 0f), palette.LightStrand));
                Group.Add(new PathShape(Fur.Strands(coat, 10, 12, 2.5f, p => new PointF(-0.35f, 1), seed + 4, p => p.Y < 30 ? 1f : 0f), palette.DarkStrand));
            }

            lowerGroup = new DrawingGroup();
            lowerGroup.Add(Outlined(Bone(lowerLength, jointWidth, endWidth), new PathStrokeStyle(1.3f), outline, legColor, null));
            pawGroup = new DrawingGroup();
            pawGroup.Add(Outlined(paw, new PathStrokeStyle(1.2f), Color.FromRgba(20, 16, 16, 160), pawColor, null));
            lowerGroup.Add(pawGroup);
            Group.Add(lowerGroup);
        }

        public DrawingGroup Group { get; }

        /// <summary>The furthest a target has lain beyond this leg's reach (0 = it always reached).</summary>
        public float Overreach { get; private set; }

        public void Solve(PointF root, LimbPose pose, PointF bendTowards)
        {
            float dx = pose.Target.X - root.X;
            float dy = pose.Target.Y - root.Y;
            if (pose.Planted) Overreach = MathF.Max(Overreach, MathF.Sqrt((dx * dx) + (dy * dy)) - (upper + lower));
            LegIk.Solve(root, pose.Target, upper, lower, bendTowards, out float a1, out float a2);
            float r1 = a1 - 90;
            float r2 = a2 - 90;
            Group.Transform = new DrawingTransform(root, r1);
            lowerGroup.Transform = new DrawingTransform(new PointF(0, upper), r2 - r1);
            pawGroup.Transform = new DrawingTransform(new PointF(0, lower), pose.PawAngle - r2);
        }

        /// <summary>A bone: a tapered shape with round ends, from (0, 0) down to (0, length).</summary>
        private static PathGeometry Bone(float length, float topWidth, float endWidth)
        {
            PathGeometry top = PathGeometry.Circle(new PointF(0, 0), topWidth / 2);
            PathGeometry end = PathGeometry.Circle(new PointF(0, length), endWidth / 2);
            PathGeometry between = PathGeometry.FromPoints(new[]
            {
                new PointF(-topWidth / 2, 0), new PointF(topWidth / 2, 0), new PointF(endWidth / 2, length), new PointF(-endWidth / 2, length),
            }, closed: true);
            return PathGeometry.CombineAll(new[] { top, between, end }, PathBooleanOperation.Union, 0.2f);
        }
    }
}
