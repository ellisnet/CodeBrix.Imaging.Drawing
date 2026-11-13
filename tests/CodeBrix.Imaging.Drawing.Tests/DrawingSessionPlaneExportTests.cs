using System;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests;

public class DrawingSessionPlaneExportTests
{
    private static DrawingSession CreateSession(Color background, byte layerOpacity = 255)
    {
        var session = new DrawingSession(new DrawingSessionOptions { BackgroundFillColor = background, LayerOpacity = layerOpacity });
        session.AddLayer("Ink", Color.FromRgb(200, 40, 90));
        session.DrawCircle(500, 500, 300, thickness: 40);
        session.DrawRectangle(100, 100, 200, 150, filled: true, color: Color.FromRgba(20, 160, 60, 128));
        return session;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExportPlaneImage_holds_exactly_the_ExportPixels_bytes(bool includeBackground)
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.FromRgb(240, 230, 210));
        var size = new Size(90, 60);

        //Act
        byte[] pixels = session.ExportPixels(size, includeBackground);
        DrawingPlaneImage image = session.ExportPlaneImage(size, includeBackground);

        //Assert - byte / 255f per channel, so converting back gives the same bytes
        image.Size.Should().Be(size);
        image.ToRgba().Should().Equal(pixels);
        image.R[45, 30].Should().Be(pixels[((30 * 90) + 45) * 4] / 255f);
    }

    [Fact]
    public void ExportPlaneImage_with_a_transparent_background_keeps_straight_alpha()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.Transparent);

        //Act
        DrawingPlaneImage image = session.ExportPlaneImage(new Size(100, 100), includeBackground: false);

        //Assert - nothing drawn: alpha 0; the half-transparent rectangle keeps its full colour
        //  with alpha near 0.5 (straight, not premultiplied)
        image.A[50, 50].Should().Be(0f);
        image.A[15, 15].Should().BeInRange(126f / 255f, 130f / 255f);
        image.R[15, 15].Should().BeInRange(18f / 255f, 22f / 255f);
        image.G[15, 15].Should().BeInRange(158f / 255f, 162f / 255f);
    }

    [Fact]
    public void ExportPlaneImage_into_an_existing_image_overwrites_every_plane()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.Transparent);
        var destination = new DrawingPlaneImage(64, 48).Fill(0.3f, 0.6f, 0.9f, 0.7f);

        //Act
        session.ExportPlaneImage(destination, includeBackground: false);

        //Assert
        destination.ToRgba().Should().Equal(session.ExportPixels(new Size(64, 48), includeBackground: false));
        destination.A[1, 1].Should().Be(0f);
        destination.R[1, 1].Should().Be(0f);
    }

    [Fact]
    public void ExportPlaneImage_without_a_size_uses_the_default_export_size()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);

        //Act
        DrawingPlaneImage image = session.ExportPlaneImage();

        //Assert
        image.Size.Should().Be(session.DefaultExportSize);
    }

    [Fact]
    public void ExportPlaneImage_rejects_a_null_destination()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);

        //Act
        Action act = () => session.ExportPlaneImage((DrawingPlaneImage)null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportMask_of_one_layer_is_that_layers_alpha_over_255()
    {
        //Arrange - full layer opacity, no fade and no background: the export's alpha is then
        //  exactly the layer's own ink alpha
        using DrawingSession session = CreateSession(Color.Transparent);
        var size = new Size(80, 80);
        byte[] pixels = session.ExportPixels(size, includeBackground: false);

        //Act
        DrawingPlane mask = session.ExportMask(session.Layers[0], size);

        //Assert
        for (int i = 0; i < mask.Length; i++) { mask.Data[i].Should().Be(pixels[(i * 4) + 3] / 255f); }
    }

    [Fact]
    public void ExportMask_ignores_the_opacities_and_the_background()
    {
        //Arrange
        using DrawingSession plain = CreateSession(Color.Transparent);
        using DrawingSession faded = CreateSession(Color.White, layerOpacity: 40);
        faded.Opacity = 0.3f;
        faded.Layers[0].Opacity = 0.5f;
        var size = new Size(100, 100);

        //Act
        DrawingPlane expected = plain.ExportMask(size);
        DrawingPlane mask = faded.ExportMask(size);

        //Assert - ring fully covered, centre untouched, translucent colour counts at its alpha
        mask.Data.Should().Equal(expected.Data);
        mask[80, 50].Should().Be(1f);
        mask[50, 50].Should().Be(0f);
        mask[15, 15].Should().BeInRange(126f / 255f, 130f / 255f);
    }

    [Fact]
    public void ExportMask_unions_the_layers_bottom_to_top()
    {
        //Arrange - two half-covering squares overlapping in the middle, on two layers
        using var session = new DrawingSession(new DrawingSessionOptions { LayerOpacity = 255 });
        session.AddLayer("A", Color.Black);
        session.DrawRectangle(100, 100, 500, 500, filled: true, color: Color.FromRgba(0, 0, 0, 128));
        DrawingLayer second = session.AddLayer("B", Color.Black);
        session.ActiveLayer = second;
        session.DrawRectangle(400, 400, 500, 500, filled: true);
        var size = new Size(100, 100);

        //Act
        DrawingPlane all = session.ExportMask(size);
        DrawingPlane first = session.ExportMask(session.Layers[0], size);
        DrawingPlane top = session.ExportMask(second, size);

        //Assert
        float a = first[20, 20];
        a.Should().BeInRange(126f / 255f, 130f / 255f);
        top[20, 20].Should().Be(0f);
        all[20, 20].Should().Be(a);
        all[50, 50].Should().Be(1f);
        all[80, 80].Should().Be(1f);
        first[80, 80].Should().Be(0f);
        all[5, 95].Should().Be(0f);
    }

    [Fact]
    public void ExportMask_without_a_size_uses_the_default_export_size()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);

        //Act
        DrawingPlane mask = session.ExportMask();
        DrawingPlane layerMask = session.ExportMask(session.Layers[0]);

        //Assert
        mask.Size.Should().Be(session.DefaultExportSize);
        layerMask.Data.Should().Equal(mask.Data);
    }

    [Fact]
    public void ExportMask_of_an_empty_drawing_is_all_zero()
    {
        //Arrange
        using var session = new DrawingSession();
        session.AddLayer("Empty", Color.Black);

        //Act
        DrawingPlane mask = session.ExportMask(new Size(20, 10));

        //Assert
        mask.Data.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public void ExportMask_rejects_a_layer_of_another_session()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);
        using DrawingSession other = CreateSession(Color.White);

        //Act
        Action foreign = () => session.ExportMask(other.Layers[0], new Size(10, 10));
        Action missing = () => session.ExportMask((DrawingLayer)null, new Size(10, 10));

        //Assert
        foreign.Should().Throw<ArgumentException>();
        missing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportMask_rejects_an_empty_size()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);

        //Act
        Action act = () => session.ExportMask(new Size(0, 10));

        //Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExportMask_with_coverage_meaning_is_the_default_mask()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White, layerOpacity: 100);
        var size = new Size(80, 80);

        //Act
        DrawingPlane plain = session.ExportMask(size);
        DrawingPlane coverage = session.ExportMask(size, MaskMeaning.Coverage);

        //Assert
        coverage.Data.Should().Equal(plain.Data);
    }

    [Fact]
    public void ExportMask_visible_alpha_is_the_alpha_of_the_frame_over_transparency()
    {
        //Arrange - layer opacity, an element opacity and a session fade all apply
        using DrawingSession session = CreateSession(Color.FromRgb(10, 20, 30), layerOpacity: 180);
        session.Layers[0].GetElements()[1].Opacity = 0.5f;
        session.Opacity = 0.8f;
        var size = new Size(80, 80);

        //Act
        byte[] pixels = session.ExportPixels(size, includeBackground: false);
        DrawingPlane visible = session.ExportMask(size, MaskMeaning.VisibleAlpha);
        DrawingPlane coverage = session.ExportMask(size);

        //Assert
        for (int i = 0; i < visible.Length; i++) { visible.Data[i].Should().Be(pixels[(i * 4) + 3] / 255f); }
        visible[8 * 2, 8 * 2].Should().BeLessThan(coverage[8 * 2, 8 * 2]);
        visible.Data.Should().Contain(v => v > 0f);
    }

    [Fact]
    public void A_layer_at_opacity_zero_shows_in_the_coverage_mask_but_not_in_the_visible_one()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);
        DrawingLayer layer = session.Layers[0];
        layer.Opacity = 0f;
        var size = new Size(60, 60);

        //Act
        DrawingPlane coverage = session.ExportMask(layer, size, MaskMeaning.Coverage);
        DrawingPlane visible = session.ExportMask(layer, size, MaskMeaning.VisibleAlpha);
        DrawingPlane visibleAll = session.ExportMask(size, MaskMeaning.VisibleAlpha);

        //Assert
        coverage.Data.Should().Contain(v => v == 1f);
        visible.Data.Should().OnlyContain(v => v == 0f);
        visibleAll.Data.Should().OnlyContain(v => v == 0f);
    }

    [Fact]
    public void ExportMask_into_an_existing_plane_overwrites_every_value_for_either_meaning()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White, layerOpacity: 128);
        var plane = new DrawingPlane(50, 40, 0.75f);
        var layerPlane = new DrawingPlane(50, 40, -3f);

        //Act
        session.ExportMask(plane);
        float[] coverage = (float[])plane.Data.Clone();
        session.ExportMask(plane, MaskMeaning.VisibleAlpha);
        session.ExportMask(session.Layers[0], layerPlane, MaskMeaning.VisibleAlpha);

        //Assert
        coverage.Should().Equal(session.ExportMask(new Size(50, 40)).Data);
        plane.Data.Should().Equal(session.ExportMask(new Size(50, 40), MaskMeaning.VisibleAlpha).Data);
        layerPlane.Data.Should().Equal(plane.Data);
    }

    [Fact]
    public void ExportMask_meaning_overloads_check_their_arguments()
    {
        //Arrange
        using DrawingSession session = CreateSession(Color.White);
        using DrawingSession other = CreateSession(Color.White);

        //Act
        Action undefined = () => session.ExportMask(new Size(10, 10), (MaskMeaning)5);
        Action nullPlane = () => session.ExportMask((DrawingPlane)null);
        Action foreign = () => session.ExportMask(other.Layers[0], new DrawingPlane(4, 4));
        Action nullLayer = () => session.ExportMask((DrawingLayer)null, MaskMeaning.VisibleAlpha);

        //Assert
        undefined.Should().Throw<ArgumentOutOfRangeException>();
        nullPlane.Should().Throw<ArgumentNullException>();
        foreign.Should().Throw<ArgumentException>();
        nullLayer.Should().Throw<ArgumentNullException>();
        session.ExportMask(MaskMeaning.VisibleAlpha).Size.Should().Be(session.DefaultExportSize);
    }
}
