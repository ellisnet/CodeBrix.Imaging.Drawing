using System;
using AutumnAviana.Animation;
using AutumnAviana.Lettering;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AutumnAviana.Raccoons;

/// <summary>
/// One raccoon's performance, driven by its cues - every pose a pure function of the time:
/// <list type="bullet">
///   <item>PUSH: it enters from the left edge behind its letters, reared up a little, both front paws on the back
///   of the rear letter, walking on its hind legs (a two-legged walk whose footfalls never slide); it moves exactly
///   with the letter it pushes, so it stops when the letters come to rest. Once along the way it lifts its head
///   for a look.</item>
///   <item>BEAT: it lets go - the front paws come down to the ground one after the other, the hind feet shuffle
///   into a four-legged stance, the body levels - lifts its head to look at its work and flicks its tail. A
///   raccoon about to run crouches at the end of it.</item>
///   <item>EXIT: it walks off the right edge on all fours (a lateral-sequence walk, footfalls matched to its
///   speed), passing in front of the letters along the ground line, slowing once for a look up on the way - or,
///   for the LAST raccoon, it RUNS off in a bounding lope (see <see cref="Galloping" />).</item>
/// </list>
/// No two steps are quite alike: each gait's strides lengthen and shorten a little (a seeded, smooth variation of
/// its rhythm), each footfall lands a little nearer or further, the bob of each stride is a little higher or lower,
/// and the head and tail follow through by varying amounts - all seeded per raccoon, so the three move as
/// individuals, and all functions of the distance travelled, so the planted feet never slide.
/// Throughout, its tail sways (library loop tracks on each tail segment, a little later down the tail) with a
/// stride-coupled follow-through ADDED on top (additive bindings), it blinks (a looping track squashing the eye),
/// and its head turns (a track keyed to the cues).
/// </summary>
internal sealed class RaccoonActor
{
    private const float PushPitch = -21f;
    private const float WalkPitch = 0f;
    private const float ArmReach = 58f;
    private const float HindNeutral = -50f;
    private const float PushStride = 100f;
    private const float WalkStride = 107f;
    private const float FrontNeutral = 47f;
    private const double LetGo = 0.6;

    // The lope: its full stride (and the shorter first bounds, as it gathers speed), how far the run takes to build
    // up, and the crouch it springs from.
    private const float BoundStride = 190f;
    private const float FirstBoundStride = 125f;
    private const float BoundBuild = 260f;
    private const float Crouch = 6f;

    // Where the lope's cycle stands at its start (cycle phase 0 = the near hind paw landing).
    private const float BoundStart = 0.15f;

    private readonly RaccoonCues cues;
    private readonly RollingLetter rear;
    private readonly float scale;
    private readonly float ground;
    private readonly long seed;
    private readonly GaitClock pushClock;
    private readonly FootSteps pushNear;
    private readonly FootSteps pushFar;
    private readonly GaitClock exitClock;
    private readonly FootSteps exitNearHind;
    private readonly FootSteps exitNearFront;
    private readonly FootSteps exitFarHind;
    private readonly FootSteps exitFarFront;
    private readonly Pace exitPace;
    private readonly double hesitation;
    private readonly FloatTrack[] tailSway = new FloatTrack[3];
    private static readonly float[] TailRest = { -4f, -9f, 11f };
    private readonly float pushStartX;
    private readonly float stopX;
    private readonly float exitX;

    /// <summary>Creates a raccoon's performance.</summary>
    /// <param name="look">Its size and coat.</param>
    /// <param name="cues">Its cues.</param>
    /// <param name="rearLetter">The letter it pushes (its roll must be bound already).</param>
    /// <param name="index">Which raccoon it is (varies its blink, sway and gait).</param>
    /// <param name="gallops">Whether it runs off in a bounding lope (else it walks).</param>
    public RaccoonActor(RaccoonLook look, RaccoonCues cues, RollingLetter rearLetter, int index, bool gallops)
    {
        this.cues = cues;
        rear = rearLetter;
        scale = look.Scale;
        ground = Stage.RaccoonGround;
        Index = index;
        Galloping = gallops;
        seed = 7100 + (index * 131);
        Rig = new RaccoonRig(look);
        Rig.Root.StartTime = cues.Push.StartTime;
        Rig.Root.EndTime = cues.Exit.EndTime;
        Rig.FlexAt = FlexAt;

        pushStartX = XWhilePushing(cues.Push.StartTime);
        stopX = XWhilePushing(cues.Push.EndTime);
        exitX = Stage.Width + (330 * scale);
        float pushLength = (stopX - pushStartX) / scale;
        float exitLength = (exitX - stopX) / scale;

        // Pushing: a two-legged walk with a shorter stride, varying a little from step to step; its phase is set so
        // that both feet are down (double support) at the moment the letters stop.
        pushClock = new GaitClock(pushLength + 1, d => PushStride, 0.12f, seed + 1);
        float endPhase = pushClock.PhaseAt(pushLength);
        float offset = 0.05f - (endPhase - MathF.Floor(endPhase));
        pushNear = new FootSteps(pushClock, offset, 0.62f, HindNeutral + 4, 0.62f * PushStride / 2, 13, 0.08f, seed + 2);
        pushFar = new FootSteps(pushClock, offset + 0.5f, 0.62f, HindNeutral - 4, 0.62f * PushStride / 2, 13, 0.08f, seed + 3);

        if (!gallops)
        {
            // Walking: a lateral-sequence walk (near hind, near front, far hind, far front, a quarter cycle apart).
            // With three feet down at a time, the walk starts from a stance with all four on the ground. The stride
            // keeps every footfall within the legs' reach, so a planted foot never has to slip.
            exitClock = new GaitClock(exitLength + 1, d => WalkStride, 0.08f, seed + 4);
            float reach = 0.75f * WalkStride / 2;
            exitNearHind = new FootSteps(exitClock, 0f, 0.75f, HindNeutral - 2, reach, 15, 0.05f, seed + 5);
            exitNearFront = new FootSteps(exitClock, 0.25f, 0.75f, FrontNeutral + 2, reach, 17, 0.05f, seed + 6);
            exitFarHind = new FootSteps(exitClock, 0.5f, 0.75f, HindNeutral - 6, reach, 15, 0.05f, seed + 7);
            exitFarFront = new FootSteps(exitClock, 0.75f, 0.75f, FrontNeutral - 4, reach, 17, 0.05f, seed + 8);

            // The walk off: gathering pace for the first second, then steady but never metronomic (a slow drift in
            // speed), with one hesitation part of the way along - a slowing for a look up (see the head track).
            DrawingRandom random = new DrawingRandom(seed + 9);
            hesitation = cues.Exit.Duration * (0.5 + (0.12 * random.NextDouble()));
            exitPace = new Pace(cues.Exit.StartTime, cues.Exit.Duration, s =>
            {
                double gather = s < 1.0 ? Math.Sin(Math.PI / 2 * s) : 1.0;
                double h = (s - hesitation + 0.35) / 0.9;
                double dip = h > 0 && h < 1 ? 0.55 * Math.Pow(Math.Sin(Math.PI * h), 2) : 0;
                return gather * (1 - dip) * (1 + (0.07 * DrawingNoise.Value(s * 0.9, 1.7, seed + 10)));
            });
        }
        else
        {
            // The lope: hind paws landing together (the far one a moment after the near), pushing off; a moment in
            // the air, stretched out; the front paws reaching and landing one after the other; the hind legs swinging
            // forward under the arched body to land again. Phases of the lope's cycle (0 = near hind landing):
            // hind paws down 0.00-0.28 (far 0.04-0.32), front 0.45-0.71 (far 0.54-0.80); in the air 0.32-0.45 and
            // briefly before the hind paws land. Its first bounds are shorter, lengthening as it gathers speed.
            exitClock = new GaitClock(exitLength + 1, d => FirstBoundStride + ((BoundStride - FirstBoundStride) * Smooth(d / BoundBuild)), 0.07f, seed + 4);
            const float hindDuty = 0.28f;
            const float frontDuty = 0.26f;
            exitNearHind = new FootSteps(exitClock, BoundStart, hindDuty, -40, hindDuty * BoundStride / 2, 32, 0.06f, seed + 5);
            exitFarHind = new FootSteps(exitClock, BoundStart - 0.04f, hindDuty, -44, hindDuty * BoundStride / 2, 30, 0.06f, seed + 7);
            exitNearFront = new FootSteps(exitClock, BoundStart - 0.45f, frontDuty, 50, frontDuty * BoundStride / 2, 30, 0.06f, seed + 6);
            exitFarFront = new FootSteps(exitClock, BoundStart - 0.54f, frontDuty, 46, frontDuty * BoundStride / 2, 27, 0.06f, seed + 8);

            // It starts from a crouch on all fours: the hind paws push off where they stand, the front paws reach
            // forward at once.
            exitNearHind.StandAt(-30, 0.30f);
            exitFarHind.StandAt(-40, 0.30f);
            exitNearFront.StandAt(52, 0.19f - 0.45f);
            exitFarFront.StandAt(44, 0.22f - 0.54f);

            // The run off: a quick start, then a steady lope that keeps gathering pace a little.
            double duration = cues.Exit.Duration;
            exitPace = new Pace(cues.Exit.StartTime, duration, s =>
            {
                const double ramp = 0.7;
                double start = s < ramp ? 1 - Math.Pow(1 - (s / ramp), 2) : 1.0;
                return start * (1 + (0.12 * s / duration));
            });
        }
    }

    /// <summary>Which raccoon this is.</summary>
    public int Index { get; }

    /// <summary>Whether it runs off in a bounding lope rather than walking.</summary>
    public bool Galloping { get; }

    /// <summary>The raccoon's rig (its root group goes on a layer).</summary>
    public RaccoonRig Rig { get; }

    /// <summary>Binds its tracks and its per-frame pose to the session's animations.</summary>
    /// <param name="animations">The animations.</param>
    public void Bind(DrawingAnimations animations)
    {
        // The head: tucked down while pushing (with one look up along the way), lifted to look at the letters in the
        // beat, level while walking - with a look up during the hesitation - or held low and forward for the run.
        TimelineCue push = cues.Push;
        TimelineCue beat = cues.Beat;
        double glance = push.StartTime + new[] { 3.3, 4.6, 2.4 }[Index % 3];
        FloatTrack head = new FloatTrack
        {
            { push.StartTime, 9f },
            { glance, 9f },
            { glance + 0.3, -5f, EasingKind.SineOut },
            { glance + 0.85, -3f },
            { glance + 1.2, 9f, EasingKind.SineInOut },
            { push.EndTime, 9f },
            { push.EndTime + 0.4, -15f, EasingKind.SineOut },
            { beat.EndTime - 0.1, -11f },
        };
        if (Galloping)
        {
            head.Add(beat.EndTime + 0.3, 5f, EasingKind.SineInOut);
        }
        else
        {
            double look = cues.Exit.StartTime + hesitation;
            head.Add(beat.EndTime + 0.4, 3f, EasingKind.SineInOut);
            head.Add(look - 0.2, 3f);
            head.Add(look + 0.15, -10f, EasingKind.SineOut);
            head.Add(look + 0.6, -8f);
            head.Add(look + 0.95, 3f, EasingKind.SineInOut);
        }

        animations.Bind(Rig.Head, AnimatedProperty.Rotation, head);

        // The tail sways, each segment a little later and further than the one before (a wave down the tail).
        float[] rest = TailRest;
        float[] swing = { 4f, 6f, 8f };
        double period = 1.5 + (0.2 * Index);
        for (int k = 0; k < Rig.TailSegments.Count; k++)
        {
            double t0 = (0.17 * k) + (0.4 * Index);
            tailSway[k] = new FloatTrack
            {
                { t0, rest[k] - swing[k] },
                { t0 + (period / 2), rest[k] + swing[k], EasingKind.SineInOut },
            }.WithLoop(TrackLoopMode.PingPong);
            animations.Bind(Rig.TailSegments[k], AnimatedProperty.Rotation, tailSway[k]);

            // On top of the sway, the stride-coupled follow-through (see FollowThroughAt): an ADDITIVE binding, so it
            // adds to whatever the sway track sets for that moment, whatever the order the bindings were made in.
            int segment = k;
            animations.Bind(Rig.TailSegments[k], AnimatedProperty.Rotation, t => FollowThroughAt(t, segment), BindingMode.Add);
        }

        // The flick: a quick lift of the whole tail in the beat, and back.
        animations.Bind(Rig.TailFlick, AnimatedProperty.Rotation, new FloatTrack
        {
            { beat.StartTime + 0.15, 0f },
            { beat.StartTime + 0.32, 17f, EasingKind.SineOut },
            { beat.StartTime + 0.62, -5f, EasingKind.SineInOut },
            { beat.StartTime + 0.86, 0f, EasingKind.SineInOut },
        });

        // Blinks: the eye squashes shut for a moment every few seconds.
        double blinkEvery = 2.9 + (0.7 * Index);
        double first = push.StartTime + 0.8 + (0.5 * Index);
        foreach (DrawingGroup eye in Rig.Eyes)
        {
            animations.Bind(eye, AnimatedProperty.ScaleY, new FloatTrack
            {
                { first, 1f },
                { first + blinkEvery - 0.16, 1f },
                { first + blinkEvery - 0.08, 0.1f, EasingKind.SineIn },
                { first + blinkEvery, 1f, EasingKind.SineOut },
            }.WithLoop(TrackLoopMode.Loop));
        }

        animations.Bind(Pose);
    }

    /// <summary>The raccoon's x while pushing: its hands on the back of the rear letter.</summary>
    private float XWhilePushing(double seconds)
    {
        PointF hub = rear.HubAt(seconds);
        PointF shoulder = RaccoonRig.ShoulderAt(0, PushPitch);
        return hub.X - (rear.HalfWidth * 0.9f) - ((shoulder.X + ArmReach) * scale);
    }

    private void Pose(double t)
    {
        if (t < cues.Push.StartTime || t >= cues.Exit.EndTime) return;
        Posture p = PostureAt(t);
        Rig.Pose(p.X, ground, p.Bob, p.Pitch, p.Flex, p.HindNear, p.HindFar, p.FrontNear, p.FrontFar, p.Nod);
    }

    /// <summary>The spine's bend at a time (the rig redraws the torso from it).</summary>
    private float FlexAt(double t) => t < cues.Push.StartTime || t >= cues.Exit.EndTime ? 0f : PostureAt(t).Flex;

    private Posture PostureAt(double t)
    {
        if (t < cues.Push.EndTime) return Pushing(t);
        if (t < cues.Exit.StartTime) return Beat(t);
        return Galloping ? Bounding(t) : Walking(t);
    }

    /// <summary>
    /// Pushing: hands on the letter, the hind legs stepping. Each hind step drives the body forward and up a
    /// little (a bob and a lean twice per stride, a little different each step), and the head nods just after it.
    /// </summary>
    private Posture Pushing(double t)
    {
        float x = XWhilePushing(t);
        float d = (x - pushStartX) / scale;
        float phase = pushClock.PhaseAt(d);
        float vary = Vary(phase, 0.3f);
        LimbPose hindNear = Foot(pushNear, d, RaccoonRig.AnkleHeight, 22);
        LimbPose hindFar = Foot(pushFar, d, RaccoonRig.AnkleHeight, 22);
        float bob = 1.8f * vary * MathF.Sin(4 * MathF.PI * phase);
        float lean = 1.3f * MathF.Sin((4 * MathF.PI * phase) + 0.6f);
        float nod = 2.2f * Vary(phase + 3.1f, 0.35f) * MathF.Sin((4 * MathF.PI * phase) - 1.1f);
        (LimbPose near, LimbPose far) = Hands(t, x);
        return new Posture(x, bob, PushPitch + lean, 0, hindNear, hindFar, near, far, nod);
    }

    /// <summary>Both front paws on the back of the rear letter (the far one a little higher).</summary>
    private (LimbPose Near, LimbPose Far) Hands(double t, float x)
    {
        PointF hub = rear.HubAt(t);
        float cx = (hub.X - (rear.HalfWidth * 0.9f) - x) / scale;
        float cy = (hub.Y - ground) / scale;
        return (new LimbPose(new PointF(cx, cy + 6), -62), new LimbPose(new PointF(cx - 3, cy - 10), -70));
    }

    /// <summary>
    /// The beat: the paws come off the letter and down to the ground (near paw first), the hind feet shuffle into a
    /// four-legged stance, the body levels; the head's look up is its track. The nod and tail wave of the push die
    /// away. A runner sinks into a crouch at the end, ready to spring.
    /// </summary>
    private Posture Beat(double t)
    {
        double local = t - cues.Beat.StartTime;
        float x = stopX;
        float dEnd = (stopX - pushStartX) / scale;

        LimbPose hindNear = Blend(Foot(pushNear, dEnd, RaccoonRig.AnkleHeight, 22), Foot(exitNearHind, 0, RaccoonRig.AnkleHeight, 20), Stage01(local, 0.05, 0.35), 9);
        LimbPose hindFar = Blend(Foot(pushFar, dEnd, RaccoonRig.AnkleHeight, 22), Foot(exitFarHind, 0, RaccoonRig.AnkleHeight, 20), Stage01(local, 0.3, 0.6), 9);

        (LimbPose onNear, LimbPose onFar) = Hands(cues.Push.EndTime, x);
        LimbPose frontNear = Blend(onNear, Foot(exitNearFront, 0, RaccoonRig.WristHeight, 18), Stage01(local, 0.0, LetGo * 0.75), 14);
        LimbPose frontFar = Blend(onFar, Foot(exitFarFront, 0, RaccoonRig.WristHeight, 18), Stage01(local, LetGo * 0.25, LetGo), 14);

        float level = Stage01(local, 0, LetGo);
        float pitch = PushPitch + ((WalkPitch - PushPitch) * level);

        // As the front paws come down the body dips a little and rises again: weight shifting onto four legs.
        float settle = 2.5f * MathF.Sin(MathF.PI * level);
        if (Galloping) settle += Crouch * Stage01(local, cues.Beat.Duration - 0.35, cues.Beat.Duration);
        float fade = 1 - Stage01(local, 0, 0.5);
        float phase = pushClock.PhaseAt(dEnd);
        float nod = 2.2f * fade * MathF.Sin((4 * MathF.PI * phase) - 1.1f);
        return new Posture(x, settle, pitch, 0, hindNear, hindFar, frontNear, frontFar, nod);
    }

    /// <summary>
    /// Walking off: a four-legged walk. The body bobs twice a stride (lowest as a foot takes the weight, a little
    /// more in some strides than others) and rocks a little, and the head nods a beat behind the body. (The spine
    /// stays at rest in the walk: the torso is only redrawn bent when the raccoon runs.)
    /// </summary>
    private Posture Walking(double t)
    {
        float x = stopX + ((exitX - stopX) * exitPace.At(t));
        float d = (x - stopX) / scale;
        float phase = exitClock.PhaseAt(d);
        float gait = Math.Clamp(d / 30f, 0, 1);                // the rhythm builds over the first steps
        float bob = gait * 2.4f * Vary(phase, 0.35f) * MathF.Sin(4 * MathF.PI * phase);
        float pitch = WalkPitch + (gait * 1.3f * MathF.Sin(2 * MathF.PI * phase));
        float nod = gait * 3f * Vary(phase + 5.3f, 0.4f) * MathF.Sin((4 * MathF.PI * phase) - 1.2f);
        return new Posture(x, bob, pitch, 0,
            Foot(exitNearHind, d, RaccoonRig.AnkleHeight, 20),
            Foot(exitFarHind, d, RaccoonRig.AnkleHeight, 20),
            Foot(exitNearFront, d, RaccoonRig.WristHeight, 18),
            Foot(exitFarFront, d, RaccoonRig.WristHeight, 18),
            nod);
    }

    /// <summary>
    /// The run off, a bounding lope. Through each cycle the spine flexes and extends - arched as the hind legs
    /// gather under it and land, stretched out as they push off and the front legs reach - the body rises into the
    /// air after the push and sinks as the paws take the weight, and it pitches nose-up on the push and nose-down
    /// as the front paws land. The head stays nearly level (it turns against the body's pitch and bend), and the
    /// tail streams out behind and bobs after the body (see <see cref="FollowThroughAt" />).
    /// </summary>
    private Posture Bounding(double t)
    {
        float x = stopX + ((exitX - stopX) * exitPace.At(t));
        float d = (x - stopX) / scale;
        float g = exitClock.PhaseAt(d) + BoundStart;
        float run = Smooth(d / 60f);
        float flex = run * Cycle(g, BoundFlex);
        float bob = (run * Vary(g, 0.25f) * Cycle(g, BoundRise)) + (Crouch * (1 - run));
        float pitch = run * Cycle(g, BoundPitch);
        float level = -(pitch + RaccoonRig.BendAngle(RaccoonRig.Neck.X, flex)) * 0.85f;
        float nod = level + (run * 2.5f * MathF.Sin(2 * MathF.PI * (g - 0.62f)));
        return new Posture(x, bob, pitch, flex,
            Foot(exitNearHind, d, RaccoonRig.AnkleHeight, 34),
            Foot(exitFarHind, d, RaccoonRig.AnkleHeight, 34),
            Foot(exitNearFront, d, RaccoonRig.WristHeight, 26),
            Foot(exitFarFront, d, RaccoonRig.WristHeight, 26),
            nod);
    }

    /// <summary>
    /// The tail's follow-through at a time, for one segment: on top of its own sway, each segment swings with the
    /// stride, later and further the nearer the tip - the tail is carried along by the body rather than moving
    /// rigidly with it. The stride's phase and how strongly it shows are those of the pose at that time (the push,
    /// the beat - where it dies away - and the walk); in the lope the tail also straightens and streams out behind,
    /// bobbing a beat after the body; outside the cues it is 0.
    /// </summary>
    private float FollowThroughAt(double t, int segment)
    {
        float phase;
        float amount;
        if (t < cues.Push.StartTime || t >= cues.Exit.EndTime) return 0f;
        if (t < cues.Push.EndTime)
        {
            float x = XWhilePushing(t);
            phase = pushClock.PhaseAt((x - pushStartX) / scale);
            amount = 0.7f;
        }
        else if (t < cues.Exit.StartTime)
        {
            double local = t - cues.Beat.StartTime;
            phase = pushClock.PhaseAt((stopX - pushStartX) / scale);
            amount = 0.7f * (1 - Stage01(local, 0, 0.5));
        }
        else
        {
            float x = stopX + ((exitX - stopX) * exitPace.At(t));
            float d = (x - stopX) / scale;
            if (Galloping)
            {
                float g = exitClock.PhaseAt(d) + BoundStart;
                float run = Smooth(d / 80f);
                // Streaming: the lazy sway mostly gives way to a straighter tail held out behind, which bobs with
                // the bounds, each segment later than the one before.
                float[] stream = { 3f, 3f, -7f };
                float[] wave = { 3f, 4.5f, 6.5f };
                float calm = -0.65f * (tailSway[segment].Evaluate(t) - TailRest[segment]);
                return run * (calm + stream[segment] + (wave[segment] * Vary(g + 11.7f, 0.3f) * MathF.Sin((2 * MathF.PI * (g - 0.15f)) - (0.95f * (segment + 1)))));
            }

            phase = exitClock.PhaseAt(d);
            amount = Math.Clamp(d / 30f, 0, 1);
        }

        float[] swing = { 2f, 3.5f, 5.5f };
        return amount * swing[segment] * Vary(phase + 7.9f, 0.3f) * MathF.Sin((2 * MathF.PI * phase) - (0.9f * (segment + 1)));
    }

    // The lope's body through one cycle (phase, value), 0 = the near hind paw landing: the spine's bend (arched as
    // the hind legs gather and land, stretched out from the push to the front paws landing), the body's height (+
    // down: lowest as the paws take the weight, highest in the air after the push) and its pitch (nose up on the
    // push, nose down over the front paws).
    private static readonly (float Phase, float Value)[] BoundFlex = { (0.0f, 0.9f), (0.15f, 0.45f), (0.3f, -0.55f), (0.42f, -1f), (0.6f, -0.3f), (0.8f, 0.7f), (0.94f, 1f) };
    private static readonly (float Phase, float Value)[] BoundRise = { (0.0f, 1f), (0.12f, 3f), (0.26f, -5f), (0.36f, -12f), (0.45f, -6f), (0.58f, 3f), (0.75f, 0f), (0.88f, -4f) };
    private static readonly (float Phase, float Value)[] BoundPitch = { (0.0f, -2f), (0.18f, -6f), (0.3f, -5f), (0.4f, 0f), (0.5f, 4f), (0.65f, 5f), (0.82f, 1f) };

    /// <summary>A value through a repeating cycle, smoothly through keys (Catmull-Rom, wrapping round).</summary>
    private static float Cycle(float phase, (float Phase, float Value)[] keys)
    {
        float p = phase - MathF.Floor(phase);
        int n = keys.Length;
        int i = n - 1;
        for (int k = 0; k < n; k++)
        {
            if (keys[k].Phase <= p) i = k;
        }

        (float Phase, float Value) Key(int k) => (keys[((k % n) + n) % n].Phase + MathF.Floor((float)k / n), keys[((k % n) + n) % n].Value);
        int at = keys[i].Phase <= p ? i : i - n;
        (float t1, float v1) = Key(at);
        (float t2, float v2) = Key(at + 1);
        float v0 = Key(at - 1).Value;
        float v3 = Key(at + 2).Value;
        float u = (p - t1) / (t2 - t1);
        float u2 = u * u;
        return 0.5f * ((2 * v1) + ((-v0 + v2) * u) + (((2 * v0) - (5 * v1) + (4 * v2) - v3) * u2) + ((-v0 + (3 * v1) - (3 * v2) + v3) * u2 * u));
    }

    /// <summary>A seeded, smooth variation about 1 along a gait's phase (by up to the given fraction either way).</summary>
    private float Vary(float phase, float by) => 1 + (by * (float)DrawingNoise.Value(phase * 0.6, 3.3, seed + 20));

    /// <summary>A foot from its footfalls: its target (ankle or wrist) and its paw angle (toes droop in the swing).</summary>
    private static LimbPose Foot(FootSteps steps, float distance, float jointHeight, float droop)
    {
        PointF place = steps.At(distance, out float swing);
        float angle = swing > 0 ? droop * MathF.Sin(MathF.PI * swing) : 0;
        return new LimbPose(new PointF(place.X, -jointHeight + place.Y), angle, swing == 0);
    }

    /// <summary>Moves a paw from one pose to another along an arc (lifted in the middle), easing in and out.</summary>
    private static LimbPose Blend(LimbPose from, LimbPose to, float u, float lift)
    {
        float x = from.Target.X + ((to.Target.X - from.Target.X) * u);
        float y = from.Target.Y + ((to.Target.Y - from.Target.Y) * u) - (lift * MathF.Sin(MathF.PI * u));
        bool planted = (u <= 0 && from.Planted) || (u >= 1 && to.Planted);
        return new LimbPose(new PointF(x, y), from.PawAngle + ((to.PawAngle - from.PawAngle) * u), planted);
    }

    /// <summary>0 before <paramref name="start" />, 1 after <paramref name="end" />, eased in between.</summary>
    private static float Stage01(double t, double start, double end)
    {
        double u = Math.Clamp((t - start) / (end - start), 0, 1);
        return (float)Easing.Evaluate(EasingKind.SineInOut, u);
    }

    /// <summary>0 at or below 0, 1 at or above 1, smooth between.</summary>
    private static float Smooth(float u)
    {
        u = Math.Clamp(u, 0, 1);
        return u * u * (3 - (2 * u));
    }

    /// <summary>A whole pose: where, the body's bob, pitch and bend, the four paws and the head's nod.</summary>
    private readonly record struct Posture(float X, float Bob, float Pitch, float Flex, LimbPose HindNear, LimbPose HindFar, LimbPose FrontNear, LimbPose FrontFar, float Nod);
}

/// <summary>Small conveniences for building tracks.</summary>
internal static class TrackExtensions
{
    /// <summary>Sets a track's loop mode and returns it (for use in an initializer chain).</summary>
    /// <param name="track">The track.</param>
    /// <param name="mode">The loop mode.</param>
    /// <returns>The track.</returns>
    public static FloatTrack WithLoop(this FloatTrack track, TrackLoopMode mode)
    {
        track.LoopMode = mode;
        return track;
    }
}
