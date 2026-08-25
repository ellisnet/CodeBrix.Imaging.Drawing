using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Draws a run of text whose code points each carry their own position - the form SVG
/// produces for per-character placement (<c>x</c>/<c>y</c>/<c>dx</c>/<c>dy</c> lists), and
/// the form a consumer that wants glyph-level placement can ask for. Each code point is
/// drawn on its own, left-aligned at its position offset by
/// <paramref name="X"/>/<paramref name="Y"/>, so
/// <see cref="DrawingTextStyle.Align"/> has already been applied by the producer.
/// </summary>
/// <param name="Text">The run's text.</param>
/// <param name="Positions">
/// One position per code point of <paramref name="Text"/>, relative to
/// <paramref name="X"/>/<paramref name="Y"/>. A run with fewer positions than code points
/// draws only the code points that have one.
/// </param>
/// <param name="X">The horizontal offset added to every position.</param>
/// <param name="Y">The vertical offset added to every position.</param>
/// <param name="Paint">The paint each outline is filled and/or stroked with.</param>
/// <param name="Style">The typeface selection the run was recorded with.</param>
/// <param name="Outliner">
/// The outliner the producer supplied, which turns each code point into a path; or
/// <c>null</c> when no outline can be produced.
/// </param>
public sealed record DrawPositionedTextCommand(
    string Text,
    DrawingPoint[] Positions,
    float X,
    float Y,
    DrawingPaint Paint,
    DrawingTextStyle Style,
    IDrawingTextOutliner Outliner) : DrawingCommand
{
    /// <summary>
    /// Builds one outline per positioned code point, in the coordinate space the command
    /// was recorded in. Drawing these separately - rather than the single merged path from
    /// <see cref="GetOutline"/> - is what replay does, because overlapping anti-aliased
    /// glyphs composite differently when they are merged into one fill.
    /// </summary>
    /// <returns>The outlines, in run order; empty when nothing could be outlined.</returns>
    public IReadOnlyList<DrawingPath> GetOutlines()
    {
        var outlines = new List<DrawingPath>();
        if (Outliner == null || String.IsNullOrEmpty(Text) || Positions == null) { return outlines; }

        var positionIndex = 0;
        for (int i = 0; i < Text.Length && positionIndex < Positions.Length; positionIndex++)
        {
            int length = Char.IsHighSurrogate(Text[i]) && i + 1 < Text.Length ? 2 : 1;
            string element = Text.Substring(i, length);
            i += length;

            DrawingPoint position = Positions[positionIndex];
            DrawingPath outline = Outliner.GetOutline(
                element, Style, new DrawingPoint(position.X + X, position.Y + Y));
            if (outline != null && !outline.IsEmpty) { outlines.Add(outline); }
        }
        return outlines;
    }

    /// <summary>
    /// Builds a single outline holding every positioned code point - convenient for bounds
    /// and for consumers that only need one path.
    /// </summary>
    /// <returns>The merged outline; or <c>null</c> when nothing could be outlined.</returns>
    public DrawingPath GetOutline()
    {
        IReadOnlyList<DrawingPath> outlines = GetOutlines();
        if (outlines.Count == 0) { return null; }

        var builder = new DrawingPathBuilder();
        builder.SetFillType(outlines[0].FillType);
        foreach (DrawingPath outline in outlines)
        {
            builder.AddPath(outline);
        }
        return builder.Detach();
    }

    internal override void Dispatch(IDrawingCommandVisitor visitor) => visitor.Visit(this);
}
