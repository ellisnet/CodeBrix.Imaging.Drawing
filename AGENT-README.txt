========================================================================
              AGENT-README: CodeBrix.Imaging.Drawing
           A Comprehensive Guide for AI Coding Agents
========================================================================

OVERVIEW
========================================================================

CodeBrix.Imaging.Drawing is a stroke-based drawing, painting and
highlighting library for SkiaSharp canvases. It provides:

- Interactive freehand drawing: pointer (mouse, pen, or touch) input is
  captured as resolution-independent calibrated strokes on named,
  colored layers.
- Programmatic drawing primitives: lines, arrows, circles, ellipses,
  rectangles, and polylines/polygons drawn with plain coordinates and
  CodeBrix.Imaging colors - no SkiaSharp knowledge required - ideal for
  computer-vision-driven annotation of a live video feed.
- Translucent "highlighter" rendering over a background image, over a
  solid fill, or over a fully transparent canvas above live content
  such as a webcam video feed (the "telestrator" scenario).
- Export of the finished drawing as PNG/JPEG bytes, a SkiaSharp
  SKImage, or a CodeBrix.Imaging Image<Rgba32>.

The library is UI-framework-agnostic: it never references any UI
framework. A hosting view (CodeBrix.Platform SKXamlCanvas, native
WinUI 3 SKXamlCanvas, WPF SKElement, MAUI SKCanvasView, etc.) forwards
its pointer events and paint callbacks to a DrawingSession, and the
session raises RedrawRequested whenever the view should invalidate.

Key rendering idea - the "highlighter" effect: each layer's elements
(strokes and shapes) are drawn fully OPAQUE onto a private transparent
cache bitmap, and the whole cache is then composited over the
background at the layer opacity (default alpha 100/255). Overlapping
elements within one layer therefore never darken each other, which is
what makes translucent ink read as highlighter rather than marker
scribble. Set LayerOpacity = 255 for opaque whiteboard-marker ink.

Key coordinate idea - the calibrated drawing space: all strokes and
shapes are stored in a fixed logical space (CalibrationSize, default
1000 x 1000, any width x height allowed), never in screen pixels. The
renderer maps that space to the centered aspect-fit rectangle of
whatever canvas it is given, so a drawing survives window resizing,
DPI changes, and orientation flips, and exports at any resolution.

Performance: the renderer caches (a) the background image pre-scaled to
the current canvas size, (b) one bitmap per layer, drawn incrementally
(only new elements are rasterized), and (c) the fully composited static
scene. The per-frame cost while the user is actively drawing is one 1:1
bitmap blit plus the in-progress stroke (~4 ms at desktop sizes), so
live drawing feels immediate even over a 3100 x 3100 background image.

The repository also contains a complete reference application -
samples/PainDiagram - documented in detail in THE PAINDIAGRAM SAMPLE
APPLICATION section near the end of this file.

INSTALLATION
========================================================================

NuGet Package: CodeBrix.Imaging.Drawing.ApacheLicenseForever

  dotnet add package CodeBrix.Imaging.Drawing.ApacheLicenseForever

- The package ID carries the ".ApacheLicenseForever" suffix - a
  permanent guarantee that this package ID will only ever be published
  under the Apache-2.0 license. Namespaces do NOT carry the suffix:
  code uses the CodeBrix.Imaging.Drawing namespace.
- Requires .NET 10.0 or higher.
- Dependencies: SkiaSharp (4.150.0 or later) and
  CodeBrix.Imaging.ApacheLicenseForever. Applications must also
  provide the SkiaSharp native assets for their platform - desktop
  apps built on CodeBrix.Platform get them from their platform head
  package; plain .NET apps add SkiaSharp.NativeAssets.Linux /
  .macOS / .Win32 as appropriate.

KEY NAMESPACE
========================================================================

  using CodeBrix.Imaging.Drawing;             // DrawingSession, DrawingSessionOptions, CalibrationSizing
  using CodeBrix.Imaging.Drawing.Models;      // DrawingLayer, DrawingElement, Stroke, StrokePoint
  using CodeBrix.Imaging.Drawing.Shapes;      // DrawingShape + Line/Circle/Ellipse/Rectangle/Polyline/Arrow shapes
  using CodeBrix.Imaging.Drawing.Rendering;   // DrawingRenderer, CanvasCalibration
  using CodeBrix.Imaging.Drawing.Extensions;  // CodeBrix.Imaging image + color bridge extensions

Because CodeBrix.Imaging.Drawing nests under the CodeBrix.Imaging
namespace, the CodeBrix.Imaging `Color` type resolves as just `Color`
inside consuming code that uses these namespaces. (Add an explicit
`using CodeBrix.Imaging;` when your own code lives in an unrelated
namespace, so that `Color`, `Size`, `PointF`, etc. resolve.)

SKIASHARP-FREE BY DEFAULT (AND SKIASHARP-FRIENDLY WHEN YOU WANT IT)
========================================================================

The goal of this library's public surface is that a consumer never has
to write a `using SkiaSharp;` line - EXCEPT where it hands SkiaSharp the
actual drawing surface (the `SKCanvas`/`SKSurface`/`SKImageInfo` passed
to `Render`, and the `Draw(SKCanvas, SKColor)` override when authoring a
custom shape). Everywhere else, colors, sizes, points and rectangles are
expressed with the CodeBrix.Imaging value types, which map one-to-one to
their SkiaSharp counterparts:

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

  1. Method overloads and helpers. Methods that took a SkiaSharp type
     still do (for example `AddLayer(string, SKColor)` and
     `ExportPng(SKSizeI, ...)` remain), alongside the CodeBrix.Imaging
     overload. Properties cannot be overloaded by type, so each property
     that is a CodeBrix.Imaging type also has a `Set…(SKColor/SKSizeI)`
     helper (or a fluent one on DrawingSessionOptions) for writing and a
     `Get…AsSkia()` method for reading. For example:
       session.BackgroundFillColor = Color.White;      // default path
       session.SetBackgroundFillColor(SKColors.White); // Skia path
       SKColor c = session.GetBackgroundFillColorAsSkia();

  2. Bridge extension methods (namespace CodeBrix.Imaging.Drawing.
     Extensions) convert between the two worlds in one call, both
     directions - see "Bridge extensions" below:
       SKColor sk = session.BackgroundFillColor.ToSKColor();
       Size    s  = someSkSizeI.ToImagingSize();

KEEPING SKIASHARP OUT OF THE HOSTING APP'S XAML (the DrawingCanvas trick)
========================================================================

The section above keeps SkiaSharp out of your model and view-model code.
A XAML host still normally names a SkiaSharp view control in its markup -
SkiaSharp.Views.Windows.SKXamlCanvas on WinUI-dialect XAML (CodeBrix.
Platform Skia heads and native WinUI 3) and SkiaSharp.Views.WPF.SKElement
on WPF - so each head's XAML carries a SkiaSharp xmlns and a per-framework
control name.

You can hide all of that behind ONE control name that is identical in
every head's XAML - <drawing:DrawingCanvas/> - by adding a single small
source file to your app and letting conditional compilation pick the
correct base control per head. The same file also carries a
DrawCanvasHelper with two tiny converters, so the hosting page's
code-behind can wire the canvas without ever NAMING a SkiaSharp type -
which keeps `using SkiaSharp;` out of the code-behind entirely. The file
(taken verbatim from the PainDiagram sample; put it in whatever namespace
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
       // ...PointerMoved / PointerReleased / PointerCaptureLost / SizeChanged likewise...
     The redraw side stays wired as before through the session's
     RedrawRequested event (in PainDiagram, via the VM's ICanvasInvalidator).

Why this keeps the code-behind SkiaSharp-free: DrawingCanvas DERIVES from
the platform control, so every member (Invalidate/InvalidateVisual,
CapturePointer/CaptureMouse, ActualWidth/Height, ...) is inherited. The
lambda's `e` is inferred (SKPaintSurfaceEventArgs / a pointer-args type),
so it is never spelled out; and DrawCanvasHelper.GetPointFromPosition /
GetViewSize return the SKPoint/SKSize that session.PointerPressed/Moved
want, so the code-behind never names a SkiaSharp type. (The pointer input
is fed as SKPoint/SKSize to the SkiaSharp overloads here; you may instead
use the PointF/SizeF overloads and skip the helper entirely.)

Scope of the trick: the one genuine SkiaSharp touchpoint - the drawing
surface handed to session.Render(e.Surface, e.Info) - still exists, but it
now lives inside an inferred-type lambda in this reusable file, not spread
across your pages. Net result: clean one-line XAML, and code-behinds (and
view-models) with no `using SkiaSharp;` at all.

CORE API REFERENCE
========================================================================

DrawingSession (main entry point; IDisposable)
------------------------------------------------------------------------
The interactive drawing surface model. One session = one drawing.

Construction:
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

Image-annotation factories (photos of any aspect ratio):
  DrawingSession CreateForImage(byte[] encodedImage, Size calibrationSize, DrawingSessionOptions options = null)
  DrawingSession CreateForImage(byte[] encodedImage, CalibrationSizing sizing, DrawingSessionOptions options = null)
  DrawingSession CreateForImage(SKBitmap image, Size calibrationSize, DrawingSessionOptions options = null)
  DrawingSession CreateForImage(SKBitmap image, CalibrationSizing sizing, DrawingSessionOptions options = null)
  // (SKSizeI overloads of the two size-taking factories also exist.)
  // Decodes/uses the image as the session's background. The calibration size
  // is ALWAYS an explicit caller choice - there is no automatic behavior:
  //   * pass a Size to state the drawing space inline (match its aspect
  //     ratio to the image's to avoid stretching), or
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
                                                     //   (SKColor overload also exists)
  DrawingLayer GetLayer(string name)
  bool RemoveLayer(DrawingLayer layer)
  IReadOnlyList<DrawingLayer> Layers { get; }
  DrawingLayer ActiveLayer { get; set; }             // strokes/shapes commit here

Background:
  void SetBackgroundImage(byte[] encodedImage)  // decodes; session owns the bitmap
  SKBitmap BackgroundImage { get; set; }        // caller-owned alternative (image type)
  Color BackgroundFillColor { get; set; }       // + SetBackgroundFillColor(SKColor)
                                                //   + GetBackgroundFillColorAsSkia()
  Color SurfaceClearColor { get; set; }         // + SetSurfaceClearColor(SKColor)
                                                //   + GetSurfaceClearColorAsSkia()
  Size CalibrationSize { get; }                 // + GetCalibrationSizeAsSkia()
  // Leave both colors transparent and the image null to highlight over
  // externally drawn content (e.g. a live video frame the host draws
  // before calling Render).

Pointer input (forward from the hosting view; viewPoint/viewSize are in
the control's logical coordinates - the session handles DPI scaling):
  bool PointerPressed(PointF viewPoint, SizeF viewSize)   // SKPoint/SKSize overload also exists
  bool PointerMoved(PointF viewPoint, SizeF viewSize)     // SKPoint/SKSize overload also exists
  bool PointerReleased()
  void PointerCanceled()
  bool IsPointerActive { get; }
  // A press outside the drawing area returns false (no stroke started).
  // Moves are clamped to the drawing area. A press+release without
  // movement commits a single-point dot. Strokes commit to the layer
  // that was active at PRESS time, even if ActiveLayer changes mid-stroke.
  // PointerPressed requires one prior Render call (it needs the canvas
  // size to calibrate coordinates); before that it returns false.

Rendering (call from the view's paint handler):
  void Render(SKSurface surface, SKImageInfo info, bool clearCanvas = true)
  void Render(SKCanvas canvas, SKImageInfo info, bool clearCanvas = true)
  // clearCanvas: false renders the drawing OVER whatever the caller already
  // drew on the canvas (e.g. a live video frame painted immediately before
  // this call) instead of clearing to SurfaceClearColor first.

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
  DrawingShape DrawShape(DrawingShape shape)   //pre-built or custom shape
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
  event EventHandler RedrawRequested   // invalidate the hosting canvas
  event EventHandler DrawingChanged    // elements committed/cleared/undone

Clearing and undo:
  void Clear()                 // all layers; layers themselves are kept
  bool UndoLastStroke()        // most recent element (stroke OR shape), any layer

Export:
  Size DefaultExportSize { get; }     // background image's pixel size when set
                                      //   (photo exports at original resolution),
                                      //   otherwise CalibrationSize - both match
                                      //   the drawing aspect, never distorted;
                                      //   + GetDefaultExportSizeAsSkia()
  SKImage ExportImage(bool includeBackground = true)              // DefaultExportSize
  byte[] ExportPng(bool includeBackground = true)                 // DefaultExportSize
  byte[] ExportJpeg(int quality = 90)                             // DefaultExportSize
  SKImage ExportImage(Size outputSize, bool includeBackground = true)
  byte[] ExportPng(Size outputSize, bool includeBackground = true)
  void ExportPng(Stream destination, Size outputSize, bool includeBackground = true)
  byte[] ExportJpeg(Size outputSize, int quality = 90)
  // Every size-taking export method also has an SKSizeI overload. Exports
  // are complete from-scratch renders (no display caches), so export
  // quality is independent of the on-screen canvas size. JPEG has no alpha
  // channel - set an opaque BackgroundFillColor for JPEG export. ExportImage
  // returns a SkiaSharp SKImage; for a CodeBrix.Imaging Image<Rgba32>
  // instead, use the ExportImagingImage extension (see Bridge extensions).

DrawingLayer (Models)
------------------------------------------------------------------------
A named, colored, ordered collection of DrawingElement values (freehand
strokes and geometric shapes, interleaved in the order added):
  string Name { get; }               // unique, case-sensitive, trimmed
  Color Color { get; set; }          // changing forces full layer re-render;
                                     //   + SetColor(SKColor) + GetColorAsSkia()
  int ElementCount { get; }          // strokes + shapes
  bool AddStroke(Stroke stroke)      // programmatic stroke injection
  bool AddShape(DrawingShape shape)  // programmatic shape injection
  bool RemoveLastElement()           // simple undo (stroke or shape)
  void Clear()
  DrawingElement[] GetElements()     // everything, in render order
  Stroke[] GetStrokes()              // only the freehand strokes
  int ResetVersion { get; }          // cache-invalidation counter (renderers)
NOTE: AddStroke/AddShape called directly on a layer do NOT raise the
session's events - use the session's Draw* methods (or invalidate the
hosting canvas yourself) when the UI must react.

DrawingElement / Stroke / StrokePoint (Models)
------------------------------------------------------------------------
DrawingElement is the abstract base of everything on a layer; its two
families are Stroke (freehand) and DrawingShape (geometric).

  new Stroke(float width = 15f, DateTimeOffset? startedAtUtc = null)
  bool AddPoint(int x, int y, int timeOffsetMs = 0)  // dedupes repeats
  StrokePoint[] GetPoints(); int PointCount; StrokePoint? LastPoint
  // StrokePoint: readonly struct { int X, int Y, int TimeOffsetMs }.
  // Points are calibrated-space integers; TimeOffsetMs enables replay.
  // A stroke is rendered as a round-capped, round-joined polyline;
  // a single-point stroke renders as a dot (filled circle of the width).

DrawingShape and the shape catalog (Shapes)
------------------------------------------------------------------------
All shape coordinates, radii, and thicknesses are calibrated drawing
units. Every shape has StrokeThickness and an optional Color (a
CodeBrix.Imaging Color?; null = owning layer's color). Read the color
back as SkiaSharp with GetColorAsSkia() (returns SKColor?). The
constructors take Color? (not SKColor?) - a caller holding an SKColor
converts it with skColor.ToImagingColor(); the session's Draw* methods
also take Color?:

  LineShape(x1, y1, x2, y2, strokeThickness = 15, color = null)
  ArrowShape(x1, y1, x2, y2, strokeThickness = 15, color = null,
      headLength = null)                 // V-head at (x2, y2); default head
                                         //   length = max(30, 3 x thickness)
  CircleShape(centerX, centerY, radius, strokeThickness = 15,
      color = null, isFilled = false)
  EllipseShape(centerX, centerY, radiusX, radiusY, strokeThickness = 15,
      color = null, isFilled = false)
  RectangleShape(x, y, width, height, strokeThickness = 15, color = null,
      isFilled = false, cornerRadius = 0)
  PolylineShape(IReadOnlyList<PointF> points, strokeThickness = 15,
      color = null, isClosed = false, isFilled = false)
      // >= 2 points; isFilled implies closed; GetPoints() snapshots as
      //   PointF[], GetPointsAsSkia() as SKPoint[]

Custom shapes: derive from DrawingShape and override
  public override void Draw(SKCanvas canvas, SKColor color)
The canvas arrives PRE-TRANSFORMED to the calibrated drawing space -
draw in calibrated coordinates and use calibrated stroke widths; the
transform scales everything (including paint stroke widths) to the
output size. Helpers: CreateOutlinePaint(color) / CreateFillPaint(color)
build the standard antialiased paints. This is the one extension point
where Skia knowledge is needed.

DrawingRenderer (Rendering; IDisposable)
------------------------------------------------------------------------
Standalone renderer used by DrawingSession - usable directly when an
application manages its own layer collections:
  new DrawingRenderer(Size calibrationSize)          // SKSizeI overload also exists
  void Render(SKCanvas canvas, SKImageInfo info,     // canvas = the Skia surface
      IReadOnlyList<DrawingLayer> layers,
      Stroke activeStroke = null, SKColor? activeStrokeColor = null,
      bool clearCanvas = true)
  SKImage RenderToImage(Size outputSize,             // SKSizeI overload also exists
      IReadOnlyList<DrawingLayer> layers, bool includeBackground = true)
  SKBitmap BackgroundImage;                          // image type (stays SkiaSharp)
  Color BackgroundFillColor / SurfaceClearColor;     // + Set…(SKColor)/Get…AsSkia()
  Size CalibrationSize;                              // + GetCalibrationSizeAsSkia()
  byte LayerOpacity / ActiveStrokeOpacity;
  RectangleF LastDrawingRect; Size LastCanvasSize    // + GetLast…AsSkia() each
Caching internals (all automatic): the background image is rescaled
once per canvas size (high-quality Mitchell resampling); each layer has
an incremental cache bitmap (only elements added since the previous
render are rasterized); and the composited static scene (background +
all layers) is kept in one bitmap so a live-drawing frame costs a 1:1
blit plus the in-progress stroke. Caches invalidate automatically on
canvas resize, background change, opacity/fill/clear-color change,
layer membership change, and DrawingLayer.ResetVersion bumps (element
removal, layer clear, color change). Element APPENDS never force a
full redraw. Never rescale-per-frame work is reintroduced here - it
was the cause of a 17x drawing-lag bug (66 ms -> 4 ms per frame).

CanvasCalibration (Rendering; static)
------------------------------------------------------------------------
Pure coordinate math - useful for custom hit testing or overlays. Each
helper has a CodeBrix.Imaging-typed form and a SkiaSharp-typed form:
  RectangleF GetDrawingRect(Size canvasSize, Size calibrationSize)
  Point? ViewPointToCalibrated(PointF viewPoint, SizeF viewSize,
      Size canvasSize, Size calibrationSize, bool clampToDrawingArea = false)
  PointF CalibratedToCanvas(Point calibratedPoint, Size calibrationSize, RectangleF drawingRect)
  float ScaleStrokeWidth(float calibratedWidth, Size calibrationSize, RectangleF drawingRect)
  // The SkiaSharp forms (SKRect/SKPointI/SKSizeI/SKPoint/SKSize) also exist.
The drawing rectangle is the centered aspect-fit rectangle with the
calibration space's aspect ratio - match CalibrationSize to the
background image's aspect ratio for edge-to-edge alignment.

Bridge extensions (Extensions)
------------------------------------------------------------------------
To CodeBrix.Imaging images (for further processing pipelines):
  Image<Rgba32> skImage.ToImagingImage()
  Image<Rgba32> skBitmap.ToImagingImage()
  Image<Rgba32> session.ExportImagingImage(Size outputSize, bool includeBackground = true)
  // (An SKSizeI overload also exists.) Returned images are caller-disposed.
Between color types (ColorBridgeExtensions):
  SKColor imagingColor.ToSKColor()
  Color   skColor.ToImagingColor()
Between geometry types (GeometryBridgeExtensions) - both directions:
  Size <-> SKSizeI    (ToSKSizeI / ToImagingSize)
  SizeF <-> SKSize     (ToSKSize / ToImagingSizeF)
  Point <-> SKPointI   (ToSKPointI / ToImagingPoint)
  PointF <-> SKPoint   (ToSKPoint / ToImagingPointF)
  RectangleF <-> SKRect (ToSKRect / ToImagingRectangleF)
  // These are the general-purpose way to move any returned CodeBrix.Imaging
  // value into SkiaSharp (or vice versa) in a single call.

Error model
------------------------------------------------------------------------
Standard .NET exceptions only: ArgumentNullException /
ArgumentException / ArgumentOutOfRangeException for bad inputs,
ObjectDisposedException after disposal, InvalidOperationException for
invalid states (drawing a shape with no active layer; unreadable pixel
data). No custom exception types.

Typical hosting pattern (any UI framework)
------------------------------------------------------------------------
  // paint:    session.Render(e.Surface, e.Info);
  // invalidate: session.RedrawRequested += (s, e) => canvas.Invalidate();
  //   (marshal to the UI thread if the framework requires it)
  // mouse:    on left-button down    -> session.PointerPressed(pt, viewSize)
  //           on move                -> session.PointerMoved(pt, viewSize)
  //           on up                  -> session.PointerReleased()
  //           on capture lost        -> session.PointerCanceled()
  // The view should capture the pointer while a stroke is active so
  // strokes continue when the pointer leaves the control.
The PainDiagram sample (next section) implements this pattern for
CodeBrix.Platform, native WinUI 3, and native WPF.

THE PAINDIAGRAM SAMPLE APPLICATION (samples/PainDiagram)
========================================================================

What it is: a complete reference application that replicates a clinical
"draw your pain, numbness and tingling on a body map" tablet workflow
with the mouse. Three highlighter layers (Pain = pink #FF1EE6,
Numbness = blue #1E80CC, Tingling = yellow-gold #CCAA0A) over a square
3100 x 3100 black-and-white body-map PNG, with Clear (confirmation once
more than two elements exist) and Save (file dialog -> 1000 x 1000 PNG
-> optional clear-after-save prompt). It is the template to copy when
building a new application on this library.

Solution layout - two solutions, per the CodeBrix.Platform convention:
  PainDiagram.slnx          - cross-platform: builds with the plain .NET
                              SDK on Linux/macOS/Windows (all Skia heads)
  PainDiagram.Windows.slnx  - superset adding the native WinUI 3 and WPF
                              heads, which need Windows-only build tooling
                              (open on a Windows machine; solution
                              platforms restricted to x86/x64/ARM64 to
                              match the WinUI project)

Project map (8 heads, ONE view model):
  Shared/                        - loose files, linked (not a project):
    ViewModels/MainViewModel.cs  - THE app logic, compiled into 3 different
                                   assemblies (Core, WinUI, Wpf)
    Helpers/HostHelper.cs        - IHostBuilderProvider over Generic Host
    Helpers/FileDialogHelper.cs  - RemoveEmptyPlaceholder (see Save flow)
    Assets/body_map_master.png   - the background image
  CodeBrixPlatform/PainDiagram.Core     - class library: all common
    CodeBrix.Platform package refs + linked Shared files + the embedded
    body map + ProjectReference to this library
  CodeBrixPlatform/PainDiagram.UI       - shared project (.shproj/.projitems):
    App.xaml(.cs) + Views/MainPage.xaml(.cs) - the XAML UI compiled into
    every Skia head (WinUI XAML dialect)
  CodeBrixPlatform/PainDiagram.<Head>   - six thin executables:
    Win32Skia, WinWpfSkia, LinuxX11, LinuxWayland, LinuxFrameBuffer, MacOS
  PainDiagram.WinUI              - native WinUI 3 head (own XAML copy)
  PainDiagram.Wpf                - native WPF head (own XAML copy)

How the pieces connect, end to end:

1. MainViewModel (Shared) derives from SimpleViewModel - the CodeBrix
   "Simple" MVVM API that is IDENTICAL across CodeBrix.Platform
   (package CodeBrix.Platform.ApacheLicenseForever, namespace
   CodeBrix.Platform.Simple), native WinUI (CodeBrix.Platform.WinUI.*)
   and WPF (CodeBrix.Platform.WPF.*). That identical API is what lets
   one view-model file serve all 8 heads. Patterns used:
   - Properties: backing field + SetProperty(ref field, value), with
     [AffectsCommands(nameof(SaveCommand), ...)] to re-evaluate command
     enablement when the property changes.
   - Commands: lazily created SimpleCommand((CanX, DoX)) where DoX is
     an async Task method.
   - Dialogs: await ShowInfo(...) / ShowError(...) / ConfirmDialog(msg,
     title) from the base class.
   - The VM carries [Microsoft.UI.Xaml.Data.Bindable] under
     #if HAS_CODEBRIX (required for bindings on Skia heads; compiles
     out on native WPF where that symbol is not defined).

2. The VM owns the DrawingSession. In its constructor (guarded by
   !IsDesignMode(true)) it creates the session with White background
   fill and surface colors, adds the three layers, and loads the body
   map via Assembly.GetManifestResourceStream. The embedded-resource
   trick: the VM is compiled into three DIFFERENT assemblies, so each
   of those projects (Core, WinUI, Wpf) embeds the PNG with the SAME
   explicit <LogicalName>PainDiagram.Assets.body_map_master.png
   </LogicalName>, letting one line of VM code find it everywhere.

3. View <-> VM decoupling uses two tiny bridge interfaces the VM
   implements and each head's code-behind fulfills:
     IFileSaveBridge  { Func<string, Task<string>> PickSavePngPathAsync }
     ICanvasInvalidator { Action InvalidateCanvas }
   The session's RedrawRequested event is forwarded through
   InvalidateCanvas, so the VM never references a UI type.

4. CRITICAL wiring order (a real bug the first build shipped): the
   pages subscribe DataContextChanged BEFORE calling
   InitializeComponent(), because the XAML itself sets Page.DataContext
   (<Page.DataContext><vm:MainViewModel /></Page.DataContext>) during
   InitializeComponent - subscribing afterwards means the handler never
   fires and none of the bridges get wired (symptom: drawing input is
   captured but nothing ever repaints).

5. Canvas wiring per head (the part to copy into a new app): every head
   hosts the shared <drawing:DrawingCanvas/> control - see "KEEPING
   SKIASHARP OUT OF THE HOSTING APP'S XAML (the DrawingCanvas trick)"
   above. That one linked file compiles to SkiaSharp.Views.Windows.
   SKXamlCanvas on the Skia heads + native WinUI (from CodeBrix.Platform.
   SkiaSharp.Views.MitLicenseForever 4.150.0 and SkiaSharp.Views.WinUI
   4.150.0 respectively) and to SkiaSharp.Views.WPF.SKElement on native
   WPF (4.150.0). The XAML is just <drawing:DrawingCanvas x:Name=
   "DrawCanvas" />; the page's constructor wires it after
   InitializeComponent with lambdas, so no code-behind carries a
   using SkiaSharp;:
   - WinUI dialect (Skia heads + native WinUI):
       PaintSurface        -> Session.Render(e.Surface, e.Info)
       PointerPressed      -> if left button && Session.PointerPressed(
                                DrawCanvasHelper.GetPointFromPosition(pos),
                                DrawCanvas.GetViewSize())
                              then DrawCanvas.CapturePointer(e.Pointer)
       PointerMoved        -> Session.PointerMoved(...) when IsPointerActive
       PointerReleased     -> Session.PointerReleased() + release capture
       PointerCaptureLost  -> Session.PointerCanceled()
       SizeChanged         -> DrawCanvas.Invalidate()
   - Native WPF: the same shape with MouseDown/MouseMove/MouseUp/
     LostMouseCapture + CaptureMouse()/ReleaseMouseCapture(), and
     InvalidateVisual() for redraw (marshalled via Dispatcher when
     needed). NOTE: the WPF project must target
     net10.0-windows10.0.19041.0 - SkiaSharp.Views.WPF has no assets
     for bare net10.0-windows and would silently restore its .NET
     Framework assembly (NU1701).

6. Save flow (one overwrite prompt, owned by the VM): the VM asks the
   head for a path via PickSavePngPathAsync(suggestedName); if the file
   exists it asks ConfirmDialog("replace?"); then ExportPng + write;
   then asks whether to clear the drawing. To keep that single prompt,
   every head's native dialog suppresses its own overwrite prompt:
   - Skia heads: CodeBrix.Platform's WinRT-shaped FileSavePicker
     (Windows.Storage.Pickers). It creates an empty placeholder file
     for new names - FileDialogHelper.RemoveEmptyPlaceholder deletes it
     (only if genuinely empty). Awaiting the picker REQUIRES
     `using System;` (the IAsyncOperation GetAwaiter extension lives in
     the System namespace inside CodeBrix.Platform).
   - Native WinUI: Views/Win32SaveFileDialog.cs, a COM IFileSaveDialog
     wrapper, because the WinRT picker's own overwrite prompt cannot be
     turned off. It clears FOS_OVERWRITEPROMPT and needs the window
     HWND (exposed as App.CurrentWindow).
   - Native WPF: Microsoft.Win32.SaveFileDialog with
     OverwritePrompt = false.
   - Linux framebuffer head has no dialogs: PickSavePngPathAsync stays
     null and the VM saves to a default Pictures-folder path instead.

7. Head project recipe (each Skia head csproj is ~30 lines):
   - <DefineConstants>$(DefineConstants);HAS_CODEBRIX;HAS_CODEBRIX_WINUI
     on Core AND every head (CodeBrix.Platform internal conditionals).
   - The Uno-style XAML glob: <Page Include="**\*.xaml" .../> +
     <None Remove="**\*.xaml" />.
   - <Import Project="..\PainDiagram.UI\PainDiagram.UI.projitems" />.
   - ProjectReference to Core + exactly ONE platform package:
     CodeBrix.Platform.Runtime.Skia.{Win32|Wpf|X11|Wayland|FrameBuffer|
     MacOS}.ApacheLicenseForever.
   - Program.cs: CodeBrixPlatformHostBuilder.Create().App(() => new
     App()).Use<Backend>().Build().Run(); the WinWpfSkia head also
     forces WpfHost.RenderSurfaceType = RenderSurfaceType.Software
     (avoids WPF airspace conflicts) and must NOT set <UseWPF>; it
     targets net10.0-windows with <EnableWindowsTargeting> so it still
     compiles inside the cross-platform solution on Linux/macOS.

To reuse PainDiagram as a template for a new drawing app:
  1. Copy the folder structure; rename projects/namespaces.
  2. Rewrite MainViewModel: your layers, your background (or a
     CreateForImage factory for photo annotation), your commands - keep
     the two bridge interfaces and the SimpleViewModel patterns.
  3. Keep the pages' canvas wiring verbatim (PaintSurface + pointer
     handlers + capture + the DataContextChanged-before-
     InitializeComponent ordering).
  4. Keep the per-head save-dialog implementations; change the file
     extension/filters.
  5. Update the embedded resource LogicalName in all three embedding
     projects if you rename the app.

CODING CONVENTIONS (CodeBrix family)
========================================================================

- Nullable reference types are OFF family-wide: no "?" annotations on
  reference types (string?, MyClass?), no null-forgiveness operator
  (!). Value-type nullables (int?, SKPointI?, Color?) are fine.
- No global usings; no ImplicitUsings; all using directives are at the
  top of each file, System.* first.
- File-scoped namespaces only (namespace X; - never braced blocks).
- XML documentation comments are REQUIRED on every public type and
  member (GenerateDocumentationFile is on; CS1591 is fixed at source,
  never suppressed).
- No project-level warning suppression (<NoWarn>, pragma disables).
- Tests: xUnit v3 + SilverAssertions fluent assertions
  (value.Should().Be(expected)), test classes named
  <ClassUnderTest>Tests, methods in Member_snake_case or snake_case
  form, //Arrange //Act //Assert comments in multi-statement tests.
- The date-stamped package version (1.<years>.<dayOfYear>.<minuteOfDay>)
  is computed by MSBuild at build time - never hardcode <Version>.
- SkiaSharp 4.148 API notes: SKPath.MoveTo/LineTo are obsolete (build
  with SKPathBuilder then .Detach()); DrawBitmap overloads require
  SKSamplingOptions.

ARCHITECTURE
========================================================================

src/CodeBrix.Imaging.Drawing/
  DrawingSession.cs            - main entry point (root namespace)
  DrawingSessionOptions.cs     - session construction options
  CalibrationSizing.cs         - explicit factory sizing choice (enum)
  InternalsVisibleTo.cs        - grants internals to the .Tests project
  Models/                      - DrawingElement, Stroke, StrokePoint, DrawingLayer
  Shapes/                      - DrawingShape + Line/Circle/Ellipse/
                                 Rectangle/Polyline/Arrow shapes
  Rendering/                   - DrawingRenderer, CanvasCalibration
  Extensions/                  - ImagingBridgeExtensions, ColorBridgeExtensions

tests/CodeBrix.Imaging.Drawing.Tests/   - xUnit v3 test suite

samples/PainDiagram/           - reference application (see the section above)

The library layers cleanly: Models + Shapes (SkiaSharp types only) ->
Rendering (SkiaSharp only) -> DrawingSession (facade) -> Extensions
(CodeBrix.Imaging bridge). Nothing references any UI framework. The
drawing-primitives geometry is original code rendered through the
SkiaSharp dependency - no third-party drawing source is incorporated
(see THIRD-PARTY-NOTICES.txt).

TESTING
========================================================================

  dotnet test CodeBrix.Imaging.Drawing.slnx

Tests are pure managed SkiaSharp (raster surfaces; no GPU, no display
server needed) and run headlessly on Linux, macOS, and Windows. The
test project references the SkiaSharp.NativeAssets.* packages so the
native Skia library is present at test time. Rendering tests assert on
actual pixels (e.g. the highlighter guarantee: two overlapping strokes
on one layer produce pixel-identical output to one stroke; a blue
shape on a red layer renders blue). When changing the renderer, keep
the pixel tests passing and never reintroduce per-frame rescaling of
the background image.
