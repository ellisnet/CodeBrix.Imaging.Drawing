namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// The reasons a loaded SVG document can render as something other than an exact rendition
/// of what it asked for. Every one of them is a graceful degradation - the document still
/// renders - and every one is reported through <see cref="DrawingSvg.Warnings"/>.
/// </summary>
public enum DrawingSvgWarningKind
{
    /// <summary>
    /// A filter primitive the managed filter evaluator does not implement (lighting,
    /// displacement, morphology, convolution, tiling) passed its input through unchanged.
    /// </summary>
    UnsupportedFilterPrimitive,

    /// <summary>
    /// An <c>feTurbulence</c> primitive was dropped: Perlin noise is not generated, so the
    /// primitive contributes nothing.
    /// </summary>
    TurbulenceDropped,

    /// <summary>
    /// A text run arrived as glyph identifiers with no characters behind them, so it could
    /// not be shaped through the managed font stack and was skipped.
    /// </summary>
    GlyphIdTextRunUnsupported,

    /// <summary>
    /// A <c>textPath</c> run was recorded but not drawn: laying glyphs along a path is not
    /// implemented.
    /// </summary>
    TextOnPathUnsupported,

    /// <summary>
    /// The document draws text but no fonts were registered before it was loaded, so every
    /// run outlines to nothing. Register the fonts the document needs through
    /// <see cref="DrawingSvg.Fonts"/> BEFORE calling a load method.
    /// </summary>
    NoFontsRegistered,
}
