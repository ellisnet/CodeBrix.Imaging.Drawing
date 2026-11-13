using System;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// The settings that control how <see cref="TextLayoutEngine"/> lays a block of text out:
/// the width to wrap within, the box height, line limits, alignment, line spacing, trimming
/// and kerning. Every property has a default, so <c>new TextLayoutOptions()</c> lays the text
/// out unwrapped, left-aligned and kerned. The engine copies the values it uses, so an options
/// object can be changed and reused for the next layout.
/// </summary>
public sealed class TextLayoutOptions
{
    /// <summary>
    /// The width to wrap within, in calibrated drawing units; or <c>null</c> (the default) for no
    /// wrapping - lines then break only where the text itself does. A width also gives
    /// <see cref="Align"/> a box to align within (without one, lines align to the widest).
    /// </summary>
    public float? MaxWidth { get; set; }

    /// <summary>
    /// The box height, in calibrated drawing units; or <c>null</c> (the default) for "as tall as
    /// the text". Lines that would end below it are dropped (see <see cref="Trimming"/>; the first
    /// line is always kept), and <see cref="VerticalAlign"/> places the kept lines within it.
    /// </summary>
    public float? MaxHeight { get; set; }

    /// <summary>The most lines to keep; 0 (the default) for no limit.</summary>
    public int MaxLines { get; set; }

    /// <summary>How each line sits within the block's width; <see cref="TextAlign.Left"/> by default.</summary>
    public TextAlign Align { get; set; } = TextAlign.Left;

    /// <summary>Where the lines sit within <see cref="MaxHeight"/>; <see cref="TextVerticalAlign.Top"/> by default.</summary>
    public TextVerticalAlign VerticalAlign { get; set; } = TextVerticalAlign.Top;

    /// <summary>
    /// A multiplier on each line's natural height (the font's ascent + descent + line gap); 1 by
    /// default. Ignored when <see cref="LineHeight"/> is set.
    /// </summary>
    public float LineSpacing { get; set; } = 1f;

    /// <summary>
    /// An explicit line height in calibrated drawing units for every line; or 0 (the default) to
    /// take it from the fonts (times <see cref="LineSpacing"/>).
    /// </summary>
    public float LineHeight { get; set; }

    /// <summary>What happens to text that does not fit; <see cref="TextTrimming.None"/> by default.</summary>
    public TextTrimming Trimming { get; set; } = TextTrimming.None;

    /// <summary>
    /// The text a trimmed line ends in; "\u2026" (the horizontal ellipsis) by default. Use "..."
    /// for a font without that character.
    /// </summary>
    public string Ellipsis { get; set; } = "\u2026";

    /// <summary>
    /// Whether the font's kerning (its GPOS kern feature or legacy kern table) is applied;
    /// <c>true</c> by default. A font without kerning data lays out the same either way.
    /// </summary>
    public bool Kerning { get; set; } = true;

    /// <summary>Creates a copy of these options.</summary>
    /// <returns>A new, independent options object with the same values.</returns>
    public TextLayoutOptions Clone() => (TextLayoutOptions)MemberwiseClone();

    //Rejects values no layout can honour, naming the property at fault
    internal void Validate()
    {
        if (MaxWidth.HasValue && (!(MaxWidth.Value > 0f) || !Single.IsFinite(MaxWidth.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(MaxWidth), "MaxWidth must be positive and finite (or null).");
        }
        if (MaxHeight.HasValue && (!(MaxHeight.Value > 0f) || !Single.IsFinite(MaxHeight.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(MaxHeight), "MaxHeight must be positive and finite (or null).");
        }
        if (MaxLines < 0) { throw new ArgumentOutOfRangeException(nameof(MaxLines)); }
        if (!Enum.IsDefined(Align)) { throw new ArgumentOutOfRangeException(nameof(Align)); }
        if (!Enum.IsDefined(VerticalAlign)) { throw new ArgumentOutOfRangeException(nameof(VerticalAlign)); }
        if (!Enum.IsDefined(Trimming)) { throw new ArgumentOutOfRangeException(nameof(Trimming)); }
        if (!(LineSpacing > 0f) || !Single.IsFinite(LineSpacing)) { throw new ArgumentOutOfRangeException(nameof(LineSpacing)); }
        if (!(LineHeight >= 0f) || !Single.IsFinite(LineHeight)) { throw new ArgumentOutOfRangeException(nameof(LineHeight)); }
    }
}
