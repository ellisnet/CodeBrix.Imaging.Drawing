using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Imaging.Drawing.SafeAreas;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.SafeAreas;

public class SafeAreaGuideTests
{
    private static readonly SizeF Portrait = new SizeF(1080, 1920);
    private static readonly SizeF Landscape = new SizeF(1920, 1080);

    private static SafeAreaZone Zone(IReadOnlyList<SafeAreaZone> zones, string name) => zones.Single(z => z.ZoneName == name);

    public static TheoryData<string> PresetNames()
        => new TheoryData<string>(
            nameof(SafeAreaGuides.InstagramReelsAd), nameof(SafeAreaGuides.InstagramStoriesAd),
            nameof(SafeAreaGuides.FacebookReelsAd), nameof(SafeAreaGuides.FacebookStoriesAd),
            nameof(SafeAreaGuides.YouTubeShortsAd), nameof(SafeAreaGuides.TikTok),
            nameof(SafeAreaGuides.OrdinaryPortraitPost), nameof(SafeAreaGuides.InstagramProfileGrid),
            nameof(SafeAreaGuides.InstagramFeedPreview), nameof(SafeAreaGuides.BroadcastTitleSafe),
            nameof(SafeAreaGuides.BroadcastActionSafe), nameof(SafeAreaGuides.YouTubeEndScreen));

    private static SafeAreaGuide Preset(string name)
        => (SafeAreaGuide)typeof(SafeAreaGuides).GetProperty(name).GetValue(null);

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Every_preset_is_named_sourced_and_marked_advisory_or_a_standard(string presetName)
    {
        //Act
        SafeAreaGuide guide = Preset(presetName);

        //Assert
        guide.Name.Should().NotBeNullOrWhiteSpace();
        guide.Source.Should().NotBeNullOrWhiteSpace();
        (guide.Source.StartsWith("FACT", StringComparison.Ordinal) || guide.Source.Contains("Advisory", StringComparison.Ordinal))
            .Should().BeTrue();
        guide.GetUnsafeZones(Portrait).Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Meta_ad_margins_lay_out_on_a_portrait_frame()
    {
        //Act
        IReadOnlyList<SafeAreaZone> zones = SafeAreaGuides.InstagramReelsAd.GetUnsafeZones(Portrait);

        //Assert - top 14%, bottom 35%, sides 6%
        zones.Count.Should().Be(4);
        Zone(zones, "top margin").Bounds.Height.Should().BeApproximately(268.8f, 0.01f);
        Zone(zones, "bottom margin").Bounds.Top.Should().BeApproximately(1248f, 0.01f);
        Zone(zones, "left margin").Bounds.Width.Should().BeApproximately(64.8f, 0.01f);
        Zone(zones, "right margin").Bounds.Left.Should().BeApproximately(1015.2f, 0.01f);
        zones.All(z => z.GuideName == "Instagram Reels ad").Should().BeTrue();
    }

    [Fact]
    public void YouTube_shorts_ad_matches_the_reported_pixels()
    {
        //Act
        IReadOnlyList<SafeAreaZone> zones = SafeAreaGuides.YouTubeShortsAd.GetUnsafeZones(Portrait);

        //Assert - clear box x 48..888, y 288..1248
        Zone(zones, "top margin").Bounds.Bottom.Should().BeApproximately(288f, 0.01f);
        Zone(zones, "bottom margin").Bounds.Top.Should().BeApproximately(1248f, 0.01f);
        Zone(zones, "left margin").Bounds.Right.Should().BeApproximately(48f, 0.01f);
        Zone(zones, "right margin").Bounds.Left.Should().BeApproximately(888f, 0.01f);
    }

    [Fact]
    public void Instagram_crops_cut_away_the_top_and_bottom_of_a_portrait_frame()
    {
        //Act
        IReadOnlyList<SafeAreaZone> grid = SafeAreaGuides.InstagramProfileGrid.GetUnsafeZones(Portrait);
        IReadOnlyList<SafeAreaZone> feed = SafeAreaGuides.InstagramFeedPreview.GetUnsafeZones(Portrait);

        //Assert - 3:4 keeps y 240..1680; 4:5 keeps y 285..1635
        grid.Count.Should().Be(2);
        grid[0].Bounds.Height.Should().BeApproximately(240f, 0.01f);
        grid[1].Bounds.Top.Should().BeApproximately(1680f, 0.01f);
        feed[0].Bounds.Height.Should().BeApproximately(285f, 0.01f);
        feed[1].Bounds.Top.Should().BeApproximately(1635f, 0.01f);
    }

    [Fact]
    public void A_portrait_crop_cuts_the_sides_of_a_landscape_frame()
    {
        //Act - a 4:5 crop of 1920 x 1080 keeps 864 x 1080, centered
        IReadOnlyList<SafeAreaZone> zones = SafeAreaGuides.InstagramFeedPreview.GetUnsafeZones(Landscape);

        //Assert
        zones.Count.Should().Be(2);
        zones[0].Bounds.Width.Should().BeApproximately(528f, 0.01f);
        zones[1].Bounds.Left.Should().BeApproximately(1392f, 0.01f);
    }

    [Fact]
    public void Broadcast_guides_use_the_smpte_margins()
    {
        //Act
        IReadOnlyList<SafeAreaZone> title = SafeAreaGuides.BroadcastTitleSafe.GetUnsafeZones(Landscape);
        IReadOnlyList<SafeAreaZone> action = SafeAreaGuides.BroadcastActionSafe.GetUnsafeZones(Landscape);

        //Assert
        Zone(title, "left margin").Bounds.Width.Should().BeApproximately(96f, 0.01f);
        Zone(title, "top margin").Bounds.Height.Should().BeApproximately(54f, 0.01f);
        Zone(action, "left margin").Bounds.Width.Should().BeApproximately(67.2f, 0.01f);
    }

    [Fact]
    public void End_screen_covers_the_whole_frame_only_in_the_final_twenty_seconds()
    {
        //Arrange
        SafeAreaGuide guide = SafeAreaGuides.YouTubeEndScreen;

        //Act
        IReadOnlyList<SafeAreaZone> early = guide.GetUnsafeZones(Landscape, seconds: 30, duration: 60);
        IReadOnlyList<SafeAreaZone> late = guide.GetUnsafeZones(Landscape, seconds: 41, duration: 60);
        IReadOnlyList<SafeAreaZone> unknownTime = guide.GetUnsafeZones(Landscape);
        IReadOnlyList<SafeAreaZone> unknownDuration = guide.GetUnsafeZones(Landscape, seconds: 1);

        //Assert
        early.Count.Should().Be(0);
        late.Count.Should().Be(1);
        late[0].ZoneName.Should().Be(SafeAreaGuide.WholeFrameZoneName);
        late[0].Bounds.Should().Be(new RectangleF(0, 0, 1920, 1080));
        unknownTime.Count.Should().Be(1);
        unknownDuration.Count.Should().Be(1);
    }

    [Fact]
    public void Between_rule_applies_inside_its_window_only()
    {
        //Arrange
        var guide = new SafeAreaGuide("Lower third card", new SafeAreaMargins(0f, 0.3f, 0f, 0f),
            timeRule: SafeAreaTimeRule.Between(5, 8));

        //Act + Assert
        guide.GetUnsafeZones(Landscape, 4.9).Count.Should().Be(0);
        guide.GetUnsafeZones(Landscape, 5).Count.Should().Be(1);
        guide.GetUnsafeZones(Landscape, 8).Count.Should().Be(1);
        guide.GetUnsafeZones(Landscape, 8.1).Count.Should().Be(0);
    }

    [Fact]
    public void Combine_keeps_every_part_zone_under_its_own_name()
    {
        //Act
        SafeAreaGuide everywhere = SafeAreaGuide.Combine("Everywhere",
            SafeAreaGuides.InstagramReelsAd, SafeAreaGuides.TikTok, SafeAreaGuides.YouTubeEndScreen);
        IReadOnlyList<SafeAreaZone> zones = everywhere.GetUnsafeZones(Portrait, seconds: 1, duration: 60);

        //Assert - the end screen does not apply at 1 s of 60
        everywhere.Parts.Count.Should().Be(3);
        zones.Count.Should().Be(8);
        zones.Select(z => z.GuideName).Distinct().Should().Equal("Instagram Reels ad", "TikTok");
        everywhere.Source.Should().Contain("TikTok: ");
    }

    [Fact]
    public void Combine_flattens_combined_guides()
    {
        //Act
        SafeAreaGuide inner = SafeAreaGuide.Combine("Inner", SafeAreaGuides.TikTok, SafeAreaGuides.YouTubeShortsAd);
        SafeAreaGuide outer = SafeAreaGuide.Combine("Outer", inner, SafeAreaGuides.BroadcastTitleSafe);

        //Assert
        outer.Parts.Count.Should().Be(3);
    }

    [Fact]
    public void Time_rules_report_their_window_and_its_duration()
    {
        //Act
        SafeAreaTimeRule final = SafeAreaTimeRule.FinalSeconds(20);
        SafeAreaTimeRule window = SafeAreaTimeRule.Between(5, 8);

        //Assert
        final.IsFinalSeconds.Should().BeTrue();
        final.Duration.Should().Be(20);
        final.StartTime.Should().Be(0);
        final.EndTime.Should().Be(0);
        final.AppliesAt(41, 60).Should().BeTrue();
        final.AppliesAt(39, 60).Should().BeFalse();
        window.IsFinalSeconds.Should().BeFalse();
        window.StartTime.Should().Be(5);
        window.EndTime.Should().Be(8);
        window.Duration.Should().Be(3);
        window.AppliesAt(null, null).Should().BeTrue();
    }

    [Fact]
    public void Custom_guides_and_margins_validate()
    {
        //Arrange
        Action noName = () => _ = new SafeAreaGuide(" ", SafeAreaMargins.None);
        Action tooTall = () => _ = new SafeAreaMargins(0.6f, 0.5f, 0f, 0f);
        Action negative = () => _ = new SafeAreaMargins(0f, 0f, -0.1f, 0f);
        Action badCrop = () => _ = new SafeAreaCrop("x", 0f, 1f);
        Action emptyCombine = () => SafeAreaGuide.Combine("x");
        Action badRule = () => SafeAreaTimeRule.FinalSeconds(0);

        //Act + Assert
        noName.Should().Throw<ArgumentException>();
        tooTall.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
        badCrop.Should().Throw<ArgumentOutOfRangeException>();
        emptyCombine.Should().Throw<ArgumentException>();
        badRule.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Margins_max_covers_both()
    {
        //Act
        SafeAreaMargins max = SafeAreaMargins.Max(new SafeAreaMargins(0.1f, 0.3f, 0.05f, 0.2f), new SafeAreaMargins(0.15f, 0.2f, 0.08f, 0.1f));

        //Assert
        max.Should().Be(new SafeAreaMargins(0.15f, 0.3f, 0.08f, 0.2f));
        SafeAreaMargins.None.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void A_crop_of_the_frame_aspect_removes_nothing()
    {
        //Arrange
        var guide = new SafeAreaGuide("Same", SafeAreaMargins.None, crops: new[] { new SafeAreaCrop("16:9", 16f, 9f) });

        //Act + Assert
        guide.GetUnsafeZones(Landscape).Count.Should().Be(0);
    }
}
