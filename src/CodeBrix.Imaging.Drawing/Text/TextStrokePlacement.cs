namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// Where an outlined text block's stroke is drawn relative to its glyph outlines
/// (<see cref="CodeBrix.Imaging.Drawing.Shapes.TextBlockShape.StrokePlacement"/>).
/// </summary>
public enum TextStrokePlacement
{
    /// <summary>
    /// Centred on the outline and drawn over the fill (the default), like any stroked path. Many fonts
    /// build glyphs from overlapping contours (Roboto does); a centred stroke traces those overlaps
    /// inside the glyphs too.
    /// </summary>
    Center = 0,

    /// <summary>
    /// Only the half of the stroke that lies OUTSIDE the glyphs is drawn (the inside half is cut away
    /// by the glyphs' own shape), and the fill is drawn over it: a clean outline of the visible
    /// width StrokeStyle.Width / 2, with no lines where contours overlap inside a glyph.
    /// </summary>
    Outside = 1,
}
