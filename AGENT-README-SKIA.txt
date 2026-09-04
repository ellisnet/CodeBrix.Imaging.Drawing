================================================================================
AGENT-README: CodeBrix.Imaging.Drawing
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Imaging.Drawing.ApacheLicenseForever NuGet package
================================================================================

OVERVIEW
========

CodeBrix.Imaging.Drawing is a stroke-based drawing, painting and highlighting
library for SkiaSharp canvases, for .NET 10 or later. It provides:

- Interactive freehand drawing: pointer (mouse, pen, or touch) input is
  captured as resolution-independent calibrated strokes on named, colored
  layers.
- Programmatic drawing primitives: lines, arrows, circles, ellipses,
  rectangles, and polylines/polygons drawn with plain coordinates and
  CodeBrix.Imaging colors - no SkiaSharp knowledge required - ideal for
  computer-vision-driven annotation of a live video feed.
- Translucent "highlighter" rendering over a background image, over a solid
  fill, or over a fully transparent canvas above live content such as a
  webcam video feed (the "telestrator" scenario).
- Export of the finished drawing as PNG/JPEG bytes, a SkiaSharp SKImage, or
  a CodeBrix.Imaging Image<Rgba32>.

The library is UI-framework-agnostic: it never references any UI framework.
A hosting view (CodeBrix.Platform SKXamlCanvas, native WinUI 3 SKXamlCanvas,
WPF SKElement, MAUI SKCanvasView, etc.) forwards its pointer events and
paint callbacks to a DrawingSession, and the session raises RedrawRequested
whenever the view should invalidate.

Key rendering idea - the "highlighter" effect: each layer's elements
(strokes and shapes) are drawn fully OPAQUE onto a private transparent cache
bitmap, and the whole cache is then composited over the background at the
layer opacity (default alpha 100/255). Overlapping elements within one layer
therefore never darken each other, which is what makes translucent ink read
as highlighter rather than marker scribble. Set LayerOpacity = 255 for
opaque whiteboard-marker ink.

Key coordinate idea - the calibrated drawing space: all strokes and shapes
are stored in a fixed logical space (CalibrationSize, default 1000 x 1000,
any width x height allowed), never in screen pixels. The renderer maps that
space to the centered aspect-fit rectangle of whatever canvas it is given,
so a drawing survives window resizing, DPI changes, and orientation flips,
and exports at any resolution.

Performance: the renderer caches (a) the background image pre-scaled to the
current canvas size, (b) one bitmap per layer, drawn incrementally (only new
elements are rasterized), and (c) the fully composited static scene. The
per-frame cost while the user is actively drawing is one 1:1 bitmap blit
plus the in-progress stroke (~4 ms at desktop sizes), so live drawing feels
immediate even over a 3100 x 3100 background image.

A COMPLETELY MANAGED companion package with the identical drawing-session
API is in development in the same repository, documented in
AGENT-README-NOSKIA.txt. It is NOT yet published on nuget.org, so it cannot
be referenced today; see "WHICH ONE DO I REFERENCE" under INSTALLATION.

INSTALLATION
============

NuGet Package: CodeBrix.Imaging.Drawing.ApacheLicenseForever

    dotnet add package CodeBrix.Imaging.Drawing.ApacheLicenseForever

- The package ID carries the ".ApacheLicenseForever" suffix - a permanent
  guarantee that this package ID will only ever be published under the
  Apache-2.0 license. Namespaces do NOT carry the suffix: code uses the
  CodeBrix.Imaging.Drawing namespace.
- License: Apache-2.0.
- Requires .NET 10 or later.
- NuGet dependencies (restored automatically):
    SkiaSharp
    CodeBrix.Imaging.ApacheLicenseForever
- Native requirement: applications must provide the SkiaSharp native assets
  for their platform. Desktop apps built on CodeBrix.Platform get them from
  their platform head package; plain .NET apps add
  SkiaSharp.NativeAssets.Linux / SkiaSharp.NativeAssets.macOS /
  SkiaSharp.NativeAssets.Win32 as appropriate.

WHICH ONE DO I REFERENCE
------------------------
Reference CodeBrix.Imaging.Drawing.ApacheLicenseForever - the package THIS
file documents. It is the only one of the repository's two packages that is
published on nuget.org. Apache-2.0. SkiaSharp-backed: Skia's performance and
the full non-drawing Skia surface alongside it.

The same repository also produces a COMPLETELY MANAGED companion package,
CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever (Apache-2.0): the
identical drawing-session API plus a fully managed drawing engine and a
managed SVG renderer, with zero native dependence and slower CPU rendering.
It is a companion package, NOT yet published - it is not on nuget.org and
`dotnet add package` cannot restore it, so do not write project files that
reference it. Its guide is AGENT-README-NOSKIA.txt.

The two are EITHER/OR alternatives: once the companion ships, NEVER
reference both packages from one application, because they compile the SAME
drawing-session source files into the SAME CodeBrix.Imaging.Drawing
namespaces (deliberately, so switching is a package swap).

KEY NAMESPACES / USINGS
=======================

    using CodeBrix.Imaging.Drawing;             // DrawingSession, DrawingSessionOptions, CalibrationSizing
    using CodeBrix.Imaging.Drawing.Models;      // DrawingLayer, DrawingElement, Stroke, StrokePoint
    using CodeBrix.Imaging.Drawing.Shapes;      // DrawingShape + Line/Arrow/Circle/Ellipse/Rectangle/Polyline shapes
    using CodeBrix.Imaging.Drawing.Rendering;   // DrawingRenderer, CanvasCalibration
    using CodeBrix.Imaging.Drawing.Extensions;  // CodeBrix.Imaging image + color + geometry bridge extensions

Because CodeBrix.Imaging.Drawing nests under the CodeBrix.Imaging namespace,
the CodeBrix.Imaging `Color` type resolves as just `Color` inside consuming
code that uses these namespaces. (Add an explicit `using CodeBrix.Imaging;`
when your own code lives in an unrelated namespace, so that `Color`, `Size`,
`SizeF`, `Point`, `PointF` and `RectangleF` resolve.)

Public types (19): DrawingSession, DrawingSessionOptions, CalibrationSizing
(root); DrawingElement, Stroke, StrokePoint, DrawingLayer (Models);
DrawingShape, LineShape, ArrowShape, CircleShape, EllipseShape,
RectangleShape, PolylineShape (Shapes); DrawingRenderer, CanvasCalibration
(Rendering); ColorBridgeExtensions, GeometryBridgeExtensions,
ImagingBridgeExtensions (Extensions). Every one is documented below.

SKIASHARP-FREE BY DEFAULT (AND SKIASHARP-FRIENDLY WHEN YOU WANT IT)
===================================================================

The goal of this library's public surface is that a consumer never has to
write a `using SkiaSharp;` line - EXCEPT where it hands SkiaSharp the actual
drawing surface (the `SKCanvas`/`SKSurface`/`SKImageInfo` passed to `Render`,
and the `Draw(SKCanvas, SKColor)` override when authoring a custom shape).
Everywhere else, colors, sizes, points and rectangles are expressed with the
CodeBrix.Imaging value types, which map one-to-one to their SkiaSharp
counterparts:

    CodeBrix.Imaging     SkiaSharp        used for
    ------------------   --------------   ------------------------------
    Color                SKColor          layer/shape/fill/clear colors
    Size                 SKSizeI          calibration + export sizes
    SizeF                SKSize           view sizes (pointer input)
    Point                SKPointI         calibrated integer points
    PointF               SKPoint          view points (pointer input)
    RectangleF           SKRect           drawing rectangles

This is the DEFAULT and NORMAL way to use the library - every example in
this document uses the CodeBrix.Imaging types.

The ability to work in SkiaSharp types is deliberately PRESERVED, though,
for callers who already hold them (for example values that came from a
SkiaSharp.Views canvas). Two mechanisms:

  1. Method overloads and helpers. Every method that takes a CodeBrix.Imaging
     value type has a sibling overload taking the SkiaSharp type - the full
     overload sets are enumerated in CORE API REFERENCE below. Properties
     cannot be overloaded by type, so each property that is a
     CodeBrix.Imaging type also has a `Set…(SKColor/SKSizeI)` helper (or a
     fluent one on DrawingSessionOptions) for writing and a `Get…AsSkia()`
     method for reading. For example:
         session.BackgroundFillColor = Color.White;      // default path
         session.SetBackgroundFillColor(SKColors.White); // Skia path
         SKColor c = session.GetBackgroundFillColorAsSkia();

  2. Bridge extension methods (namespace CodeBrix.Imaging.Drawing.Extensions)
     convert between the two worlds in one call, both directions - see
     "Bridge extensions" below:
         SKColor sk = session.BackgroundFillColor.ToSKColor();
         Size    s  = someSkSizeI.ToImagingSize();

KEEPING SKIASHARP OUT OF THE HOSTING APP'S XAML (THE DrawingCanvas TRICK)
=========================================================================

The section above keeps SkiaSharp out of your model and view-model code. A
XAML host still normally names a SkiaSharp view control in its markup -
SkiaSharp.Views.Windows.SKXamlCanvas on WinUI-dialect XAML (CodeBrix.Platform
Skia heads and native WinUI 3) and SkiaSharp.Views.WPF.SKElement on WPF - so
each head's XAML carries a SkiaSharp xmlns and a per-framework control name.

You can hide all of that behind ONE control name that is identical in every
head's XAML - <drawing:DrawingCanvas/> - by adding a single small source
file to your app and letting conditional compilation pick the correct base
control per head. The same file also carries a DrawCanvasHelper with two
tiny converters, so the hosting page's code-behind can wire the canvas
without ever NAMING a SkiaSharp type - which keeps `using SkiaSharp;` out of
the code-behind entirely. The file (this is the PainDiagram reference
application's Shared/Drawing/DrawingCanvas.cs; put it in whatever namespace
you like):

    namespace CodeBrix.Imaging.Drawing;

    /// <summary>
    /// SkiaSharp-based drawing surface, abstracted so a single control name -
    /// <c>&lt;drawing:DrawingCanvas /&gt;</c> - can be used in the XAML of every head. This one
    /// linked source file is compiled into each head's assembly and resolves to the correct
    /// base control for that head via conditional compilation:
    /// <list type="bullet">
    ///   <item>CodeBrix.Platform Skia heads (which should have HAS_CODEBRIXPLATFORM defined on
    ///   their shared assembly); and native WinUI 3 (which should have HAS_WINUI defined):
    ///   SkiaSharp.Views.Windows.SKXamlCanvas.</item>
    ///   <item>native WPF (neither symbol): SkiaSharp.Views.WPF.SKElement.</item>
    /// </list>
    /// It is a plain subclass that carries no extra behavior - the hosting page's code-behind
    /// wires PaintSurface and the pointer/mouse events to the DrawingSession exactly as before.
    /// </summary>
    #if (HAS_CODEBRIXPLATFORM || HAS_WINUI)
    public class DrawingCanvas : SkiaSharp.Views.Windows.SKXamlCanvas { }
    #else
    public class DrawingCanvas : SkiaSharp.Views.WPF.SKElement { }
    #endif

    public static class DrawCanvasHelper
    {
        public static SkiaSharp.SKSize GetViewSize(this DrawingCanvas canvas) =>
            (canvas == null)
            ? default
            : new SkiaSharp.SKSize((float)canvas.ActualWidth, (float)canvas.ActualHeight);

    #if (HAS_CODEBRIXPLATFORM || HAS_WINUI)
        public static SkiaSharp.SKPoint GetPointFromPosition(Windows.Foundation.Point point) =>
            new ((float)point.X, (float)point.Y);
    #else
        public static SkiaSharp.SKPoint GetPointFromPosition(System.Windows.Point point) =>
            new ((float)point.X, (float)point.Y);
    #endif
    }

How to use it:
  1. Add this one file to your app as a LINKED source file - exactly like a
     shared view-model file. Link it into each ASSEMBLY that has to compile
     it: your shared CodeBrix.Platform library (the Skia head executables
     then inherit it through their reference to that library), and each
     native head project. In PainDiagram that is three assemblies -
     PainDiagram.Core, PainDiagram.WinUI, and PainDiagram.Wpf.
  2. Define the right compile symbol so the correct branch is selected.
     Define each symbol ONCE, on the assembly that actually compiles the
     file - never repeat it across executables that share that code:
       * HAS_CODEBRIXPLATFORM - for CodeBrix.Platform Skia apps, define it on
         the ONE shared library that holds the linked file and references
         CodeBrix.Platform.SkiaSharp.Views.MitLicenseForever (in PainDiagram,
         PainDiagram.Core). Do NOT define it on the individual Skia head
         executables - they inherit it through their reference to that
         shared library.
       * HAS_WINUI            - on the native WinUI 3 head (references
         SkiaSharp.Views.WinUI)
       * neither symbol       - on the native WPF head (references
         SkiaSharp.Views.WPF)
     For example, in the appropriate .csproj:
       <DefineConstants>$(DefineConstants);HAS_CODEBRIXPLATFORM</DefineConstants>
  3. Reference it in XAML with the namespace the file declares, and give it
     just an x:Name - NO event attributes. The xmlns form differs by XAML
     dialect, but the element tag is identical:
       WinUI dialect: xmlns:drawing="using:CodeBrix.Imaging.Drawing"
       WPF dialect:   xmlns:drawing="clr-namespace:CodeBrix.Imaging.Drawing"
       <drawing:DrawingCanvas x:Name="DrawCanvas" />
  4. Wire the canvas in the page's code-behind constructor (after
     InitializeComponent) with lambdas. Because the lambda's event-args and
     the helper's return values are never NAMED, the code-behind needs no
     `using SkiaSharp;` and no `using SkiaSharp.Views.*`. WinUI dialect
     (native WPF is the same shape with MouseDown/Move/Up + CaptureMouse):
       DrawCanvas.PaintSurface += (_, e) => ViewModel?.Session?.Render(e.Surface, e.Info);
       DrawCanvas.PointerPressed += (_, e) =>
       {
           var session = ViewModel?.Session;
           if (session == null) { return; }
           var p = e.GetCurrentPoint(DrawCanvas);
           if (!p.Properties.IsLeftButtonPressed) { return; }
           if (session.PointerPressed(DrawCanvasHelper.GetPointFromPosition(p.Position),
                                      DrawCanvas.GetViewSize()))
           {
               DrawCanvas.CapturePointer(e.Pointer);
               e.Handled = true;
           }
       };
       DrawCanvas.PointerMoved += (_, e) =>
       {
           var session = ViewModel?.Session;
           if (session is not { IsPointerActive: true }) { return; }
           session.PointerMoved(DrawCanvasHelper.GetPointFromPosition(e.GetCurrentPoint(DrawCanvas).Position),
                                DrawCanvas.GetViewSize());
           e.Handled = true;
       };
       DrawCanvas.PointerReleased += (_, e) =>
       {
           var session = ViewModel?.Session;
           if (session is not { IsPointerActive: true }) { return; }
           session.PointerReleased();
           DrawCanvas.ReleasePointerCapture(e.Pointer);
           e.Handled = true;
       };
       // capture lost mid-stroke (e.g. the window deactivates): discard the stroke
       DrawCanvas.PointerCaptureLost += (_, _) => ViewModel?.Session?.PointerCanceled();
       DrawCanvas.SizeChanged += (_, _) => DrawCanvas.Invalidate();
     The redraw side stays wired through the session's RedrawRequested event
     (in PainDiagram, via the view model's ICanvasInvalidator bridge).

Why this keeps the code-behind SkiaSharp-free: DrawingCanvas DERIVES from the
platform control, so every member (Invalidate/InvalidateVisual,
CapturePointer/CaptureMouse, ActualWidth/Height, ...) is inherited. The
lambda's `e` is inferred (SKPaintSurfaceEventArgs / a pointer-args type), so
it is never spelled out; and DrawCanvasHelper.GetPointFromPosition /
GetViewSize return the SKPoint/SKSize that session.PointerPressed/Moved
want, so the code-behind never names a SkiaSharp type. (The pointer input is
fed as SKPoint/SKSize to the SkiaSharp overloads here; you may instead use
the PointF/SizeF overloads and skip the helper entirely.)

Scope of the trick: the one genuine SkiaSharp touchpoint - the drawing
surface handed to session.Render(e.Surface, e.Info) - still exists, but it
now lives inside an inferred-type lambda in this reusable file, not spread
across your pages. Net result: clean one-line XAML, and code-behinds (and
view-models) with no `using SkiaSharp;` at all.

CORE API REFERENCE
==================

DrawingSession (root namespace; sealed; IDisposable)
----------------------------------------------------
The interactive drawing surface model. One session = one drawing.

Construction:
    public DrawingSession(DrawingSessionOptions options = null)

    var session = new DrawingSession();                    // defaults
    var session = new DrawingSession(new DrawingSessionOptions
    {
        CalibrationSize = new Size(1000, 1000),    // logical stroke space - ANY
                                                   //   width x height; match it to the
                                                   //   background's aspect ratio
        LayerOpacity = 100,                        // highlighter alpha (255 = opaque)
        ActiveStrokeOpacity = 200,                 // in-progress stroke alpha
        BackgroundFillColor = Color.White,         // behind the image
        SurfaceClearColor = Color.Transparent,     // whole-canvas clear
        StrokeWidth = 15f,                         // calibrated units
    });
    // A caller holding SkiaSharp values can use the fluent Skia setters instead:
    //   new DrawingSessionOptions().SetCalibrationSize(new SKSizeI(1000, 1000))
    //       .SetBackgroundFillColor(SKColors.White)

Constants:
    public const int CalibrationLongSide = 1000;
    // The longest side, in calibrated units, of a drawing space derived by
    // the CreateForImage factories with CalibrationSizing.DeriveFromBackgroundImage.

Image-annotation factories (photos of any aspect ratio) - all nine:
    static DrawingSession CreateForImage(byte[] encodedImage, Size calibrationSize, DrawingSessionOptions options = null)
    static DrawingSession CreateForImage(byte[] encodedImage, SKSizeI calibrationSize, DrawingSessionOptions options = null)
    static DrawingSession CreateForImage(byte[] encodedImage, CalibrationSizing sizing, DrawingSessionOptions options = null)
    static DrawingSession CreateForImage(SKBitmap image, Size calibrationSize, DrawingSessionOptions options = null)
    static DrawingSession CreateForImage(SKBitmap image, SKSizeI calibrationSize, DrawingSessionOptions options = null)
    static DrawingSession CreateForImage(SKBitmap image, CalibrationSizing sizing, DrawingSessionOptions options = null)
    static DrawingSession CreateForImage(byte[] bgraPixels, int width, int height, Size calibrationSize,
        DrawingSessionOptions options = null, bool mirrorHorizontally = false)
    static DrawingSession CreateForImage(byte[] bgraPixels, int width, int height, SKSizeI calibrationSize,
        DrawingSessionOptions options = null, bool mirrorHorizontally = false)
    static DrawingSession CreateForImage(byte[] bgraPixels, int width, int height, CalibrationSizing sizing,
        DrawingSessionOptions options = null, bool mirrorHorizontally = false)
    // The raw-pixels overloads take a tightly packed 32-bit BGRA buffer - exactly what
    // webcam and video-capture libraries produce (e.g. CodeBrix.Webcam's WebcamPhoto.
    // PixelsBgra32) - so "annotate a captured photo" needs no PNG round-trip. The pixels
    // are COPIED (reuse your buffer immediately) and the session owns the bitmap.
    // mirrorHorizontally flips the image left-to-right, for stills that must read like a
    // mirror because the user watched a mirrored ("selfie") live preview when capturing.
    // Decodes/uses the image as the session's background. The calibration size
    // is ALWAYS an explicit caller choice - there is no automatic behavior:
    //   * pass a Size/SKSizeI to state the drawing space inline (match its
    //     aspect ratio to the image's to avoid stretching), or
    //   * pass CalibrationSizing.DeriveFromBackgroundImage to compute it from
    //     the image's aspect ratio (longest side = CalibrationLongSide = 1000)
    //     so the image never displays or exports distorted, or
    //   * pass CalibrationSizing.FromOptions to use options.CalibrationSize
    //     exactly as given (or the documented 1000x1000 default).
    // The byte[] overloads' decoded bitmap is owned by the session; the
    // SKBitmap overloads leave ownership with the caller.
    // Note: EXIF orientation is NOT applied - normalize photo orientation first
    // (CodeBrix.Imaging: image.Mutate(x => x.AutoOrient())).

Layers:
    DrawingLayer AddLayer(string name, Color color)    // first added becomes ActiveLayer
    DrawingLayer AddLayer(string name, SKColor color)
    DrawingLayer GetLayer(string name)
    bool RemoveLayer(DrawingLayer layer)
    IReadOnlyList<DrawingLayer> Layers { get; }
    DrawingLayer ActiveLayer { get; set; }             // strokes/shapes commit here

Background and appearance:
    void SetBackgroundImage(byte[] encodedImage)  // decodes; session owns the bitmap
    void SetBackgroundImage(byte[] bgraPixels, int width, int height,
        bool mirrorHorizontally = false)          // raw 32-bit BGRA pixels (webcam frames);
                                                  //   copied, session owns the bitmap.
                                                  //   NOTE: does not change CalibrationSize -
                                                  //   use a raw-pixels CreateForImage factory
                                                  //   to derive the drawing space instead
    SKBitmap BackgroundImage { get; set; }        // caller-owned alternative (image type)
    Color BackgroundFillColor { get; set; }       // void SetBackgroundFillColor(SKColor color)
                                                  //   SKColor GetBackgroundFillColorAsSkia()
    Color SurfaceClearColor { get; set; }         // void SetSurfaceClearColor(SKColor color)
                                                  //   SKColor GetSurfaceClearColorAsSkia()
    Size CalibrationSize { get; }                 // SKSizeI GetCalibrationSizeAsSkia()
                                                  //   (read-only; fixed at construction)
    byte LayerOpacity { get; set; }               // 100 default; 255 = opaque ink
    byte ActiveStrokeOpacity { get; set; }        // 200 default
    float StrokeWidth { get; set; }               // width of NEW strokes, calibrated units
    // Leave both colors transparent and the image null to highlight over
    // externally drawn content (e.g. a live video frame the host draws
    // before calling Render).

Pointer input (forward from the hosting view; viewPoint/viewSize are in the
control's logical coordinates - the session handles DPI scaling):
    bool PointerPressed(PointF viewPoint, SizeF viewSize)
    bool PointerPressed(SKPoint viewPoint, SKSize viewSize)
    bool PointerMoved(PointF viewPoint, SizeF viewSize)
    bool PointerMoved(SKPoint viewPoint, SKSize viewSize)
    bool PointerReleased()
    void PointerCanceled()
    bool IsPointerActive { get; }
    // A press outside the drawing area returns false (no stroke started).
    // Moves are clamped to the drawing area. A press+release without
    // movement commits a single-point dot. Strokes commit to the layer
    // that was active at PRESS time, even if ActiveLayer changes mid-stroke.
    // PointerPressed requires one prior Render call (it needs the canvas
    // size to calibrate coordinates); before that it returns false.

Normalized (programmatic / vision-driven) stroke input - positions are 0..1
across the calibrated drawing space, NOT view coordinates:
    bool PointerPressedNormalized(float normX, float normY)
    bool PointerMovedNormalized(float normX, float normY)
    // The programmatic companions of PointerPressed/PointerMoved, for input
    // that does not come from a pointing device - e.g. computer-vision hand
    // or object tracking that reports positions as fractions of the image.
    // (0,0) = drawing-space top-left, (1,1) = bottom-right. They work in the
    // calibrated space directly, so they need NO view size and NO prior
    // Render call. A press outside 0..1 is ignored; moves clamp to the
    // edge; NaN is rejected - all mirroring the view-coordinate semantics.
    // Strokes complete through the same PointerReleased()/PointerCanceled(),
    // and normalized and view-driven input may be mixed freely, even within
    // one stroke. When the calibration size was derived from a background
    // photo (CreateForImage), normalized drawing-space coordinates ARE
    // normalized photo coordinates - a vision result maps straight in.

Rendering (call from the view's paint handler):
    void Render(SKSurface surface, SKImageInfo info, bool clearCanvas = true)
    void Render(SKCanvas canvas, SKImageInfo info, bool clearCanvas = true)
    // clearCanvas: false renders the drawing OVER whatever the caller already
    // drew on the canvas (e.g. a live video frame painted immediately before
    // this call) instead of clearing to SurfaceClearColor first.

Overlay positioning helpers (cursors, markers, hit regions that must line up
with the drawing):
    RectangleF GetDrawingRect(SizeF viewSize)
    SKRect     GetDrawingRect(SKSize viewSize)
    float ScaleToView(float calibratedLength, SizeF viewSize)
    float ScaleToView(float calibratedLength, SKSize viewSize)
    // GetDrawingRect returns the centered aspect-fit rectangle the drawing
    // occupies in a view (or canvas) of the given size - the same mapping the
    // renderer uses. A normalized drawing-space position (nx, ny) lands at
    // (rect.X + nx * rect.Width, rect.Y + ny * rect.Height). ScaleToView
    // converts a calibrated length (stroke width, brush radius) into view
    // units - e.g. to draw a cursor ring exactly as wide as the stroke that
    // painting at that spot will produce. Both are pure math passthroughs of
    // CanvasCalibration bound to the session's own CalibrationSize.

Live-video overlay ("telestrator") hosting - two patterns:
  1. Separate elements: put the video view underneath and a Skia canvas view
     on top; leave BackgroundImage null and both SurfaceClearColor and
     BackgroundFillColor transparent (the defaults), and the video shows
     through everywhere strokes were not drawn.
  2. Same canvas: in the paint handler, draw the current video frame first,
     then call session.Render(canvas, info, clearCanvas: false).
  For opaque whiteboard-marker ink instead of translucent highlighter ink,
  set LayerOpacity = 255 (and ActiveStrokeOpacity = 255).

Programmatic drawing primitives (no pointer input, no Skia knowledge needed):
    DrawingShape DrawLine(float x1, float y1, float x2, float y2,
        float thickness = 15, Color? color = null)
    DrawingShape DrawArrow(float x1, float y1, float x2, float y2,
        float thickness = 15, Color? color = null, float? headLength = null)
    DrawingShape DrawCircle(float centerX, float centerY, float radius,
        float thickness = 15, Color? color = null, bool filled = false)
    DrawingShape DrawEllipse(float centerX, float centerY, float radiusX, float radiusY,
        float thickness = 15, Color? color = null, bool filled = false)
    DrawingShape DrawRectangle(float x, float y, float width, float height,
        float thickness = 15, Color? color = null, bool filled = false,
        float cornerRadius = 0)
    DrawingShape DrawPolyline(IReadOnlyList<(float X, float Y)> points,
        float thickness = 15, Color? color = null,
        bool closed = false, bool filled = false)
    DrawingShape DrawShape(DrawingShape shape)   // pre-built or custom shape
    // Coordinates/thicknesses are calibrated drawing units; `Color?` is the
    // CodeBrix.Imaging Color type (null = the active layer's color, and a
    // shape with its own color still composites at the layer opacity).
    // Shapes commit to the ActiveLayer, raise RedrawRequested and
    // DrawingChanged, render/export exactly like strokes, and are undone by
    // UndoLastStroke(). Example (annotating a detected object on video):
    //   session.DrawCircle(500, 400, 120, thickness: 20,
    //       color: Color.FromRgb(255, 255, 255));
    //   session.DrawArrow(200, 800, 480, 520);
    // Committed elements are persistent marks; for shapes that move every
    // frame (e.g. tracking a detected object), draw them directly on the
    // canvas after session.Render instead of committing them.

State and events:
    bool HasStrokes { get; }             // any completed element (stroke or shape)
    int StrokeCount { get; }             // total elements across all layers
    bool IsDisposed { get; }
    event EventHandler RedrawRequested   // invalidate the hosting canvas
    event EventHandler DrawingChanged    // elements committed/cleared/undone

Clearing and undo:
    void Clear()                 // all layers; layers themselves are kept
    bool UndoLastStroke()        // most recent element (stroke OR shape), any layer

Export - all overloads:
    Size DefaultExportSize { get; }     // background image's pixel size when set
                                        //   (photo exports at original resolution),
                                        //   otherwise CalibrationSize - both match
                                        //   the drawing aspect, never distorted
    SKSizeI GetDefaultExportSizeAsSkia()
    SKImage ExportImage(bool includeBackground = true)              // DefaultExportSize
    byte[]  ExportPng(bool includeBackground = true)                // DefaultExportSize
    byte[]  ExportJpeg(int quality = 90)                            // DefaultExportSize
    SKImage ExportImage(Size outputSize, bool includeBackground = true)
    SKImage ExportImage(SKSizeI outputSize, bool includeBackground = true)
    byte[]  ExportPng(Size outputSize, bool includeBackground = true)
    byte[]  ExportPng(SKSizeI outputSize, bool includeBackground = true)
    void    ExportPng(Stream destination, Size outputSize, bool includeBackground = true)
    void    ExportPng(Stream destination, SKSizeI outputSize, bool includeBackground = true)
    byte[]  ExportJpeg(Size outputSize, int quality = 90)
    byte[]  ExportJpeg(SKSizeI outputSize, int quality = 90)
    void Dispose()
    // Exports are complete from-scratch renders (no display caches), so
    // export quality is independent of the on-screen canvas size. JPEG has
    // no alpha channel - set an opaque BackgroundFillColor for JPEG export.
    // ExportImage returns a SkiaSharp SKImage; for a CodeBrix.Imaging
    // Image<Rgba32> instead, use the ExportImagingImage extension (see
    // Bridge extensions).

DrawingSessionOptions (root namespace; sealed)
----------------------------------------------
Initial settings for a DrawingSession. Every property has a default, so an
options instance is only needed to override specific values.

Properties (with defaults):
    static readonly Size DefaultCalibrationSize    // 1000 x 1000
    Size  CalibrationSize     { get; set; }        // = DefaultCalibrationSize; fixed after construction
    byte  LayerOpacity        { get; set; }        // = 100  (255 = fully opaque painting)
    byte  ActiveStrokeOpacity { get; set; }        // = 200
    Color BackgroundFillColor { get; set; }        // = Color.Transparent
    Color SurfaceClearColor   { get; set; }        // = Color.Transparent
    float StrokeWidth         { get; set; }        // = Stroke.DefaultWidth (15f), calibrated units

Fluent SkiaSharp setters (each returns the same options instance):
    DrawingSessionOptions SetCalibrationSize(SKSizeI calibrationSize)
    DrawingSessionOptions SetBackgroundFillColor(SKColor color)
    DrawingSessionOptions SetSurfaceClearColor(SKColor color)

SkiaSharp readers:
    SKSizeI GetCalibrationSizeAsSkia()
    SKColor GetBackgroundFillColorAsSkia()
    SKColor GetSurfaceClearColorAsSkia()
    static SKSizeI GetDefaultCalibrationSizeAsSkia()

CalibrationSizing (root namespace; enum)
----------------------------------------
The explicit choice a CreateForImage factory takes for the session's drawing
space. There is no default - callers state which behavior they want:
    FromOptions               = 0   // use options.CalibrationSize exactly as given
                                    //   (or the 1000 x 1000 default with no options);
                                    //   an image with a different aspect ratio is
                                    //   STRETCHED to fill the drawing rectangle
    DeriveFromBackgroundImage = 1   // compute from the image's aspect ratio with the
                                    //   longest side = DrawingSession.CalibrationLongSide;
                                    //   never distorted; options.CalibrationSize ignored

DrawingLayer (Models; sealed)
-----------------------------
A named, colored, ordered collection of DrawingElement values (freehand
strokes and geometric shapes, interleaved in the order added):
    DrawingLayer(string name, Color color)
    DrawingLayer(string name, SKColor color)
    string Name { get; }               // unique, case-sensitive, trimmed
    Color Color { get; set; }          // changing forces full layer re-render
    void SetColor(SKColor color)
    SKColor GetColorAsSkia()
    int ElementCount { get; }          // strokes + shapes
    int ResetVersion { get; }          // cache-invalidation counter (renderers)
    bool AddStroke(Stroke stroke)      // programmatic stroke injection
    bool AddShape(DrawingShape shape)  // programmatic shape injection
    bool RemoveLastElement()           // simple undo (stroke or shape)
    void Clear()
    DrawingElement[] GetElements()     // everything, in render order
    Stroke[] GetStrokes()              // only the freehand strokes
    string ToString()
NOTE: AddStroke/AddShape called directly on a layer do NOT raise the
session's events - use the session's Draw* methods (or invalidate the
hosting canvas yourself) when the UI must react. Layers are normally created
through DrawingSession.AddLayer; the public constructors exist for
applications that manage their own layer collections with DrawingRenderer.

DrawingElement / Stroke / StrokePoint (Models)
----------------------------------------------
DrawingElement is the abstract base of everything on a layer; its two
families are Stroke (freehand) and DrawingShape (geometric).

    public abstract class DrawingElement
    // It exposes NO public members and its constructor is private protected:
    // only Stroke and DrawingShape derive from it directly. A custom element
    // kind derives from DrawingShape (whose Draw method renderers know how to
    // call), never from DrawingElement itself. Use it as the element type
    // when enumerating DrawingLayer.GetElements() and pattern-match
    // (`element is Stroke s` / `element is DrawingShape shape`).

    public sealed class Stroke : DrawingElement
    const float DefaultWidth = 15f
    Stroke(float width = DefaultWidth, DateTimeOffset? startedAtUtc = null)
    float Width { get; }
    DateTimeOffset? StartedAtUtc { get; }
    int PointCount { get; }
    StrokePoint? LastPoint { get; }             // null when empty
    bool AddPoint(StrokePoint point)            // false when it repeats the last position
    bool AddPoint(int x, int y, int timeOffsetMs = 0)
    StrokePoint[] GetPoints()
    // Points are calibrated-space integers; TimeOffsetMs enables replay.
    // A stroke is rendered as a round-capped, round-joined polyline;
    // a single-point stroke renders as a dot (filled circle of the width).

    public readonly struct StrokePoint : IEquatable<StrokePoint>
    StrokePoint(int x, int y, int timeOffsetMs = 0)
    int X { get; }  int Y { get; }  int TimeOffsetMs { get; }
    bool IsSamePositionAs(StrokePoint other)    // ignores TimeOffsetMs
    bool Equals(StrokePoint other); operators == and !=; GetHashCode(); ToString()

DrawingShape and the shape catalog (Shapes)
-------------------------------------------
All shape coordinates, radii, and thicknesses are calibrated drawing units.
Every shape has StrokeThickness and an optional Color (a CodeBrix.Imaging
Color?; null = owning layer's color). Read the color back as SkiaSharp with
GetColorAsSkia() (returns SKColor?). The constructors take Color? (not
SKColor?) - a caller holding an SKColor converts it with
skColor.ToImagingColor(); the session's Draw* methods also take Color?.

    public abstract class DrawingShape : DrawingElement
    float StrokeThickness { get; }         // must be positive (ArgumentOutOfRangeException)
    Color? Color { get; }
    SKColor? GetColorAsSkia()
    protected DrawingShape(float strokeThickness, Color? color)
    protected DrawingShape(float strokeThickness, SKColor? color)
    public abstract void Draw(SKCanvas canvas, SKColor color)
    protected SKPaint CreateOutlinePaint(SKColor color)         // antialiased, round cap/join, StrokeThickness
    protected static SKPaint CreateFillPaint(SKColor color)     // antialiased fill

    LineShape(float x1, float y1, float x2, float y2,
        float strokeThickness = 15, Color? color = null)
        // X1, Y1, X2, Y2
    ArrowShape(float x1, float y1, float x2, float y2,
        float strokeThickness = 15, Color? color = null, float? headLength = null)
        // X1, Y1, X2, Y2, HeadLength; V-head at (x2, y2); default head
        //   length = max(30, 3 x thickness)
    CircleShape(float centerX, float centerY, float radius,
        float strokeThickness = 15, Color? color = null, bool isFilled = false)
        // CenterX, CenterY, Radius, IsFilled
    EllipseShape(float centerX, float centerY, float radiusX, float radiusY,
        float strokeThickness = 15, Color? color = null, bool isFilled = false)
        // CenterX, CenterY, RadiusX, RadiusY, IsFilled
    RectangleShape(float x, float y, float width, float height,
        float strokeThickness = 15, Color? color = null, bool isFilled = false,
        float cornerRadius = 0)
        // X, Y, Width, Height, CornerRadius, IsFilled
    PolylineShape(IReadOnlyList<PointF> points, float strokeThickness = 15,
        Color? color = null, bool isClosed = false, bool isFilled = false)
        // IsClosed, IsFilled, PointCount; >= 2 points; isFilled implies closed;
        //   PointF[] GetPoints() snapshots, SKPoint[] GetPointsAsSkia()
    Each shape's geometry properties are read-only (get-only) and every shape
    overrides Draw(SKCanvas, SKColor).

Custom shapes: derive from DrawingShape and override
    public override void Draw(SKCanvas canvas, SKColor color)
The canvas arrives PRE-TRANSFORMED to the calibrated drawing space - draw in
calibrated coordinates and use calibrated stroke widths; the transform scales
everything (including paint stroke widths) to the output size. Helpers:
CreateOutlinePaint(color) / CreateFillPaint(color) build the standard
antialiased paints. This is the one extension point where Skia knowledge is
needed. Example:

    using CodeBrix.Imaging.Drawing.Shapes;
    using SkiaSharp;

    public sealed class CrossShape : DrawingShape
    {
        private readonly float _cx, _cy, _half;
        public CrossShape(float cx, float cy, float size, float thickness = 15f, Color? color = null)
            : base(thickness, color) { _cx = cx; _cy = cy; _half = size / 2f; }

        public override void Draw(SKCanvas canvas, SKColor color)
        {
            using SKPaint paint = CreateOutlinePaint(color);
            canvas.DrawLine(_cx - _half, _cy, _cx + _half, _cy, paint);
            canvas.DrawLine(_cx, _cy - _half, _cx, _cy + _half, paint);
        }
    }
    // session.DrawShape(new CrossShape(500, 500, 80));

DrawingRenderer (Rendering; sealed; IDisposable)
------------------------------------------------
Standalone renderer used by DrawingSession - usable directly when an
application manages its own layer collections:
    DrawingRenderer(Size calibrationSize)
    DrawingRenderer(SKSizeI calibrationSize)
    void Render(SKCanvas canvas, SKImageInfo info,     // canvas = the Skia surface
        IReadOnlyList<DrawingLayer> layers,
        Stroke activeStroke = null, SKColor? activeStrokeColor = null,
        bool clearCanvas = true)
    SKImage RenderToImage(Size outputSize,
        IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
    SKImage RenderToImage(SKSizeI outputSize,
        IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
    SKBitmap BackgroundImage { get; set; }             // image type (stays SkiaSharp)
    Color BackgroundFillColor { get; set; }            // void SetBackgroundFillColor(SKColor)
                                                       //   SKColor GetBackgroundFillColorAsSkia()
    Color SurfaceClearColor { get; set; }              // void SetSurfaceClearColor(SKColor)
                                                       //   SKColor GetSurfaceClearColorAsSkia()
    Size CalibrationSize { get; }                      // SKSizeI GetCalibrationSizeAsSkia()
    byte LayerOpacity { get; set; }                    // = 100
    byte ActiveStrokeOpacity { get; set; }             // = 200
    RectangleF LastDrawingRect { get; }                // SKRect GetLastDrawingRectAsSkia()
    Size LastCanvasSize { get; }                       // SKSizeI GetLastCanvasSizeAsSkia()
    bool IsDisposed { get; }
    void Dispose()
Caching internals (all automatic): the background image is rescaled once per
canvas size (high-quality Mitchell resampling); each layer has an
incremental cache bitmap (only elements added since the previous render are
rasterized); and the composited static scene (background + all layers) is
kept in one bitmap so a live-drawing frame costs a 1:1 blit plus the
in-progress stroke. Caches invalidate automatically on canvas resize,
background change, opacity/fill/clear-color change, layer membership change,
and DrawingLayer.ResetVersion bumps (element removal, layer clear, color
change). Element APPENDS never force a full redraw.

CanvasCalibration (Rendering; static)
-------------------------------------
Pure coordinate math - useful for custom hit testing or overlays. Each helper
has a CodeBrix.Imaging-typed form and a SkiaSharp-typed form:

  CodeBrix.Imaging forms:
    static RectangleF GetDrawingRect(Size canvasSize, Size calibrationSize)
    static RectangleF GetDrawingRect(SizeF viewSize, Size calibrationSize)   // fractional view sizes
    static Point? ViewPointToCalibrated(PointF viewPoint, SizeF viewSize,
        Size canvasSize, Size calibrationSize, bool clampToDrawingArea = false)
    static PointF CalibratedToCanvas(Point calibratedPoint, Size calibrationSize, RectangleF drawingRect)
    static float ScaleStrokeWidth(float calibratedWidth, Size calibrationSize, RectangleF drawingRect)

  SkiaSharp forms:
    static SKRect GetDrawingRect(SKSizeI canvasSize, SKSizeI calibrationSize)
    static SKRect GetDrawingRect(SKSize viewSize, SKSizeI calibrationSize)
    static SKPointI? ViewPointToCalibrated(SKPoint viewPoint, SKSize viewSize,
        SKSizeI canvasSize, SKSizeI calibrationSize, bool clampToDrawingArea = false)
    static SKPoint CalibratedToCanvas(SKPointI calibratedPoint, SKSizeI calibrationSize, SKRect drawingRect)
    static float ScaleStrokeWidth(float calibratedWidth, SKSizeI calibrationSize, SKRect drawingRect)

    // Most callers can use the DrawingSession-level GetDrawingRect/ScaleToView
    // sugar instead (see "Overlay positioning helpers" above), which binds
    // these statics to the session's own CalibrationSize.
The drawing rectangle is the centered aspect-fit rectangle with the
calibration space's aspect ratio - match CalibrationSize to the background
image's aspect ratio for edge-to-edge alignment.

Bridge extensions (Extensions)
------------------------------
To CodeBrix.Imaging images (ImagingBridgeExtensions), for further
processing pipelines:
    Image<Rgba32> ToImagingImage(this SKImage image)
    Image<Rgba32> ToImagingImage(this SKBitmap bitmap)
    Image<Rgba32> ExportImagingImage(this DrawingSession session, Size outputSize, bool includeBackground = true)
    Image<Rgba32> ExportImagingImage(this DrawingSession session, SKSizeI outputSize, bool includeBackground = true)
    // Returned images are caller-disposed.
Between color types (ColorBridgeExtensions):
    SKColor ToSKColor(this Color color)
    Color   ToImagingColor(this SKColor color)
Between geometry types (GeometryBridgeExtensions) - both directions:
    SKSizeI  ToSKSizeI(this Size size)          Size       ToImagingSize(this SKSizeI size)
    SKSize   ToSKSize(this SizeF size)          SizeF      ToImagingSizeF(this SKSize size)
    SKPointI ToSKPointI(this Point point)       Point      ToImagingPoint(this SKPointI point)
    SKPoint  ToSKPoint(this PointF point)       PointF     ToImagingPointF(this SKPoint point)
    SKRect   ToSKRect(this RectangleF rect)     RectangleF ToImagingRectangleF(this SKRect rect)
    // These are the general-purpose way to move any returned CodeBrix.Imaging
    // value into SkiaSharp (or vice versa) in a single call.

Error model
-----------
Standard .NET exceptions only: ArgumentNullException / ArgumentException /
ArgumentOutOfRangeException for bad inputs, ObjectDisposedException after
disposal, InvalidOperationException for invalid states (drawing a shape with
no active layer; unreadable pixel data). No custom exception types.

Typical hosting pattern (any UI framework)
------------------------------------------
    // paint:      session.Render(e.Surface, e.Info);
    // invalidate: session.RedrawRequested += (s, e) => canvas.Invalidate();
    //   (marshal to the UI thread if the framework requires it)
    // mouse:      on left-button down    -> session.PointerPressed(pt, viewSize)
    //             on move                -> session.PointerMoved(pt, viewSize)
    //             on up                  -> session.PointerReleased()
    //             on capture lost        -> session.PointerCanceled()
    // The view should capture the pointer while a stroke is active so
    // strokes continue when the pointer leaves the control.
The PainDiagram reference application (see WORKING EXAMPLES ON GITHUB)
implements this pattern for CodeBrix.Platform, native WinUI 3, and native
WPF.

COMPLETE EXAMPLES
=================

1. Highlighter layers over a line diagram, exported to PNG
---------------------------------------------------------
    using System.IO;
    using CodeBrix.Imaging;          // Color, Size
    using CodeBrix.Imaging.Drawing;  // DrawingSession, DrawingSessionOptions
    using CodeBrix.Imaging.Drawing.Models;

    var session = new DrawingSession(new DrawingSessionOptions
    {
        BackgroundFillColor = Color.White,
        SurfaceClearColor = Color.White,
    });
    session.SetBackgroundImage(File.ReadAllBytes("body_map.png"));   // square image, 1000x1000 space

    DrawingLayer pain = session.AddLayer("Pain", Color.FromRgb(255, 30, 230));      // becomes ActiveLayer
    DrawingLayer numbness = session.AddLayer("Numbness", Color.FromRgb(30, 128, 204));
    session.ActiveLayer = numbness;

    // Wire the hosting canvas (paint + pointer events) as in "Typical hosting pattern".
    session.RedrawRequested += (s, e) => canvasControl.Invalidate();

    // Save: a from-scratch render at an explicit size
    byte[] png = session.ExportPng(new Size(1000, 1000));
    File.WriteAllBytes("highlighted_body_map.png", png);

2. Annotating a photo of any aspect ratio (no distortion)
---------------------------------------------------------
    using CodeBrix.Imaging;
    using CodeBrix.Imaging.Drawing;

    var session = DrawingSession.CreateForImage(
        File.ReadAllBytes("car_photo.jpg"),
        CalibrationSizing.DeriveFromBackgroundImage,      // drawing space = photo aspect
        new DrawingSessionOptions { BackgroundFillColor = Color.White });
    session.AddLayer("Damage", Color.Red);

    // ...pointer events + Render as usual; the user draws on the photo...

    File.WriteAllBytes("car_photo_annotated.png", session.ExportPng());   // original resolution
    File.WriteAllBytes("car_photo_annotated.jpg", session.ExportJpeg(quality: 85));

3. Programmatic (vision-driven) annotation of a video frame
-----------------------------------------------------------
    // session created over the current frame's raw BGRA pixels:
    var session = DrawingSession.CreateForImage(frame.PixelsBgra32, frame.Width, frame.Height,
        CalibrationSizing.DeriveFromBackgroundImage);
    session.AddLayer("Detections", Color.FromRgb(255, 255, 255));

    // detection reported as normalized (0..1) box -> calibrated units
    Size cal = session.CalibrationSize;
    float x = box.Left * cal.Width, y = box.Top * cal.Height;
    float w = box.Width * cal.Width, h = box.Height * cal.Height;
    session.DrawRectangle(x, y, w, h, thickness: 12, cornerRadius: 20);
    session.DrawArrow(x + w / 2, y + h + 200, x + w / 2, y + h + 20);
    session.DrawCircle(x + w / 2, y + h / 2, 30, filled: true, color: Color.Red);

    using var annotated = session.ExportImagingImage(session.DefaultExportSize);   // Image<Rgba32>

4. Vision-driven painting (the WebcamPainter recipe)
----------------------------------------------------
Painting on a captured webcam still with a hand gesture tracked through a
webcam - "spreading frosting on a cake". The essential recipe for driving
this library from a computer-vision pipeline instead of a mouse:

  1. Create the session straight from the captured frame's raw pixels,
     deriving the drawing space from the photo so normalized photo
     coordinates and normalized drawing coordinates coincide:
       var session = DrawingSession.CreateForImage(
           photo.PixelsBgra32, photo.Width, photo.Height,
           CalibrationSizing.DeriveFromBackgroundImage,
           new DrawingSessionOptions { BackgroundFillColor = Color.White,
                                       StrokeWidth = 60f },
           mirrorHorizontally: true);   // matches a mirrored live preview
  2. Feed tracking results in as normalized strokes - no view size, no prior
     render, no coordinate mapping in the app:
       gesture active   -> session.PointerPressedNormalized(nx, ny)   // first frame
                           session.PointerMovedNormalized(nx, ny)     // subsequent
       gesture lost/off -> session.PointerReleased()
     Vision positions are normally in UNMIRRORED camera coordinates; when the
     still was mirrored, mirror the hand too: nx = 1 - nx.
  3. Draw a cursor so the user can aim. In the paint handler, after
     session.Render(...):
       RectangleF rect = session.GetDrawingRect(new SizeF(info.Width, info.Height));
       float cx = rect.X + (nx * rect.Width);
       float cy = rect.Y + (ny * rect.Height);
       float radius = session.ScaleToView(brushRadius, new SizeF(info.Width, info.Height));
       // draw a ring of that radius at (cx, cy) on the canvas
  4. Marshal threading carefully: tracking results arrive on a vision worker
     thread; make all Pointer* calls from ONE thread (the UI thread is the
     natural choice, since Render runs there too).

Lessons learned building that sample (each of these was app code before the
normalized-input API existed - now the library covers them):
  - Deriving the calibration from the photo (DeriveFromBackgroundImage) is
    what makes vision coordinates line up edge-to-edge; an explicit
    calibration size with a different aspect ratio letterboxes the image and
    shifts every stroke off-target.
  - JPEG export of an annotated photo "just works" at native resolution:
    ExportJpeg() with no size uses DefaultExportSize = the background image's
    pixel size. Set an opaque BackgroundFillColor (JPEG has no alpha).
  - Set StrokeWidth for a wide "spatula" feel; a brush-size cursor ring via
    ScaleToView makes the width tangible to the user before they commit a
    stroke.
  - Translucent highlighter compositing (LayerOpacity default 100) means
    repeated palm passes over the same spot do NOT darken - which reads as
    "spreading" rather than "scribbling". Use LayerOpacity = 255 for opaque
    paint instead.

5. Hosting page code-behind
---------------------------
See "KEEPING SKIASHARP OUT OF THE HOSTING APP'S XAML" above for the complete
DrawingCanvas file and the page constructor wiring (PaintSurface + pointer
handlers + capture).

MINIMUM VIABLE PROJECT
======================

A CodeBrix.Platform desktop app hosting one DrawingSession, in the shape of
the PainDiagram reference application (one shared library + thin per-head
executables). Only the drawing-specific parts are listed; the rest is the
standard CodeBrix.Platform application layout.

Shared library (e.g. MyApp.Core.csproj):
    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
      <!-- HAS_CODEBRIX/HAS_CODEBRIX_WINUI: CodeBrix.Platform's own conditionals.
           HAS_CODEBRIXPLATFORM: selects SKXamlCanvas for the shared DrawingCanvas -->
      <DefineConstants>$(DefineConstants);HAS_CODEBRIX;HAS_CODEBRIX_WINUI;HAS_CODEBRIXPLATFORM</DefineConstants>
    </PropertyGroup>
    <ItemGroup>
      <Compile Include="..\..\Shared\Drawing\DrawingCanvas.cs" Link="Drawing\DrawingCanvas.cs" />
      <Compile Include="..\..\Shared\ViewModels\MainViewModel.cs" Link="ViewModels\MainViewModel.cs" />
    </ItemGroup>
    <ItemGroup>
      <EmbeddedResource Include="..\..\Shared\Assets\body_map_master.png" Link="Assets\body_map_master.png">
        <LogicalName>MyApp.Assets.body_map_master.png</LogicalName>
      </EmbeddedResource>
    </ItemGroup>
    <ItemGroup>
      <PackageReference Include="CodeBrix.Platform.ApacheLicenseForever" Version="..." />
      <PackageReference Include="CodeBrix.Platform.SkiaSharp.Views.MitLicenseForever" Version="..." />   <!-- SKXamlCanvas -->
      <PackageReference Include="CodeBrix.Platform.Fonts.OpenSans.ApacheLicenseForever" Version="..." />
      <PackageReference Include="CodeBrix.Imaging.Drawing.ApacheLicenseForever" Version="..." />
    </ItemGroup>

Each Skia head executable (e.g. MyApp.LinuxX11.csproj, ~30 lines):
    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
      <OutputType>Exe</OutputType>
      <DefineConstants>$(DefineConstants);HAS_CODEBRIX;HAS_CODEBRIX_WINUI</DefineConstants>
    </PropertyGroup>
    <!-- treat .xaml files as CodeBrix.Platform XAML pages -->
    <ItemGroup>
      <Page Include="**\*.xaml" Exclude="bin\**\*.xaml;obj\**\*.xaml" />
      <None Remove="**\*.xaml" />
    </ItemGroup>
    <Import Project="..\MyApp.UI\MyApp.UI.projitems" Label="Shared" />   <!-- App.xaml + Views -->
    <ItemGroup>
      <ProjectReference Include="..\MyApp.Core\MyApp.Core.csproj" />
      <!-- exactly ONE platform runtime package per head:
           CodeBrix.Platform.Runtime.Skia.{Win32|Wpf|X11|Wayland|FrameBuffer|MacOS}.ApacheLicenseForever -->
      <PackageReference Include="CodeBrix.Platform.Runtime.Skia.X11.ApacheLicenseForever" Version="..." />
    </ItemGroup>

Program.cs (each head; the Use… method is per head):
    using System;
    using CodeBrix.Platform.UI.Hosting;

    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var host = CodeBrixPlatformHostBuilder.Create()
                .App(() => new App())
                .UseLinuxX11()          // per head: .UseWindowsWin32() / .UseWindowsWpf() /
                                        //   .UseLinuxWayland() / .UseLinuxFrameBuffer() / .UseMacOS()
                .Build();
            host.Run();
        }
    }
    // The WPF-hosted Skia head additionally sets the WPF host's
    // RenderSurfaceType = RenderSurfaceType.Software (avoids WPF airspace
    // conflicts), must NOT set <UseWPF>, and targets net10.0-windows with
    // <EnableWindowsTargeting>true</EnableWindowsTargeting> so it still
    // compiles inside the cross-platform solution on Linux/macOS.

Views/MainPage.xaml (WinUI dialect, shared across the Skia heads):
    xmlns:drawing="using:CodeBrix.Imaging.Drawing"
    ...
    <drawing:DrawingCanvas x:Name="DrawCanvas" />

Views/MainPage.xaml.cs - CRITICAL wiring order:
    public MainPage()
    {
        // Subscribe BEFORE InitializeComponent(): the XAML itself sets
        // Page.DataContext (<Page.DataContext><vm:MainViewModel/></Page.DataContext>)
        // during InitializeComponent, so a later subscription never fires and the
        // bridges are never wired (symptom: input is captured but nothing repaints).
        DataContextChanged += (_, _) =>
        {
            if (DataContext is IFileSaveBridge fileSave)
            {
                fileSave.PickSavePngPathAsync = PickSavePngPathAsync;   // this head's save dialog
            }
            if (DataContext is ICanvasInvalidator invalidator)
            {
                invalidator.InvalidateCanvas = () => DrawCanvas?.Invalidate();
            }
        };
        InitializeComponent();
        // then the DrawCanvas.PaintSurface / Pointer* lambdas from the DrawingCanvas section
    }

View model (shared, one file for every head):
    - Derives from SimpleViewModel (the CodeBrix "Simple" MVVM API, identical
      across CodeBrix.Platform, native WinUI and WPF - which is what lets one
      view-model file serve every head). Properties use SetProperty(ref
      field, value) with [AffectsCommands(nameof(SaveCommand), ...)];
      commands are lazily created `new SimpleCommand(CanX, DoX)`; dialogs are
      `await ConfirmDialog(msg, title)` / ShowInfo / ShowError.
    - Carries [Microsoft.UI.Xaml.Data.Bindable] under #if HAS_CODEBRIX
      (required for bindings on the Skia heads; compiles out on native WPF).
    - Owns the DrawingSession: in its constructor (guarded by
      !IsDesignMode(true)) it creates the session, adds the layers, and loads
      the background via typeof(MainViewModel).Assembly
      .GetManifestResourceStream(logicalName). Because the view model is
      compiled into several assemblies, every embedding project uses the
      SAME explicit <LogicalName> so one line of code finds the image
      everywhere.
    - Two tiny bridge interfaces keep UI types out of the view model:
        IFileSaveBridge   { Func<string, Task<string>> PickSavePngPathAsync }
        ICanvasInvalidator { Action InvalidateCanvas }
      RedrawRequested is forwarded through InvalidateCanvas.
    - Save flow: ask the head for a path via PickSavePngPathAsync(suggested
      name); if the file exists ask ConfirmDialog("replace?"); then
      session.ExportPng(new Size(1000, 1000)) + File.WriteAllBytes; then ask
      whether to clear. Keep that ONE overwrite prompt by suppressing each
      head's native one: CodeBrix.Platform's FileSavePicker creates an empty
      placeholder file for new names (delete it if it is genuinely empty;
      awaiting the picker requires `using System;` because the
      IAsyncOperation GetAwaiter extension lives in the System namespace);
      native WinUI uses a COM IFileSaveDialog wrapper with FOS_OVERWRITEPROMPT
      cleared (the WinRT picker's prompt cannot be turned off); native WPF
      uses Microsoft.Win32.SaveFileDialog with OverwritePrompt = false; the
      Linux framebuffer head has no dialogs, so it saves to a default
      Pictures-folder path instead.

Native heads: a native WinUI 3 head links the same view-model and
DrawingCanvas files, defines HAS_WINUI, references SkiaSharp.Views.WinUI
and CodeBrix.Platform.WinUI.ApacheLicenseForever; a native WPF head links the
same files with neither symbol, references SkiaSharp.Views.WPF and
CodeBrix.Platform.WPF.ApacheLicenseForever, wires MouseDown/MouseMove/
MouseUp/LostMouseCapture with CaptureMouse()/ReleaseMouseCapture() and
InvalidateVisual() for redraw, and MUST target net10.0-windows10.0.19041.0
(see COMMON PITFALLS).

The complete worked instance of this recipe - eight heads, one view model -
is the PainDiagram sample (WORKING EXAMPLES ON GITHUB). To reuse it as a
template: copy the folder structure and rename projects/namespaces; rewrite
the view model (your layers, your background or a CreateForImage factory,
your commands - keep the two bridge interfaces and the SimpleViewModel
patterns); keep the pages' canvas wiring verbatim including the
DataContextChanged-before-InitializeComponent ordering; keep the per-head
save-dialog implementations and change only the file extension/filters; and
update the embedded resource LogicalName in every embedding project.

PERFORMANCE TIPS
================

- Live drawing is cheap by design: per frame, one 1:1 blit of the cached
  static scene plus the in-progress stroke (~4 ms at desktop sizes, even
  over a 3100 x 3100 background). Do nothing special to keep it fast.
- Never rescale the background yourself per frame and never bypass the
  session to redraw all layers each paint: the renderer's cache model (one
  pre-scaled background, one incremental bitmap per layer, one composited
  static scene) is what made live drawing fast - reintroducing per-frame
  rescaling caused a 17x drawing-lag regression (66 ms -> 4 ms per frame
  when fixed).
- Element APPENDS (new strokes/shapes) are incremental; REMOVALS, layer
  clears and layer color changes bump DrawingLayer.ResetVersion and cost one
  full layer re-render. Undo-heavy UIs still feel fine; just do not churn
  colors every frame.
- Canvas resize, background change, and opacity/fill/clear-color changes
  invalidate the caches (one full re-render each) - batch such changes
  rather than animating them.
- Exports are from-scratch renders at the requested size, independent of the
  on-screen canvas; large exports cost proportional to output pixels.
- Committed shapes are persistent marks; for per-frame moving overlays
  (tracked-object boxes, cursors) draw directly on the canvas AFTER
  session.Render instead of committing shapes every frame.
- Raw-pixel factories (CreateForImage(byte[] bgraPixels, ...) and
  SetBackgroundImage(byte[] bgraPixels, ...)) copy the buffer once and skip
  any encode/decode round-trip - use them for camera frames.

COMMON PITFALLS TO AVOID
========================

- Subscribing DataContextChanged AFTER InitializeComponent() in a page whose
  XAML sets Page.DataContext: the handler never fires, the bridges never get
  wired, and drawing input is captured but nothing ever repaints. Subscribe
  BEFORE InitializeComponent().
- Calling PointerPressed before the first Render: it returns false (the
  session needs a canvas size to calibrate). PointerPressedNormalized has no
  such requirement.
- Mismatched aspect ratio between CalibrationSize and the background image:
  the image is letterboxed (or, with CalibrationSizing.FromOptions in a
  factory, STRETCHED) and strokes drift off-target. Use
  CalibrationSizing.DeriveFromBackgroundImage or match the sizes yourself.
- EXIF orientation is NOT applied by the decoders used here - auto-orient
  photos first (CodeBrix.Imaging image.Mutate(x => x.AutoOrient())).
- JPEG export with a transparent BackgroundFillColor: JPEG has no alpha
  channel; set an opaque fill first.
- Mirrored still + unmirrored vision coordinates: when the session was
  created with mirrorHorizontally: true, mirror the tracked x too
  (nx = 1 - nx) or strokes land on the wrong side.
- Feeding Pointer* calls from more than one thread (UI + vision worker):
  marshal everything to one thread.
- Layer.AddStroke/AddShape directly on a DrawingLayer does not raise the
  session's RedrawRequested/DrawingChanged; use session.Draw* or invalidate
  yourself.
- SetBackgroundImage(bgraPixels, ...) does NOT change CalibrationSize; to
  derive the drawing space from a frame use a raw-pixels CreateForImage
  factory instead.
- Defining HAS_CODEBRIXPLATFORM on every Skia head executable as well as on
  the shared library: define it once, on the assembly that compiles the
  linked DrawingCanvas file.
- Native WPF head targeting bare net10.0-windows: SkiaSharp.Views.WPF has no
  assets for it and silently restores its .NET Framework assembly (NU1701).
  Target net10.0-windows10.0.19041.0.
- Referencing the NoSkia companion package: it is not yet published, so a
  restore of CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever fails with
  a package-not-found error. Once it ships, referencing it alongside this
  package in one application makes identical types in identical namespaces
  collide - pick one.
- Missing SkiaSharp native assets in a plain .NET app (the first SkiaSharp
  call fails to load the native library): add the SkiaSharp.NativeAssets.*
  package for the OS - CodeBrix.Platform heads already carry it.
- Layer names are unique, case-sensitive and trimmed; AddLayer with a
  duplicate name throws ArgumentException.
- The SKBitmap passed to the CreateForImage(SKBitmap, ...) factories and to
  the BackgroundImage property stays caller-owned - do not dispose it while
  the session is alive; the byte[] overloads hand ownership to the session.

WHAT THIS PACKAGE DOES NOT DO
=============================

- No UI controls: it never references a UI framework. You host a SkiaSharp
  view and forward events (the DrawingCanvas trick above is app code).
- No EXIF orientation handling when decoding photos.
- No text, image-stamp or bitmap-brush elements: elements are freehand
  strokes and the six geometric shapes (plus custom DrawingShape
  subclasses you write against SKCanvas).
- No selection, move, resize or per-element editing of committed elements;
  the edit model is append, undo-last, clear.
- No serialization format: persist strokes yourself from
  DrawingLayer.GetElements()/Stroke.GetPoints() if you need replay or
  storage (StrokePoint.TimeOffsetMs is there for replay).
- No SVG rendering and no managed rasterizer - those live in the NoSkia
  companion package, which is not yet published (AGENT-README-NOSKIA.txt).
- No custom exception types; standard .NET exceptions only.
- Committed shapes are persistent marks, not animated overlays.

WORKING EXAMPLES ON GITHUB
==========================

Test suite (xUnit v3; every behavioral guarantee above has a test):
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.Tests
    DrawingSessionTests.cs                 layers, pointer input, undo, clear, export sizes
    DrawingSessionImageFactoryTests.cs     the nine CreateForImage overloads + CalibrationSizing
    DrawingSessionRawPixelsTests.cs        BGRA raw-pixel factories, mirroring, ownership
    DrawingSessionNormalizedInputTests.cs  PointerPressedNormalized/PointerMovedNormalized
    DrawingSessionShapeTests.cs            Draw* primitives, colors vs layer opacity
    Models/DrawingLayerTests.cs, StrokeTests.cs, StrokePointTests.cs
    Shapes/DrawingShapeTests.cs            shape catalog + custom shapes
    Rendering/DrawingRendererTests.cs      pixel assertions (highlighter guarantee: two
                                           overlapping strokes on one layer == one stroke)
    Rendering/CanvasCalibrationTests.cs    coordinate math
    Extensions/GeometryBridgeExtensionsTests.cs, ImagingBridgeExtensionsTests.cs

Cross-backend parity (both packages rendering identical scenes):
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.ParityTests

PainDiagram reference application (eight heads, one view model; the
DrawingCanvas file and every code-behind pattern in this guide):
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/samples/PainDiagram
    Shared/Drawing/DrawingCanvas.cs             the DrawingCanvas trick file
    Shared/ViewModels/MainViewModel.cs          session ownership, bridges, save flow
    CodeBrixPlatform/PainDiagram.UI/Views/MainPage.xaml(.cs)   canvas wiring
    PainDiagram.WinUI/, PainDiagram.Wpf/        native heads

The WebcamPainter sample (vision-driven painting, example 4 above) is a
separate application in the CodeBrix.Samples repository
(https://github.com/ellisnet/CodeBrix.Samples); the recipe above is
self-contained.

QUICK REFERENCE CARD
====================

    using CodeBrix.Imaging;                      // Color, Size, SizeF, PointF, RectangleF
    using CodeBrix.Imaging.Drawing;              // DrawingSession, DrawingSessionOptions, CalibrationSizing
    using CodeBrix.Imaging.Drawing.Extensions;   // ExportImagingImage, ToSKColor, ToImagingSize...

    // create
    var s = new DrawingSession();                                    // 1000x1000 space, transparent
    var s = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(w, h),
                LayerOpacity = 100, ActiveStrokeOpacity = 200, BackgroundFillColor = Color.White,
                SurfaceClearColor = Color.Transparent, StrokeWidth = 15f });
    var s = DrawingSession.CreateForImage(pngOrJpegBytes, CalibrationSizing.DeriveFromBackgroundImage);
    var s = DrawingSession.CreateForImage(bgra, width, height, CalibrationSizing.DeriveFromBackgroundImage,
                options, mirrorHorizontally: true);                  // camera frame
    // layers
    DrawingLayer l = s.AddLayer("Pain", Color.FromRgb(255, 30, 230)); s.ActiveLayer = l;
    s.GetLayer("Pain"); s.RemoveLayer(l); s.Layers;
    // background / look
    s.SetBackgroundImage(bytes); s.SetBackgroundImage(bgra, w, h, mirrorHorizontally: false);
    s.BackgroundImage (SKBitmap, caller-owned); s.BackgroundFillColor; s.SurfaceClearColor;
    s.LayerOpacity = 255 /* opaque ink */; s.ActiveStrokeOpacity; s.StrokeWidth = 30f;
    // input (view coords)            // input (normalized 0..1, no Render needed)
    s.PointerPressed(pt, viewSize);   s.PointerPressedNormalized(nx, ny);
    s.PointerMoved(pt, viewSize);     s.PointerMovedNormalized(nx, ny);
    s.PointerReleased(); s.PointerCanceled(); s.IsPointerActive;
    // paint + overlays
    s.Render(e.Surface, e.Info); s.Render(canvas, info, clearCanvas: false);   // over live video
    RectangleF r = s.GetDrawingRect(viewSize); float px = s.ScaleToView(calibratedLen, viewSize);
    // programmatic shapes (calibrated units; Color? null = layer color)
    s.DrawLine(x1,y1,x2,y2); s.DrawArrow(x1,y1,x2,y2, headLength: 60);
    s.DrawCircle(cx,cy,r, filled: true); s.DrawEllipse(cx,cy,rx,ry);
    s.DrawRectangle(x,y,w,h, cornerRadius: 20); s.DrawPolyline(pts, closed: true, filled: true);
    s.DrawShape(new LineShape(...));   // or your DrawingShape subclass
    // state / edit
    s.HasStrokes; s.StrokeCount; s.UndoLastStroke(); s.Clear();
    s.RedrawRequested += (_, _) => canvas.Invalidate(); s.DrawingChanged += ...;
    // export (DefaultExportSize = image pixel size, else CalibrationSize)
    byte[] png = s.ExportPng(); byte[] jpg = s.ExportJpeg(90); SKImage img = s.ExportImage();
    s.ExportPng(new Size(2000, 2000)); s.ExportPng(stream, new Size(1000, 1000));
    Image<Rgba32> im = s.ExportImagingImage(s.DefaultExportSize);
    s.Dispose();
    // SkiaSharp-typed twins: SKColor/SKSizeI/SKSize/SKPoint overloads of every method above,
    //   Set…(SKColor|SKSizeI) + Get…AsSkia() for properties, and the bridge extensions
    //   ToSKColor/ToImagingColor, ToSKSizeI/ToImagingSize, ToSKSize/ToImagingSizeF,
    //   ToSKPointI/ToImagingPoint, ToSKPoint/ToImagingPointF, ToSKRect/ToImagingRectangleF.

Rules of thumb: match CalibrationSize to the background aspect (or derive
it); Render once before view-coordinate PointerPressed; opaque fill for JPEG;
one thread for Pointer* calls; the NoSkia companion package is not yet
published, and must never be referenced alongside this one.
