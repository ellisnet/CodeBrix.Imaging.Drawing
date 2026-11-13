using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class PathTravelTests
{
    //A straight, level road 200 units long
    private static readonly PathGeometry Road = PathGeometry.Line(new PointF(0, 50), new PointF(200, 50));

    private static FloatTrack Progress() => new FloatTrack
    {
        { 0, 0f },
        { 2, 0.6f, EasingKind.CubicIn },
        { 3, 1.05f, u => u * (2 - u) },
        { 4, 1f, EasingKind.SineInOut },
    };

    [Fact]
    public void A_roll_turns_by_distance_over_radius_and_is_upright_at_rest()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var wheel = new CircleShape(0, 0, 20);
        animations.BindRollAlongPath(wheel, Road, new FloatTrack { { 0, 0f }, { 1, 1f } }, 20f);

        //Act
        animations.Apply(0);
        float start = wheel.Transform.Rotation;
        animations.Apply(1);
        float end = wheel.Transform.Rotation;

        //Assert - 200 units on a radius of 20: 10 radians, clockwise, so -10 rad at the start
        start.Should().BeApproximately((float)(-10 * 180 / Math.PI), 1e-3f);
        end.Should().Be(0f);
        wheel.Transform.Translation.Should().Be(new PointF(200, 50));
    }

    [Fact]
    public void The_roll_equals_a_rotation_track_mirrored_from_the_progress_track_bit_for_bit()
    {
        //Arrange - the hand-made way: the progress keys copied into a rotation track
        FloatTrack progress = Progress();
        const float radius = 13f;
        const float upright = 0.95f;
        double degreesPerFraction = Road.GetLength() / radius * 180.0 / Math.PI;
        var mirrored = new FloatTrack();
        foreach (Keyframe<float> key in progress.GetKeyframes()) { mirrored.Add(key.WithValue((float)((key.Value - upright) * degreesPerFraction))); }
        var animations = new DrawingAnimations();
        var wheel = new CircleShape(0, 0, radius);
        animations.BindAlongPath(wheel, Road, progress, new PathTravelOptions { RollRadius = radius, UprightAtProgress = upright, ExtendBeyondEnds = true });

        //Assert
        for (double t = 0; t <= 4.0; t += 1.0 / 30)
        {
            animations.Apply(t);
            wheel.Transform.Rotation.Should().Be(mirrored.Evaluate(t));
        }
    }

    [Fact]
    public void Facing_and_rolling_add()
    {
        //Arrange - a road heading straight down: facing is 90 degrees
        PathGeometry down = PathGeometry.Line(new PointF(0, 0), new PointF(0, 100));
        var animations = new DrawingAnimations();
        var plain = new CircleShape(0, 0, 10);
        var both = new CircleShape(0, 0, 10);
        animations.BindRollAlongPath(plain, down, new FloatTrack { { 0, 0.5f } }, 10f);
        animations.BindRollAlongPath(both, down, new FloatTrack { { 0, 0.5f } }, 10f, orientToPath: true);

        //Act
        animations.Apply(0);

        //Assert
        both.Transform.Rotation.Should().BeApproximately(90f + plain.Transform.Rotation, 1e-3f);
    }

    [Fact]
    public void Extending_beyond_the_end_carries_an_overshoot_past_the_path()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var clamped = new CircleShape(0, 0, 5);
        var extended = new CircleShape(0, 0, 5);
        animations.BindAlongPath(clamped, Road, new FloatTrack { { 0, 1.1f } }, new PathTravelOptions());
        animations.BindAlongPath(extended, Road, new FloatTrack { { 0, 1.1f } }, new PathTravelOptions { ExtendBeyondEnds = true });

        //Act
        animations.Apply(0);

        //Assert
        clamped.Transform.Translation.Should().Be(new PointF(200, 50));
        extended.Transform.Translation.X.Should().BeApproximately(220f, 1e-3f);
    }

    [Fact]
    public void Travel_options_reject_values_that_are_not_finite()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var shape = new CircleShape(0, 0, 5);

        //Act
        Action roll = () => animations.BindAlongPath(shape, Road, Progress(), new PathTravelOptions { RollRadius = Single.NaN });
        Action zero = () => animations.BindRollAlongPath(shape, Road, Progress(), 0f);

        //Assert
        roll.Should().Throw<ArgumentOutOfRangeException>();
        zero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_train_keeps_its_spacing_behind_the_lead()
    {
        //Arrange - three markers 30 apart, the first leading
        var animations = new DrawingAnimations();
        var cars = new List<DrawingElement> { new CircleShape(0, 0, 4), new CircleShape(0, 0, 4), new CircleShape(0, 0, 4) };
        PathTrain train = animations.BindTrainAlongPath(cars, Road, new FloatTrack { { 0, 0.5f } }, new PathTrainOptions { Spacing = 30f });

        //Act
        animations.Apply(0);

        //Assert
        cars[0].Transform.Translation.Should().Be(new PointF(100, 50));
        cars[1].Transform.Translation.Should().Be(new PointF(70, 50));
        cars[2].Transform.Translation.Should().Be(new PointF(40, 50));
        train.GetDistance(2, 0).Should().Be(40f);
        train.Cars[2].Rank.Should().Be(2);
    }

    [Fact]
    public void A_train_s_gaps_close_with_the_spacing_scale_and_followers_lag_by_the_stagger()
    {
        //Arrange - reversed: the last element leads
        var animations = new DrawingAnimations();
        var cars = new List<DrawingElement> { new CircleShape(0, 0, 4), new CircleShape(0, 0, 4) };
        animations.BindTrainAlongPath(cars, Road, new FloatTrack { { 0, 0f }, { 2, 1f } }, new PathTrainOptions
        {
            Spacing = 20f,
            SpacingScale = new FloatTrack { { 0, 2f }, { 2, 0f } },
            Stagger = 0.5,
            Order = StaggerOrder.Reverse,
        });

        //Act
        animations.Apply(1);

        //Assert - lead (index 1) at 100; the follower follows the track at 0.5 s (50) less 20 x 1
        cars[1].Transform.Translation.Should().Be(new PointF(100, 50));
        cars[0].Transform.Translation.Should().Be(new PointF(30, 50));
    }

    [Fact]
    public void A_train_scales_and_fades_each_element_by_its_own_progress()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var cars = new List<DrawingElement> { new CircleShape(0, 0, 4), new CircleShape(0, 0, 4) };
        animations.BindTrainAlongPath(cars, Road, new FloatTrack { { 0, 1f } }, new PathTrainOptions
        {
            Spacing = 100f,
            ScaleAlongPath = new FloatTrack { { 0, 1f }, { 1, 3f } },
            OpacityAlongPath = new FloatTrack { { 0, 0f }, { 0.5, 1f } },
        });

        //Act
        animations.Apply(0);

        //Assert
        cars[0].Transform.ScaleX.Should().Be(3f);
        cars[1].Transform.ScaleX.Should().Be(2f);
        cars[0].Opacity.Should().Be(1f);
        cars[1].Opacity.Should().Be(1f);
    }

    [Fact]
    public void Train_elements_roll_upright_where_they_rest()
    {
        //Arrange
        var animations = new DrawingAnimations();
        var cars = new List<DrawingElement> { new CircleShape(0, 0, 10), new CircleShape(0, 0, 10) };
        animations.BindTrainAlongPath(cars, Road, new FloatTrack { { 0, 0f }, { 1, 1f } }, new PathTrainOptions
        {
            Spacing = 40f,
            RollRadius = 10f,
            OrientToPath = true,
        });

        //Act
        animations.Apply(1);

        //Assert
        cars[0].Transform.Rotation.Should().Be(0f);
        cars[1].Transform.Rotation.Should().Be(0f);
        cars[1].Transform.Translation.Should().Be(new PointF(160, 50));
    }

    [Fact]
    public void Letters_travel_along_a_path_and_settle_exactly_where_the_block_draws_them()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(400, 200) });
        session.Fonts.RegisterFont(TestAssets.RobotoPath);
        TextLayoutResult layout = session.LayoutText("Rolling in", 40);
        var text = new TextBlockShape(layout, 120, 80, Color.White);
        //A swoop that ends heading right (along the baseline), so facing the path ends upright
        PathGeometry route = new PathGeometryBuilder().MoveTo(new PointF(-200, 0))
            .CubicTo(new PointF(-100, 200), new PointF(0, 120), new PointF(100, 120)).Build();
        var progress = new FloatTrack { { 0, 0f }, { 2, 1f, EasingKind.CubicOut } };

        //Act
        PathTrain train = session.Animations.BindTextAlongPath(text, TextUnit.Character, route, progress, new PathTrainOptions
        {
            Stagger = 0.1,
            Order = StaggerOrder.Reverse,
            RollOnBaseline = true,
            OrientToPath = true,
        });
        session.Time = 2 + (0.1 * 9);

        //Assert - "Rolling in" has nine inked characters; every one at rest is untransformed
        train.Elements.Count.Should().Be(9);
        foreach (DrawingElement piece in train.Elements)
        {
            piece.Transform.Translation.Should().Be(new PointF(0, 0));
            piece.Transform.Rotation.Should().Be(0f);
        }
        foreach (PathTrainCar car in train.Cars) { car.RollRadius.Should().BeGreaterThan(0f); }
    }

    [Fact]
    public void A_text_train_in_flight_follows_copies_of_the_route()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(400, 200) });
        session.Fonts.RegisterFont(TestAssets.RobotoPath);
        var text = new TextBlockShape(session.LayoutText("AB", 40), 100, 50, Color.White);
        PathGeometry route = PathGeometry.Line(new PointF(0, 0), new PointF(100, 0));
        PathTrain train = session.Animations.BindTextAlongPath(text, TextUnit.Character, route, new FloatTrack { { 0, 0f }, { 1, 1f } });

        //Act
        session.Time = 0.5;

        //Assert - both pieces halfway: moved back by half the route
        train.Elements[0].Transform.Translation.Should().Be(new PointF(-50, 0));
        train.Elements[1].Transform.Translation.Should().Be(new PointF(-50, 0));
    }
}
