using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Covers reading a color filter back out: the kind, and the parameters that kind carries.
/// </summary>
public class DrawingColorFilterTests
{
    private static float[] BuildMatrix()
    {
        var matrix = new float[20];
        for (int i = 0; i < matrix.Length; i++)
        {
            matrix[i] = i / 20f;
        }
        return matrix;
    }

    private static byte[] BuildTable(byte offset)
    {
        var table = new byte[256];
        for (int i = 0; i < table.Length; i++)
        {
            table[i] = (byte)((i + offset) % 256);
        }
        return table;
    }

    [Fact]
    public void A_color_matrix_filter_reports_its_matrix()
    {
        //Arrange
        float[] matrix = BuildMatrix();

        //Act
        DrawingColorFilter filter = DrawingColorFilter.CreateColorMatrix(matrix);

        //Assert
        filter.Kind.Should().Be(DrawingColorFilterKind.ColorMatrix);
        filter.Matrix.Should().Equal(matrix);
        filter.AlphaTable.Should().BeNull();
    }

    [Fact]
    public void A_table_filter_reports_the_tables_it_was_given()
    {
        //Arrange
        byte[] red = BuildTable(1);
        byte[] green = BuildTable(2);

        //Act
        DrawingColorFilter filter = DrawingColorFilter.CreateTable(null, red, green, null);

        //Assert
        filter.Kind.Should().Be(DrawingColorFilterKind.Table);
        filter.RedTable.Should().Equal(red);
        filter.GreenTable.Should().Equal(green);
        filter.AlphaTable.Should().BeNull();
        filter.BlueTable.Should().BeNull();
    }

    [Fact]
    public void A_blend_mode_filter_reports_its_color_and_mode()
    {
        //Arrange + Act
        DrawingColorFilter filter = DrawingColorFilter.CreateBlendMode(
            DrawingColors.Teal, DrawingBlendMode.Multiply);

        //Assert
        filter.Kind.Should().Be(DrawingColorFilterKind.BlendMode);
        filter.Color.Should().Be(DrawingColors.Teal);
        filter.BlendMode.Should().Be(DrawingBlendMode.Multiply);
    }

    [Fact]
    public void A_luminance_filter_reports_its_kind_and_nothing_else()
    {
        //Arrange + Act
        DrawingColorFilter filter = DrawingColorFilter.CreateLumaColor();

        //Assert
        filter.Kind.Should().Be(DrawingColorFilterKind.LumaColor);
        filter.Matrix.Should().BeNull();
        filter.BlendMode.Should().Be(DrawingBlendMode.SrcOver);
    }

    [Fact]
    public void Reported_filter_arrays_are_copies()
    {
        //Arrange
        DrawingColorFilter filter = DrawingColorFilter.CreateColorMatrix(BuildMatrix());

        //Act
        float[] matrix = filter.Matrix;
        matrix[0] = 99f;

        //Assert
        filter.Matrix[0].Should().Be(0f);
    }
}
