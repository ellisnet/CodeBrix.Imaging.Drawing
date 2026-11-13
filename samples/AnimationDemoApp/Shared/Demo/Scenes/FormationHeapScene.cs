using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Formations;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// A formation that heaps up. A heart and an arch stand on one ground line; leaves fall into them and pile up from
/// the ground (<see cref="HeapArrivalOrder" />: the heart grows from its tip, the arch rises up both legs and closes
/// over the middle last). Each leaf drifts down with a <see cref="SwayingFallArrival" /> in a light breeze and lands
/// in a seeded resting pose; the arrivals follow an <see cref="ArrivalSchedule" /> that is done by 7.4 s, and
/// <see cref="DrawingAnimations.BindFormation" /> keeps the resting leaves on one layer and the falling ones on
/// another. The leaves are fill-only <see cref="PathShape" />s. A read-out counts the falling and resting leaves.
/// </summary>
internal sealed class FormationHeapScene : DemoScene
{
    private const float Ground = 600;
    private const long Seed = 5;
    private static readonly Color[] Palette =
    {
        Color.FromRgb(214, 96, 40), Color.FromRgb(232, 150, 50), Color.FromRgb(190, 60, 40),
        Color.FromRgb(240, 190, 70), Color.FromRgb(160, 84, 40), Color.FromRgb(226, 120, 64),
    };

    private readonly DrawingSession session;
    private readonly FormationElements formation;
    private readonly double[] times;
    private readonly DrawingLayer now;

    /// <summary>Builds the shapes, the spots, the falls and the formation.</summary>
    public FormationHeapScene()
    {
        session = CreateSession(DemoFrame.Width, DemoFrame.Height, Backdrop);
        DrawingLayer stage = session.AddLayer("Stage", Dim);
        session.ActiveLayer = stage;

        PathGeometry heart = new PathGeometryBuilder()
            .MoveTo(330, Ground - 2)
            .CubicTo(new PointF(250, 520), new PointF(140, 470), new PointF(140, 380))
            .CubicTo(new PointF(140, 315), new PointF(190, 280), new PointF(242, 280))
            .CubicTo(new PointF(284, 280), new PointF(314, 302), new PointF(330, 334))
            .CubicTo(new PointF(346, 302), new PointF(376, 280), new PointF(418, 280))
            .CubicTo(new PointF(470, 280), new PointF(520, 315), new PointF(520, 380))
            .CubicTo(new PointF(520, 470), new PointF(410, 520), new PointF(330, Ground - 2))
            .Close()
            .Build();
        PathGeometry arch = PathGeometry.RoundedRectangle(new RectangleF(680, 280, 480, Ground - 280), 36)
            .Subtract(PathGeometry.RoundedRectangle(new RectangleF(790, 390, 260, 300), 130));
        PathGeometry shape = heart.Union(arch);

        session.DrawShape(new RectangleShape(0, Ground, DemoFrame.Width, DemoFrame.Height - Ground, 1, Color.FromRgb(34, 30, 34), isFilled: true));
        session.DrawShape(new LineShape(40, Ground, DemoFrame.Width - 40, Ground, 2, Color.FromRgb(110, 92, 80)));
        session.DrawShape(new PathShape(shape, new PathStrokeStyle(1.5f, dashPattern: new[] { 6f, 6f }), Color.FromRgba(132, 138, 164, 120)));
        Label(stage, session, "HeapArrivalOrder(groundY: 600): bottom up - the heart from its tip, the arch up both legs, its middle last",
            640, Ground + 30, 14, Dim, TextAlign.Center);
        Label(stage, session, "SwayingFallArrival (sway, rock, tumble, flip, flutter) in a breeze; leaves: fill-only PathShape(geometry, color)",
            640, Ground + 52, 14, Dim, TextAlign.Center);

        List<FormationTarget> targets = FormationTargets.Fill(shape, new FormationTargetOptions
        {
            Spacing = 12f, Jitter = 0.3f, EdgeClearance = 4f, Seed = Seed, Order = new HeapArrivalOrder(Ground),
        });
        times = new ArrivalSchedule(3.0, 7.4).GetTimes(targets.Count);
        PathGeometry leaf = PathGeometry.Parse("M -10 0 C -5 -7 5 -7 10 0 C 5 7 -5 7 -10 0 Z");
        var rests = new FormationPose[targets.Count];
        List<FormationMember> members = FormationMember.FromTargets(targets, times, (target, arrival, k) =>
        {
            DrawingRandom random = DrawingRandom.ForKey(Seed, k);
            rests[k] = new FormationPose(target.Position.X, target.Position.Y, (float)random.Uniform(0, 180), (float)random.Uniform(0.85, 1.15));
            return new SwayingFallArrival(random, rests[k], arrival, 40f, Breeze);
        });

        DrawingLayer resting = session.AddLayer("Resting", Accent);
        DrawingLayer falling = session.AddLayer("Falling", Accent);
        formation = session.Animations.BindFormation(members, resting, falling,
            k => new PathShape(leaf, ColorOf(k)) { Transform = rests[k].ToTransform() },
            k => new DrawingElement[] { new PathShape(leaf, ColorOf(k)) });

        AddHeader(session, opaqueBand: true);
        now = session.AddLayer("Now", Ink);
    }

    /// <inheritdoc />
    public override string Name => "formation-heap";

    /// <inheritdoc />
    public override string Title => "Formation: leaves fall and heap up";

    /// <inheritdoc />
    public override string Description => "FormationTargets with HeapArrivalOrder (bottom-up from a ground line), SwayingFallArrival, ArrivalSchedule, BindFormation";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;

        now.Clear();
        int landed = 0;
        while (landed < times.Length && times[landed] <= seconds) landed++;
        string readout = string.Format(CultureInfo.InvariantCulture, "falling {0,4}    resting {1,4} of {2}",
            formation.GetTravellingCount(seconds), landed, formation.Count);
        now.AddShape(new TextShape(readout, 1248, 46, 16, session.Fonts, Accent, TextAlign.Right));

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    //How far the air has moved sideways by a time: a steady breeze that gusts a little
    private static double Breeze(double seconds) => (14 * seconds) + (10 * Math.Sin(0.7 * seconds));

    private static Color ColorOf(int k) => Palette[DrawingRandom.ForKey(Seed + 1, k).NextInt(Palette.Length)];
}
