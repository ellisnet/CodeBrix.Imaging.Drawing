extern alias noskia;
extern alias skia;

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using SilverAssertions;
using Xunit;
using NA = noskia::CodeBrix.Imaging.Drawing.Animation;
using ND = noskia::CodeBrix.Imaging.Drawing;
using NF = noskia::CodeBrix.Imaging.Drawing.Formations;
using NM = noskia::CodeBrix.Imaging.Drawing.Models;
using NP = noskia::CodeBrix.Imaging.Drawing.Paths;
using NR = noskia::CodeBrix.Imaging.Drawing.Planes;
using NS = noskia::CodeBrix.Imaging.Drawing.Shapes;
using NT = noskia::CodeBrix.Imaging.Drawing.Text;
using SA = skia::CodeBrix.Imaging.Drawing.Animation;
using SD = skia::CodeBrix.Imaging.Drawing;
using SF = skia::CodeBrix.Imaging.Drawing.Formations;
using SM = skia::CodeBrix.Imaging.Drawing.Models;
using SP = skia::CodeBrix.Imaging.Drawing.Paths;
using SR = skia::CodeBrix.Imaging.Drawing.Planes;
using SS = skia::CodeBrix.Imaging.Drawing.Shapes;
using ST = skia::CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for the motion and formation tools: path point queries, travel past a path's ends,
/// rolling, trains (plain and text), combined bindings, formation targets for every arrival
/// order (from a geometry and from a mask), every arrival style's poses, schedules and the
/// formation binding - all shared managed arithmetic, so both assemblies must agree EXACTLY
/// (compared through the round-trip text of every float). Each scenario is written once per alias.
/// </summary>
public class MotionAndFormationParityTests
{
    private const string Signature =
        "M 40 120 C 80 40, 140 40, 160 120 S 240 200, 280 120 Q 320 60 360 120 T 440 120 A 40 30 15 1 1 520 120 L 560 80 Z m 20 20 h 30 v 30 z";

    private static readonly string RobotoPath = Path.Combine(SvgTestAssets.AssetsDirectory, "Fonts", "Roboto-Regular.ttf");

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string D(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static void Append(StringBuilder text, PointF point) => text.Append(F(point.X)).Append(',').Append(F(point.Y)).Append(';');

    private static void Append(StringBuilder text, SM.DrawingTransform t)
        => text.Append(F(t.Translation.X)).Append(',').Append(F(t.Translation.Y)).Append(',').Append(F(t.Rotation)).Append(',')
            .Append(F(t.ScaleX)).Append(',').Append(F(t.ScaleY)).Append(',').Append(F(t.Anchor.X)).Append(',').Append(F(t.Anchor.Y)).Append('|');

    private static void Append(StringBuilder text, NM.DrawingTransform t)
        => text.Append(F(t.Translation.X)).Append(',').Append(F(t.Translation.Y)).Append(',').Append(F(t.Rotation)).Append(',')
            .Append(F(t.ScaleX)).Append(',').Append(F(t.ScaleY)).Append(',').Append(F(t.Anchor.X)).Append(',').Append(F(t.Anchor.Y)).Append('|');

    private static void Append(StringBuilder text, SF.FormationPose p)
        => text.Append(F(p.X)).Append(',').Append(F(p.Y)).Append(',').Append(F(p.Rotation)).Append(',').Append(F(p.ScaleX)).Append(',').Append(F(p.ScaleY)).Append('|');

    private static void Append(StringBuilder text, NF.FormationPose p)
        => text.Append(F(p.X)).Append(',').Append(F(p.Y)).Append(',').Append(F(p.Rotation)).Append(',').Append(F(p.ScaleX)).Append(',').Append(F(p.ScaleY)).Append('|');

    private static IEnumerable<PointF> Probes()
    {
        for (int y = -20; y <= 260; y += 13)
        {
            for (int x = -20; x <= 620; x += 17) { yield return new PointF(x + 0.37f, y + 0.61f); }
        }
    }

    // ----- path queries -----

    private static string Queries(SP.PathGeometry g, SP.PathGeometry other)
    {
        var text = new StringBuilder();
        foreach (float tolerance in new[] { 0.25f, 1.5f })
        {
            foreach (PointF p in Probes())
            {
                text.Append(g.Contains(p, tolerance) ? '1' : '0').Append(F(g.DistanceToEdge(p, tolerance))).Append(',')
                    .Append(F(g.GetSignedDistance(p, tolerance))).Append(',');
                Append(text, g.GetNearestPoint(p, tolerance));
            }
            for (int dx = -300; dx <= 600; dx += 45) { text.Append(F(g.Distance(other, new PointF(dx, 7.5f), tolerance))).Append(';'); }
        }
        for (float f = -0.5f; f <= 1.5f; f += 0.0625f)
        {
            SP.PathPoint p = g.GetPointAt(f, true);
            Append(text, p.Position);
            text.Append(F(p.Distance)).Append(F(p.Angle)).Append(';');
        }
        return text.ToString();
    }

    private static string Queries(NP.PathGeometry g, NP.PathGeometry other)
    {
        var text = new StringBuilder();
        foreach (float tolerance in new[] { 0.25f, 1.5f })
        {
            foreach (PointF p in Probes())
            {
                text.Append(g.Contains(p, tolerance) ? '1' : '0').Append(F(g.DistanceToEdge(p, tolerance))).Append(',')
                    .Append(F(g.GetSignedDistance(p, tolerance))).Append(',');
                Append(text, g.GetNearestPoint(p, tolerance));
            }
            for (int dx = -300; dx <= 600; dx += 45) { text.Append(F(g.Distance(other, new PointF(dx, 7.5f), tolerance))).Append(';'); }
        }
        for (float f = -0.5f; f <= 1.5f; f += 0.0625f)
        {
            NP.PathPoint p = g.GetPointAt(f, true);
            Append(text, p.Position);
            text.Append(F(p.Distance)).Append(F(p.Angle)).Append(';');
        }
        return text.ToString();
    }

    [Fact]
    public void Path_point_queries_and_travel_past_the_ends_are_identical()
    {
        //Arrange
        SP.PathGeometry skiaPath = SP.PathGeometry.Parse(Signature).WithFillRule(SP.PathFillRule.EvenOdd);
        SP.PathGeometry skiaStar = SP.PathGeometry.Star(new PointF(100, 100), 60, 25, 7);
        NP.PathGeometry noSkiaPath = NP.PathGeometry.Parse(Signature).WithFillRule(NP.PathFillRule.EvenOdd);
        NP.PathGeometry noSkiaStar = NP.PathGeometry.Star(new PointF(100, 100), 60, 25, 7);

        //Assert
        Queries(noSkiaPath, noSkiaStar).Should().Be(Queries(skiaPath, skiaStar));
        Queries(noSkiaStar, noSkiaPath).Should().Be(Queries(skiaStar, skiaPath));
    }

    // ----- travel, rolling, trains, combined bindings -----

    private static string Motion(SD.DrawingSession session)
    {
        SP.PathGeometry hill = SP.PathGeometry.Parse("M 0 200 C 100 200, 150 80, 250 80 S 400 200, 500 200");
        var progress = new SA.FloatTrack { { 0, 0f }, { 2, 0.7f, SA.EasingKind.CubicIn }, { 3, 1.06f, u => u * (2 - u) }, { 4, 1f, SA.EasingKind.SineInOut } };
        session.AddLayer("Layer", Color.White);
        var wheel = new SS.CircleShape(0, 0, 14);
        var rolled = new SS.CircleShape(0, 0, 14);
        session.Animations.BindAlongPath(wheel, hill, progress, new SM.PathTravelOptions { RollRadius = 14f, OrientToPath = true, ExtendBeyondEnds = true, UprightAtProgress = 0.98f });
        session.Animations.BindRollAlongPath(rolled, hill, new SA.FloatTrack { { 0, 0f }, { 4, 1f, SA.EasingKind.QuadInOut } }, -9f);
        session.Animations.Bind(rolled, SM.AnimatedProperty.Rotation, t => (float)(5 * System.Math.Sin(t * 3)), SM.BindingMode.Add);
        session.Animations.Bind(rolled, SM.AnimatedProperty.Scale, new SA.FloatTrack { { 0, 0.5f }, { 4, 1.5f } }, SM.BindingMode.Multiply);
        var cars = new List<SM.DrawingElement>();
        for (int i = 0; i < 5; i++) { cars.Add(new SS.RectangleShape(i * 3, 0, 10, 6)); }
        session.Animations.BindTrainAlongPath(cars, hill, progress, new SM.PathTrainOptions
        {
            Spacing = 22f,
            SpacingScale = new SA.FloatTrack { { 0, 2f }, { 4, 0.5f } },
            Stagger = 0.13,
            Order = SM.StaggerOrder.CenterOut,
            OrientToPath = true,
            RollRadius = 6f,
            ScaleAlongPath = new SA.FloatTrack { { 0, 0.5f }, { 0.5, 1.3f }, { 1, 1f } },
            OpacityAlongPath = new SA.FloatTrack { { 0, 0f }, { 0.2, 1f } },
        });
        var text = new SS.TextBlockShape(session.LayoutText("Rolling in!", 36), 140, 260, Color.White);
        SM.PathTrain letters = session.Animations.BindTextAlongPath(text, ST.TextUnit.Character, hill, progress,
            new SM.PathTrainOptions { Stagger = 0.07, Order = SM.StaggerOrder.Reverse, RollOnBaseline = true, Spacing = 8f, SpacingScale = new SA.FloatTrack { { 0, 1f }, { 3, 0f } } });

        var result = new StringBuilder();
        for (double t = -0.25; t <= 4.5; t += 1.0 / 12)
        {
            session.Time = t;
            Append(result, wheel.Transform);
            Append(result, rolled.Transform);
            foreach (SM.DrawingElement car in cars) { Append(result, car.Transform); result.Append(F(car.Opacity)); }
            foreach (SM.DrawingElement piece in letters.Elements) { Append(result, piece.Transform); }
        }
        return result.ToString();
    }

    private static string Motion(ND.DrawingSession session)
    {
        NP.PathGeometry hill = NP.PathGeometry.Parse("M 0 200 C 100 200, 150 80, 250 80 S 400 200, 500 200");
        var progress = new NA.FloatTrack { { 0, 0f }, { 2, 0.7f, NA.EasingKind.CubicIn }, { 3, 1.06f, u => u * (2 - u) }, { 4, 1f, NA.EasingKind.SineInOut } };
        session.AddLayer("Layer", Color.White);
        var wheel = new NS.CircleShape(0, 0, 14);
        var rolled = new NS.CircleShape(0, 0, 14);
        session.Animations.BindAlongPath(wheel, hill, progress, new NM.PathTravelOptions { RollRadius = 14f, OrientToPath = true, ExtendBeyondEnds = true, UprightAtProgress = 0.98f });
        session.Animations.BindRollAlongPath(rolled, hill, new NA.FloatTrack { { 0, 0f }, { 4, 1f, NA.EasingKind.QuadInOut } }, -9f);
        session.Animations.Bind(rolled, NM.AnimatedProperty.Rotation, t => (float)(5 * System.Math.Sin(t * 3)), NM.BindingMode.Add);
        session.Animations.Bind(rolled, NM.AnimatedProperty.Scale, new NA.FloatTrack { { 0, 0.5f }, { 4, 1.5f } }, NM.BindingMode.Multiply);
        var cars = new List<NM.DrawingElement>();
        for (int i = 0; i < 5; i++) { cars.Add(new NS.RectangleShape(i * 3, 0, 10, 6)); }
        session.Animations.BindTrainAlongPath(cars, hill, progress, new NM.PathTrainOptions
        {
            Spacing = 22f,
            SpacingScale = new NA.FloatTrack { { 0, 2f }, { 4, 0.5f } },
            Stagger = 0.13,
            Order = NM.StaggerOrder.CenterOut,
            OrientToPath = true,
            RollRadius = 6f,
            ScaleAlongPath = new NA.FloatTrack { { 0, 0.5f }, { 0.5, 1.3f }, { 1, 1f } },
            OpacityAlongPath = new NA.FloatTrack { { 0, 0f }, { 0.2, 1f } },
        });
        var text = new NS.TextBlockShape(session.LayoutText("Rolling in!", 36), 140, 260, Color.White);
        NM.PathTrain letters = session.Animations.BindTextAlongPath(text, NT.TextUnit.Character, hill, progress,
            new NM.PathTrainOptions { Stagger = 0.07, Order = NM.StaggerOrder.Reverse, RollOnBaseline = true, Spacing = 8f, SpacingScale = new NA.FloatTrack { { 0, 1f }, { 3, 0f } } });

        var result = new StringBuilder();
        for (double t = -0.25; t <= 4.5; t += 1.0 / 12)
        {
            session.Time = t;
            Append(result, wheel.Transform);
            Append(result, rolled.Transform);
            foreach (NM.DrawingElement car in cars) { Append(result, car.Transform); result.Append(F(car.Opacity)); }
            foreach (NM.DrawingElement piece in letters.Elements) { Append(result, piece.Transform); }
        }
        return result.ToString();
    }

    [Fact]
    public void Rolling_trains_text_trains_and_combined_bindings_are_identical()
    {
        //Arrange
        using var skiaSession = new SD.DrawingSession(new SD.DrawingSessionOptions { CalibrationSize = new Size(600, 400) });
        skiaSession.Fonts.RegisterFont(RobotoPath);
        using var noSkiaSession = new ND.DrawingSession(new ND.DrawingSessionOptions { CalibrationSize = new Size(600, 400) });
        noSkiaSession.Fonts.RegisterFont(RobotoPath);

        //Assert
        Motion(noSkiaSession).Should().Be(Motion(skiaSession));
    }

    // ----- formations -----

    private static string Targets(List<SF.FormationTarget> targets)
    {
        var text = new StringBuilder().Append(targets.Count).Append(':');
        foreach (SF.FormationTarget t in targets) { Append(text, t.Position); text.Append(F(t.Key)).Append(',').Append(F(t.Clearance)).Append(';'); }
        return text.ToString();
    }

    private static string Targets(List<NF.FormationTarget> targets)
    {
        var text = new StringBuilder().Append(targets.Count).Append(':');
        foreach (NF.FormationTarget t in targets) { Append(text, t.Position); text.Append(F(t.Key)).Append(',').Append(F(t.Clearance)).Append(';'); }
        return text.ToString();
    }

    private static string Formation(SD.DrawingSession session)
    {
        var text = new StringBuilder();
        SP.PathGeometry word = SP.PathGeometry.Parse(Signature).Inflate(14f);
        SF.IArrivalOrder[] orders =
        {
            null, new SF.CenterOutArrivalOrder(), new SF.CenterOutArrivalOrder(true), new SF.HeapArrivalOrder(205f),
            new SF.DirectionalArrivalOrder(250f, 9f), new SF.CustomArrivalOrder((x, y, c) => (x * 0.3f) - c),
        };
        foreach (SF.IArrivalOrder order in orders)
        {
            text.Append(Targets(SF.FormationTargets.Fill(SF.FormationArea.FromGeometry(word, 0.5f), new SF.FormationTargetOptions { Spacing = 11f, Seed = 99, Order = order })));
        }
        var plane = new SR.DrawingPlane(60, 40);
        SR.DrawingNoise.Fill(plane, SR.DrawingNoiseKind.Gradient, 4, 0.08);
        SF.FormationArea mask = SF.FormationArea.FromMask(plane, new RectangleF(10, 20, 300, 200), 0.1f);
        text.Append(Targets(SF.FormationTargets.Fill(mask, new SF.FormationTargetOptions { Spacing = 7f, EdgeClearance = 3f, Order = new SF.HeapArrivalOrder(220f) })));
        for (float y = 0; y < 240; y += 9.5f) { text.Append(F(mask.DistanceToEdge(160.3f, y))).Append(mask.Contains(160.3f, y) ? 'i' : 'o'); }

        double[] times = new SF.ArrivalSchedule(2, 30, new SF.ArrivalRateProfile { RampUntil = 0.34, TaperFrom = 0.58, StartRate = 0.1 }).GetTimes(200);
        foreach (double t in times) { text.Append(D(t)).Append(';'); }
        var target = new SF.FormationPose(120, 90, 33, -0.6f, 0.9f);
        var start = new SF.FormationPose(-40, -30, 200, 2, 0.5f);
        var members = new List<SF.FormationMember>();
        for (int k = 0; k < 40; k++)
        {
            SF.IArrivalPath path = (k % 4) switch
            {
                0 => new SF.SwayingFallArrival(SR.DrawingRandom.ForKey(7, k), target, times[k * 5], -60f, s => 30 * System.Math.Sin(s * 0.4)),
                1 => new SF.StraightArrival(start, target, times[k * 5] - 2, times[k * 5], SA.EasingKind.QuartOut),
                2 => new SF.CurvedArrival(start, target, times[k * 5] - 3, times[k * 5], -0.6f),
                _ => new SF.SpiralArrival(target, times[k * 5] - 2.5, times[k * 5], 150f, k * 17f, 2.25f, 0.2f, true),
            };
            members.Add(k % 7 == 3 ? new SF.FormationMember(times[k * 5]) : new SF.FormationMember(path));
            text.Append(D(path.StartTime)).Append('/');
            for (double s = path.StartTime - 0.5; s <= path.ArrivalTime + 0.25; s += 0.3) { Append(text, path.GetPoseAt(s)); }
        }
        SD.Models.DrawingLayer rest = session.AddLayer("Rest", Color.White);
        SD.Models.DrawingLayer air = session.AddLayer("Air", Color.White);
        SF.FormationElements formation = session.Animations.BindFormation(members, rest, air,
            i => new SS.CircleShape(0, 0, 3), i => new SM.DrawingElement[] { new SS.CircleShape(0, 0, 3), new SS.RectangleShape(-2, -2, 4, 4) });
        foreach (int i in formation.ArrivalOrder) { text.Append(i).Append(','); }
        for (double t = 0; t <= 32; t += 0.7)
        {
            session.Time = t;
            text.Append(formation.GetTravellingCount(t)).Append(':');
            for (int i = 0; i < formation.Count; i++)
            {
                foreach (SM.DrawingElement part in formation.GetTravelling(i)) { Append(text, part.Transform); }
            }
        }
        return text.ToString();
    }

    private static string Formation(ND.DrawingSession session)
    {
        var text = new StringBuilder();
        NP.PathGeometry word = NP.PathGeometry.Parse(Signature).Inflate(14f);
        NF.IArrivalOrder[] orders =
        {
            null, new NF.CenterOutArrivalOrder(), new NF.CenterOutArrivalOrder(true), new NF.HeapArrivalOrder(205f),
            new NF.DirectionalArrivalOrder(250f, 9f), new NF.CustomArrivalOrder((x, y, c) => (x * 0.3f) - c),
        };
        foreach (NF.IArrivalOrder order in orders)
        {
            text.Append(Targets(NF.FormationTargets.Fill(NF.FormationArea.FromGeometry(word, 0.5f), new NF.FormationTargetOptions { Spacing = 11f, Seed = 99, Order = order })));
        }
        var plane = new NR.DrawingPlane(60, 40);
        NR.DrawingNoise.Fill(plane, NR.DrawingNoiseKind.Gradient, 4, 0.08);
        NF.FormationArea mask = NF.FormationArea.FromMask(plane, new RectangleF(10, 20, 300, 200), 0.1f);
        text.Append(Targets(NF.FormationTargets.Fill(mask, new NF.FormationTargetOptions { Spacing = 7f, EdgeClearance = 3f, Order = new NF.HeapArrivalOrder(220f) })));
        for (float y = 0; y < 240; y += 9.5f) { text.Append(F(mask.DistanceToEdge(160.3f, y))).Append(mask.Contains(160.3f, y) ? 'i' : 'o'); }

        double[] times = new NF.ArrivalSchedule(2, 30, new NF.ArrivalRateProfile { RampUntil = 0.34, TaperFrom = 0.58, StartRate = 0.1 }).GetTimes(200);
        foreach (double t in times) { text.Append(D(t)).Append(';'); }
        var target = new NF.FormationPose(120, 90, 33, -0.6f, 0.9f);
        var start = new NF.FormationPose(-40, -30, 200, 2, 0.5f);
        var members = new List<NF.FormationMember>();
        for (int k = 0; k < 40; k++)
        {
            NF.IArrivalPath path = (k % 4) switch
            {
                0 => new NF.SwayingFallArrival(NR.DrawingRandom.ForKey(7, k), target, times[k * 5], -60f, s => 30 * System.Math.Sin(s * 0.4)),
                1 => new NF.StraightArrival(start, target, times[k * 5] - 2, times[k * 5], NA.EasingKind.QuartOut),
                2 => new NF.CurvedArrival(start, target, times[k * 5] - 3, times[k * 5], -0.6f),
                _ => new NF.SpiralArrival(target, times[k * 5] - 2.5, times[k * 5], 150f, k * 17f, 2.25f, 0.2f, true),
            };
            members.Add(k % 7 == 3 ? new NF.FormationMember(times[k * 5]) : new NF.FormationMember(path));
            text.Append(D(path.StartTime)).Append('/');
            for (double s = path.StartTime - 0.5; s <= path.ArrivalTime + 0.25; s += 0.3) { Append(text, path.GetPoseAt(s)); }
        }
        ND.Models.DrawingLayer rest = session.AddLayer("Rest", Color.White);
        ND.Models.DrawingLayer air = session.AddLayer("Air", Color.White);
        NF.FormationElements formation = session.Animations.BindFormation(members, rest, air,
            i => new NS.CircleShape(0, 0, 3), i => new NM.DrawingElement[] { new NS.CircleShape(0, 0, 3), new NS.RectangleShape(-2, -2, 4, 4) });
        foreach (int i in formation.ArrivalOrder) { text.Append(i).Append(','); }
        for (double t = 0; t <= 32; t += 0.7)
        {
            session.Time = t;
            text.Append(formation.GetTravellingCount(t)).Append(':');
            for (int i = 0; i < formation.Count; i++)
            {
                foreach (NM.DrawingElement part in formation.GetTravelling(i)) { Append(text, part.Transform); }
            }
        }
        return text.ToString();
    }

    [Fact]
    public void Formation_targets_orders_arrival_styles_schedules_and_bindings_are_identical()
    {
        //Arrange
        using var skiaSession = new SD.DrawingSession(new SD.DrawingSessionOptions { CalibrationSize = new Size(600, 400) });
        using var noSkiaSession = new ND.DrawingSession(new ND.DrawingSessionOptions { CalibrationSize = new Size(600, 400) });

        //Act
        string skia = Formation(skiaSession);
        string noSkia = Formation(noSkiaSession);

        //Assert
        skia.Length.Should().BeGreaterThan(10000);
        noSkia.Should().Be(skia);
    }
}
