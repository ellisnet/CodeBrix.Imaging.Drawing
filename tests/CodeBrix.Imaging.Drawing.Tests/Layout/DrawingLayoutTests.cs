using System;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.SafeAreas;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Layout;

public class DrawingLayoutTests
{
    [Fact]
    public void GetSafeRectangle_insets_by_the_margins_and_cuts_to_crops()
    {
        //Arrange
        var frame = new SizeF(1080, 1920);
        SafeAreaGuide combined = SafeAreaGuide.Combine("both", SafeAreaGuides.InstagramFeedPreview, SafeAreaGuides.BroadcastTitleSafe);

        //Act
        RectangleF title = SafeAreaGuides.BroadcastTitleSafe.GetSafeRectangle(frame);
        RectangleF crop = SafeAreaGuides.InstagramFeedPreview.GetSafeRectangle(frame);
        RectangleF both = combined.GetSafeRectangle(frame);

        //Assert
        title.X.Should().BeApproximately(54f, 1e-3f);
        title.Y.Should().BeApproximately(96f, 1e-3f);
        title.Width.Should().BeApproximately(972f, 1e-3f);
        title.Height.Should().BeApproximately(1728f, 1e-3f);
        crop.Should().Be(new RectangleF(0, 285, 1080, 1350));
        both.Y.Should().Be(285f);
        both.X.Should().BeApproximately(54f, 1e-3f);
    }

    [Fact]
    public void GetSafeRectangle_respects_time_rules_and_whole_frame_zones()
    {
        //Arrange
        var frame = new SizeF(1920, 1080);
        SafeAreaGuide endScreen = SafeAreaGuides.YouTubeEndScreen;

        //Act + Assert
        endScreen.GetSafeRectangle(frame, seconds: 10, duration: 60).Should().Be(new RectangleF(0, 0, 1920, 1080));
        endScreen.GetSafeRectangle(frame, seconds: 50, duration: 60).Should().Be(RectangleF.Empty);
    }

    [Fact]
    public void Place_measures_from_an_anchor_of_the_frame_or_the_safe_area()
    {
        //Arrange
        var layout = new DrawingLayout(new Size(1000, 500), SafeAreaGuides.BroadcastTitleSafe);

        //Act
        PointF centre = layout.Place(LayoutAnchor.Center);
        PointF safeBottom = layout.Place(new LayoutPosition(LayoutAnchor.BottomCenter, LayoutReference.SafeArea, fractionY: -0.05f));
        PointF corner = layout.Place(LayoutAnchor.TopRight, LayoutReference.SafeArea, offsetX: -10, offsetY: 5);

        //Assert - the safe area is 50..950 x 25..475
        centre.Should().Be(new PointF(500, 250));
        safeBottom.X.Should().Be(500f);
        safeBottom.Y.Should().BeApproximately(475f - 22.5f, 1e-3f);
        corner.X.Should().BeApproximately(940f, 1e-3f);
        corner.Y.Should().BeApproximately(30f, 1e-3f);
        layout.Frame.Should().Be(new RectangleF(0, 0, 1000, 500));
        layout.IsPortrait.Should().BeFalse();
        layout.ShortSide.Should().Be(500f);
    }

    [Fact]
    public void One_description_serves_portrait_and_landscape()
    {
        //Arrange
        var position = new LayoutPosition(LayoutAnchor.BottomCenter, LayoutReference.SafeArea, fractionY: -0.05f);
        var portrait = new DrawingLayout(new Size(1080, 1920), SafeAreaGuides.InstagramReelsAd);
        var landscape = new DrawingLayout(new Size(1920, 1080), SafeAreaGuides.BroadcastActionSafe);

        //Act
        PointF p = portrait.Place(position);
        PointF l = landscape.Place(position);

        //Assert - each sits centred in its own safe area, just above its safe bottom edge
        p.X.Should().BeApproximately(portrait.SafeArea.X + (portrait.SafeArea.Width / 2f), 1e-3f);
        p.Y.Should().BeLessThan(portrait.SafeArea.Bottom);
        p.Y.Should().BeGreaterThan(portrait.SafeArea.Bottom - (0.06f * portrait.SafeArea.Height));
        l.Y.Should().BeApproximately(landscape.SafeArea.Bottom - (0.05f * landscape.SafeArea.Height), 1e-2f);
    }

    [Fact]
    public void PlaceBox_puts_the_box_anchor_on_the_point()
    {
        //Arrange
        var layout = new DrawingLayout(new Size(800, 600));

        //Act
        RectangleF bottom = layout.PlaceBox(new SizeF(200, 50), new LayoutPosition(LayoutAnchor.BottomCenter, offsetY: -10));
        RectangleF topLeft = layout.PlaceBox(new SizeF(100, 40), new LayoutPosition(LayoutAnchor.TopLeft, offsetX: 20, offsetY: 20));

        //Assert
        bottom.Should().Be(new RectangleF(300, 540, 200, 50));
        topLeft.Should().Be(new RectangleF(20, 20, 100, 40));
        layout.SafeArea.Should().Be(layout.Frame);
    }

    [Fact]
    public void The_safe_area_maps_into_calibrated_units_through_the_aspect_fit()
    {
        //Arrange - a 500 x 250 drawing exported at 1000 x 500 (scale 2)
        var layout = new DrawingLayout(new Size(500, 250), SafeAreaGuides.BroadcastTitleSafe, new Size(1000, 500));

        //Act + Assert
        layout.SafeArea.X.Should().BeApproximately(25f, 1e-3f);
        layout.SafeArea.Y.Should().BeApproximately(12.5f, 1e-3f);
        layout.SafeArea.Width.Should().BeApproximately(450f, 1e-3f);
    }

    [Fact]
    public void Constructor_and_anchor_reject_bad_values()
    {
        //Arrange
        Action size = () => _ = new DrawingLayout(new Size(0, 10));
        Action anchor = () => _ = new LayoutAnchor(float.NaN, 0);

        //Act + Assert
        size.Should().Throw<ArgumentOutOfRangeException>();
        anchor.Should().Throw<ArgumentOutOfRangeException>();
    }
}
