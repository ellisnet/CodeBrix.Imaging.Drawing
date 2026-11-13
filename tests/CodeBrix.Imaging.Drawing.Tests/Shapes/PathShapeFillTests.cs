using System;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

/// <summary>A filled path's thickness is not used: 0 is accepted when filled, and a fill-only constructor exists.</summary>
public class PathShapeFillTests
{
    private static readonly PathGeometry Star = PathGeometry.Star(new PointF(50, 50), 40, 18, 5);
    private static readonly Size Frame = new Size(100, 100);

    private static byte[] Draw(DrawingShape shape)
    {
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.Black });
        session.AddLayer("Layer", Color.White);
        session.DrawShape(shape);
        return session.ExportPixels(Frame);
    }

    [Fact]
    public void A_filled_path_accepts_a_thickness_of_zero_and_draws_like_any_thickness()
    {
        //Act
        var zero = new PathShape(Star, 0, Color.Orange, isFilled: true);
        var one = new PathShape(Star, 1, Color.Orange, isFilled: true);

        //Assert
        zero.StrokeThickness.Should().Be(0f);
        zero.IsFilled.Should().BeTrue();
        zero.IsStroked.Should().BeFalse();
        Draw(zero).Should().Equal(Draw(one));
        zero.GetBounds().Should().Be(one.GetBounds());
    }

    [Fact]
    public void The_fill_only_constructor_fills_in_the_colour_without_an_outline()
    {
        //Act
        var fill = new PathShape(Star, Color.Orange);

        //Assert
        fill.IsFilled.Should().BeTrue();
        fill.IsStroked.Should().BeFalse();
        fill.Color.Should().Be(Color.Orange);
        Draw(fill).Should().Equal(Draw(new PathShape(Star, 3, Color.Orange, isFilled: true)));
    }

    [Fact]
    public void An_outline_still_needs_a_positive_thickness_and_no_thickness_may_be_negative()
    {
        //Act
        Action outline = () => new PathShape(Star, 0, Color.Orange);
        Action negative = () => new PathShape(Star, -1, Color.Orange, isFilled: true);
        Action morph = () => new PathShape(new PathMorph(Star, PathGeometry.Circle(new PointF(50, 50), 30)), 0f, 0f, null, isFilled: true);
        Action drawPath = () =>
        {
            using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame });
            session.AddLayer("Layer", Color.White);
            session.DrawPath(Star, 0, null, filled: true);
        };

        //Assert
        outline.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
        morph.Should().NotThrow();
        drawPath.Should().NotThrow();
    }
}
