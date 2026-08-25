using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

// <summary>
// Renders the text commands of a replayed SVG display list onto a
// <see cref="DrawingCanvas"/>. The display-list replayer (<see cref="NoSkiaModel"/>)
// carries no font stack of its own; an implementation of this interface supplies one.
// When no implementation is provided, text commands are skipped.
// </summary>
internal interface INoSkiaTextRenderer
{
    // <summary>
    // Draws a run of text at the given baseline origin.
    // </summary>
    // <param name="text">The text to draw.</param>
    // <param name="x">The horizontal position of the text origin.</param>
    // <param name="y">The vertical position of the text origin (the baseline).</param>
    // <param name="paint">The shim paint carrying the fill/stroke and font properties.</param>
    // <param name="canvas">The canvas to draw onto.</param>
    void DrawText(string text, float x, float y, SKPaint paint, DrawingCanvas canvas);

    // <summary>
    // Draws a text blob (per-glyph positioned text) offset by the given origin.
    // </summary>
    // <param name="textBlob">The shim text blob holding the text or glyphs and their positions.</param>
    // <param name="x">The horizontal offset added to every position.</param>
    // <param name="y">The vertical offset added to every position.</param>
    // <param name="paint">The shim paint carrying the fill/stroke and font properties.</param>
    // <param name="canvas">The canvas to draw onto.</param>
    void DrawTextBlob(SKTextBlob textBlob, float x, float y, SKPaint paint, DrawingCanvas canvas);

    // <summary>
    // Draws a run of text along a path.
    // </summary>
    // <param name="text">The text to draw.</param>
    // <param name="path">The shim path the text follows.</param>
    // <param name="hOffset">The distance along the path at which the text starts.</param>
    // <param name="vOffset">The offset perpendicular to the path.</param>
    // <param name="paint">The shim paint carrying the fill/stroke and font properties.</param>
    // <param name="canvas">The canvas to draw onto.</param>
    void DrawTextOnPath(string text, SKPath path, float hOffset, float vOffset, SKPaint paint, DrawingCanvas canvas);
}
