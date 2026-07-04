using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Rendering;

/// <summary>
/// Renders <see cref="DrawingLayer"/> collections onto SkiaSharp canvases with the
/// "highlighter" compositing model: each layer's strokes are drawn fully opaque onto a
/// private, transparent cache bitmap, and the whole cache is then composited over the
/// background at the layer opacity. Overlapping strokes within one layer therefore never
/// darken each other, which is what makes translucent strokes read as highlighter ink.
/// Layer caches are rendered incrementally - only strokes added since the previous render
/// are drawn - so live drawing stays cheap even for large stroke counts.
/// </summary>
public sealed class DrawingRenderer : IDisposable
{
    private sealed class LayerCache : IDisposable
    {
        public SKBitmap Bitmap;
        public int DrawnElementCount;
        public int ResetVersion;

        public void Dispose()
        {
            Bitmap?.Dispose();
            Bitmap = null;
        }
    }

    private sealed class SceneLayerState
    {
        public DrawingLayer Layer;
        public int ResetVersion;
        public int ElementCount;
    }

    private readonly object _renderLocker = new object();
    private readonly Dictionary<DrawingLayer, LayerCache> _layerCaches = new Dictionary<DrawingLayer, LayerCache>();

    private readonly SKSizeI _calibrationSize;
    private SKColor _backgroundFillColor = SKColors.Transparent;
    private SKColor _surfaceClearColor = SKColors.Transparent;
    private SKRect _lastDrawingRect;
    private SKSizeI _lastCanvasSize;

    private SKSizeI _cachedCanvasSize;

    //The static scene (background + committed layers) is kept pre-composited so that the
    //  per-frame cost while a stroke is being drawn is one 1:1 bitmap blit plus the
    //  in-progress stroke - NOT a rescale of a potentially large background image
    private SKBitmap _scaledBackground;
    private SKBitmap _backgroundImage;
    private SKBitmap _sceneCache;
    private bool _sceneDirty = true;
    private SKColor _sceneFillColor;
    private SKColor _sceneClearColor;
    private byte _sceneLayerOpacity;
    private readonly List<SceneLayerState> _sceneLayers = new List<SceneLayerState>();

    /// <summary>
    /// The size of the calibrated drawing space that all strokes are expressed in.
    /// </summary>
    public Size CalibrationSize => SkiaInterop.ToImaging(_calibrationSize);

    /// <summary>Gets <see cref="CalibrationSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The calibration size as a SkiaSharp size.</returns>
    public SKSizeI GetCalibrationSizeAsSkia() => _calibrationSize;

    /// <summary>
    /// An optional background image drawn behind the layers, scaled (aspect-fit) into the
    /// drawing rectangle - for example a black-and-white body-map line diagram. The
    /// renderer does not take ownership of the bitmap; the caller disposes it. When
    /// <c>null</c>, the layers render over the <see cref="BackgroundFillColor"/> alone,
    /// which (left transparent) supports highlighting over externally drawn content such
    /// as a live video frame. The image is rescaled once per canvas size (not per frame),
    /// so large source images do not slow down live drawing.
    /// </summary>
    public SKBitmap BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            if (!ReferenceEquals(_backgroundImage, value))
            {
                _backgroundImage = value;
                _scaledBackground?.Dispose();
                _scaledBackground = null;
                _sceneDirty = true;
            }
        }
    }

    /// <summary>
    /// The color that the drawing rectangle is filled with before the background image and
    /// layers are drawn. Defaults to transparent; set an opaque color (for example white)
    /// when the drawing should supply its own page background.
    /// </summary>
    public Color BackgroundFillColor
    {
        get => SkiaInterop.ToImaging(_backgroundFillColor);
        set => _backgroundFillColor = SkiaInterop.ToSK(value);
    }

    /// <summary>Sets <see cref="BackgroundFillColor"/> from a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <param name="color">The background fill color.</param>
    public void SetBackgroundFillColor(SKColor color) => _backgroundFillColor = color;

    /// <summary>Gets <see cref="BackgroundFillColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The background fill color as a SkiaSharp color.</returns>
    public SKColor GetBackgroundFillColorAsSkia() => _backgroundFillColor;

    /// <summary>
    /// The color that the whole canvas is cleared to at the start of every render.
    /// Defaults to transparent so the drawing can be composited over other content.
    /// </summary>
    public Color SurfaceClearColor
    {
        get => SkiaInterop.ToImaging(_surfaceClearColor);
        set => _surfaceClearColor = SkiaInterop.ToSK(value);
    }

    /// <summary>Sets <see cref="SurfaceClearColor"/> from a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <param name="color">The surface clear color.</param>
    public void SetSurfaceClearColor(SKColor color) => _surfaceClearColor = color;

    /// <summary>Gets <see cref="SurfaceClearColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The surface clear color as a SkiaSharp color.</returns>
    public SKColor GetSurfaceClearColorAsSkia() => _surfaceClearColor;

    /// <summary>
    /// The alpha (0-255) that completed layers are composited with; the default of 100
    /// produces the translucent highlighter effect. Use 255 for fully opaque painting.
    /// </summary>
    public byte LayerOpacity { get; set; } = 100;

    /// <summary>
    /// The alpha (0-255) that the in-progress (active) stroke is drawn with; slightly more
    /// vivid than settled layer content by default.
    /// </summary>
    public byte ActiveStrokeOpacity { get; set; } = 200;

    /// <summary>
    /// The drawing rectangle, in canvas pixel coordinates, computed by the most recent
    /// <see cref="Render"/> call; an empty rectangle before the first render.
    /// </summary>
    public RectangleF LastDrawingRect => SkiaInterop.ToImaging(_lastDrawingRect);

    /// <summary>Gets <see cref="LastDrawingRect"/> as a SkiaSharp <see cref="SKRect"/>.</summary>
    /// <returns>The last drawing rectangle as a SkiaSharp rectangle.</returns>
    public SKRect GetLastDrawingRectAsSkia() => _lastDrawingRect;

    /// <summary>
    /// The canvas pixel size seen by the most recent <see cref="Render"/> call;
    /// an empty size before the first render.
    /// </summary>
    public Size LastCanvasSize => SkiaInterop.ToImaging(_lastCanvasSize);

    /// <summary>Gets <see cref="LastCanvasSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The last canvas size as a SkiaSharp size.</returns>
    public SKSizeI GetLastCanvasSizeAsSkia() => _lastCanvasSize;

    /// <summary>
    /// Indicates whether this renderer has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Creates a new renderer for the given calibrated drawing space.
    /// </summary>
    /// <param name="calibrationSize">
    /// The size of the calibrated drawing space; both dimensions must be positive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either dimension of <paramref name="calibrationSize"/> is less than 1.
    /// </exception>
    public DrawingRenderer(SKSizeI calibrationSize)
    {
        if (calibrationSize.Width < 1 || calibrationSize.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(calibrationSize));
        }
        _calibrationSize = calibrationSize;
    }

    /// <summary>
    /// Creates a new renderer for the given calibrated drawing space.
    /// </summary>
    /// <param name="calibrationSize">
    /// The size of the calibrated drawing space; both dimensions must be positive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either dimension of <paramref name="calibrationSize"/> is less than 1.
    /// </exception>
    public DrawingRenderer(Size calibrationSize)
        : this(SkiaInterop.ToSK(calibrationSize))
    {
    }

    /// <summary>
    /// Renders the given layers (plus an optional in-progress stroke) onto a canvas. The
    /// canvas is cleared first, so this call produces the complete frame.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="info">The image info describing the canvas surface, including its pixel size.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="activeStroke">The in-progress stroke to draw on top of the layers, if any.</param>
    /// <param name="activeStrokeColor">
    /// The color for <paramref name="activeStroke"/>; required when an active stroke is supplied.
    /// </param>
    /// <param name="clearCanvas">
    /// When <c>true</c> (the default), the canvas is cleared to <see cref="SurfaceClearColor"/>
    /// first, producing the complete frame. Pass <c>false</c> when the caller has already
    /// drawn content that the drawing should overlay - for example a live video frame
    /// drawn onto the same canvas immediately before this call.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="canvas"/> or <paramref name="layers"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public void Render(
        SKCanvas canvas,
        SKImageInfo info,
        IReadOnlyList<DrawingLayer> layers,
        Stroke activeStroke = null,
        SKColor? activeStrokeColor = null,
        bool clearCanvas = true)
    {
        if (canvas == null) { throw new ArgumentNullException(nameof(canvas)); }
        if (layers == null) { throw new ArgumentNullException(nameof(layers)); }
        ThrowIfDisposed();

        lock (_renderLocker)
        {
            var canvasSize = new SKSizeI(info.Width, info.Height);
            SKRect drawingRect = CanvasCalibration.GetDrawingRect(canvasSize, _calibrationSize);

            _lastCanvasSize = canvasSize;
            _lastDrawingRect = drawingRect;

            if (clearCanvas)
            {
                canvas.Clear(_surfaceClearColor);
            }

            //If the drawing area is degenerate (control not laid out yet), there is nothing to draw
            if (drawingRect.Width < 2 || drawingRect.Height < 2) { return; }

            if (canvasSize != _cachedCanvasSize)
            {
                ResetLayerCaches();
                ResetSceneCaches();
                _cachedCanvasSize = canvasSize;
            }
            PruneLayerCaches(layers);

            var cacheSize = new SKSizeI(
                Math.Max(1, (int)Math.Round(drawingRect.Width, MidpointRounding.AwayFromZero)),
                Math.Max(1, (int)Math.Round(drawingRect.Height, MidpointRounding.AwayFromZero)));
            var cacheRect = new SKRect(0, 0, cacheSize.Width, cacheSize.Height);

            foreach (DrawingLayer layer in layers)
            {
                UpdateLayerCache(layer, cacheSize, cacheRect);
            }

            EnsureScaledBackground(cacheSize);

            if (SceneNeedsRebuild(canvasSize, layers))
            {
                RebuildSceneCache(canvasSize, drawingRect, cacheSize, layers);
            }

            canvas.DrawBitmap(_sceneCache,
                SKRect.Create(0, 0, _sceneCache.Width, _sceneCache.Height), SKSamplingOptions.Default);

            if (activeStroke != null && activeStroke.PointCount > 0 && activeStrokeColor.HasValue)
            {
                DrawStroke(canvas, activeStroke, drawingRect,
                    activeStrokeColor.Value.WithAlpha(ActiveStrokeOpacity));
            }
        }
    }

    /// <summary>
    /// Renders the layers to a new image of the given size - for saving a finished drawing
    /// to a file. The image is always a complete, from-scratch render (no caches are used),
    /// with every layer composited at <see cref="LayerOpacity"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the <see cref="BackgroundFillColor"/> and
    /// <see cref="BackgroundImage"/> are rendered behind the layers; when <c>false</c>,
    /// the layers render over transparency.
    /// </param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public SKImage RenderToImage(SKSizeI outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
    {
        if (outputSize.Width < 1 || outputSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(outputSize)); }
        if (layers == null) { throw new ArgumentNullException(nameof(layers)); }
        ThrowIfDisposed();

        lock (_renderLocker)
        {
            var imageInfo = new SKImageInfo(outputSize.Width, outputSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(imageInfo);
            SKCanvas canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);
            SKRect drawingRect = CanvasCalibration.GetDrawingRect(outputSize, _calibrationSize);

            if (includeBackground)
            {
                DrawBackground(canvas, drawingRect);
            }

            foreach (DrawingLayer layer in layers)
            {
                using SKBitmap layerBitmap = RenderLayerBitmap(layer, outputSize, drawingRect);
                if (layerBitmap != null)
                {
                    using var compositePaint = new SKPaint
                    {
                        Color = SKColors.White.WithAlpha(LayerOpacity),
                    };
                    canvas.DrawBitmap(layerBitmap, new SKRect(0, 0, outputSize.Width, outputSize.Height),
                        SKSamplingOptions.Default, compositePaint);
                }
            }

            return surface.Snapshot();
        }
    }

    /// <summary>
    /// Renders the layers to a new image of the given size - for saving a finished drawing
    /// to a file. The image is always a complete, from-scratch render (no caches are used),
    /// with every layer composited at <see cref="LayerOpacity"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the <see cref="BackgroundFillColor"/> and
    /// <see cref="BackgroundImage"/> are rendered behind the layers; when <c>false</c>,
    /// the layers render over transparency.
    /// </param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public SKImage RenderToImage(Size outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
        => RenderToImage(SkiaInterop.ToSK(outputSize), layers, includeBackground);

    private void EnsureScaledBackground(SKSizeI cacheSize)
    {
        if (_backgroundImage == null)
        {
            if (_scaledBackground != null)
            {
                _scaledBackground.Dispose();
                _scaledBackground = null;
                _sceneDirty = true;
            }
            return;
        }

        if (_scaledBackground != null
            && _scaledBackground.Width == cacheSize.Width
            && _scaledBackground.Height == cacheSize.Height)
        {
            return;
        }

        _scaledBackground?.Dispose();
        _scaledBackground = new SKBitmap(cacheSize.Width, cacheSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _backgroundImage.ScalePixels(_scaledBackground, new SKSamplingOptions(SKCubicResampler.Mitchell));
        _sceneDirty = true;
    }

    private bool SceneNeedsRebuild(SKSizeI canvasSize, IReadOnlyList<DrawingLayer> layers)
    {
        if (_sceneDirty
            || _sceneCache == null
            || _sceneCache.Width != canvasSize.Width
            || _sceneCache.Height != canvasSize.Height
            || _sceneFillColor != _backgroundFillColor
            || _sceneClearColor != _surfaceClearColor
            || _sceneLayerOpacity != LayerOpacity
            || _sceneLayers.Count != layers.Count)
        {
            return true;
        }

        for (int i = 0; i < layers.Count; i++)
        {
            SceneLayerState state = _sceneLayers[i];
            DrawingLayer layer = layers[i];
            if (!ReferenceEquals(state.Layer, layer)
                || state.ResetVersion != layer.ResetVersion
                || state.ElementCount != _layerCaches[layer].DrawnElementCount)
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildSceneCache(SKSizeI canvasSize, SKRect drawingRect, SKSizeI cacheSize, IReadOnlyList<DrawingLayer> layers)
    {
        if (_sceneCache == null || _sceneCache.Width != canvasSize.Width || _sceneCache.Height != canvasSize.Height)
        {
            _sceneCache?.Dispose();
            _sceneCache = new SKBitmap(canvasSize.Width, canvasSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        }

        //The scaled background and layer caches share cacheSize, so they blit 1:1 at the
        //  (rounded) drawing-rect origin
        var cacheDest = SKRect.Create(
            (float)Math.Round(drawingRect.Left, MidpointRounding.AwayFromZero),
            (float)Math.Round(drawingRect.Top, MidpointRounding.AwayFromZero),
            cacheSize.Width, cacheSize.Height);

        using var sceneCanvas = new SKCanvas(_sceneCache);
        sceneCanvas.Clear(_surfaceClearColor);

        if (_backgroundFillColor.Alpha > 0)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = _backgroundFillColor,
            };
            sceneCanvas.DrawRect(cacheDest, fillPaint);
        }

        if (_scaledBackground != null)
        {
            sceneCanvas.DrawBitmap(_scaledBackground, cacheDest, SKSamplingOptions.Default);
        }

        foreach (DrawingLayer layer in layers)
        {
            LayerCache cache = _layerCaches[layer];
            if (cache.DrawnElementCount > 0)
            {
                using var compositePaint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha(LayerOpacity),
                };
                sceneCanvas.DrawBitmap(cache.Bitmap, cacheDest, SKSamplingOptions.Default, compositePaint);
            }
        }

        //Record what the scene was built from, to detect staleness on later renders
        _sceneFillColor = _backgroundFillColor;
        _sceneClearColor = _surfaceClearColor;
        _sceneLayerOpacity = LayerOpacity;
        _sceneLayers.Clear();
        foreach (DrawingLayer layer in layers)
        {
            _sceneLayers.Add(new SceneLayerState
            {
                Layer = layer,
                ResetVersion = layer.ResetVersion,
                ElementCount = _layerCaches[layer].DrawnElementCount,
            });
        }
        _sceneDirty = false;
    }

    private void ResetSceneCaches()
    {
        _sceneCache?.Dispose();
        _sceneCache = null;
        _scaledBackground?.Dispose();
        _scaledBackground = null;
        _sceneLayers.Clear();
        _sceneDirty = true;
    }

    private void DrawBackground(SKCanvas canvas, SKRect drawingRect)
    {
        if (_backgroundFillColor.Alpha > 0)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = _backgroundFillColor,
            };
            canvas.DrawRect(drawingRect, fillPaint);
        }

        if (BackgroundImage != null)
        {
            using var imagePaint = new SKPaint();
            canvas.DrawBitmap(BackgroundImage, drawingRect,
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), imagePaint);
        }
    }

    private void UpdateLayerCache(DrawingLayer layer, SKSizeI cacheSize, SKRect cacheRect)
    {
        if (!_layerCaches.TryGetValue(layer, out LayerCache cache))
        {
            cache = new LayerCache();
            _layerCaches.Add(layer, cache);
        }

        bool needsFullRedraw = cache.Bitmap == null
            || cache.Bitmap.Width != cacheSize.Width
            || cache.Bitmap.Height != cacheSize.Height
            || cache.ResetVersion != layer.ResetVersion;

        if (needsFullRedraw)
        {
            cache.Dispose();
            cache.Bitmap = new SKBitmap(cacheSize.Width, cacheSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            cache.DrawnElementCount = 0;
            cache.ResetVersion = layer.ResetVersion;

            using var clearCanvas = new SKCanvas(cache.Bitmap);
            clearCanvas.Clear(SKColors.Transparent);
        }

        DrawingElement[] elements = layer.GetElements();
        if (elements.Length > cache.DrawnElementCount)
        {
            using var cacheCanvas = new SKCanvas(cache.Bitmap);
            for (int i = cache.DrawnElementCount; i < elements.Length; i++)
            {
                DrawElement(cacheCanvas, elements[i], cacheRect, layer.GetColorAsSkia());
            }
            cache.DrawnElementCount = elements.Length;
        }
    }

    private SKBitmap RenderLayerBitmap(DrawingLayer layer, SKSizeI size, SKRect drawingRect)
    {
        DrawingElement[] elements = layer.GetElements();
        if (elements.Length < 1) { return null; }

        var bitmap = new SKBitmap(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        foreach (DrawingElement element in elements)
        {
            DrawElement(canvas, element, drawingRect, layer.GetColorAsSkia());
        }

        return bitmap;
    }

    private void DrawElement(SKCanvas canvas, DrawingElement element, SKRect drawingRect, SKColor layerColor)
    {
        if (element is Stroke stroke)
        {
            DrawStroke(canvas, stroke, drawingRect, layerColor);
        }
        else if (element is DrawingShape shape)
        {
            DrawShape(canvas, shape, drawingRect, shape.GetColorAsSkia() ?? layerColor);
        }
    }

    private void DrawShape(SKCanvas canvas, DrawingShape shape, SKRect drawingRect, SKColor color)
    {
        //Shapes draw in calibrated coordinates; the canvas transform maps them (and their
        //  paint stroke widths, which scale with the matrix) to the output size
        float scale = drawingRect.Width / _calibrationSize.Width;

        canvas.Save();
        canvas.Translate(drawingRect.Left, drawingRect.Top);
        canvas.Scale(scale);
        shape.Draw(canvas, color);
        canvas.Restore();
    }

    private void DrawStroke(SKCanvas canvas, Stroke stroke, SKRect drawingRect, SKColor color)
    {
        StrokePoint[] points = stroke.GetPoints();
        if (points.Length < 1) { return; }

        float pixelWidth = CanvasCalibration.ScaleStrokeWidth(stroke.Width, _calibrationSize, drawingRect);

        if (points.Length == 1)
        {
            //A single-point stroke renders as a dot
            SKPoint dot = CanvasCalibration.CalibratedToCanvas(
                new SKPointI(points[0].X, points[0].Y), _calibrationSize, drawingRect);
            using var dotPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = color,
                IsAntialias = true,
            };
            canvas.DrawCircle(dot, pixelWidth / 2f, dotPaint);
            return;
        }

        var pathBuilder = new SKPathBuilder();
        SKPoint first = CanvasCalibration.CalibratedToCanvas(
            new SKPointI(points[0].X, points[0].Y), _calibrationSize, drawingRect);
        pathBuilder.MoveTo(first);
        for (int i = 1; i < points.Length; i++)
        {
            pathBuilder.LineTo(CanvasCalibration.CalibratedToCanvas(
                new SKPointI(points[i].X, points[i].Y), _calibrationSize, drawingRect));
        }
        using SKPath path = pathBuilder.Detach();

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = pixelWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        };
        canvas.DrawPath(path, strokePaint);
    }

    private void ResetLayerCaches()
    {
        foreach (LayerCache cache in _layerCaches.Values)
        {
            cache.Dispose();
        }
        _layerCaches.Clear();
    }

    private void PruneLayerCaches(IReadOnlyList<DrawingLayer> layers)
    {
        if (_layerCaches.Count < 1) { return; }

        List<DrawingLayer> stale = null;
        foreach (DrawingLayer cachedLayer in _layerCaches.Keys)
        {
            bool found = false;
            for (int i = 0; i < layers.Count; i++)
            {
                if (ReferenceEquals(layers[i], cachedLayer))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                (stale ??= new List<DrawingLayer>()).Add(cachedLayer);
            }
        }

        if (stale != null)
        {
            foreach (DrawingLayer layer in stale)
            {
                _layerCaches[layer].Dispose();
                _layerCaches.Remove(layer);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) { throw new ObjectDisposedException(nameof(DrawingRenderer)); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;

        lock (_renderLocker)
        {
            ResetLayerCaches();
            ResetSceneCaches();
        }
    }
}
