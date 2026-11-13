using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeBrix.Imaging.Drawing.Captions;

/// <summary>
/// A list of captions (<see cref="CaptionCue"/>) in time order, read from SRT or WebVTT text or
/// built in code, that answers "which cues show at time t". Immutable and thread-safe. Draw it
/// burned into frames with a <see cref="CaptionPresenter"/> (or
/// <see cref="DrawingSession.DrawCaptions"/>).
/// <para>
/// Parsing (shared managed code, no dependency) covers the basic cue syntax of both formats:
/// cue identifiers, timings ("hh:mm:ss,mmm" for SRT, "[hh:]mm:ss.mmm" for WebVTT; either
/// separator is accepted in both), multi-line cue text, markup tags such as &lt;i&gt;, &lt;b&gt;,
/// &lt;c.class&gt;, &lt;v Speaker&gt; and &lt;font&gt; (removed - the text keeps its words, styling
/// comes from the <see cref="CaptionStyle"/>), SRT "{\...}" override tags (removed), the character
/// references &amp;amp; &amp;lt; &amp;gt; &amp;quot; &amp;apos; &amp;nbsp; &amp;lrm; &amp;rlm; and
/// numeric ones, WebVTT NOTE/STYLE/REGION blocks (skipped) and the WebVTT cue settings
/// <c>align:</c>, <c>line:N%</c> and <c>position:N%</c> (others are kept in
/// <see cref="CaptionCue.Settings"/> but not applied). A malformed timing line throws
/// <see cref="FormatException"/> naming the line.
/// </para>
/// </summary>
public sealed class CaptionTrack
{
    private readonly CaptionCue[] _cues;

    /// <summary>
    /// Creates a track from cues in any order; they are kept sorted by start time (cues starting
    /// together keep their given order).
    /// </summary>
    /// <param name="cues">The cues.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cues"/> is null or holds a null cue.</exception>
    public CaptionTrack(IEnumerable<CaptionCue> cues)
    {
        if (cues == null) { throw new ArgumentNullException(nameof(cues)); }
        var list = new List<(CaptionCue Cue, int Order)>();
        foreach (CaptionCue cue in cues)
        {
            if (cue == null) { throw new ArgumentNullException(nameof(cues), "A cue is null."); }
            list.Add((cue, list.Count));
        }
        list.Sort((a, b) =>
        {
            int byStart = a.Cue.StartTime.CompareTo(b.Cue.StartTime);
            return byStart != 0 ? byStart : a.Order.CompareTo(b.Order);
        });
        _cues = new CaptionCue[list.Count];
        double end = 0;
        for (int i = 0; i < _cues.Length; i++)
        {
            _cues[i] = list[i].Cue;
            end = Math.Max(end, _cues[i].EndTime);
        }
        EndTime = end;
    }

    /// <summary>The cues, sorted by start time.</summary>
    public IReadOnlyList<CaptionCue> Cues => _cues;

    /// <summary>How many cues there are.</summary>
    public int Count => _cues.Length;

    /// <summary>When the last cue ends, in seconds (0 for an empty track).</summary>
    public double EndTime { get; }

    /// <summary>
    /// The cues that show at a time (StartTime &lt;= t &lt; EndTime), in start order.
    /// </summary>
    /// <param name="seconds">The time; NaN shows nothing.</param>
    /// <returns>A new list (empty when none show).</returns>
    public IReadOnlyList<CaptionCue> GetActiveCues(double seconds)
    {
        var active = new List<CaptionCue>();
        foreach (CaptionCue cue in _cues)
        {
            if (cue.StartTime > seconds) { break; }
            if (cue.IsActiveAt(seconds)) { active.Add(cue); }
        }
        return active;
    }

    /// <summary>The indices (in <see cref="Cues"/>) of the cues that show at a time, in start order.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns>A new list of indices.</returns>
    public IReadOnlyList<int> GetActiveCueIndices(double seconds)
    {
        var active = new List<int>();
        for (int i = 0; i < _cues.Length; i++)
        {
            if (_cues[i].StartTime > seconds) { break; }
            if (_cues[i].IsActiveAt(seconds)) { active.Add(i); }
        }
        return active;
    }

    /// <summary>Tells the two formats apart: text whose first line starts with "WEBVTT" is WebVTT, anything else SRT.</summary>
    /// <param name="text">The caption file's text.</param>
    /// <returns>The format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static CaptionFormat DetectFormat(string text)
    {
        if (text == null) { throw new ArgumentNullException(nameof(text)); }
        string trimmed = text.TrimStart((char)0xFEFF);
        return trimmed.StartsWith("WEBVTT", StringComparison.Ordinal) ? CaptionFormat.WebVtt : CaptionFormat.Srt;
    }

    /// <summary>Reads SRT or WebVTT text, telling them apart by <see cref="DetectFormat"/>.</summary>
    /// <param name="text">The caption file's text.</param>
    /// <returns>The track.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the text is malformed.</exception>
    public static CaptionTrack Parse(string text)
        => DetectFormat(text) == CaptionFormat.WebVtt ? ParseWebVtt(text) : ParseSrt(text);

    /// <summary>Reads text in a given format.</summary>
    /// <param name="text">The caption file's text.</param>
    /// <param name="format">Its format.</param>
    /// <returns>The track.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not defined.</exception>
    /// <exception cref="FormatException">Thrown when the text is malformed.</exception>
    public static CaptionTrack Parse(string text, CaptionFormat format) => format switch
    {
        CaptionFormat.Srt => ParseSrt(text),
        CaptionFormat.WebVtt => ParseWebVtt(text),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <summary>Reads SubRip (.srt) text.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The track.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when a cue has no valid timing line.</exception>
    public static CaptionTrack ParseSrt(string text)
    {
        if (text == null) { throw new ArgumentNullException(nameof(text)); }
        return new CaptionTrack(CaptionParser.ParseSrt(text));
    }

    /// <summary>Reads WebVTT (.vtt) text.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The track.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the "WEBVTT" header is missing or a timing line is malformed.</exception>
    public static CaptionTrack ParseWebVtt(string text)
    {
        if (text == null) { throw new ArgumentNullException(nameof(text)); }
        return new CaptionTrack(CaptionParser.ParseWebVtt(text));
    }

    /// <summary>Reads an SRT or WebVTT file (UTF-8), telling the formats apart by content.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The track.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="FormatException">Thrown when the text is malformed.</exception>
    public static CaptionTrack Load(string path)
    {
        if (String.IsNullOrEmpty(path)) { throw new ArgumentException("A caption file path is required.", nameof(path)); }
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }
}
