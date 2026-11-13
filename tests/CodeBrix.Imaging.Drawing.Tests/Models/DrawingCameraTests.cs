using System;
using CodeBrix.Imaging.Drawing.Models;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Models;

public class DrawingCameraTests
{
    private static readonly Size Calibration = new Size(200, 100);

    private static void ShouldBeNear(PointF actual, float x, float y)
    {
        actual.X.Should().BeApproximately(x, 1e-3f);
        actual.Y.Should().BeApproximately(y, 1e-3f);
    }

    [Fact]
    public void A_new_camera_changes_nothing()
    {
        //Arrange
        var camera = new DrawingCamera();

        //Act + Assert
        camera.IsIdentity.Should().BeTrue();
        ShouldBeNear(camera.ToView(new PointF(30, 40), Calibration), 30, 40);
    }

    [Fact]
    public void Panning_slides_the_scene_the_other_way()
    {
        //Arrange
        var camera = new DrawingCamera { Pan = new PointF(50, -10) };

        //Act + Assert
        ShouldBeNear(camera.ToView(new PointF(100, 50), Calibration), 50, 60);
    }

    [Fact]
    public void Zoom_and_roll_happen_about_the_pivot()
    {
        //Arrange - default pivot is the centre (100, 50)
        var zoom = new DrawingCamera { Zoom = 2 };
        var roll = new DrawingCamera { Rotation = 90 };

        //Act + Assert
        ShouldBeNear(zoom.ToView(new PointF(110, 50), Calibration), 120, 50);
        ShouldBeNear(zoom.ToView(new PointF(100, 50), Calibration), 100, 50);
        ShouldBeNear(roll.ToView(new PointF(110, 50), Calibration), 100, 40);
    }

    [Fact]
    public void Parallax_scales_the_camera_and_zero_ignores_it()
    {
        //Arrange
        var camera = new DrawingCamera { Pan = new PointF(40, 0), Zoom = 4, Pivot = new PointF(0, 0) };

        //Act + Assert
        ShouldBeNear(camera.ToView(new PointF(10, 10), Calibration, 0f), 10, 10);
        ShouldBeNear(camera.ToView(new PointF(30, 10), Calibration, 0.5f), 20, 20);
        ShouldBeNear(camera.ToView(new PointF(50, 10), Calibration, 1f), 40, 40);
    }

    [Fact]
    public void ToScene_inverts_ToView()
    {
        //Arrange
        var camera = new DrawingCamera { Pan = new PointF(12, 7), Zoom = 1.5f, Rotation = 30, Pivot = new PointF(60, 40) };

        //Act
        PointF back = camera.ToScene(camera.ToView(new PointF(33, 21), Calibration), Calibration);

        //Assert
        ShouldBeNear(back, 33, 21);
    }

    [Fact]
    public void Zoom_must_be_positive()
    {
        //Arrange
        var camera = new DrawingCamera();
        Action act = () => camera.Zoom = 0;

        //Act + Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
