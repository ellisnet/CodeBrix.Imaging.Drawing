namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// How a loaded document's text is recorded into its <see cref="DrawingSvg.Picture"/>.
/// Set this BEFORE loading a document: the picture is converted once, at load time.
/// </summary>
public enum DrawingSvgTextEmission
{
    /// <summary>
    /// Text is recorded as whole runs - one <c>DrawTextCommand</c> per run, carrying its
    /// characters and one origin. This is the default, and the form a consumer that emits
    /// real text (a PDF writer, a search index) wants.
    /// </summary>
    Runs = 0,

    /// <summary>
    /// Every run is expanded into a <c>DrawPositionedTextCommand</c> whose code points each
    /// carry their own position, measured through the registered fonts. This is the form a
    /// consumer that places glyphs itself wants; positions are prefix-measured, so kerning
    /// between the characters of the run survives.
    /// </summary>
    PositionedGlyphs = 1,
}
