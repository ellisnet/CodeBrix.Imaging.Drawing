using System;
using CodeBrix.Imaging.Drawing.Fills;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// One styled run of text to lay out with <see cref="TextLayoutEngine"/>. A block is a
/// sequence of runs concatenated in order, so the text indices a <see cref="TextLayoutResult"/>
/// reports address that concatenation, not any single run. Line breaks come from the text itself
/// (a run is not a line) and from wrapping. Each run is shaped on its own - kerning applies within
/// a run, not across the boundary between two runs.
/// </summary>
public sealed class TextRunDescriptor
{
    /// <summary>
    /// Creates a run.
    /// </summary>
    /// <param name="text">The run's text; may be empty, not null.</param>
    /// <param name="size">The em size, in calibrated drawing units; must be positive and finite.</param>
    /// <param name="familyName">The registered family to draw it in; or <c>null</c> for the registry's first font.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive and finite.</exception>
    public TextRunDescriptor(string text, float size = 12f, string familyName = null)
    {
        if (text == null) { throw new ArgumentNullException(nameof(text)); }
        if (!(size > 0f) || !Single.IsFinite(size)) { throw new ArgumentOutOfRangeException(nameof(size)); }
        Text = text;
        FamilyName = familyName;
        Size = size;
    }

    /// <summary>The run's text.</summary>
    public string Text { get; }

    /// <summary>The family the run asks for; <c>null</c> for the registry's first font.</summary>
    public string FamilyName { get; }

    /// <summary>The em size, in calibrated drawing units.</summary>
    public float Size { get; }

    /// <summary>
    /// The color this run's glyphs are filled with when the block is drawn, instead of the
    /// shape's own fill; or <c>null</c> (the default) for the shape's. Color affects drawing only,
    /// never layout.
    /// </summary>
    public Color? Color { get; init; }

    /// <summary>
    /// A fill (for example a gradient) for this run's glyphs instead of the shape's own; or
    /// <c>null</c> (the default). It wins over <see cref="Color"/>.
    /// </summary>
    public ShapeFill Fill { get; init; }

    /// <summary>
    /// Extra space after every character of this run except the last one on a line, as a
    /// fraction of <see cref="Size"/> (0.1 = a tenth of an em); 0 by default. It is added between
    /// the shaped glyphs, so the run stays kerned.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or infinity.</exception>
    public float Tracking
    {
        get => _tracking;
        init
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _tracking = value;
        }
    }

    private readonly float _tracking;
}
