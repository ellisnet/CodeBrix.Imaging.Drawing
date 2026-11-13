using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Rendering;

/// <summary>
/// Renders <see cref="DrawingLayer"/> collections onto a <see cref="SKCanvas"/> with the
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
        //How many leading entries of the layer's element list the bitmap reflects (each one
        //  either drawn, or skipped because it did not exist at the render time)
        public int DrawnElementCount;
        public int ResetVersion;
        //Increments whenever the bitmap's content changes; the scene cache compares it
        public int ContentVersion;
        //The layer view (camera and layer transform) the bitmap was drawn through
        public bool HasView;
        public Matrix3x2 View;
        //What each processed DYNAMIC entry looked like when processed; null until the layer
        //  first holds one (static layers never allocate it)
        public ElementState[] States;
        //A masked layer or one with effects is drawn whole and post-processed; the state of
        //  everything that went into it
        public bool PostProcessed;
        public long PostStamp;

        public void Dispose()
        {
            Bitmap?.Dispose();
            Bitmap = null;
        }
    }

    private struct ElementState
    {
        public bool Tracked;
        public bool Visible;
        public long Stamp;
    }

    private sealed class SceneLayerState
    {
        public DrawingLayer Layer;
        public int ResetVersion;
        public int ElementCount;
        public int ContentVersion;
        public float Opacity;
        public bool Visible;
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
    private float _sceneOpacity = 1f;
    private float _opacity = 1f;
    private double _time;

    //How many times a layer cache has been cleared and redrawn from scratch (a test hook that
    //  shows which changes stay incremental)
    internal int LayerFullRedrawCount { get; private set; }
    private readonly List<SceneLayerState> _sceneLayers = new List<SceneLayerState>();

    /// <summary>
    /// The size of the calibrated drawing space that all strokes are expressed in.
    /// </summary>
    public Size CalibrationSize => GraphicsInterop.ToImaging(_calibrationSize);

    //The name-neutral accessors that DrawingSession reads, so the public accessors can carry
    //  a different name in each package without an #if at any call site
    internal SKSizeI CalibrationSizeValue => _calibrationSize;

#if NOSKIA
    /// <summary>Gets <see cref="CalibrationSize"/> as a <see cref="DrawingSizeI"/>.</summary>
    /// <returns>The calibration size as a backend size.</returns>
    public DrawingSizeI GetCalibrationSizeAsDrawing() => _calibrationSize;
#else
    /// <summary>Gets <see cref="CalibrationSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The calibration size as a SkiaSharp size.</returns>
    public SKSizeI GetCalibrationSizeAsSkia() => _calibrationSize;
#endif

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
        get => GraphicsInterop.ToImaging(_backgroundFillColor);
        set => _backgroundFillColor = GraphicsInterop.ToGraphics(value);
    }

    /// <summary>Sets <see cref="BackgroundFillColor"/> from a <see cref="SKColor"/>.</summary>
    /// <param name="color">The background fill color.</param>
    public void SetBackgroundFillColor(SKColor color) => _backgroundFillColor = color;

    internal SKColor BackgroundFillColorValue => _backgroundFillColor;

#if NOSKIA
    /// <summary>Gets <see cref="BackgroundFillColor"/> as a <see cref="DrawingColor"/>.</summary>
    /// <returns>The background fill color as a backend color.</returns>
    public DrawingColor GetBackgroundFillColorAsDrawing() => _backgroundFillColor;
#else
    /// <summary>Gets <see cref="BackgroundFillColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The background fill color as a SkiaSharp color.</returns>
    public SKColor GetBackgroundFillColorAsSkia() => _backgroundFillColor;
#endif

    /// <summary>
    /// The color that the whole canvas is cleared to at the start of every render.
    /// Defaults to transparent so the drawing can be composited over other content.
    /// </summary>
    public Color SurfaceClearColor
    {
        get => GraphicsInterop.ToImaging(_surfaceClearColor);
        set => _surfaceClearColor = GraphicsInterop.ToGraphics(value);
    }

    /// <summary>Sets <see cref="SurfaceClearColor"/> from a <see cref="SKColor"/>.</summary>
    /// <param name="color">The surface clear color.</param>
    public void SetSurfaceClearColor(SKColor color) => _surfaceClearColor = color;

    internal SKColor SurfaceClearColorValue => _surfaceClearColor;

#if NOSKIA
    /// <summary>Gets <see cref="SurfaceClearColor"/> as a <see cref="DrawingColor"/>.</summary>
    /// <returns>The surface clear color as a backend color.</returns>
    public DrawingColor GetSurfaceClearColorAsDrawing() => _surfaceClearColor;
#else
    /// <summary>Gets <see cref="SurfaceClearColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The surface clear color as a SkiaSharp color.</returns>
    public SKColor GetSurfaceClearColorAsSkia() => _surfaceClearColor;
#endif

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
    /// The opacity, 0..1, of the whole drawing - every layer, composited together as ONE
    /// group and then faded over the <see cref="BackgroundFillColor"/> and
    /// <see cref="BackgroundImage"/> (which are not faded). Animate it for a fade in or out
    /// of the entire frame; because the layers fade as a group, layers that cover each
    /// other do not show through one another part-way through the fade. The default of 1
    /// renders exactly as before, and <see cref="LayerOpacity"/> keeps its meaning (each
    /// layer's own compositing alpha within the group). The in-progress stroke fades too.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or a value outside 0..1.</exception>
    public float Opacity
    {
        get => _opacity;
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _opacity = value;
        }
    }

    /// <summary>
    /// A safe-area mask drawn over the live <see cref="Render"/> output (after everything
    /// else, over the whole canvas); or <c>null</c> (the default) for none. It never
    /// affects <see cref="RenderToImage(Size, IReadOnlyList{DrawingLayer}, bool)"/> or
    /// <see cref="RenderToPixels(Span{byte}, Size, IReadOnlyList{DrawingLayer}, bool)"/>;
    /// those include a mask only when one is passed to their overlay overloads.
    /// </summary>
    public SafeAreaOverlay SafeAreaOverlay { get; set; }

    /// <summary>
    /// A debugging read-out (timecode, frame number, active cues) drawn over the live
    /// <see cref="Render"/> output, on top of everything; or <c>null</c> (the default) for
    /// none. It never affects an export; the export overloads that take a
    /// <see cref="Rendering.DebugOverlay"/> argument include one explicitly.
    /// </summary>
    public DebugOverlay DebugOverlay { get; set; }

    /// <summary>
    /// The scene time, seconds, that elements' time windows
    /// (<see cref="DrawingElement.StartTime"/>, <see cref="DrawingElement.EndTime"/>) are
    /// evaluated at - in the live render and in every export. 0 by default. Setting it does
    /// not apply animations (a session's <c>Time</c> does that, then sets this).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN.</exception>
    public double Time
    {
        get => _time;
        set
        {
            if (Double.IsNaN(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _time = value;
        }
    }

    /// <summary>
    /// A view transform over the whole scene - pan, zoom and roll - applied while drawing; or
    /// <c>null</c> (the default) for none. Each layer follows it by its
    /// <see cref="DrawingLayer.Parallax"/>.
    /// </summary>
    public DrawingCamera Camera { get; set; }

    /// <summary>
    /// The drawing rectangle, in canvas pixel coordinates, computed by the most recent
    /// <see cref="Render"/> call; an empty rectangle before the first render.
    /// </summary>
    public RectangleF LastDrawingRect => GraphicsInterop.ToImaging(_lastDrawingRect);

#if NOSKIA
    /// <summary>Gets <see cref="LastDrawingRect"/> as a <see cref="DrawingRect"/>.</summary>
    /// <returns>The last drawing rectangle as a backend rectangle.</returns>
    public DrawingRect GetLastDrawingRectAsDrawing() => _lastDrawingRect;
#else
    /// <summary>Gets <see cref="LastDrawingRect"/> as a SkiaSharp <see cref="SKRect"/>.</summary>
    /// <returns>The last drawing rectangle as a SkiaSharp rectangle.</returns>
    public SKRect GetLastDrawingRectAsSkia() => _lastDrawingRect;
#endif

    /// <summary>
    /// The canvas pixel size seen by the most recent <see cref="Render"/> call;
    /// an empty size before the first render.
    /// </summary>
    public Size LastCanvasSize => GraphicsInterop.ToImaging(_lastCanvasSize);

    internal SKSizeI LastCanvasSizeValue => _lastCanvasSize;

#if NOSKIA
    /// <summary>Gets <see cref="LastCanvasSize"/> as a <see cref="DrawingSizeI"/>.</summary>
    /// <returns>The last canvas size as a backend size.</returns>
    public DrawingSizeI GetLastCanvasSizeAsDrawing() => _lastCanvasSize;
#else
    /// <summary>Gets <see cref="LastCanvasSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The last canvas size as a SkiaSharp size.</returns>
    public SKSizeI GetLastCanvasSizeAsSkia() => _lastCanvasSize;
#endif

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
        : this(GraphicsInterop.ToGraphics(calibrationSize))
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
                //A hidden layer costs nothing: its cache is neither updated nor composited
                if (!layer.IsVisible) { continue; }
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
                    activeStrokeColor.Value.WithAlpha(ScaleAlpha(ActiveStrokeOpacity, _opacity)));
            }

            SafeAreaOverlay?.Draw(canvas, new SizeF(canvasSize.Width, canvasSize.Height), _time);
            DebugOverlay?.Draw(canvas, new SizeF(canvasSize.Width, canvasSize.Height), _time);
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
        => RenderToImageCore(outputSize, layers, includeBackground, null, null);

    /// <summary>
    /// Renders the layers to a new image of the given size, with a safe-area mask drawn
    /// over the finished frame - for a preview still that shows the unsafe zones. Apart
    /// from the mask this is exactly <see cref="RenderToImage(Size, IReadOnlyList{DrawingLayer}, bool)"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">
    /// When <c>true</c>, the <see cref="BackgroundFillColor"/> and <see cref="BackgroundImage"/>
    /// are rendered behind the layers; when <c>false</c>, the layers render over transparency.
    /// </param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw over the frame; or <c>null</c> for none.</param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public SKImage RenderToImage(Size outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground, SafeAreaOverlay safeAreaOverlay)
        => RenderToImageCore(GraphicsInterop.ToGraphics(outputSize), layers, includeBackground, safeAreaOverlay, null);

    /// <summary>
    /// Renders the layers to a new image of the given size with preview overlays drawn over
    /// the finished frame - a safe-area mask, a debugging read-out, or both (either may be
    /// <c>null</c>). Apart from the overlays this is exactly <see cref="RenderToImage(Size, IReadOnlyList{DrawingLayer}, bool)"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw; or <c>null</c> for none.</param>
    /// <param name="debugOverlay">The debugging read-out to draw on top; or <c>null</c> for none.</param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public SKImage RenderToImage(Size outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground,
        SafeAreaOverlay safeAreaOverlay, DebugOverlay debugOverlay)
        => RenderToImageCore(GraphicsInterop.ToGraphics(outputSize), layers, includeBackground, safeAreaOverlay, debugOverlay);

    /// <summary>
    /// Renders the layers - exactly as <see cref="RenderToImage(Size, IReadOnlyList{DrawingLayer}, bool)"/>
    /// would - and writes the frame into a caller buffer as plain 8-bit RGBA bytes: four
    /// bytes per pixel in R, G, B, A order, rows top to bottom with no padding (a stride of
    /// width x 4), and STRAIGHT (not premultiplied) alpha - the same layout as a
    /// CodeBrix.Imaging <c>Image&lt;Rgba32&gt;</c>. Use it to hand frames to a video encoder, a
    /// window texture or any pixel pipeline without an encode/decode round trip.
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes (any extra bytes are left untouched).</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the background fill and image render behind the
    /// layers; when <c>false</c>, the layers render over transparency and the alpha bytes
    /// carry it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public void RenderToPixels(Span<byte> destination, SKSizeI outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
        => RenderToPixelsCore(destination, outputSize, layers, includeBackground, null, null);

    /// <summary>
    /// Renders the layers and writes the frame into a caller buffer as plain, straight-alpha
    /// RGBA bytes - see <see cref="RenderToPixels(Span{byte}, SKSizeI, IReadOnlyList{DrawingLayer}, bool)"/>
    /// for the exact byte layout.
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes.</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public void RenderToPixels(Span<byte> destination, Size outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
        => RenderToPixelsCore(destination, GraphicsInterop.ToGraphics(outputSize), layers, includeBackground, null, null);

    /// <summary>
    /// Renders the layers and writes the frame into a caller buffer as plain, straight-alpha
    /// RGBA bytes, with a safe-area mask drawn over the finished frame.
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes.</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw over the frame; or <c>null</c> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public void RenderToPixels(Span<byte> destination, Size outputSize, IReadOnlyList<DrawingLayer> layers,
        bool includeBackground, SafeAreaOverlay safeAreaOverlay)
        => RenderToPixelsCore(destination, GraphicsInterop.ToGraphics(outputSize), layers, includeBackground, safeAreaOverlay, null);

    /// <summary>
    /// Renders the layers and writes the frame into a caller buffer as plain, straight-alpha
    /// RGBA bytes, with preview overlays drawn over the finished frame - a safe-area mask, a
    /// debugging read-out, or both (either may be <c>null</c>).
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes.</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw; or <c>null</c> for none.</param>
    /// <param name="debugOverlay">The debugging read-out to draw on top; or <c>null</c> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public void RenderToPixels(Span<byte> destination, Size outputSize, IReadOnlyList<DrawingLayer> layers,
        bool includeBackground, SafeAreaOverlay safeAreaOverlay, DebugOverlay debugOverlay)
        => RenderToPixelsCore(destination, GraphicsInterop.ToGraphics(outputSize), layers, includeBackground, safeAreaOverlay, debugOverlay);

    private void RenderToPixelsCore(Span<byte> destination, SKSizeI outputSize, IReadOnlyList<DrawingLayer> layers,
        bool includeBackground, SafeAreaOverlay overlay, DebugOverlay debugOverlay)
    {
        if (outputSize.Width < 1 || outputSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(outputSize)); }
        long required = (long)outputSize.Width * outputSize.Height * 4;
        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"The destination holds {destination.Length} bytes but a {outputSize.Width} x {outputSize.Height} RGBA frame needs {required}.",
                nameof(destination));
        }

        using SKImage image = RenderToImageCore(outputSize, layers, includeBackground, overlay, debugOverlay);
        var info = new SKImageInfo(outputSize.Width, outputSize.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        int byteCount = (int)required;

        //Read through a pinned pooled buffer, then copy: the shared sources compile without
        //  unsafe code, and a Span cannot be pinned without it
        byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, 0, 0))
            {
                throw new InvalidOperationException("The rendered frame's pixels could not be read.");
            }
            new ReadOnlySpan<byte>(buffer, 0, byteCount).CopyTo(destination);
        }
        finally
        {
            handle.Free();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private SKImage RenderToImageCore(SKSizeI outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground,
        SafeAreaOverlay overlay, DebugOverlay debugOverlay)
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

            byte groupAlpha = ScaleAlpha(255, _opacity);
            if (groupAlpha > 0)
            {
                bool fading = groupAlpha < 255;
                if (fading)
                {
                    using var groupPaint = new SKPaint { Color = SKColors.White.WithAlpha(groupAlpha) };
                    canvas.SaveLayer(groupPaint);
                }

                foreach (DrawingLayer layer in layers)
                {
                    if (!layer.IsVisible) { continue; }
                    byte layerAlpha = ScaleAlpha(LayerOpacity, layer.Opacity);
                    if (layerAlpha == 0) { continue; }
                    using SKBitmap layerBitmap = RenderLayerBitmap(layer, outputSize, drawingRect);
                    if (layerBitmap != null)
                    {
                        using var compositePaint = new SKPaint
                        {
                            Color = SKColors.White.WithAlpha(layerAlpha),
                        };
                        canvas.DrawBitmap(layerBitmap, new SKRect(0, 0, outputSize.Width, outputSize.Height),
                            SKSamplingOptions.Default, compositePaint);
                    }
                }

                if (fading) { canvas.Restore(); }
            }

            overlay?.Draw(canvas, new SizeF(outputSize.Width, outputSize.Height), _time);
            debugOverlay?.Draw(canvas, new SizeF(outputSize.Width, outputSize.Height), _time);

            return surface.Snapshot();
        }
    }

    //An 8-bit alpha scaled by a 0..1 factor; a factor of 1 returns the alpha unchanged, so the
    //  default (unfaded) path composites with exactly the values it always has
    internal static byte ScaleAlpha(byte alpha, float factor)
        => factor >= 1f ? alpha : (byte)Math.Round(alpha * factor, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Renders the layers - exactly as <see cref="RenderToPixels(Span{byte}, Size, IReadOnlyList{DrawingLayer}, bool)"/>
    /// would - into a new float image of the given size, for further work with the
    /// <see cref="DrawingPlaneImage"/> tools. Straight (not premultiplied) alpha, like the
    /// byte frame it is read from: each channel is <c>byte / 255f</c>, so
    /// <see cref="DrawingPlaneImage.ToRgba(Span{byte})"/> gives back exactly the bytes
    /// <c>RenderToPixels</c> writes.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the background fill and image render behind the
    /// layers; when <c>false</c>, the layers render over transparency and the image's
    /// <see cref="DrawingPlaneImage.A"/> plane carries it.
    /// </param>
    /// <returns>A new image of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public DrawingPlaneImage RenderToPlaneImage(Size outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
    {
        if (outputSize.Width < 1 || outputSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(outputSize)); }
        if (layers == null) { throw new ArgumentNullException(nameof(layers)); }
        var image = new DrawingPlaneImage(outputSize);
        RenderToPlaneImage(image, layers, includeBackground);
        return image;
    }

    /// <summary>
    /// Renders the layers into an existing float image, overwriting all four of its planes -
    /// the allocation-free form of <see cref="RenderToPlaneImage(Size, IReadOnlyList{DrawingLayer}, bool)"/>
    /// for a frame loop. The frame is rendered at the destination's own size.
    /// </summary>
    /// <param name="destination">The image to overwrite; its size is the frame size.</param>
    /// <param name="layers">The layers to render, bottom-most first.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> or <paramref name="layers"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public void RenderToPlaneImage(DrawingPlaneImage destination, IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        if (layers == null) { throw new ArgumentNullException(nameof(layers)); }
        var size = new SKSizeI(destination.Width, destination.Height);
        int byteCount = checked(destination.Width * destination.Height * 4);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            RenderToPixelsCore(new Span<byte>(buffer, 0, byteCount), size, layers, includeBackground, null, null);
            destination.CopyFromRgba(new ReadOnlySpan<byte>(buffer, 0, byteCount));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Renders the COVERAGE of the layers' content as a mask: one <see cref="DrawingPlane"/>
    /// of values 0..1, where 1 means fully covered by drawn ink and 0 means untouched - a
    /// shape's mask to multiply by, blend a colour through or composite with. Each layer
    /// contributes the alpha of its own ink exactly as it is drawn into the layer's private
    /// bitmap before compositing (anti-aliased edges, an element color's own alpha and a
    /// fill's stop alpha all count), so <see cref="LayerOpacity"/>,
    /// <see cref="DrawingLayer.Opacity"/> and <see cref="Opacity"/> do NOT reduce it and the
    /// background is never part of it. Layers combine as a union, bottom to top:
    /// <c>m = m + a * (1 - m)</c> (a fully covered pixel is exactly 1). The drawing occupies
    /// its centered aspect-fit rectangle, exactly as in an export.
    /// </summary>
    /// <param name="outputSize">The pixel size of the mask to produce.</param>
    /// <param name="layers">The layers whose coverage to combine; one layer for a single layer's mask.</param>
    /// <returns>A new plane of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layers"/> contains a null entry.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public DrawingPlane RenderToMask(Size outputSize, IReadOnlyList<DrawingLayer> layers)
        => RenderToMask(outputSize, layers, MaskMeaning.Coverage);

    /// <summary>
    /// Renders a mask of the layers with an explicit choice of what it measures:
    /// <see cref="MaskMeaning.Coverage"/> - where the ink is, exactly as
    /// <see cref="RenderToMask(Size, IReadOnlyList{DrawingLayer})"/> (layer and renderer opacities
    /// ignored); <see cref="MaskMeaning.VisibleAlpha"/> - the alpha of the frame as
    /// <see cref="RenderToPixels(Span{byte}, Size, IReadOnlyList{DrawingLayer}, bool)"/> exports it
    /// without the background (every opacity, time window, mask and effect counts), each value
    /// that alpha byte divided by 255; or <see cref="MaskMeaning.HardEdged"/> - where the
    /// elements are, 0 or 1 per pixel by the pixel-centre rule, computed from their geometry in
    /// shared code (identical in both packages).
    /// </summary>
    /// <param name="outputSize">The pixel size of the mask to produce.</param>
    /// <param name="layers">The layers to measure; one layer for a single layer's mask.</param>
    /// <param name="meaning">Coverage, visible alpha or hard-edged coverage.</param>
    /// <returns>A new plane of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1, or <paramref name="meaning"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layers"/> contains a null entry.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public DrawingPlane RenderToMask(Size outputSize, IReadOnlyList<DrawingLayer> layers, MaskMeaning meaning)
    {
        if (outputSize.Width < 1 || outputSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(outputSize)); }
        CheckMaskArguments(layers, meaning);
        var mask = new DrawingPlane(outputSize);
        RenderToMaskCore(mask, layers, meaning);
        return mask;
    }

    /// <summary>
    /// Renders a mask of the layers into an existing plane at that plane's size, overwriting
    /// every value - the allocation-free form of
    /// <see cref="RenderToMask(Size, IReadOnlyList{DrawingLayer}, MaskMeaning)"/> for a frame loop.
    /// </summary>
    /// <param name="destination">The plane to overwrite; its size is the frame size.</param>
    /// <param name="layers">The layers to measure.</param>
    /// <param name="meaning">Coverage (the default), visible alpha or hard-edged coverage.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> or <paramref name="layers"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layers"/> contains a null entry.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="meaning"/> is undefined.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public void RenderToMask(DrawingPlane destination, IReadOnlyList<DrawingLayer> layers, MaskMeaning meaning = MaskMeaning.Coverage)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        CheckMaskArguments(layers, meaning);
        RenderToMaskCore(destination, layers, meaning);
    }

    private static void CheckMaskArguments(IReadOnlyList<DrawingLayer> layers, MaskMeaning meaning)
    {
        if (layers == null) { throw new ArgumentNullException(nameof(layers)); }
        foreach (DrawingLayer layer in layers)
        {
            if (layer == null) { throw new ArgumentException("The layer list contains a null entry.", nameof(layers)); }
        }
        if (!Enum.IsDefined(meaning)) { throw new ArgumentOutOfRangeException(nameof(meaning)); }
    }

    private void RenderToMaskCore(DrawingPlane mask, IReadOnlyList<DrawingLayer> layers, MaskMeaning meaning)
    {
        ThrowIfDisposed();
        float[] m = mask.Data;
        var size = new SKSizeI(mask.Width, mask.Height);
        if (meaning == MaskMeaning.VisibleAlpha)
        {
            int pixels = checked(mask.Width * mask.Height);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(pixels * 4);
            try
            {
                RenderToPixelsCore(new Span<byte>(buffer, 0, pixels * 4), size, layers, false, null, null);
                for (int i = 0; i < pixels; i++) { m[i] = buffer[(i * 4) + 3] / 255f; }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            return;
        }

        Array.Clear(m);
        if (meaning == MaskMeaning.HardEdged)
        {
            int width = mask.Width;
            lock (_renderLocker)
            {
                var areas = new List<CoverageCollector.Area>();
                foreach (DrawingLayer layer in layers)
                {
                    if (!layer.IsVisible) { continue; }
                    HardEdgeMatrix parent = GetCoverageLayerMatrix(layer, size);
                    foreach (DrawingElement element in layer.GetElements()) { CoverageCollector.Collect(element, parent, _time, areas); }
                }
                CoverageCollector.Rasterize(areas, width, mask.Height, (y, x0, x1) => m.AsSpan((y * width) + x0, x1 - x0).Fill(1f));
            }
            return;
        }

        lock (_renderLocker)
        {
            SKRect drawingRect = CanvasCalibration.GetDrawingRect(size, _calibrationSize);
            foreach (DrawingLayer layer in layers)
            {
                if (!layer.IsVisible) { continue; }
                using SKBitmap bitmap = RenderLayerBitmap(layer, size, drawingRect);
                if (bitmap == null) { continue; }
                byte[] bytes = bitmap.Bytes;
                int rowBytes = bitmap.RowBytes;
                for (int y = 0; y < size.Height; y++)
                {
                    int row = y * size.Width;
                    int offset = (y * rowBytes) + 3;
                    for (int x = 0; x < size.Width; x++, offset += 4)
                    {
                        byte alpha = bytes[offset];
                        if (alpha == 0) { continue; }
                        float current = m[row + x];
                        m[row + x] = alpha == 255 ? 1f : Math.Min(1f, current + ((alpha / 255f) * (1f - current)));
                    }
                }
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
    public SKImage RenderToImage(Size outputSize, IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
        => RenderToImage(GraphicsInterop.ToGraphics(outputSize), layers, includeBackground);

    /// <summary>
    /// Builds the OWNERSHIP MAP of the layers' elements at a pixel size: for every pixel, which
    /// element owns it - the last one, in painting order, whose area covers the pixel's centre
    /// (the hard-edged rule of <see cref="MaskMeaning.HardEdged"/>, computed from geometry in
    /// shared code, so both packages give identical maps). Elements are numbered in painting
    /// order across the listed layers (the first layer's elements first, each layer in its own
    /// order), every element on those layers counting - an element hidden at the render time
    /// keeps its number and owns nothing. A group or an animated shape is ONE element (its
    /// children paint into its number). Pixels no element covers hold
    /// <see cref="DrawingOwnershipMap.Nobody"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the map.</param>
    /// <param name="layers">The layers whose elements own pixels, bottom-most first.</param>
    /// <returns>A new map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layers"/> contains a null entry.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the renderer has been disposed.</exception>
    public DrawingOwnershipMap RenderToOwnershipMap(Size outputSize, IReadOnlyList<DrawingLayer> layers)
    {
        if (outputSize.Width < 1 || outputSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(outputSize)); }
        CheckMaskArguments(layers, MaskMeaning.HardEdged);
        ThrowIfDisposed();
        int width = outputSize.Width, height = outputSize.Height;
        int[] owners = new int[checked(width * height)];
        Array.Fill(owners, DrawingOwnershipMap.Nobody);
        var elements = new List<DrawingElement>();
        var elementLayers = new List<DrawingLayer>();
        var matrices = new List<HardEdgeMatrix>();
        var size = new SKSizeI(width, height);
        lock (_renderLocker)
        {
            var areas = new List<CoverageCollector.Area>();
            foreach (DrawingLayer layer in layers)
            {
                if (!layer.IsVisible) { continue; }
                HardEdgeMatrix parent = GetCoverageLayerMatrix(layer, size);
                foreach (DrawingElement element in layer.GetElements())
                {
                    int index = elements.Count;
                    elements.Add(element);
                    elementLayers.Add(layer);
                    matrices.Add(CoverageCollector.OwnMatrix(element, parent));
                    areas.Clear();
                    CoverageCollector.Collect(element, parent, _time, areas);
                    CoverageCollector.Rasterize(areas, width, height, (y, x0, x1) => owners.AsSpan((y * width) + x0, x1 - x0).Fill(index));
                }
            }
        }
        return new DrawingOwnershipMap(width, height, owners, elements.ToArray(), elementLayers.ToArray(), matrices.ToArray());
    }

    //The map from a layer's element coordinates to an output of this size, for hard-edged work:
    //  the layer's view, then the same fit the exports draw with
    private HardEdgeMatrix GetCoverageLayerMatrix(DrawingLayer layer, SKSizeI size)
    {
        SKRect drawingRect = CanvasCalibration.GetDrawingRect(size, _calibrationSize);
        float scale = drawingRect.Width / _calibrationSize.Width;
        return CoverageCollector.LayerMatrix(GetLayerViewMatrix(layer), scale, drawingRect.Left, drawingRect.Top);
    }

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
            || _sceneOpacity != _opacity
            || _sceneLayers.Count != layers.Count)
        {
            return true;
        }

        for (int i = 0; i < layers.Count; i++)
        {
            SceneLayerState state = _sceneLayers[i];
            DrawingLayer layer = layers[i];
            if (!ReferenceEquals(state.Layer, layer) || state.Visible != layer.IsVisible) { return true; }
            if (!layer.IsVisible) { continue; }
            if (state.ResetVersion != layer.ResetVersion
                || state.Opacity != layer.Opacity
                || state.ElementCount != _layerCaches[layer].DrawnElementCount
                || state.ContentVersion != _layerCaches[layer].ContentVersion)
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

        byte groupAlpha = ScaleAlpha(255, _opacity);
        if (groupAlpha > 0)
        {
            bool fading = groupAlpha < 255;
            if (fading)
            {
                using var groupPaint = new SKPaint { Color = SKColors.White.WithAlpha(groupAlpha) };
                sceneCanvas.SaveLayer(groupPaint);
            }

            foreach (DrawingLayer layer in layers)
            {
                if (!layer.IsVisible) { continue; }
                LayerCache cache = _layerCaches[layer];
                byte layerAlpha = ScaleAlpha(LayerOpacity, layer.Opacity);
                if (cache.DrawnElementCount > 0 && layerAlpha > 0)
                {
                    using var compositePaint = new SKPaint
                    {
                        Color = SKColors.White.WithAlpha(layerAlpha),
                    };
                    sceneCanvas.DrawBitmap(cache.Bitmap, cacheDest, SKSamplingOptions.Default, compositePaint);
                }
            }

            if (fading) { sceneCanvas.Restore(); }
        }

        //Record what the scene was built from, to detect staleness on later renders
        _sceneFillColor = _backgroundFillColor;
        _sceneClearColor = _surfaceClearColor;
        _sceneLayerOpacity = LayerOpacity;
        _sceneOpacity = _opacity;
        _sceneLayers.Clear();
        foreach (DrawingLayer layer in layers)
        {
            if (!layer.IsVisible)
            {
                _sceneLayers.Add(new SceneLayerState { Layer = layer, Visible = false });
                continue;
            }
            _sceneLayers.Add(new SceneLayerState
            {
                Layer = layer,
                Visible = true,
                ResetVersion = layer.ResetVersion,
                ElementCount = _layerCaches[layer].DrawnElementCount,
                ContentVersion = _layerCaches[layer].ContentVersion,
                Opacity = layer.Opacity,
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

        bool hasView = TryGetLayerView(layer, out Matrix3x2 view);
        bool needsFullRedraw = cache.Bitmap == null
            || cache.Bitmap.Width != cacheSize.Width
            || cache.Bitmap.Height != cacheSize.Height
            || cache.ResetVersion != layer.ResetVersion
            || cache.HasView != hasView
            || (hasView && cache.View != view);

        DrawingElement[] elements = layer.GetElements();

        //A masked layer, or one with effects, is post-processed as a whole: it is redrawn whole
        //  whenever anything that goes into it changes, and otherwise kept as it is
        if (layer.HasPostProcessing)
        {
            long stamp = GetPostProcessStamp(layer, elements);
            if (!needsFullRedraw && cache.PostProcessed && cache.PostStamp == stamp) { return; }
            LayerFullRedrawCount++;
            cache.Dispose();
            cache.Bitmap = DrawLayerBitmap(layer, elements, cacheSize, cacheRect, hasView);
            cache.DrawnElementCount = elements.Length;
            cache.ResetVersion = layer.ResetVersion;
            cache.HasView = hasView;
            cache.View = view;
            cache.States = null;
            cache.PostProcessed = true;
            cache.PostStamp = stamp;
            cache.ContentVersion++;
            return;
        }
        if (cache.PostProcessed) { needsFullRedraw = true; }

        if (!needsFullRedraw && cache.DrawnElementCount > 0)
        {
            needsFullRedraw = !TryKeepProcessedPrefix(cache, elements);
        }

        if (needsFullRedraw)
        {
            LayerFullRedrawCount++;
            cache.Dispose();
            cache.Bitmap = new SKBitmap(cacheSize.Width, cacheSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            cache.DrawnElementCount = 0;
            cache.ResetVersion = layer.ResetVersion;
            cache.HasView = hasView;
            cache.View = view;
            cache.States = null;
            cache.PostProcessed = false;
            cache.ContentVersion++;

            using var clearCanvas = new SKCanvas(cache.Bitmap);
            clearCanvas.Clear(SKColors.Transparent);
        }

        if (elements.Length > cache.DrawnElementCount)
        {
            bool drew = false;
            using var cacheCanvas = new SKCanvas(cache.Bitmap);
            PaintContext context = CreatePaintContext(layer, cacheSize.Width, cacheSize.Height, cacheRect.Width / _calibrationSize.Width);
            for (int i = cache.DrawnElementCount; i < elements.Length; i++)
            {
                DrawingElement element = elements[i];
                bool dynamic = element.IsDynamic;
                bool visible = !dynamic || element.IsVisibleAt(_time);
                if (visible)
                {
                    DrawElement(cacheCanvas, element, cacheRect, layer, hasView, context);
                    drew = true;
                }

                if (dynamic && cache.States == null) { cache.States = new ElementState[elements.Length]; }
                if (cache.States != null)
                {
                    if (cache.States.Length < elements.Length) { Array.Resize(ref cache.States, elements.Length); }
                    cache.States[i] = dynamic
                        ? new ElementState { Tracked = true, Visible = visible, Stamp = visible ? element.GetRenderStamp(_time) : 0 }
                        : default;
                }
            }
            cache.DrawnElementCount = elements.Length;
            if (drew) { cache.ContentVersion++; }
        }
    }

    //Checks the part of the layer the cache already reflects against what it would draw now.
    //  Returns true when the bitmap still holds the right pixels for that part - possibly after
    //  forgetting a tail of entries that drew nothing (they are then processed again as new);
    //  false when something already drawn has changed and the layer needs a full redraw
    private bool TryKeepProcessedPrefix(LayerCache cache, DrawingElement[] elements)
    {
        int processed = Math.Min(cache.DrawnElementCount, elements.Length);
        ElementState[] states = cache.States;
        int firstChange = -1;
        for (int i = 0; i < processed; i++)
        {
            DrawingElement element = elements[i];
            if (!element.IsDynamic) { continue; }
            if (states == null || i >= states.Length || !states[i].Tracked)
            {
                //It became dynamic after it was drawn: what it looked like then is unknown
                firstChange = i;
                break;
            }

            bool visible = element.IsVisibleAt(_time);
            if (visible != states[i].Visible || (visible && element.GetRenderStamp(_time) != states[i].Stamp))
            {
                firstChange = i;
                break;
            }
        }
        if (firstChange < 0) { return true; }

        //Only a change among entries that drew nothing can be absorbed: forget them, draw on
        for (int i = firstChange; i < processed; i++)
        {
            if (states == null || i >= states.Length || !states[i].Tracked || states[i].Visible) { return false; }
        }
        cache.DrawnElementCount = firstChange;
        return true;
    }

    private SKBitmap RenderLayerBitmap(DrawingLayer layer, SKSizeI size, SKRect drawingRect)
    {
        DrawingElement[] elements = layer.GetElements();
        if (elements.Length < 1) { return null; }

        bool hasView = TryGetLayerView(layer, out _);
        return DrawLayerBitmap(layer, elements, size, drawingRect, hasView);
    }

    //A layer's private bitmap: every element that exists at the render time, then the layer's
    //  mask and effects
    private SKBitmap DrawLayerBitmap(DrawingLayer layer, DrawingElement[] elements, SKSizeI size, SKRect drawingRect, bool hasView)
    {
        var bitmap = new SKBitmap(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        float scale = drawingRect.Width / _calibrationSize.Width;
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            PaintContext context = CreatePaintContext(layer, size.Width, size.Height, scale);
            foreach (DrawingElement element in elements)
            {
                if (element.IsDynamic && !element.IsVisibleAt(_time)) { continue; }
                DrawElement(canvas, element, drawingRect, layer, hasView, context);
            }

            DrawingMask mask = layer.Mask;
            if (mask != null)
            {
                canvas.Save();
                canvas.Translate(drawingRect.Left, drawingRect.Top);
                canvas.Scale(scale);
                if (hasView) { ApplyLayerView(canvas, layer); }
                mask.Apply(canvas, _time);
                canvas.Restore();
            }
        }

        DrawingEffects effects = layer.Effects;
        if (effects != null && !effects.IsEmpty)
        {
            Matrix3x2 device = layer.Transform.ToMatrix() * GetCameraMatrix(layer) * Matrix3x2.CreateScale(scale);
            SKBitmap processed = EffectProcessor.ApplyToLayer(bitmap, effects, device, GetSceneRotation(layer));
            bitmap.Dispose();
            bitmap = processed;
        }
        return bitmap;
    }

    //Everything a post-processed layer's pixels depend on, as one state value: its elements
    //  (count, reset version, each dynamic element's visibility and state), its mask (with a mask
    //  layer's own content) and its effects
    private long GetPostProcessStamp(DrawingLayer layer, DrawingElement[] elements)
    {
        ulong hash = DrawingElement.MixStamp(0x13198A2E03707344UL, (ulong)elements.Length);
        hash = DrawingElement.MixStamp(hash, (ulong)layer.ResetVersion);
        foreach (DrawingElement element in elements)
        {
            if (!element.IsDynamic) { continue; }
            bool visible = element.IsVisibleAt(_time);
            hash = DrawingElement.MixStamp(hash, visible ? 1UL : 2UL);
            if (visible) { hash = DrawingElement.MixStamp(hash, (ulong)element.GetRenderStamp(_time)); }
        }
        DrawingMask mask = layer.Mask;
        hash = DrawingElement.MixStamp(hash, mask == null ? 0UL : (ulong)mask.GetStamp(_time));
        DrawingEffects effects = layer.Effects;
        hash = DrawingElement.MixStamp(hash, effects == null ? 0UL : effects.GetStateHash());
        return (long)hash;
    }

    private void DrawElement(SKCanvas canvas, DrawingElement element, SKRect drawingRect, DrawingLayer layer, bool hasView,
        PaintContext context)
    {
        //A plain element on a layer without a view takes the original drawing code exactly,
        //  so drawings that use none of the animation model keep their pixels
        if (!hasView && !NeedsCalibratedPath(element))
        {
            if (element is Stroke stroke)
            {
                DrawStroke(canvas, stroke, drawingRect, layer.ColorValue);
            }
            else if (element is DrawingShape shape)
            {
                DrawShape(canvas, shape, drawingRect, shape.ColorValue ?? layer.ColorValue);
            }
            return;
        }

        float scale = drawingRect.Width / _calibrationSize.Width;
        canvas.Save();
        canvas.Translate(drawingRect.Left, drawingRect.Top);
        canvas.Scale(scale);
        if (hasView) { ApplyLayerView(canvas, layer); }
        ElementPainter.Draw(canvas, element, layer.ColorValue, _time, context);
        canvas.Restore();
    }

    private static bool NeedsCalibratedPath(DrawingElement element)
        => element.IsDynamic
            && (!element.Transform.IsIdentity || element.Opacity < 1f || element is DrawingGroup || element is AnimatedShape
                || element is SpriteShape || element.HasEffects);

    //The layer's view - the camera as this layer feels it, then the layer's own transform -
    //  as one matrix in calibrated units; false (and the identity) when it has none
    private bool TryGetLayerView(DrawingLayer layer, out Matrix3x2 view)
    {
        view = Matrix3x2.Identity;
        DrawingCamera camera = Camera;
        bool hasCamera = camera != null && camera.TryGetEffective(_calibrationSize.Width, _calibrationSize.Height,
            layer.Parallax, out _, out _, out _, out _);
        DrawingTransform transform = layer.Transform;
        if (!hasCamera && transform.IsIdentity) { return false; }

        Matrix3x2 cameraMatrix = hasCamera
            ? camera.GetMatrix(GraphicsInterop.ToImaging(_calibrationSize), layer.Parallax)
            : Matrix3x2.Identity;
        view = transform.ToMatrix() * cameraMatrix;
        return true;
    }

    //The paint context for one layer's bitmap: device size, calibration scale and the scene's turn
    private PaintContext CreatePaintContext(DrawingLayer layer, int width, int height, float scale)
        => new PaintContext(width, height, scale, GetSceneRotation(layer));

    //How the camera, as this layer feels it, turns the scene (its roll; the identity without one)
    private Matrix3x2 GetSceneRotation(DrawingLayer layer)
    {
        DrawingCamera camera = Camera;
        if (camera == null
            || !camera.TryGetEffective(_calibrationSize.Width, _calibrationSize.Height, layer.Parallax, out _, out _, out _, out float roll)
            || roll == 0f)
        {
            return Matrix3x2.Identity;
        }
        return Matrix3x2.CreateRotation(-roll * MathF.PI / 180f);
    }

    //The camera's matrix for this layer (the identity without one)
    private Matrix3x2 GetCameraMatrix(DrawingLayer layer)
        => Camera == null ? Matrix3x2.Identity : Camera.GetMatrix(GraphicsInterop.ToImaging(_calibrationSize), layer.Parallax);

    //Applies the layer's view to a canvas that draws in calibrated units: the camera first
    //  (outermost), then the layer's transform
    private void ApplyLayerView(SKCanvas canvas, DrawingLayer layer)
    {
        Camera?.ApplyTo(canvas, _calibrationSize.Width, _calibrationSize.Height, layer.Parallax);
        layer.Transform.ApplyTo(canvas);
    }

    //The layer's view as a matrix, for measuring (the identity when it has none)
    internal Matrix3x2 GetLayerViewMatrix(DrawingLayer layer)
    {
        TryGetLayerView(layer, out Matrix3x2 view);
        return view;
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
