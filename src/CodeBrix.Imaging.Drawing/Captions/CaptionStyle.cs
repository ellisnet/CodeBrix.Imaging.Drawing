using System;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.Captions;

/// <summary>
/// How burned-in captions look and where they go. The defaults are a readable broadcast-style
/// caption: white text about 4.5% of the frame's short side, centred, wrapped to 86% of the safe
/// area's width, on a rounded translucent black box, sitting on the bottom edge of the safe area.
/// Sizes that scale with the text (padding, corner radius, outline width, cue spacing) are
/// FRACTIONS OF THE TEXT SIZE. Mutable: a <see cref="CaptionPresenter"/> copies the style it is
/// given, so change a style before creating the presenter.
/// </summary>
public sealed class CaptionStyle
{
    /// <summary>The registered family to draw in; <c>null</c> (the default) for the registry's first font.</summary>
    public string FamilyName { get; set; }

    /// <summary>The text's em size in calibrated units; 0 (the default) for 4.5% of the layout's short side.</summary>
    public float Size { get; set; }

    /// <summary>The text's color; white by default.</summary>
    public Color TextColor { get; set; } = Color.White;

    /// <summary>A fill (for example a gradient) for the text instead of <see cref="TextColor"/>; <c>null</c> by default.</summary>
    public ShapeFill TextFill { get; set; }

    /// <summary>A color to outline the glyphs in; <c>null</c> (the default) for no outline.</summary>
    public Color? OutlineColor { get; set; }

    /// <summary>The glyph outline's width as a fraction of the text size; 0.08 by default (used when <see cref="OutlineColor"/> is set).</summary>
    public float OutlineWidth { get; set; } = 0.08f;

    /// <summary>The box behind each cue; translucent black (alpha 176) by default; <c>null</c> for no box.</summary>
    public Color? BackgroundColor { get; set; } = Color.FromRgba(0, 0, 0, 176);

    /// <summary>The space between the text and the box's left and right edges, as a fraction of the text size; 0.45 by default.</summary>
    public float PaddingX { get; set; } = 0.45f;

    /// <summary>The space between the text and the box's top and bottom edges, as a fraction of the text size; 0.22 by default.</summary>
    public float PaddingY { get; set; } = 0.22f;

    /// <summary>The box's corner radius as a fraction of the text size; 0.25 by default.</summary>
    public float CornerRadius { get; set; } = 0.25f;

    /// <summary>The widest a cue's box may be, as a fraction of its reference rectangle's width (the safe area by default); 0.86 by default.</summary>
    public float MaxWidthFraction { get; set; } = 0.86f;

    /// <summary>How each cue's lines align; centred by default (a cue's own alignment wins).</summary>
    public TextAlign Align { get; set; } = TextAlign.Center;

    /// <summary>The line spacing multiplier; 1.1 by default.</summary>
    public float LineSpacing { get; set; } = 1.1f;

    /// <summary>The most lines a cue shows (the rest is cut, ending in an ellipsis); 0 (the default) for no limit.</summary>
    public int MaxLines { get; set; }

    /// <summary>
    /// Where the cues' box goes - its own anchor fractions land on the position (see
    /// <see cref="DrawingLayout.PlaceBox"/>); the bottom centre of the safe area by default. A cue
    /// with its own position goes there instead.
    /// </summary>
    public LayoutPosition Position { get; set; } = new LayoutPosition(LayoutAnchor.BottomCenter, LayoutReference.SafeArea);

    /// <summary>The gap between the boxes of cues that show at the same time, as a fraction of the text size; 0.3 by default.</summary>
    public float CueSpacing { get; set; } = 0.3f;

    /// <summary>Creates an independent copy of the style.</summary>
    /// <returns>The copy.</returns>
    public CaptionStyle Clone() => (CaptionStyle)MemberwiseClone();

    //Rejects values no caption can be drawn with, naming the property at fault
    internal void Validate()
    {
        if (!(Size >= 0f) || !Single.IsFinite(Size)) { throw new ArgumentOutOfRangeException(nameof(Size)); }
        if (!(OutlineWidth >= 0f) || !Single.IsFinite(OutlineWidth)) { throw new ArgumentOutOfRangeException(nameof(OutlineWidth)); }
        if (!(PaddingX >= 0f) || !Single.IsFinite(PaddingX)) { throw new ArgumentOutOfRangeException(nameof(PaddingX)); }
        if (!(PaddingY >= 0f) || !Single.IsFinite(PaddingY)) { throw new ArgumentOutOfRangeException(nameof(PaddingY)); }
        if (!(CornerRadius >= 0f) || !Single.IsFinite(CornerRadius)) { throw new ArgumentOutOfRangeException(nameof(CornerRadius)); }
        if (!(MaxWidthFraction > 0f) || !Single.IsFinite(MaxWidthFraction)) { throw new ArgumentOutOfRangeException(nameof(MaxWidthFraction)); }
        if (!Enum.IsDefined(Align)) { throw new ArgumentOutOfRangeException(nameof(Align)); }
        if (!(LineSpacing > 0f) || !Single.IsFinite(LineSpacing)) { throw new ArgumentOutOfRangeException(nameof(LineSpacing)); }
        if (MaxLines < 0) { throw new ArgumentOutOfRangeException(nameof(MaxLines)); }
        if (!(CueSpacing >= 0f) || !Single.IsFinite(CueSpacing)) { throw new ArgumentOutOfRangeException(nameof(CueSpacing)); }
    }
}
