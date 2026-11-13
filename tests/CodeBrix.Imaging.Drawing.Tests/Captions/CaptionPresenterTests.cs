using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Captions;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Captions;

public class CaptionPresenterTests
{
    private static readonly Size Frame = new Size(640, 360);

    private const string Srt =
        "1\n00:00:01,000 --> 00:00:03,000\nThe first caption, long enough that it has to wrap onto a second line here\n\n" +
        "2\n00:00:02,000 --> 00:00:04,000\nOverlapping\n\n" +
        "3\n00:00:05,000 --> 00:00:06,000\nLast\n";

    private static FontRegistry CreateFonts()
    {
        var fonts = new FontRegistry();
        fonts.RegisterFont(TestAssets.RobotoPath);
        return fonts;
    }

    private static CaptionPresenter CreatePresenter(CaptionStyle style = null)
        => new CaptionPresenter(CaptionTrack.ParseSrt(Srt), CreateFonts(), new DrawingLayout(Frame, SafeAreaGuides.BroadcastTitleSafe), style);

    [Fact]
    public void GetShapeAt_returns_an_empty_group_between_cues_and_the_same_instance_while_cues_hold()
    {
        //Arrange
        CaptionPresenter presenter = CreatePresenter();

        //Act
        var none = (DrawingGroup)presenter.GetShapeAt(0.5);
        DrawingShape first = presenter.GetShapeAt(1.2);
        DrawingShape firstAgain = presenter.GetShapeAt(1.9);
        DrawingShape both = presenter.GetShapeAt(2.5);

        //Assert
        none.Count.Should().Be(0);
        firstAgain.Should().BeSameAs(first);
        both.Should().NotBeSameAs(first);
        ((DrawingGroup)first).Count.Should().Be(2);
        ((DrawingGroup)both).Count.Should().Be(4);
    }

    [Fact]
    public void Captions_sit_inside_the_safe_area_at_its_bottom_centre()
    {
        //Arrange
        CaptionPresenter presenter = CreatePresenter();
        RectangleF safe = presenter.Layout.SafeArea;

        //Act
        RectangleF box = presenter.GetShapeAt(1.5).GetBounds().Value;

        //Assert
        box.Left.Should().BeGreaterThanOrEqualTo(safe.Left);
        box.Right.Should().BeLessThanOrEqualTo(safe.Right);
        box.Bottom.Should().BeApproximately(safe.Bottom, 0.01f);
        ((box.Left + box.Right) / 2f).Should().BeApproximately((safe.Left + safe.Right) / 2f, 0.01f);
        presenter.GetCueLayout(0).LineCount.Should().Be(2);
    }

    [Fact]
    public void Overlapping_cues_stack_with_the_earliest_on_top()
    {
        //Arrange
        CaptionPresenter presenter = CreatePresenter();

        //Act
        DrawingElement[] parts = ((DrawingGroup)presenter.GetShapeAt(2.5)).GetChildren();

        //Assert - box, text, box, text
        RectangleF firstBox = ((RectangleShape)parts[0]).GetBounds().Value;
        RectangleF secondBox = ((RectangleShape)parts[2]).GetBounds().Value;
        firstBox.Bottom.Should().BeLessThan(secondBox.Top);
        parts[1].Should().BeOfType<TextBlockShape>();
    }

    [Fact]
    public void Style_controls_size_box_and_outline()
    {
        //Arrange
        var style = new CaptionStyle { Size = 30, BackgroundColor = null, OutlineColor = Color.Black, MaxLines = 1 };

        //Act
        CaptionPresenter presenter = CreatePresenter(style);
        DrawingElement[] parts = ((DrawingGroup)presenter.GetShapeAt(1.5)).GetChildren();
        style.Size = 99;

        //Assert
        parts.Length.Should().Be(2);
        ((TextBlockShape)parts[0]).IsStroked.Should().BeTrue();
        ((TextBlockShape)parts[1]).IsFilled.Should().BeTrue();
        presenter.GetCueLayout(0).LineCount.Should().Be(1);
        presenter.GetCueLayout(0).HasEllipsis.Should().BeTrue();
        presenter.Style.Size.Should().Be(30f);
    }

    [Fact]
    public void A_cue_position_places_it_by_itself()
    {
        //Arrange
        var track = new CaptionTrack(new[]
        {
            new CaptionCue(0, 2, "Top") { Position = new LayoutPosition(LayoutAnchor.TopCenter, LayoutReference.SafeArea) },
            new CaptionCue(0, 2, "Bottom"),
        });
        var presenter = new CaptionPresenter(track, CreateFonts(), new DrawingLayout(Frame, SafeAreaGuides.BroadcastTitleSafe));

        //Act
        DrawingElement[] parts = ((DrawingGroup)presenter.GetShapeAt(1)).GetChildren();

        //Assert
        parts[0].GetTransformedBounds().Value.Top.Should().BeApproximately(presenter.Layout.SafeArea.Top, 0.01f);
        parts[2].GetTransformedBounds().Value.Bottom.Should().BeApproximately(presenter.Layout.SafeArea.Bottom, 0.01f);
    }

    [Fact]
    public void DrawCaptions_follows_the_session_time_and_passes_a_safe_area_check()
    {
        //Arrange
        using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Frame, LayerOpacity = 255 });
        session.Fonts.RegisterFont(TestAssets.RobotoPath);
        session.AddLayer("Captions", Color.White);
        AnimatedShape captions = session.DrawCaptions(CaptionTrack.ParseSrt(Srt), new DrawingLayout(Frame, SafeAreaGuides.BroadcastTitleSafe));
        captions.IsSafeAreaChecked = true;

        //Act
        session.Time = 0.5;
        int quiet = TestAssets.CountInked(session.ExportPixels(Frame, includeBackground: false));
        session.Time = 1.5;
        int shown = TestAssets.CountInked(session.ExportPixels(Frame, includeBackground: false));
        IReadOnlyList<SafeAreaFinding> findings = session.CheckSafeArea(SafeAreaGuides.BroadcastTitleSafe, Frame);

        //Assert
        quiet.Should().Be(0);
        shown.Should().BeGreaterThan(2000);
        findings.Count.Should().Be(0);
    }

    [Fact]
    public void Invalid_arguments_throw()
    {
        //Act
        Action noTrack = () => _ = new CaptionPresenter(null, CreateFonts(), new DrawingLayout(Frame));
        Action badStyle = () => _ = new CaptionPresenter(CaptionTrack.ParseSrt(Srt), CreateFonts(), new DrawingLayout(Frame), new CaptionStyle { MaxWidthFraction = 0 });

        //Assert
        noTrack.Should().Throw<ArgumentNullException>();
        badStyle.Should().Throw<ArgumentOutOfRangeException>();
    }
}
