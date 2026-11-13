namespace CodeBrix.Imaging.Drawing.Captions;

/// <summary>The caption file formats <see cref="CaptionTrack"/> reads.</summary>
public enum CaptionFormat
{
    /// <summary>SubRip (.srt): numbered cues, "00:00:01,000 --> 00:00:04,000" timings.</summary>
    Srt = 0,

    /// <summary>WebVTT (.vtt): a "WEBVTT" header, "00:01.000 --> 00:04.000" timings and optional cue settings.</summary>
    WebVtt = 1,
}
