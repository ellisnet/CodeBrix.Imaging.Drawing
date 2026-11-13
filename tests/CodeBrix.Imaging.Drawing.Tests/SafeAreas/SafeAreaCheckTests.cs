using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.SafeAreas;

public class SafeAreaCheckTests
{
    private sealed class UnboundedShape : DrawingShape
    {
        public UnboundedShape() : base(10f, (Color?)null) { }

        public override void Draw(SKCanvas canvas, SKColor color) { }
    }

    private sealed class FailingBoundsShape : DrawingShape
    {
        public FailingBoundsShape() : base(10f, (Color?)null) { }

        public override RectangleF? GetBounds() => throw new InvalidOperationException("no bounds today");

        public override void Draw(SKCanvas canvas, SKColor color) { }
    }

    private static DrawingSession CreatePortraitSession()
        => new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(1080, 1920) });

    [Fact]
    public void Unmarked_elements_are_not_checked()
    {
        //Arrange
        using DrawingSession session = CreatePortraitSession();
        session.AddLayer("Captions", Color.White);
        session.DrawRectangle(0, 1800, 1080, 100, filled: true);

        //Act
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.InstagramReelsAd);

        //Assert
        findings.Count.Should().Be(0);
    }

    [Fact]
    public void A_marked_element_in_the_bottom_band_is_reported_with_its_overlap()
    {
        //Arrange
        using DrawingSession session = CreatePortraitSession();
        session.AddLayer("Captions", Color.White);
        DrawingShape caption = session.DrawRectangle(200, 1200, 600, 100, filled: true);
        caption.IsSafeAreaChecked = true;

        //Act
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.InstagramReelsAd, new Size(1080, 1920));

        //Assert - y 1248..1300 is inside the bottom 35%
        findings.Count.Should().Be(1);
        SafeAreaFinding finding = findings[0];
        finding.Kind.Should().Be(SafeAreaFindingKind.Overlap);
        finding.Element.Should().BeSameAs(caption);
        finding.Layer.Name.Should().Be("Captions");
        finding.ElementIndex.Should().Be(0);
        finding.GuideName.Should().Be("Instagram Reels ad");
        finding.ZoneName.Should().Be("bottom margin");
        finding.Overlap.Value.Top.Should().BeApproximately(1248f, 0.01f);
        finding.Overlap.Value.Height.Should().BeApproximately(52f, 0.01f);
        finding.Overlap.Value.Width.Should().BeApproximately(600f, 0.01f);
    }

    [Fact]
    public void A_marked_layer_checks_all_of_its_elements_and_reports_each_zone()
    {
        //Arrange - one element in the top-left corner touches two bands
        using DrawingSession session = CreatePortraitSession();
        DrawingLayer layer = session.AddLayer("Titles", Color.White);
        layer.IsSafeAreaChecked = true;
        session.DrawRectangle(20, 20, 100, 100, filled: true);
        session.DrawRectangle(400, 800, 100, 100, filled: true); //clear

        //Act
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.InstagramReelsAd);

        //Assert
        findings.Select(f => f.ZoneName).Should().Equal("top margin", "left margin");
        findings.All(f => f.ElementIndex == 0).Should().BeTrue();
    }

    [Fact]
    public void Unknown_bounds_are_skipped_by_default_and_listed_on_request()
    {
        //Arrange
        using DrawingSession session = CreatePortraitSession();
        DrawingLayer layer = session.AddLayer("Custom", Color.White);
        layer.IsSafeAreaChecked = true;
        session.DrawShape(new UnboundedShape());
        session.DrawShape(new FailingBoundsShape());

        //Act
        IReadOnlyList<SafeAreaFinding> quiet = session.CheckSafeArea(SafeAreaGuides.TikTok);
        IReadOnlyList<SafeAreaFinding> listed = session.CheckSafeArea(SafeAreaGuides.TikTok,
            new SafeAreaCheckOptions { ReportUncheckedElements = true });

        //Assert
        quiet.Count.Should().Be(0);
        listed.Count.Should().Be(2);
        listed.All(f => f.Kind == SafeAreaFindingKind.CouldNotCheck && f.Zone == null && f.Overlap == null).Should().BeTrue();
    }

    [Fact]
    public void Strokes_and_text_are_checked_by_their_bounds()
    {
        //Arrange
        using DrawingSession session = CreatePortraitSession();
        session.Fonts.RegisterFont(TestAssets.OpenSansPath);
        session.AddLayer("Ink", Color.White).IsSafeAreaChecked = true;
        session.PointerPressedNormalized(0.5f, 0.02f);
        session.PointerMovedNormalized(0.6f, 0.03f);
        session.PointerReleased();
        session.DrawText("Follow for more", 540, 1700, 60, align: TextAlign.Center);

        //Act
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.YouTubeShortsAd);

        //Assert
        findings.Count(f => f.Element is Stroke && f.ZoneName == "top margin").Should().Be(1);
        findings.Count(f => f.Element is TextShape && f.ZoneName == "bottom margin").Should().Be(1);
    }

    [Fact]
    public void CheckAllElements_ignores_the_marks()
    {
        //Arrange
        using DrawingSession session = CreatePortraitSession();
        session.AddLayer("Everything", Color.White);
        session.DrawCircle(540, 1850, 40);

        //Act
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.OrdinaryPortraitPost,
            new SafeAreaCheckOptions { CheckAllElements = true });

        //Assert
        findings.Count.Should().Be(1);
        findings[0].ZoneName.Should().Be("bottom margin");
    }

    [Fact]
    public void Time_rules_are_evaluated_at_the_session_time()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(1920, 1080) });
        session.AddLayer("Logo", Color.White).IsSafeAreaChecked = true;
        session.DrawRectangle(800, 400, 300, 200, filled: true);
        var options = new SafeAreaCheckOptions { Duration = 60 };

        //Act
        session.Time = 10;
        int early = session.CheckSafeArea(SafeAreaGuides.YouTubeEndScreen, options).Count;
        int unknownDuration = session.CheckSafeArea(SafeAreaGuides.YouTubeEndScreen).Count;
        session.Time = 45;
        int late = session.CheckSafeArea(SafeAreaGuides.YouTubeEndScreen, options).Count;

        //Assert
        early.Should().Be(0);
        unknownDuration.Should().Be(1);
        late.Should().Be(1);
    }

    [Fact]
    public void A_time_window_rule_applies_only_while_the_session_time_is_inside_it()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(1920, 1080) });
        session.AddLayer("Logo", Color.White).IsSafeAreaChecked = true;
        session.DrawRectangle(800, 400, 300, 200, filled: true);
        var guide = new SafeAreaGuide("Mid-roll card", SafeAreaMargins.WholeFrame, timeRule: SafeAreaTimeRule.Between(5, 10));

        //Act
        int before = session.CheckSafeArea(guide).Count;
        session.Time = 7;
        int during = session.CheckSafeArea(guide).Count;
        session.Time = 12;
        int after = session.CheckSafeArea(guide).Count;

        //Assert
        before.Should().Be(0);
        during.Should().Be(1);
        after.Should().Be(0);
    }

    [Fact]
    public void A_letterboxed_frame_maps_the_drawing_into_its_centered_rectangle()
    {
        //Arrange - a square drawing in a portrait frame sits in y 420..1500 of 1920
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(1000, 1000) });
        session.AddLayer("Art", Color.White).IsSafeAreaChecked = true;
        session.DrawRectangle(0, 900, 1000, 100, filled: true); //the bottom tenth of the drawing

        //Act
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.InstagramReelsAd, new Size(1080, 1920));
        SafeAreaFinding bottom = findings.Single(f => f.ZoneName == "bottom margin");

        //Assert - frame y 1392..1500 lies in the bottom band (from 1248), so all 100 units overlap
        bottom.FrameOverlap.Value.Top.Should().BeApproximately(1392f, 0.01f);
        bottom.FrameOverlap.Value.Bottom.Should().BeApproximately(1500f, 0.01f);
        bottom.Overlap.Value.Top.Should().BeApproximately(900f, 0.01f);
        bottom.Overlap.Value.Height.Should().BeApproximately(100f, 0.01f);
    }

    [Fact]
    public void CheckSafeArea_rejects_a_null_guide()
    {
        //Arrange
        using DrawingSession session = CreatePortraitSession();
        Action act = () => session.CheckSafeArea(null);

        //Act + Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
