using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Covers reading a shader back out: whatever was handed to a factory method must come
/// back through the inspection properties, so a consumer can re-emit the paint source
/// (a PDF gradient, for example) instead of sampling pixels.
/// </summary>
public class DrawingShaderTests
{
    private static readonly DrawingColor[] Stops =
    {
        DrawingColors.Red,
        DrawingColors.Green,
        DrawingColors.Blue,
    };

    private static readonly float[] Positions = { 0f, 0.25f, 1f };

    [Fact]
    public void A_color_shader_reports_its_color()
    {
        //Arrange + Act
        DrawingShader shader = DrawingShader.CreateColor(DrawingColors.Orange);

        //Assert
        shader.Kind.Should().Be(DrawingShaderKind.Color);
        shader.Color.Should().Be(DrawingColors.Orange);
        shader.Colors.Should().BeNull();
        shader.LocalMatrix.Should().BeNull();
    }

    [Fact]
    public void A_linear_gradient_reports_the_factory_inputs()
    {
        //Arrange
        Matrix3x2 localMatrix = Matrix3x2.CreateScale(2f, 3f);

        //Act
        DrawingShader shader = DrawingShader.CreateLinearGradient(
            new DrawingPoint(5f, 6f), new DrawingPoint(40f, 50f), Stops, Positions,
            DrawingShaderTileMode.Mirror, localMatrix);

        //Assert
        shader.Kind.Should().Be(DrawingShaderKind.LinearGradient);
        shader.Start.Should().Be(new DrawingPoint(5f, 6f));
        shader.End.Should().Be(new DrawingPoint(40f, 50f));
        shader.Colors.Should().Equal(Stops);
        shader.Positions.Should().Equal(Positions);
        shader.TileMode.Should().Be(DrawingShaderTileMode.Mirror);
        shader.LocalMatrix.Should().Be(localMatrix);
    }

    [Fact]
    public void A_radial_gradient_reports_its_center_and_radius()
    {
        //Arrange + Act
        DrawingShader shader = DrawingShader.CreateRadialGradient(
            new DrawingPoint(20f, 24f), 12f, Stops, null, DrawingShaderTileMode.Clamp);

        //Assert
        shader.Kind.Should().Be(DrawingShaderKind.RadialGradient);
        shader.Center.Should().Be(new DrawingPoint(20f, 24f));
        shader.Radius.Should().Be(12f);
        shader.Positions.Should().BeNull(); //null means evenly spaced, as it was given
        shader.TileMode.Should().Be(DrawingShaderTileMode.Clamp);
    }

    [Fact]
    public void A_two_point_conical_gradient_reports_both_circles()
    {
        //Arrange + Act
        DrawingShader shader = DrawingShader.CreateTwoPointConicalGradient(
            new DrawingPoint(4f, 5f), 2f, new DrawingPoint(30f, 35f), 18f,
            Stops, Positions, DrawingShaderTileMode.Repeat);

        //Assert
        shader.Kind.Should().Be(DrawingShaderKind.TwoPointConicalGradient);
        shader.Start.Should().Be(new DrawingPoint(4f, 5f));
        shader.StartRadius.Should().Be(2f);
        shader.End.Should().Be(new DrawingPoint(30f, 35f));
        shader.EndRadius.Should().Be(18f);
        shader.TileMode.Should().Be(DrawingShaderTileMode.Repeat);
    }

    [Fact]
    public void A_picture_shader_reports_the_factory_inputs()
    {
        //Arrange
        var tile = new DrawingPicture(DrawingRect.Create(0f, 0f, 8f, 8f),
            new DrawingCommand[] { new SaveCommand(0) });
        DrawingRect tileRect = DrawingRect.Create(2f, 3f, 8f, 8f);
        Matrix3x2 localMatrix = Matrix3x2.CreateTranslation(4f, 5f);

        //Act
        DrawingShader shader = DrawingShader.CreatePicture(tile, tileRect,
            DrawingShaderTileMode.Repeat, DrawingShaderTileMode.Mirror, localMatrix);

        //Assert
        shader.Kind.Should().Be(DrawingShaderKind.Picture);
        shader.Picture.Should().BeSameAs(tile);
        shader.TileRect.Should().Be(tileRect);
        shader.TileModeX.Should().Be(DrawingShaderTileMode.Repeat);
        shader.TileModeY.Should().Be(DrawingShaderTileMode.Mirror);
        shader.TileMode.Should().Be(DrawingShaderTileMode.Repeat);
        shader.LocalMatrix.Should().Be(localMatrix);
    }

    [Fact]
    public void A_picture_shader_needs_a_tile_picture()
    {
        //Arrange + Act
        System.Action create = () => DrawingShader.CreatePicture(null,
            DrawingRect.Create(0f, 0f, 8f, 8f),
            DrawingShaderTileMode.Repeat, DrawingShaderTileMode.Repeat);

        //Assert
        create.Should().Throw<System.ArgumentNullException>();
    }

    [Fact]
    public void Every_other_kind_reports_one_tile_mode_on_both_axes()
    {
        //Arrange + Act
        DrawingShader shader = DrawingShader.CreateLinearGradient(
            new DrawingPoint(0f, 0f), new DrawingPoint(10f, 0f), Stops, Positions,
            DrawingShaderTileMode.Repeat);

        //Assert - a gradient tiles along its axis, so both axes report the same mode
        shader.TileModeX.Should().Be(DrawingShaderTileMode.Repeat);
        shader.TileModeY.Should().Be(DrawingShaderTileMode.Repeat);
        shader.Picture.Should().BeNull();
        shader.TileRect.Should().Be(default(DrawingRect));
    }

    [Fact]
    public void Reported_gradient_arrays_are_copies()
    {
        //Arrange
        DrawingShader shader = DrawingShader.CreateLinearGradient(
            DrawingPoint.Empty, new DrawingPoint(10f, 0f), Stops, Positions, DrawingShaderTileMode.Clamp);

        //Act
        DrawingColor[] colors = shader.Colors;
        colors[0] = DrawingColors.Black;
        float[] positions = shader.Positions;
        positions[0] = 0.9f;

        //Assert
        shader.Colors[0].Should().Be(DrawingColors.Red);
        shader.Positions[0].Should().Be(0f);
    }
}
