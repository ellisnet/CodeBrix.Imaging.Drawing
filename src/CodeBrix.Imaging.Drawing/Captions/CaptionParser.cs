using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.Captions;

/// <summary>
/// The SRT and WebVTT readers behind <see cref="CaptionTrack"/>: the basic cue syntax of each,
/// in plain managed code with invariant-culture number parsing.
/// </summary>
internal static class CaptionParser
{
    private const string Arrow = "-->";

    public static List<CaptionCue> ParseSrt(string text)
    {
        var cues = new List<CaptionCue>();
        foreach (List<(int Number, string Text)> block in Blocks(Lines(text)))
        {
            int timing = block[0].Text.Contains(Arrow, StringComparison.Ordinal) ? 0 : 1;
            if (timing >= block.Count || !block[timing].Text.Contains(Arrow, StringComparison.Ordinal))
            {
                throw new FormatException($"Line {block[Math.Min(timing, block.Count - 1)].Number}: expected an SRT timing line (\"00:00:01,000 --> 00:00:04,000\").");
            }
            ParseTiming(block[timing], out double start, out double end, out _);
            string identifier = timing == 1 ? block[0].Text.Trim() : null;
            cues.Add(new CaptionCue(start, end, CueText(block, timing + 1, srt: true), identifier));
        }
        return cues;
    }

    public static List<CaptionCue> ParseWebVtt(string text)
    {
        List<(int Number, string Text)> lines = Lines(text);
        if (lines.Count == 0 || !IsSignature(lines[0].Text))
        {
            throw new FormatException("Line 1: a WebVTT file must start with \"WEBVTT\".");
        }

        var cues = new List<CaptionCue>();
        bool header = true;
        foreach (List<(int Number, string Text)> block in Blocks(lines))
        {
            if (header)
            {
                //The first block is the header (the signature line and anything up to a blank line)
                header = false;
                if (block[0].Number == lines[0].Number) { continue; }
            }
            string first = block[0].Text;
            if (StartsWord(first, "NOTE") || StartsWord(first, "STYLE") || StartsWord(first, "REGION")) { continue; }

            int timing = first.Contains(Arrow, StringComparison.Ordinal) ? 0 : 1;
            if (timing >= block.Count || !block[timing].Text.Contains(Arrow, StringComparison.Ordinal))
            {
                throw new FormatException($"Line {block[Math.Min(timing, block.Count - 1)].Number}: expected a WebVTT timing line (\"00:01.000 --> 00:04.000\").");
            }
            ParseTiming(block[timing], out double start, out double end, out string settings);
            string identifier = timing == 1 ? first.Trim() : null;
            ApplySettings(settings, out LayoutPosition? position, out TextAlign? align);
            cues.Add(new CaptionCue(start, end, CueText(block, timing + 1, srt: false), identifier)
            {
                Settings = String.IsNullOrWhiteSpace(settings) ? null : settings.Trim(),
                Position = position,
                Align = align,
            });
        }
        return cues;
    }

    private static bool IsSignature(string line)
    {
        if (!line.StartsWith("WEBVTT", StringComparison.Ordinal)) { return false; }
        return line.Length == 6 || line[6] == ' ' || line[6] == '\t';
    }

    private static bool StartsWord(string line, string word)
        => line.StartsWith(word, StringComparison.Ordinal) && (line.Length == word.Length || line[word.Length] == ' ' || line[word.Length] == '\t');

    //The text's lines with 1-based numbers; CR LF and CR count as LF, a leading BOM is dropped
    private static List<(int Number, string Text)> Lines(string text)
    {
        string normal = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normal.Length > 0 && normal[0] == (char)0xFEFF) { normal = normal.Substring(1); }
        string[] split = normal.Split('\n');
        var lines = new List<(int, string)>(split.Length);
        for (int i = 0; i < split.Length; i++) { lines.Add((i + 1, split[i])); }
        return lines;
    }

    //Runs of non-blank lines
    private static IEnumerable<List<(int Number, string Text)>> Blocks(List<(int Number, string Text)> lines)
    {
        var block = new List<(int, string)>();
        foreach ((int number, string text) in lines)
        {
            if (text.Trim().Length == 0)
            {
                if (block.Count > 0)
                {
                    yield return block;
                    block = new List<(int, string)>();
                }
                continue;
            }
            block.Add((number, text));
        }
        if (block.Count > 0) { yield return block; }
    }

    private static void ParseTiming((int Number, string Text) line, out double start, out double end, out string settings)
    {
        string text = line.Text;
        int arrow = text.IndexOf(Arrow, StringComparison.Ordinal);
        string left = text.Substring(0, arrow).Trim();
        string right = text.Substring(arrow + Arrow.Length).Trim();
        int space = right.IndexOfAny(new[] { ' ', '\t' });
        string endText = space < 0 ? right : right.Substring(0, space);
        settings = space < 0 ? String.Empty : right.Substring(space + 1);
        if (!TryParseTime(left, out start) || !TryParseTime(endText, out end))
        {
            throw new FormatException($"Line {line.Number}: \"{text.Trim()}\" is not a valid timing (expected \"start --> end\").");
        }
        if (end < start)
        {
            throw new FormatException($"Line {line.Number}: the cue ends before it starts.");
        }
    }

    //[hh:]mm:ss[.,]fff -> seconds, through whole milliseconds so the value is exact
    internal static bool TryParseTime(string text, out double seconds)
    {
        seconds = 0;
        string[] parts = text.Split(':');
        if (parts.Length < 2 || parts.Length > 3) { return false; }
        long hours = 0;
        int index = 0;
        if (parts.Length == 3)
        {
            if (!TryDigits(parts[0], 1, 9, out hours)) { return false; }
            index = 1;
        }
        if (!TryDigits(parts[index], 1, 2, out long minutes) || minutes > 59) { return false; }
        string last = parts[index + 1];
        int separator = last.IndexOfAny(new[] { '.', ',' });
        string wholeText = separator < 0 ? last : last.Substring(0, separator);
        string fractionText = separator < 0 ? String.Empty : last.Substring(separator + 1);
        if (!TryDigits(wholeText, 1, 2, out long wholeSeconds) || wholeSeconds > 59) { return false; }
        long millis = 0;
        if (separator >= 0)
        {
            if (!TryDigits(fractionText, 1, 3, out long fraction)) { return false; }
            millis = fraction * (fractionText.Length == 1 ? 100 : fractionText.Length == 2 ? 10 : 1);
        }
        long total = (((hours * 60) + minutes) * 60 + wholeSeconds) * 1000 + millis;
        seconds = total / 1000.0;
        return true;
    }

    private static bool TryDigits(string text, int minLength, int maxLength, out long value)
    {
        value = 0;
        if (text.Length < minLength || text.Length > maxLength) { return false; }
        foreach (char c in text)
        {
            if (c < '0' || c > '9') { return false; }
            value = (value * 10) + (c - '0');
        }
        return true;
    }

    private static string CueText(List<(int Number, string Text)> block, int from, bool srt)
    {
        var text = new StringBuilder();
        for (int i = from; i < block.Count; i++)
        {
            if (text.Length > 0) { text.Append('\n'); }
            text.Append(Clean(block[i].Text, srt));
        }
        return text.ToString();
    }

    //Drops markup tags (and SRT {\...} overrides) and decodes character references
    internal static string Clean(string line, bool srt)
    {
        var text = new StringBuilder(line.Length);
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (c == '<')
            {
                int close = line.IndexOf('>', i + 1);
                if (close > i)
                {
                    i = close + 1;
                    continue;
                }
            }
            if (srt && c == '{' && i + 1 < line.Length && line[i + 1] == '\\')
            {
                int close = line.IndexOf('}', i + 2);
                if (close > i)
                {
                    i = close + 1;
                    continue;
                }
            }
            if (c == '&')
            {
                int semicolon = line.IndexOf(';', i + 1);
                if (semicolon > i && semicolon - i <= 10 && TryDecode(line.Substring(i + 1, semicolon - i - 1), out string decoded))
                {
                    text.Append(decoded);
                    i = semicolon + 1;
                    continue;
                }
            }
            text.Append(c);
            i++;
        }
        return text.ToString().Trim();
    }

    private static bool TryDecode(string name, out string decoded)
    {
        decoded = name switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            "nbsp" => ((char)0x00A0).ToString(),
            "lrm" => ((char)0x200E).ToString(),
            "rlm" => ((char)0x200F).ToString(),
            _ => null,
        };
        if (decoded != null) { return true; }
        if (name.Length > 1 && name[0] == '#')
        {
            bool hex = name.Length > 2 && (name[1] == 'x' || name[1] == 'X');
            string digits = hex ? name.Substring(2) : name.Substring(1);
            NumberStyles style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
            if (Int32.TryParse(digits, style, CultureInfo.InvariantCulture, out int value) && value > 0 && value <= 0x10FFFF
                && (value < 0xD800 || value > 0xDFFF))
            {
                decoded = Char.ConvertFromUtf32(value);
                return true;
            }
        }
        return false;
    }

    //WebVTT settings: align (start/left, center/middle, end/right), line:N% (the box's top at N% of
    //  the frame height), position:N% (the box's anchor at N% of the frame width)
    private static void ApplySettings(string settings, out LayoutPosition? position, out TextAlign? align)
    {
        position = null;
        align = null;
        if (String.IsNullOrWhiteSpace(settings)) { return; }
        float? line = null;
        float? horizontal = null;
        foreach (string token in settings.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = token.IndexOf(':');
            if (colon <= 0) { continue; }
            string key = token.Substring(0, colon);
            string value = token.Substring(colon + 1);
            int comma = value.IndexOf(',');
            if (comma >= 0) { value = value.Substring(0, comma); }
            switch (key)
            {
                case "align":
                    align = value switch
                    {
                        "start" or "left" => TextAlign.Left,
                        "center" or "middle" => TextAlign.Center,
                        "end" or "right" => TextAlign.Right,
                        _ => align,
                    };
                    break;
                case "line":
                    if (TryPercent(value, out float l)) { line = l; }
                    break;
                case "position":
                    if (TryPercent(value, out float p)) { horizontal = p; }
                    break;
            }
        }
        if (line.HasValue)
        {
            float x = horizontal ?? 0.5f;
            position = new LayoutPosition(new LayoutAnchor(x, 0f), LayoutReference.Frame, 0f, 0f, 0f, line.Value);
        }
        else if (horizontal.HasValue)
        {
            position = new LayoutPosition(new LayoutAnchor(horizontal.Value, 1f), LayoutReference.SafeArea);
        }
    }

    private static bool TryPercent(string value, out float fraction)
    {
        fraction = 0f;
        if (!value.EndsWith('%')) { return false; }
        if (!Single.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float percent))
        {
            return false;
        }
        if (percent < 0f || percent > 100f) { return false; }
        fraction = percent / 100f;
        return true;
    }
}
