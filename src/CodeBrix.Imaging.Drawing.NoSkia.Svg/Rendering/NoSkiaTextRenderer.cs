using System;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

// <summary>
// Renders replayed SVG text commands by converting text to glyph-outline paths through
// the <see cref="ImagingSvgAssetLoader"/> font stack (CodeBrix.Imaging.Fonts) and filling
// them on the managed canvas - no font rasterization outside managed code.
// </summary>
internal sealed class NoSkiaTextRenderer : INoSkiaTextRenderer
{
    private readonly ImagingSvgAssetLoader _assetLoader;

    // <summary>
    // Creates a text renderer over the given asset loader (whose font registry supplies
    // every typeface used).
    // </summary>
    // <param name="assetLoader">The asset loader that provides text outlines.</param>
    // <exception cref="ArgumentNullException">Thrown when <paramref name="assetLoader"/> is null.</exception>
    public NoSkiaTextRenderer(ImagingSvgAssetLoader assetLoader)
    {
        _assetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
    }

    // <summary>
    // The display-list replayer used to convert shim paints and paths. Assign after
    // constructing the <see cref="NoSkiaModel"/> (the two reference each other).
    // </summary>
    public NoSkiaModel Model { get; set; }

    // <summary>
    // An optional callback invoked with a short description of each text feature that
    // could not be rendered (glyph-id runs, text-on-path).
    // </summary>
    public Action<string> Unsupported { get; set; }

    // <inheritdoc />
    public void DrawText(string text, float x, float y, SKPaint paint, DrawingCanvas canvas)
    {
        if (String.IsNullOrEmpty(text) || paint == null || Model == null) { return; }

        SKPath shimPath = _assetLoader.GetTextPath(text, paint, x, y);
        FillTextPath(shimPath, paint, canvas);
    }

    // <inheritdoc />
    public void DrawTextBlob(SKTextBlob textBlob, float x, float y, SKPaint paint, DrawingCanvas canvas)
    {
        if (textBlob == null || paint == null || Model == null) { return; }

        if (textBlob.Text == null || textBlob.Points == null)
        {
            //A glyph-id-only blob carries no text to shape through the managed font stack
            Unsupported?.Invoke("positioned glyph-id text run");
            return;
        }

        //Positions are per code point; the compiler has already applied anchoring, so each
        //code point draws left-aligned at its own position
        SKPaint perGlyphPaint = paint.Clone();
        perGlyphPaint.TextAlign = SKTextAlign.Left;

        string text = textBlob.Text;
        var pointIndex = 0;
        for (int i = 0; i < text.Length && pointIndex < textBlob.Points.Length; pointIndex++)
        {
            int length = Char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
            string element = text.Substring(i, length);
            i += length;

            SKPoint position = textBlob.Points[pointIndex];
            SKPath shimPath = _assetLoader.GetTextPath(element, perGlyphPaint, position.X + x, position.Y + y);
            FillTextPath(shimPath, paint, canvas);
        }
    }

    // <inheritdoc />
    public void DrawTextOnPath(string text, SKPath path, float hOffset, float vOffset, SKPaint paint,
        DrawingCanvas canvas)
    {
        Unsupported?.Invoke("text-on-path");
    }

    private void FillTextPath(SKPath shimPath, SKPaint paint, DrawingCanvas canvas)
    {
        if (shimPath == null) { return; }

        using DrawingPath path = Model.ToDrawingPath(shimPath);
        if (path == null || path.IsEmpty) { return; }

        using DrawingPaint drawingPaint = Model.ToDrawingPaint(paint);
        canvas.DrawPath(path, drawingPaint);
    }
}
