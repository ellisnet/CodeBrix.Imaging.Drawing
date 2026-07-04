using System;
using CodeBrix.Imaging.Drawing.Extensions;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionShapeTests
{
    private static DrawingSession CreateSession()
    {
        var session = new DrawingSession();
        session.AddLayer("Damage", SKColors.Red);
        return session;
    }

    [Fact]
    public void DrawLine_commits_shape_to_active_layer()
    {
        //Arrange
        using DrawingSession session = CreateSession();

        //Act
        session.DrawLine(100, 100, 400, 250);

        //Assert
        session.ActiveLayer.ElementCount.Should().Be(1);
        session.HasStrokes.Should().BeTrue();
        session.StrokeCount.Should().Be(1);
    }

    [Fact]
    public void Draw_methods_raise_events()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        var changedCount = 0;
        var redrawCount = 0;
        session.DrawingChanged += (s, e) => changedCount++;
        session.RedrawRequested += (s, e) => redrawCount++;

        //Act
        session.DrawCircle(500, 500, 100);
        session.DrawArrow(100, 100, 300, 300);

        //Assert
        changedCount.Should().Be(2);
        redrawCount.Should().Be(2);
    }

    [Fact]
    public void Draw_methods_accept_imaging_colors()
    {
        //Arrange - the color parameter is CodeBrix.Imaging's Color type, not a Skia type
        using DrawingSession session = CreateSession();

        //Act
        session.DrawCircle(500, 500, 200, thickness: 20, color: Color.FromRgb(0, 0, 255));

        //Assert - the exported drawing carries a blue circle rim, not the layer's red
        session.LayerOpacity = 255;
        session.BackgroundFillColor = SKColors.White;
        byte[] png = session.ExportPng(new SKSizeI(200, 200));
        using SKBitmap decoded = SKBitmap.Decode(png);
        SKColor rim = decoded.GetPixel(100, 60); //calibrated (500, 300): top of the circle
        rim.Blue.Should().Be((byte)255);
        rim.Red.Should().Be((byte)0);
    }

    [Fact]
    public void DrawPolyline_accepts_plain_tuples()
    {
        //Arrange
        using DrawingSession session = CreateSession();

        //Act - no Skia point types needed
        session.DrawPolyline(new (float X, float Y)[] { (100, 100), (500, 500), (900, 100) });

        //Assert
        session.ActiveLayer.ElementCount.Should().Be(1);
    }

    [Fact]
    public void UndoLastStroke_removes_a_committed_shape()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawRectangle(100, 100, 300, 200);

        //Act
        bool undone = session.UndoLastStroke();

        //Assert
        undone.Should().BeTrue();
        session.HasStrokes.Should().BeFalse();
    }

    [Fact]
    public void Draw_methods_require_an_active_layer()
    {
        //Arrange
        using var session = new DrawingSession();
        Action act = () => session.DrawLine(0, 0, 10, 10);

        //Act + Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Clear_removes_shapes_too()
    {
        //Arrange
        using DrawingSession session = CreateSession();
        session.DrawCircle(500, 500, 100);
        session.DrawLine(0, 0, 100, 100);

        //Act
        session.Clear();

        //Assert
        session.HasStrokes.Should().BeFalse();
        session.ActiveLayer.ElementCount.Should().Be(0);
    }

    [Fact]
    public void Color_bridge_round_trips()
    {
        //Arrange
        Color original = Color.FromRgba(12, 34, 56, 200);

        //Act
        SKColor skColor = original.ToSKColor();
        Color roundTripped = skColor.ToImagingColor();

        //Assert
        skColor.Red.Should().Be((byte)12);
        skColor.Green.Should().Be((byte)34);
        skColor.Blue.Should().Be((byte)56);
        skColor.Alpha.Should().Be((byte)200);
        roundTripped.Should().Be(original);
    }
}
