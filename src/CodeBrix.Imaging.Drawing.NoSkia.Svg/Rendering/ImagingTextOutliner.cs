using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

// <summary>
// Turns the text runs recorded in a display list into filled outlines, using the fonts an
// <see cref="ImagingSvgAssetLoader"/> resolves from its <see cref="NoSkiaFontRegistry"/>.
// This is the SVG assembly's implementation of the core assembly's only text seam,
// <see cref="IDrawingTextOutliner"/>: a text command carries the outliner that recorded
// it, so the command can still be outlined for as long as the picture is alive - after
// the <see cref="DrawingSvg"/> that produced it has been disposed included, because the
// outliner holds the font registry itself.
// </summary>
internal sealed class ImagingTextOutliner : IDrawingTextOutliner
{
    private readonly object _syncRoot = new object();
    private readonly ImagingSvgAssetLoader _assetLoader;
    private readonly NoSkiaModel _model;

    //A style is a value, and the paint built from one is read-only to the asset loader, so
    //  one paint per distinct style serves every glyph of every run that shares it
    private readonly Dictionary<DrawingTextStyle, SKPaint> _paints =
        new Dictionary<DrawingTextStyle, SKPaint>();

    // <summary>
    // Creates an outliner over the given asset loader and display-list replayer.
    // </summary>
    // <param name="assetLoader">The asset loader whose font registry supplies every typeface.</param>
    // <param name="model">The replayer used to convert shim glyph paths to drawing paths.</param>
    // <exception cref="ArgumentNullException">
    // Thrown when <paramref name="assetLoader"/> or <paramref name="model"/> is null.
    // </exception>
    public ImagingTextOutliner(ImagingSvgAssetLoader assetLoader, NoSkiaModel model)
    {
        _assetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    // <inheritdoc />
    public DrawingPath GetOutline(string text, DrawingTextStyle style, DrawingPoint origin)
    {
        if (String.IsNullOrEmpty(text) || style == null) { return null; }

        SKPath shimPath = _assetLoader.GetTextPath(text, GetShimPaint(style), origin.X, origin.Y);
        if (shimPath == null) { return null; }

        DrawingPath path = _model.ToDrawingPath(shimPath);
        return path == null || path.IsEmpty ? null : path;
    }

    // <summary>
    // Measures the advance width of a run laid out in the style's typeface - what the
    // display-list producer needs to place code points individually.
    // </summary>
    // <param name="text">The text to measure; empty text measures zero.</param>
    // <param name="style">The typeface selection to measure with.</param>
    // <returns>The advance width, in the coordinate space the style's size is expressed in.</returns>
    public float MeasureAdvance(string text, DrawingTextStyle style)
    {
        if (String.IsNullOrEmpty(text) || style == null) { return 0f; }

        SKRect bounds = SKRect.Empty;
        return _assetLoader.MeasureText(text, GetShimPaint(style), ref bounds);
    }

    private SKPaint GetShimPaint(DrawingTextStyle style)
    {
        lock (_syncRoot)
        {
            if (!_paints.TryGetValue(style, out SKPaint paint))
            {
                paint = ToShimPaint(style);
                _paints[style] = paint;
            }
            return paint;
        }
    }

    // <summary>
    // Rebuilds the shim paint the asset loader resolves fonts from. A style with no family
    // name is one the compiler could not resolve to a registered font, so it is rebuilt
    // with no typeface at all - which is exactly what the loader saw when it recorded the
    // run, and what makes it fall back to the first registered font the same way.
    // </summary>
    // <param name="style">The recorded style.</param>
    // <returns>The equivalent shim paint.</returns>
    private static SKPaint ToShimPaint(DrawingTextStyle style)
    {
        var paint = new SKPaint
        {
            TextSize = style.Size,
            TextAlign = style.Align switch
            {
                DrawingTextAlign.Center => SKTextAlign.Center,
                DrawingTextAlign.Right => SKTextAlign.Right,
                _ => SKTextAlign.Left,
            },
        };

        if (style.FamilyName != null)
        {
            paint.Typeface = SKTypeface.FromFamilyName(
                style.FamilyName,
                (SKFontStyleWeight)(int)style.Weight,
                (SKFontStyleWidth)(int)style.Width,
                (SKFontStyleSlant)(int)style.Slant);
        }

        return paint;
    }
}
