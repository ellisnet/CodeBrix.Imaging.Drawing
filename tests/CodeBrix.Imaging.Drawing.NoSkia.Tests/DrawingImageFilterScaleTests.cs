using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.NoSkia;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

/// <summary>
/// Covers the device scale a save-layer's image filter is evaluated at. Filters hold their
/// parameters in user units, so the canvas must hand each one the transform scale that was
/// in force when its layer opened - otherwise one picture could not render correctly at
/// more than one output scale.
/// </summary>
public class DrawingImageFilterScaleTests
{
    private sealed class RecordingFilter : DrawingImageFilter
    {
        public List<float> Scales { get; } = new List<float>();

        public override string Description => "recording";

        public override DrawingBitmap Apply(DrawingBitmap source, float deviceScale)
        {
            Scales.Add(deviceScale);
            return source.Copy();
        }
    }

    private static DrawingBitmap CreateTarget()
        => new DrawingBitmap(new DrawingImageInfo(40, 40, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));

    [Fact]
    public void An_unscaled_layer_filter_is_evaluated_at_scale_one()
    {
        //Arrange
        var filter = new RecordingFilter();
        var paint = new DrawingPaint { ImageFilter = filter };
        using DrawingBitmap bitmap = CreateTarget();
        using var canvas = new DrawingCanvas(bitmap);

        //Act
        canvas.SaveLayer(paint);
        canvas.Restore();

        //Assert
        filter.Scales.Should().Equal(new[] { 1f });
    }

    [Fact]
    public void A_layer_filter_is_evaluated_at_the_transform_scale_in_force_when_the_layer_opened()
    {
        //Arrange
        var filter = new RecordingFilter();
        var paint = new DrawingPaint { ImageFilter = filter };
        using DrawingBitmap bitmap = CreateTarget();
        using var canvas = new DrawingCanvas(bitmap);

        //Act
        canvas.Scale(3f);
        canvas.SaveLayer(paint);
        canvas.Scale(10f); //Applied after the layer opened, so it must NOT count
        canvas.Restore();

        //Assert
        filter.Scales.Should().Equal(new[] { 3f });
    }

    [Fact]
    public void Nested_layer_filters_see_the_accumulated_transform_scale()
    {
        //Arrange
        var outerFilter = new RecordingFilter();
        var innerFilter = new RecordingFilter();
        using DrawingBitmap bitmap = CreateTarget();
        using var canvas = new DrawingCanvas(bitmap);

        //Act
        canvas.Scale(2f);
        canvas.SaveLayer(new DrawingPaint { ImageFilter = outerFilter });
        canvas.Scale(3f);
        canvas.SaveLayer(new DrawingPaint { ImageFilter = innerFilter });
        canvas.Restore();
        canvas.Restore();

        //Assert
        innerFilter.Scales.Should().Equal(new[] { 6f });
        outerFilter.Scales.Should().Equal(new[] { 2f });
    }

    [Fact]
    public void A_rotated_transform_reports_its_largest_axis_scale()
    {
        //Arrange
        var filter = new RecordingFilter();
        using DrawingBitmap bitmap = CreateTarget();
        using var canvas = new DrawingCanvas(bitmap);

        //Act - rotation alone does not change how many device pixels a user unit covers
        canvas.Scale(4f);
        canvas.RotateDegrees(30f);
        canvas.SaveLayer(new DrawingPaint { ImageFilter = filter });
        canvas.Restore();

        //Assert
        filter.Scales.Count.Should().Be(1);
        filter.Scales[0].Should().BeApproximately(4f, 0.0001f);
    }
}
