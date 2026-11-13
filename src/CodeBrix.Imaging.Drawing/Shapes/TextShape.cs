using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Text;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A single line of text in a caller-registered font (see <see cref="FontRegistry"/> -
/// system fonts are never used), drawn as filled glyph outlines. The outlines are laid out
/// once, when the shape is created, by CodeBrix.Imaging's font support in managed code, so
/// both packages draw exactly the same geometry and <see cref="Width"/> is known before the
/// shape is ever drawn. Coordinates and sizes are calibrated drawing units: (<see cref="X"/>,
/// <see cref="Y"/>) is the anchor on the baseline, and <see cref="Size"/> is the em size
/// (the same meaning a font size has in a 2D canvas API). The text paints in its color - or
/// its layer's color - or, when created with a <see cref="ShapeFill"/>, in that fill.
/// <para>
/// The line is kerned when the font carries kerning data (switch it off with the constructors
/// that take <c>kerning</c>). For several lines, wrapping, alignment within a box, outlined text
/// or a per-character reveal, lay the text out with <see cref="TextLayoutEngine"/> and draw it
/// with a <see cref="TextBlockShape"/>.
/// </para>
/// </summary>
public sealed class TextShape : DrawingShape
{
    private readonly TextOutline _outline;

    /// <summary>The text.</summary>
    public string Text { get; }

    /// <summary>The anchor's horizontal position (left edge, center or right edge by <see cref="Align"/>), in calibrated drawing units.</summary>
    public float X { get; }

    /// <summary>The baseline's vertical position, in calibrated drawing units.</summary>
    public float Y { get; }

    /// <summary>The em size, in calibrated drawing units.</summary>
    public float Size { get; }

    /// <summary>How the line sits about <see cref="X"/>.</summary>
    public TextAlign Align { get; }

    /// <summary>The extra space after every character but the last, as a fraction of <see cref="Size"/> (0.1 = a tenth of an em).</summary>
    public float Tracking { get; }

    /// <summary>
    /// Whether the font's kerning is applied (<c>true</c> unless the shape was created with
    /// kerning switched off). Text is kerned whenever the font carries kerning data - tracked
    /// text included, because a tracked line is shaped as one run and the tracking is added
    /// between its glyphs.
    /// </summary>
    public bool Kerning { get; }

    /// <summary>The family name the text resolved to in its registry.</summary>
    public string FamilyName { get; }

    /// <summary>
    /// The line's advance width - from the pen's start to its end, tracking included - in
    /// calibrated drawing units. This is the width alignment uses.
    /// </summary>
    public float Width => _outline.Advance;

    /// <summary>The horizontal position where the line starts, after alignment, in calibrated drawing units.</summary>
    public float Left => Align switch
    {
        TextAlign.Center => X - (Width / 2f),
        TextAlign.Right => X - Width,
        _ => X,
    };

    /// <summary>
    /// Creates a line of text in a flat color.
    /// </summary>
    /// <param name="text">The text; null is treated as empty (which draws nothing).</param>
    /// <param name="x">The anchor's horizontal position, in calibrated drawing units.</param>
    /// <param name="y">The baseline's vertical position, in calibrated drawing units.</param>
    /// <param name="size">The em size, in calibrated drawing units; must be positive.</param>
    /// <param name="fonts">The registry the font comes from.</param>
    /// <param name="color">The text's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="align">How the line sits about <paramref name="x"/>; <see cref="TextAlign.Left"/> by default.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>; 0 by default.</param>
    /// <param name="familyName">The family; or <c>null</c> (the default) for the registry's first font.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive, <paramref name="tracking"/> is not finite, or <paramref name="align"/> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the registry holds no font at all.</exception>
    public TextShape(string text, float x, float y, float size, FontRegistry fonts,
        Color? color = null, TextAlign align = TextAlign.Left, float tracking = 0f, string familyName = null)
        : this(text, x, y, size, fonts, color, null, align, tracking, familyName, true)
    {
    }

    /// <summary>
    /// Creates a line of text in a flat color, with the font's kerning switched on or off.
    /// </summary>
    /// <param name="text">The text; null is treated as empty (which draws nothing).</param>
    /// <param name="x">The anchor's horizontal position, in calibrated drawing units.</param>
    /// <param name="y">The baseline's vertical position, in calibrated drawing units.</param>
    /// <param name="size">The em size, in calibrated drawing units; must be positive.</param>
    /// <param name="fonts">The registry the font comes from.</param>
    /// <param name="color">The text's color; or <c>null</c> to use the owning layer's color.</param>
    /// <param name="align">How the line sits about <paramref name="x"/>.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>.</param>
    /// <param name="familyName">The family; or <c>null</c> for the registry's first font.</param>
    /// <param name="kerning"><c>true</c> to apply the font's kerning (what the other constructors do); <c>false</c> to space the glyphs by their plain advances.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive, <paramref name="tracking"/> is not finite, or <paramref name="align"/> is not Left, Center or Right.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the registry holds no font at all.</exception>
    public TextShape(string text, float x, float y, float size, FontRegistry fonts,
        Color? color, TextAlign align, float tracking, string familyName, bool kerning)
        : this(text, x, y, size, fonts, color, null, align, tracking, familyName, kerning)
    {
    }

    /// <summary>
    /// Creates a line of text painted by a fill, with the font's kerning switched on or off.
    /// </summary>
    /// <param name="text">The text; null is treated as empty (which draws nothing).</param>
    /// <param name="x">The anchor's horizontal position, in calibrated drawing units.</param>
    /// <param name="y">The baseline's vertical position, in calibrated drawing units.</param>
    /// <param name="size">The em size, in calibrated drawing units; must be positive.</param>
    /// <param name="fonts">The registry the font comes from.</param>
    /// <param name="fill">The fill that paints the glyphs.</param>
    /// <param name="align">How the line sits about <paramref name="x"/>.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>.</param>
    /// <param name="familyName">The family; or <c>null</c> for the registry's first font.</param>
    /// <param name="kerning"><c>true</c> to apply the font's kerning; <c>false</c> to space the glyphs by their plain advances.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> or <paramref name="fill"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive, <paramref name="tracking"/> is not finite, or <paramref name="align"/> is not Left, Center or Right.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the registry holds no font at all.</exception>
    public TextShape(string text, float x, float y, float size, FontRegistry fonts, ShapeFill fill,
        TextAlign align, float tracking, string familyName, bool kerning)
        : this(text, x, y, size, fonts, null, fill ?? throw new ArgumentNullException(nameof(fill)), align, tracking, familyName, kerning)
    {
    }

    /// <summary>
    /// Creates a line of text painted by a fill - for example a gradient across the line.
    /// </summary>
    /// <param name="text">The text; null is treated as empty (which draws nothing).</param>
    /// <param name="x">The anchor's horizontal position, in calibrated drawing units.</param>
    /// <param name="y">The baseline's vertical position, in calibrated drawing units.</param>
    /// <param name="size">The em size, in calibrated drawing units; must be positive.</param>
    /// <param name="fonts">The registry the font comes from.</param>
    /// <param name="fill">The fill that paints the glyphs.</param>
    /// <param name="align">How the line sits about <paramref name="x"/>; <see cref="TextAlign.Left"/> by default.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>; 0 by default.</param>
    /// <param name="familyName">The family; or <c>null</c> (the default) for the registry's first font.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> or <paramref name="fill"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive, <paramref name="tracking"/> is not finite, or <paramref name="align"/> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the registry holds no font at all.</exception>
    public TextShape(string text, float x, float y, float size, FontRegistry fonts, ShapeFill fill,
        TextAlign align = TextAlign.Left, float tracking = 0f, string familyName = null)
        : this(text, x, y, size, fonts, null, fill ?? throw new ArgumentNullException(nameof(fill)), align, tracking, familyName, true)
    {
    }

    private TextShape(string text, float x, float y, float size, FontRegistry fonts,
        Color? color, ShapeFill fill, TextAlign align, float tracking, string familyName, bool kerning)
        : base(1f, color, fill)
    {
        if (fonts == null) { throw new ArgumentNullException(nameof(fonts)); }
        if (align != TextAlign.Left && align != TextAlign.Center && align != TextAlign.Right)
        {
            throw new ArgumentOutOfRangeException(nameof(align));
        }

        Text = text ?? String.Empty;
        X = x;
        Y = y;
        Size = size;
        Align = align;
        Tracking = tracking;
        Kerning = kerning;
        _outline = TextOutline.Build(fonts, Text, size, familyName, tracking, kerning);
        FamilyName = _outline.FamilyName;
    }

    /// <summary>
    /// The glyph outlines exactly as drawn (closed contours of lines and curves, placed at the
    /// text's position), with the non-zero fill rule; <see cref="PathGeometry.Empty"/> for text
    /// with no visible glyphs.
    /// </summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline()
    {
        var builder = new PathGeometryBuilder();
        float dx = Left;
        float dy = Y;
        float[] p = _outline.Points;
        int index = 0;
        foreach (byte verb in _outline.Verbs)
        {
            switch (verb)
            {
                case TextOutline.VerbMove:
                    builder.MoveTo(new PointF(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case TextOutline.VerbLine:
                    builder.LineTo(new PointF(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case TextOutline.VerbQuad:
                    builder.QuadraticTo(new PointF(p[index] + dx, p[index + 1] + dy), new PointF(p[index + 2] + dx, p[index + 3] + dy));
                    index += 4;
                    break;
                case TextOutline.VerbCubic:
                    builder.CubicTo(new PointF(p[index] + dx, p[index + 1] + dy), new PointF(p[index + 2] + dx, p[index + 3] + dy),
                        new PointF(p[index + 4] + dx, p[index + 5] + dy));
                    index += 6;
                    break;
                default:
                    builder.Close();
                    break;
            }
        }
        return builder.Build();
    }

    /// <summary>
    /// Reports the box around the glyph outlines (their curve control points included, so it
    /// may be slightly larger than the ink), in calibrated drawing units. Text with no
    /// visible glyphs (empty or only spaces) reports an empty rectangle at its anchor.
    /// </summary>
    /// <returns>The text's bounds in calibrated drawing units.</returns>
    public override RectangleF? GetBounds()
    {
        RectangleF? ink = _outline.InkBounds;
        if (!ink.HasValue) { return new RectangleF(Left, Y, 0f, 0f); }
        RectangleF box = ink.Value;
        return new RectangleF(box.X + Left, box.Y + Y, box.Width, box.Height);
    }

    /// <summary>The area the text covers: its glyph outlines, filled with the non-zero rule.</summary>
    /// <param name="tolerance">Not used (glyphs are filled, not outlined).</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance) => new[] { GetOutline() };

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        if (_outline.Verbs.Length < 1) { return; }

        float dx = Left;
        float dy = Y;
        float[] p = _outline.Points;
        var pathBuilder = new SKPathBuilder();
        int index = 0;
        foreach (byte verb in _outline.Verbs)
        {
            switch (verb)
            {
                case TextOutline.VerbMove:
                    pathBuilder.MoveTo(new SKPoint(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case TextOutline.VerbLine:
                    pathBuilder.LineTo(new SKPoint(p[index] + dx, p[index + 1] + dy));
                    index += 2;
                    break;
                case TextOutline.VerbQuad:
                    pathBuilder.QuadTo(
                        new SKPoint(p[index] + dx, p[index + 1] + dy),
                        new SKPoint(p[index + 2] + dx, p[index + 3] + dy));
                    index += 4;
                    break;
                case TextOutline.VerbCubic:
                    pathBuilder.CubicTo(
                        new SKPoint(p[index] + dx, p[index + 1] + dy),
                        new SKPoint(p[index + 2] + dx, p[index + 3] + dy),
                        new SKPoint(p[index + 4] + dx, p[index + 5] + dy));
                    index += 6;
                    break;
                default:
                    pathBuilder.Close();
                    break;
            }
        }
        using SKPath path = pathBuilder.Detach();

        using SKPaint paint = CreateFillPaint(color, Fill);
        canvas.DrawPath(path, paint);
    }
}
