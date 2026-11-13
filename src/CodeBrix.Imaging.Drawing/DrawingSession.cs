using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using CodeBrix.Imaging.Drawing.Captions;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// The main entry point of the CodeBrix.Imaging.Drawing library: an interactive drawing
/// surface model that turns pointer (mouse, pen, or touch) events into calibrated strokes
/// on named, colored <see cref="DrawingLayer"/> collections, renders them onto any
/// <see cref="SKCanvas"/> with translucent "highlighter" compositing, and exports the finished
/// drawing as an image. The session is UI-framework-agnostic: the hosting view forwards
/// its pointer events and paint callbacks, and the session raises
/// <see cref="RedrawRequested"/> whenever the view should invalidate its canvas.
/// </summary>
public sealed class DrawingSession : IDisposable
{
    private readonly object _layersLocker = new object();
    private readonly List<DrawingLayer> _layers = new List<DrawingLayer>();
    private readonly List<DrawingLayer> _strokeOrder = new List<DrawingLayer>();

    private readonly DrawingRenderer _renderer;

    private Stroke _activeStroke;
    private Stopwatch _activeStrokeTimer;
    private DrawingLayer _activeStrokeLayer;

    private SKBitmap _ownedBackgroundImage;

    private readonly DrawingAnimations _animations = new DrawingAnimations();

    //The session's renderer, for tests that look at its caches
    internal DrawingRenderer Renderer => _renderer;

    /// <summary>
    /// The size of the calibrated drawing space that all of the session's strokes are
    /// expressed in.
    /// </summary>
    public Size CalibrationSize => _renderer.CalibrationSize;

    //The name-neutral accessor that this class's own code reads, so the public accessor
    //  can carry a different name in each package without an #if at any call site
    private SKSizeI CalibrationSizeValue => _renderer.CalibrationSizeValue;

#if NOSKIA
    /// <summary>Gets <see cref="CalibrationSize"/> as a <see cref="DrawingSizeI"/>.</summary>
    /// <returns>The calibration size as a backend size.</returns>
    public DrawingSizeI GetCalibrationSizeAsDrawing() => _renderer.CalibrationSizeValue;
#else
    /// <summary>Gets <see cref="CalibrationSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The calibration size as a SkiaSharp size.</returns>
    public SKSizeI GetCalibrationSizeAsSkia() => _renderer.CalibrationSizeValue;
#endif

    /// <summary>
    /// The layers of the drawing, bottom-most first, as configured via <see cref="AddLayer(string, Color)"/>.
    /// </summary>
    public IReadOnlyList<DrawingLayer> Layers
    {
        get
        {
            lock (_layersLocker) { return _layers.ToArray(); }
        }
    }

    /// <summary>
    /// The layer that newly drawn strokes are committed to. Set automatically to the first
    /// layer added; change it to switch "ink colors" (for example from a Pain layer to a
    /// Numbness layer).
    /// </summary>
    public DrawingLayer ActiveLayer { get; set; }

    /// <summary>
    /// The width of newly drawn strokes, in calibrated drawing units.
    /// </summary>
    public float StrokeWidth { get; set; }

    /// <summary>
    /// An optional background image drawn behind the layers - for example a body-map line
    /// diagram. Assigning an image here does NOT transfer ownership (the caller disposes
    /// it); alternatively use <see cref="SetBackgroundImage(byte[])"/> to have the session
    /// decode and own the image.
    /// </summary>
    public SKBitmap BackgroundImage
    {
        get => _renderer.BackgroundImage;
        set
        {
            DisposeOwnedBackground();
            _renderer.BackgroundImage = value;
            RaiseRedrawRequested();
        }
    }

    /// <summary>
    /// The alpha (0-255) that completed layers are composited with; 100 (the default)
    /// produces the translucent highlighter effect, 255 paints fully opaque.
    /// </summary>
    public byte LayerOpacity
    {
        get => _renderer.LayerOpacity;
        set => _renderer.LayerOpacity = value;
    }

    /// <summary>
    /// The alpha (0-255) that the in-progress stroke is drawn with.
    /// </summary>
    public byte ActiveStrokeOpacity
    {
        get => _renderer.ActiveStrokeOpacity;
        set => _renderer.ActiveStrokeOpacity = value;
    }

    /// <summary>
    /// The opacity, 0..1, of the whole drawing: every layer, composited together as one
    /// group, faded over the background fill and image (which are not faded) - a
    /// whole-frame fade in or out. Over an opaque <see cref="BackgroundFillColor"/> that is
    /// a fade from (or to) that color, and exported frames stay opaque. The default of 1
    /// renders exactly as before; <see cref="LayerOpacity"/> keeps its meaning, and each
    /// layer's own <see cref="DrawingLayer.Opacity"/> multiplies in as well. Setting it
    /// raises <see cref="RedrawRequested"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or a value outside 0..1.</exception>
    public float Opacity
    {
        get => _renderer.Opacity;
        set
        {
            if (_renderer.Opacity == value) { return; }
            _renderer.Opacity = value;
            RaiseRedrawRequested();
        }
    }

    /// <summary>
    /// The fonts this session's text uses (see <see cref="DrawText(string, float, float, float, Color?, TextAlign, float, string)"/>): an empty registry of
    /// the session's own unless <see cref="DrawingSessionOptions.Fonts"/> supplied one.
    /// Register at least one font file before drawing text - system fonts are never used.
    /// </summary>
    public FontRegistry Fonts { get; }

    /// <summary>
    /// A safe-area mask drawn over the live <see cref="Render(SKCanvas, SKImageInfo, bool)"/>
    /// output - a preview aid; or <c>null</c> (the default) for none. Exports never include
    /// it: to bake a mask into an exported still, pass an overlay to the export overloads
    /// that take one. Setting it raises <see cref="RedrawRequested"/>.
    /// </summary>
    public SafeAreaOverlay SafeAreaOverlay
    {
        get => _renderer.SafeAreaOverlay;
        set
        {
            _renderer.SafeAreaOverlay = value;
            RaiseRedrawRequested();
        }
    }

    /// <summary>
    /// A debugging read-out - timecode, frame number and (optionally) the active cue names -
    /// drawn over the live <see cref="Render(SKCanvas, SKImageInfo, bool)"/> output on top of
    /// everything; or <c>null</c> (the default) for none. Exports never include it: to bake
    /// one into a still, pass it to the export overloads that take a
    /// <see cref="Rendering.DebugOverlay"/>. Setting it raises <see cref="RedrawRequested"/>.
    /// </summary>
    public DebugOverlay DebugOverlay
    {
        get => _renderer.DebugOverlay;
        set
        {
            _renderer.DebugOverlay = value;
            RaiseRedrawRequested();
        }
    }

    /// <summary>
    /// The scene time, seconds (0 by default). SETTING it shows the scene at that moment: every
    /// binding in <see cref="Animations"/> is applied (in the order bound), every
    /// <see cref="AnimatedShape"/> on the layers (and inside groups) is rebuilt for the time,
    /// and from then on elements exist only inside their time windows
    /// (<see cref="DrawingElement.StartTime"/>, <see cref="DrawingElement.EndTime"/>) - in the
    /// live render and in every export. Setting the same time again re-applies everything.
    /// Seeking backwards needs no rebuilding. Raises <see cref="RedrawRequested"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not finite.</exception>
    public double Time
    {
        get => _renderer.Time;
        set
        {
            if (!Double.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            ThrowIfDisposed();
            _renderer.Time = value;
            _animations.Apply(value);
            foreach (DrawingLayer layer in Layers)
            {
                foreach (DrawingElement element in layer.GetElements())
                {
                    UpdateAnimatedShapes(element, value, 0);
                }
            }
            RaiseRedrawRequested();
        }
    }

    /// <summary>
    /// The keyframe tracks and time callbacks bound to this scene's elements, groups, layers
    /// and camera; applied whenever <see cref="Time"/> is set. Empty at first.
    /// </summary>
    public DrawingAnimations Animations => _animations;

    /// <summary>
    /// A view over the whole scene - pan, zoom and roll, see <see cref="DrawingCamera"/> -
    /// applied while drawing, live and in exports; or <c>null</c> (the default) for none. Each
    /// layer follows it by its <see cref="DrawingLayer.Parallax"/> (0 = fixed to the frame).
    /// Setting it raises <see cref="RedrawRequested"/>; changing the camera's own properties
    /// does not (the next render picks them up).
    /// </summary>
    public DrawingCamera Camera
    {
        get => _renderer.Camera;
        set
        {
            _renderer.Camera = value;
            RaiseRedrawRequested();
        }
    }

    /// <summary>
    /// The color that the drawing rectangle is filled with before the background image and
    /// layers are drawn.
    /// </summary>
    public Color BackgroundFillColor
    {
        get => _renderer.BackgroundFillColor;
        set => _renderer.BackgroundFillColor = value;
    }

    /// <summary>Sets <see cref="BackgroundFillColor"/> from a <see cref="SKColor"/>.</summary>
    /// <param name="color">The background fill color.</param>
    public void SetBackgroundFillColor(SKColor color) => _renderer.SetBackgroundFillColor(color);

#if NOSKIA
    /// <summary>Gets <see cref="BackgroundFillColor"/> as a <see cref="DrawingColor"/>.</summary>
    /// <returns>The background fill color as a backend color.</returns>
    public DrawingColor GetBackgroundFillColorAsDrawing() => _renderer.BackgroundFillColorValue;
#else
    /// <summary>Gets <see cref="BackgroundFillColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The background fill color as a SkiaSharp color.</returns>
    public SKColor GetBackgroundFillColorAsSkia() => _renderer.BackgroundFillColorValue;
#endif

    /// <summary>
    /// The color that the whole canvas is cleared to at the start of every render.
    /// </summary>
    public Color SurfaceClearColor
    {
        get => _renderer.SurfaceClearColor;
        set => _renderer.SurfaceClearColor = value;
    }

    /// <summary>Sets <see cref="SurfaceClearColor"/> from a <see cref="SKColor"/>.</summary>
    /// <param name="color">The surface clear color.</param>
    public void SetSurfaceClearColor(SKColor color) => _renderer.SetSurfaceClearColor(color);

#if NOSKIA
    /// <summary>Gets <see cref="SurfaceClearColor"/> as a <see cref="DrawingColor"/>.</summary>
    /// <returns>The surface clear color as a backend color.</returns>
    public DrawingColor GetSurfaceClearColorAsDrawing() => _renderer.SurfaceClearColorValue;
#else
    /// <summary>Gets <see cref="SurfaceClearColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The surface clear color as a SkiaSharp color.</returns>
    public SKColor GetSurfaceClearColorAsSkia() => _renderer.SurfaceClearColorValue;
#endif

    /// <summary>
    /// Indicates whether any layer currently holds at least one completed element (stroke or shape).
    /// </summary>
    public bool HasStrokes
    {
        get
        {
            lock (_layersLocker)
            {
                foreach (DrawingLayer layer in _layers)
                {
                    if (layer.ElementCount > 0) { return true; }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// The total number of completed elements (strokes and shapes) across all layers.
    /// </summary>
    public int StrokeCount
    {
        get
        {
            var count = 0;
            lock (_layersLocker)
            {
                foreach (DrawingLayer layer in _layers)
                {
                    count += layer.ElementCount;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Indicates whether a stroke is currently in progress (the pointer is down).
    /// </summary>
    public bool IsPointerActive => _activeStroke != null;

    /// <summary>
    /// Indicates whether this session has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Raised whenever the drawing's appearance has changed and the hosting view should
    /// invalidate (repaint) its canvas. May be raised on any thread; hosting views should
    /// marshal to their UI thread as needed.
    /// </summary>
    public event EventHandler RedrawRequested;

    /// <summary>
    /// Raised whenever the set of completed elements changes - a stroke or shape was
    /// committed, undone, or the drawing was cleared. Useful for re-evaluating Save/Clear
    /// command availability.
    /// </summary>
    public event EventHandler DrawingChanged;

    /// <summary>
    /// Creates a new drawing session.
    /// </summary>
    /// <param name="options">Initial settings; when omitted, defaults are used (see <see cref="DrawingSessionOptions"/>).</param>
    public DrawingSession(DrawingSessionOptions options = null)
    {
        options = options ?? new DrawingSessionOptions();

        _renderer = new DrawingRenderer(options.CalibrationSize)
        {
            LayerOpacity = options.LayerOpacity,
            ActiveStrokeOpacity = options.ActiveStrokeOpacity,
            BackgroundFillColor = options.BackgroundFillColor,
            SurfaceClearColor = options.SurfaceClearColor,
        };
        StrokeWidth = options.StrokeWidth;
        Fonts = options.Fonts ?? new FontRegistry();
    }

    #region Image-based factory methods

    /// <summary>
    /// The length, in calibrated units, of the longest side of a drawing space derived by
    /// the <c>CreateForImage</c> factory methods when
    /// <see cref="CalibrationSizing.DeriveFromBackgroundImage"/> is chosen.
    /// </summary>
    public const int CalibrationLongSide = 1000;

    /// <summary>
    /// Creates a drawing session for annotating an image, with an explicitly stated
    /// calibrated drawing space: the encoded image (PNG, JPEG, etc.) becomes the session's
    /// background, and <paramref name="calibrationSize"/> becomes the calibration size.
    /// Match its aspect ratio to the image's to avoid stretching the image.
    /// </summary>
    /// <param name="encodedImage">The encoded image bytes to annotate.</param>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions must be positive.</param>
    /// <param name="options">
    /// Other initial settings; when omitted, defaults are used. Its
    /// <see cref="DrawingSessionOptions.CalibrationSize"/> is replaced by
    /// <paramref name="calibrationSize"/>.
    /// </param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encodedImage"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the bytes cannot be decoded as an image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="calibrationSize"/> is less than 1.</exception>
    public static DrawingSession CreateForImage(byte[] encodedImage, SKSizeI calibrationSize, DrawingSessionOptions options = null)
    {
        if (calibrationSize.Width < 1 || calibrationSize.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(calibrationSize));
        }
        return CreateForEncodedImage(encodedImage, imageSize => calibrationSize, options);
    }

    /// <summary>
    /// Creates a drawing session for annotating an image, with an explicitly stated
    /// calibrated drawing space: the encoded image (PNG, JPEG, etc.) becomes the session's
    /// background, and <paramref name="calibrationSize"/> becomes the calibration size.
    /// Match its aspect ratio to the image's to avoid stretching the image.
    /// </summary>
    /// <param name="encodedImage">The encoded image bytes to annotate.</param>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions must be positive.</param>
    /// <param name="options">
    /// Other initial settings; when omitted, defaults are used. Its
    /// <see cref="DrawingSessionOptions.CalibrationSize"/> is replaced by
    /// <paramref name="calibrationSize"/>.
    /// </param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encodedImage"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the bytes cannot be decoded as an image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="calibrationSize"/> is less than 1.</exception>
    public static DrawingSession CreateForImage(byte[] encodedImage, Size calibrationSize, DrawingSessionOptions options = null)
        => CreateForImage(encodedImage, GraphicsInterop.ToGraphics(calibrationSize), options);

    /// <summary>
    /// Creates a drawing session for annotating an image: the encoded image (PNG, JPEG,
    /// etc.) becomes the session's background, and the calibrated drawing space is set
    /// according to the explicitly chosen <paramref name="sizing"/> behavior.
    /// </summary>
    /// <param name="encodedImage">The encoded image bytes to annotate.</param>
    /// <param name="sizing">How the session's calibration size is determined.</param>
    /// <param name="options">Other initial settings; when omitted, defaults are used.</param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encodedImage"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the bytes cannot be decoded as an image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizing"/> is not a defined value.</exception>
    public static DrawingSession CreateForImage(byte[] encodedImage, CalibrationSizing sizing, DrawingSessionOptions options = null)
    {
        return CreateForEncodedImage(encodedImage, imageSize => ResolveCalibrationSize(sizing, imageSize, options), options);
    }

    /// <summary>
    /// Creates a drawing session for annotating an already-decoded image, with an
    /// explicitly stated calibrated drawing space: the bitmap becomes the session's
    /// background (the caller keeps ownership and disposes it), and
    /// <paramref name="calibrationSize"/> becomes the calibration size. Match its aspect
    /// ratio to the image's to avoid stretching the image.
    /// </summary>
    /// <param name="image">The bitmap to annotate; not disposed by the session.</param>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions must be positive.</param>
    /// <param name="options">
    /// Other initial settings; when omitted, defaults are used. Its
    /// <see cref="DrawingSessionOptions.CalibrationSize"/> is replaced by
    /// <paramref name="calibrationSize"/>.
    /// </param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="calibrationSize"/> is less than 1.</exception>
    public static DrawingSession CreateForImage(SKBitmap image, SKSizeI calibrationSize, DrawingSessionOptions options = null)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        if (calibrationSize.Width < 1 || calibrationSize.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(calibrationSize));
        }

        DrawingSession session = CreateWithCalibration(calibrationSize, options);
        session._renderer.BackgroundImage = image;
        return session;
    }

    /// <summary>
    /// Creates a drawing session for annotating an already-decoded image, with an
    /// explicitly stated calibrated drawing space: the bitmap becomes the session's
    /// background (the caller keeps ownership and disposes it), and
    /// <paramref name="calibrationSize"/> becomes the calibration size. Match its aspect
    /// ratio to the image's to avoid stretching the image.
    /// </summary>
    /// <param name="image">The bitmap to annotate; not disposed by the session.</param>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions must be positive.</param>
    /// <param name="options">
    /// Other initial settings; when omitted, defaults are used. Its
    /// <see cref="DrawingSessionOptions.CalibrationSize"/> is replaced by
    /// <paramref name="calibrationSize"/>.
    /// </param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="calibrationSize"/> is less than 1.</exception>
    public static DrawingSession CreateForImage(SKBitmap image, Size calibrationSize, DrawingSessionOptions options = null)
        => CreateForImage(image, GraphicsInterop.ToGraphics(calibrationSize), options);

    /// <summary>
    /// Creates a drawing session for annotating an already-decoded image: the bitmap
    /// becomes the session's background (the caller keeps ownership and disposes it), and
    /// the calibrated drawing space is set according to the explicitly chosen
    /// <paramref name="sizing"/> behavior.
    /// </summary>
    /// <param name="image">The bitmap to annotate; not disposed by the session.</param>
    /// <param name="sizing">How the session's calibration size is determined.</param>
    /// <param name="options">Other initial settings; when omitted, defaults are used.</param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizing"/> is not a defined value.</exception>
    public static DrawingSession CreateForImage(SKBitmap image, CalibrationSizing sizing, DrawingSessionOptions options = null)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }

        SKSizeI calibrationSize = ResolveCalibrationSize(sizing, new SKSizeI(image.Width, image.Height), options);
        DrawingSession session = CreateWithCalibration(calibrationSize, options);
        session._renderer.BackgroundImage = image;
        return session;
    }

    /// <summary>
    /// Creates a drawing session for annotating an image supplied as raw 32-bit BGRA
    /// pixels - for example a webcam frame or photo capture - with the calibrated drawing
    /// space set according to the explicitly chosen <paramref name="sizing"/> behavior.
    /// The pixels are copied, so the caller may reuse its buffer immediately.
    /// </summary>
    /// <param name="bgraPixels">The image's tightly packed 32-bit BGRA pixels.</param>
    /// <param name="width">The image's width in pixels.</param>
    /// <param name="height">The image's height in pixels.</param>
    /// <param name="sizing">How the session's calibration size is determined.</param>
    /// <param name="options">Other initial settings; when omitted, defaults are used.</param>
    /// <param name="mirrorHorizontally">
    /// <c>true</c> to flip the image left-to-right - for example so a webcam still reads
    /// like a mirror, matching a mirrored ("selfie") live preview.
    /// </param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bgraPixels"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1, or <paramref name="sizing"/> is not a defined value.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bgraPixels"/> is too small for the stated dimensions.</exception>
    public static DrawingSession CreateForImage(byte[] bgraPixels, int width, int height,
        CalibrationSizing sizing, DrawingSessionOptions options = null, bool mirrorHorizontally = false)
    {
        SKBitmap bitmap = DecodeBgraPixels(bgraPixels, width, height, mirrorHorizontally);
        try
        {
            SKSizeI calibrationSize = ResolveCalibrationSize(sizing, new SKSizeI(width, height), options);
            DrawingSession session = CreateWithCalibration(calibrationSize, options);
            session._ownedBackgroundImage = bitmap;
            session._renderer.BackgroundImage = bitmap;
            return session;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a drawing session for annotating an image supplied as raw 32-bit BGRA
    /// pixels, with an explicitly stated calibrated drawing space. Match the calibration
    /// size's aspect ratio to the image's to avoid stretching. The pixels are copied, so
    /// the caller may reuse its buffer immediately.
    /// </summary>
    /// <param name="bgraPixels">The image's tightly packed 32-bit BGRA pixels.</param>
    /// <param name="width">The image's width in pixels.</param>
    /// <param name="height">The image's height in pixels.</param>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions must be positive.</param>
    /// <param name="options">
    /// Other initial settings; when omitted, defaults are used. Its
    /// <see cref="DrawingSessionOptions.CalibrationSize"/> is replaced by
    /// <paramref name="calibrationSize"/>.
    /// </param>
    /// <param name="mirrorHorizontally"><c>true</c> to flip the image left-to-right.</param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bgraPixels"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension of the image or of <paramref name="calibrationSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bgraPixels"/> is too small for the stated dimensions.</exception>
    public static DrawingSession CreateForImage(byte[] bgraPixels, int width, int height,
        SKSizeI calibrationSize, DrawingSessionOptions options = null, bool mirrorHorizontally = false)
    {
        if (calibrationSize.Width < 1 || calibrationSize.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(calibrationSize));
        }

        SKBitmap bitmap = DecodeBgraPixels(bgraPixels, width, height, mirrorHorizontally);
        try
        {
            DrawingSession session = CreateWithCalibration(calibrationSize, options);
            session._ownedBackgroundImage = bitmap;
            session._renderer.BackgroundImage = bitmap;
            return session;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a drawing session for annotating an image supplied as raw 32-bit BGRA
    /// pixels, with an explicitly stated calibrated drawing space. Match the calibration
    /// size's aspect ratio to the image's to avoid stretching. The pixels are copied, so
    /// the caller may reuse its buffer immediately.
    /// </summary>
    /// <param name="bgraPixels">The image's tightly packed 32-bit BGRA pixels.</param>
    /// <param name="width">The image's width in pixels.</param>
    /// <param name="height">The image's height in pixels.</param>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions must be positive.</param>
    /// <param name="options">
    /// Other initial settings; when omitted, defaults are used. Its
    /// <see cref="DrawingSessionOptions.CalibrationSize"/> is replaced by
    /// <paramref name="calibrationSize"/>.
    /// </param>
    /// <param name="mirrorHorizontally"><c>true</c> to flip the image left-to-right.</param>
    /// <returns>A new session with the image as its background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bgraPixels"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension of the image or of <paramref name="calibrationSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bgraPixels"/> is too small for the stated dimensions.</exception>
    public static DrawingSession CreateForImage(byte[] bgraPixels, int width, int height,
        Size calibrationSize, DrawingSessionOptions options = null, bool mirrorHorizontally = false)
        => CreateForImage(bgraPixels, width, height, GraphicsInterop.ToGraphics(calibrationSize), options, mirrorHorizontally);

    private static SKBitmap DecodeBgraPixels(byte[] bgraPixels, int width, int height, bool mirrorHorizontally)
    {
        if (bgraPixels == null) { throw new ArgumentNullException(nameof(bgraPixels)); }
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height)); }

        int required = width * height * 4;
        if (bgraPixels.Length < required)
        {
            throw new ArgumentException(
                $"The pixel buffer holds {bgraPixels.Length} bytes but {width} x {height} 32-bit BGRA pixels require {required}.",
                nameof(bgraPixels));
        }

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        Marshal.Copy(bgraPixels, 0, bitmap.GetPixels(), required);
        if (!mirrorHorizontally) { return bitmap; }

        var mirrored = new SKBitmap(bitmap.Info);
        using (var canvas = new SKCanvas(mirrored))
        {
            canvas.Scale(-1, 1, width / 2f, 0);
            canvas.DrawBitmap(bitmap, new SKPoint(0, 0), new SKSamplingOptions(SKFilterMode.Nearest));
        }
        bitmap.Dispose();
        return mirrored;
    }

    private static DrawingSession CreateForEncodedImage(
        byte[] encodedImage,
        Func<SKSizeI, SKSizeI> calibrationForImageSize,
        DrawingSessionOptions options)
    {
        if (encodedImage == null) { throw new ArgumentNullException(nameof(encodedImage)); }

        SKBitmap decoded = SKBitmap.Decode(encodedImage);
        if (decoded == null)
        {
            throw new ArgumentException("The provided bytes could not be decoded as an image.", nameof(encodedImage));
        }

        try
        {
            SKSizeI calibrationSize = calibrationForImageSize(new SKSizeI(decoded.Width, decoded.Height));
            DrawingSession session = CreateWithCalibration(calibrationSize, options);
            session._ownedBackgroundImage = decoded;
            session._renderer.BackgroundImage = decoded;
            return session;
        }
        catch
        {
            decoded.Dispose();
            throw;
        }
    }

    private static SKSizeI ResolveCalibrationSize(CalibrationSizing sizing, SKSizeI imageSize, DrawingSessionOptions options)
    {
        switch (sizing)
        {
            case CalibrationSizing.FromOptions:
                return (options ?? new DrawingSessionOptions()).CalibrationSizeValue;

            case CalibrationSizing.DeriveFromBackgroundImage:
                return DeriveCalibrationSize(imageSize);

            default:
                throw new ArgumentOutOfRangeException(nameof(sizing));
        }
    }

    private static DrawingSession CreateWithCalibration(SKSizeI calibrationSize, DrawingSessionOptions options)
    {
        var effective = new DrawingSessionOptions
        {
            CalibrationSize = GraphicsInterop.ToImaging(calibrationSize),
        };
        if (options != null)
        {
            effective.LayerOpacity = options.LayerOpacity;
            effective.ActiveStrokeOpacity = options.ActiveStrokeOpacity;
            effective.BackgroundFillColor = options.BackgroundFillColor;
            effective.SurfaceClearColor = options.SurfaceClearColor;
            effective.StrokeWidth = options.StrokeWidth;
            effective.Fonts = options.Fonts;
        }
        return new DrawingSession(effective);
    }

    private static SKSizeI DeriveCalibrationSize(SKSizeI imageSize)
    {
        int width = Math.Max(1, imageSize.Width);
        int height = Math.Max(1, imageSize.Height);

        return width >= height
            ? new SKSizeI(CalibrationLongSide,
                Math.Max(1, (int)Math.Round(CalibrationLongSide * (height / (double)width), MidpointRounding.AwayFromZero)))
            : new SKSizeI(
                Math.Max(1, (int)Math.Round(CalibrationLongSide * (width / (double)height), MidpointRounding.AwayFromZero)),
                CalibrationLongSide);
    }

    #endregion

    #region Layer management

    /// <summary>
    /// Adds a new drawing layer on top of any existing layers. The first layer added
    /// becomes the <see cref="ActiveLayer"/>.
    /// </summary>
    /// <param name="name">The display name of the layer; must be unique within the session.</param>
    /// <param name="color">The color that the layer's strokes are drawn with.</param>
    /// <returns>The newly created layer.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null/whitespace, or a layer with that name already exists.
    /// </exception>
    public DrawingLayer AddLayer(string name, SKColor color)
    {
        ThrowIfDisposed();
        var layer = new DrawingLayer(name, color);

        lock (_layersLocker)
        {
            foreach (DrawingLayer existing in _layers)
            {
                if (String.Equals(existing.Name, layer.Name, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"A layer named '{layer.Name}' already exists.", nameof(name));
                }
            }
            _layers.Add(layer);
            ActiveLayer = ActiveLayer ?? layer;
        }

        return layer;
    }

    /// <summary>
    /// Adds a new drawing layer on top of any existing layers. The first layer added
    /// becomes the <see cref="ActiveLayer"/>.
    /// </summary>
    /// <param name="name">The display name of the layer; must be unique within the session.</param>
    /// <param name="color">The color that the layer's strokes are drawn with.</param>
    /// <returns>The newly created layer.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null/whitespace, or a layer with that name already exists.
    /// </exception>
    public DrawingLayer AddLayer(string name, Color color) => AddLayer(name, GraphicsInterop.ToGraphics(color));

    /// <summary>
    /// Finds a layer by its (case-sensitive) name.
    /// </summary>
    /// <param name="name">The name of the layer to find.</param>
    /// <returns>The matching layer; or <c>null</c> when no layer has that name.</returns>
    public DrawingLayer GetLayer(string name)
    {
        if (String.IsNullOrWhiteSpace(name)) { return null; }
        string trimmed = name.Trim();

        lock (_layersLocker)
        {
            foreach (DrawingLayer layer in _layers)
            {
                if (String.Equals(layer.Name, trimmed, StringComparison.Ordinal)) { return layer; }
            }
        }
        return null;
    }

    /// <summary>
    /// Removes a layer (and all of its strokes) from the session.
    /// </summary>
    /// <param name="layer">The layer to remove.</param>
    /// <returns><c>true</c> when the layer was found and removed.</returns>
    public bool RemoveLayer(DrawingLayer layer)
    {
        if (layer == null) { return false; }

        bool removed;
        lock (_layersLocker)
        {
            removed = _layers.Remove(layer);
            if (removed)
            {
                _strokeOrder.RemoveAll(l => ReferenceEquals(l, layer));
                if (ReferenceEquals(ActiveLayer, layer))
                {
                    ActiveLayer = _layers.Count > 0 ? _layers[0] : null;
                }
            }
        }

        if (removed)
        {
            RaiseDrawingChanged();
            RaiseRedrawRequested();
        }
        return removed;
    }

    #endregion

    #region Background image

    /// <summary>
    /// Decodes the given encoded image (PNG, JPEG, etc.) and uses it as the drawing's
    /// background. The session owns the decoded bitmap and disposes it when replaced or
    /// when the session is disposed.
    /// </summary>
    /// <param name="encodedImage">The encoded image bytes; pass <c>null</c> to remove the background image.</param>
    /// <exception cref="ArgumentException">Thrown when the bytes cannot be decoded as an image.</exception>
    public void SetBackgroundImage(byte[] encodedImage)
    {
        ThrowIfDisposed();
        DisposeOwnedBackground();

        if (encodedImage == null)
        {
            _renderer.BackgroundImage = null;
        }
        else
        {
            SKBitmap decoded = SKBitmap.Decode(encodedImage);
            if (decoded == null)
            {
                throw new ArgumentException("The provided bytes could not be decoded as an image.", nameof(encodedImage));
            }
            _ownedBackgroundImage = decoded;
            _renderer.BackgroundImage = decoded;
        }

        RaiseRedrawRequested();
    }

    /// <summary>
    /// Uses raw 32-bit BGRA pixels - for example a webcam frame or photo capture - as the
    /// drawing's background. The pixels are copied (so the caller may reuse its buffer
    /// immediately) and the session owns the resulting bitmap. Note that, like every
    /// background setter, this does NOT change the session's calibration size; to derive
    /// the drawing space from the image, create the session with a raw-pixels
    /// <c>CreateForImage</c> factory overload instead.
    /// </summary>
    /// <param name="bgraPixels">The image's tightly packed 32-bit BGRA pixels.</param>
    /// <param name="width">The image's width in pixels.</param>
    /// <param name="height">The image's height in pixels.</param>
    /// <param name="mirrorHorizontally">
    /// <c>true</c> to flip the image left-to-right - for example so a webcam still reads
    /// like a mirror, matching a mirrored ("selfie") live preview.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bgraPixels"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bgraPixels"/> is too small for the stated dimensions.</exception>
    public void SetBackgroundImage(byte[] bgraPixels, int width, int height, bool mirrorHorizontally = false)
    {
        ThrowIfDisposed();
        SKBitmap bitmap = DecodeBgraPixels(bgraPixels, width, height, mirrorHorizontally);

        DisposeOwnedBackground();
        _ownedBackgroundImage = bitmap;
        _renderer.BackgroundImage = bitmap;

        RaiseRedrawRequested();
    }

    #endregion

    #region Pointer input

    /// <summary>
    /// Begins a new stroke at the given pointer position. Call from the hosting view's
    /// pointer-pressed (or mouse-down) handler. A press outside the drawing area is ignored.
    /// </summary>
    /// <param name="viewPoint">The pointer position, in the hosting control's logical coordinates.</param>
    /// <param name="viewSize">The logical size of the hosting control.</param>
    /// <returns><c>true</c> when a stroke was started (the view should capture the pointer).</returns>
    public bool PointerPressed(SKPoint viewPoint, SKSize viewSize)
    {
        ThrowIfDisposed();

        DrawingLayer layer = ActiveLayer;
        if (layer == null || _renderer.LastCanvasSize.IsEmpty) { return false; }

        SKPointI? calibrated = CanvasCalibration.ViewPointToCalibrated(
            viewPoint, viewSize, _renderer.LastCanvasSizeValue, CalibrationSizeValue);
        if (!calibrated.HasValue) { return false; }

        return BeginStrokeAtCalibrated(calibrated.Value, layer);
    }

    /// <summary>
    /// Begins a new stroke at the given pointer position. Call from the hosting view's
    /// pointer-pressed (or mouse-down) handler. A press outside the drawing area is ignored.
    /// </summary>
    /// <param name="viewPoint">The pointer position, in the hosting control's logical coordinates.</param>
    /// <param name="viewSize">The logical size of the hosting control.</param>
    /// <returns><c>true</c> when a stroke was started (the view should capture the pointer).</returns>
    public bool PointerPressed(PointF viewPoint, SizeF viewSize)
        => PointerPressed(GraphicsInterop.ToGraphics(viewPoint), GraphicsInterop.ToGraphics(viewSize));

    /// <summary>
    /// Extends the in-progress stroke to the given pointer position. Call from the hosting
    /// view's pointer-moved (or mouse-move) handler; calls made while no stroke is in
    /// progress are ignored, so the handler does not need to track button state itself.
    /// Positions outside the drawing area are clamped to its edge.
    /// </summary>
    /// <param name="viewPoint">The pointer position, in the hosting control's logical coordinates.</param>
    /// <param name="viewSize">The logical size of the hosting control.</param>
    /// <returns><c>true</c> when the stroke was extended with a new point.</returns>
    public bool PointerMoved(SKPoint viewPoint, SKSize viewSize)
    {
        ThrowIfDisposed();
        if (_activeStroke == null) { return false; }

        SKPointI? calibrated = CanvasCalibration.ViewPointToCalibrated(
            viewPoint, viewSize, _renderer.LastCanvasSizeValue, CalibrationSizeValue, clampToDrawingArea: true);
        if (!calibrated.HasValue) { return false; }

        return ExtendStrokeToCalibrated(calibrated.Value);
    }

    /// <summary>
    /// Extends the in-progress stroke to the given pointer position. Call from the hosting
    /// view's pointer-moved (or mouse-move) handler; calls made while no stroke is in
    /// progress are ignored, so the handler does not need to track button state itself.
    /// Positions outside the drawing area are clamped to its edge.
    /// </summary>
    /// <param name="viewPoint">The pointer position, in the hosting control's logical coordinates.</param>
    /// <param name="viewSize">The logical size of the hosting control.</param>
    /// <returns><c>true</c> when the stroke was extended with a new point.</returns>
    public bool PointerMoved(PointF viewPoint, SizeF viewSize)
        => PointerMoved(GraphicsInterop.ToGraphics(viewPoint), GraphicsInterop.ToGraphics(viewSize));

    /// <summary>
    /// Begins a new stroke at the given NORMALIZED drawing-space position - (0, 0) is the
    /// drawing space's top-left corner, (1, 1) its bottom-right. Unlike the view-coordinate
    /// <see cref="PointerPressed(SKPoint, SKSize)"/>, this works in the calibrated drawing
    /// space directly, needs no prior <see cref="Render(SKCanvas, SKImageInfo, bool)"/>
    /// call, and never depends on a canvas or view size - ideal for programmatic input
    /// sources such as computer-vision hand or object tracking. A position outside the
    /// 0..1 range is ignored (mirroring how a view-coordinate press outside the drawing
    /// area is ignored). The stroke completes through the same
    /// <see cref="PointerReleased"/> / <see cref="PointerCanceled"/> calls as view-driven
    /// strokes, and the two input styles may be mixed freely.
    /// </summary>
    /// <param name="normX">The horizontal position across the drawing space, 0..1.</param>
    /// <param name="normY">The vertical position down the drawing space, 0..1.</param>
    /// <returns><c>true</c> when a stroke was started.</returns>
    public bool PointerPressedNormalized(float normX, float normY)
    {
        ThrowIfDisposed();

        DrawingLayer layer = ActiveLayer;
        if (layer == null) { return false; }
        if (Single.IsNaN(normX) || Single.IsNaN(normY)
            || normX < 0f || normX > 1f || normY < 0f || normY > 1f)
        {
            return false;
        }

        return BeginStrokeAtCalibrated(NormalizedToCalibrated(normX, normY), layer);
    }

    /// <summary>
    /// Extends the in-progress stroke to the given NORMALIZED drawing-space position -
    /// the programmatic companion of <see cref="PointerMoved(SKPoint, SKSize)"/> (see
    /// <see cref="PointerPressedNormalized"/>). Calls made while no stroke is in progress
    /// are ignored; positions outside the 0..1 range are clamped to the drawing area's
    /// edge, matching the view-coordinate behavior.
    /// </summary>
    /// <param name="normX">The horizontal position across the drawing space, 0..1.</param>
    /// <param name="normY">The vertical position down the drawing space, 0..1.</param>
    /// <returns><c>true</c> when the stroke was extended with a new point.</returns>
    public bool PointerMovedNormalized(float normX, float normY)
    {
        ThrowIfDisposed();
        if (_activeStroke == null) { return false; }
        if (Single.IsNaN(normX) || Single.IsNaN(normY)) { return false; }

        return ExtendStrokeToCalibrated(NormalizedToCalibrated(normX, normY));
    }

    private SKPointI NormalizedToCalibrated(float normX, float normY)
    {
        SKSizeI calibration = CalibrationSizeValue;
        return new SKPointI(
            (int)Math.Round(Math.Clamp(normX, 0f, 1f) * calibration.Width, MidpointRounding.AwayFromZero),
            (int)Math.Round(Math.Clamp(normY, 0f, 1f) * calibration.Height, MidpointRounding.AwayFromZero));
    }

    private bool BeginStrokeAtCalibrated(SKPointI calibrated, DrawingLayer layer)
    {
        //A new press while a stroke is somehow still active commits the earlier stroke first
        if (_activeStroke != null) { PointerReleased(); }

        _activeStrokeTimer = Stopwatch.StartNew();
        _activeStroke = new Stroke(StrokeWidth, DateTimeOffset.UtcNow);
        _activeStrokeLayer = layer;
        _activeStroke.AddPoint(calibrated.X, calibrated.Y, 0);

        RaiseRedrawRequested();
        return true;
    }

    private bool ExtendStrokeToCalibrated(SKPointI calibrated)
    {
        bool added = _activeStroke.AddPoint(calibrated.X, calibrated.Y,
            (int)Math.Min(_activeStrokeTimer.ElapsedMilliseconds, Int32.MaxValue));

        if (added) { RaiseRedrawRequested(); }
        return added;
    }

    /// <summary>
    /// Completes the in-progress stroke and commits it to the active layer. Call from the
    /// hosting view's pointer-released (or mouse-up) handler. A stroke that never moved
    /// commits as a single-point dot.
    /// </summary>
    /// <returns><c>true</c> when a stroke was committed.</returns>
    public bool PointerReleased()
    {
        ThrowIfDisposed();

        Stroke stroke = _activeStroke;
        DrawingLayer layer = _activeStrokeLayer;
        _activeStroke = null;
        _activeStrokeLayer = null;
        _activeStrokeTimer = null;

        if (stroke == null || layer == null || stroke.PointCount < 1) { return false; }

        layer.AddStroke(stroke);
        lock (_layersLocker) { _strokeOrder.Add(layer); }

        RaiseDrawingChanged();
        RaiseRedrawRequested();
        return true;
    }

    /// <summary>
    /// Discards the in-progress stroke without committing it - for example when pointer
    /// capture is lost to another control or window.
    /// </summary>
    public void PointerCanceled()
    {
        if (_activeStroke == null) { return; }

        _activeStroke = null;
        _activeStrokeLayer = null;
        _activeStrokeTimer = null;
        RaiseRedrawRequested();
    }

    #endregion

    #region Drawing primitives

    /// <summary>
    /// Draws a straight line from (<paramref name="x1"/>, <paramref name="y1"/>) to
    /// (<paramref name="x2"/>, <paramref name="y2"/>) on the active layer. All coordinates
    /// are in calibrated drawing units.
    /// </summary>
    /// <param name="x1">The horizontal position of the start point.</param>
    /// <param name="y1">The vertical position of the start point.</param>
    /// <param name="x2">The horizontal position of the end point.</param>
    /// <param name="y2">The vertical position of the end point.</param>
    /// <param name="thickness">The line thickness, in calibrated drawing units.</param>
    /// <param name="color">The line's color; or <c>null</c> (the default) to use the active layer's color.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public DrawingShape DrawLine(float x1, float y1, float x2, float y2,
        float thickness = Stroke.DefaultWidth, Color? color = null)
        => CommitShape(new LineShape(x1, y1, x2, y2, thickness, color));

    /// <summary>
    /// Draws an arrow pointing from (<paramref name="x1"/>, <paramref name="y1"/>) to
    /// (<paramref name="x2"/>, <paramref name="y2"/>) on the active layer. All coordinates
    /// are in calibrated drawing units.
    /// </summary>
    /// <param name="x1">The horizontal position of the tail.</param>
    /// <param name="y1">The vertical position of the tail.</param>
    /// <param name="x2">The horizontal position of the tip.</param>
    /// <param name="y2">The vertical position of the tip.</param>
    /// <param name="thickness">The line thickness, in calibrated drawing units.</param>
    /// <param name="color">The arrow's color; or <c>null</c> (the default) to use the active layer's color.</param>
    /// <param name="headLength">The length of each side of the arrow head; when omitted, a proportional default is used.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public DrawingShape DrawArrow(float x1, float y1, float x2, float y2,
        float thickness = Stroke.DefaultWidth, Color? color = null, float? headLength = null)
        => CommitShape(new ArrowShape(x1, y1, x2, y2, thickness, color, headLength));

    /// <summary>
    /// Draws a circle of radius <paramref name="radius"/> centered at
    /// (<paramref name="centerX"/>, <paramref name="centerY"/>) on the active layer. All
    /// coordinates are in calibrated drawing units.
    /// </summary>
    /// <param name="centerX">The horizontal position of the center.</param>
    /// <param name="centerY">The vertical position of the center.</param>
    /// <param name="radius">The radius, in calibrated drawing units; must be positive.</param>
    /// <param name="thickness">The outline thickness, in calibrated drawing units (not used when filled, but it must still be positive).</param>
    /// <param name="color">The circle's color; or <c>null</c> (the default) to use the active layer's color.</param>
    /// <param name="filled"><c>true</c> to fill the circle solid; <c>false</c> (the default) for an outline.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public DrawingShape DrawCircle(float centerX, float centerY, float radius,
        float thickness = Stroke.DefaultWidth, Color? color = null, bool filled = false)
        => CommitShape(new CircleShape(centerX, centerY, radius, thickness, color, filled));

    /// <summary>
    /// Draws an axis-aligned ellipse centered at (<paramref name="centerX"/>,
    /// <paramref name="centerY"/>) on the active layer. All coordinates are in calibrated
    /// drawing units.
    /// </summary>
    /// <param name="centerX">The horizontal position of the center.</param>
    /// <param name="centerY">The vertical position of the center.</param>
    /// <param name="radiusX">The horizontal radius; must be positive.</param>
    /// <param name="radiusY">The vertical radius; must be positive.</param>
    /// <param name="thickness">The outline thickness, in calibrated drawing units (not used when filled, but it must still be positive).</param>
    /// <param name="color">The ellipse's color; or <c>null</c> (the default) to use the active layer's color.</param>
    /// <param name="filled"><c>true</c> to fill the ellipse solid; <c>false</c> (the default) for an outline.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public DrawingShape DrawEllipse(float centerX, float centerY, float radiusX, float radiusY,
        float thickness = Stroke.DefaultWidth, Color? color = null, bool filled = false)
        => CommitShape(new EllipseShape(centerX, centerY, radiusX, radiusY, thickness, color, filled));

    /// <summary>
    /// Draws an axis-aligned rectangle with its top-left corner at (<paramref name="x"/>,
    /// <paramref name="y"/>) on the active layer. All coordinates are in calibrated
    /// drawing units.
    /// </summary>
    /// <param name="x">The horizontal position of the left edge.</param>
    /// <param name="y">The vertical position of the top edge.</param>
    /// <param name="width">The width; must be positive.</param>
    /// <param name="height">The height; must be positive.</param>
    /// <param name="thickness">The outline thickness, in calibrated drawing units (not used when filled, but it must still be positive).</param>
    /// <param name="color">The rectangle's color; or <c>null</c> (the default) to use the active layer's color.</param>
    /// <param name="filled"><c>true</c> to fill the rectangle solid; <c>false</c> (the default) for an outline.</param>
    /// <param name="cornerRadius">The corner radius; zero (the default) for square corners.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public DrawingShape DrawRectangle(float x, float y, float width, float height,
        float thickness = Stroke.DefaultWidth, Color? color = null, bool filled = false, float cornerRadius = 0f)
        => CommitShape(new RectangleShape(x, y, width, height, thickness, color, filled, cornerRadius));

    /// <summary>
    /// Draws a connected series of line segments through the given points on the active
    /// layer - optionally closed into a polygon, and optionally filled. All coordinates
    /// are in calibrated drawing units.
    /// </summary>
    /// <param name="points">The points, as (X, Y) pairs; at least two are required.</param>
    /// <param name="thickness">The outline thickness, in calibrated drawing units (not used when filled, but it must still be positive).</param>
    /// <param name="color">The polyline's color; or <c>null</c> (the default) to use the active layer's color.</param>
    /// <param name="closed"><c>true</c> to connect the last point back to the first.</param>
    /// <param name="filled"><c>true</c> to fill the resulting polygon solid (implies closed).</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fewer than two points are provided.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public DrawingShape DrawPolyline(IReadOnlyList<(float X, float Y)> points,
        float thickness = Stroke.DefaultWidth, Color? color = null, bool closed = false, bool filled = false)
    {
        if (points == null) { throw new ArgumentNullException(nameof(points)); }

        var polylinePoints = new PointF[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            polylinePoints[i] = new PointF(points[i].X, points[i].Y);
        }
        return CommitShape(new PolylineShape(polylinePoints, thickness, color, closed, filled));
    }

    /// <summary>
    /// Draws a line of text on the active layer, in a font from the session's
    /// <see cref="Fonts"/>, as filled glyph outlines. All coordinates and sizes are in
    /// calibrated drawing units; (<paramref name="x"/>, <paramref name="y"/>) is the anchor
    /// on the baseline. The returned shape is a <see cref="TextShape"/> whose
    /// <see cref="TextShape.Width"/> reports the measured line width.
    /// </summary>
    /// <param name="text">The text; null or empty draws nothing (but still commits a shape).</param>
    /// <param name="x">The anchor's horizontal position (left edge, center or right edge by <paramref name="align"/>).</param>
    /// <param name="y">The baseline's vertical position.</param>
    /// <param name="size">The em size; must be positive.</param>
    /// <param name="color">The text's color; or <c>null</c> (the default) to use the active layer's color.</param>
    /// <param name="align">How the line sits about <paramref name="x"/>; <see cref="TextAlign.Left"/> by default.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>.</param>
    /// <param name="familyName">The family; or <c>null</c> (the default) for the first registered font.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive or <paramref name="tracking"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no font is registered, or the session has no active layer.</exception>
    public TextShape DrawText(string text, float x, float y, float size, Color? color = null,
        TextAlign align = TextAlign.Left, float tracking = 0f, string familyName = null)
    {
        ThrowIfDisposed();
        var shape = new TextShape(text, x, y, size, Fonts, color, align, tracking, familyName);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Draws a single line of text on the active layer with the font's kerning switched on or off
    /// (the other overload always kerns, when the font carries kerning data). Otherwise the same as
    /// <see cref="DrawText(string, float, float, float, Color?, TextAlign, float, string)"/>.
    /// </summary>
    /// <param name="text">The text; null or empty draws nothing (but still commits a shape).</param>
    /// <param name="x">The anchor's horizontal position (left edge, center or right edge by <paramref name="align"/>).</param>
    /// <param name="y">The baseline's vertical position.</param>
    /// <param name="size">The em size; must be positive.</param>
    /// <param name="color">The text's color; or <c>null</c> to use the active layer's color.</param>
    /// <param name="align">How the line sits about <paramref name="x"/>.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>.</param>
    /// <param name="familyName">The family; or <c>null</c> for the first registered font.</param>
    /// <param name="kerning"><c>true</c> to apply the font's kerning; <c>false</c> to space glyphs by their plain advances.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive, <paramref name="tracking"/> is not finite, or <paramref name="align"/> is not Left, Center or Right.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no font is registered, or the session has no active layer.</exception>
    public TextShape DrawText(string text, float x, float y, float size, Color? color, TextAlign align, float tracking,
        string familyName, bool kerning)
    {
        ThrowIfDisposed();
        var shape = new TextShape(text, x, y, size, Fonts, color, align, tracking, familyName, kerning);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Lays out a block of text in this session's <see cref="Fonts"/> (see
    /// <see cref="TextLayoutEngine.Layout(FontRegistry, string, float, string, TextLayoutOptions)"/>):
    /// several lines, wrapped to a width, aligned, spaced and trimmed as the options say. Nothing is
    /// drawn - draw the result with <see cref="DrawTextBlock"/> or a <see cref="TextBlockShape"/>.
    /// </summary>
    /// <param name="text">The text; null is treated as empty.</param>
    /// <param name="size">The em size, in calibrated drawing units.</param>
    /// <param name="familyName">The family; or <c>null</c> for the first registered font.</param>
    /// <param name="options">The layout settings; or <c>null</c> for the defaults.</param>
    /// <returns>The laid-out block.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size or an option is out of range.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no font is registered.</exception>
    public TextLayoutResult LayoutText(string text, float size, string familyName = null, TextLayoutOptions options = null)
    {
        ThrowIfDisposed();
        return TextLayoutEngine.Layout(Fonts, text, size, familyName, options);
    }

    /// <summary>
    /// Lays out a sequence of styled runs as one block in this session's <see cref="Fonts"/> (see
    /// <see cref="TextLayoutEngine.Layout(FontRegistry, IReadOnlyList{TextRunDescriptor}, TextLayoutOptions)"/>).
    /// </summary>
    /// <param name="runs">The runs; at least one.</param>
    /// <param name="options">The layout settings; or <c>null</c> for the defaults.</param>
    /// <returns>The laid-out block.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="runs"/> is null or holds a null run.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="runs"/> is empty or a family is not registered.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no font is registered.</exception>
    public TextLayoutResult LayoutText(IReadOnlyList<TextRunDescriptor> runs, TextLayoutOptions options = null)
    {
        ThrowIfDisposed();
        return TextLayoutEngine.Layout(Fonts, runs, options);
    }

    /// <summary>
    /// Adds a laid-out block of text to the active layer, filled in a flat color, with its box's
    /// top-left corner at (<paramref name="x"/>, <paramref name="y"/>). For a gradient, an outline,
    /// a reveal or pieces, build a <see cref="TextBlockShape"/> and add it with
    /// <see cref="DrawShape(DrawingShape)"/>.
    /// </summary>
    /// <param name="layout">The laid-out text (from <see cref="LayoutText(string, float, string, TextLayoutOptions)"/>).</param>
    /// <param name="x">Where the box's left edge goes, calibrated units.</param>
    /// <param name="y">Where the box's top edge goes, calibrated units.</param>
    /// <param name="color">The text's color; or <c>null</c> for the active layer's.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public TextBlockShape DrawTextBlock(TextLayoutResult layout, float x, float y, Color? color = null)
    {
        ThrowIfDisposed();
        var shape = new TextBlockShape(layout, x, y, color);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Adds burned-in captions to the active layer: an element that shows the cues of a
    /// <see cref="CaptionTrack"/> active at the session's <see cref="Time"/> (see
    /// <see cref="CaptionPresenter"/>), in this session's <see cref="Fonts"/>.
    /// </summary>
    /// <param name="track">The captions.</param>
    /// <param name="layout">The frame and safe area to place them in; or <c>null</c> for the whole calibrated drawing (no safe-area guide).</param>
    /// <param name="style">How they look; or <c>null</c> for the readable defaults.</param>
    /// <returns>The element that was added (an <see cref="AnimatedShape"/>), which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="track"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a style value is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer, or no font is registered when a cue shows.</exception>
    public AnimatedShape DrawCaptions(CaptionTrack track, DrawingLayout layout = null, CaptionStyle style = null)
    {
        ThrowIfDisposed();
        var presenter = new CaptionPresenter(track, Fonts, layout ?? new DrawingLayout(CalibrationSize), style);
        AnimatedShape shape = presenter.CreateShape(Time);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Draws the live particles of a particle system at one moment on the active layer, as
    /// soft round dots in each particle's own colour, size and opacity (see
    /// <see cref="ParticlesShape"/>). Particle positions and sizes are read as calibrated
    /// drawing units. The particles are taken once, now: draw a new shape for each frame.
    /// </summary>
    /// <param name="system">The particle system to draw.</param>
    /// <param name="time">The moment, in seconds, whose particles to draw.</param>
    /// <param name="softness">How soft each dot's edge is, 0..1 (1, the default, fades from the centre; 0 draws plain discs).</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="system"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite or <paramref name="softness"/> is outside 0..1.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public ParticlesShape DrawParticles(DrawingParticleSystem system, double time, float softness = 1f)
    {
        ThrowIfDisposed();
        var shape = new ParticlesShape(system, time, softness);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Adds a general path to the active layer: outlined with round caps and joins (or filled).
    /// For dashes, other caps and joins, a fill under an outline, a gradient, trimming (draw-on)
    /// or morphing, build a <see cref="PathShape"/> and add it with <see cref="DrawShape(DrawingShape)"/>.
    /// </summary>
    /// <param name="geometry">The path, in calibrated drawing units.</param>
    /// <param name="thickness">The outline thickness; positive for an outline. When filled it is not used, and 0 is accepted.</param>
    /// <param name="color">The colour; or <c>null</c> for the layer's.</param>
    /// <param name="filled"><c>true</c> to fill the path instead of outlining it.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public PathShape DrawPath(PathGeometry geometry, float thickness = Stroke.DefaultWidth, Color? color = null, bool filled = false)
    {
        ThrowIfDisposed();
        var shape = new PathShape(geometry, thickness, color, filled);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Adds a painterly brushstroke (a tapered, bowed lozenge - see <see cref="BrushStrokeShape"/>)
    /// to the active layer, filled in a flat colour.
    /// </summary>
    /// <param name="centerX">The horizontal position of the stroke's centre, calibrated units.</param>
    /// <param name="centerY">The vertical position of the stroke's centre, calibrated units.</param>
    /// <param name="length">Its length end to end; positive.</param>
    /// <param name="width">Its width at the middle; positive.</param>
    /// <param name="angle">Its direction, in degrees, clockwise on screen from +x (y points down) - the unit of a path point's angle.</param>
    /// <param name="bend">How far it bows sideways at the middle, as a fraction of its length.</param>
    /// <param name="color">The colour; or <c>null</c> for the layer's.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite, or the length or width is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public BrushStrokeShape DrawBrushStroke(float centerX, float centerY, float length, float width, float angle,
        float bend = 0f, Color? color = null)
    {
        ThrowIfDisposed();
        var shape = new BrushStrokeShape(centerX, centerY, length, width, angle, bend, BrushStrokeShape.DefaultProfile, color);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Adds a picture to the active layer, stretched into a destination rectangle. The shape
    /// refers to the source and never disposes it: keep the source alive while the session may
    /// render it.
    /// </summary>
    /// <param name="source">The picture.</param>
    /// <param name="destination">Where it is drawn, calibrated units.</param>
    /// <param name="sampling">How pixels are sampled; bilinear by default.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public ImageShape DrawImage(ImageSource source, RectangleF destination, ImageSampling sampling = ImageSampling.Linear)
    {
        ThrowIfDisposed();
        var shape = new ImageShape(source, destination, null, sampling);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Adds a sprite to the active layer: a sheet's frames shown one at a time in a destination
    /// rectangle, advancing with the session's <see cref="Time"/> at a frame rate (0 for a fixed
    /// frame chosen by the sprite's <see cref="SpriteShape.FrameIndex"/>).
    /// </summary>
    /// <param name="sheet">The frames.</param>
    /// <param name="destination">Where each frame is drawn, calibrated units.</param>
    /// <param name="framesPerSecond">How fast the frames advance; 0 for none.</param>
    /// <param name="playback">How the frames repeat; loop by default.</param>
    /// <param name="sampling">How the frames are resampled to the destination; bilinear by default.</param>
    /// <returns>The shape that was added, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sheet"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public SpriteShape DrawSprite(SpriteSheet sheet, RectangleF destination, double framesPerSecond = 0,
        SpritePlayback playback = SpritePlayback.Loop, ImageSampling sampling = ImageSampling.Linear)
    {
        ThrowIfDisposed();
        var shape = new SpriteShape(sheet, destination, framesPerSecond, playback, sampling);
        CommitShape(shape);
        return shape;
    }

    /// <summary>
    /// Adds a pre-built (or custom) shape to the active layer.
    /// </summary>
    /// <param name="shape">The shape to add.</param>
    /// <returns>The same shape, which <see cref="UndoLastStroke"/> can remove.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shape"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public DrawingShape DrawShape(DrawingShape shape)
    {
        if (shape == null) { throw new ArgumentNullException(nameof(shape)); }
        return CommitShape(shape);
    }

    private DrawingShape CommitShape(DrawingShape shape)
    {
        ThrowIfDisposed();

        DrawingLayer layer = ActiveLayer;
        if (layer == null)
        {
            throw new InvalidOperationException("Add a layer to the session before drawing shapes.");
        }

        layer.AddShape(shape);
        lock (_layersLocker) { _strokeOrder.Add(layer); }

        RaiseDrawingChanged();
        RaiseRedrawRequested();
        return shape;
    }

    #endregion

    #region Rendering and export

    /// <summary>
    /// Renders the drawing onto the given surface - call from the hosting view's
    /// paint-surface handler.
    /// </summary>
    /// <param name="surface">The surface to render onto.</param>
    /// <param name="info">The image info describing the surface, including its pixel size.</param>
    /// <param name="clearCanvas">
    /// When <c>true</c> (the default), the canvas is cleared to <see cref="SurfaceClearColor"/>
    /// first. Pass <c>false</c> when the caller has already drawn content that the drawing
    /// should overlay - for example a live video frame drawn onto the same canvas
    /// immediately before this call.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
    public void Render(SKSurface surface, SKImageInfo info, bool clearCanvas = true)
    {
        if (surface == null) { throw new ArgumentNullException(nameof(surface)); }
        Render(surface.Canvas, info, clearCanvas);
    }

    /// <summary>
    /// Renders the drawing onto the given canvas - call from the hosting view's
    /// paint-surface handler.
    /// </summary>
    /// <param name="canvas">The canvas to render onto.</param>
    /// <param name="info">The image info describing the canvas, including its pixel size.</param>
    /// <param name="clearCanvas">
    /// When <c>true</c> (the default), the canvas is cleared to <see cref="SurfaceClearColor"/>
    /// first. Pass <c>false</c> when the caller has already drawn content that the drawing
    /// should overlay - for example a live video frame drawn onto the same canvas
    /// immediately before this call.
    /// </param>
    public void Render(SKCanvas canvas, SKImageInfo info, bool clearCanvas = true)
    {
        ThrowIfDisposed();
        _renderer.Render(canvas, info, Layers, _activeStroke, _activeStrokeLayer?.ColorValue, clearCanvas);
    }

    /// <summary>
    /// The rectangle that the drawing occupies within a view (or canvas) of the given size:
    /// the centered aspect-fit rectangle with the calibration space's aspect ratio. Useful
    /// for positioning overlays - cursors, markers, hit regions - that must line up with
    /// the drawing: a normalized drawing-space position (nx, ny) lands at
    /// (rect.X + nx * rect.Width, rect.Y + ny * rect.Height).
    /// </summary>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <returns>The drawing's aspect-fit rectangle; empty when <paramref name="viewSize"/> is unusable.</returns>
    public RectangleF GetDrawingRect(SizeF viewSize)
        => GraphicsInterop.ToImaging(GetDrawingRect(GraphicsInterop.ToGraphics(viewSize)));

    /// <summary>
    /// The rectangle that the drawing occupies within a view (or canvas) of the given size -
    /// the <see cref="SKSize"/>-typed companion of <see cref="GetDrawingRect(SizeF)"/>.
    /// </summary>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <returns>The drawing's aspect-fit rectangle; empty when <paramref name="viewSize"/> is unusable.</returns>
    public SKRect GetDrawingRect(SKSize viewSize)
        => CanvasCalibration.GetDrawingRect(viewSize, CalibrationSizeValue);

    /// <summary>
    /// Scales a length from calibrated drawing units to view (or canvas) units for a view
    /// of the given size - for example to draw a cursor ring whose radius matches what a
    /// stroke of that calibrated width will actually cover on screen.
    /// </summary>
    /// <param name="calibratedLength">The length, in calibrated drawing units (e.g. a stroke width or brush radius).</param>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <returns>The equivalent length in view units; the input value when the sizes are unusable.</returns>
    public float ScaleToView(float calibratedLength, SizeF viewSize)
        => ScaleToView(calibratedLength, GraphicsInterop.ToGraphics(viewSize));

    /// <summary>
    /// Scales a length from calibrated drawing units to view (or canvas) units - the
    /// <see cref="SKSize"/>-typed companion of <see cref="ScaleToView(float, SizeF)"/>.
    /// </summary>
    /// <param name="calibratedLength">The length, in calibrated drawing units.</param>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <returns>The equivalent length in view units; the input value when the sizes are unusable.</returns>
    public float ScaleToView(float calibratedLength, SKSize viewSize)
    {
        SKRect drawingRect = GetDrawingRect(viewSize);
        if (drawingRect.IsEmpty || calibratedLength <= 0) { return calibratedLength; }

        SKSizeI calibration = CalibrationSizeValue;
        return calibratedLength * (drawingRect.Width / calibration.Width);
    }

    /// <summary>
    /// The output size that the parameterless export methods use: the background image's
    /// pixel size when a background image is set (so an annotated photo exports at its
    /// original resolution), otherwise the <see cref="CalibrationSize"/>. Both match the
    /// drawing space's aspect ratio, so a default-size export is never distorted.
    /// </summary>
    public Size DefaultExportSize
    {
        get
        {
            SKBitmap background = _renderer.BackgroundImage;
            return background != null
                ? new Size(background.Width, background.Height)
                : CalibrationSize;
        }
    }

#if NOSKIA
    /// <summary>Gets <see cref="DefaultExportSize"/> as a <see cref="DrawingSizeI"/>.</summary>
    /// <returns>The default export size as a backend size.</returns>
    public DrawingSizeI GetDefaultExportSizeAsDrawing() => GraphicsInterop.ToGraphics(DefaultExportSize);
#else
    /// <summary>Gets <see cref="DefaultExportSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The default export size as a SkiaSharp size.</returns>
    public SKSizeI GetDefaultExportSizeAsSkia() => GraphicsInterop.ToGraphics(DefaultExportSize);
#endif

    /// <summary>
    /// Renders the completed drawing to a new image at the <see cref="DefaultExportSize"/>.
    /// </summary>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the background fill and image render behind the
    /// layers; when <c>false</c>, the layers render over transparency.
    /// </param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    public SKImage ExportImage(bool includeBackground = true) => ExportImage(DefaultExportSize, includeBackground);

    /// <summary>
    /// Renders the completed drawing to PNG-encoded image bytes at the <see cref="DefaultExportSize"/>.
    /// </summary>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>The PNG-encoded image bytes.</returns>
    public byte[] ExportPng(bool includeBackground = true) => ExportPng(DefaultExportSize, includeBackground);

    /// <summary>
    /// Renders the completed drawing to JPEG-encoded image bytes at the <see cref="DefaultExportSize"/>.
    /// </summary>
    /// <param name="quality">The JPEG quality, 1-100; defaults to 90.</param>
    /// <returns>The JPEG-encoded image bytes.</returns>
    public byte[] ExportJpeg(int quality = 90) => ExportJpeg(DefaultExportSize, quality);

    /// <summary>
    /// Renders the completed drawing to a new image - for saving to a file.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the background fill and image render behind the
    /// layers; when <c>false</c>, the layers render over transparency.
    /// </param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    public SKImage ExportImage(SKSizeI outputSize, bool includeBackground = true)
    {
        ThrowIfDisposed();
        return _renderer.RenderToImage(outputSize, Layers, includeBackground);
    }

    /// <summary>
    /// Renders the completed drawing to a new image - for saving to a file.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the background fill and image render behind the
    /// layers; when <c>false</c>, the layers render over transparency.
    /// </param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    public SKImage ExportImage(Size outputSize, bool includeBackground = true)
        => ExportImage(GraphicsInterop.ToGraphics(outputSize), includeBackground);

    /// <summary>
    /// Renders the completed drawing to PNG-encoded image bytes.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>The PNG-encoded image bytes.</returns>
    public byte[] ExportPng(SKSizeI outputSize, bool includeBackground = true)
    {
        using SKImage image = ExportImage(outputSize, includeBackground);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Renders the completed drawing to PNG-encoded image bytes.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>The PNG-encoded image bytes.</returns>
    public byte[] ExportPng(Size outputSize, bool includeBackground = true)
        => ExportPng(GraphicsInterop.ToGraphics(outputSize), includeBackground);

    /// <summary>
    /// Renders the completed drawing to JPEG-encoded image bytes. JPEG has no alpha
    /// channel, so the background is always included and transparent areas render black
    /// unless an opaque <see cref="BackgroundFillColor"/> is set.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="quality">The JPEG quality, 1-100; defaults to 90.</param>
    /// <returns>The JPEG-encoded image bytes.</returns>
    public byte[] ExportJpeg(SKSizeI outputSize, int quality = 90)
    {
        if (quality < 1 || quality > 100) { throw new ArgumentOutOfRangeException(nameof(quality)); }
        using SKImage image = ExportImage(outputSize);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    /// <summary>
    /// Renders the completed drawing to JPEG-encoded image bytes. JPEG has no alpha
    /// channel, so the background is always included and transparent areas render black
    /// unless an opaque <see cref="BackgroundFillColor"/> is set.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="quality">The JPEG quality, 1-100; defaults to 90.</param>
    /// <returns>The JPEG-encoded image bytes.</returns>
    public byte[] ExportJpeg(Size outputSize, int quality = 90)
        => ExportJpeg(GraphicsInterop.ToGraphics(outputSize), quality);

    /// <summary>
    /// Renders the completed drawing to a stream in PNG format.
    /// </summary>
    /// <param name="destination">The stream to write the PNG image to.</param>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    public void ExportPng(Stream destination, SKSizeI outputSize, bool includeBackground = true)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        byte[] bytes = ExportPng(outputSize, includeBackground);
        destination.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Renders the completed drawing to a stream in PNG format.
    /// </summary>
    /// <param name="destination">The stream to write the PNG image to.</param>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    public void ExportPng(Stream destination, Size outputSize, bool includeBackground = true)
        => ExportPng(destination, GraphicsInterop.ToGraphics(outputSize), includeBackground);

    /// <summary>
    /// Renders the completed drawing - exactly as <see cref="ExportImage(Size, bool)"/> would -
    /// and writes the frame into a caller buffer as plain 8-bit RGBA bytes: four bytes per
    /// pixel in R, G, B, A order, rows top to bottom with no padding (a stride of width x 4),
    /// with STRAIGHT (not premultiplied) alpha - the layout of a CodeBrix.Imaging
    /// <c>Image&lt;Rgba32&gt;</c>. The fast way to feed frames to a video encoder or a window
    /// texture: no PNG encode and decode per frame. Reuse one buffer for every frame.
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes (any extra bytes are left untouched).</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the background fill and image render behind the
    /// layers; when <c>false</c>, the layers render over transparency and the alpha bytes
    /// carry it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    public void ExportPixels(Span<byte> destination, Size outputSize, bool includeBackground = true)
    {
        ThrowIfDisposed();
        _renderer.RenderToPixels(destination, outputSize, Layers, includeBackground);
    }

    /// <summary>
    /// Writes the completed drawing into a caller buffer as plain, straight-alpha RGBA bytes
    /// - the <see cref="SKSizeI"/>-typed companion of <see cref="ExportPixels(Span{byte}, Size, bool)"/>.
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes.</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    public void ExportPixels(Span<byte> destination, SKSizeI outputSize, bool includeBackground = true)
    {
        ThrowIfDisposed();
        _renderer.RenderToPixels(destination, outputSize, Layers, includeBackground);
    }

    /// <summary>
    /// Writes the completed drawing into a caller buffer as plain, straight-alpha RGBA bytes,
    /// with a safe-area mask drawn over the finished frame - for a preview still. Apart from
    /// the mask this is exactly <see cref="ExportPixels(Span{byte}, Size, bool)"/>.
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes.</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw over the frame; or <c>null</c> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    public void ExportPixels(Span<byte> destination, Size outputSize, bool includeBackground, SafeAreaOverlay safeAreaOverlay)
    {
        ThrowIfDisposed();
        _renderer.RenderToPixels(destination, outputSize, Layers, includeBackground, safeAreaOverlay);
    }

    /// <summary>
    /// Renders the completed drawing to a new array of plain, straight-alpha RGBA bytes
    /// (width x height x 4, laid out as described on <see cref="ExportPixels(Span{byte}, Size, bool)"/>).
    /// </summary>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>The frame's RGBA bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    public byte[] ExportPixels(Size outputSize, bool includeBackground = true)
    {
        if (outputSize.Width < 1 || outputSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(outputSize)); }
        var pixels = new byte[(long)outputSize.Width * outputSize.Height * 4];
        ExportPixels(pixels, outputSize, includeBackground);
        return pixels;
    }

    /// <summary>
    /// Renders the completed drawing to a new array of plain, straight-alpha RGBA bytes - the
    /// <see cref="SKSizeI"/>-typed companion of <see cref="ExportPixels(Size, bool)"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>The frame's RGBA bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    public byte[] ExportPixels(SKSizeI outputSize, bool includeBackground = true)
        => ExportPixels(GraphicsInterop.ToImaging(outputSize), includeBackground);

    /// <summary>
    /// Renders the completed drawing at the <see cref="DefaultExportSize"/> to a new array of
    /// plain, straight-alpha RGBA bytes.
    /// </summary>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>The frame's RGBA bytes (DefaultExportSize width x height x 4).</returns>
    public byte[] ExportPixels(bool includeBackground = true) => ExportPixels(DefaultExportSize, includeBackground);

    /// <summary>
    /// Renders the completed drawing to a new image with a safe-area mask drawn over the
    /// finished frame - for a preview still that shows the unsafe zones. Apart from the
    /// mask this is exactly <see cref="ExportImage(Size, bool)"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw over the frame; or <c>null</c> for none.</param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    public SKImage ExportImage(Size outputSize, bool includeBackground, SafeAreaOverlay safeAreaOverlay)
    {
        ThrowIfDisposed();
        return _renderer.RenderToImage(outputSize, Layers, includeBackground, safeAreaOverlay);
    }

    /// <summary>
    /// Renders the completed drawing to PNG-encoded bytes with a safe-area mask drawn over
    /// the finished frame. Apart from the mask this is exactly <see cref="ExportPng(Size, bool)"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw over the frame; or <c>null</c> for none.</param>
    /// <returns>The PNG-encoded image bytes.</returns>
    public byte[] ExportPng(Size outputSize, bool includeBackground, SafeAreaOverlay safeAreaOverlay)
    {
        using SKImage image = ExportImage(outputSize, includeBackground, safeAreaOverlay);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Writes the completed drawing into a caller buffer as plain, straight-alpha RGBA bytes,
    /// with preview overlays drawn over the finished frame - a safe-area mask, a debugging
    /// read-out, or both (either may be <c>null</c>). Apart from the overlays this is exactly
    /// <see cref="ExportPixels(Span{byte}, Size, bool)"/>.
    /// </summary>
    /// <param name="destination">The buffer to fill; at least width x height x 4 bytes.</param>
    /// <param name="outputSize">The pixel size of the frame to produce.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw; or <c>null</c> for none.</param>
    /// <param name="debugOverlay">The debugging read-out to draw on top; or <c>null</c> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small for the frame.</exception>
    public void ExportPixels(Span<byte> destination, Size outputSize, bool includeBackground,
        SafeAreaOverlay safeAreaOverlay, DebugOverlay debugOverlay)
    {
        ThrowIfDisposed();
        _renderer.RenderToPixels(destination, outputSize, Layers, includeBackground, safeAreaOverlay, debugOverlay);
    }

    /// <summary>
    /// Renders the completed drawing to a new image with preview overlays drawn over the
    /// finished frame - a safe-area mask, a debugging read-out, or both (either may be
    /// <c>null</c>). Apart from the overlays this is exactly <see cref="ExportImage(Size, bool)"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw; or <c>null</c> for none.</param>
    /// <param name="debugOverlay">The debugging read-out to draw on top; or <c>null</c> for none.</param>
    /// <returns>A new <see cref="SKImage"/> that the caller must dispose.</returns>
    public SKImage ExportImage(Size outputSize, bool includeBackground, SafeAreaOverlay safeAreaOverlay, DebugOverlay debugOverlay)
    {
        ThrowIfDisposed();
        return _renderer.RenderToImage(outputSize, Layers, includeBackground, safeAreaOverlay, debugOverlay);
    }

    /// <summary>
    /// Renders the completed drawing to PNG-encoded bytes with preview overlays drawn over the
    /// finished frame - a safe-area mask, a debugging read-out, or both (either may be
    /// <c>null</c>). Apart from the overlays this is exactly <see cref="ExportPng(Size, bool)"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">When <c>true</c>, the background renders behind the layers.</param>
    /// <param name="safeAreaOverlay">The safe-area mask to draw; or <c>null</c> for none.</param>
    /// <param name="debugOverlay">The debugging read-out to draw on top; or <c>null</c> for none.</param>
    /// <returns>The PNG-encoded image bytes.</returns>
    public byte[] ExportPng(Size outputSize, bool includeBackground, SafeAreaOverlay safeAreaOverlay, DebugOverlay debugOverlay)
    {
        using SKImage image = ExportImage(outputSize, includeBackground, safeAreaOverlay, debugOverlay);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Renders the completed drawing - exactly as <see cref="ExportPixels(Span{byte}, Size, bool)"/>
    /// would - into a new float image, ready for the pixel-plane tools (blur, bloom, tone
    /// curves, compositing, transitions). Straight (not premultiplied) alpha, the same layout
    /// as the byte frame: each channel is <c>byte / 255f</c>, so
    /// <see cref="DrawingPlaneImage.ToRgba(Span{byte})"/> gives back exactly the bytes
    /// <c>ExportPixels</c> writes.
    /// </summary>
    /// <param name="outputSize">The pixel size of the image to produce.</param>
    /// <param name="includeBackground">
    /// When <c>true</c> (the default), the background fill and image render behind the
    /// layers; when <c>false</c>, the layers render over transparency and the image's
    /// <see cref="DrawingPlaneImage.A"/> plane carries it.
    /// </param>
    /// <returns>A new image of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    public DrawingPlaneImage ExportPlaneImage(Size outputSize, bool includeBackground = true)
    {
        ThrowIfDisposed();
        return _renderer.RenderToPlaneImage(outputSize, Layers, includeBackground);
    }

    /// <summary>
    /// Renders the completed drawing at the <see cref="DefaultExportSize"/> into a new float
    /// image (see <see cref="ExportPlaneImage(Size, bool)"/>).
    /// </summary>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <returns>A new image of the <see cref="DefaultExportSize"/>.</returns>
    public DrawingPlaneImage ExportPlaneImage(bool includeBackground = true) => ExportPlaneImage(DefaultExportSize, includeBackground);

    /// <summary>
    /// Renders the completed drawing into an existing float image at that image's size,
    /// overwriting all four of its planes - the allocation-free form of
    /// <see cref="ExportPlaneImage(Size, bool)"/> for a frame loop (reuse one image per frame).
    /// </summary>
    /// <param name="destination">The image to overwrite; its size is the frame size.</param>
    /// <param name="includeBackground">When <c>true</c> (the default), the background renders behind the layers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    public void ExportPlaneImage(DrawingPlaneImage destination, bool includeBackground = true)
    {
        ThrowIfDisposed();
        _renderer.RenderToPlaneImage(destination, Layers, includeBackground);
    }

    /// <summary>
    /// Exports the COVERAGE of everything drawn as a mask - one <see cref="DrawingPlane"/>
    /// of values 0..1 (1 = fully covered by ink, 0 = untouched), to multiply by, blend a
    /// colour through or composite with. Each layer contributes the alpha of its own ink
    /// as drawn (anti-aliased edges, an element color's alpha and a fill's stop alpha
    /// count; <see cref="LayerOpacity"/>, <see cref="DrawingLayer.Opacity"/> and
    /// <see cref="Opacity"/> do not, and the background is never part of it), and the
    /// layers combine as a union, bottom to top: <c>m = m + a * (1 - m)</c>. The drawing
    /// occupies its centered aspect-fit rectangle, exactly as in an export.
    /// </summary>
    /// <param name="outputSize">The pixel size of the mask to produce.</param>
    /// <returns>A new plane of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    public DrawingPlane ExportMask(Size outputSize)
    {
        ThrowIfDisposed();
        return _renderer.RenderToMask(outputSize, Layers);
    }

    /// <summary>
    /// Exports the coverage of everything drawn as a mask at the <see cref="DefaultExportSize"/>
    /// (see <see cref="ExportMask(Size)"/>).
    /// </summary>
    /// <returns>A new plane of the <see cref="DefaultExportSize"/>.</returns>
    public DrawingPlane ExportMask() => ExportMask(DefaultExportSize);

    /// <summary>
    /// Exports the coverage of ONE layer's ink as a mask (see <see cref="ExportMask(Size)"/>):
    /// each value is that layer's alpha byte divided by 255.
    /// </summary>
    /// <param name="layer">One of this session's layers.</param>
    /// <param name="outputSize">The pixel size of the mask to produce.</param>
    /// <returns>A new plane of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layer"/> is not one of this session's layers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    public DrawingPlane ExportMask(DrawingLayer layer, Size outputSize)
    {
        ThrowIfDisposed();
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        bool owned;
        lock (_layersLocker) { owned = _layers.Contains(layer); }
        if (!owned) { throw new ArgumentException("The layer does not belong to this session.", nameof(layer)); }
        return _renderer.RenderToMask(outputSize, new[] { layer });
    }

    /// <summary>
    /// Exports the coverage of one layer's ink as a mask at the <see cref="DefaultExportSize"/>
    /// (see <see cref="ExportMask(DrawingLayer, Size)"/>).
    /// </summary>
    /// <param name="layer">One of this session's layers.</param>
    /// <returns>A new plane of the <see cref="DefaultExportSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layer"/> is not one of this session's layers.</exception>
    public DrawingPlane ExportMask(DrawingLayer layer) => ExportMask(layer, DefaultExportSize);

    /// <summary>
    /// Exports a mask of everything drawn, with an explicit choice of what it measures (see
    /// <see cref="MaskMeaning"/>): <see cref="MaskMeaning.Coverage"/> is exactly
    /// <see cref="ExportMask(Size)"/> - where the ink is, the layer and session opacities ignored;
    /// <see cref="MaskMeaning.VisibleAlpha"/> is the alpha of the frame as
    /// <see cref="ExportPixels(Span{byte}, Size, bool)"/> writes it without the background -
    /// everything the viewer would see, every opacity (element, group, layer,
    /// <see cref="LayerOpacity"/>, <see cref="Opacity"/>), time window, mask and effect included -
    /// each value that alpha byte divided by 255; <see cref="MaskMeaning.HardEdged"/> is where the
    /// elements are, 0 or 1 per pixel by the pixel-centre rule, computed from their geometry in
    /// shared code - the same mask, bit for bit, in both packages.
    /// </summary>
    /// <param name="outputSize">The pixel size of the mask to produce.</param>
    /// <param name="meaning">Coverage, visible alpha or hard-edged coverage.</param>
    /// <returns>A new plane of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1, or <paramref name="meaning"/> is undefined.</exception>
    public DrawingPlane ExportMask(Size outputSize, MaskMeaning meaning)
    {
        ThrowIfDisposed();
        return _renderer.RenderToMask(outputSize, Layers, meaning);
    }

    /// <summary>
    /// Exports a mask of everything drawn at the <see cref="DefaultExportSize"/>, coverage or
    /// visible alpha (see <see cref="ExportMask(Size, MaskMeaning)"/>).
    /// </summary>
    /// <param name="meaning">Coverage, visible alpha or hard-edged coverage.</param>
    /// <returns>A new plane of the <see cref="DefaultExportSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="meaning"/> is undefined.</exception>
    public DrawingPlane ExportMask(MaskMeaning meaning) => ExportMask(DefaultExportSize, meaning);

    /// <summary>
    /// Exports a mask of everything drawn into an existing plane at that plane's size,
    /// overwriting every value - the allocation-free form of
    /// <see cref="ExportMask(Size, MaskMeaning)"/> for a frame loop (reuse one plane per frame).
    /// </summary>
    /// <param name="destination">The plane to overwrite; its size is the frame size.</param>
    /// <param name="meaning">Coverage (the default), visible alpha or hard-edged coverage.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="meaning"/> is undefined.</exception>
    public void ExportMask(DrawingPlane destination, MaskMeaning meaning = MaskMeaning.Coverage)
    {
        ThrowIfDisposed();
        _renderer.RenderToMask(destination, Layers, meaning);
    }

    /// <summary>
    /// Exports a mask of ONE layer, coverage or visible alpha (see
    /// <see cref="ExportMask(Size, MaskMeaning)"/>). Visible alpha is that layer alone exported
    /// over transparency: its own opacity, <see cref="LayerOpacity"/> and <see cref="Opacity"/>
    /// all count.
    /// </summary>
    /// <param name="layer">One of this session's layers.</param>
    /// <param name="outputSize">The pixel size of the mask to produce.</param>
    /// <param name="meaning">Coverage, visible alpha or hard-edged coverage.</param>
    /// <returns>A new plane of <paramref name="outputSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layer"/> is not one of this session's layers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1, or <paramref name="meaning"/> is undefined.</exception>
    public DrawingPlane ExportMask(DrawingLayer layer, Size outputSize, MaskMeaning meaning)
    {
        ThrowIfDisposed();
        CheckOwnLayer(layer);
        return _renderer.RenderToMask(outputSize, new[] { layer }, meaning);
    }

    /// <summary>
    /// Exports a mask of one layer at the <see cref="DefaultExportSize"/>, coverage or visible
    /// alpha (see <see cref="ExportMask(DrawingLayer, Size, MaskMeaning)"/>).
    /// </summary>
    /// <param name="layer">One of this session's layers.</param>
    /// <param name="meaning">Coverage, visible alpha or hard-edged coverage.</param>
    /// <returns>A new plane of the <see cref="DefaultExportSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layer"/> is not one of this session's layers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="meaning"/> is undefined.</exception>
    public DrawingPlane ExportMask(DrawingLayer layer, MaskMeaning meaning) => ExportMask(layer, DefaultExportSize, meaning);

    /// <summary>
    /// Exports a mask of one layer into an existing plane at that plane's size, overwriting
    /// every value (see <see cref="ExportMask(DrawingLayer, Size, MaskMeaning)"/>).
    /// </summary>
    /// <param name="layer">One of this session's layers.</param>
    /// <param name="destination">The plane to overwrite; its size is the frame size.</param>
    /// <param name="meaning">Coverage (the default), visible alpha or hard-edged coverage.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> or <paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layer"/> is not one of this session's layers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="meaning"/> is undefined.</exception>
    public void ExportMask(DrawingLayer layer, DrawingPlane destination, MaskMeaning meaning = MaskMeaning.Coverage)
    {
        ThrowIfDisposed();
        CheckOwnLayer(layer);
        _renderer.RenderToMask(destination, new[] { layer }, meaning);
    }

    /// <summary>
    /// Exports the OWNERSHIP MAP of everything drawn: for each pixel, which element owns it -
    /// the last element in painting order whose area covers the pixel's centre, under the
    /// hard-edged rule of <see cref="MaskMeaning.HardEdged"/> (identical in both packages).
    /// Elements are numbered across the layers, bottom layer first, each layer's elements in
    /// order (see <see cref="DrawingOwnershipMap"/>); pixels nothing covers hold
    /// <see cref="DrawingOwnershipMap.Nobody"/>. The drawing occupies its centered aspect-fit
    /// rectangle, as in every export, and elements count as they stand at <see cref="Time"/>.
    /// </summary>
    /// <param name="outputSize">The pixel size of the map.</param>
    /// <returns>A new map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    public DrawingOwnershipMap ExportOwnershipMap(Size outputSize)
    {
        ThrowIfDisposed();
        return _renderer.RenderToOwnershipMap(outputSize, Layers);
    }

    /// <summary>
    /// Exports the ownership map of everything drawn at the <see cref="DefaultExportSize"/> (see
    /// <see cref="ExportOwnershipMap(Size)"/>).
    /// </summary>
    /// <returns>A new map.</returns>
    public DrawingOwnershipMap ExportOwnershipMap() => ExportOwnershipMap(DefaultExportSize);

    /// <summary>
    /// Exports the ownership map of ONE layer's elements (numbered in that layer's order; see
    /// <see cref="ExportOwnershipMap(Size)"/>).
    /// </summary>
    /// <param name="layer">One of this session's layers.</param>
    /// <param name="outputSize">The pixel size of the map.</param>
    /// <returns>A new map.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layer"/> is not one of this session's layers.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension of <paramref name="outputSize"/> is less than 1.</exception>
    public DrawingOwnershipMap ExportOwnershipMap(DrawingLayer layer, Size outputSize)
    {
        ThrowIfDisposed();
        CheckOwnLayer(layer);
        return _renderer.RenderToOwnershipMap(outputSize, new[] { layer });
    }

    /// <summary>
    /// Exports the ownership map of one layer's elements at the <see cref="DefaultExportSize"/>
    /// (see <see cref="ExportOwnershipMap(DrawingLayer, Size)"/>).
    /// </summary>
    /// <param name="layer">One of this session's layers.</param>
    /// <returns>A new map.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layer"/> is not one of this session's layers.</exception>
    public DrawingOwnershipMap ExportOwnershipMap(DrawingLayer layer) => ExportOwnershipMap(layer, DefaultExportSize);

    private void CheckOwnLayer(DrawingLayer layer)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        bool owned;
        lock (_layersLocker) { owned = _layers.Contains(layer); }
        if (!owned) { throw new ArgumentException("The layer does not belong to this session.", nameof(layer)); }
    }

    #endregion

    #region Safe areas

    /// <summary>
    /// Checks the marked elements against a safe-area guide for a frame of the
    /// <see cref="DefaultExportSize"/>; see <see cref="CheckSafeArea(SafeAreaGuide, Size, SafeAreaCheckOptions)"/>.
    /// </summary>
    /// <param name="guide">The guide to check against.</param>
    /// <param name="options">Optional settings (duration, which elements); or <c>null</c> for the defaults.</param>
    /// <returns>The findings; empty when every checked element is clear.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="guide"/> is null.</exception>
    public IReadOnlyList<SafeAreaFinding> CheckSafeArea(SafeAreaGuide guide, SafeAreaCheckOptions options = null)
        => CheckSafeArea(guide, DefaultExportSize, options);

    /// <summary>
    /// Checks the elements that matter against a safe-area guide, for frames of the given
    /// size: every element whose own <see cref="DrawingElement.IsSafeAreaChecked"/> flag is
    /// set, or that sits on a layer whose <see cref="DrawingLayer.IsSafeAreaChecked"/> flag
    /// is set (or every element, with <see cref="SafeAreaCheckOptions.CheckAllElements"/>),
    /// is measured by its bounds and compared with each unsafe zone the guide lays out on
    /// the frame. The frame is the whole exported image; the drawing occupies its centered
    /// aspect-fit rectangle, exactly as in an export. Purely advisory: the check never
    /// changes the drawing and never throws because of what it finds. Elements whose
    /// bounds are unknown (custom shapes that do not override
    /// <see cref="DrawingShape.GetBounds"/>) are skipped silently unless
    /// <see cref="SafeAreaCheckOptions.ReportUncheckedElements"/> asks for them to be listed.
    /// <para>
    /// "NOW" IS <see cref="Time"/>: the check looks at the session as it stands at its current
    /// Time - that one moment decides which time-keyed elements exist, where animated and
    /// transformed elements are, and which of the guide's time rules apply. To check another
    /// moment, set <see cref="Time"/> first.
    /// </para>
    /// </summary>
    /// <param name="guide">The guide to check against (a preset from <see cref="SafeAreaGuides"/>, a custom guide, or a combination).</param>
    /// <param name="frameSize">The pixel size of the frames the drawing will be exported at.</param>
    /// <param name="options">Optional settings (duration, which elements); or <c>null</c> for the defaults.</param>
    /// <returns>
    /// The findings, in layer order then element order - one per element per zone it
    /// reaches into; empty when every checked element is clear.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="guide"/> is null.</exception>
    public IReadOnlyList<SafeAreaFinding> CheckSafeArea(SafeAreaGuide guide, Size frameSize, SafeAreaCheckOptions options = null)
    {
        if (guide == null) { throw new ArgumentNullException(nameof(guide)); }
        ThrowIfDisposed();
        options ??= new SafeAreaCheckOptions();

        var findings = new List<SafeAreaFinding>();
        if (frameSize.Width < 1 || frameSize.Height < 1) { return findings; }

        double time = _renderer.Time;
        IReadOnlyList<SafeAreaZone> zones = guide.GetUnsafeZones(
            new SizeF(frameSize.Width, frameSize.Height), time, options.Duration);
        Size calibration = CalibrationSize;
        RectangleF drawingRect = CanvasCalibration.GetDrawingRect(frameSize, calibration);
        float scale = drawingRect.Width / calibration.Width;
        if (!(scale > 0f)) { return findings; }

        foreach (DrawingLayer layer in Layers)
        {
            if (!layer.IsVisible) { continue; }
            Matrix3x2 view = _renderer.GetLayerViewMatrix(layer);
            DrawingElement[] elements = layer.GetElements();
            for (int i = 0; i < elements.Length; i++)
            {
                DrawingElement element = elements[i];
                if (!options.CheckAllElements && !layer.IsSafeAreaChecked && !element.IsSafeAreaChecked) { continue; }
                if (element.IsDynamic && !element.IsVisibleAt(time)) { continue; }

                RectangleF? bounds = GetElementBounds(element, time, view);
                if (!bounds.HasValue)
                {
                    if (options.ReportUncheckedElements)
                    {
                        findings.Add(new SafeAreaFinding(SafeAreaFindingKind.CouldNotCheck, layer, element, i, null, null, null));
                    }
                    continue;
                }

                RectangleF b = bounds.Value;
                var frameBounds = new RectangleF(
                    drawingRect.X + (b.X * scale), drawingRect.Y + (b.Y * scale), b.Width * scale, b.Height * scale);
                foreach (SafeAreaZone zone in zones)
                {
                    RectangleF overlap = RectangleF.Intersect(frameBounds, zone.Bounds);
                    if (overlap.Width <= 0f || overlap.Height <= 0f) { continue; }

                    var calibratedOverlap = new RectangleF(
                        (overlap.X - drawingRect.X) / scale, (overlap.Y - drawingRect.Y) / scale,
                        overlap.Width / scale, overlap.Height / scale);
                    findings.Add(new SafeAreaFinding(SafeAreaFindingKind.Overlap, layer, element, i, zone, calibratedOverlap, overlap));
                }
            }
        }
        return findings;
    }

    //An element's bounds as the frame sees them: its own bounds (for a group, of the children
    //  that exist at the time), through its own transform, then through its layer's view
    //  (the layer transform and the camera); plain elements on plain layers are untouched
    private static RectangleF? GetElementBounds(DrawingElement element, double time, Matrix3x2 view)
    {
        try
        {
            RectangleF? bounds;
            if (element is DrawingGroup group)
            {
                bounds = group.GetBoundsAt(time);
                if (bounds.HasValue && !group.Transform.IsIdentity) { bounds = group.Transform.TransformBounds(bounds.Value); }
            }
            else
            {
                bounds = element.GetTransformedBounds();
            }
            if (bounds.HasValue && !view.IsIdentity) { bounds = DrawingTransform.TransformBounds(bounds.Value, view); }
            if (bounds.HasValue)
            {
                RectangleF b = bounds.Value;
                if (!Single.IsFinite(b.X) || !Single.IsFinite(b.Y) || !Single.IsFinite(b.Width) || !Single.IsFinite(b.Height)
                    || b.Width < 0f || b.Height < 0f)
                {
                    return null;
                }
            }
            return bounds;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            //A custom shape whose bounds cannot be computed counts as "unknown", never as a failure
            return null;
        }
    }

    #endregion

    #region Clearing and undo

    /// <summary>
    /// Removes all strokes from every layer and discards any in-progress stroke. Layers
    /// themselves (and the active-layer selection) are kept.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        _activeStroke = null;
        _activeStrokeLayer = null;
        _activeStrokeTimer = null;

        lock (_layersLocker)
        {
            foreach (DrawingLayer layer in _layers)
            {
                layer.Clear();
            }
            _strokeOrder.Clear();
        }

        RaiseDrawingChanged();
        RaiseRedrawRequested();
    }

    /// <summary>
    /// Removes the most recently committed element (stroke or shape), regardless of which
    /// layer it was committed to.
    /// </summary>
    /// <returns><c>true</c> when an element was removed.</returns>
    public bool UndoLastStroke()
    {
        ThrowIfDisposed();

        var removed = false;
        lock (_layersLocker)
        {
            while (_strokeOrder.Count > 0 && !removed)
            {
                DrawingLayer layer = _strokeOrder[_strokeOrder.Count - 1];
                _strokeOrder.RemoveAt(_strokeOrder.Count - 1);
                removed = layer.RemoveLastElement();
            }
        }

        if (removed)
        {
            RaiseDrawingChanged();
            RaiseRedrawRequested();
        }
        return removed;
    }

    #endregion

    //Rebuilds animated shapes for a time - on a layer, inside groups, and inside what an
    //  animated shape built - skipping plain elements without looking inside them
    private static void UpdateAnimatedShapes(DrawingElement element, double seconds, int depth)
    {
        if (!element.IsDynamic) { return; }
        if (depth > 64)
        {
            throw new InvalidOperationException("Groups and animated shapes are nested too deeply (does one contain itself?).");
        }

        if (element is AnimatedShape animated)
        {
            animated.Update(seconds);
            DrawingShape current = animated.Current;
            if (current != null) { UpdateAnimatedShapes(current, seconds, depth + 1); }
        }
        else if (element is DrawingGroup group)
        {
            foreach (DrawingElement child in group.GetChildren())
            {
                UpdateAnimatedShapes(child, seconds, depth + 1);
            }
        }
    }

    private void RaiseRedrawRequested() => RedrawRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseDrawingChanged() => DrawingChanged?.Invoke(this, EventArgs.Empty);

    private void DisposeOwnedBackground()
    {
        if (_ownedBackgroundImage != null)
        {
            if (ReferenceEquals(_renderer.BackgroundImage, _ownedBackgroundImage))
            {
                _renderer.BackgroundImage = null;
            }
            _ownedBackgroundImage.Dispose();
            _ownedBackgroundImage = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) { throw new ObjectDisposedException(nameof(DrawingSession)); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;

        RedrawRequested = null;
        DrawingChanged = null;

        _activeStroke = null;
        _activeStrokeLayer = null;
        _activeStrokeTimer = null;

        _renderer.Dispose();
        DisposeOwnedBackground();
    }
}
