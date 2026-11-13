using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using CodeBrix.Imaging.Fonts;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// The shaping half of the text engine, on CodeBrix.Imaging's managed font code: a run of text
/// is shaped ONCE as a whole (so kerning, ligatures and mark composition apply), and the shaped
/// glyphs are mapped back onto the text as clusters - the units every later step (line
/// breaking, tracking, reveal, per-character animation) works in. Each cluster is then outlined
/// on its own at the position the whole-run shaping gave it.
/// </summary>
internal static class TextShaping
{
    /// <summary>One shaped cluster of a run: which characters it covers and how far it advances the pen.</summary>
    internal struct Cluster
    {
        /// <summary>The index of its first character in the text that was shaped.</summary>
        public int Start;

        /// <summary>How many characters (UTF-16 units) it covers.</summary>
        public int Length;

        /// <summary>The pen advance the whole-run shaping gave it (kerning included when enabled).</summary>
        public float Advance;

        /// <summary>Whether every character in it is white space.</summary>
        public bool IsWhiteSpace;

        /// <summary>Whether every character in it is a space a line may break after (not a no-break space).</summary>
        public bool IsBreakingSpace;
    }

    /// <summary>The font stack's kerning switch for a layout's kerning flag.</summary>
    /// <param name="kerning">Whether kerning is wanted.</param>
    /// <returns>The kerning mode.</returns>
    public static KerningMode ModeOf(bool kerning) => kerning ? KerningMode.Normal : KerningMode.None;

    /// <summary>
    /// Shapes part of a text as one run and maps the shaped glyphs back onto it.
    /// </summary>
    /// <param name="font">The font, already at its size.</param>
    /// <param name="text">The whole text.</param>
    /// <param name="start">Where the run starts.</param>
    /// <param name="length">How many characters it covers.</param>
    /// <param name="kerning">Whether to kern.</param>
    /// <param name="clusters">Receives the clusters, in order, with indices into <paramref name="text"/>.</param>
    public static void Shape(Font font, string text, int start, int length, bool kerning, List<Cluster> clusters)
    {
        if (length <= 0) { return; }
        string segment = text.Substring(start, length);
        var options = new TextOptions(font) { Dpi = 72f, KerningMode = ModeOf(kerning) };
        TextMeasurer.TryMeasureCharacterDimensions(segment, options, out GlyphBounds[] glyphs);

        //The characters, as code points (a lone surrogate counts as one unit)
        var runeStarts = new List<int>(segment.Length);
        var runeValues = new List<int>(segment.Length);
        int index = 0;
        while (index < segment.Length)
        {
            Rune.DecodeFromUtf16(segment.AsSpan(index), out Rune rune, out int consumed);
            if (consumed < 1) { consumed = 1; }
            runeStarts.Add(index);
            runeValues.Add(rune.Value);
            index += consumed;
        }

        int first = clusters.Count;
        int next = 0; //the first character not yet given to a cluster
        int previousGlyphValue = -1;
        foreach (GlyphBounds glyph in glyphs)
        {
            int value = glyph.Codepoint.Value;
            float advance = IsIgnorable(value) ? 0f : glyph.Bounds.Width;
            if (next < runeValues.Count && runeValues[next] == value)
            {
                clusters.Add(new Cluster { Start = runeStarts[next], Length = 0, Advance = advance });
                next++;
            }
            else
            {
                int match = value == previousGlyphValue ? -1 : FindFrom(runeValues, next, value);
                if (match >= 0 && clusters.Count > first)
                {
                    //The characters between were folded into the previous glyph (a ligature)
                    clusters.Add(new Cluster { Start = runeStarts[match], Length = 0, Advance = advance });
                    next = match + 1;
                }
                else if (match >= 0)
                {
                    //Leading characters with no glyph of their own join the first cluster
                    clusters.Add(new Cluster { Start = runeStarts[0], Length = 0, Advance = advance });
                    next = match + 1;
                }
                else if (clusters.Count > first)
                {
                    //One character shaped into several glyphs: they all belong to the previous cluster
                    Cluster last = clusters[clusters.Count - 1];
                    last.Advance += advance;
                    clusters[clusters.Count - 1] = last;
                }
                else
                {
                    clusters.Add(new Cluster { Start = 0, Length = 0, Advance = advance });
                    next = Math.Max(next, 1);
                }
            }
            previousGlyphValue = value;
        }
        if (clusters.Count == first)
        {
            clusters.Add(new Cluster { Start = 0, Length = 0, Advance = 0f });
        }

        //Each cluster runs to the next one's start (the last to the end), then is classified
        for (int i = first; i < clusters.Count; i++)
        {
            Cluster cluster = clusters[i];
            int end = i + 1 < clusters.Count ? clusters[i + 1].Start : segment.Length;
            cluster.Length = Math.Max(0, end - cluster.Start);
            Classify(segment, ref cluster);
            cluster.Start += start;
            clusters[i] = cluster;
        }
    }

    /// <summary>
    /// Outlines one cluster's characters alone, with the pen starting at (<paramref name="x"/>,
    /// baseline) where the baseline is <paramref name="top"/> plus the font's ascent.
    /// </summary>
    /// <param name="builder">Receives the outline.</param>
    /// <param name="font">The font, already at its size.</param>
    /// <param name="text">The cluster's characters.</param>
    /// <param name="x">The pen's start.</param>
    /// <param name="top">The baseline minus the font's ascent.</param>
    /// <param name="kerning">Whether to kern (matters only inside a multi-glyph cluster).</param>
    public static void Render(TextOutline.Builder builder, Font font, string text, float x, float top, bool kerning)
    {
        var options = new TextOptions(font) { Dpi = 72f, KerningMode = ModeOf(kerning), Origin = new Vector2(x, top) };
        TextRenderer.RenderTextTo(builder, text, options);
    }

    /// <summary>The font's ascent at its size, in calibrated units (one unit per point at 72 DPI).</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The em size.</param>
    /// <returns>The ascent.</returns>
    public static float Ascent(Font font, float size) => font.FontMetrics.Ascender * size / font.FontMetrics.UnitsPerEm;

    /// <summary>The font's descent (positive, below the baseline) at its size.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The em size.</param>
    /// <returns>The descent.</returns>
    public static float Descent(Font font, float size) => -font.FontMetrics.Descender * size / font.FontMetrics.UnitsPerEm;

    /// <summary>The font's line gap at its size.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The em size.</param>
    /// <returns>The line gap.</returns>
    public static float LineGap(Font font, float size) => font.FontMetrics.LineGap * size / font.FontMetrics.UnitsPerEm;

    /// <summary>Whether a character never breaks a line although it is a space.</summary>
    /// <param name="value">The code point.</param>
    /// <returns><c>true</c> for the no-break spaces.</returns>
    public static bool IsNoBreakSpace(int value) => value == 0x00A0 || value == 0x2007 || value == 0x202F;

    /// <summary>
    /// Whether a code point is Unicode "default ignorable" (zero width joiners, the zero width
    /// space, direction marks, variation selectors...): the font stack draws nothing for it, so it
    /// advances the pen by nothing either.
    /// </summary>
    /// <param name="value">The code point.</param>
    /// <returns><c>true</c> when the code point takes no space.</returns>
    public static bool IsIgnorable(int value)
    {
        if (value < 0x00AD) { return false; }
        return value == 0x00AD || value == 0x034F || value == 0x061C
            || (value >= 0x17B4 && value <= 0x17B5) || (value >= 0x180B && value <= 0x180F)
            || (value >= 0x200B && value <= 0x200F) || (value >= 0x202A && value <= 0x202E)
            || (value >= 0x2060 && value <= 0x206F) || (value >= 0xFE00 && value <= 0xFE0F)
            || value == 0xFEFF || (value >= 0xFFF0 && value <= 0xFFF8)
            || (value >= 0x1D173 && value <= 0x1D17A) || (value >= 0xE0000 && value <= 0xE0FFF);
    }

    private static int FindFrom(List<int> values, int from, int value)
    {
        for (int i = from; i < values.Count; i++)
        {
            if (values[i] == value) { return i; }
        }
        return -1;
    }

    private static void Classify(string segment, ref Cluster cluster)
    {
        bool white = cluster.Length > 0;
        bool breaking = cluster.Length > 0;
        int index = cluster.Start;
        int end = cluster.Start + cluster.Length;
        while (index < end)
        {
            Rune.DecodeFromUtf16(segment.AsSpan(index, end - index), out Rune rune, out int consumed);
            if (consumed < 1) { consumed = 1; }
            if (!Rune.IsWhiteSpace(rune))
            {
                white = false;
                breaking = false;
                break;
            }
            if (IsNoBreakSpace(rune.Value)) { breaking = false; }
            index += consumed;
        }
        cluster.IsWhiteSpace = white;
        cluster.IsBreakingSpace = breaking;
    }
}
