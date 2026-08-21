using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model.Services;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.SvgParse;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// The facade of the fully managed SVG renderer - the NoSkia counterpart of
/// CodeBrix.SkiaSvg's <c>SKSvg</c>. Load an SVG (from a stream, file, or markup string),
/// then rasterize it to a <see cref="DrawingBitmap"/> or encoded PNG bytes at any scale,
/// or replay it onto any <see cref="DrawingCanvas"/>. Fonts used by SVG text must be
/// registered explicitly through <see cref="Fonts"/> BEFORE loading (text is measured at
/// load time); system fonts are never consulted, keeping output identical on every
/// machine.
/// </summary>
public sealed class DrawingSvg : IDisposable
{
    private readonly NoSkiaFontRegistry _fonts = new NoSkiaFontRegistry();
    private readonly ImagingSvgAssetLoader _assetLoader;
    private readonly List<string> _warnings = new List<string>();

    /// <summary>
    /// Creates an empty renderer; call a <c>Load</c> method (or <see cref="FromSvg"/>)
    /// to compile an SVG document.
    /// </summary>
    public DrawingSvg()
    {
        _assetLoader = new ImagingSvgAssetLoader(_fonts);
    }

    /// <summary>
    /// The font registry consulted for every SVG text element. Register the font files
    /// the document needs before loading it.
    /// </summary>
    public NoSkiaFontRegistry Fonts => _fonts;

    /// <summary>
    /// The compiled display list of the loaded document; <c>null</c> before a successful load.
    /// </summary>
    public SKPicture Picture { get; private set; }

    /// <summary>
    /// The loaded document's bounds, in SVG user units (CSS pixels) - an empty rectangle
    /// before a successful load. The origin can be non-zero.
    /// </summary>
    public DrawingRect Bounds
    {
        get
        {
            if (Picture == null) { return DrawingRect.Empty; }
            SKRect cullRect = Picture.CullRect;
            return new DrawingRect(cullRect.Left, cullRect.Top, cullRect.Right, cullRect.Bottom);
        }
    }

    /// <summary>
    /// Warnings collected while rendering - one entry per SVG feature (exotic filter
    /// primitive, glyph-id text run, text-on-path) that degraded gracefully instead of
    /// rendering exactly.
    /// </summary>
    public IReadOnlyList<string> Warnings
    {
        get
        {
            lock (_warnings) { return _warnings.ToArray(); }
        }
    }

    /// <summary>
    /// Loads and compiles an SVG document from a stream.
    /// </summary>
    /// <param name="stream">The stream holding the SVG markup.</param>
    /// <returns>The compiled display list; or <c>null</c> when the document cannot be compiled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    public SKPicture Load(Stream stream)
    {
        if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
        return Compile(SvgService.Open(stream));
    }

    /// <summary>
    /// Loads and compiles an SVG document from a file path (plain or gzip-compressed SVG).
    /// </summary>
    /// <param name="path">The path of the SVG file.</param>
    /// <returns>The compiled display list; or <c>null</c> when the document cannot be compiled.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
    public SKPicture Load(string path)
    {
        if (String.IsNullOrWhiteSpace(path)) { throw new ArgumentException("A file path is required.", nameof(path)); }
        return Compile(SvgService.Open(path));
    }

    /// <summary>
    /// Loads and compiles an SVG document from a markup string.
    /// </summary>
    /// <param name="svgMarkup">The SVG markup.</param>
    /// <returns>The compiled display list; or <c>null</c> when the document cannot be compiled.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="svgMarkup"/> is null or whitespace.</exception>
    public SKPicture FromSvg(string svgMarkup)
    {
        if (String.IsNullOrWhiteSpace(svgMarkup))
        {
            throw new ArgumentException("SVG markup is required.", nameof(svgMarkup));
        }
        return Compile(SvgService.FromSvg(svgMarkup));
    }

    /// <summary>
    /// Replays the loaded document onto a canvas, honoring whatever transform the canvas
    /// already carries.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="deviceScale">
    /// The approximate device pixels per SVG user unit the canvas transform produces -
    /// used to evaluate filter effects (blur radii, offsets) at the right resolution.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no document has been loaded.</exception>
    public void Render(DrawingCanvas canvas, float deviceScale = 1f)
    {
        if (canvas == null) { throw new ArgumentNullException(nameof(canvas)); }
        if (Picture == null) { throw new InvalidOperationException("Load an SVG document before rendering."); }

        NoSkiaModel model = CreateReplayModel(deviceScale);
        model.Draw(Picture, canvas);
    }

    /// <summary>
    /// Rasterizes the loaded document to a new bitmap at the given scale (device pixels
    /// per SVG user unit).
    /// </summary>
    /// <param name="scale">The raster scale; must be positive.</param>
    /// <param name="backgroundColor">
    /// The color the bitmap is cleared to before rendering; transparent when omitted.
    /// </param>
    /// <returns>The rasterized bitmap, which the caller must dispose.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no document has been loaded or it has no drawable area.</exception>
    public DrawingBitmap RasterizeToBitmap(float scale = 1f, DrawingColor? backgroundColor = null)
    {
        if (scale <= 0) { throw new ArgumentOutOfRangeException(nameof(scale)); }
        if (Picture == null) { throw new InvalidOperationException("Load an SVG document before rasterizing."); }

        SKRect bounds = Picture.CullRect;
        int width = Math.Max(1, (int)MathF.Ceiling(bounds.Width * scale));
        int height = Math.Max(1, (int)MathF.Ceiling(bounds.Height * scale));

        var bitmap = new DrawingBitmap(new DrawingImageInfo(
            width, height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));
        try
        {
            using var canvas = new DrawingCanvas(bitmap);
            if (backgroundColor.HasValue)
            {
                canvas.Clear(backgroundColor.Value);
            }
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);

            NoSkiaModel model = CreateReplayModel(scale);
            model.Draw(Picture, canvas);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Rasterizes the loaded document to PNG bytes at the given scale (device pixels per
    /// SVG user unit).
    /// </summary>
    /// <param name="scale">The raster scale; must be positive.</param>
    /// <param name="backgroundColor">
    /// The color rendered behind the document; transparent when omitted.
    /// </param>
    /// <returns>The PNG-encoded bytes.</returns>
    public byte[] RasterizeToPng(float scale = 1f, DrawingColor? backgroundColor = null)
    {
        using DrawingBitmap bitmap = RasterizeToBitmap(scale, backgroundColor);
        using DrawingImage image = DrawingImage.FromBitmap(bitmap);
        using DrawingData data = image.Encode(DrawingEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Releases the compiled document.
    /// </summary>
    public void Dispose()
    {
        Picture = null;
    }

    private SKPicture Compile(SvgDocument document)
    {
        Picture = document != null
            ? SvgSceneRuntime.CreateModel(document, _assetLoader)
            : null;
        return Picture;
    }

    private NoSkiaModel CreateReplayModel(float deviceScale)
    {
        var textRenderer = new NoSkiaTextRenderer(_assetLoader) { Unsupported = AddWarning };
        var filterFactory = new NoSkiaImageFilterFactory
        {
            DeviceScale = deviceScale,
            UnsupportedPrimitive = AddWarning,
        };
        var model = new NoSkiaModel(textRenderer, filterFactory);
        textRenderer.Model = model;
        filterFactory.Model = model;
        return model;
    }

    private void AddWarning(string warning)
    {
        if (String.IsNullOrEmpty(warning)) { return; }
        lock (_warnings)
        {
            if (!_warnings.Contains(warning)) { _warnings.Add(warning); }
        }
    }
}
