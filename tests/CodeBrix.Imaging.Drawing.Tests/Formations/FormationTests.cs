using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Formations;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Formations;

public class FormationTests
{
    private static readonly PathGeometry Disc = PathGeometry.Circle(new PointF(200, 200), 100);

    //A thick "U": two legs on a ground line at y = 300 joined at the bottom
    private static readonly PathGeometry Cup = PathGeometry.FromPoints(new[]
    {
        new PointF(100, 100), new PointF(150, 100), new PointF(150, 250), new PointF(250, 250), new PointF(250, 100),
        new PointF(300, 100), new PointF(300, 300), new PointF(100, 300),
    }, closed: true);

    [Fact]
    public void Fill_gives_the_same_spots_for_the_same_seed_inside_and_clear_of_the_edge()
    {
        //Arrange
        var options = new FormationTargetOptions { Spacing = 12f, EdgeClearance = 5f, Seed = 7 };

        //Act
        List<FormationTarget> a = FormationTargets.Fill(Disc, options);
        List<FormationTarget> b = FormationTargets.Fill(Disc, options);
        List<FormationTarget> other = FormationTargets.Fill(Disc, new FormationTargetOptions { Spacing = 12f, EdgeClearance = 5f, Seed = 8 });

        //Assert
        a.Count.Should().BeGreaterThan(150);
        b.Select(t => t.Position).Should().Equal(a.Select(t => t.Position));
        other.Select(t => t.Position).Should().NotEqual(a.Select(t => t.Position));
        foreach (FormationTarget target in a)
        {
            Disc.Contains(target.Position).Should().BeTrue();
            target.Clearance.Should().BeGreaterThanOrEqualTo(5f);
        }
        a.Select(t => t.Key).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Center_out_fills_from_the_middle_and_reversed_from_the_outside()
    {
        //Act
        List<FormationTarget> outward = FormationTargets.Fill(Disc, new FormationTargetOptions { Order = new CenterOutArrivalOrder() });
        List<FormationTarget> inward = FormationTargets.Fill(Disc, new FormationTargetOptions { Order = new CenterOutArrivalOrder(reverse: true) });

        //Assert
        Distance(outward[0].Position).Should().BeLessThan(15f);
        Distance(outward[^1].Position).Should().BeGreaterThan(80f);
        Distance(inward[0].Position).Should().BeGreaterThan(80f);
    }

    private static float Distance(PointF p) => MathF.Sqrt(((p.X - 200) * (p.X - 200)) + ((p.Y - 200) * (p.Y - 200)));

    [Fact]
    public void Heap_order_grows_from_the_ground_up_each_leg()
    {
        //Act
        List<FormationTarget> spots = FormationTargets.Fill(Cup, new FormationTargetOptions
        {
            Spacing = 10f,
            Order = new HeapArrivalOrder(300f) { Scatter = 0f, Lumpiness = 0f },
        });

        //Assert - the first tenth sits low, the last tenth high (the tops of the legs)
        int tenth = spots.Count / 10;
        spots.Take(tenth).Average(t => t.Position.Y).Should().BeGreaterThan(260f);
        spots.Skip(spots.Count - tenth).Average(t => t.Position.Y).Should().BeLessThan(150f);
    }

    [Fact]
    public void Directional_and_custom_orders_sort_as_their_keys_say()
    {
        //Act
        List<FormationTarget> leftToRight = FormationTargets.Fill(Disc, new FormationTargetOptions { Order = new DirectionalArrivalOrder(0f) });
        List<FormationTarget> deepFirst = FormationTargets.Fill(Disc, new FormationTargetOptions
        {
            Order = new CustomArrivalOrder((x, y, clearance) => -clearance),
        });

        //Assert
        leftToRight.Select(t => t.Position.X).Should().BeInAscendingOrder();
        deepFirst.Select(t => t.Clearance).Should().BeInDescendingOrder();
    }

    [Fact]
    public void A_mask_area_matches_its_pixels()
    {
        //Arrange - a 40 x 20 plane with a 10 x 10 block at (10, 5), stretched x2 over (100, 0)
        var plane = new DrawingPlane(40, 20);
        for (int y = 5; y < 15; y++)
        {
            for (int x = 10; x < 20; x++) { plane.Data[(y * 40) + x] = 1f; }
        }

        //Act
        FormationArea area = FormationArea.FromMask(plane, new RectangleF(100, 0, 80, 40));

        //Assert
        area.Bounds.Should().Be(new RectangleF(120, 10, 20, 20));
        area.Contains(130, 20).Should().BeTrue();
        area.Contains(119, 20).Should().BeFalse();
        area.Geometry.Should().BeNull();
        area.DistanceToEdge(131, 21).Should().Be(10f);
        FormationTargets.Fill(area, new FormationTargetOptions { Spacing = 4f, EdgeClearance = 2f }).Count.Should().BeGreaterThan(10);
    }

    [Fact]
    public void Every_arrival_style_starts_where_it_should_and_ends_exactly_on_its_target()
    {
        //Arrange
        var target = new FormationPose(50, 60, 30, 0.5f, 0.8f);
        var start = new FormationPose(-100, -50, -90, 2f, 2f);
        IArrivalPath[] styles =
        {
            new StraightArrival(start, target, 1, 3),
            new CurvedArrival(start, target, 1, 3, 0.5f),
            new SpiralArrival(target, 1, 3, 120f, 45f, 2f, 3f, spinsWithPath: true),
            new SwayingFallArrival(new DrawingRandom(5), target, 3, -40f),
        };

        //Assert
        foreach (IArrivalPath path in styles)
        {
            path.GetPoseAt(path.ArrivalTime).Should().Be(target);
            path.GetPoseAt(path.ArrivalTime + 10).Should().Be(target);
            path.GetPoseAt(path.ArrivalTime - 0.001).Position.X.Should().BeApproximately(50f, 1f);
        }
        styles[0].GetPoseAt(1).Should().Be(start);
        styles[1].GetPoseAt(0).Position.Should().Be(start.Position);
        PointF spiralStart = styles[2].GetPoseAt(1).Position;
        MathF.Sqrt(((spiralStart.X - 50) * (spiralStart.X - 50)) + ((spiralStart.Y - 60) * (spiralStart.Y - 60))).Should().BeApproximately(120f, 1e-3f);
        styles[3].GetPoseAt(styles[3].StartTime).Y.Should().BeApproximately(-40f, 0.01f);
        styles[3].StartTime.Should().BeLessThan(3 - 0.5);
    }

    [Fact]
    public void A_curved_arrival_bows_to_the_right_of_its_direction()
    {
        //Arrange - travelling right along y = 0; right of travel is down the screen
        var path = new CurvedArrival(new FormationPose(0, 0), new FormationPose(100, 0), 0, 1, 0.5f, EasingKind.Linear);

        //Assert
        path.GetPoseAt(0.5).Y.Should().BeGreaterThan(10f);
    }

    [Fact]
    public void The_schedule_starts_and_ends_on_time_and_crowds_its_middle()
    {
        //Arrange
        var schedule = new ArrivalSchedule(10, 40);

        //Act
        double[] times = schedule.GetTimes(301);

        //Assert
        times[0].Should().Be(10);
        times[^1].Should().Be(40);
        for (int i = 1; i < times.Length; i++) { times[i].Should().BeGreaterThanOrEqualTo(times[i - 1]); }
        double early = times[30] - times[0];
        double middle = times[180] - times[150];
        middle.Should().BeLessThan(early);
        schedule.GetTimes(0).Length.Should().Be(0);
    }

    [Fact]
    public void A_schedule_with_a_flat_rate_spaces_evenly()
    {
        //Act
        double[] times = new ArrivalSchedule(0, 10, u => 1.0).GetTimes(11);

        //Assert
        for (int i = 0; i < 11; i++) { times[i].Should().BeApproximately(i, 1e-9); }
    }

    [Fact]
    public void BindFormation_adds_resting_elements_in_arrival_order_and_poses_the_travellers()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(400, 400), LayerOpacity = 255 });
        DrawingLayer rest = session.AddLayer("Rest", Color.White);
        DrawingLayer air = session.AddLayer("Air", Color.White);
        var members = new List<FormationMember>
        {
            new FormationMember(new StraightArrival(new FormationPose(0, 0), new FormationPose(100, 100), 0, 2)),
            new FormationMember(1.0),
            new FormationMember(new StraightArrival(new FormationPose(0, 0), new FormationPose(300, 100), 0.5, 1.5)),
        };

        //Act
        FormationElements formation = session.Animations.BindFormation(members, rest, air,
            i => new CircleShape(0, 0, 3) { Transform = members[i].Path?.Target.ToTransform() ?? DrawingTransform.Identity },
            i => new DrawingElement[] { new RectangleShape(-2, -2, 4, 4) });
        session.Time = 1.0;

        //Assert
        formation.ArrivalOrder.Should().Equal(1, 2, 0);
        rest.GetElements().Should().Equal(formation.GetResting(1), formation.GetResting(2), formation.GetResting(0));
        formation.GetResting(0).StartTime.Should().Be(2);
        formation.GetTravelling(1).Count.Should().Be(0);
        DrawingElement traveller = formation.GetTravelling(0)[0];
        traveller.StartTime.Should().Be(0);
        traveller.EndTime.Should().Be(2);
        traveller.Transform.Should().Be(members[0].Path.GetPoseAt(1.0).ToTransform());
        formation.GetTravellingCount(1.0).Should().Be(2);
        formation.GetTravellingCount(1.6).Should().Be(1);
    }

    private static DrawingSession FallingDots()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(100, 100), LayerOpacity = 255, BackgroundFillColor = Color.Black });
        DrawingLayer rest = session.AddLayer("Rest", Color.White);
        DrawingLayer air = session.AddLayer("Air", Color.White);
        var members = new List<FormationMember>();
        for (int i = 0; i < 20; i++)
        {
            members.Add(new FormationMember(new StraightArrival(new FormationPose(50, -10), new FormationPose(5 + (i * 4.5f), 90), i * 0.1, (i * 0.1) + 1)));
        }
        session.Animations.BindFormation(members, rest, air,
            i => new CircleShape(members[i].Path.Target.X, members[i].Path.Target.Y, 2, 1, null, isFilled: true),
            i => new DrawingElement[] { new CircleShape(0, 0, 2, 1, null, isFilled: true) });
        return session;
    }

    [Fact]
    public void A_formation_s_resting_layer_draws_incrementally_and_matches_a_fresh_render()
    {
        //Arrange
        using DrawingSession session = FallingDots();
        var info = new SKImageInfo(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        session.Time = 1.0;
        session.Render(canvas, info);
        int redraws = session.Renderer.LayerFullRedrawCount;

        //Act - twenty frames played forward, more dots landing
        for (int frame = 1; frame <= 20; frame++)
        {
            session.Time = 1.0 + (frame * 0.1);
            session.Render(canvas, info);
        }
        using DrawingSession fresh = FallingDots();
        fresh.Time = session.Time;
        using var freshBitmap = new SKBitmap(info);
        using var freshCanvas = new SKCanvas(freshBitmap);
        fresh.Render(freshCanvas, info);

        //Assert - at most the moving layer redraws, once a frame; the resting layer only appends
        (session.Renderer.LayerFullRedrawCount - redraws).Should().BeLessThanOrEqualTo(20);
        bitmap.Bytes.Should().Equal(freshBitmap.Bytes);
    }
}
