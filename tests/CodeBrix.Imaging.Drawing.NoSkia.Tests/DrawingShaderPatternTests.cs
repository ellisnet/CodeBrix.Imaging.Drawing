using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Covers what a picture-backed shader paints - the mechanism behind an SVG pattern fill.
/// Every test fills with the same two-color tile (red on its left half, blue on its right)
/// and reads pixels back, so a wrap mode is told apart by WHERE the two colors land rather
/// than by any claim the shader makes about itself.
/// </summary>
public class DrawingShaderPatternTests
{
    private const float TileExtent = 10f;

    private static DrawingPicture BuildTile()
    {
        var left = new DrawingPathBuilder();
        left.AddRect(DrawingRect.Create(0f, 0f, TileExtent / 2f, TileExtent));

        var right = new DrawingPathBuilder();
        right.AddRect(DrawingRect.Create(TileExtent / 2f, 0f, TileExtent / 2f, TileExtent));

        return new DrawingPicture(DrawingRect.Create(0f, 0f, TileExtent, TileExtent),
            new DrawingCommand[]
            {
                new DrawPathCommand(left.Detach(), new DrawingPaint { Color = DrawingColors.Red }),
                new DrawPathCommand(right.Detach(), new DrawingPaint { Color = DrawingColors.Blue }),
            });
    }

    private static DrawingShader BuildShader(DrawingShaderTileMode tileModeX,
        Matrix3x2? localMatrix = null)
        => DrawingShader.CreatePicture(BuildTile(), DrawingRect.Create(0f, 0f, TileExtent, TileExtent),
            tileModeX, DrawingShaderTileMode.Repeat, localMatrix);

    private static DrawingBitmap Fill(DrawingShader shader, int width, int height, float scale = 1f)
    {
        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            width, height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));

        using var canvas = new DrawingCanvas(bitmap);
        canvas.Scale(scale);
        canvas.DrawRect(DrawingRect.Create(0f, 0f, width / scale, height / scale),
            new DrawingPaint { Shader = shader });
        return bitmap;
    }

    [Fact]
    public void A_repeating_tile_paints_the_same_columns_in_every_repetition()
    {
        //Arrange
        DrawingShader shader = BuildShader(DrawingShaderTileMode.Repeat);

        //Act
        using DrawingBitmap filled = Fill(shader, 30, 10);

        //Assert - red|blue, red|blue, red|blue
        filled.GetPixel(0, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(4, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(5, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(9, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(10, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(25, 5).Should().Be(DrawingColors.Blue);
    }

    [Fact]
    public void A_clamped_tile_extends_its_edge_texels_outside_the_tile()
    {
        //Arrange
        DrawingShader shader = BuildShader(DrawingShaderTileMode.Clamp);

        //Act
        using DrawingBitmap filled = Fill(shader, 30, 10);

        //Assert - the tile paints once, then its last column runs on forever
        filled.GetPixel(4, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(5, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(10, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(29, 5).Should().Be(DrawingColors.Blue);
    }

    [Fact]
    public void A_mirrored_tile_flips_every_other_repetition()
    {
        //Arrange
        DrawingShader shader = BuildShader(DrawingShaderTileMode.Mirror);

        //Act
        using DrawingBitmap filled = Fill(shader, 30, 10);

        //Assert - red|blue, blue|red, red|blue
        filled.GetPixel(4, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(9, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(10, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(15, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(20, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(25, 5).Should().Be(DrawingColors.Blue);
    }

    [Fact]
    public void A_decal_tile_leaves_everything_outside_the_tile_transparent()
    {
        //Arrange
        DrawingShader shader = BuildShader(DrawingShaderTileMode.Decal);

        //Act
        using DrawingBitmap filled = Fill(shader, 30, 10);

        //Assert
        filled.GetPixel(4, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(9, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(10, 5).Alpha.Should().Be(0);
        filled.GetPixel(29, 5).Alpha.Should().Be(0);
    }

    [Fact]
    public void A_local_matrix_moves_the_tiling()
    {
        //Arrange - the tiling slides three units to the right
        DrawingShader shader = BuildShader(DrawingShaderTileMode.Repeat,
            Matrix3x2.CreateTranslation(3f, 0f));

        //Act
        using DrawingBitmap filled = Fill(shader, 30, 10);

        //Assert - what was at x=0 is now at x=3, and the columns before it wrap around
        filled.GetPixel(0, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(2, 5).Should().Be(DrawingColors.Blue);
        filled.GetPixel(3, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(7, 5).Should().Be(DrawingColors.Red);
        filled.GetPixel(8, 5).Should().Be(DrawingColors.Blue);
    }

    [Fact]
    public void A_tile_is_rasterized_at_the_scale_it_is_painted_at()
    {
        //Arrange - at scale 4 the tile's half-way edge lands on device pixel 20
        DrawingShader shader = BuildShader(DrawingShaderTileMode.Repeat);

        //Act
        using DrawingBitmap filled = Fill(shader, 160, 40, scale: 4f);

        //Assert - the pixels either side of the edge are PURE: a tile rasterized at unit
        //  size and magnified would blur that edge across four device pixels
        filled.GetPixel(19, 20).Should().Be(DrawingColors.Red);
        filled.GetPixel(20, 20).Should().Be(DrawingColors.Blue);
        filled.GetPixel(39, 20).Should().Be(DrawingColors.Blue);
        filled.GetPixel(40, 20).Should().Be(DrawingColors.Red);
    }

    [Fact]
    public void A_tile_with_no_area_paints_nothing()
    {
        //Arrange
        DrawingShader shader = DrawingShader.CreatePicture(BuildTile(),
            DrawingRect.Create(0f, 0f, 0f, TileExtent),
            DrawingShaderTileMode.Repeat, DrawingShaderTileMode.Repeat);

        //Act
        using DrawingBitmap filled = Fill(shader, 10, 10);

        //Assert
        filled.GetPixel(5, 5).Alpha.Should().Be(0);
    }

    [Fact]
    public void The_paint_alpha_modulates_a_pattern_the_way_it_modulates_any_shader()
    {
        //Arrange
        DrawingShader shader = BuildShader(DrawingShaderTileMode.Repeat);
        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            10, 10, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));

        //Act
        using (var canvas = new DrawingCanvas(bitmap))
        {
            canvas.DrawRect(DrawingRect.Create(0f, 0f, 10f, 10f), new DrawingPaint
            {
                Shader = shader,
                Color = new DrawingColor(0, 0, 0, 128),
            });
        }

        //Assert
        bitmap.GetPixel(2, 5).Alpha.Should().Be(128);
        bitmap.Dispose();
    }
}
