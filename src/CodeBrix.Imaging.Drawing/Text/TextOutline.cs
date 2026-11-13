using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Fonts;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// A line of text laid out and turned into glyph outlines, entirely in managed code through
/// CodeBrix.Imaging's font support - so both packages draw exactly the same outline
/// geometry. The outline is stored relative to the line's own origin (pen start at x = 0,
/// baseline at y = 0); a text shape offsets it to its anchor when it draws.
/// </summary>
internal sealed class TextOutline
{
    internal const byte VerbMove = 0;
    internal const byte VerbLine = 1;
    internal const byte VerbQuad = 2;
    internal const byte VerbCubic = 3;
    internal const byte VerbClose = 4;

    private TextOutline(byte[] verbs, float[] points, float advance, string familyName, RectangleF? ink)
    {
        Verbs = verbs;
        Points = points;
        Advance = advance;
        FamilyName = familyName;
        InkBounds = ink;
    }

    /// <summary>The path verbs, in order.</summary>
    public byte[] Verbs { get; }

    /// <summary>The verbs' points as x, y pairs: one for move/line, two for quad, three for cubic, none for close.</summary>
    public float[] Points { get; }

    /// <summary>The line's advance width, tracking included.</summary>
    public float Advance { get; }

    /// <summary>The family the text resolved to.</summary>
    public string FamilyName { get; }

    /// <summary>The box around every outline point (control points included); null when there is no ink.</summary>
    public RectangleF? InkBounds { get; }

    /// <summary>
    /// Lays out and outlines a line of text.
    /// </summary>
    /// <param name="fonts">The registry to resolve the family from.</param>
    /// <param name="text">The text; null is treated as empty.</param>
    /// <param name="size">The em size, calibrated units (one unit per point at 72 DPI).</param>
    /// <param name="familyName">The family, or null for the default font.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of the size.</param>
    /// <returns>The outline.</returns>
    public static TextOutline Build(FontRegistry fonts, string text, float size, string familyName, float tracking)
        => Build(fonts, text, size, familyName, tracking, kerning: true);

    /// <summary>
    /// Lays out and outlines a line of text, with kerning on or off.
    /// </summary>
    /// <param name="fonts">The registry to resolve the family from.</param>
    /// <param name="text">The text; null is treated as empty.</param>
    /// <param name="size">The em size, calibrated units (one unit per point at 72 DPI).</param>
    /// <param name="familyName">The family, or null for the default font.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of the size.</param>
    /// <param name="kerning">Whether the font's kerning is applied.</param>
    /// <returns>The outline.</returns>
    public static TextOutline Build(FontRegistry fonts, string text, float size, string familyName, float tracking, bool kerning)
    {
        if (fonts == null) { throw new ArgumentNullException(nameof(fonts)); }
        if (!(size > 0f) || Single.IsInfinity(size)) { throw new ArgumentOutOfRangeException(nameof(size)); }
        if (!Single.IsFinite(tracking)) { throw new ArgumentOutOfRangeException(nameof(tracking)); }

        Font font = fonts.CreateFont(familyName, size, out string resolvedName);
        text ??= String.Empty;
        var builder = new Builder();
        if (text.Length == 0)
        {
            return new TextOutline(Array.Empty<byte>(), Array.Empty<float>(), 0f, resolvedName, null);
        }

        //At 72 DPI one point is one unit, so the size maps 1:1 to calibrated units. The layout
        //  origin is the top of the line box: lift it by the ascent so y = 0 is the baseline
        float ascent = font.FontMetrics.Ascender * size / font.FontMetrics.UnitsPerEm;
        KerningMode mode = TextShaping.ModeOf(kerning);
        var measure = new TextOptions(font) { Dpi = 72f, KerningMode = mode };
        float trackingUnits = tracking * size;

        float advance;
        if (trackingUnits == 0f)
        {
            var options = new TextOptions(font) { Dpi = 72f, KerningMode = mode, Origin = new Vector2(0f, -ascent) };
            TextRenderer.RenderTextTo(builder, text, options);
            advance = MeasureAdvance(text, measure);
        }
        else
        {
            //The line is shaped ONCE as a whole run, so kerning (and ligatures) survive; each
            //  shaped cluster is then outlined where the run puts it, pushed right by the
            //  tracking of every cluster before it
            var clusters = new List<TextShaping.Cluster>();
            TextShaping.Shape(font, text, 0, text.Length, kerning, clusters);
            int count = 0;
            float pen = 0f;
            foreach (TextShaping.Cluster cluster in clusters)
            {
                if (!cluster.IsWhiteSpace)
                {
                    string element = text.Substring(cluster.Start, cluster.Length);
                    TextShaping.Render(builder, font, element, pen + (count * trackingUnits), -ascent, kerning);
                }
                pen += cluster.Advance;
                count++;
            }
            advance = pen + ((count - 1) * trackingUnits);
        }

        return new TextOutline(builder.Verbs.ToArray(), builder.Points.ToArray(), advance, resolvedName, builder.GetInkBounds());
    }

    /// <summary>
    /// Adds stored glyph outlines to a path builder, moved by an offset.
    /// </summary>
    /// <param name="builder">The builder to add to.</param>
    /// <param name="verbs">The outline's verbs.</param>
    /// <param name="p">The outline's points, as x, y pairs.</param>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    internal static void AppendTo(PathGeometryBuilder builder, byte[] verbs, float[] p, float dx, float dy)
    {
        int index = 0;
        foreach (byte verb in verbs)
        {
            switch (verb)
            {
                case VerbMove:
                    builder.MoveTo(new PointF(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case VerbLine:
                    builder.LineTo(new PointF(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case VerbQuad:
                    builder.QuadraticTo(new PointF(p[index] + dx, p[index + 1] + dy), new PointF(p[index + 2] + dx, p[index + 3] + dy));
                    index += 4;
                    break;
                case VerbCubic:
                    builder.CubicTo(new PointF(p[index] + dx, p[index + 1] + dy), new PointF(p[index + 2] + dx, p[index + 3] + dy),
                        new PointF(p[index + 4] + dx, p[index + 5] + dy));
                    index += 6;
                    break;
                default:
                    builder.Close();
                    break;
            }
        }
    }

    //The exact (unrounded) advance of a run: the sum of its glyphs' advance widths. The
    //  font stack's whole-run measurement rounds its box up to whole units, which would
    //  misplace aligned text by up to a unit
    private static float MeasureAdvance(string text, TextOptions options)
    {
        TextMeasurer.TryMeasureCharacterDimensions(text, options, out GlyphBounds[] glyphs);
        float advance = 0f;
        foreach (GlyphBounds glyph in glyphs) { advance += glyph.Bounds.Width; }
        return advance;
    }

    internal sealed class Builder : IGlyphRenderer
    {
        public readonly List<byte> Verbs = new List<byte>();
        public readonly List<float> Points = new List<float>();

        public void BeginText(FontRectangle bounds)
        {
        }

        public bool BeginGlyph(FontRectangle bounds, GlyphRendererParameters parameters) => true;

        public void BeginFigure()
        {
        }

        public void MoveTo(Vector2 point)
        {
            Verbs.Add(VerbMove);
            Add(point);
        }

        public void LineTo(Vector2 point)
        {
            Verbs.Add(VerbLine);
            Add(point);
        }

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
        {
            Verbs.Add(VerbQuad);
            Add(secondControlPoint);
            Add(point);
        }

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
        {
            Verbs.Add(VerbCubic);
            Add(secondControlPoint);
            Add(thirdControlPoint);
            Add(point);
        }

        public void EndFigure() => Verbs.Add(VerbClose);

        public void EndGlyph()
        {
        }

        public void EndText()
        {
        }

        public RectangleF? GetInkBounds()
        {
            if (Points.Count < 2) { return null; }
            float left = Points[0], top = Points[1], right = left, bottom = top;
            for (int i = 2; i < Points.Count; i += 2)
            {
                left = Math.Min(left, Points[i]);
                right = Math.Max(right, Points[i]);
                top = Math.Min(top, Points[i + 1]);
                bottom = Math.Max(bottom, Points[i + 1]);
            }
            return new RectangleF(left, top, right - left, bottom - top);
        }

        private void Add(Vector2 point)
        {
            Points.Add(point.X);
            Points.Add(point.Y);
        }
    }
}
