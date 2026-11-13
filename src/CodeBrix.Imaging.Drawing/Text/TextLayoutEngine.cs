using System;
using System.Collections.Generic;
using System.Text;
using CodeBrix.Imaging.Fonts;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// Lays out blocks of text - several lines, wrapped to a width, aligned, spaced, trimmed - in
/// fonts the caller registered (<see cref="FontRegistry"/>; system fonts are never used). Shaping
/// (glyph choice, advances, kerning, ligatures) is CodeBrix.Imaging's managed font code; line
/// breaking, alignment, justification and trimming are this library's own managed code. Nothing
/// touches a rasterizer, so both packages lay any text out EXACTLY the same, on every machine.
/// <para>
/// Line breaking: the text breaks at its own line breaks (LF, CR, CR LF, NEL, LS, PS, VT, FF) and,
/// with <see cref="TextLayoutOptions.MaxWidth"/>, wraps after spaces (not no-break spaces), after
/// hyphens inside words, after a zero width space and between CJK ideographs; a word wider than
/// the line is broken between characters. Spaces at a wrapped line's end hang past the width and
/// do not count in it. Text is laid out left to right; right-to-left scripts are not reordered.
/// </para>
/// </summary>
public static class TextLayoutEngine
{
    /// <summary>
    /// Lays out one uniformly styled block of text.
    /// </summary>
    /// <param name="fonts">The registry the font comes from.</param>
    /// <param name="text">The text; null is treated as empty.</param>
    /// <param name="size">The em size, in calibrated drawing units; must be positive and finite.</param>
    /// <param name="familyName">The family; or <c>null</c> for the registry's first font.</param>
    /// <param name="options">The layout settings; or <c>null</c> for the defaults.</param>
    /// <returns>The laid-out block.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size or an option is out of range.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the registry holds no font at all.</exception>
    public static TextLayoutResult Layout(FontRegistry fonts, string text, float size, string familyName = null, TextLayoutOptions options = null)
        => Layout(fonts, new[] { new TextRunDescriptor(text ?? String.Empty, size, familyName) }, options);

    /// <summary>
    /// Lays out a sequence of styled runs as one block; the runs' texts are concatenated in order.
    /// </summary>
    /// <param name="fonts">The registry the fonts come from.</param>
    /// <param name="runs">The runs; at least one (pass one run with empty text to lay out nothing).</param>
    /// <param name="options">The layout settings; or <c>null</c> for the defaults.</param>
    /// <returns>The laid-out block.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> or <paramref name="runs"/> is null, or a run is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="runs"/> is empty or a run's family is not registered.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the registry holds no font at all.</exception>
    public static TextLayoutResult Layout(FontRegistry fonts, IReadOnlyList<TextRunDescriptor> runs, TextLayoutOptions options = null)
    {
        if (fonts == null) { throw new ArgumentNullException(nameof(fonts)); }
        if (runs == null) { throw new ArgumentNullException(nameof(runs)); }
        if (runs.Count == 0) { throw new ArgumentException("At least one run is required; pass one run with empty text to lay out nothing.", nameof(runs)); }
        TextLayoutOptions settings = (options ?? new TextLayoutOptions()).Clone();
        settings.Validate();
        settings.Ellipsis ??= String.Empty;
        return new Builder(fonts, Copy(runs), settings).Build();
    }

    private static TextRunDescriptor[] Copy(IReadOnlyList<TextRunDescriptor> runs)
    {
        var copy = new TextRunDescriptor[runs.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = runs[i] ?? throw new ArgumentNullException(nameof(runs), "A run is null.");
        }
        return copy;
    }

    //A run's font and vertical metrics, resolved once
    private sealed class RunFont
    {
        public Font Font;
        public float Size;
        public float Ascent;
        public float Descent;
        public float Natural;
        public float Tracking;
    }

    //A cluster while the block is being laid out
    private struct Item
    {
        public int Start;
        public int Length;
        public float Advance;
        public bool IsWhiteSpace;
        public bool IsBreakingSpace;
        public int Run;
        public bool BreakAfter;
        public bool IsEllipsis;
        public string EllipsisText;
        public float X;
    }

    //A line while the block is being laid out: a range of items
    private sealed class Line
    {
        public List<Item> Items = new List<Item>();
        public int TextStart;
        public int TextEnd;
        public bool EndsParagraph;
        public int EmptyRun;
        public float Width;
        public float Ascent;
        public float Descent;
        public float Height;
    }

    private sealed class Builder
    {
        private readonly FontRegistry _fonts;
        private readonly TextRunDescriptor[] _runs;
        private readonly TextLayoutOptions _options;
        private readonly RunFont[] _runFonts;
        private readonly int[] _runStarts;
        private readonly string _text;

        public Builder(FontRegistry fonts, TextRunDescriptor[] runs, TextLayoutOptions options)
        {
            _fonts = fonts;
            _runs = runs;
            _options = options;
            _runFonts = new RunFont[runs.Length];
            _runStarts = new int[runs.Length + 1];
            var text = new StringBuilder();
            for (int i = 0; i < runs.Length; i++)
            {
                _runStarts[i] = text.Length;
                text.Append(runs[i].Text);
                Font font = fonts.CreateFont(runs[i].FamilyName, runs[i].Size, out _);
                float size = runs[i].Size;
                float ascent = TextShaping.Ascent(font, size);
                float descent = TextShaping.Descent(font, size);
                _runFonts[i] = new RunFont
                {
                    Font = font,
                    Size = size,
                    Ascent = ascent,
                    Descent = descent,
                    Natural = ascent + descent + TextShaping.LineGap(font, size),
                    Tracking = runs[i].Tracking * size,
                };
            }
            _runStarts[runs.Length] = text.Length;
            _text = text.ToString();
        }

        public TextLayoutResult Build()
        {
            List<Line> lines = BreakLines();

            //Line heights, then the lines that fit
            foreach (Line line in lines) { Measure(line); }
            int kept = 0;
            float used = 0f;
            foreach (Line line in lines)
            {
                if (_options.MaxLines > 0 && kept >= _options.MaxLines) { break; }
                if (kept > 0 && _options.MaxHeight.HasValue && used + line.Height > _options.MaxHeight.Value) { break; }
                used += line.Height;
                kept++;
            }
            bool truncated = kept < lines.Count;
            lines.RemoveRange(kept, lines.Count - kept);
            int shownEnd = _text.Length;
            if (truncated)
            {
                Line last = lines[lines.Count - 1];
                shownEnd = last.Items.Count > 0 ? last.Items[last.Items.Count - 1].Start + last.Items[last.Items.Count - 1].Length : last.TextStart;
                last.TextEnd = shownEnd;
                last.EndsParagraph = true;
                if (_options.Trimming != TextTrimming.None)
                {
                    AddEllipsis(last);
                    shownEnd = last.TextEnd;
                }
            }
            foreach (Line line in lines) { line.Width = VisibleWidth(line.Items, line.Items.Count); }

            //The box, then each line's place in it
            float boxWidth = _options.MaxWidth ?? 0f;
            if (!_options.MaxWidth.HasValue)
            {
                foreach (Line line in lines) { boxWidth = Math.Max(boxWidth, line.Width); }
            }
            float contentHeight = 0f;
            foreach (Line line in lines) { contentHeight += line.Height; }
            float boxHeight = _options.MaxHeight ?? contentHeight;
            float top = _options.VerticalAlign switch
            {
                TextVerticalAlign.Center => (boxHeight - contentHeight) / 2f,
                TextVerticalAlign.Bottom => boxHeight - contentHeight,
                _ => 0f,
            };

            var metrics = new TextLineMetrics[lines.Count];
            var clusters = new List<TextCluster>();
            int words = 0;
            for (int l = 0; l < lines.Count; l++)
            {
                Line line = lines[l];
                float extra = 0f;
                float left = Place(line, boxWidth, out extra);
                float baselineOffset = ((line.Height - (line.Ascent + line.Descent)) / 2f) + line.Ascent;
                float baseline = top + baselineOffset;
                int firstCluster = clusters.Count;
                bool previousWasWord = false;
                PositionItems(line, left, extra);
                if (extra > 0f) { line.Width = VisibleWidth(line.Items, line.Items.Count); }
                for (int i = 0; i < line.Items.Count; i++)
                {
                    Item item = line.Items[i];
                    int word = -1;
                    if (!item.IsWhiteSpace)
                    {
                        if (!previousWasWord) { words++; }
                        word = words - 1;
                    }
                    previousWasWord = !item.IsWhiteSpace;
                    clusters.Add(MakeCluster(clusters.Count, item, l, word, top, line.Height, baseline));
                }
                metrics[l] = new TextLineMetrics(l, line.TextStart, Math.Max(0, line.TextEnd - line.TextStart), top, line.Height,
                    baselineOffset, left, line.Width, line.Ascent, line.Descent, firstCluster, clusters.Count - firstCluster, line.EndsParagraph);
                top += line.Height;
            }

            return new TextLayoutResult(_text, _runs, _options, new SizeF(boxWidth, boxHeight), metrics, clusters.ToArray(), words, truncated, shownEnd);
        }

        //Splits the text into paragraphs, shapes each paragraph's runs and wraps them into lines
        private List<Line> BreakLines()
        {
            var lines = new List<Line>();
            int start = 0;
            while (true)
            {
                int end = FindBreak(start, out int breakLength);
                bool last = end >= _text.Length;
                List<Item> items = ShapeParagraph(start, end);
                WrapParagraph(items, start, end, end + breakLength, lines);
                if (last) { break; }
                start = end + breakLength;
                if (start >= _text.Length)
                {
                    //A break at the very end leaves one empty line after it
                    lines.Add(new Line { TextStart = start, TextEnd = start, EndsParagraph = true, EmptyRun = RunAt(start) });
                    break;
                }
            }
            return lines;
        }

        private int FindBreak(int start, out int length)
        {
            for (int i = start; i < _text.Length; i++)
            {
                char c = _text[i];
                if (c == '\r')
                {
                    length = i + 1 < _text.Length && _text[i + 1] == '\n' ? 2 : 1;
                    return i;
                }
                if (c == '\n' || c == '\u000B' || c == '\u000C' || c == '\u0085' || c == '\u2028' || c == '\u2029')
                {
                    length = 1;
                    return i;
                }
            }
            length = 0;
            return _text.Length;
        }

        private int RunAt(int index)
        {
            for (int r = 0; r < _runs.Length; r++)
            {
                if (index < _runStarts[r + 1]) { return r; }
            }
            return _runs.Length - 1;
        }

        private List<Item> ShapeParagraph(int start, int end)
        {
            var items = new List<Item>();
            var shaped = new List<TextShaping.Cluster>();
            for (int r = 0; r < _runs.Length; r++)
            {
                int s = Math.Max(start, _runStarts[r]);
                int e = Math.Min(end, _runStarts[r + 1]);
                if (e <= s) { continue; }
                shaped.Clear();
                TextShaping.Shape(_runFonts[r].Font, _text, s, e - s, _options.Kerning, shaped);
                foreach (TextShaping.Cluster c in shaped)
                {
                    items.Add(new Item
                    {
                        Start = c.Start,
                        Length = c.Length,
                        Advance = c.Advance,
                        IsWhiteSpace = c.IsWhiteSpace,
                        IsBreakingSpace = c.IsBreakingSpace,
                        Run = r,
                    });
                }
            }
            for (int i = 0; i + 1 < items.Count; i++)
            {
                Item item = items[i];
                item.BreakAfter = CanBreakBetween(items[i], items[i + 1]);
                items[i] = item;
            }
            return items;
        }

        private bool CanBreakBetween(Item a, Item b)
        {
            if (a.IsBreakingSpace) { return !b.IsWhiteSpace; }
            if (a.IsWhiteSpace || b.IsWhiteSpace) { return false; }
            int lastA = LastRune(a);
            int firstB = FirstRune(b);
            if (lastA == 0x200B) { return true; }
            if (IsIdeograph(lastA) || IsIdeograph(firstB)) { return true; }
            if ((lastA == '-' || lastA == 0x2010 || lastA == 0x2012 || lastA == 0x2013) && a.Length == 1)
            {
                //A hyphen inside a word: only between letters or digits
                return a.Start > 0 && IsWordChar(_text, a.Start - 1) && IsWordChar(_text, b.Start);
            }
            return false;
        }

        private static bool IsWordChar(string text, int index)
        {
            if (index < 0 || index >= text.Length) { return false; }
            Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out _);
            return Rune.IsLetterOrDigit(rune);
        }

        private int LastRune(Item item)
        {
            if (item.Length < 1) { return 0; }
            Rune.DecodeLastFromUtf16(_text.AsSpan(item.Start, item.Length), out Rune rune, out _);
            return rune.Value;
        }

        private int FirstRune(Item item)
        {
            if (item.Length < 1) { return 0; }
            Rune.DecodeFromUtf16(_text.AsSpan(item.Start, item.Length), out Rune rune, out _);
            return rune.Value;
        }

        private static bool IsIdeograph(int value)
            => (value >= 0x2E80 && value <= 0x2FDF) || (value >= 0x3040 && value <= 0x30FF) || (value >= 0x3400 && value <= 0x4DBF)
            || (value >= 0x4E00 && value <= 0x9FFF) || (value >= 0xF900 && value <= 0xFAFF) || (value >= 0xAC00 && value <= 0xD7AF)
            || (value >= 0x20000 && value <= 0x3FFFF);

        //Greedy wrapping: a line takes items until the next visible one would pass the width, then
        //  breaks at the last opportunity (or, with none, before that item)
        private void WrapParagraph(List<Item> items, int paragraphStart, int paragraphEnd, int nextParagraph, List<Line> lines)
        {
            if (items.Count == 0)
            {
                lines.Add(new Line { TextStart = paragraphStart, TextEnd = nextParagraph, EndsParagraph = true, EmptyRun = RunAt(paragraphStart) });
                return;
            }

            float? maxWidth = _options.MaxWidth;
            int s = 0;
            while (s < items.Count)
            {
                int e = items.Count - 1;
                if (maxWidth.HasValue)
                {
                    float pen = 0f;
                    int lastBreak = -1;
                    bool anyVisible = false;
                    for (int i = s; i < items.Count; i++)
                    {
                        Item item = items[i];
                        if (!item.IsWhiteSpace)
                        {
                            if (anyVisible && pen + item.Advance > maxWidth.Value)
                            {
                                e = lastBreak >= s ? lastBreak : i - 1;
                                break;
                            }
                            anyVisible = true;
                        }
                        pen += item.Advance + _runFonts[item.Run].Tracking;
                        if (item.BreakAfter) { lastBreak = i; }
                    }
                }

                var line = new Line { EmptyRun = items[s].Run };
                for (int i = s; i <= e; i++) { line.Items.Add(items[i]); }
                line.TextStart = s == 0 ? paragraphStart : items[s].Start;
                bool endsParagraph = e == items.Count - 1;
                line.TextEnd = endsParagraph ? nextParagraph : items[e + 1].Start;
                line.EndsParagraph = endsParagraph;
                lines.Add(line);
                s = e + 1;
            }
        }

        private void Measure(Line line)
        {
            float ascent = 0f, descent = 0f, natural = 0f;
            if (line.Items.Count == 0)
            {
                RunFont run = _runFonts[line.EmptyRun];
                ascent = run.Ascent;
                descent = run.Descent;
                natural = run.Natural;
            }
            foreach (Item item in line.Items)
            {
                RunFont run = _runFonts[item.Run];
                ascent = Math.Max(ascent, run.Ascent);
                descent = Math.Max(descent, run.Descent);
                natural = Math.Max(natural, run.Natural);
            }
            line.Ascent = ascent;
            line.Descent = descent;
            line.Height = _options.LineHeight > 0f ? _options.LineHeight : natural * _options.LineSpacing;
        }

        //The width from the first item's pen start to the last visible item's end, tracking
        //  between items included, over the first `count` items
        private float VisibleWidth(List<Item> items, int count)
        {
            int last = count - 1;
            while (last >= 0 && items[last].IsWhiteSpace) { last--; }
            float width = 0f;
            for (int i = 0; i <= last; i++)
            {
                width += items[i].Advance;
                if (i < last) { width += _runFonts[items[i].Run].Tracking; }
            }
            return width;
        }

        private void AddEllipsis(Line line)
        {
            int run = line.Items.Count > 0 ? line.Items[line.Items.Count - 1].Run : line.EmptyRun;
            string ellipsis = _options.Ellipsis;
            var shaped = new List<TextShaping.Cluster>();
            float ellipsisAdvance = 0f;
            if (ellipsis.Length > 0)
            {
                TextShaping.Shape(_runFonts[run].Font, ellipsis, 0, ellipsis.Length, _options.Kerning, shaped);
                foreach (TextShaping.Cluster c in shaped) { ellipsisAdvance += c.Advance; }
            }

            float available = _options.MaxWidth ?? Single.PositiveInfinity;
            List<Item> items = line.Items;
            int keep = items.Count;
            while (keep > 0 && items[keep - 1].IsWhiteSpace) { keep--; }
            int characterKeep = keep;
            while (characterKeep > 0 && !Fits(items, characterKeep, ellipsisAdvance, available))
            {
                characterKeep--;
                while (characterKeep > 0 && items[characterKeep - 1].IsWhiteSpace) { characterKeep--; }
            }
            int newCount = characterKeep;
            if (_options.Trimming == TextTrimming.WordEllipsis)
            {
                int wordKeep = keep;
                while (wordKeep > 0 && !(Fits(items, wordKeep, ellipsisAdvance, available) && EndsWord(items, wordKeep)))
                {
                    wordKeep--;
                    while (wordKeep > 0 && items[wordKeep - 1].IsWhiteSpace) { wordKeep--; }
                }
                if (wordKeep > 0) { newCount = wordKeep; }
            }

            items.RemoveRange(newCount, items.Count - newCount);
            int cut = newCount > 0 ? items[newCount - 1].Start + items[newCount - 1].Length : line.TextStart;
            line.TextEnd = cut;
            if (ellipsis.Length > 0)
            {
                items.Add(new Item
                {
                    Start = cut,
                    Length = 0,
                    Advance = ellipsisAdvance,
                    Run = run,
                    IsEllipsis = true,
                    EllipsisText = ellipsis,
                });
            }
        }

        //Whether the first `count` items plus the ellipsis fit the width
        private bool Fits(List<Item> items, int count, float ellipsis, float available)
        {
            float width = VisibleWidth(items, count);
            if (count > 0) { width += _runFonts[items[count - 1].Run].Tracking; }
            return width + ellipsis <= available;
        }

        //Whether a cut after the first `count` items falls at a word's end
        private static bool EndsWord(List<Item> items, int count)
            => count == items.Count || items[count].IsWhiteSpace || items[count - 1].BreakAfter;

        //The line's left edge in the box, and (justified) the stretch each inner space gets
        private float Place(Line line, float boxWidth, out float extra)
        {
            extra = 0f;
            float slack = boxWidth - line.Width;
            switch (_options.Align)
            {
                case TextAlign.Center:
                    return slack / 2f;
                case TextAlign.Right:
                    return slack;
                case TextAlign.Justify:
                    if (!line.EndsParagraph && slack > 0f)
                    {
                        int spaces = CountInnerSpaces(line.Items);
                        if (spaces > 0) { extra = slack / spaces; }
                    }
                    return 0f;
                default:
                    return 0f;
            }
        }

        private static int CountInnerSpaces(List<Item> items)
        {
            int first = 0;
            while (first < items.Count && items[first].IsWhiteSpace) { first++; }
            int last = items.Count - 1;
            while (last >= 0 && items[last].IsWhiteSpace) { last--; }
            int spaces = 0;
            for (int i = first; i < last; i++)
            {
                if (items[i].IsWhiteSpace) { spaces++; }
            }
            return spaces;
        }

        private void PositionItems(Line line, float left, float extra)
        {
            List<Item> items = line.Items;
            int first = 0;
            while (first < items.Count && items[first].IsWhiteSpace) { first++; }
            int last = items.Count - 1;
            while (last >= 0 && items[last].IsWhiteSpace) { last--; }
            float x = left;
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                item.X = x;
                if (extra > 0f && item.IsWhiteSpace && i > first && i < last) { item.Advance += extra; }
                items[i] = item;
                x += item.Advance;
                if (i < items.Count - 1) { x += _runFonts[item.Run].Tracking; }
            }
        }

        private TextCluster MakeCluster(int index, Item item, int lineIndex, int wordIndex, float lineTop, float lineHeight, float baseline)
        {
            RunFont run = _runFonts[item.Run];
            string text = item.IsEllipsis ? item.EllipsisText : _text.Substring(item.Start, item.Length);
            byte[] verbs = null;
            float[] points = null;
            RectangleF? ink = null;
            if (!item.IsWhiteSpace && text.Length > 0)
            {
                var builder = new TextOutline.Builder();
                TextShaping.Render(builder, run.Font, text, item.X, baseline - run.Ascent, _options.Kerning);
                if (builder.Verbs.Count > 0)
                {
                    verbs = builder.Verbs.ToArray();
                    points = builder.Points.ToArray();
                    ink = builder.GetInkBounds();
                }
            }
            return new TextCluster(index, item.Start, item.Length, text, lineIndex, wordIndex, item.Run, item.IsWhiteSpace, item.IsEllipsis,
                new PointF(item.X, baseline), item.Advance, new RectangleF(item.X, lineTop, item.Advance, lineHeight), verbs, points, ink);
        }
    }
}
