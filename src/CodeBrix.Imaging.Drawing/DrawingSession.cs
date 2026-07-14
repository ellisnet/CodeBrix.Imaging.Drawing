using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.Shapes;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// The main entry point of the CodeBrix.Imaging.Drawing library: an interactive drawing
/// surface model that turns pointer (mouse, pen, or touch) events into calibrated strokes
/// on named, colored <see cref="DrawingLayer"/> collections, renders them onto any
/// SkiaSharp canvas with translucent "highlighter" compositing, and exports the finished
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

    /// <summary>
    /// The size of the calibrated drawing space that all of the session's strokes are
    /// expressed in.
    /// </summary>
    public Size CalibrationSize => _renderer.CalibrationSize;

    /// <summary>Gets <see cref="CalibrationSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The calibration size as a SkiaSharp size.</returns>
    public SKSizeI GetCalibrationSizeAsSkia() => _renderer.GetCalibrationSizeAsSkia();

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
    /// The color that the drawing rectangle is filled with before the background image and
    /// layers are drawn.
    /// </summary>
    public Color BackgroundFillColor
    {
        get => _renderer.BackgroundFillColor;
        set => _renderer.BackgroundFillColor = value;
    }

    /// <summary>Sets <see cref="BackgroundFillColor"/> from a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <param name="color">The background fill color.</param>
    public void SetBackgroundFillColor(SKColor color) => _renderer.SetBackgroundFillColor(color);

    /// <summary>Gets <see cref="BackgroundFillColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The background fill color as a SkiaSharp color.</returns>
    public SKColor GetBackgroundFillColorAsSkia() => _renderer.GetBackgroundFillColorAsSkia();

    /// <summary>
    /// The color that the whole canvas is cleared to at the start of every render.
    /// </summary>
    public Color SurfaceClearColor
    {
        get => _renderer.SurfaceClearColor;
        set => _renderer.SurfaceClearColor = value;
    }

    /// <summary>Sets <see cref="SurfaceClearColor"/> from a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <param name="color">The surface clear color.</param>
    public void SetSurfaceClearColor(SKColor color) => _renderer.SetSurfaceClearColor(color);

    /// <summary>Gets <see cref="SurfaceClearColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The surface clear color as a SkiaSharp color.</returns>
    public SKColor GetSurfaceClearColorAsSkia() => _renderer.GetSurfaceClearColorAsSkia();

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
        => CreateForImage(encodedImage, SkiaInterop.ToSK(calibrationSize), options);

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
        => CreateForImage(image, SkiaInterop.ToSK(calibrationSize), options);

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
        => CreateForImage(bgraPixels, width, height, SkiaInterop.ToSK(calibrationSize), options, mirrorHorizontally);

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
                return (options ?? new DrawingSessionOptions()).GetCalibrationSizeAsSkia();

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
            CalibrationSize = SkiaInterop.ToImaging(calibrationSize),
        };
        if (options != null)
        {
            effective.LayerOpacity = options.LayerOpacity;
            effective.ActiveStrokeOpacity = options.ActiveStrokeOpacity;
            effective.BackgroundFillColor = options.BackgroundFillColor;
            effective.SurfaceClearColor = options.SurfaceClearColor;
            effective.StrokeWidth = options.StrokeWidth;
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
    public DrawingLayer AddLayer(string name, Color color) => AddLayer(name, SkiaInterop.ToSK(color));

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
            viewPoint, viewSize, _renderer.GetLastCanvasSizeAsSkia(), GetCalibrationSizeAsSkia());
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
        => PointerPressed(SkiaInterop.ToSK(viewPoint), SkiaInterop.ToSK(viewSize));

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
            viewPoint, viewSize, _renderer.GetLastCanvasSizeAsSkia(), GetCalibrationSizeAsSkia(), clampToDrawingArea: true);
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
        => PointerMoved(SkiaInterop.ToSK(viewPoint), SkiaInterop.ToSK(viewSize));

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
        SKSizeI calibration = GetCalibrationSizeAsSkia();
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
    /// <param name="thickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
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
    /// <param name="thickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
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
    /// <param name="thickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
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
    /// <param name="thickness">The outline thickness, in calibrated drawing units (ignored when filled).</param>
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
    /// <param name="surface">The Skia surface to render onto.</param>
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
    /// <param name="canvas">The Skia canvas to render onto.</param>
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
        _renderer.Render(canvas, info, Layers, _activeStroke, _activeStrokeLayer?.GetColorAsSkia(), clearCanvas);
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
        => SkiaInterop.ToImaging(GetDrawingRect(SkiaInterop.ToSK(viewSize)));

    /// <summary>
    /// The rectangle that the drawing occupies within a view (or canvas) of the given size -
    /// the SkiaSharp-typed companion of <see cref="GetDrawingRect(SizeF)"/>.
    /// </summary>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <returns>The drawing's aspect-fit rectangle; empty when <paramref name="viewSize"/> is unusable.</returns>
    public SKRect GetDrawingRect(SKSize viewSize)
        => CanvasCalibration.GetDrawingRect(viewSize, GetCalibrationSizeAsSkia());

    /// <summary>
    /// Scales a length from calibrated drawing units to view (or canvas) units for a view
    /// of the given size - for example to draw a cursor ring whose radius matches what a
    /// stroke of that calibrated width will actually cover on screen.
    /// </summary>
    /// <param name="calibratedLength">The length, in calibrated drawing units (e.g. a stroke width or brush radius).</param>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <returns>The equivalent length in view units; the input value when the sizes are unusable.</returns>
    public float ScaleToView(float calibratedLength, SizeF viewSize)
        => ScaleToView(calibratedLength, SkiaInterop.ToSK(viewSize));

    /// <summary>
    /// Scales a length from calibrated drawing units to view (or canvas) units - the
    /// SkiaSharp-typed companion of <see cref="ScaleToView(float, SizeF)"/>.
    /// </summary>
    /// <param name="calibratedLength">The length, in calibrated drawing units.</param>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <returns>The equivalent length in view units; the input value when the sizes are unusable.</returns>
    public float ScaleToView(float calibratedLength, SKSize viewSize)
    {
        SKRect drawingRect = GetDrawingRect(viewSize);
        if (drawingRect.IsEmpty || calibratedLength <= 0) { return calibratedLength; }

        SKSizeI calibration = GetCalibrationSizeAsSkia();
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

    /// <summary>Gets <see cref="DefaultExportSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The default export size as a SkiaSharp size.</returns>
    public SKSizeI GetDefaultExportSizeAsSkia() => SkiaInterop.ToSK(DefaultExportSize);

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
        => ExportImage(SkiaInterop.ToSK(outputSize), includeBackground);

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
        => ExportPng(SkiaInterop.ToSK(outputSize), includeBackground);

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
        => ExportJpeg(SkiaInterop.ToSK(outputSize), quality);

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
        => ExportPng(destination, SkiaInterop.ToSK(outputSize), includeBackground);

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
