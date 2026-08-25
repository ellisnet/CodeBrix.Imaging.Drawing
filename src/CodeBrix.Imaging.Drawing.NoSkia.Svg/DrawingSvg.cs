using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model.Services;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.SvgParse;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// The facade of the fully managed SVG renderer - load an SVG (from a stream, file, or
/// markup string) and get back three things: a <see cref="Picture"/> to draw or walk, a
/// <see cref="Scene"/> that says what each part of it means, and the
/// <see cref="Warnings"/> raised by anything the document asked for that could not be done
/// exactly. Rasterize it with <see cref="RasterizeToBitmap"/> or
/// <see cref="RasterizeToPng"/>, or replay it onto any <see cref="DrawingCanvas"/> with
/// <see cref="Render"/>.
/// <para>
/// Fonts used by SVG text must be registered through <see cref="Fonts"/> BEFORE loading -
/// text is measured and outlined at load time. System fonts are never consulted, so the
/// same document plus the same font files produce the same output on every machine.
/// </para>
/// <para>
/// The document's coordinate space is CSS pixels at 96 DPI, the space
/// <see cref="Bounds"/> and every node's <see cref="DrawingSvgNode.DocumentBounds"/> are
/// expressed in. <see cref="DeclaredWidth"/> and <see cref="DeclaredHeight"/> report what
/// the document itself asked for, in whatever unit it used.
/// </para>
/// <para>
/// A text command in the picture names the font family it was RESOLVED to - the face its
/// outlines were measured from, and the name
/// <see cref="NoSkiaFontRegistry.TryGetFontData"/> hands that face back for. The family the
/// document asked for is not carried: the compiler replaces it with the one it matched
/// before the command is recorded.
/// </para>
/// </summary>
public sealed class DrawingSvg : IDisposable
{
    private readonly NoSkiaFontRegistry _fonts = new NoSkiaFontRegistry();
    private readonly ImagingSvgAssetLoader _assetLoader;
    private readonly List<DrawingSvgWarning> _warnings = new List<DrawingSvgWarning>();

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
    /// How text is recorded into <see cref="Picture"/>. Set this BEFORE loading: the
    /// picture is built once, when the document is loaded.
    /// </summary>
    public DrawingSvgTextEmission TextEmission { get; set; } = DrawingSvgTextEmission.Runs;

    /// <summary>Whether a document has been loaded and compiled successfully.</summary>
    public bool IsLoaded => Picture != null;

    /// <summary>
    /// The loaded document as a display list - every drawing operation it compiles down to,
    /// in order. Walk it, hand it an <c>IDrawingCommandVisitor</c>, or replay it onto a
    /// canvas; it is <c>null</c> before a successful load. The picture is built once, at
    /// load time, and does not depend on this renderer afterwards: it keeps working after
    /// <see cref="Dispose"/>, text commands included, because the outliner they carry holds
    /// the font registry itself.
    /// </summary>
    public DrawingPicture Picture { get; private set; }

    /// <summary>
    /// The loaded document's structure - which element ended up where, what it links to,
    /// what sits under a point; <c>null</c> before a successful load. Like
    /// <see cref="Picture"/>, it stays valid after <see cref="Dispose"/>.
    /// </summary>
    public DrawingSvgScene Scene { get; private set; }

    /// <summary>
    /// The loaded document's bounds, in CSS pixels at 96 DPI, rounded out to whole pixels -
    /// an empty rectangle before a successful load. The origin can be non-zero. This is the
    /// same rectangle as <c>Picture.CullRect</c>, and the space every scene node's
    /// <see cref="DrawingSvgNode.DocumentBounds"/> is expressed in.
    /// </summary>
    public DrawingRect Bounds => Picture?.CullRect ?? DrawingRect.Empty;

    /// <summary>
    /// The width the document declared, in the unit it used (for example 80 millimeters);
    /// <c>SvgUnit.None</c> before a successful load. <see cref="Bounds"/> reports the same
    /// width converted to CSS pixels.
    /// </summary>
    public SvgUnit DeclaredWidth { get; private set; } = SvgUnit.None;

    /// <summary>
    /// The height the document declared, in the unit it used; <c>SvgUnit.None</c> before a
    /// successful load. <see cref="Bounds"/> reports the same height converted to CSS pixels.
    /// </summary>
    public SvgUnit DeclaredHeight { get; private set; } = SvgUnit.None;

    /// <summary>
    /// Everything the loaded document asked for that the managed renderer could not do
    /// exactly - each one reported once, with the reason typed as a
    /// <see cref="DrawingSvgWarningKind"/>. Most are raised while the document is loaded;
    /// the ones a filter effect raises appear once the document has actually been rendered,
    /// because that is when its primitives are evaluated.
    /// </summary>
    public IReadOnlyList<DrawingSvgWarning> Warnings
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
    /// <returns><c>true</c> when the document compiled; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    public bool Load(Stream stream)
    {
        if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
        return Compile(SvgService.Open(stream));
    }

    /// <summary>
    /// Loads and compiles an SVG document from a file path (plain or gzip-compressed SVG).
    /// </summary>
    /// <param name="path">The path of the SVG file.</param>
    /// <returns><c>true</c> when the document compiled; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
    public bool Load(string path)
    {
        if (String.IsNullOrWhiteSpace(path)) { throw new ArgumentException("A file path is required.", nameof(path)); }
        return Compile(SvgService.Open(path));
    }

    /// <summary>
    /// Loads and compiles an SVG document from a markup string.
    /// </summary>
    /// <param name="svgMarkup">The SVG markup.</param>
    /// <returns><c>true</c> when the document compiled; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="svgMarkup"/> is null or whitespace.</exception>
    public bool FromSvg(string svgMarkup)
    {
        if (String.IsNullOrWhiteSpace(svgMarkup))
        {
            throw new ArgumentException("SVG markup is required.", nameof(svgMarkup));
        }
        return Compile(SvgService.FromSvg(svgMarkup));
    }

    /// <summary>
    /// Replays the loaded document onto a canvas, honoring whatever transform the canvas
    /// already carries. Filter effects (blur radii, offsets) are evaluated at the scale that
    /// transform implies when each filtered layer opens, so the same picture is correct at
    /// any output resolution.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no document has been loaded.</exception>
    public void Render(DrawingCanvas canvas)
    {
        if (canvas == null) { throw new ArgumentNullException(nameof(canvas)); }
        if (Picture == null) { throw new InvalidOperationException("Load an SVG document before rendering."); }

        canvas.DrawPicture(Picture);
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

        DrawingRect bounds = Picture.CullRect;
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
            canvas.DrawPicture(Picture);
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
    /// Releases the renderer. The managed renderer holds no unmanaged resources, so this
    /// does nothing beyond letting a caller write the usual <c>using</c> - and, by design,
    /// it releases nothing that <see cref="Picture"/> or <see cref="Scene"/> depend on:
    /// both keep working, and keep being returned by this instance, after it is disposed.
    /// </summary>
    public void Dispose()
    {
    }

    private bool Compile(SvgDocument document)
    {
        Picture = null;
        Scene = null;
        DeclaredWidth = SvgUnit.None;
        DeclaredHeight = SvgUnit.None;
        lock (_warnings) { _warnings.Clear(); }

        if (document == null) { return false; }

        if (!SvgSceneRuntime.TryCompile(document, _assetLoader, DrawAttributes.None, out SvgSceneDocument sceneDocument)
            || sceneDocument == null)
        {
            return false;
        }

        SKPicture shimPicture = sceneDocument.CreateModel();
        if (shimPicture == null) { return false; }

        //One model for the loaded document's lifetime: the outliner the text commands carry
        //  and the image cache the picture's bitmaps come from both live on it, so the
        //  picture keeps working for as long as anyone holds it
        var filterFactory = new NoSkiaImageFilterFactory
        {
            UnsupportedPrimitive = OnUnsupportedFilterPrimitive,
        };
        var model = new NoSkiaModel(null, filterFactory)
        {
            UnsupportedShader = OnUnsupportedShader,
        };
        filterFactory.Model = model;

        var converter = new DrawingPictureConverter(model, new ImagingTextOutliner(_assetLoader, model))
        {
            TextEmission = TextEmission,
            Warning = AddWarning,
        };

        //A pattern fill's tile is itself a compiled picture, so the model converts it
        //  through the same converter - one identity cache for the whole document
        model.ConvertPicture = converter.Convert;

        DrawingPicture picture = converter.Convert(shimPicture);
        if (picture == null) { return false; }

        Picture = picture;
        Scene = new DrawingSvgScene(sceneDocument);
        DeclaredWidth = document.Width;
        DeclaredHeight = document.Height;

        if (_fonts.Count == 0 && ContainsText(picture))
        {
            AddWarning(DrawingSvgWarningKind.NoFontsRegistered,
                "The document draws text but no fonts were registered before it was loaded, "
                + "so every run outlines to nothing.");
        }

        return true;
    }

    private void OnUnsupportedFilterPrimitive(string primitiveName)
    {
        AddWarning(DrawingSvgWarningKind.UnsupportedFilterPrimitive,
            $"The filter primitive '{primitiveName}' is not evaluated; its input passed through unchanged.");
    }

    private void OnUnsupportedShader(SKShader shader)
    {
        switch (shader)
        {
            case PerlinNoiseFractalNoiseShader _:
            case PerlinNoiseTurbulenceShader _:
                AddWarning(DrawingSvgWarningKind.TurbulenceDropped,
                    "An feTurbulence primitive was dropped: Perlin noise is not generated.");
                break;

            default:
                AddWarning(DrawingSvgWarningKind.UnsupportedFilterPrimitive,
                    $"The shader '{shader?.GetType().Name}' cannot be used as a filter source.");
                break;
        }
    }

    private void AddWarning(DrawingSvgWarningKind kind, string message)
    {
        var warning = new DrawingSvgWarning(kind, message);
        lock (_warnings)
        {
            if (!_warnings.Contains(warning)) { _warnings.Add(warning); }
        }
    }

    private static bool ContainsText(DrawingPicture picture)
    {
        var detector = new TextDetector();
        picture.Accept(detector);
        return detector.Found;
    }

    /// <summary>
    /// Answers whether a picture draws any text at all, nested pictures included - what
    /// decides whether an empty font registry is worth warning about.
    /// </summary>
    private sealed class TextDetector : IDrawingCommandVisitor
    {
        public bool Found { get; private set; }

        public void Visit(DrawTextCommand command) => Found = true;

        public void Visit(DrawPositionedTextCommand command) => Found = true;

        public void Visit(DrawTextOnPathCommand command) => Found = true;

        public void Visit(DrawPictureCommand command)
        {
            if (!Found) { command.Picture?.Accept(this); }
        }
    }
}
