using CodeBrix.Imaging.Drawing.Extensions;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Extensions;

public class GeometryBridgeExtensionsTests
{
    [Fact]
    public void Size_round_trips_through_skia()
    {
        //Arrange
        var size = new Size(1000, 500);

        //Act
        SKSizeI sk = size.ToSKSizeI();
        Size back = sk.ToImagingSize();

        //Assert
        sk.Width.Should().Be(1000);
        sk.Height.Should().Be(500);
        back.Should().Be(size);
    }

    [Fact]
    public void SizeF_round_trips_through_skia()
    {
        //Arrange
        var size = new SizeF(12.5f, 7.25f);

        //Act
        SKSize sk = size.ToSKSize();
        SizeF back = sk.ToImagingSizeF();

        //Assert
        sk.Width.Should().Be(12.5f);
        sk.Height.Should().Be(7.25f);
        back.Should().Be(size);
    }

    [Fact]
    public void Point_round_trips_through_skia()
    {
        //Arrange
        var point = new Point(3, 9);

        //Act
        SKPointI sk = point.ToSKPointI();
        Point back = sk.ToImagingPoint();

        //Assert
        sk.X.Should().Be(3);
        sk.Y.Should().Be(9);
        back.Should().Be(point);
    }

    [Fact]
    public void PointF_round_trips_through_skia()
    {
        //Arrange
        var point = new PointF(3.5f, 9.75f);

        //Act
        SKPoint sk = point.ToSKPoint();
        PointF back = sk.ToImagingPointF();

        //Assert
        sk.X.Should().Be(3.5f);
        sk.Y.Should().Be(9.75f);
        back.Should().Be(point);
    }

    [Fact]
    public void RectangleF_to_skrect_maps_xywh_to_ltrb()
    {
        //Arrange - X/Y/Width/Height must become Left/Top/Right/Bottom, not a field copy
        var rect = new RectangleF(10f, 20f, 30f, 40f);

        //Act
        SKRect sk = rect.ToSKRect();

        //Assert
        sk.Left.Should().Be(10f);
        sk.Top.Should().Be(20f);
        sk.Right.Should().Be(40f);   //Left + Width
        sk.Bottom.Should().Be(60f);  //Top + Height
        sk.Width.Should().Be(30f);
        sk.Height.Should().Be(40f);
    }

    [Fact]
    public void SkRect_to_rectanglef_maps_ltrb_to_xywh()
    {
        //Arrange
        var sk = new SKRect(10f, 20f, 40f, 60f);

        //Act
        RectangleF rect = sk.ToImagingRectangleF();

        //Assert
        rect.X.Should().Be(10f);
        rect.Y.Should().Be(20f);
        rect.Width.Should().Be(30f);
        rect.Height.Should().Be(40f);
    }
}
