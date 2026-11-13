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
/// A formation spiralling in. A word's glyph outline (<see cref="TextLayoutResult.GetOutline(float, float)" />) is
/// covered with target spots (<see cref="FormationTargets.Fill(PathGeometry, FormationTargetOptions)" />, filling
/// from the centre outwards with <see cref="CenterOutArrivalOrder" />); each spot gets an arrival time from an
/// <see cref="ArrivalSchedule" /> (a lazy start, a busy middle and a long taper, done by 6.4 s) and a
/// <see cref="SpiralArrival" />; <see cref="DrawingAnimations.BindFormation" /> turns them into resting dots on one
/// layer and travelling dots on another. Below, the schedule's rate with the play head and a count of the dots.
/// </summary>
internal sealed class FormationSpiralScene : DemoScene
{
    private const string Word = "BLOOM";
    private const float Radius = 3f;
    private const long Seed = 11;
    private const double FirstArrival = 1.6;
    private const double LastArrival = 6.4;
    private const float GraphLeft = 240, GraphRight = 1040, GraphTop = 590, GraphBottom = 650;
    private static readonly Color[] Palette =
    {
        Color.FromRgb(255, 176, 72), Color.FromRgb(255, 128, 72), Color.FromRgb(240, 84, 110),
        Color.FromRgb(255, 214, 110), Color.FromRgb(250, 150, 96),
    };

    private readonly DrawingSession session;
    private readonly FormationElements formation;
    private readonly double[] times;
    private readonly DrawingLayer now;

    /// <summary>Lays out the word, chooses the spots and their arrivals, and binds the formation.</summary>
    public FormationSpiralScene()
    {
        session = CreateSession(DemoFrame.Width, DemoFrame.Height, Backdrop);
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        session.ActiveLayer = notes;

        TextLayoutResult layout = session.LayoutText(Word, 300);
        float left = (DemoFrame.Width - layout.Width) / 2f;
        PathGeometry outline = layout.GetOutline(left, 150);
        session.DrawShape(new PathShape(outline, 1f, Color.FromRgba(132, 138, 164, 60)));

        List<FormationTarget> targets = FormationTargets.Fill(outline, new FormationTargetOptions
        {
            Spacing = 7.5f, Jitter = 0.25f, EdgeClearance = 2.5f, Seed = Seed, Order = new CenterOutArrivalOrder(),
        });
        var profile = new ArrivalRateProfile();
        times = new ArrivalSchedule(FirstArrival, LastArrival, profile).GetTimes(targets.Count);
        List<FormationMember> members = FormationMember.FromTargets(targets, times, (target, arrival, k) =>
        {
            DrawingRandom random = DrawingRandom.ForKey(Seed, k);
            double travel = random.Uniform(1.2, 1.7);
            return new SpiralArrival(new FormationPose(target.Position.X, target.Position.Y), arrival - travel, arrival,
                (float)random.Uniform(240, 460), (float)random.Uniform(0, 360), 1.25f, 1.8f);
        });

        DrawingLayer resting = session.AddLayer("Resting", Accent);
        DrawingLayer travelling = session.AddLayer("Travelling", Accent);
        formation = session.Animations.BindFormation(members, resting, travelling,
            k => new CircleShape(targets[k].Position.X, targets[k].Position.Y, Radius, 1, ColorOf(k), isFilled: true),
            k => new DrawingElement[] { new CircleShape(0, 0, Radius, 1, ColorOf(k), isFilled: true) });

        // The schedule's rate over the scene, with the span of the arrivals marked.
        var rate = new List<PointF>();
        for (int i = 0; i <= 200; i++)
        {
            double u = i / 200.0;
            rate.Add(new PointF(GraphX(FirstArrival + (u * (LastArrival - FirstArrival))), GraphBottom - ((GraphBottom - GraphTop) * (float)profile.GetRate(u))));
        }
        rate.Add(new PointF(GraphX(LastArrival), GraphBottom));
        rate.Add(new PointF(GraphX(FirstArrival), GraphBottom));
        session.DrawShape(new PolylineShape(rate, 1, Color.FromRgba(255, 176, 72, 60), isClosed: true, isFilled: true));
        session.DrawShape(new PolylineShape(rate.GetRange(0, 201), 2, Color.FromRgba(255, 176, 72, 200)));
        session.DrawShape(new LineShape(GraphLeft, GraphBottom, GraphRight, GraphBottom, 1, Dim));
        for (int s = 0; s <= 8; s++)
        {
            session.DrawShape(new LineShape(GraphX(s), GraphBottom, GraphX(s), GraphBottom + 5, 1, Dim));
            Label(notes, session, s.ToString(CultureInfo.InvariantCulture) + " s", GraphX(s), GraphBottom + 20, 12, Dim, TextAlign.Center);
        }
        Label(notes, session, "arrival rate: ArrivalSchedule(1.6 s, 6.4 s, new ArrivalRateProfile()) - ramp, hold, taper", GraphLeft, GraphTop - 12, 13, Dim);
        Label(notes, session, "FormationTargets.Fill(outline, spacing 7.5, CenterOutArrivalOrder)  +  SpiralArrival  +  BindFormation", 640, 140, 14, Dim, TextAlign.Center);

        // The header goes on top (with an opaque band), so the dots fly behind it.
        AddHeader(session, opaqueBand: true);
        now = session.AddLayer("Now", Ink);
    }

    /// <inheritdoc />
    public override string Name => "formation-spiral";

    /// <inheritdoc />
    public override string Title => "Formation: dots spiral in to form a word";

    /// <inheritdoc />
    public override string Description => "FormationTargets over a glyph outline, CenterOutArrivalOrder, ArrivalSchedule, SpiralArrival, DrawingAnimations.BindFormation";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;

        now.Clear();
        int arrived = 0;
        while (arrived < times.Length && times[arrived] <= seconds) arrived++;
        now.AddShape(new LineShape(GraphX(seconds), GraphTop - 4, GraphX(seconds), GraphBottom, 2, Ink));
        string readout = string.Format(CultureInfo.InvariantCulture, "spots {0}    resting {1}    spiralling in {2}",
            formation.Count, arrived, formation.GetTravellingCount(seconds));
        now.AddShape(new TextShape(readout, 1248, 46, 16, session.Fonts, Accent, TextAlign.Right));

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private static float GraphX(double seconds) => GraphLeft + ((GraphRight - GraphLeft) * (float)(seconds / 8.0));

    private static Color ColorOf(int k) => Palette[DrawingRandom.ForKey(Seed + 1, k).NextInt(Palette.Length)];
}
