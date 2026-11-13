using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.SafeAreas;

/// <summary>Safe-area checks measure what the frame shows: transformed, time-windowed and camera-moved bounds.</summary>
public class SafeAreaTransformedCheckTests
{
    private static readonly Size Frame = new Size(1000, 1000);

    private static (DrawingSession Session, DrawingShape Box) CreateSessionWithCentredBox()
    {
        var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame });
        session.AddLayer("Titles", Color.White).IsSafeAreaChecked = true;
        DrawingShape box = session.DrawRectangle(400, 400, 200, 200, filled: true);
        return (session, box);
    }

    [Fact]
    public void An_element_moved_into_a_margin_by_its_transform_is_reported()
    {
        //Arrange
        (DrawingSession session, DrawingShape box) = CreateSessionWithCentredBox();
        using DrawingSession owned = session;
        IReadOnlyList<SafeAreaFinding> before = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);

        //Act
        box.Transform = DrawingTransform.Translate(0, -380);
        IReadOnlyList<SafeAreaFinding> after = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);

        //Assert - the box now spans y 20..220; the top margin is 0..50
        before.Count.Should().Be(0);
        after.Count.Should().Be(1);
        after[0].ZoneName.Should().Be("top margin");
        RectangleF overlap = after[0].Overlap.Value;
        overlap.X.Should().Be(400f);
        overlap.Y.Should().Be(20f);
        overlap.Width.Should().Be(200f);
        overlap.Height.Should().BeApproximately(30f, 1e-3f);
    }

    [Fact]
    public void A_layer_transform_and_the_camera_count_too()
    {
        //Arrange
        (DrawingSession session, DrawingShape _) = CreateSessionWithCentredBox();
        using DrawingSession owned = session;

        //Act
        session.ActiveLayer.Transform = DrawingTransform.Scale(5f, new PointF(500, 500));
        IReadOnlyList<SafeAreaFinding> scaled = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);
        session.ActiveLayer.Transform = DrawingTransform.Identity;
        session.Camera = new DrawingCamera { Pan = new PointF(-420, 0) };
        IReadOnlyList<SafeAreaFinding> panned = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);
        session.ActiveLayer.Parallax = 0f;
        IReadOnlyList<SafeAreaFinding> fixedToFrame = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);

        //Assert
        scaled.Count.Should().Be(4);
        panned.Count.Should().Be(1);
        panned[0].ZoneName.Should().Be("right margin");
        fixedToFrame.Count.Should().Be(0);
    }

    [Fact]
    public void Elements_outside_their_time_window_are_not_checked()
    {
        //Arrange
        (DrawingSession session, DrawingShape box) = CreateSessionWithCentredBox();
        using DrawingSession owned = session;
        box.Transform = DrawingTransform.Translate(0, -380);
        box.StartTime = 2;

        //Act
        IReadOnlyList<SafeAreaFinding> early = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);
        session.Time = 2;
        IReadOnlyList<SafeAreaFinding> later = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);

        //Assert
        early.Count.Should().Be(0);
        later.Count.Should().Be(1);
    }

    [Fact]
    public void A_group_is_measured_by_its_children_at_the_session_time()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame });
        session.AddLayer("Titles", Color.White).IsSafeAreaChecked = true;
        var group = new DrawingGroup();
        group.Add(new RectangleShape(400, 400, 100, 100, 1, null, isFilled: true));
        group.Add(new RectangleShape(400, 960, 100, 30, 1, null, isFilled: true)).EndTime = 1;
        session.DrawShape(group);

        //Act
        IReadOnlyList<SafeAreaFinding> early = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);
        session.Time = 1;
        IReadOnlyList<SafeAreaFinding> later = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe);

        //Assert
        early.Count.Should().Be(1);
        early[0].ZoneName.Should().Be("bottom margin");
        later.Count.Should().Be(0);
    }
}
