using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Captions;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Captions;

public class CaptionTrackTests
{
    private const string Srt =
        "1\r\n" +
        "00:00:01,000 --> 00:00:04,500\r\n" +
        "Hello <i>there</i>,\r\n" +
        "{\\an8}friend &amp; neighbour\r\n" +
        "\r\n" +
        "2\r\n" +
        "00:00:03,250 --> 00:00:06,000\r\n" +
        "<font color=\"#ffcc00\">Second</font> cue\r\n";

    private const string Vtt =
        "WEBVTT - a sample\n" +
        "\n" +
        "NOTE this block is a comment\n" +
        "and spans two lines\n" +
        "\n" +
        "intro\n" +
        "00:01.000 --> 00:02.500 align:start line:10%\n" +
        "<v Narrator>Once upon</v> a <b>time</b>\n" +
        "\n" +
        "01:00:00.000 --> 01:00:01.000 position:25%\n" +
        "An hour in &lt;3&gt; &#65;&#x42;\n";

    [Fact]
    public void ParseSrt_reads_numbers_timings_and_cleans_the_text()
    {
        //Act
        CaptionTrack track = CaptionTrack.ParseSrt(Srt);

        //Assert
        track.Count.Should().Be(2);
        track.Cues[0].Identifier.Should().Be("1");
        track.Cues[0].StartTime.Should().Be(1.0);
        track.Cues[0].EndTime.Should().Be(4.5);
        track.Cues[0].Text.Should().Be("Hello there,\nfriend & neighbour");
        track.Cues[1].StartTime.Should().Be(3.25);
        track.Cues[1].Text.Should().Be("Second cue");
        track.EndTime.Should().Be(6.0);
    }

    [Fact]
    public void ParseWebVtt_skips_notes_and_applies_basic_settings()
    {
        //Act
        CaptionTrack track = CaptionTrack.ParseWebVtt(Vtt);

        //Assert
        track.Count.Should().Be(2);
        CaptionCue intro = track.Cues[0];
        intro.Identifier.Should().Be("intro");
        intro.StartTime.Should().Be(1.0);
        intro.EndTime.Should().Be(2.5);
        intro.Text.Should().Be("Once upon a time");
        intro.Align.Should().Be(TextAlign.Left);
        intro.Settings.Should().Be("align:start line:10%");
        intro.Position.Value.Anchor.Should().Be(new LayoutAnchor(0.5f, 0f));
        intro.Position.Value.FractionOffset.Y.Should().BeApproximately(0.1f, 0.00001f);
        CaptionCue hour = track.Cues[1];
        hour.StartTime.Should().Be(3600.0);
        hour.Identifier.Should().BeNull();
        hour.Text.Should().Be("An hour in <3> AB");
        hour.Position.Value.Reference.Should().Be(LayoutReference.SafeArea);
        hour.Position.Value.Anchor.X.Should().BeApproximately(0.25f, 0.00001f);
    }

    [Fact]
    public void Parse_tells_the_formats_apart()
    {
        //Act
        CaptionFormat vtt = CaptionTrack.DetectFormat(Vtt);
        CaptionFormat srt = CaptionTrack.DetectFormat(Srt);
        CaptionTrack parsed = CaptionTrack.Parse(((char)0xFEFF) + Vtt);

        //Assert
        vtt.Should().Be(CaptionFormat.WebVtt);
        srt.Should().Be(CaptionFormat.Srt);
        parsed.Count.Should().Be(2);
    }

    [Fact]
    public void GetActiveCues_uses_half_open_spans_in_start_order()
    {
        //Arrange
        CaptionTrack track = CaptionTrack.ParseSrt(Srt);

        //Act
        IReadOnlyList<CaptionCue> before = track.GetActiveCues(0.999);
        IReadOnlyList<CaptionCue> both = track.GetActiveCues(3.5);
        IReadOnlyList<CaptionCue> atEnd = track.GetActiveCues(4.5);
        IReadOnlyList<CaptionCue> after = track.GetActiveCues(6.0);

        //Assert
        before.Count.Should().Be(0);
        both.Count.Should().Be(2);
        both[0].Identifier.Should().Be("1");
        atEnd.Count.Should().Be(1);
        atEnd[0].Identifier.Should().Be("2");
        after.Count.Should().Be(0);
        track.GetActiveCueIndices(3.5).Should().Equal(0, 1);
    }

    [Fact]
    public void Cues_built_in_code_are_sorted_by_start()
    {
        //Act
        var track = new CaptionTrack(new[]
        {
            new CaptionCue(5, 6, "late"),
            new CaptionCue(1, 2, "early"),
            new CaptionCue(1, 3, "early too"),
        });

        //Assert
        track.Cues[0].Text.Should().Be("early");
        track.Cues[1].Text.Should().Be("early too");
        track.Cues[2].Text.Should().Be("late");
    }

    [Fact]
    public void Character_references_and_no_break_spaces_are_decoded()
    {
        //Act
        CaptionTrack track = CaptionTrack.ParseWebVtt("WEBVTT\n\n00:00.000 --> 00:01.000\na&nbsp;b &quot;c&quot; &unknown; d\n");

        //Assert
        track.Cues[0].Text.Should().Be("a" + (char)0xA0 + "b \"c\" &unknown; d");
    }

    [Theory]
    [InlineData("1\n00:00:01,000 --> soon\nno end time\n")]
    [InlineData("1\n00:00:01,000 -> 00:00:02,000\nbad arrow\n")]
    [InlineData("1\n00:00:05,000 --> 00:00:02,000\nbackwards\n")]
    [InlineData("1\n00:61:00,000 --> 00:62:00,000\nbad minutes\n")]
    public void ParseSrt_rejects_malformed_timings_naming_the_line(string text)
    {
        //Act
        Action parse = () => CaptionTrack.ParseSrt(text);

        //Assert
        parse.Should().Throw<FormatException>().WithMessage("Line *");
    }

    [Fact]
    public void ParseWebVtt_requires_the_signature()
    {
        //Act
        Action parse = () => CaptionTrack.ParseWebVtt("00:01.000 --> 00:02.000\nhi\n");
        Action nullText = () => CaptionTrack.ParseWebVtt(null);

        //Assert
        parse.Should().Throw<FormatException>();
        nullText.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void A_cue_rejects_a_backwards_span()
    {
        //Act
        Action backwards = () => _ = new CaptionCue(2, 1, "x");
        Action infinite = () => _ = new CaptionCue(Double.NaN, 1, "x");

        //Assert
        backwards.Should().Throw<ArgumentOutOfRangeException>();
        infinite.Should().Throw<ArgumentOutOfRangeException>();
    }
}
