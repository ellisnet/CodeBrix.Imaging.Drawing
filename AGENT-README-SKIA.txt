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
- Export of the finished drawing as PNG/JPEG bytes, a SkiaSharp SKImage, a
  CodeBrix.Imaging Image<Rgba32>, or plain straight-alpha RGBA bytes.
- Animation-frame building blocks: easing curves and color ramps, linear and
  radial gradient fills, text from a font file you register (never a system
  font), shape bounds, a whole-frame fade, and social-media / broadcast
  safe-area guides (an opt-in check and an opt-in preview overlay). The
  library PRODUCES frames; assembling them into a video or GIF is left to
  your encoder (for example CodeBrix.VideoProcessing).
- Pixel planes for generating frames in float colour: per-pixel procedural
  fills, blur, bloom, vignette and tone curves, compositing, frame
  transitions (dissolve, wipe, slide, push, fade through a colour), seeded
  random numbers and noise, particles and motion-blur averaging - plain
  managed arithmetic, bit-identical in both packages - plus session exports
  into float images and coverage masks, and a particles shape.
- An animation model: a timeline with named cues, keyframe tracks bound to
  element, group, layer and camera properties, transforms and opacity,
  groups that fade as one unit, elements that exist only within a time
  window, shapes rebuilt from the time, a camera with per-layer parallax,
  layout anchors against the frame or a safe area, a frame-sequence renderer
  that can use several threads, a UI-free playback controller and an opt-in
  debug overlay. Build a scene once, then set session.Time to show any
  moment.
- Paths, pictures and effects: general paths (lines, curves, arcs or SVG
  path data) stroked with caps, joins and dashes and filled, drawn on by
  trimming, followed by moving elements and morphed into each other (every
  measurement identical in both packages); pictures and sprites as
  elements; clipping and masks per layer and per group; drop shadow, glow
  and blur per element, group or layer, computed from the drawn pixels and
  sized with the element's scale and the camera; paths combined as areas
  (union, intersection, difference, exclusive-or), strokes turned into
  fillable outlines and shapes inflated or deflated, in exact integer
  arithmetic.
- Text for animation: kerned text (tracked text too); laid-out blocks -
  wrapped, aligned (justified too), spaced, trimmed with an ellipsis - with
  every line's metrics and every character's box and glyph outline;
  typewriter and soft reveals by character, word or line; characters, words
  and lines as separate elements with staggered entrances; outlined,
  gradient, drawn-on text and text along a path; and burned-in captions
  from SRT or WebVTT placed on a safe area. Layout is identical in both
  packages.

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
API is published from the same repository, documented in its own
AGENT-README.txt (AGENT-README-NOSKIA.txt in the repository); see "WHICH ONE
DO I REFERENCE" under INSTALLATION.

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
- Trimming: trim-clean - a fully trimmed publish (PublishTrimmed, and
  Android Release builds) reports no trim warnings for this package.
- NuGet dependencies (restored automatically):
    SkiaSharp
    CodeBrix.Imaging.ApacheLicenseForever
    CodeBrix.PolygonTools.MitLicenseForever   (MIT AND BSL-1.0; the managed,
        dependency-free clipping engine behind path combining, stroke
        outlines and inflating)
- Native requirement: applications must provide the SkiaSharp native assets
  for their platform. Desktop apps built on CodeBrix.Platform get them from
  their platform head package; plain .NET apps add
  SkiaSharp.NativeAssets.Linux / SkiaSharp.NativeAssets.macOS /
  SkiaSharp.NativeAssets.Win32 as appropriate.

WHICH ONE DO I REFERENCE
------------------------
Reference CodeBrix.Imaging.Drawing.ApacheLicenseForever - the package THIS
file documents - when you want Skia. Apache-2.0. SkiaSharp-backed: Skia's
performance and the full non-drawing Skia surface alongside it.

The same repository also produces a COMPLETELY MANAGED companion package,
CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever (Apache-2.0): the
identical drawing-session API plus a fully managed drawing engine and a
managed SVG renderer, with zero native dependence and slower CPU rendering.
It is published on nuget.org as well; reference it instead of this package
when the application must carry no native dependency. Its guide ships inside
that package (AGENT-README-NOSKIA.txt in the repository).

The two are EITHER/OR alternatives: NEVER reference both packages from one
application, because they compile the SAME drawing-session source files into
the SAME CodeBrix.Imaging.Drawing namespaces (deliberately, so switching is a package swap).

KEY NAMESPACES / USINGS
=======================

    using CodeBrix.Imaging.Drawing;             // DrawingSession, DrawingSessionOptions, CalibrationSizing,
                                                //   MaskMeaning, FrameSequenceRenderer, FrameRenderedCallback,
                                                //   FrameCache
    using CodeBrix.Imaging.Drawing.Models;      // DrawingLayer, DrawingElement, Stroke, StrokePoint, DrawingTransform,
                                                //   DrawingCamera, DrawingAnimations, AnimationBinding, AnimatedProperty,
                                                //   StaggerOptions, StaggerOrder, ElementAxes, BindingMode,
                                                //   PathTravelOptions, PathTrain, PathTrainCar, PathTrainOptions
    using CodeBrix.Imaging.Drawing.Shapes;      // DrawingShape + Line/Arrow/Circle/Ellipse/Rectangle/Polyline shapes,
                                                //   DrawingGroup, AnimatedShape, PathShape, ImageShape, SpriteShape,
                                                //   TextShape, TextBlockShape, BrushStrokeShape, ParticlesShape
    using CodeBrix.Imaging.Drawing.Rendering;   // DrawingRenderer, CanvasCalibration, DebugOverlay, DebugOverlayCorner
    using CodeBrix.Imaging.Drawing.Extensions;  // CodeBrix.Imaging image + color + geometry bridge extensions
    using CodeBrix.Imaging.Drawing.Animation;   // Easing, EasingKind, StepPosition, CubicBezierEasing, ColorRamp,
                                                //   AnimationTimeline, TimelineCue, KeyframeTrack<T>, Keyframe<T>,
                                                //   FloatTrack, PointTrack, ColorTrack, BoolTrack, TrackLoopMode,
                                                //   PlaybackController
    using CodeBrix.Imaging.Drawing.Layout;      // DrawingLayout, LayoutAnchor, LayoutPosition, LayoutReference
    using CodeBrix.Imaging.Drawing.Fills;       // ShapeFill, GradientFill, LinearGradientFill, RadialGradientFill,
                                                //   ColorStop, GradientSpread
    using CodeBrix.Imaging.Drawing.Text;        // FontRegistry, TextAlign, TextLayoutEngine, TextLayoutOptions,
                                                //   TextLayoutResult, TextLineMetrics, TextCluster, TextPiece,
                                                //   TextRunDescriptor, TextVerticalAlign, TextTrimming, TextUnit,
                                                //   TextStrokePlacement
    using CodeBrix.Imaging.Drawing.Captions;    // CaptionTrack, CaptionCue, CaptionStyle, CaptionPresenter,
                                                //   CaptionFormat
    using CodeBrix.Imaging.Drawing.SafeAreas;   // SafeAreaGuide, SafeAreaGuides, SafeAreaMargins, SafeAreaCrop,
                                                //   SafeAreaTimeRule, SafeAreaZone, SafeAreaOverlay,
                                                //   SafeAreaCheckOptions, SafeAreaFinding, SafeAreaFindingKind
    using CodeBrix.Imaging.Drawing.Planes;      // float planes/images, filters, transitions, noise,
                                                //   seeded random, particles, motion blur
    using CodeBrix.Imaging.Drawing.Paths;       // PathGeometry, PathGeometryBuilder, PathContour, PathSegment,
                                                //   PathPoint, PathPolyline, PathMorph, PathStrokeStyle,
                                                //   PathBooleanOperation, ...
    using CodeBrix.Imaging.Drawing.Images;      // ImageSource, SpriteSheet, ImageSampling, SpritePlayback
    using CodeBrix.Imaging.Drawing.Effects;     // DrawingEffects, DrawingMask, DrawingMaskKind, ShadowDirection
    using CodeBrix.Imaging.Drawing.Painting;    // DrawingOwnershipMap, ElementPixel, KeyframePlayer,
                                                //   KeyframePaintCallback, PlaneKeyframePlayer
    using CodeBrix.Imaging.Drawing.Formations;  // FormationArea, FormationTargets, FormationTarget(Options),
                                                //   arrival orders and styles, FormationPose, ArrivalSchedule,
                                                //   ArrivalRateProfile, FormationMember, FormationElements

Because CodeBrix.Imaging.Drawing nests under the CodeBrix.Imaging namespace,
the CodeBrix.Imaging `Color` type resolves as just `Color` inside consuming
code that uses these namespaces. (Add an explicit `using CodeBrix.Imaging;`
when your own code lives in an unrelated namespace, so that `Color`, `Size`,
`SizeF`, `Point`, `PointF` and `RectangleF` resolve.)

Public types: DrawingSession, DrawingSessionOptions, CalibrationSizing,
MaskMeaning, FrameSequenceRenderer, FrameRenderedCallback, FrameCache (root); DrawingElement, Stroke,
StrokePoint, DrawingLayer, DrawingTransform, DrawingCamera,
DrawingAnimations, AnimationBinding, AnimatedProperty, StaggerOptions,
StaggerOrder, ElementAxes, BindingMode, PathTravelOptions, PathTrain,
PathTrainCar, PathTrainOptions (Models);
DrawingShape, LineShape, ArrowShape, CircleShape, EllipseShape,
RectangleShape, PolylineShape, TextShape, ParticlesShape, DrawingGroup,
AnimatedShape, PathShape, ImageShape, SpriteShape, TextBlockShape,
BrushStrokeShape (Shapes); DrawingRenderer, CanvasCalibration, DebugOverlay,
DebugOverlayCorner (Rendering); ColorBridgeExtensions,
GeometryBridgeExtensions, ImagingBridgeExtensions (Extensions); Easing,
EasingKind, StepPosition, CubicBezierEasing, ColorRamp, AnimationTimeline,
TimelineCue, KeyframeTrack<T>, Keyframe<T>, FloatTrack, PointTrack,
ColorTrack, BoolTrack, TrackLoopMode, PlaybackController (Animation); DrawingLayout,
LayoutAnchor, LayoutPosition, LayoutReference (Layout);
ShapeFill, GradientFill, LinearGradientFill, RadialGradientFill, ColorStop,
GradientSpread (Fills); FontRegistry, TextAlign, TextLayoutEngine,
TextLayoutOptions, TextLayoutResult, TextLineMetrics, TextCluster, TextPiece,
TextRunDescriptor, TextVerticalAlign, TextTrimming, TextUnit,
TextStrokePlacement (Text); CaptionTrack, CaptionCue, CaptionStyle,
CaptionPresenter, CaptionFormat (Captions); SafeAreaGuide,
SafeAreaGuides, SafeAreaMargins, SafeAreaCrop, SafeAreaTimeRule,
SafeAreaZone, SafeAreaOverlay, SafeAreaCheckOptions, SafeAreaFinding,
SafeAreaFindingKind (SafeAreas); DrawingPlane, DrawingPlaneImage,
DrawingPlaneResampling, DrawingTransition, DrawingTransitionKind,
DrawingTransitionDirection, DrawingRandom, DrawingNoise, DrawingNoiseKind,
DrawingParticleSystem, DrawingParticleEmitter, DrawingParticle,
DrawingFrameAccumulator, DrawingMotionBlur (Planes); PathGeometry,
PathGeometryBuilder, PathContour, PathSegment, PathSegmentKind, PathPoint,
PathPolyline, PathFillRule, PathMorph, PathStrokeStyle, PathStrokeCap,
PathStrokeJoin, PathBooleanOperation (Paths); ImageSource, SpriteSheet, ImageSampling,
SpritePlayback (Images); DrawingEffects, DrawingMask, DrawingMaskKind, ShadowDirection
(Effects); DrawingOwnershipMap, ElementPixel, KeyframePlayer,
KeyframePaintCallback, PlaneKeyframePlayer (Painting); FormationArea,
FormationTargets, FormationTarget, FormationTargetOptions, IArrivalOrder,
RandomArrivalOrder, CenterOutArrivalOrder, DirectionalArrivalOrder,
HeapArrivalOrder, CustomArrivalOrder, FormationPose, IArrivalPath,
SwayingFallArrival, StraightArrival, CurvedArrival, SpiralArrival,
ArrivalRateProfile, ArrivalSchedule, FormationMember, FormationElements
(Formations). Every one is documented below.

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

  1. Method overloads and helpers. The core drawing-session surface -
     construction and CreateForImage, layers, the background, pointer
     input, Render, GetDrawingRect/ScaleToView, the PNG/JPEG/SKImage exports
     and DrawingRenderer's core members - has a sibling overload taking the
     SkiaSharp type wherever it takes a CodeBrix.Imaging value type (the
     full overload sets are enumerated in CORE API REFERENCE below).
     Properties cannot be overloaded by type, so each core property that is
     a CodeBrix.Imaging type also has a `Set…(SKColor/SKSizeI)` helper (or a
     fluent one on DrawingSessionOptions) for writing and a `Get…AsSkia()`
     method for reading. Everything else - the Draw* helpers, the raw-pixel,
     plane, mask, ownership-map and overlay exports, and the animation,
     planes, paths, text, captions, safe-area and painterly APIs - takes
     CodeBrix.Imaging types only; convert with the bridge extensions (2).
     For example:
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
    PathShape    DrawPath(PathGeometry geometry, float thickness = 15, Color? color = null, bool filled = false)
    ImageShape   DrawImage(ImageSource source, RectangleF destination, ImageSampling sampling = Linear)
    SpriteShape  DrawSprite(SpriteSheet sheet, RectangleF destination, double framesPerSecond = 0,
                            SpritePlayback playback = Loop, ImageSampling sampling = Linear)
    // (paths, pictures and sprites: see PATHS, PICTURES, SPRITES, MASKS AND EFFECTS)
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
    // frame (e.g. tracking a detected object), either draw them directly on
    // the canvas after session.Render, or give the element a Transform (see
    // ANIMATION MODEL) - moving it redraws only its own layer.

State and events:
    bool HasStrokes { get; }             // any completed element (stroke or shape)
    int StrokeCount { get; }             // total elements across all layers
    bool IsDisposed { get; }
    event EventHandler RedrawRequested   // invalidate the hosting canvas
    event EventHandler DrawingChanged    // elements committed/cleared/undone

Clearing and undo:
    void Clear()                 // all layers; layers themselves are kept
    bool UndoLastStroke()        // most recent element (stroke OR shape), any layer

Export - the encoded and SKImage overloads (the raw RGBA, plane, mask,
ownership-map and overlay exports are under ANIMATION FRAMES, PIXEL PLANES
and PAINTERLY TOOLS):
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
    FontRegistry Fonts        { get; set; }        // = null: the session gets an empty registry of its own;
                                                   //   set one to share a registry between sessions

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
    // Its constructor is private protected: only Stroke and DrawingShape
    // derive from it directly. Its members: IsSafeAreaChecked, and the
    // animation-model properties Transform, Opacity, StartTime, EndTime,
    // IsVisibleAt(seconds) and GetTransformedBounds() (see ANIMATION MODEL). A custom element
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
    TextShape(text, x, y, size, FontRegistry fonts, Color? color = null, ...)
        // single-line text from a registered font - see ANIMATION FRAMES
    Each shape's geometry properties are read-only (get-only) and every shape
    overrides Draw(SKCanvas, SKColor). Filled shapes also take an optional
    ShapeFill (gradients) and every shape reports GetBounds() - see
    ANIMATION FRAMES - and GetOutline(), its geometry as a PathGeometry (see
    PATHS, PICTURES, SPRITES, MASKS AND EFFECTS, which also covers PathShape,
    ImageShape and SpriteShape).

Custom shapes: derive from DrawingShape and override
    public override void Draw(SKCanvas canvas, SKColor color)
The canvas arrives PRE-TRANSFORMED to the calibrated drawing space - draw in
calibrated coordinates and use calibrated stroke widths; the transform scales
everything (including paint stroke widths) to the output size. Helpers:
CreateOutlinePaint(color) / CreateFillPaint(color) build the standard
antialiased paints. This is the one extension point where Skia knowledge is
needed. Example:

    using CodeBrix.Imaging;
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
    double Time { get; set; }                          // time windows are evaluated at it (ANIMATION MODEL)
    DrawingCamera Camera { get; set; }                 // null = none
    DebugOverlay DebugOverlay { get; set; }            // live Render only
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
change). Element APPENDS never force a full redraw. With the animation model
a layer's cache is also redrawn when something already drawn on it changes
(a transform, an opacity, a time window closing, a group's content) or its
layer transform or the camera changes; elements appearing in list order stay
incremental (see "How the caches treat changing content").

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

ANIMATION FRAMES
================

Everything in this section is OPT-IN: a drawing that uses none of it renders
exactly as a plain drawing does, through the same code. It is compiled from the same shared sources
in both packages, so values COMPUTED in managed code (easing values, ramp
colors, text layout and outline geometry, shape bounds, safe-area zones and
findings) are identical in both packages; what is drawn through each
package's antialiased canvas (gradients, glyph fills, fades, overlays)
matches within the usual edge tolerance.

The simplest animation model is "rebuild what moves": shapes are immutable
values, so a frame loop clears a "moving" layer, adds this frame's shapes,
then exports. Static layers (backgrounds, labels) are built once. (The
ANIMATION MODEL section below adds the alternative: build everything once,
with transforms, keyframe tracks and time windows, and set session.Time.)

    var session = new DrawingSession(new DrawingSessionOptions
    {
        CalibrationSize = new Size(1920, 1080), LayerOpacity = 255,
        BackgroundFillColor = Color.Black,
    });
    session.Fonts.RegisterFont("assets/Fonts/OpenSans-Regular.ttf");
    session.AddLayer("Static", Color.White);         // title etc., built once
    DrawingLayer now = session.AddLayer("Now", Color.White);
    var frame = new byte[1920 * 1080 * 4];
    for (int i = 0; i < frameCount; i++)
    {
        double t = i / 30.0;
        now.Clear();
        float x = Easing.Lerp(100f, 1800f, Easing.CubicInOut(t / 4));
        now.AddShape(new CircleShape(x, 540, 40, 1, ramp.At(t / 4), isFilled: true));
        session.Opacity = (float)Easing.SmoothStep(t / 1.0);   // 1 s fade-in
        session.ExportPixels(frame, new Size(1920, 1080));       // raw RGBA
        encoder.Write(frame);                                    // your video pipe
    }

Raw RGBA frames (DrawingSession, DrawingRenderer)
-------------------------------------------------
    void   ExportPixels(Span<byte> destination, Size outputSize, bool includeBackground = true)
    void   ExportPixels(Span<byte> destination, SKSizeI outputSize, bool includeBackground = true)
    void   ExportPixels(Span<byte> destination, Size outputSize, bool includeBackground, SafeAreaOverlay safeAreaOverlay)
    byte[] ExportPixels(Size outputSize, bool includeBackground = true)
    byte[] ExportPixels(SKSizeI outputSize, bool includeBackground = true)
    byte[] ExportPixels(bool includeBackground = true)          // DefaultExportSize
    // DrawingRenderer: RenderToPixels(Span<byte>, Size|SKSizeI, layers, includeBackground = true)
    //   and RenderToPixels(Span<byte>, Size, layers, includeBackground, SafeAreaOverlay)
BYTE LAYOUT: four bytes per pixel in R, G, B, A order, rows top to bottom,
no row padding (stride = width x 4), STRAIGHT (not premultiplied) alpha -
exactly the layout of CodeBrix.Imaging Image<Rgba32> and of ffmpeg's "rgba"
pixel format. The frame is a complete from-scratch render, like every
export. includeBackground: false renders over transparency and the alpha
bytes carry it. The destination must hold at least width x height x 4 bytes
(ArgumentException otherwise); extra bytes are left untouched. Reuse one
buffer for every frame.

Easing (Animation; static)
--------------------------
Each curve maps progress t in 0..1 to an eased value from 0 to 1. Inputs
outside 0..1 are clamped and NaN counts as 0. Back curves overshoot and
Elastic curves oscillate by design; everything else stays in 0..1.
    double Linear/SmoothStep/SmootherStep(double t)
    double QuadIn/QuadOut/QuadInOut, CubicIn/Out/InOut, QuartIn/Out/InOut,
           QuintIn/Out/InOut, SineIn/Out/InOut, ExpoIn/Out/InOut,
           CircIn/Out/InOut, BackIn/Out/InOut, ElasticIn/Out/InOut,
           BounceIn/Out/InOut (double t)
    double BackIn/BackOut/BackInOut(double t, double overshoot)   // DefaultBackOvershoot = 1.70158
    double SmoothStep(double edge0, double edge1, double x)       // GLSL smoothstep
    double Steps(double t, int count, StepPosition position = StepPosition.End)
                                         // CSS steps(): End, Start, None, Both
    double CubicBezier(double t, double x1, double y1, double x2, double y2)
    double Evaluate(EasingKind kind, double t)
    Func<double, double> GetFunction(EasingKind kind)             // cached delegate
    double Clamp01(double t)
    double Lerp(double from, double to, double t)                 // not clamped
    float  Lerp(float from, float to, double t)
EasingKind lists every named curve above (for choosing a curve by value).
CubicBezierEasing(x1, y1, x2, y2) is the CSS cubic-bezier() curve (x values
in 0..1, y values may overshoot): Evaluate(double t); presets Ease, EaseIn,
EaseOut, EaseInOut. Its Evaluate method group converts to Func<double, double>.

ColorRamp (Animation; sealed, immutable)
----------------------------------------
    ColorRamp(IReadOnlyList<ColorStop> stops)   // >= 2 stops, positions non-decreasing
    ColorRamp(params Color[] colors)            // evenly spaced 0..1
    Color At(double t)                          // alpha interpolated too
    Color At(double t, byte alpha)              // ramp hue, your alpha
    ColorStop[] GetStops(); int StopCount
INTERPOLATION SPACE: straight (not premultiplied) 8-bit sRGB, per channel
including alpha, linear between the two stops around t, rounded half away
from zero; no gamma or perceptual correction. Before the first stop the
first color holds, after the last the last color holds; stops sharing a
position make a hard edge, and t exactly there takes the later stop. This is
the same space the gradient fills use, so
new LinearGradientFill(a, b, ramp.GetStops()) paints what ramp.At reports.

Gradient fills (Fills)
----------------------
    ColorStop(float position, Color color)      // position 0..1
    LinearGradientFill(PointF start, PointF end, IReadOnlyList<ColorStop> stops,
                       GradientSpread spread = GradientSpread.Pad)
    LinearGradientFill(PointF start, PointF end, Color startColor, Color endColor)
    RadialGradientFill(PointF center, float radius, IReadOnlyList<ColorStop> stops,
                       GradientSpread spread = GradientSpread.Pad)
    RadialGradientFill(PointF center, float radius, Color centerColor, Color edgeColor)
    GradientSpread: Pad (default), Repeat, Reflect
    // GradientFill (base): Spread, StopCount, ColorStop[] GetStops(); ShapeFill is
    //   the abstract base every fill kind (and every future one) derives from.
Fill geometry is in CALIBRATED drawing units in the same space as the shape
(not relative to the shape's bounds), so a gradient stays put while shapes
move. Fills are immutable and can be shared by many shapes. A fill is an
optional extra on filled shapes, through dedicated constructor overloads:
    new CircleShape(cx, cy, radius, ShapeFill fill)
    new EllipseShape(cx, cy, rx, ry, ShapeFill fill)
    new RectangleShape(x, y, width, height, ShapeFill fill, float cornerRadius = 0)
    new PolylineShape(IReadOnlyList<PointF> points, ShapeFill fill)   // closed, filled
    new TextShape(text, x, y, size, fonts, ShapeFill fill, ...)       // see Text
    // e.g. a glow:
    session.DrawShape(new CircleShape(500, 400, 120, new RadialGradientFill(
        new PointF(500, 400), 120, Color.FromRgba(255, 220, 160, 160), Color.FromRgba(255, 220, 160, 0))));
DrawingShape also has: ShapeFill Fill { get; } (null unless a fill was given),
a protected constructor DrawingShape(float strokeThickness, Color? color,
ShapeFill fill), and protected static SKPaint CreateFillPaint(SKColor color,
ShapeFill fill) for custom shapes (with a fill, the paint is opaque and the
stops' own alpha is what paints). Outlines always draw in the flat color.

Text (Shapes.TextShape, Text.FontRegistry, Text.TextAlign)
----------------------------------------------------------
FONTS: text uses only fonts YOU register - system fonts are never consulted
and there is no fallback to them.
    FontRegistry():  string RegisterFont(string path | byte[] data | Stream stream)
                     string RegisterFont(..., string familyNameOverride)
                     IReadOnlyList<string> GetRegisteredFamilyNames(); int Count
                     bool Contains(string familyName)      // null = "the default font"
                     float MeasureText(string text, float size, float tracking = 0f, string familyName = null)
    DrawingSession.Fonts                // every session owns one (empty at first) ...
    DrawingSessionOptions.Fonts         // ... or shares one you supply
A null family means the FIRST registered font; a named family must be
registered (by its own name or the override), matched case-insensitively -
an unknown name throws ArgumentException, and drawing text with no font
registered throws InvalidOperationException. One font file is one face.

    TextShape DrawText(string text, float x, float y, float size, Color? color = null,
        TextAlign align = TextAlign.Left, float tracking = 0f, string familyName = null)   // session
    TextShape(string text, float x, float y, float size, FontRegistry fonts, Color? color = null,
        TextAlign align = TextAlign.Left, float tracking = 0f, string familyName = null)
    TextShape(string text, float x, float y, float size, FontRegistry fonts, ShapeFill fill,
        TextAlign align = TextAlign.Left, float tracking = 0f, string familyName = null)
    // Text, X, Y, Size, Align, Tracking, FamilyName (resolved), Width, Left, GetBounds()
    TextAlign: Left, Center, Right   // about x, over the line's advance width (Justify is for text
                                     //   blocks; TextShape and DrawText throw ArgumentOutOfRangeException for it)
    // kerning: false overloads of the constructors, DrawText and MeasureText - see TEXT BLOCKS below
(x, y) is the anchor ON THE BASELINE; size is the em size in calibrated units
(as a font size in a 2D canvas). tracking is extra space after every
character but the last, as a FRACTION OF THE SIZE (0.1 = a tenth of an em).
Width is the measured advance width (tracking included), known as soon as
the shape exists - use it (or FontRegistry.MeasureText beforehand) for
layout. The text is laid out ONCE, at construction, and drawn as filled
glyph outlines built by CodeBrix.Imaging's font support, so both packages
draw the same outlines. Outlines are unhinted and advances are the font's
exact (unrounded) advances, KERNED when the font carries kerning data (also
when tracked: the line is shaped as one run and the tracking goes between its
glyphs). A TextShape is one line; for several lines, wrapping, alignment in a
box, outlines or a reveal, see TEXT BLOCKS, REVEALS AND CAPTIONS.

Shape bounds
------------
    public virtual RectangleF? GetBounds()   // DrawingShape; default null = "unknown"
    public RectangleF? GetBounds()           // Stroke; null while empty
Every built-in shape reports an axis-aligned box in calibrated units,
outline thickness included (half the thickness each side; filled shapes
without). Curves may over-cover slightly (control points), never under.
Custom shapes that do not override GetBounds keep compiling and drawing;
they are simply "unknown" to a safe-area check.

Whole-frame and per-layer fade
------------------------------
    float DrawingSession.Opacity { get; set; }    // 0..1, default 1
    float DrawingRenderer.Opacity { get; set; }   // the same, on the renderer
    float DrawingLayer.Opacity { get; set; }      // 0..1, default 1
session.Opacity fades EVERY LAYER AS ONE GROUP over the background fill and
image (which are not faded): over an opaque BackgroundFillColor that is a
fade from/to that color and exported frames stay opaque; with
includeBackground: false the fade lands in the alpha bytes. Layers that
cover each other do not show through one another mid-fade. A layer's own
Opacity multiplies its compositing alpha (LayerOpacity keeps its meaning:
effective layer alpha = LayerOpacity x layer.Opacity, inside the group fade).
Out-of-range or NaN values throw ArgumentOutOfRangeException. Setting
session.Opacity raises RedrawRequested; changing a layer's Opacity does not
(like AddShape on a layer) - the next Render picks it up.

Safe areas (SafeAreas)
----------------------
A SafeAreaGuide is DATA: Name, Source (where the numbers came from),
SafeAreaMargins Margins (four edge bands as FRACTIONS of the frame height
(top, bottom) and width (left, right)), IReadOnlyList<SafeAreaCrop> Crops
(centered crops of an aspect ratio - everything outside is unsafe),
SafeAreaTimeRule TimeRule (null = always), and Parts (for combinations).
    new SafeAreaGuide(string name, SafeAreaMargins margins, string source = null,
        IReadOnlyList<SafeAreaCrop> crops = null, SafeAreaTimeRule timeRule = null)
    SafeAreaGuide.Combine(string name, params SafeAreaGuide[] guides)   // union of unsafe areas
    IReadOnlyList<SafeAreaZone> GetUnsafeZones(SizeF frameSize, double? seconds = null, double? duration = null)
    SafeAreaMargins(top, bottom, left, right); Uniform(f); Max(a, b); None; WholeFrame
    SafeAreaCrop(string name, float aspectWidth, float aspectHeight); GetKeptRectangle(SizeF)
    SafeAreaTimeRule.FinalSeconds(double seconds); SafeAreaTimeRule.Between(double startTime, double endTime)
    //   StartTime, EndTime (the Between window; 0 for a final-seconds rule), Duration (the window's
    //   length: the final N seconds, or EndTime - StartTime), IsFinalSeconds, AppliesAt(seconds, duration)
    SafeAreaZone: GuideName, ZoneName ("top margin", "bottom margin", "left margin",
                  "right margin", "whole frame", or the crop's name), Bounds (frame pixels)
BUILT-IN PRESETS (SafeAreaGuides) - ALL ADVISORY; each preset's Source says
whether it is FACT (a published standard), REPORTED (third parties quoting a
platform) or MEASURED (third parties measuring the app) and where it came
from. Platforms change their interfaces without notice: check against the
platform's own template before relying on a preset.
    InstagramReelsAd, InstagramStoriesAd, FacebookReelsAd, FacebookStoriesAd
        REPORTED (Meta Ads Guide): top 14%, bottom 35%, sides 6%
    YouTubeShortsAd   REPORTED (Google ad template): 288/672/48/192 px of 1080x1920
    TikTok            MEASURED (lower confidence): about 288/461/86/216 px
    OrdinaryPortraitPost  MEASURED ranges, larger end used: 200/370/60/164 px
    InstagramProfileGrid  REPORTED: centered 3:4 crop
    InstagramFeedPreview  REPORTED: centered 4:5 crop
    BroadcastTitleSafe    FACT (SMPTE ST 2046-1): 90% (5% margins)
    BroadcastActionSafe   FACT (SMPTE ST 2046-1): 93% (3.5% margins)
    YouTubeEndScreen      REPORTED: whole frame, final 20 seconds
CHECK (opt-in, never throws because of what it finds):
    element.IsSafeAreaChecked = true;   // DrawingElement: mark one element
    layer.IsSafeAreaChecked = true;     // DrawingLayer: mark every element on it
    IReadOnlyList<SafeAreaFinding> session.CheckSafeArea(SafeAreaGuide guide, SafeAreaCheckOptions options = null)
    IReadOnlyList<SafeAreaFinding> session.CheckSafeArea(SafeAreaGuide guide, Size frameSize, SafeAreaCheckOptions options = null)
    SafeAreaCheckOptions: double? Duration (the video's length, for final-seconds rules),
        bool ReportUncheckedElements (default false), bool CheckAllElements (default false)
    SafeAreaFinding: Kind (Overlap | CouldNotCheck), Layer, Element, ElementIndex,
        Zone, GuideName, ZoneName, RectangleF? Overlap (calibrated units),
        RectangleF? FrameOverlap (frame pixels)
The frame is the whole exported image (frameSize, default DefaultExportSize);
the drawing sits in its centered aspect-fit rectangle exactly as in an
export. One finding per element per zone it reaches into. Elements are
measured as the frame shows them - through their own Transform, their
layer's Transform and the camera (with the layer's parallax) - and only
while they exist at the session's Time (a group by the children that exist
then). Elements with unknown bounds are skipped SILENTLY by default; ReportUncheckedElements
lists them as CouldNotCheck.
"NOW" IS THE SESSION'S TIME: the check looks at the session as it stands at
session.Time - that one moment decides which time-keyed elements exist,
where animated elements are AND which of the guide's time rules apply (there
is no separate time option). To check another moment, set session.Time
first. A FinalSeconds rule also applies when Duration is unknown.
AN UNSET TIME, EVERYWHERE: wherever a session or renderer supplies the clock
(CheckSafeArea, SafeAreaOverlay.Time, DebugOverlay.Time), "not set" means
the session's / renderer's current Time. The guide's own methods
(GetUnsafeZones, GetSafeRectangle, AppliesAt, and new DrawingLayout(...,
seconds)) have no clock: given no time they treat the moment as unknown and
count every time rule as applying.
OVERLAY (opt-in, OFF by default everywhere):
    new SafeAreaOverlay(SafeAreaGuide guide) { Opacity = ..., Time = ..., Duration = ... }
    //   Time        double? - the moment its time rules are evaluated at; null (default) =
    //               the moment the frame shows (the renderer's / session's Time)
    //   Duration    double? - the video's length, for final-seconds rules; null = unknown
    //   MaskColor   the tint; its alpha is the tint's strength (default DefaultMaskColor,
    //               rgba(255, 64, 112, 40) - a light rose wash, about a sixth opaque)
    //   Opacity     0..1, default 1: scales the tint - drawn at round(MaskColor.A * Opacity)
    //               (halves away from zero); 0 = no tint. The outline is not affected.
    //   OutlineColor   the line along the safe area's edge (default DefaultOutlineColor,
    //               rgba(255, 64, 112, 230)); transparent = no line
    //   OutlineWidth   frame pixels (default DefaultOutlineWidth = 2); 0 = no line
    //   GetZones(SizeF frameSize, double seconds) lists the zones it tints at that moment
    session.SafeAreaOverlay = overlay;    // shows in the live Render ONLY (null = off)
    session.ExportPng(size, includeBackground: true, overlay);   // a still WITH the overlay
    session.ExportImage(size, true, overlay); session.ExportPixels(buffer, size, true, overlay);
    // DrawingRenderer: SafeAreaOverlay property (live Render only) and
    //   RenderToImage(Size, layers, includeBackground, overlay)
An export NEVER includes the overlay unless one is passed explicitly -
setting session.SafeAreaOverlay does not affect any export. Zones are laid
out on the whole output frame (the canvas, or the export size) and tinted as
one union, so overlapping zones are no darker than one zone. The tint is
deliberately light, so the picture under it stays easy to read on dark and
light frames; the outline - solid, not anti-aliased, drawn just OUTSIDE the
safe rectangle (on the unsafe side, so it never covers safe content) - is
what marks the edge unmistakably. A guide whose zone is the whole frame
(nothing safe) is tinted with no outline. For a stronger or weaker mark set
Opacity (or MaskColor's alpha); for a heavier edge set OutlineWidth.

PIXEL PLANES, FILTERS, TRANSITIONS, NOISE, PARTICLES
====================================================
(namespace CodeBrix.Imaging.Drawing.Planes; the session bridge and
ParticlesShape are in the root and Shapes namespaces)

Tools for GENERATING animation frames in float colour: paint a scene per
pixel, add lights past 1.0, blur, bloom, vignette and tone-map it, convert to
8-bit RGBA, and dissolve, wipe, slide or fade between frames. Everything in
this namespace is plain managed arithmetic on arrays with a documented order
of operations - no rasterizer is involved - so BOTH packages produce
BIT-IDENTICAL results (the parity test suite asserts byte equality), and
results never depend on the thread count. The planes types draw no shapes or
text themselves; the session bridges the two worlds (see "From a drawing
session to planes" below).

What "bit-identical" promises: the two packages, on the same machine and
runtime, always agree to the last bit. The same holds across machines and
operating systems for everything built from plain arithmetic (filling,
mixing, compositing, resampling, downsampling, vignettes, tone curves,
transitions, the integer random sequence, noise, motion blur). The few steps
that call the runtime's math library - the blur kernel's Math.Exp, ApplyGamma's
MathF.Pow, DrawingRandom.Normal's Math.Log/Math.Cos, the particle system's
Math.Cos/Math.Sin - can differ in the last bit between operating systems or
processors, so a pixel near a rounding boundary may then differ by one level.

The library PRODUCES frames; it does not assemble them into video or
animated images.

Conventions that hold for every type below:
  - Straight (unpremultiplied) alpha everywhere: R, G, B hold the colour as it
    would look fully opaque; A holds coverage 0..1. 8-bit RGBA in and out is
    straight alpha too.
  - Same-size operations change the object IN PLACE and return it (calls
    chain); size-changing ones (Resized, Downsampled, CreateBloom) return a
    NEW object. Clone() first to keep an original.
  - Float -> byte rounding rule (used by every 8-bit output): NaN and values
    <= 0 give 0, values >= 1 give 255, otherwise (byte)(value * 255 + 0.5).
    Byte -> float is byte / 255f. Every byte value, and therefore every frame
    including fully transparent ones, round-trips exactly.
  - A CodeBrix.Imaging Color is taken through its 8-bit form: each channel is
    byte / 255f.
  - Standard .NET exceptions only (ArgumentNullException, ArgumentException
    for size mismatches and short buffers, ArgumentOutOfRangeException,
    InvalidOperationException).

DrawingPlane (sealed) - one plane of open-ended float values
    DrawingPlane(int width, int height)                 // zeros
    DrawingPlane(int width, int height, float value)
    DrawingPlane(Size size)
    DrawingPlane(int width, int height, float[] data)   // WRAPS data (no copy); length = w * h
    int Width, Height, Length;  Size Size
    float[] Data                                        // live, row-major: [y * Width + x]
    float this[int x, int y]
    Span<float> AsSpan();  Span<float> GetRow(int y)
    DrawingPlane Clone();  void CopyTo(DrawingPlane destination)
  Filling:
    Clear(); Fill(float value); Fill(Rectangle area, float value)
    Fill(Func<int, int, float> f); Fill(Rectangle area, Func<int, int, float> f)   // f(x, y), absolute coords
    FillParallel(Func<int, int, float> f, int maxDegreeOfParallelism = 0)         // rows across threads
    FillParallel(Rectangle area, Func<int, int, float> f, int maxDegreeOfParallelism = 0)
    // Rectangles are clipped to the plane. FillParallel gives exactly the result of Fill as long
    // as f is pure and thread-safe (each pixel is computed once, by one thread).
  Arithmetic (in place):
    Add(float v); Add(DrawingPlane other, float scale = 1)      // this += other * scale
    Subtract(DrawingPlane other); Multiply(float f); Multiply(DrawingPlane other)
    Lerp(DrawingPlane other, float t)        // this * (1 - t) + other * t  (1 - t computed once)
    Lerp(DrawingPlane other, DrawingPlane mask)
    Min(float) / Min(DrawingPlane); Max(float) / Max(DrawingPlane)
    Clamp(float min, float max)              // NaN becomes min
    Apply(Func<float, float> f)              // any per-value curve
  Tone, gamma, vignette (in place):
    ApplyToneCurve(float k, float gain)      // v / (1 + k * v) * gain, in float, in that order
    ApplyGamma(float exponent)               // MathF.Pow(v, exponent) for v > 0; others unchanged
    ApplyVignette(double centerX, double centerY, double radiusX, double radiusY,
                  double strength, double minimum = 0)
    static DrawingPlane CreateVignette(int width, int height, double centerX, double centerY,
                  double radiusX, double radiusY, double strength, double minimum = 0)
    // factor = clamp(1 - strength * (dx*dx + dy*dy), minimum, 1), dx = (x - centerX) / radiusX,
    // dy = (y - centerY) / radiusY, computed in double with x, y the integer column and row.
  Filters:
    GaussianBlur(float sigma)                // in place
    DrawingPlane Resized(int width, int height,
        DrawingPlaneResampling resampling = DrawingPlaneResampling.Bilinear)   // NEW plane
    DrawingPlane Downsampled(int factor)                                       // NEW plane
    DrawingPlane CreateBloom(int downsampleFactor, IReadOnlyList<(float Sigma, float Weight)> blurs,
        float gain = 1f, Size? outputSize = null,
        DrawingPlaneResampling upsampling = DrawingPlaneResampling.Bilinear)   // NEW plane (glow only)
  8-bit:
    void ToBytes(Span<byte> destination); byte[] ToBytes()          // one byte per pixel
    static DrawingPlane FromBytes(ReadOnlySpan<byte> bytes, int width, int height)
    void CopyFromBytes(ReadOnlySpan<byte> bytes)                    // overwrite in place (no allocation)

Exact filter arithmetic:
  GaussianBlur  radius = (int)(4 * sigma + 0.5). The kernel is built
                entirely in double: each weight exp(-0.5 * i * i / (sigma *
                sigma)) (sigma widened from float), their sum (added from
                -radius to +radius), and each weight divided by that sum;
                only the normalised weight is rounded, once, to float.
                Separable: a horizontal pass, then a vertical pass; each
                output sums weight * sample from -radius to +radius starting
                at 0, in float. EDGES ARE MIRRORED including the edge pixel
                (index -1 reads 0, -2 reads 1, width reads width - 1;
                repeated for kernels wider than the plane), so a flat plane
                stays flat up to its borders. sigma <= 0 does nothing.
  Resampling    DrawingPlaneResampling.Bilinear (the default and the
                enum's zero): pixel-centre grid, source position
                (i + 0.5) * src / dst - 0.5, clamped.
                .Nearest: output i reads source
                floor((2i + 1) * src / (2 * dst)) (integer arithmetic; values
                copied, never blended).
                .BilinearAlignedCorners: position i * (src - 1) / (dst - 1) -
                first and last pixels land exactly on the source's first and
                last (the "stretch a small glow plane" grid).
                Positions in double; left index = floor (capped at src - 2),
                fraction stored as float; value = p00*w00 + p10*w10 +
                p01*w01 + p11*w11 summed in that order, with
                w00 = (1-fx)(1-fy), w10 = fx(1-fy), w01 = (1-fx)fy,
                w11 = fx*fy. Bilinear does not average away detail when
                shrinking a lot - Downsampled first.
  Downsampled   size = ceiling(size / factor); each pixel is the mean of its
                factor x factor block (partial blocks at the right/bottom
                average only the pixels they have). Block rows summed left to
                right, row sums added top to bottom, divided by the count as
                float.
  CreateBloom   Downsampled(factor); for each (Sigma, Weight) in order: blur a
                copy of the small plane, sum = sum + blurred * Weight (sum
                starts at 0); multiply by gain; Resized to outputSize
                (default: this plane's size). Sigmas are in SMALL-plane
                pixels. Add the result to the scene to make its lights glow.

DrawingPlaneImage (sealed) - a float RGBA image made of four DrawingPlane values
    DrawingPlaneImage(int width, int height)          // transparent black
    DrawingPlaneImage(Size size)
    DrawingPlaneImage(DrawingPlane red, DrawingPlane green, DrawingPlane blue, DrawingPlane alpha)  // WRAPS
    DrawingPlane R, G, B, A;  int Width, Height;  Size Size
    Vector4 GetPixel(int x, int y); void SetPixel(int x, int y, Vector4 value)   // X=R Y=G Z=B W=A
    Clone(); CopyTo(DrawingPlaneImage destination)
  Filling: Clear(); Fill(Color); Fill(float r, float g, float b, float a = 1); Fill(Rectangle, Color);
    Fill(Func<int, int, Vector4>); Fill(Rectangle, Func<int, int, Vector4>);
    FillParallel(Func<int, int, Vector4>, int maxDegreeOfParallelism = 0); FillParallel(Rectangle, ...)
  Colour arithmetic - R, G, B ONLY; alpha untouched (work on .A directly to change alpha):
    Add(DrawingPlaneImage other, float scale = 1); Add(float r, float g, float b)
    Multiply(float); Multiply(float r, float g, float b); Multiply(DrawingPlane factor);
    Multiply(DrawingPlaneImage other); Min(float); Max(float); Clamp(float min, float max)
    Apply(Func<float, float>); Apply(Func<float, float> red, green, blue)   // per-channel curves
    ApplyToneCurve(float k, float gain); ApplyGamma(float exponent);
    ApplyVignette(double cx, double cy, double rx, double ry, double strength, double minimum = 0)
  Mixing and compositing (Porter-Duff, alpha clamped to 0..1, straight colour in and out):
    Lerp(DrawingPlaneImage other, float t); Lerp(DrawingPlaneImage other, DrawingPlane mask)
        // premultiplied mix: equal alphas -> C = Ca*(1-t) + Cb*t, alpha kept; otherwise
        // A = Aa*(1-t) + Ab*t, C = (Ca*Aa*(1-t) + Cb*Ab*t) / A (0 when A is 0).
        // t <= 0 keeps this image exactly, t >= 1 copies other exactly.
    CompositeOver(DrawingPlaneImage source, float opacity = 1)       // source-over
        // as = clamp(source.A * opacity), ad = clamp(A); Ao = as + ad*(1-as);
        // C = (Cs*as + Cd*ad*(1-as)) / Ao. Over an opaque pixel: Cs*as + Cd*(1-as), Ao exactly 1.
    CompositeAdd(DrawingPlaneImage source, float opacity = 1)        // plus ("lighter")
        // Ao = min(1, as + ad); C = (Cs*as + Cd*ad) / Ao. Over an opaque pixel: Cd + Cs*as.
    BlendColor(Color color, DrawingPlane mask); BlendColor(float r, g, b, a, DrawingPlane mask)
        // source-over of the colour with source alpha color.A * mask - "paint through a mask"
    BlendImage(DrawingPlaneImage source, DrawingPlane mask)          // source alpha source.A * mask
  Filters (neighbouring pixels are combined in PREMULTIPLIED space, so
  transparent pixels never bleed a dark fringe; when every alpha value is
  equal - e.g. an opaque frame - the colour planes are filtered directly and A
  is kept, which is the same result without rounding drift):
    GaussianBlur(float sigma)                                               // in place
    DrawingPlaneImage Resized(int width, int height, DrawingPlaneResampling resampling = Bilinear)
    DrawingPlaneImage Downsampled(int factor)
    DrawingPlaneImage CreateBloom(int downsampleFactor, IReadOnlyList<(float Sigma, float Weight)> blurs,
        float gain = 1f, Size? outputSize = null, DrawingPlaneResampling upsampling = Bilinear)
        // opaque input: a glow of the colour planes, alpha kept -> image.Add(glow)
        // transparent input: a straight-alpha glow that spreads its own coverage ->
        //   image.CompositeAdd(glow) (works over transparency too)
  8-bit and CodeBrix.Imaging:
    void ToRgba(Span<byte> destination); byte[] ToRgba()       // tightly packed RGBA, straight alpha
    void CopyFromRgba(ReadOnlySpan<byte> rgba)                  // overwrite in place (no allocation)
    static DrawingPlaneImage FromRgba(ReadOnlySpan<byte> rgba, int width, int height)
    Image<Rgba32> ToImagingImage()                              // caller disposes
    static DrawingPlaneImage FromImagingImage(Image<Rgba32> image)

From a drawing session to planes (DrawingSession, DrawingRenderer)
    DrawingPlaneImage ExportPlaneImage(Size outputSize, bool includeBackground = true)
    DrawingPlaneImage ExportPlaneImage(bool includeBackground = true)          // DefaultExportSize
    void ExportPlaneImage(DrawingPlaneImage destination, bool includeBackground = true)
                                                       // at the destination's size; reuse per frame
    DrawingPlane ExportMask(Size outputSize)           // coverage of everything drawn
    DrawingPlane ExportMask()                          // DefaultExportSize
    DrawingPlane ExportMask(DrawingLayer layer, Size outputSize)               // one layer
    DrawingPlane ExportMask(DrawingLayer layer)
    DrawingPlane ExportMask(Size outputSize, MaskMeaning meaning)   // Coverage | VisibleAlpha | HardEdged
    DrawingPlane ExportMask(MaskMeaning meaning)
    void ExportMask(DrawingPlane destination, MaskMeaning meaning = Coverage)   // reuse per frame
    DrawingPlane ExportMask(DrawingLayer layer, Size outputSize, MaskMeaning meaning)
    DrawingPlane ExportMask(DrawingLayer layer, MaskMeaning meaning)
    void ExportMask(DrawingLayer layer, DrawingPlane destination, MaskMeaning meaning = Coverage)
    // DrawingRenderer: RenderToPlaneImage(Size, layers, includeBackground = true),
    //   RenderToPlaneImage(DrawingPlaneImage destination, layers, includeBackground = true),
    //   RenderToMask(Size, layers), RenderToMask(Size, layers, MaskMeaning),
    //   RenderToMask(DrawingPlane destination, layers, MaskMeaning meaning = Coverage)
  ExportPlaneImage is exactly ExportPixels read into float: straight alpha,
  each channel byte / 255f, so ToRgba() on the result gives back the very
  bytes ExportPixels writes. includeBackground: false renders over
  transparency and the A plane carries it.
  ExportMask is the COVERAGE of the ink as one plane of 0..1 (1 = fully
  covered, 0 = untouched) - shape masks as data, to Multiply by, BlendColor
  through or Lerp with. Each layer contributes the alpha of its own ink as it
  is drawn into the layer's private bitmap: anti-aliased edges, an element
  colour's own alpha and a gradient's stop alpha count; LayerOpacity,
  DrawingLayer.Opacity and session Opacity do NOT, and the background is
  never part of it. One layer's mask is exactly its alpha byte / 255; layers
  combine as a union, bottom to top, m = m + a * (1 - m) (a fully covered
  pixel is exactly 1). The drawing sits in its centered aspect-fit rectangle,
  as in every export. TWO MEANINGS (MaskMeaning, root namespace):
  Coverage - the default, and all the overloads without a MaskMeaning - is
  the above: where the ink is (element and group opacity, masks and effects
  count, because they are part of what a layer draws; the layer and session
  opacities do not, so a layer faded to 0 still shows fully). VisibleAlpha is
  the mask as the viewer sees it: the alpha of the frame exactly as
  ExportPixels writes it without the background - every opacity (element,
  group, layer, LayerOpacity, session), time windows, masks, clips and
  effects count - each value that alpha byte / 255; for one layer, that layer
  alone exported over transparency. The DrawingPlane overloads render at the
  plane's own size and overwrite every value (no allocation per frame). Both exports are rasterized by the package's canvas, so
  their edge pixels carry the usual small difference between the two
  packages; everything you then do to them with planes is bit-identical.
  A THIRD meaning, MaskMeaning.HardEdged, is not rasterized at all: 0 or 1
  per pixel by the pixel-centre rule, computed from the elements' geometry -
  identical in both packages (see PAINTERLY TOOLS).

DrawingTransition (sealed, immutable, thread-safe) - frame-to-frame transitions
    static DrawingTransition Dissolve()
    static DrawingTransition Wipe(DrawingTransitionDirection direction, float softness = 0)
    static DrawingTransition Slide(DrawingTransitionDirection direction)   // incoming covers outgoing
    static DrawingTransition Push(DrawingTransitionDirection direction)    // incoming pushes outgoing out
    static DrawingTransition FadeThroughColor(Color color)
    DrawingTransitionKind Kind; DrawingTransitionDirection Direction; float Softness; Color Color
    void Apply(ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, Span<byte> destination,
               int width, int height, float fraction)          // 8-bit straight-alpha RGBA frames
    DrawingPlaneImage Apply(DrawingPlaneImage from, DrawingPlaneImage to, float fraction)
    void Apply(DrawingPlaneImage from, DrawingPlaneImage to, DrawingPlaneImage destination, float fraction)
  enum DrawingTransitionKind      { Dissolve, Wipe, Slide, Push, FadeThroughColor }
  enum DrawingTransitionDirection { Left, Right, Up, Down }   // the way the edge / frame TRAVELS:
                                                              //   Right = incoming enters at the left
  Rules: fraction 0 gives the outgoing frame and 1 the incoming frame
  EXACTLY; outside 0..1 is clamped; NaN throws. Pixels mix with the
  premultiplied rule of DrawingPlaneImage.Lerp; for opaque 8-bit frames that
  is (byte)(a * (1 - u) + b * u + 0.5), otherwise colour is premultiplied-
  mixed, divided by the mixed alpha and rounded half up. Wipe weight for a
  pixel centre c pixels from the start side: hard edge 1 where
  c < fraction * extent; soft edge of width s
  clamp((fraction * (extent + s) - c) / s, 0, 1). Slides and pushes move
  WHOLE pixels: offset = floor(fraction * extent + 0.5), every output pixel
  an exact copy of one input pixel. FadeThroughColor mixes outgoing -> colour
  by 2f below 0.5 and colour -> incoming by 2f - 1 from 0.5 (at exactly 0.5
  the frame is the colour; Color.Transparent fades through transparency).
  The destination may be one of the inputs for Dissolve, Wipe and
  FadeThroughColor, never for Slide or Push (ArgumentException).

DrawingRandom (sealed) - seeded random numbers with a FIXED, documented sequence
    DrawingRandom(long seed); long Seed
    static DrawingRandom ForKey(long seed, long key)   // independent stream per item (particle, star)
    ulong NextUInt64(); double NextDouble(); float NextSingle()
    double Uniform(double low, double high)
    double Normal(double mean = 0, double deviation = 1)
    int NextInt(int maxExclusive); int NextInt(int minInclusive, int maxExclusive)
  Algorithm: SplitMix64 seeding + xoshiro256** (Blackman and Vigna);
  NextDouble = (next >> 11) * 2^-53; NextInt = Lemire's multiply-shift with
  rejection (exactly uniform); Normal = Box-Muller (cosine branch, two
  draws). NOT System.Random, so a seed's sequence never changes between .NET
  versions. The integer sequence is bit-exact everywhere; Normal also uses
  Math.Log/Math.Cos. One instance per thread.

DrawingNoise (static) - seeded coherent noise, values in -1..1
    double Value(double x, double y, long seed);  double Value(double x, double y, double z, long seed)
    double Gradient(double x, double y, long seed); double Gradient(double x, double y, double z, long seed)
    double Sample(DrawingNoiseKind kind, double x, double y, long seed)      // (+ z overload)
    double Fractal(DrawingNoiseKind kind, double x, double y, long seed,
                   int octaves = 4, double lacunarity = 2.0, double gain = 0.5)   // (+ z overload)
    DrawingPlane Fill(DrawingPlane plane, DrawingNoiseKind kind, long seed, double frequency,
                      double? z = null, int octaves = 1, double lacunarity = 2.0, double gain = 0.5,
                      double offsetX = 0, double offsetY = 0)
  enum DrawingNoiseKind { Value, Gradient }   // gradient = Perlin-style, exactly 0 at whole coordinates
  One lattice cell per unit of coordinate. Fill samples pixel CENTRES:
  ((x + 0.5 + offsetX) * frequency, (y + 0.5 + offsetY) * frequency) and,
  when z is given, 3D noise at that z (pass time times a speed for noise that
  evolves smoothly frame to frame). Fractal octave o uses frequency
  lacunarity^o, amplitude gain^o and seed + o, normalised by the amplitude
  sum. Map to 0..1 with plane.Multiply(0.5f).Add(0.5f).

Particles as DATA, and drawn
    DrawingParticleSystem(long seed); long Seed
    IReadOnlyList<DrawingParticleEmitter> Emitters
    DrawingParticleEmitter AddEmitter(DrawingParticleEmitter emitter)      // keeps the instance
    IReadOnlyList<DrawingParticle> GetParticles(double seconds)
    void GetParticles(double seconds, List<DrawingParticle> results)       // reuse one list per frame
  DrawingParticleEmitter (settable properties; pixels, seconds (double), DEGREES, y DOWN):
    X, Y, SpawnRadius; Rate (per second), Burst (count at StartTime); StartTime, EndTime (exclusive,
    default +infinity); double Lifetime, LifetimeVariation; float Direction (degrees clockwise on
    screen: 0 = right, 90 = down, -90 = up), Spread (full cone width, degrees; 360 = all round); Speed, SpeedVariation; GravityX, GravityY (px/s^2, +Y pulls down);
    DriftX, DriftY (constant px/s); StartSize, EndSize, SizeVariation (fraction);
    StartOpacity, EndOpacity; StartColor, EndColor; Clone()
  DrawingParticle (readonly struct): EmitterIndex, Index (burst 0..Burst-1, then birth order),
    BirthTime, Age, Lifetime, LifeFraction, X, Y, Size (diameter), Opacity, Color
  GetParticles(t) is a PURE FUNCTION of t and the seed - no state is stepped
  - so frames can be rendered in any order, skipped, repeated or in parallel
  and always agree. Continuous particle n is born at StartTime + n / Rate; a
  particle lives while birth <= t < birth + lifetime; its centre is emitter +
  spawn offset + velocity * age + gravity * age^2 / 2; size, opacity and
  colour (8-bit channels, rounded half up) blend linearly by age / lifetime.
  Each particle's random choices come from its own DrawingRandom.ForKey
  stream, so adding an emitter or querying another time never changes any
  other particle. Results are ordered by emitter, then birth number.
  Drawing them in a session (namespace CodeBrix.Imaging.Drawing.Shapes):
    ParticlesShape DrawParticles(DrawingParticleSystem system, double time, float softness = 1f)  // session
    ParticlesShape(DrawingParticleSystem system, double time, float softness = 1f)
    // ParticleSystem, Time, Softness, IReadOnlyList<DrawingParticle> Particles, GetBounds()
  Each live particle becomes a round dot of diameter Size centred on (X, Y),
  in its own Color with that colour's alpha multiplied by Opacity, drawn in
  the system's order (later particles over earlier ones). Positions and sizes
  are CALIBRATED drawing units - pixels when CalibrationSize equals the frame
  size. softness is the fraction of the radius, from the edge inwards, over
  which a dot fades linearly to transparent (1 = from the centre, 0 = a plain
  anti-aliased disc). The layer colour is not used. The particles are taken
  ONCE, when the shape is created - like every shape it is a fixed mark - so
  draw a new one for each frame (clear the layer first). The dots are drawn
  through the canvas: the particle DATA is bit-identical in both packages,
  the drawn dots match within the usual edge tolerance.

Motion blur as DATA
    DrawingFrameAccumulator(int width, int height)
    void Add(ReadOnlySpan<byte> rgba); void Add(DrawingPlaneImage image)   // one input kind per run
    int Count; void Reset()
    void GetAverage(Span<byte> destination); byte[] GetAverage(); DrawingPlaneImage GetAverageImage()
    static class DrawingMotionBlur:
      double[] GetSampleTimes(double frameTime, double frameDuration, int samples, double shutter = 0.5)
      DrawingPlaneImage Average(IReadOnlyList<DrawingPlaneImage> frames)
      void Average(IReadOnlyList<byte[]> frames, int width, int height, Span<byte> destination)
      byte[] Average(IReadOnlyList<byte[]> frames, int width, int height)
  Frames are averaged in PREMULTIPLIED space: alpha is the mean alpha and
  colour comes only from the sub-frames where the pixel is covered, so a
  moving object over transparency blurs into a correctly fading streak. 8-bit
  frames use exact integer sums: alpha = round(sum A / N), colour =
  round(sum(C * A) / sum A), half up (0 where sum A is 0). Float images sum
  in double. Sample i of GetSampleTimes is at
  frameTime + (i + 0.5) / samples * shutter * frameDuration.

ANIMATION MODEL: TIMELINES, TRACKS, TRANSFORMS, GROUPS, TIME WINDOWS, CAMERA
============================================================================
(namespaces CodeBrix.Imaging.Drawing.Animation, .Models, .Shapes, .Layout,
.Rendering and the root namespace)

Everything here is OPT-IN. A drawing that uses none of it renders exactly as
a plain drawing does - the same pixels, through the same caches, at the same
cost. The model adds elements whose appearance changes over time, so ONE
scene can be built once and then shown at any moment:

    session.Time = t;   // applies every binding, rebuilds animated shapes,
                        // and decides which time-keyed elements exist
    session.ExportPixels(frame, size);   // or session.Render(...) live

Values COMPUTED in managed code (track values, timeline frames and cues,
transform and camera matrices, bounds, layout positions, safe rectangles,
playback state, debug read-out lines) are identical in both packages; what is
drawn through each package's canvas matches within the usual edge tolerance.

A complete scene:

    using CodeBrix.Imaging;
    using CodeBrix.Imaging.Drawing;
    using CodeBrix.Imaging.Drawing.Animation;
    using CodeBrix.Imaging.Drawing.Models;
    using CodeBrix.Imaging.Drawing.Shapes;

    var timeline = new AnimationTimeline(8, 30);               // 8 s at 30 fps = 240 frames
    timeline.AddCue("fade in", 0, 1, EasingKind.SmoothStep);
    var session = new DrawingSession(new DrawingSessionOptions
    {
        CalibrationSize = new Size(1920, 1080), LayerOpacity = 255, BackgroundFillColor = Color.Black,
    });
    session.AddLayer("Scene", Color.White);
    DrawingShape ball = session.DrawCircle(0, 0, 40, filled: true, color: Color.Orange);
    session.Animations.Bind(ball, AnimatedProperty.Position, new PointTrack
    {
        { 0, new PointF(200, 540) },
        { 4, new PointF(1700, 540), EasingKind.CubicInOut },   // easing of the segment ARRIVING here
        { 8, new PointF(200, 540), EasingKind.BounceOut },
    });
    for (int i = 0; i < 100; i++)                              // marks that appear one by one
    {
        session.DrawLine(i * 19, 900, i * 19, 1000, thickness: 4).StartTime = i * 0.08;
    }
    session.Animations.Bind(t => session.Opacity = (float)timeline.Progress("fade in", t));

    var frame = new byte[1920 * 1080 * 4];
    for (int i = 0; i < timeline.FrameCount; i++)
    {
        session.Time = timeline.TimeOfFrame(i);
        session.ExportPixels(frame, new Size(1920, 1080));
        encoder.Write(frame);
    }

Timeline and cues (Animation)
-----------------------------
    AnimationTimeline(double duration, double framesPerSecond = 30)
    double Duration, FramesPerSecond, FrameDuration;  int FrameCount
    double TimeOfFrame(int frame)            // frame / fps
    int    FrameAt(double seconds)           // clamped to 0..FrameCount - 1
    double Clamp(double seconds)
    string FormatTimecode(double seconds)    // "HH:MM:SS:FF" (nominal frame rate, no dropped numbers)
    TimelineCue AddCue(string name, double startTime, double endTime, EasingKind easing = Linear)
    TimelineCue AddCue(string name, double startTime, double endTime, Func<double, double> curve)
    TimelineCue AddMarker(string name, double seconds)        // a cue with StartTime == EndTime
    TimelineCue AddCue(TimelineCue cue); TimelineCue GetCue(string name)   // null when missing
    double Progress(string cueName, double seconds)            // eased 0..1
    IReadOnlyList<TimelineCue> Cues; IReadOnlyList<TimelineCue> GetActiveCues(double seconds)
  TimelineCue (immutable): Name, StartTime, EndTime, Duration, IsMarker, Easing, HasCustomCurve,
    bool IsActiveAt(seconds) (StartTime <= t < EndTime), double LinearProgress(seconds),
    double Progress(seconds) (eased), double LocalTime(seconds)
FRAMES: frame i shows i / fps; FrameCount counts every frame that starts
before the end (21 s at 30 fps = 630). A time within a millionth of a frame
before a frame boundary counts as that frame, so FrameAt(TimeOfFrame(i)) == i.
Progress is 0 before a cue, 1 after it, (t - Start) / (End - Start) eased in
between (overshooting curves may leave 0..1). A marker's progress steps from 0
to 1 at its time. GetActiveCues lists the stretches containing the time and
the markers that fall in the same frame. Cue names are unique (case-sensitive).

Keyframe tracks (Animation)
---------------------------
    FloatTrack, PointTrack, ColorTrack : KeyframeTrack<T>   // derive for your own T
    KeyframeTrack<T> Add(double time, T value, EasingKind easing = Linear)
    KeyframeTrack<T> Add(double time, T value, Func<double, double> curve)
    KeyframeTrack<T> AddHold(double time, T value)          // step: previous value holds, then jumps
    KeyframeTrack<T> Add(Keyframe<T> key); void Clear(); Keyframe<T>[] GetKeyframes(); int Count
    TrackLoopMode LoopMode { get; set; }                     // Hold (default), Loop, PingPong
    double StartTime, EndTime, Duration;  T Evaluate(double seconds)
    // collection initializer: new FloatTrack { { 0, 0f }, { 2, 1f, EasingKind.CubicOut } }
  Keyframe<T> (struct): Time, Value, Easing, HasCustomCurve, IsHold; static Hold(time, value)
EVALUATION: before the first key the first value; at or after the last key
the last value; between keys a and b, u = (t - a.Time) / (b.Time - a.Time) is
eased by key b's curve - a key's easing shapes the segment that ARRIVES at it
("get to this value by this time, like this") - and the value is
Interpolate(a, b, eased); a hold key b keeps a's value until b's time. Keys
sharing a time make a jump (from that time the later-added key holds).
Loop repeats the span first key .. last key forever in both directions (the
last key's time is the next cycle's start); PingPong plays it forward then
back. Floats and point coordinates interpolate as from + (to - from) * u
(overshooting curves extrapolate); colors interpolate exactly like a
ColorRamp (straight 8-bit sRGB per channel, alpha included, rounded half away
from zero, clamped to 0..255). Evaluate on an empty track throws
InvalidOperationException. Tracks are thread-safe to read (copy-on-write).

Binding tracks to properties (Models)
-------------------------------------
    DrawingAnimations session.Animations         // applied whenever session.Time is set
    AnimationBinding Bind(DrawingElement element, AnimatedProperty property, KeyframeTrack<float|PointF|Color> track)
    AnimationBinding Bind(DrawingGroup group, AnimatedProperty.Color, KeyframeTrack<Color> track)
    AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<float|PointF|Color> track)
    AnimationBinding Bind(DrawingCamera camera, AnimatedProperty property, KeyframeTrack<float|PointF> track)
    AnimationBinding Bind<T>(KeyframeTrack<T> track, Action<T> apply)   // any setter of yours
    AnimationBinding Bind(Action<double> applyAtTime)                   // any per-time code
    bool Remove(AnimationBinding); void Clear(); int Count; AnimationBinding[] GetBindings()
    void Apply(double seconds)        // what session.Time does (a renderer of your own can call it)
  AnimatedProperty and what each sets:
    Position (point)   Transform.Translation        camera: Pan
    Anchor (point)     Transform.Anchor             camera: Pivot
    Rotation (float)   Transform.Rotation, degrees  camera: Rotation
    Scale (float: both ways; point: X and Y)        camera: Zoom (float; keep it positive)
    ScaleX, ScaleY (float)  one axis of the scale   (not the camera)
    Opacity (float)    Opacity, clamped to 0..1     (not the camera)
    Color (color)      a layer's Color, a group's ContentColor
    TrimStart, TrimEnd, TrimOffset, MorphFraction (float)   a PathShape's trim and morph
    FrameIndex (float, rounded down)   a SpriteShape's frame index
    Tint (color)       an ImageShape's or SpriteShape's tint
    BlurRadius, ShadowBlurRadius, GlowRadius (float); ShadowOffset (point);
    ShadowColor, GlowColor (color)     an element's, group's or layer's Effects
    RevealFraction, RevealCount (float)   a TextBlockShape's reveal (see TEXT BLOCKS)
    (and BindStagger staggers an entrance over many elements - see TEXT BLOCKS)
    (and BindAlongPath moves an element along a path - see PATHS, PICTURES,
    SPRITES, MASKS AND EFFECTS)
A track of the wrong kind for a property throws ArgumentException at Bind.
Bindings apply in the order bound (a later binding of the same property
wins) - unless made with BindingMode.Add or Multiply, which combine with the
property instead, whatever the order (see MOTION ALONG PATHS, COMBINED
BINDINGS, ...). A single shape's OWN properties (a line's ends, a radius, a text's
words or color) animate through AnimatedShape (below).

Transforms and opacity (Models, Shapes)
---------------------------------------
    DrawingTransform (immutable class): DrawingTransform(PointF translation, float rotation = 0,
        float scaleX = 1, float scaleY = 1, PointF anchor = default)
        Translation, Rotation (degrees; positive = clockwise on screen), ScaleX, ScaleY, Anchor, IsIdentity
        static Identity, Translate(x, y), Rotate(degrees, anchor), Scale(scale, anchor)
        WithTranslation / WithRotation / WithScale(s) / WithScale(sx, sy) / WithAnchor
        Matrix3x2 ToMatrix(); PointF TransformPoint(PointF); RectangleF TransformBounds(RectangleF)
        value equality (==, !=, Equals, GetHashCode)
    DrawingElement.Transform  { get; set; }   // Identity by default; null restores it
    DrawingElement.Opacity    { get; set; }   // 0..1, default 1
    DrawingLayer.Transform    { get; set; }   // the whole layer
    RectangleF? DrawingElement.GetTransformedBounds()   // own bounds through own transform
A point p of the content lands at Anchor + Translation + Rotate(Scale(p -
Anchor)). Transforms are applied WHILE DRAWING (never by resampling a
picture), in calibrated units, so transformed content is as sharp as plain
content, live and exported. Opacity below 1 draws the element whole into a
layer that is then faded, so a multi-part shape (an arrow's head and shaft)
fades as one unit; 0 draws nothing (so animated element fades cost a layer
each per frame - fade a LAYER where you can, see What things cost). These properties may change while the
element is on a layer - that is the point - and the next render shows it.

Groups (Shapes)
---------------
    DrawingGroup(Color? color = null) : DrawingShape     // put it on a layer like any shape
    T Add<T>(T element) where T : DrawingElement; bool Remove(DrawingElement); void Clear()
    DrawingElement[] GetChildren(); int Count
    Color? ContentColor { get; set; }     // the color of children without their own (animatable)
    RectangleF? GetBounds()               // children after their transforms; Empty when empty;
    RectangleF? GetBoundsAt(double t)     //   null when any child is unknown
A group moves, turns, scales and FADES AS ONE UNIT through its own Transform
and Opacity: its children are drawn together first and the result is faded,
so overlapping children never show through each other (two separately faded
elements do). Children keep their own transforms, opacities and time windows
inside it (child coordinates are the group's coordinates); groups nest; a
group cannot contain itself (ArgumentException). Children without a color
draw in ContentColor, else the group's color, else the layer's.

Time windows (Models)
---------------------
    double? DrawingElement.StartTime { get; set; }   // null = from the beginning
    double? DrawingElement.EndTime   { get; set; }   // null = until the end (exclusive)
    bool DrawingElement.IsVisibleAt(double seconds)
    double DrawingSession.Time { get; set; }; double DrawingRenderer.Time { get; set; }
An element exists while StartTime <= Time < EndTime: elsewhere it is not
drawn, exported, measured or safe-area checked. Add everything once and set
the session's Time - seeking backwards needs no rebuilding. Before Time is
first set it is 0.

Animated shapes (Shapes)
------------------------
    AnimatedShape(Func<double, DrawingShape> factory, double initialTime = 0) : DrawingShape
    Factory; DrawingShape Current; double Time; void Update(double seconds); GetBounds()
The factory returns the shape to draw at a time (any shape, a group, or null
for nothing); the session calls Update for every animated shape on its layers
(and inside groups) whenever its Time is set. The returned shape draws with
its own color, transform and opacity, inside the animated shape's own. Write
the factory as a pure function of the time. Returning the same instance as
before tells the renderer nothing changed. The safe-area flag goes on the
AnimatedShape itself (it is the element on the layer).

Camera and parallax (Models)
----------------------------
    DrawingCamera: PointF Pan; float Zoom (> 0); float Rotation (degrees); PointF? Pivot (null = centre);
        bool IsIdentity; Matrix3x2 GetMatrix(Size calibrationSize, float parallax = 1);
        PointF ToView(PointF scenePoint, Size calibrationSize, float parallax = 1); PointF ToScene(...)
    DrawingCamera DrawingSession.Camera { get; set; }    // null (default) = none; also DrawingRenderer.Camera
    float DrawingLayer.Parallax { get; set; }             // 1 default; 0 = fixed to the frame (HUD, titles)
The values describe the CAMERA, so the picture moves the other way: pan right
and the scene slides left, zoom 2 shows it twice as large, roll clockwise and
it turns counter-clockwise; zoom and roll happen about the pivot. A scene
point p on a layer with parallax f appears at
P + Rotate(-f * Rotation, Zoom^f * (p - P - f * Pan)), P the pivot. Applied
while drawing, so content stays sharp at any zoom. Changing the camera
redraws every layer it touches; layers with parallax 0 keep their caches.

Layout anchors (Layout; SafeAreaGuide)
--------------------------------------
    RectangleF SafeAreaGuide.GetSafeRectangle(SizeF frameSize, double? seconds = null, double? duration = null)
    DrawingLayout(Size calibrationSize)
    DrawingLayout(Size calibrationSize, SafeAreaGuide guide, Size? frameSize = null,
        double? seconds = null, double? duration = null)
        CalibrationSize, FrameSize, Guide, RectangleF Frame, RectangleF SafeArea, IsPortrait, ShortSide
        RectangleF GetRectangle(LayoutReference)
        PointF Place(LayoutPosition position)
        PointF Place(LayoutAnchor anchor, LayoutReference reference = Frame, float offsetX = 0, float offsetY = 0)
        RectangleF PlaceBox(SizeF size, LayoutPosition position)   // the box's own anchor lands on the point
    LayoutAnchor (struct): X, Y fractions; TopLeft, TopCenter, TopRight, CenterLeft, Center,
        CenterRight, BottomLeft, BottomCenter, BottomRight; PointF PointIn(RectangleF)
    LayoutPosition (struct): (anchor, reference = Frame, offsetX = 0, offsetY = 0, fractionX = 0, fractionY = 0)
    LayoutReference: Frame, SafeArea
A position is an anchor of the frame or of the safe rectangle, plus an offset
in calibrated units and an offset as fractions of that rectangle's size -
"centred, 5% above the safe bottom" is
new LayoutPosition(LayoutAnchor.BottomCenter, LayoutReference.SafeArea, fractionY: -0.05f).
Size things from ShortSide or the safe rectangle and one scene description
serves portrait and landscape. The safe rectangle is the frame inset by the
guide's margins, cut to its crops, intersected across combined parts (whole
frame when no rule applies, Empty when a zone covers everything), mapped into
calibrated units through the drawing's aspect-fit rectangle.

Frame-sequence renderer (root namespace)
----------------------------------------
    FrameSequenceRenderer(AnimationTimeline timeline, Func<DrawingSession> sessionFactory, Size frameSize)
    bool IncludeBackground (true); int MaxDegreeOfParallelism (1); bool DisposeSessions (true)
    bool KeepSessions (false); int KeptSessionCount; void ReleaseSessions()   // reuse sessions across renders
    Action<DrawingSession, double> PrepareFrame      // per-frame code after Time is set
    int FrameBytes
    void Render(int firstFrame, int lastFrame, FrameRenderedCallback onFrame, CancellationToken cancellationToken = default)
    int  RenderTimeRange(double startTime, double endTime, FrameRenderedCallback onFrame, CancellationToken cancellationToken = default)
    void Render(int firstFrame, IReadOnlyList<byte[]> destinations, CancellationToken cancellationToken = default)
    delegate void FrameRenderedCallback(int frame, double seconds, ReadOnlySpan<byte> rgba)
Each frame: session.Time = frame time, PrepareFrame, then ExportPixels (the
documented straight-alpha RGBA layout). The callback receives frames ONE AT A
TIME, IN ORDER, on the calling thread, whatever the thread count; the span is
valid only during the call. RenderTimeRange takes the frames whose time lies
in [startTime, endTime). Cancellation throws OperationCanceledException; an exception
on a worker reaches the caller.
WRITING A SCENE FOR PARALLEL RENDERING: the factory is called once per worker
(on the calling thread) and must build a COMPLETE, INDEPENDENT scene each
time - no element, group, animated shape, layer or camera shared between
sessions; the frame at t must depend only on t (drive change through
bindings, animated shapes, time windows and PrepareFrame, computing values
from t rather than accumulating them); shared objects (tracks, timelines,
fills, font registries) are only read. Written that way, the frames are
identical for any thread count (exports never use the live caches).

Playback controller (Animation)
-------------------------------
    PlaybackController(double duration, double framesPerSecond = 30)
    PlaybackController(AnimationTimeline timeline)
    Timeline, Duration, Time, Frame, IsPlaying, IsAtEnd
    bool IsLooping { get; set; }; double Speed { get; set; }  // 1 = real time, negative = backwards
    double MaxStep { get; set; }                        // DefaultMaxStep = 0.1
    void Play(); void Pause(); void Toggle(); void Restart()
    void Seek(double seconds); void SeekBy(double delta); void Step(int frames = 1)
    bool Advance(double elapsed)                    // true when it reached the end
A clock-free state machine for a live view: feed it the real time since the
last frame, read Time, show that moment. It starts paused at 0. Each advance
covers at most MaxStep of real time, so a slow frame PAUSES the
animation instead of skipping ahead. Without looping it stops (and pauses) at
the end, or at 0 playing backwards; Play/Toggle from there starts over. Step
pauses and moves to a frame start. Seeks clamp. Drive it from one thread.

    var playback = new PlaybackController(timeline);
    playback.Play();
    // per window frame:  playback.Advance(elapsed); session.Time = playback.Time; session.Render(...)
    // keys:              Space -> Toggle(), Left/Right -> SeekBy(-1 / 1), Home -> Restart()

Debug overlay (Rendering)
-------------------------
    DebugOverlay(FontRegistry fonts, AnimationTimeline timeline = null)
    Fonts, FamilyName, Timeline, double? Time (null = the Time of the session or renderer that
    draws it), ShowTimecode (true), ShowFrameNumber (true), ShowCues (false),
    DebugOverlayCorner Corner (TopLeft), float TextSize
    (0 = from the frame height), TextColor, PlateColor, bool CanDrawText, IReadOnlyList<string> GetLines(double)
    session.DebugOverlay = overlay;       // live Render ONLY (null = off, the default); also DrawingRenderer.DebugOverlay
    session.ExportPixels(buffer, size, includeBackground, safeAreaOverlay, debugOverlay)   // a still WITH it
    session.ExportImage(size, includeBackground, safeAreaOverlay, debugOverlay)
    session.ExportPng(size, includeBackground, safeAreaOverlay, debugOverlay)
    // DrawingRenderer: RenderToPixels / RenderToImage overloads with both overlays
A timecode, "frame N / COUNT" and the active cue names on a translucent plate
in a corner, drawn last (over the safe-area mask), in output pixels. Exactly
like the safe-area overlay it is OFF by default and NEVER in an export unless
passed to one of these overloads. The text uses a font from the registry you
give it - never a system font; with no registry, no font registered or an
unknown family it FAILS SOFT: it draws nothing and never throws (CanDrawText
reports which).

How the caches treat changing content
-------------------------------------
The live render keeps one bitmap per layer and redraws incrementally. With
the animation model:
  - plain elements (no transform, opacity 1, no time window) on a layer with
    no transform and no camera are appended incrementally, on the plain
    drawing path;
  - elements that APPEAR in list order (time-keyed, playing forward) are drawn
    incrementally, like appends;
  - anything that disappears, moves, fades or changes inside the already-drawn
    part of a layer - or a change of that layer's transform or of the camera -
    redraws THAT layer once; other layers keep their caches;
  - layer Opacity stays a compositing change (no redraw).
A hidden layer (IsVisible false) is skipped entirely. Exports, and every
layer a camera move touches, are drawn from scratch, element by element (see
FrameCache for live playback of such stretches).
So keep moving things on their own layer (a "Now" layer redraws each frame),
and time-keyed marks in the order they appear. Exports are always
from-scratch renders at the session's Time.

PATHS, PICTURES, SPRITES, MASKS AND EFFECTS
===========================================
(namespaces CodeBrix.Imaging.Drawing.Paths, .Images and .Effects; the shapes
are in .Shapes)

Everything here is OPT-IN, and works with the animation model:
every new element takes a transform, an opacity, a time window, effects and
groups, and its animatable values bind to keyframe tracks through
session.Animations. What a path COMPUTES - lengths, points and directions at
a distance, bounds, trimmed and dashed pieces, flattened polylines, morphs,
motion along a path, sprite frame choice - is shared managed arithmetic in
double precision and identical, bit for bit, in both packages (measuring
uses only +, -, *, / and square roots, so it agrees across machines too;
building an arc, a star or a regular or rounded polygon calls the runtime's
trigonometry, which may differ in the last bit between operating systems). What is drawn
goes through each package's anti-aliased canvas and matches within the usual
edge tolerance. Effects (shadow, glow, blur) are COMPUTED from the drawn
pixels in shared managed code (the planes blur), so both packages apply the
same arithmetic to what their canvases drew.

    using CodeBrix.Imaging.Drawing.Paths;
    using CodeBrix.Imaging.Drawing.Images;
    using CodeBrix.Imaging.Drawing.Effects;

    PathGeometry flourish = PathGeometry.Parse("M 150 330 C 190 200, 280 190, 260 280 S 400 330, 420 300");
    PathShape ink = session.DrawPath(flourish, thickness: 7);
    session.Animations.Bind(ink, AnimatedProperty.TrimEnd, new FloatTrack { { 0, 0f }, { 4, 1f, EasingKind.SineInOut } });
    DrawingShape nib = session.DrawCircle(0, 0, 6, filled: true);         // drawn around (0, 0)
    nib.Effects = DrawingEffects.Glow(16, Color.SkyBlue, 1.5f);
    session.Animations.BindAlongPath(nib, flourish, new FloatTrack { { 0, 0f }, { 4, 1f, EasingKind.SineInOut } });
    session.Time = 2.5;  session.ExportPixels(frame, size);              // half written, the nib on its tip

Path geometry (Paths)
---------------------
    PathGeometry (immutable, thread-safe): Contours, FillRule (NonZero | EvenOdd), IsEmpty, WithFillRule
        float GetLength(); PathPoint GetPointAt(float fraction); PathPoint GetPointAtDistance(float distance)
        RectangleF GetBounds()                         // exact curve extent, not control points
        PathGeometry Trim(float start, float end, float offset = 0)
        PathGeometry Dash(IReadOnlyList<float> pattern, float offset = 0)
        IReadOnlyList<PathPolyline> Flatten(float tolerance = DefaultFlattenTolerance)   // 0.25 units
        bool Contains(PointF, tolerance); float DistanceToEdge(PointF, tolerance); float GetSignedDistance(...);
        PointF GetNearestPoint(...); float Distance(PathGeometry other[, PointF otherOffset], tolerance)
        PathPoint GetPointAt(float fraction, bool extendBeyondEnds); GetPointAtDistance(float, bool)
        (point queries and travel past the ends: see MOTION ALONG PATHS, COMBINED BINDINGS, ...)
        PathGeometry Transformed(Matrix3x2 matrix); PathGeometry Reversed()
        static Combine(params PathGeometry[]), Line, FromPoints(points, closed), Rectangle, RoundedRectangle,
            Ellipse, Circle, RegularPolygon(center, radius, sides, rotation = 0),
            Star(center, outerRadius, innerRadius, points, rotation = 0), RoundedPolygon(points, cornerRadius)
        static PathGeometry Parse(string svgPathData); static bool TryParse(string, out PathGeometry)
        string ToSvgPathData()                         // absolute M/L/Q/C/Z, round-trip numbers
        static FromSkiaPath(SKPath) (conics become quadratics); SKPath ToSkiaPath() (caller disposes)
        static readonly Empty; const float DefaultFlattenTolerance
    PathGeometryBuilder: MoveTo, LineTo, QuadraticTo, CubicTo, ArcTo(rx, ry, xAxisRotation, largeArc,
        clockwise, end) (SVG's A), AddArc(center, rx, ry, startAngle, sweepAngle), Close, AddPolygon,
        AddRectangle, AddRoundedRectangle, AddEllipse, AddCircle, AddPath, FillRule, CurrentPoint,
        ContourCount, Build() (the builder can keep going), Clear()
    PathContour (immutable): StartPoint, EndPoint, IsClosed, SegmentCount, this[i], GetSegments(), Length
        new PathContour(start, segments, isClosed)     // segments must chain exactly
    PathSegment (struct): Kind (Line | Quadratic | Cubic), Start, Control1, Control2, End; Line(), Quadratic(), Cubic()
    PathPoint (struct): Position, Tangent (unit), Angle (degrees: 0 right, 90 down), Distance, ContourIndex
    PathPolyline: IsClosed, Count, this[i], GetPoints()   // a closed polyline does not repeat its start
Coordinates are calibrated drawing units with y pointing DOWN, so a positive
angle and a "clockwise" sweep turn clockwise on screen. Arcs and the factory
circles and ellipses are stored as cubic curves (a circle's four cubics are
within 0.03% of the radius). Parse reads SVG path data: M L H V C S Q T A Z,
absolute and relative, repeated commands and compact numbers ("1-2",
".5.5", "1e3", arc flags without separators); bad data throws
FormatException with the position. A closed contour's last side is an
implicit closing line, part of its length, not a counted segment.
MEASURING: each curve is divided into the number of equal parameter steps
Wang's formula gives for chords within 0.01 units of the curve, and lengths
are the sums of those chords in double precision; a point at a distance is
found in that table and then evaluated ON the curve (its tangent is the
curve's own derivative). Fractions are of the TOTAL length, the contours
measured one after another.
TRIM (draw-on): the part between two fractions of the length, both shifted
by the offset (1 = once round; a part pushed past the end wraps round to the
start). Start 0, end 1 returns the geometry itself (a closed contour stays
closed); otherwise the pieces are open; a wrapped part of ONE closed contour
stays one piece across its start. DASH: each contour from its start, the
pattern alternating dash and gap lengths (an odd-length pattern is used
twice, as in SVG), offset = how far into the pattern each contour starts;
zero-length dashes are kept as points, so round caps draw dots. FLATTEN:
every chord within the tolerance of its curve - the polygons that
hard-edged masks and polygon algorithms want.

Path shapes (Shapes.PathShape)
------------------------------
    PathShape(PathGeometry geometry, float strokeThickness = 15, Color? color = null, bool isFilled = false)
                                         // filled: the thickness is not used and 0 is accepted
    PathShape(PathGeometry geometry, Color fillColor)                        // filled, no outline
    PathShape(PathGeometry geometry, ShapeFill fill)                         // filled by a gradient
    PathShape(PathGeometry geometry, PathStrokeStyle strokeStyle, Color? strokeColor = null,
              Color? fillColor = null, ShapeFill fill = null)                // outline over a fill
    PathShape(PathMorph morph, float fraction, ...)                          // the same two forms for a morph
    PathShape session.DrawPath(PathGeometry geometry, float thickness = 15, Color? color = null, bool filled = false)
    Geometry, Morph, MorphFraction { get; set; }, StrokeStyle, IsStroked, IsFilled, FillColor,
    TrimStart / TrimEnd / TrimOffset { get; set; }, IsTrimmed, GetDrawnGeometry(), GetBounds(), GetOutline()
    GetStrokeOutline(float tolerance = DefaultFlattenTolerance)   // the stroke's area as polygons (below)
    PathStrokeStyle(float width, PathStrokeCap cap = Round, PathStrokeJoin join = Round,
        float miterLimit = 4, IReadOnlyList<float> dashPattern = null, float dashOffset = 0)
        // Width, Cap (Round | Butt | Square), Join (Round | Miter | Bevel), MiterLimit, DashOffset,
        //   IsDashed, GetDashPattern(), WithWidth(w), WithDashOffset(o)  - immutable, shareable
The trim applies to the fill too (each piece fills as if closed), as in
animation tools. TrimStart, TrimEnd, TrimOffset and MorphFraction may change
while the shape is on a layer - bind float tracks with AnimatedProperty
TrimStart, TrimEnd, TrimOffset, MorphFraction (bindings clamp the fractions
to 0..1; set directly, TrimStart/TrimEnd/MorphFraction outside 0..1 or NaN
throw ArgumentOutOfRangeException, MorphFraction throws
InvalidOperationException on a shape not made from a PathMorph, and
TrimOffset takes any finite value).
Dashes are cut in managed code from the trimmed path; for "marching ants"
rebuild the shape with StrokeStyle.WithDashOffset in an AnimatedShape (or
animate TrimOffset). Bounds are the drawn path's exact extent grown by half
the width (times the miter limit for miter joins, root 2 for square caps).

Combining paths, stroke to outline, inflate (Paths.PathGeometry)
----------------------------------------------------------------
    PathGeometry Union(PathGeometry other, float tolerance = DefaultFlattenTolerance)
    PathGeometry Intersect(PathGeometry other, float tolerance = ...)
    PathGeometry Subtract(PathGeometry other, float tolerance = ...)        // this less other
    PathGeometry ExclusiveOr(PathGeometry other, float tolerance = ...)
    PathGeometry CombineWith(PathGeometry other, PathBooleanOperation operation, float tolerance = ...)
    static PathGeometry CombineAll(IEnumerable<PathGeometry> geometries, PathBooleanOperation operation,
        float tolerance = ...)
    PathGeometry Simplify(float tolerance = ...)                             // remove overlaps
    PathGeometry ToStrokeOutline(PathStrokeStyle style, float tolerance = ...)   // a stroke's area
    PathGeometry Inflate(float distance, PathStrokeJoin join = Round, float miterLimit = 4, float tolerance = ...)
    PathGeometry Deflate(float distance, PathStrokeJoin join = Round, float miterLimit = 4, float tolerance = ...)
    PathGeometry PathShape.GetStrokeOutline(float tolerance = ...)   // trim, morph and dashes applied; null if unstroked
    PathBooleanOperation: Union, Intersection, Difference, ExclusiveOr
    bool TextBlockShape.MergeOverlaps { get; set; }                  // merged glyph outlines (TEXT BLOCKS)
These work on the AREAS paths fill. Each operand fills by its own FillRule,
and every contour counts as closed (an open contour fills as if closed, as
when it is drawn filled). CombineAll: Union = anything any of them fills;
Intersection = only what all of them fill; Difference = the first less all
the others; ExclusiveOr = what an odd number of them fill; one geometry
gives its Simplify(), none gives Empty. Simplify is REMOVE OVERLAPS: the
filled area as clean polygons, self-intersections resolved and overlapping
contours merged (a font glyph's overlapping strokes become one outline).
HOW IT IS COMPUTED: curves are flattened into chords within the tolerance
(Wang's formula, as Flatten), every point is rounded to the nearest 1/1024
of a unit (halves away from zero), and the polygons are combined by
CodeBrix.PolygonTools - the CodeBrix family's managed clipping engine, a
dependency of this package - in exact 64-bit integer arithmetic. THE RESULT
is polygons: closed contours of straight lines (no curves), corners on that
1/1024 grid (so exactly representable as floats), non-overlapping, holes
wound against their outlines, with the NonZero fill rule (EvenOdd fills them
the same); Empty when nothing is left. Results are identical, bit for bit,
in both packages; the flattening and the integer clipping use only +, -, *,
/ and square roots, so they agree across machines too - except the round
joins, round caps and dots of stroke outlines and inflating, which call the
runtime's Math.Acos/Cos/Sin/Atan2 and may move by one grid step between
operating systems.
STROKE TO OUTLINE: the area a stroke covers, exactly as PathShape draws it -
Width; caps Butt, Round, Square; joins Miter (bevelled where miter length /
width would exceed MiterLimit, the SVG rule), Round, Bevel; the dash pattern
cut first (Dash). Trim the geometry first (Trim), or call
PathShape.GetStrokeOutline, which uses the shape's trim, morph and dashes.
Corner points inside a divided curve are joined round, so curves stay smooth
whatever the join. A zero-length open contour (a dot) gives a disc for round
caps, an axis-aligned square for square caps and nothing for butt caps.
Round arcs are chords within the tolerance. Fill the result to treat a
stroke as a shape: a gradient along it, a mask or clip from it, or combine
it with other areas.
INFLATE / DEFLATE: the filled area grown outward by the distance (negative,
or Deflate, shrinks it); 0 gives Simplify(). It is made by uniting (or taking
away) the fill's boundary stroked to twice the distance, so the corners take
exactly a stroke's join: Round (the default) is the true offset; Miter keeps
sharp corners whose tip lies at most miterLimit x the distance from the old
corner, bevelling the rest; Bevel cuts them. Every corner of the flattened
outline takes the join (on a flattened curve those corners are tiny). Holes
shrink as the fill grows; parts thinner than twice the distance vanish when
deflating.
LIMITS AND COST: coordinates must stay within a million units
(ArgumentException beyond). The results are polygons at the tolerance you
gave: to scale one up a long way, combine at the larger size or pass a
smaller tolerance. The cost grows with the number of edges and crossings
(roughly (n + k) log n): recomputing shapes of a few thousand points each
frame is fine; cache results that do not change.
    PathGeometry ring = PathGeometry.Circle(c, 80).Subtract(PathGeometry.Circle(c, 50));
    PathGeometry badge = PathGeometry.CombineAll(new[] { ring, star }, PathBooleanOperation.Union);
    session.DrawPath(badge.Inflate(6, PathStrokeJoin.Round), filled: true, color: Color.Gold);
    PathGeometry ribbon = flourish.ToStrokeOutline(new PathStrokeStyle(18, PathStrokeCap.Round));
    session.DrawShape(new PathShape(ribbon, new LinearGradientFill(a, b, Color.Orange, Color.Purple)));

Motion along a path (Models.DrawingAnimations)
----------------------------------------------
    AnimationBinding BindAlongPath(DrawingElement element, PathGeometry path, KeyframeTrack<float> progress,
        bool orientToPath = false, float rotationOffset = 0)
On every apply the element's Transform is moved so that its ANCHOR point
lies on the path at the track's fraction of the length (clamped to 0..1) -
draw the moving element around (0, 0), or set its Transform.Anchor to its
own pivot - and with orientToPath its rotation becomes the path's direction
there plus rotationOffset (for content drawn facing another way than right).
The scale is kept. For a loop, give the track TrackLoopMode.Loop.

Morphing (Paths.PathMorph)
--------------------------
    PathMorph(PathGeometry from, PathGeometry to, int samplesPerContour = 0)
    From, To, SampleCount, PairCount, PathGeometry GetPath(float fraction)
THE CORRESPONDENCE RULE: contours pair up in order; when one path has more,
each extra contour pairs with a single point at its own centre (it grows out
of, or shrinks into, that point). Each pair is resampled to the same number
of points spaced evenly by length (default one every 2 units of the longer
contour, 16 to 4096). Two closed contours are matched as loops - the second
reversed if the two run opposite ways round, then turned to the starting
sample that brings them closest (least sum of squared distances). Two open
contours are matched end to end, the second reversed when that is closer.
An open contour paired with a closed one cuts the loop at its best sample
(either way round) and the in-between is open. Points move in straight
lines; a pair is closed when both contours are. Fraction 0 returns From
itself and 1 returns To itself; in between the shape is straight segments
through the samples. The correspondence is worked out once, at
construction (the start search is quadratic in the sample count).

Shape outlines (every shape)
----------------------------
    public virtual PathGeometry DrawingShape.GetOutline()     // default null = unknown
    public PathGeometry Stroke.GetOutline()                   // points as a polyline; a dot as a circle
    public PathGeometry DrawingElement.GetTransformedOutline()  // through the element's own transform
Every built-in shape reports its GEOMETRY before its own transform: the
centre line of an outline (line, arrow shaft and head, unfilled shapes) or
the boundary of a filled area (circles, rectangles with their corners,
polygons, text glyphs exactly as drawn, particles as circles, images and
sprites as their rectangle); groups combine their children's (after each
child's transform; null if any is unknown); an animated shape reports its
current shape's. The stroke's thickness is not included (a stroke's AREA is
PathGeometry.ToStrokeOutline / PathShape.GetStrokeOutline, below). Flatten the result
for polygons.

Pictures (Images.ImageSource, Shapes.ImageShape)
-----------------------------------------------
    ImageSource (IDisposable): static FromEncoded(byte[]), FromFile(path), FromRgba(ReadOnlySpan<byte>, w, h),
        FromBgra(ReadOnlySpan<byte>, w, h), FromImagingImage(Image<Rgba32>), FromPlaneImage(DrawingPlaneImage),
        FromSkiaBitmap(SKBitmap) (copied, read as straight alpha); Width, Height, Size, IsDisposed, Crop(Rectangle) (a new source),
        byte[] ToRgba(), Dispose()
    ImageShape(ImageSource source, RectangleF destination, Rectangle? sourceRectangle = null,
        ImageSampling sampling = ImageSampling.Linear, Color? tint = null)
        // Source, Destination, SourceRectangle, Sampling, Tint { get; set; }, GetBounds(), GetOutline()
    ImageShape session.DrawImage(ImageSource source, RectangleF destination, ImageSampling sampling = Linear)
    ImageSampling: Linear (bilinear, in premultiplied colour; the default and the enum's zero),
        Nearest (hard pixels)
A picture is stored as straight-alpha RGBA; encoded bytes are decoded by
CodeBrix.Imaging, so both packages hold the same pixels. It is drawn into
its destination (calibrated units), stretched, with anti-aliased edges when
turned or between pixels; the element's Opacity fades it; Tint multiplies
every channel including alpha (white = no change; bind a ColorTrack with
AnimatedProperty.Tint). OWNERSHIP: every factory COPIES the pixels; the
source owns its copy until Dispose. Shapes only refer to a source and never
dispose it: keep it alive while anything may still render it (a shape whose
source is disposed draws nothing). One source may be shared by many shapes
and by sessions rendered on other threads (drawing only reads it).
Linear sampling of a source rectangle may read the pixels just outside it at
its edges; a sprite sheet avoids that by cutting its cells apart.

Sprites (Images.SpriteSheet, Shapes.SpriteShape)
-----------------------------------------------
    SpriteSheet (IDisposable): static FromGrid(ImageSource image, int columns, int rows, int frameCount = 0),
        static FromRectangles(ImageSource image, IReadOnlyList<Rectangle> cells),
        new SpriteSheet(IReadOnlyList<ImageSource> frames); FrameCount, GetFrame(i), OwnsFrames, IsDisposed,
        Dispose()
    SpriteShape(SpriteSheet sheet, RectangleF destination, double framesPerSecond = 0,
        SpritePlayback playback = SpritePlayback.Loop, ImageSampling sampling = ImageSampling.Linear)
        // FrameIndex { get; set; }, PlaybackStartTime { get; set; }, Tint { get; set; }, FramesPerSecond,
        //   Playback, Sampling, Destination, Sheet, int GetFrameAt(double seconds)
    SpriteShape session.DrawSprite(SpriteSheet sheet, RectangleF destination, double framesPerSecond = 0,
        SpritePlayback playback = Loop, ImageSampling sampling = Linear)
    SpritePlayback: Loop (0,1,2,0,1,2...), PingPong (0,1,2,1,0,1...), Hold (plays once, holds the ends)
FromGrid and FromRectangles COPY each cell into a frame of its own (cells
never bleed into each other) and the sheet owns and disposes those frames;
the image you cut from stays yours. A sheet of given frames only refers to
them. The frame at render time t is
n = FrameIndex + floor((t - PlaybackStartTime) * FramesPerSecond + 1e-6) (just
FrameIndex at 0 fps) mapped by the playback mode - the session's (or
renderer's) Time plays it, live and exported, with no per-frame code. Or
step it by hand: bind a FloatTrack with AnimatedProperty.FrameIndex (rounded
down). Make sheets in code by exporting small sessions or plane images and
copying the cells side by side into one RGBA buffer.

Clipping and masks (Effects.DrawingMask)
----------------------------------------
    DrawingMask.Inside(PathGeometry geometry, bool antialias = true)     // keep inside a path (a clip)
    DrawingMask.Outside(PathGeometry geometry, bool antialias = true)    // keep outside (a cut-out)
    DrawingMask.FromLayer(DrawingLayer layer, bool invert = false)       // keep where its elements cover
    DrawingMask.FromPlane(DrawingPlane plane, RectangleF destination, bool invert = false)  // alpha mask
    DrawingMask.FromPlane(DrawingPlane plane, bool invert = false)       // one unit per pixel from (0, 0)
    // Kind, Geometry, IsAntialiased, Layer, Destination, PlaneWidth, PlaneHeight, IsInverted,
    //   IsDisposed, Dispose() (DrawingMask is IDisposable - see OWNERSHIP below)
    DrawingLayer.Mask { get; set; }      DrawingGroup.Mask { get; set; }
A mask is in the SAME coordinates as what it masks: a layer's mask in the
layer's element coordinates (it follows the layer transform and the camera
like the elements), a group's in the group's coordinates (it moves and
turns with the group); mask one element by putting it in a group. The
content is drawn whole first and the mask then multiplies its coverage ONCE
(anti-aliased by default), so overlapping elements show no seam along the
edge. A layer matte uses the mask layer's ELEMENTS (their anti-aliased
coverage and colours' alpha, at the render time) drawn in the masked
content's coordinates; that layer's own opacity, transform, mask and effects
are not used, and it need not be in the session (give it Opacity 0 if it is
and should not show). A plane mask is copied when made (rounded to 8 bits),
stretched over its destination bilinearly, 0 outside it (1 when inverted);
make a new mask to animate one. Masks are immutable; assigning a new one each
frame is the way to move a layer's clip.
OWNERSHIP: a mask belongs to whoever made it; assigning it to a layer or
group does not hand it over. A PLANE mask keeps its copy of the plane as a
backend bitmap: Dispose() releases it at once (otherwise it is released when
the mask is collected) - dispose a per-frame plane mask once the next one
has replaced it, and never while a render that uses it is running. A
disposed plane mask covers nothing (the content it masks is hidden, or shown
whole when inverted). Path and layer masks hold no backend resource;
disposing them changes nothing. Fills (ShapeFill) hold no backend resource
either - they need no disposing however many shapes share them.

Shadow, glow and blur (Effects.DrawingEffects)
----------------------------------------------
    DrawingEffects.Blur(float radius); DrawingEffects.DropShadow(PointF offset, float blurRadius, Color color);
    DrawingEffects.Glow(float radius, Color color, float strength = 1); DrawingEffects.None
    WithBlurRadius, WithShadow, WithShadowOffset, WithShadowBlurRadius, WithShadowColor, WithGlow,
    WithGlowRadius, WithGlowColor, WithGlowStrength; BlurRadius, ShadowColor, ShadowOffset, ShadowBlurRadius,
    GlowColor, GlowRadius, GlowStrength, HasBlur, HasShadow, HasGlow, IsEmpty; value equality
    DrawingEffects.DropShadow(PointF offset, float blurRadius, Color color, ShadowDirection direction);
    WithShadow(offset, blurRadius, color, direction); WithShadowDirection(direction); ShadowDirection
    ShadowDirection (enum): FixedInScene = 0 (default), RotatesWithElement = 1
    DrawingElement.Effects { get; set; }  (any element, group or stroke)   DrawingLayer.Effects { get; set; }
HOW: the content is drawn into a private bitmap at the output resolution
(for a layer, its own bitmap after its mask), read back as straight-alpha
RGBA, and processed with the planes arithmetic: (1) the content is blurred
by BlurRadius; (2) the glow is its coverage blurred by GlowRadius, times
GlowStrength (capped at 1), in GlowColor; (3) the shadow is its coverage
blurred by ShadowBlurRadius and moved by ShadowOffset (bilinear for
fractions of a pixel), in ShadowColor; the result is content over glow over
shadow, the colours' alpha being each effect's opacity. A blur RADIUS
follows the CSS convention: the Gaussian's standard deviation is half of it.
SIZES FOLLOW THE CONTENT: radii and the shadow offset are in the element's
OWN units, so they grow and shrink with the element's scale, its groups'
and its layer's scale, the camera's zoom and the output size, exactly as its
outline does - zooming in keeps shadows, glows and blurs in proportion, and
the same scene exported at twice the size looks the same, only sharper. The
precise rule: let F be the linear map from the element's units to output
pixels (element transform, groups, layer transform, camera with the layer's
parallax, calibration scale). Every RADIUS is multiplied by sqrt(|det F|)
(the geometric mean of the two axis scales, so a stretched element gets a
round blur of the mean size). The shadow OFFSET o becomes F applied to o
with ShadowDirection.RotatesWithElement (it turns, stretches and mirrors with
the element); with ShadowDirection.FixedInScene (the default) it becomes
P (R o), where R is the camera's roll for that layer and P = sqrt(F^T F)
taken in the scene's axes - the element's stretch without its turn - so the
light stays fixed in the scene: the shadow ignores the element's, its
groups' and its layer's rotation (and mirroring) but turns with the camera's
roll, and is scaled per axis (a horizontal scale of 2 doubles a horizontal
offset; a uniform scale s multiplies it by s whatever the turn). For a
layer's effects the layer's transform plays the element's part. With no
transform and no camera this is exactly the frame-unit arithmetic of a plain
scale. An element's
effects apply inside its opacity (the shadow fades with it) and after its
transform; a group's apply to the group as a whole (after its mask).
Animate them with AnimatedProperty BlurRadius, ShadowOffset (point),
ShadowBlurRadius, ShadowColor, GlowRadius, GlowColor (color) on elements and
layers (and the colours on groups); an element or layer without effects
gets them. Effects are drawn by the session's renderer; a group or animated
shape drawn by your own code through DrawingShape.Draw shows no effects.
COST: each element with effects costs an offscreen bitmap of the output
size, a read-back, and the blur passes over the box around its coverage
grown by the effects' reach (about twice the largest radius) - roughly the
cost of the planes GaussianBlur on that box, four planes for a content blur
and one for each of shadow and glow. Keep radii modest on large areas, and
prefer one layer effect to many element effects on the same layer.

How the caches treat the new elements
-------------------------------------
  - A PathShape (fixed or morphing) or an ImageShape, like every other
    shape, is a STATIC mark until one of its changeable values is set (trim, morph
    fraction, tint, frame index, playback start, effects); from then on it is
    dynamic: changing it redraws its layer once.
  - A SpriteShape is dynamic from birth: its layer redraws whenever the frame
    it shows changes.
  - A layer with a Mask or Effects is POST-PROCESSED as a whole: it is
    redrawn whole whenever anything on it (or in its mask, including a mask
    layer's elements) changes, and otherwise kept as it is - appends are not
    incremental on such a layer.
  - A group's mask is part of the group's state (a mask layer's content
    included), so a changed matte redraws the group's layer.
  - Layer and coverage exports (ExportMask, ExportPlaneImage) include masks
    and effects: they are part of what the layer draws.

TEXT BLOCKS, REVEALS AND CAPTIONS
=================================
(namespaces CodeBrix.Imaging.Drawing.Text and .Captions; the shapes are in
.Shapes, the stagger in .Models)

Everything here is OPT-IN, and works with the animation model:
a text block is an ordinary element (transform, opacity, time window, groups,
camera, layout, bounds and safe-area checks, masks and effects), its reveal
binds to keyframe tracks, and its glyphs are PathGeometry. LAYOUT is shared
managed code: shaping (glyph choice, advances, KERNING, ligatures) is
CodeBrix.Imaging's font code, and line breaking, alignment, justification and
trimming are this library's own - so every line metric, cluster position and
glyph outline is IDENTICAL, value for value, in both packages and on every
machine. Only the drawing goes through each package's anti-aliased canvas.
Fonts are only those you register (FontRegistry); system fonts are never used.

    session.Fonts.RegisterFont("assets/Fonts/Roboto-Regular.ttf");
    TextLayoutResult para = session.LayoutText(text, 28, options: new TextLayoutOptions
    {
        MaxWidth = 600, Align = TextAlign.Justify, LineSpacing = 1.2f,
        MaxLines = 4, Trimming = TextTrimming.WordEllipsis,
    });
    var block = new TextBlockShape(para, 80, 120, Color.White);        // box top-left at (80, 120)
    session.DrawShape(block);
    session.Animations.Bind(block, AnimatedProperty.RevealCount,       // typewriter: 24 characters a second
        new FloatTrack { { 0, 0f }, { para.ClusterCount / 24.0, para.ClusterCount } });
    session.Time = 1.5;  session.ExportPixels(frame, size);

Kerning
-------
Text is KERNED whenever the font carries kerning data (an OpenType GPOS
"kern" feature or a legacy kern table) - TextShape, DrawText, MeasureText and
text blocks alike. Tracked (letter-spaced) text stays kerned: the line is
shaped as ONE run and the tracking is added between its shaped characters.
Fonts without kerning data (Open Sans, for one) lay out the same either way.
Switch kerning off per shape or per layout:
    new TextShape(text, x, y, size, fonts, color, align, tracking, familyName, kerning: false)
    new TextShape(text, x, y, size, fonts, fill, align, tracking, familyName, kerning: false)
    session.DrawText(text, x, y, size, color, align, tracking, familyName, kerning: false)
    fonts.MeasureText(text, size, tracking, familyName, kerning: false)
    new TextLayoutOptions { Kerning = false }                          // default true
    TextShape.Kerning                                                  // what the shape used
Ligatures the font defines (Roboto's "fi", "ffi") are shaped too; a ligature
is one CLUSTER (one character for reveals, pieces and tracking).

Laying out a block (Text.TextLayoutEngine, TextLayoutOptions, TextRunDescriptor)
-------------------------------------------------------------------------------
    TextLayoutResult TextLayoutEngine.Layout(FontRegistry fonts, string text, float size,
        string familyName = null, TextLayoutOptions options = null)
    TextLayoutResult TextLayoutEngine.Layout(FontRegistry fonts, IReadOnlyList<TextRunDescriptor> runs,
        TextLayoutOptions options = null)                              // runs concatenated in order
    session.LayoutText(string text, float size, string familyName = null, TextLayoutOptions options = null)
    session.LayoutText(IReadOnlyList<TextRunDescriptor> runs, TextLayoutOptions options = null)
    TextRunDescriptor(string text, float size = 12, string familyName = null)
        { Color = ..., Fill = ..., Tracking = ... }                    // init-only extras
    TextLayoutOptions (mutable; copied at layout; Clone()):
        float? MaxWidth          wrap width (null = no wrapping; lines break only at the text's own breaks)
        float? MaxHeight         box height: lines that would end below it are dropped (the first is kept)
        int MaxLines             0 = no limit
        TextAlign Align      Left, Center, Right, Justify (Justify: text blocks only)
        TextVerticalAlign VerticalAlign   Top, Center, Bottom - within MaxHeight
        float LineSpacing        multiplier on the font's line height (ascent + descent + gap); 1
        float LineHeight         explicit line height; 0 = from the fonts
        TextTrimming Trimming    None, CharacterEllipsis, WordEllipsis - what the last kept line does
        string Ellipsis          "\u2026" by default; set "..." yourself for a font without that
                                 character (there is no automatic fallback)
        bool Kerning             true
Runs: each run has its own family, size, tracking (fraction of its size),
and optionally its own Color or Fill (drawing only, never layout). A line's
height and baseline come from the tallest run on it; kerning applies within
a run, not across a run boundary.
LINE BREAKING: at the text's own breaks (LF, CR, CR LF, NEL, LS, PS, VT, FF)
and, with MaxWidth, after spaces (not no-break spaces), after a hyphen inside
a word, after a zero width space and between CJK ideographs; a word wider
than the line breaks between characters. Spaces at a wrapped line's end hang
(they do not count in its width). Left-to-right only: right-to-left scripts
are not reordered. Without MaxWidth, alignment uses the widest line.

The result (Text.TextLayoutResult - immutable, thread-safe)
-----------------------------------------------------------
COORDINATES: calibrated units, (0, 0) at the top-left of the block's BOX, y
down. A shape places the box.
    Text, Runs, Options (a copy)
    SizeF Size; Width; Height        // the box: MaxWidth x MaxHeight when set, else the widest line x the lines
    RectangleF ContentBounds         // what the lines actually take (the measured size)
    RectangleF? InkBounds            // around every glyph outline (control points included)
    IReadOnlyList<TextLineMetrics> Lines; LineCount; GetLineMetrics(i)
        TextLineMetrics: Index, TextIndex, TextLength (text range incl. trailing spaces and its break),
            Top, Height,
            BaselineOffset, Baseline, Left, Width, Ascent, Descent, FirstCluster, ClusterCount,
            EndsParagraph, Bounds
    IReadOnlyList<TextCluster> Clusters; ClusterCount; WordCount; GetUnitCount(TextUnit)
        TextCluster: Index, TextIndex, TextLength, Text, LineIndex, WordIndex (-1 for a space), RunIndex,
            IsWhiteSpace, IsEllipsis, Origin (pen start on the baseline), Advance, Bounds (advance x line
            height), InkBounds, HasInk, PathGeometry GetOutline()
    bool IsTruncated; bool HasEllipsis; int ShownTextLength
    IReadOnlyList<TextPiece> GetPieces(TextUnit unit)   // Character, Word, Line
        TextPiece: Unit, Index, FirstCluster, ClusterCount, TextIndex, TextLength, Text, LineIndex, Bounds,
            InkBounds, Center, IsWhiteSpace, PathGeometry GetOutline()
    PathGeometry GetOutline(); PathGeometry GetOutline(float x, float y)   // every glyph, moved to (x, y)
    int GetClusterIndexAt(int textIndex); int GetLineIndexAt(int textIndex); RectangleF GetRectForIndex(int textIndex)
    PathGeometry GetOutlineAlongPath(PathGeometry path, float startDistance = 0, float verticalOffset = 0)
    IReadOnlyList<PathGeometry> GetClusterOutlinesAlongPath(path, startDistance = 0, verticalOffset = 0)
A CLUSTER is one character as the font shaped it: a letter, a space, a letter
with its combining mark, or a ligature. Glyph outlines are closed contours
with the non-zero fill rule; many fonts (Roboto among them) build glyphs from
OVERLAPPING contours, so stroking an outline's centre line also traces the
overlaps inside the glyphs (see StrokePlacement and MergeOverlaps below;
PathGeometry.Simplify merges the contours of any outline).
TEXT ON A PATH: each cluster's horizontal centre goes to startDistance plus
its distance from the box's left edge along the path, its baseline (the
first line's) laid along the path's direction there, verticalOffset below
the path (negative: above). Clusters off the end of an open path are left
out; a closed path wraps. Turning uses the path's unit tangent (no
trigonometry), so it is identical across machines. Draw it with a PathShape:
    session.DrawShape(new PathShape(layout.GetOutlineAlongPath(wave, 12f, -4f), new LinearGradientFill(...)));

Drawing a block (Shapes.TextBlockShape)
---------------------------------------
    new TextBlockShape(TextLayoutResult layout, float x, float y, Color? color = null)   // filled
    new TextBlockShape(layout, x, y, ShapeFill fill)                                     // gradient fill
    new TextBlockShape(layout, x, y, PathStrokeStyle strokeStyle, Color? strokeColor = null,
        Color? fillColor = null, ShapeFill fill = null)                                  // outlined (+ fill)
    session.DrawTextBlock(layout, x, y, Color? color = null)
    Layout, X, Y, BoxBounds, StrokeStyle, IsStroked, IsFilled, FillColor, FirstCluster, ClusterCount
    TextStrokePlacement StrokePlacement { get; set; }   // Center (default) | Outside
    bool MergeOverlaps { get; set; }         // false (default); const float MergeTolerance = 0.1
    TextUnit RevealUnit { get; set; }        // Character (default), Word, Line
    float RevealFraction { get; set; }       // 0..1, default 1 (everything)
    float RevealCount { get; set; }          // units shown; default all
    float RevealSoftness { get; set; }       // units a unit fades in over; 0 = appears at once
    bool IsFullyRevealed; int GetUnitCount(); int GetUnitCount(TextUnit)
    IReadOnlyList<TextBlockShape> CreatePieces(TextUnit unit); DrawingGroup CreatePieceGroup(TextUnit unit)
(x, y) is the box's top-left corner. Runs with their own Color or Fill paint
in it. GetBounds is the box around the glyphs that currently show (grown by
the outline's reach when outlined); an empty rectangle at (x, y) when none
does. GetOutline is those glyphs' outlines, at the shape's position.
OUTLINES: StrokePlacement.Center strokes the outline centred and over the
fill, like any path - it traces overlapping contours inside the glyphs.
StrokePlacement.Outside keeps only the half of the stroke OUTSIDE the glyphs
(the inside half is cut away by the glyphs' own shape) and draws the fill
over it: a clean outline Width / 2 wide, with no overlap lines. Use Outside
for outlined lettering. Or set MergeOverlaps = true: each glyph's contours
are united (PathGeometry.Simplify, flattened within MergeTolerance = 0.1
units) before it is filled and stroked, so a CENTRED stroke, dashes and
GetOutline are clean too - at the cost of drawing polygons instead of the
font's curves (chords within 0.1 units). Separate glyphs are merged one by
one, not with each other; pieces keep the setting; it works with either
placement.
REVEAL: units are numbered in reading order (characters include spaces;
words skip them; lines). With RevealSoftness 0 unit n shows once RevealCount
reaches n + 1 (count 3 shows units 0, 1 and 2); with softness s it fades in
while the count goes from n + 1 - s to n + 1. RevealFraction = count / units.
Bind AnimatedProperty.RevealFraction (clamped 0..1) or RevealCount (negative
counts as 0) to float tracks; setting either makes the shape dynamic (its
layer redraws); a block that never changes is a static mark. To place a
caret, read layout.Clusters[n].Bounds (n = the next cluster) or
GetRectForIndex.
PIECES: CreatePieces(unit) returns one TextBlockShape per character, word or
line, each drawing only its clusters with the same layout, position, colour,
fill and outline, so together they draw exactly what the block draws, and
each can be moved, faded, scaled and turned on its own. CreatePieceGroup puts
them in a new DrawingGroup (its children, in order). A gradient fill stays
continuous across pieces (fill geometry is in calibrated units).
DRAW-ON: glyph outlines are paths, so trim them with a PathShape:
    var pen = new PathShape(layout.GetOutline(40, 300), new PathStrokeStyle(2f), Color.SkyBlue) { TrimEnd = 0f };
    session.DrawShape(pen);
    session.Animations.Bind(pen, AnimatedProperty.TrimEnd, new FloatTrack { { 0, 0f }, { 3, 1f } });
(the trim runs over every contour, overlapping ones included; fade a filled
TextBlockShape in afterwards for the classic write-then-fill).

Staggered entrances (Models.DrawingAnimations.BindStagger, StaggerOptions)
-------------------------------------------------------------------------
    IReadOnlyList<AnimationBinding> session.Animations.BindStagger(IReadOnlyList<DrawingElement> elements,
        StaggerOptions options = null)
    StaggerOptions: StartTime (0), Interval (0.05 s), Duration (0.4 s), Easing (CubicOut),
        Order (StaggerOrder.Forward | Reverse | CenterOut), FromOpacity (0), ToOpacity (1),
        FromOffset (PointF, none), FromScale (1), FromRotation (0), AnchorAtCenter (true)
Element k (in Order) starts at StartTime + k x Interval and takes Duration to
go from its "from" state to opacity ToOpacity with no offset, scale 1 and
rotation 0; before its start it holds the "from" state. Opacity is always
bound; Position, Scale and Rotation only when the options change them - and
then the stagger OWNS the element's translation, scale or rotation. With
AnchorAtCenter each element's transform anchor is first moved to the centre
of its own bounds, so it scales and turns in place. Any elements work; text
pieces are the usual case:
    DrawingGroup words = new TextBlockShape(layout, 40, 440, Color.Orange).CreatePieceGroup(TextUnit.Word);
    session.DrawShape(words);
    session.Animations.BindStagger(words.GetChildren(), new StaggerOptions
        { StartTime = 1, Interval = 0.18, Duration = 0.7, Easing = EasingKind.BackOut,
          FromOffset = new PointF(0, 40), FromScale = 0.4f });

Burned-in captions (Captions)
-----------------------------
    CaptionTrack.ParseSrt(string text); CaptionTrack.ParseWebVtt(string text)
    CaptionTrack.Parse(string text)                      // WEBVTT header -> WebVTT, else SRT
    CaptionTrack.Parse(string text, CaptionFormat format); CaptionTrack.Load(string path); DetectFormat(text)
    new CaptionTrack(IEnumerable<CaptionCue> cues)       // sorted by start time
        Cues, Count, EndTime, GetActiveCues(double seconds), GetActiveCueIndices(double seconds)
    new CaptionCue(double startTime, double endTime, string text, string identifier = null)
        { Position = LayoutPosition?, Align = TextAlign?, Style = CaptionStyle, Settings = "..." }
        StartTime, EndTime, Duration, Text, Identifier, IsActiveAt(t)   // shows while StartTime <= t < EndTime
    AnimatedShape session.DrawCaptions(CaptionTrack track, DrawingLayout layout = null, CaptionStyle style = null)
    new CaptionPresenter(CaptionTrack track, FontRegistry fonts, DrawingLayout layout, CaptionStyle style = null)
        DrawingShape GetShapeAt(double seconds); AnimatedShape CreateShape(double initialTime = 0);
        TextLayoutResult GetCueLayout(int cueIndex); Track, Fonts, Layout, Style (a copy)
    CaptionStyle (mutable; Clone()): FamilyName (first font), Size (0 = 4.5% of the layout's short side),
        TextColor (white), TextFill, OutlineColor (none), OutlineWidth (0.08), BackgroundColor (black,
        alpha 176; null = no box), PaddingX (0.45), PaddingY (0.22), CornerRadius (0.25),
        MaxWidthFraction (0.86), Align (Center), LineSpacing (1.1), MaxLines (0), Position (bottom
        centre of the SafeArea), CueSpacing (0.3)  - widths, padding and radii are FRACTIONS OF THE TEXT SIZE
PARSING (shared managed code): cue identifiers, timings ("hh:mm:ss,mmm" or
"[hh:]mm:ss.mmm" - either separator, 1-3 fraction digits), multi-line text;
markup tags (<i>, <b>, <u>, <c.x>, <v Name>, <font ...>) and SRT "{\...}"
overrides are removed; &amp; &lt; &gt; &quot; &apos; &nbsp; &lrm; &rlm; and
numeric references are decoded; WebVTT NOTE / STYLE / REGION blocks are
skipped; the WebVTT settings align: (start/left, center/middle, end/right),
line:N% (the box's top at N% of the frame) and position:N% (its anchor at N%
of the width) are applied, others kept in CaptionCue.Settings. A malformed
timing line throws FormatException naming the line ("Line 7: ...").
DRAWING: each active cue is laid out (wrapped to MaxWidthFraction of its
reference rectangle less the padding), on an optional rounded box hugging
the text, placed with DrawingLayout.PlaceBox at the cue's or the style's
LayoutPosition - by default sitting on the bottom edge of the layout's safe
area, centred. Cues active together that share a position stack into one
column, the earliest on top. An outline is stroked UNDER the fill, so only
OutlineWidth shows outside the glyphs. The presenter caches each cue's
layout and each set of active cues' shapes, so a caption that stays up costs
nothing per frame and does not redraw its layer. DrawCaptions adds
presenter.CreateShape(session.Time) to the active layer (an AnimatedShape,
updated whenever session.Time is set; with no layout, the whole calibrated
drawing without a safe-area guide). Mark it IsSafeAreaChecked to check it.
Everything is a pure function of the time in fonts you registered: frames
render the same in any order and in both packages.
    var layout = new DrawingLayout(new Size(1920, 1080), SafeAreaGuides.BroadcastTitleSafe);
    session.DrawCaptions(CaptionTrack.Load("captions.srt"), layout, new CaptionStyle { Size = 52 });

How the caches treat text blocks and captions
---------------------------------------------
  - A TextBlockShape is a STATIC mark until its reveal, reveal unit,
    softness or stroke placement is set; then it is dynamic (changing it
    redraws its layer once).
  - Captions are an AnimatedShape whose shape changes only when the set of
    active cues does.
  - Layout (TextLayoutEngine) happens once, when you call it - never per
    render. Re-lay out per frame only when the text or width really changes.

PAINTERLY TOOLS: HARD-EDGED MASKS, OWNERSHIP MAPS, BRUSHSTROKES, KEYFRAME PLAYBACK
=================================================================================
(the maps and players are in namespace CodeBrix.Imaging.Drawing.Painting;
BrushStrokeShape in Shapes; ElementAxes in Models; MaskMeaning in the root)

Tools for painterly animation: brushstrokes that each take the colour of the
picture beneath them, bristle textures measured along and across each stroke,
dark seams between strokes, and a painting made every few seconds with the
frames between dissolving. Everything in this section is COMPUTED in shared
managed code - no rasterizer is involved - so both packages produce
BIT-IDENTICAL results (the parity suite compares them exactly). Antialiased
drawing stays the default everywhere; hard edges are an explicit choice.

THE HARD-EDGED PIXEL RULE (one rule, used by everything below)
  A pixel belongs to a shape when its CENTRE (x + 0.5, y + 0.5), in output
  pixels, is inside the shape under the shape's own fill rule (non-zero:
  winding number not 0; even-odd: odd). A centre exactly on an edge follows
  the top-left rule: it is inside on a left or top edge and outside on a right
  or bottom edge (an edge counts for a row when yTop <= cy < yBottom, a
  crossing counts for a centre when crossingX <= cx; horizontal edges never
  count) - so two shapes sharing an edge never both take, or both miss, a
  centre lying on it.
  The shape is its AREA (DrawingShape.GetCoverageAreas, below): curves are
  flattened to within 0.1 output pixel, every contour counts as closed, and an
  outlined shape's area is its outline's thickness turned into a polygon with
  the caps and joins it is drawn with (PathGeometry.ToStrokeOutline). Points
  are mapped once, in double, by the element's transform, its groups'
  transforms, the layer view (camera with parallax, then the layer transform)
  and the export's aspect-fit; an edge from its top end (xt, yt) to its bottom
  end (xb, yb) crosses row cy at x = xt + (cy - yt) * ((xb - xt) / (yb - yt)).
  An element counts when it would draw at the session's Time (inside its time
  window, opacity above 0). Paint plays no part: colours and their alpha,
  fills, effects and masks do not change what an element covers.

Hard-edged masks (masks as exact data)
    DrawingPlane m = session.ExportMask(size, MaskMeaning.HardEdged);   // 0 or 1 per pixel
    session.ExportMask(reusedPlane, MaskMeaning.HardEdged);             // into a plane, every value written
    session.ExportMask(layer, size, MaskMeaning.HardEdged);             // one layer
    renderer.RenderToMask(size, layers, MaskMeaning.HardEdged);
  Like MaskMeaning.Coverage it answers "where is the ink" and ignores the
  layer and session opacities (a layer faded to 0 still shows); layers combine
  as a union. For softer edges that are still identical in both packages,
  export at a multiple of the size you need and average it down:
      session.ExportMask(new Size(w * 2, h * 2), MaskMeaning.HardEdged).Downsampled(2);   // 0, .25, .5, .75, 1

Coverage areas and axes (DrawingShape, Shapes namespace)
    virtual IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
        // the area the shape covers, in its own calibrated units: a point is covered when it is inside ANY of
        // the geometries (each under its own fill rule). Filled shapes: their outline; outlined shapes: the
        // outline's thickness as a polygon (round caps and joins for the standard shapes; PathShape and
        // TextBlockShape use their stroke style, dashes included; both when filled AND outlined). Pictures and
        // sprites: their destination rectangle. Groups: the children's areas through their transforms. The
        // default is null (unknown): a custom shape that does not override it covers nothing.
    virtual ElementAxes? GetAxes()
        // the shape's own frame of reference for brush textures (null by default). Reported by BrushStrokeShape
        // (centre, angle, length, width), LineShape (midpoint, along the line, its length, its thickness),
        // RectangleShape, EllipseShape, CircleShape (centre, along +x, width x height).
    ElementAxes (Models; readonly struct): ElementAxes(PointF center, float angle, float length, float width);
        Center, Angle (DEGREES, clockwise on screen from +x), Length, Width; static FromBounds(RectangleF).

BrushStrokeShape (Shapes) - a tapered, bowed brushstroke
    new BrushStrokeShape(float centerX, float centerY, float length, float width, float angle,
        float bend = 0f, float profile = BrushStrokeShape.DefaultProfile /* 4 */, Color? color = null)
    new BrushStrokeShape(centerX, centerY, length, width, angle, bend, profile, ShapeFill fill)
    session.DrawBrushStroke(centerX, centerY, length, width, angle, bend = 0f, Color? color = null)
    CenterX, CenterY, Length, Width, Angle (degrees), Bend, Profile, GetPoints(), GetOutline(),
    GetBounds(), GetCoverageAreas(...), GetAxes(); const PointsPerSide = 17
  Geometry in the stroke's own axes, s from -0.5 (one end) to +0.5 (the
  other), along = s x Length: the centre line sits Bend x Length x (4 s^2 - 1)
  across the axis (0 at the ends, -Bend x Length at the middle) and the half
  width is Width / 2 x sqrt(max(0, 1 - |2 s|^Profile)) - Profile 2 pointed, 4
  a blunt lozenge, larger squarer. "Across" is (-sin angle, cos angle): to the
  right of the direction of travel on screen (y down). The outline is a
  polygon of PointsPerSide points per side, tips shared. It draws as an
  ordinary anti-aliased filled shape, and in an ownership map it supplies its
  own axes. Building it calls Math.Cos/Sin/Pow/Sqrt (see "what bit-identical
  promises" in the planes section).

Ownership maps - which element owns each pixel
    DrawingOwnershipMap map = session.ExportOwnershipMap(size);          // every layer's elements
    DrawingOwnershipMap map = session.ExportOwnershipMap(layer, size);   // one layer (also: no size = DefaultExportSize)
    renderer.RenderToOwnershipMap(size, layers);
  Each pixel belongs to the LAST element, in painting order, whose area covers
  its centre (the rule above). Elements are numbered 0, 1, 2 ... in painting
  order across the listed layers (bottom layer first, each layer's elements in
  order); every element has a number, even one hidden at the current Time
  (it owns nothing). A group or an animated shape is ONE element (its children
  paint into its number, each child's own time window applying).
    const int DrawingOwnershipMap.Nobody = -1         // a pixel no element covers
    int Width, Height; Size Size; int ElementCount; int UnownedCount
    ReadOnlySpan<int> Owners                          // row-major [y * Width + x]; int this[x, y]; int[] ToArray()
    DrawingElement GetElement(int index); DrawingLayer GetLayer(int index); int IndexOf(DrawingElement)  // -1 if absent
    int GetPixelCount(int index); Rectangle GetBounds(int index)       // Rectangle.Empty when it owns none
    ElementAxes GetAxes(int index)                    // GetAxes(), else FromBounds(bounds), else zeros
    DrawingOwnershipMap FillUnowned()
        // NEW map: every unowned pixel takes the owner of the nearest owned pixel, by EXACT Euclidean distance
        // (integer arithmetic). TIE RULE: among owned pixels at the same smallest distance, the smallest x
        // wins, then the smallest y. A map with nothing owned (or nothing unowned) comes back as itself.
    DrawingPlane CreateBoundaryPlane(float blurSigma = 0f)
        // 1 where a pixel's owner differs from its left or upper neighbour's (Nobody counts as an owner; the
        // first row and column compare only with the neighbours they have), else 0; then GaussianBlur(sigma)
  A map is immutable and safe to read from many threads.

Colour pickup - the colour beneath each element
    Vector4[] means = map.GetMeanColors(DrawingPlaneImage source);               // any size
    Vector4[] means = map.GetMeanColors(ReadOnlySpan<byte> rgba, width, height);  // 8-bit straight RGBA
    map.ApplyColors(DrawingPlaneImage destination, IReadOnlyList<Vector4> colors);  // map's size
    map.ApplyColors(Span<byte> rgba, IReadOnlyList<Color> colors);
    map.ApplyColors(Span<byte> rgba, IReadOnlyList<Vector4> colors);  // floats -> bytes: (byte)(v * 255 + 0.5), clamped
  SAMPLING RULE: each map pixel reads ONE source pixel by the nearest rule of
  DrawingPlaneResampling.Nearest - column floor((2x + 1) * sourceWidth /
  (2 * Width)), likewise rows - so a source at the map's size is read pixel
  for pixel and a half-size one at (x / 2, y / 2). The mean is alpha-weighted
  (transparent source pixels lend no colour): with a = source alpha clamped to
  0..1, colour = sum(C x a) / sum(a) (0 when sum(a) is 0), alpha = sum(a) /
  pixel count; double sums, row by row, left to right, on one thread. Byte
  sources use exact integer sums (colour = sum(C x A) / (255 sum(A)), alpha =
  sum(A) / (255 count)). Colours are straight-alpha Vector4 (X=R, Y=G, Z=B,
  W=A); an element owning no pixel gets transparent black. ApplyColors writes
  each owned pixel's R, G, B and A (straight) and leaves unowned pixels alone.

Brush texture - pixels measured in their owner's own axes
    map.FillByElement(DrawingPlane destination, Func<ElementPixel, float> function, float unownedValue = 0f,
                      Func<int, ElementAxes?> axes = null)
    bool map.TryGetElementPixel(int x, int y, out ElementPixel pixel)
    ElementPixel (readonly struct): X, Y, ElementIndex, Along, Across (double, element units), Axes,
        NormalizedAlong (Along / (Length / 2): -1 .. +1 end to end), NormalizedAcross (Across / (Width / 2))
  Each owned pixel's CENTRE is taken back through its owner's full transform
  into the owner's own coordinates and measured from the axes' centre: Along on
  (cos angle, sin angle), Across on (-sin angle, cos angle). The optional axes
  function overrides an element's axes where it returns a value. Rows run in
  parallel, each value computed once by one thread - results are independent
  of the thread count PROVIDED the function is pure and thread-safe.
    // bristle ridges across each stroke plus a ramp along it; dark seams between strokes
    map.FillByElement(bristle, p => (float)(1 + 0.13 * Math.Sin(p.Across * 0.9 + phase[p.ElementIndex])
                                              + 0.07 * p.NormalizedAlong), 1f);
    DrawingPlane seams = map.CreateBoundaryPlane(1.3f).Multiply(1.6f).Clamp(0f, 1f);

A complete brushstroke painting step (both packages give the same bytes):
    using var strokes = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(w, h) });
    DrawingLayer layer = strokes.AddLayer("Strokes", Color.White);
    foreach (var s in layout) layer.AddShape(new BrushStrokeShape(s.X, s.Y, s.Length, s.Width, s.Angle, s.Bend));
    DrawingOwnershipMap map = strokes.ExportOwnershipMap(layer, new Size(w, h)).FillUnowned();   // once
    Vector4[] colors = map.GetMeanColors(underpainting);              // each frame or keyframe
    var painting = new DrawingPlaneImage(w, h);
    map.ApplyColors(painting, colors);
    painting.Multiply(bristle).BlendColor(Color.FromRgb(5, 8, 40), seams.Clone().Multiply(0.75f));

Keyframe playback - paint every N seconds, dissolve between
    var player = new KeyframePlayer(width, height, interval,
        (int keyframe, double seconds, Span<byte> rgba) => PaintKeyframe(seconds, rgba),   // KeyframePaintCallback
        transition: null /* = DrawingTransition.Dissolve() */, cacheCapacity: KeyframePlayer.DefaultCacheCapacity /* 4 */);
    player.RenderFrame(t, frameRgba);   // blocks until its keyframes exist
    player.Prefetch(t);                 // start painting what t needs, without waiting
    player.KeyframeAt(t); player.PaintCount; player.CachedKeyframeCount; player.Width/Height/FrameBytes/
        Interval/Transition/CacheCapacity; player.IsDisposed; player.Dispose();
    var planes = new PlaneKeyframePlayer(width, height, interval,
        (int keyframe, double seconds, DrawingPlaneImage image) => ..., transition, cacheCapacity);
    planes.RenderFrame(t, destinationImage);   // float images: dissolve before any tone curve
  Keyframe k is painted at time k x interval; the frame at t is the transition
  from keyframe k = floor(t / interval) to k + 1 at u = t / interval - k
  (exactly keyframe k when u is 0). While k and k + 1 are on screen, k + 2 is
  painted ahead. THREADS: the paint function runs on ONE background worker,
  one keyframe at a time - never twice at once, nearest-needed first; keyframes
  no longer needed after a seek are dropped before they start; at most
  cacheCapacity (at least 3) painted keyframes are kept. Every member is
  thread-safe; frames are always exactly what painting on the spot would give,
  after any seek. A paint function that throws makes the RenderFrame that
  needed it throw the same exception, and that keyframe is painted afresh when
  asked for again. Dispose drops queued paintings and waits for the one in
  progress. Never call the player from inside the paint function. Byte
  dissolves and fades through a colour run on bands of rows in parallel (the
  same bytes as one thread); RGBA frames are straight alpha and the transition
  mixes them premultiplied (DrawingTransition rules), so transparency works.

What is NOT offered: hard-edged ORDINARY rendering (an element or layer drawn
without anti-aliasing through the canvas). The backends' own aliased fills do
not follow one shared rule, so the two packages would still differ, and a
shared-code fill inside the renderer's layer caches would disturb the default
path. For flat hard-edged colour use an ownership map's ApplyColors, or
BlendColor through a hard-edged mask.

MOTION ALONG PATHS, COMBINED BINDINGS, FORMATIONS, LAYER VISIBILITY, FRAME CACHE
==============================================================================
(namespaces CodeBrix.Imaging.Drawing.Paths, .Models, .Animation,
.Formations and the root namespace)

Tools for the things an animation does over and over: ask a shape "is this
point inside, how far from the edge?"; move an element along a path while it
rolls, pulses or fades; send a train of elements - the letters of a word -
along one path to settle in place; add a sway on top of a turn; bring many
objects in along natural-looking paths to fill a shape (leaves into
letter-shaped heaps, stars into a face); switch layers on and off; and
render ahead of a moving play head for a live view. Everything here that is
COMPUTED - query answers, positions, rolls, train places, combined values,
formation spots, keys, poses and schedules - is shared managed arithmetic,
identical in both packages. Every motion is a pure function of the time, so
any frame renders the same in any order and on any thread.

Path point queries (Paths)
--------------------------
    bool  PathGeometry.Contains(PointF point, float tolerance = DefaultFlattenTolerance)
    float PathGeometry.DistanceToEdge(PointF point, float tolerance = ...)    // inside or out
    float PathGeometry.GetSignedDistance(PointF point, float tolerance = ...) // negative inside
    PointF PathGeometry.GetNearestPoint(PointF point, float tolerance = ...)   // on the path as drawn
    float PathGeometry.Distance(PathGeometry other, float tolerance = ...)    // 0 when touching/overlapping
    float PathGeometry.Distance(PathGeometry other, PointF otherOffset, float tolerance = ...)
    PathPoint PathGeometry.GetPointAt(float fraction, bool extendBeyondEnds)
    PathPoint PathGeometry.GetPointAtDistance(float distance, bool extendBeyondEnds)
The geometry is flattened once at the tolerance (curves followed within it,
so a curved edge's answers are within the tolerance of the true curve) and
kept with the geometry for the last tolerance asked, so thousands of
questions are cheap. Contains follows the geometry's own fill rule (non-zero
or even-odd) with every contour taken as closed - the area a fill paints; a
point exactly on an edge may fall either way. DistanceToEdge and Distance
measure to that area's edges; GetNearestPoint lies on the contours as DRAWN
(an open contour is not closed). Distance is 0 when either area holds a
corner of the other or their edges cross; the offset form measures as if the
other geometry were moved (the question a spacing search asks over and
over). An empty geometry: Contains false, distances positive infinity,
GetNearestPoint throws InvalidOperationException. Single-precision
arithmetic in a fixed order.
GetPointAt(fraction, extendBeyondEnds: true) continues the path straight
along its first point's direction before 0 and its last point's direction
after 1 (PathPoint.Distance then lies outside 0..length); with false it is
exactly the clamped GetPointAt(fraction).

    // Space two letter shapes 20 units apart at their nearest points
    float lo = 0, hi = 600;
    for (int k = 0; k < 40; k++)
    {
        float mid = (lo + hi) / 2;
        if (left.Distance(right, new PointF(mid, 0)) < 20) lo = mid; else hi = mid;
    }

Travelling along a path: facing, rolling, overshoot (Models)
-------------------------------------------------------------
    AnimationBinding DrawingAnimations.BindAlongPath(DrawingElement element, PathGeometry path,
        KeyframeTrack<float> progress, PathTravelOptions options)
    AnimationBinding DrawingAnimations.BindRollAlongPath(DrawingElement element, PathGeometry path,
        KeyframeTrack<float> progress, float radius, float uprightAtProgress = 1, bool orientToPath = false)
    PathTravelOptions { bool OrientToPath; float RotationOffset; bool ExtendBeyondEnds;
                        float RollRadius (0 = none); float UprightAtProgress (1) }
The element's transform ANCHOR rides the path at the track's fraction of the
length (set the anchor to the wheel's hub first:
element.Transform = element.Transform.WithAnchor(hub)). With a RollRadius it
ROLLS like a wheel: it turns by (progress - UprightAtProgress) x length /
radius radians (shown in degrees) - clockwise when the path heads right with
the ground below it; a negative radius rolls the other way - so it is
upright exactly where it comes to rest. With OrientToPath it also faces along
the path, and the two ADD (a wheel rolling over a hill tips with the slope
and turns with the distance). ExtendBeyondEnds lets progress leave 0..1 and
continue straight past the ends - an overshoot-and-settle is a progress
track that runs past 1 and eases back. For a FloatTrack the roll is computed
key by key on the converted key values, so it equals, bit for bit, a rotation
track you would mirror from the progress track by hand. The defaults
(no options) are exactly BindAlongPath(element, path, progress).

    var progress = new FloatTrack
    {
        { 0, 0f }, { 3, 0.97f, EasingKind.QuadOut },
        { 3.4, 1.03f, EasingKind.SineOut },           // a little past its place ...
        { 3.8, 1f, EasingKind.SineInOut },            // ... and rocking back
    };
    letter.Transform = letter.Transform.WithAnchor(hub);
    session.Animations.BindAlongPath(letter, road, progress,
        new PathTravelOptions { RollRadius = 30, ExtendBeyondEnds = true });
    // pulse and fade on the way: ordinary Scale / Opacity tracks bind independently

Trains along one path, and text that rolls in (Models)
------------------------------------------------------
    PathTrain DrawingAnimations.BindTrainAlongPath(IReadOnlyList<DrawingElement> elements,
        PathGeometry path, KeyframeTrack<float> progress, PathTrainOptions options = null)
    PathTrain DrawingAnimations.BindTextAlongPath(TextBlockShape text, TextUnit unit,
        PathGeometry path, KeyframeTrack<float> progress, PathTrainOptions options = null)
    PathTrainOptions { float Spacing; KeyframeTrack<float> SpacingScale; double Stagger;
        StaggerOrder Order; bool OrientToPath; float RotationOffset; bool ExtendBeyondEnds;
        float RollRadius; bool RollOnBaseline; float UprightAtProgress (1);
        KeyframeTrack<float> ScaleAlongPath, OpacityAlongPath; bool AnchorAtCenter (true);
        bool SettleInPlace }
    PathTrain: Path, Progress, Options (a copy), Cars, Elements, Binding,
        float GetDistance(int index, double seconds), PathPoint GetPoint(int index, double seconds),
        void AddTo(DrawingLayer layer)
    PathTrainCar: Element, Index, Rank (0 leads), Gap, Delay, RollRadius (settable per element)
THE RULE: element k, with rank r in Order (Forward: the first leads; Reverse:
the last leads; CenterOut: the middle), is at distance
progress(t - r x Stagger) x length - r x Spacing x SpacingScale(t) along the
path - so Spacing keeps them apart, SpacingScale opens and closes the gaps
over time, and Stagger makes each follow a moment later. Each anchor (moved
to the centre of its bounds unless AnchorAtCenter is false) rides the path,
or - with SettleInPlace - each element travels a COPY of the path moved so
the path's end lies on its own anchor, and so ends exactly where it was
drawn. Facing, rolling (upright where it rests with the gaps at their final
size), extending, and ScaleAlongPath / OpacityAlongPath (tracks whose "time"
is the element's OWN fraction 0..1 of the path: grow as it arrives, fade in
as it sets off) work per element. Distances are clamped to the path unless
ExtendBeyondEnds is set.
BindTextAlongPath splits the block into pieces (TextBlockShape.CreatePieces;
pure white space is left out) and always settles in place: draw the block
where the words should end up and give a route that ENDS where they arrive.
RollOnBaseline rolls each piece on its own line's baseline (its radius is
the height of its centre above it). For text arriving from the left use
Order = StaggerOrder.Reverse (the last letter leads); with a Spacing, let
SpacingScale end at 0 so the gaps close at rest; with OrientToPath, let the
route end heading right (along the baseline) so the letters arrive upright.

    TextLayoutResult layout = session.LayoutText("Rolling in", 64);
    var word = new TextBlockShape(layout, 600, 420, Color.Orange);
    PathGeometry route = new PathGeometryBuilder().MoveTo(new PointF(-500, 200))
        .CubicTo(new PointF(-300, 500), new PointF(-100, 0), new PointF(0, 0)).Build();
    PathTrain train = session.Animations.BindTextAlongPath(word, TextUnit.Character, route,
        new FloatTrack { { 0, 0f }, { 2.5, 1.02f, EasingKind.CubicOut }, { 3, 1f, EasingKind.SineInOut } },
        new PathTrainOptions { Stagger = 0.08, Order = StaggerOrder.Reverse, RollOnBaseline = true,
                               ExtendBeyondEnds = true });
    train.AddTo(session.ActiveLayer);

Combining bindings: Add and Multiply (Models)
---------------------------------------------
    enum BindingMode { Replace (default), Add, Multiply }
    AnimationBinding Bind(DrawingElement element, AnimatedProperty property, KeyframeTrack<float>|<PointF> track, BindingMode mode)
    AnimationBinding Bind(DrawingElement element, AnimatedProperty property, Func<double, float> valueAt, BindingMode mode = Replace)
    AnimationBinding Bind(DrawingElement element, AnimatedProperty property, Func<double, PointF> valueAt, BindingMode mode = Replace)
    AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<float>|<PointF> track, BindingMode mode)
    AnimationBinding Bind(DrawingCamera camera, AnimatedProperty property, KeyframeTrack<float>|<PointF> track, BindingMode mode)
    BindingMode AnimationBinding.Mode
Every binding made without a mode REPLACES (a later one of the same property
wins), exactly as before. An Add binding adds its value to the property, a
Multiply binding multiplies it, AFTER the others: on each apply, each
property with combining bindings is put back to its resting value (its value
when it was first bound that way), then every replace and callback binding
runs in order, then every Multiply, then every Add - so the result is
(base x factors) + additions WHATEVER ORDER THE BINDINGS WERE MADE IN, and
showing a time twice gives the same values. Combinable: Position (point),
Rotation, Scale (float or point), ScaleX, ScaleY and Opacity (clamped after
combining) of elements and layers; Position (pan), Rotation and Scale (zoom)
of the camera. Other properties throw ArgumentException for Add/Multiply.
The function forms bind a value computed from the time (a wave, a value read
from a cue) - keep them pure functions of the time.

    session.Animations.Bind(tail, AnimatedProperty.Rotation, swayTrack);              // the base
    session.Animations.Bind(tail, AnimatedProperty.Rotation,
        t => 4f * MathF.Sin((float)(t * 9)), BindingMode.Add);                         // a flutter on top
    session.Animations.Bind(badge, AnimatedProperty.Scale, pulseTrack, BindingMode.Multiply);
    session.Animations.Bind(car, AnimatedProperty.Position, shakeTrack, BindingMode.Add); // on top of a path

Keyframes you can derive tracks from (Animation)
------------------------------------------------
    Func<double, double> Keyframe<T>.Curve      // the custom curve; null for a named easing
    Keyframe<T> Keyframe<T>.WithValue(T value)  // same time, easing or curve, and hold
    BoolTrack : KeyframeTrack<bool>             // on/off over time; steps exactly at its keys
WithValue makes a track derived key by key from another one (mirrored,
scaled, offset) whose segments are eased exactly like the original's,
custom curves included.

Layer visibility (Models)
-------------------------
    bool DrawingLayer.IsVisible { get; set; }   // true by default
    AnimationBinding Bind(DrawingLayer layer, AnimatedProperty.IsVisible, KeyframeTrack<bool> track)
A hidden layer is SKIPPED: the live render neither updates its cache nor
composites it - it costs nothing while hidden, where a layer at Opacity 0
still has its cache kept up to date - and exports, plane-image exports,
masks of every meaning, ownership maps and safe-area checks leave it out as
if it were not in the session. Its elements, bindings and cache are kept;
showing it again costs at most one redraw of that layer. A hidden layer
still works as another layer's matte (DrawingMask.FromLayer). Switch layers
with a BoolTrack:
    session.Animations.Bind(titles, AnimatedProperty.IsVisible, new BoolTrack { { 0, false }, { 4, true } });

Formations: many objects coming together to fill a shape (Formations)
--------------------------------------------------------------------
Four mixable pieces - WHERE things come to rest (spots filling a shape, each
with its turn), HOW each gets there (an arrival path, a pure function of the
time that ends EXACTLY in its resting pose), WHEN (a schedule that finishes
exactly on time and tapers), and the session glue (time-keyed elements).

    FormationArea.FromGeometry(PathGeometry geometry, float tolerance = ...)   // a filled area
    FormationArea.FromMask(DrawingPlane mask, RectangleF destination, float threshold = 0.5f)
        Bounds, Geometry (null for a mask), bool Contains(x, y | PointF), float DistanceToEdge(x, y | PointF)
    List<FormationTarget> FormationTargets.Fill(PathGeometry area | FormationArea area, FormationTargetOptions options = null)
    FormationTargetOptions { float Spacing (16); float Jitter (0.33 of the spacing);
                             float EdgeClearance (4); long Seed (1); IArrivalOrder Order (null = random) }
    FormationTarget (struct): PointF Position; float Key (smaller = earlier); float Clearance
SPOTS: a triangular grid (rows 0.866 x Spacing apart from half a row above
the bottom of the bounds, every other row shifted half a spacing), each
point moved at random up to Jitter x Spacing on each axis (seeded), kept
when inside and at least EdgeClearance from the edge; sorted by key. The same
area and options always give the same spots. A mask area is inside where
its pixel is at or above the threshold, and measures distances between pixel
centres (to about a pixel).

    interface IArrivalOrder { void Prepare(FormationArea area, long seed); float GetKey(float x, float y, float clearance); }
    RandomArrivalOrder()                         // seeded random
    CenterOutArrivalOrder(bool reverse = false)  // from the middle out (or the outside in)
    DirectionalArrivalOrder(float angle, float scatter = 0)   // a sweep; 270 = bottom to top
    HeapArrivalOrder(float groundY) { SidewaysCost (1.7), DownCost (2.4), EdgeLag (0.6),
        EdgeLagReach (24), Lumpiness (15), LumpWidth (52), Scatter (6) }
    CustomArrivalOrder(Func<float, float, float, float> key)  // (x, y, clearance) => key
HEAPING (HeapArrivalOrder) grows the shape the way a heap grows - bottom up,
always resting on what is already there: a spot's key is its cheapest climb
through the shape from the ground line (a step up costs its length, sideways
SidewaysCost times, down DownCost times), so a leg rises from its foot and a
crossbar fills from its supports; spots near the edge wait a little (a
rounded top), a slow noise makes the growing top lumpy, and a seeded scatter
breaks ties. Keys are in units of climb, so several letters on one ground
line grow together when their spots are sorted together. An order keeps what
Prepare worked out: use one instance for one fill at a time.

    FormationPose(float x, float y, float rotation = 0, float scaleX = 1, float scaleY = 1)
        X, Y, Position, Rotation (degrees), ScaleX, ScaleY, DrawingTransform ToTransform()
    interface IArrivalPath { FormationPose Target; double StartTime; double ArrivalTime; FormationPose GetPoseAt(double seconds); }
    SwayingFallArrival(DrawingRandom random, FormationPose target, double arrivalTime, float startY,
        Func<double, double> airOffset = null)
    StraightArrival(FormationPose start, FormationPose target, double startTime, double arrivalTime,
        EasingKind easing = CubicOut)
    CurvedArrival(start, target, startTime, arrivalTime, float bend = 0.35f, EasingKind easing = CubicOut)
    SpiralArrival(FormationPose target, double startTime, double arrivalTime, float startRadius,
        float startAngle = 0, float turns = 1.5f, float startScale = 1, bool spinsWithPath = false,
        EasingKind easing = CubicOut)
A pose is for content drawn CENTRED ON (0, 0). Every built-in style returns
EXACTLY its Target at and after ArrivalTime, so a travelling object can be
swapped for a resting one without a jump. The SWAYING FALL is a leaf's,
feather's or paper scrap's: a pendulum swing that sinks fastest through the
bottom of each swing, tilts with it (about one in three tumbles instead),
flips slowly about its long axis, drifts with an optional air movement (the
integral of a wind speed), arrives at the END of a swing with its sinking
eased off, and flutters just before it settles; all its characteristics come
from the random stream you give it, and its StartTime is where it passes
startY going back from the arrival. CURVED bows to the right of the
direction of travel for a positive bend; SPIRAL circles the target, the
circle shrinking to nothing. Implement IArrivalPath for any other style.

    ArrivalSchedule(double firstTime, double lastTime, ArrivalRateProfile profile = null)
    ArrivalSchedule(double firstTime, double lastTime, Func<double, double> rate)
        FirstTime, LastTime, double[] GetTimes(int count)
    ArrivalRateProfile { RampUntil (0.3), TaperFrom (0.72), StartRate (0.06), RampExponent (1.6),
                         TaperExponent (2.1), EndRate (0.012); double GetRate(double fraction) }
A schedule spaces count arrivals by a rate - a ramp up from StartRate, a
hold, a long taper - so the formation completes EXACTLY at lastTime (the
first arrival is at firstTime) without an abrupt stop: arrival k is at the
k-th of count even quantiles of the rate (summed over 4000 steps, read back
by linear interpolation).

    FormationMember(IArrivalPath path)           // travels; arrives at path.ArrivalTime
    FormationMember(double arrivalTime)          // just appears, at rest
        Path, ArrivalTime, StartTime, bool IsTravellingAt(double seconds)
    static List<FormationMember> FormationMember.FromTargets(targets, arrivalTimes,
        Func<FormationTarget, double, int, IArrivalPath> createPath)
    FormationElements DrawingAnimations.BindFormation(IReadOnlyList<FormationMember> members,
        DrawingLayer restingLayer, DrawingLayer travellingLayer,
        Func<int, DrawingElement> createResting, Func<int, IReadOnlyList<DrawingElement>> createTravelling = null)
    FormationElements: Count, Members, ArrivalOrder, Binding, GetResting(i), GetTravelling(i),
        int GetTravellingCount(double seconds)
BindFormation turns members into TIME-KEYED elements in arrival order (a
stable sort by ArrivalTime): member i's resting element (from
createResting(i), already placed where it rests) goes on the resting layer
existing from its arrival on; a travelling member's elements (from
createTravelling(i), drawn centred on (0, 0)) go on the travelling layer
existing only while it travels, and one binding poses them from its path.
Because resting elements appear in the order they were added, the live
render only draws new arrivals while playing forward; seeking anywhere needs
no rebuilding. Keep the travelling layer separate (it redraws while anything
moves).

    // Dots spiral in to spell a word, centre first, all in place by 7 s
    PathGeometry word = session.LayoutText("STARS", 220).GetOutline(300, 260);
    List<FormationTarget> spots = FormationTargets.Fill(word,
        new FormationTargetOptions { Spacing = 9, EdgeClearance = 2, Order = new CenterOutArrivalOrder() });
    double[] times = new ArrivalSchedule(1, 7).GetTimes(spots.Count);
    List<FormationMember> members = FormationMember.FromTargets(spots, times, (spot, time, k) =>
        new SpiralArrival(new FormationPose(spot.Position.X, spot.Position.Y), time - 1.6, time, 400, k * 37));
    DrawingLayer rest = session.AddLayer("Rest", Color.White), air = session.AddLayer("Air", Color.White);
    session.Animations.BindFormation(members, rest, air,
        i => new CircleShape(spots[i].Position.X, spots[i].Position.Y, 3, 1, Color.Gold, isFilled: true),
        i => new DrawingElement[] { new CircleShape(0, 0, 3, 1, Color.Gold, isFilled: true) });

Frame cache for live playback (root namespace)
----------------------------------------------
    FrameCache(AnimationTimeline timeline, Func<DrawingSession> sessionFactory, Size frameSize,
        int capacity = DefaultCapacity, int workerCount = 1) : IDisposable
        Func<int, bool> ShouldRender; int KeepBehind (2); bool IncludeBackground (true);
        Action<DrawingSession, double> PrepareFrame; bool DisposeSessions (true)
        void SetPlayHead(int frame); bool TryGetFrame(int frame, Span<byte> destination);
        bool Contains(int frame); int Count; int PlayHead; int Capacity; int WorkerCount;
        int FrameBytes; Exception Failure; void Dispose()
    FrameSequenceRenderer.KeepSessions (false); int KeptSessionCount; void ReleaseSessions()
For a live view that cannot draw some stretch in real time (a camera move,
a busy stretch): call SetPlayHead every displayed frame and TryGetFrame for
the frame on screen - copy it when ready, draw live when not. WorkerCount
background threads (below normal priority) each build ONE session from the
factory on their own thread, the first time they have work, and keep it for
the cache's life. They render the first Capacity frames at or after the play
head that ShouldRender accepts (every frame when null), nearest first;
moving the play head re-aims at once - frames that left the window are
dropped (one being drawn is finished and discarded), and up to KeepBehind
frames just behind the head stay. At most Capacity + KeepBehind frames are
held (plus one per worker in flight). The frames are exactly what the
FrameSequenceRenderer makes (session Time, PrepareFrame, ExportPixels:
straight-alpha RGBA); write the scene as for parallel rendering. THREADS:
SetPlayHead, TryGetFrame, Contains and the counters may be called from any
thread at any time; ShouldRender is called on any thread (keep it a pure,
cheap function); set the properties before the first SetPlayHead. A worker's
exception stops the rendering and is kept in Failure. Dispose stops and
waits for the workers and disposes their sessions (DisposeSessions).
FrameSequenceRenderer.KeepSessions keeps the sessions a render made for the
next render (the factory is then called only for workers no kept session
serves; renders running at once never share one); ReleaseSessions lets them
go (disposing them when DisposeSessions is set).

    using var cache = new FrameCache(timeline, () => BuildScene(), new Size(1280, 720), capacity: 120, workerCount: 3)
    {
        ShouldRender = f => timeline.TimeOfFrame(f) >= pan.StartTime && timeline.TimeOfFrame(f) <= pan.EndTime,
    };
    // per displayed frame:
    cache.SetPlayHead(frame);
    if (!cache.TryGetFrame(frame, pixels)) { session.Time = playback.Time; session.Render(canvas, info); }

What things cost (live rendering)
---------------------------------
- Element Opacity below 1 draws the element whole into its own offscreen
  layer and fades that: a few faded elements are fine, many animated fades
  per frame are costly. Fading a LAYER (DrawingLayer.Opacity) is only a
  compositing change - nearly free; put things that fade together on their
  own layer and fade the layer.
- A DrawingGroup draws its children together (that is what lets it fade as
  one unit); a group of opaque shapes that only moves together can cost a
  little more than giving each shape the same transform.
- A hidden layer (IsVisible false) costs nothing; a layer at Opacity 0 still
  keeps its cache up to date.
- Exports are always rendered from scratch, and a camera move (or a layer
  transform change) redraws every layer it touches, element by element - the
  live caches are not shifted or reused. For stretches that cannot be drawn
  in real time (a pan over a busy scene), render ahead with a FrameCache.
- Strokes are rebuilt when drawn; for many outlined shapes that move every
  frame, an outline made once (PathGeometry.ToStrokeOutline, filled) is
  cheaper than a stroke.

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

6. A float-colour frame with glowing lights, sparks and a dissolve
------------------------------------------------------------------
    using System;
    using System.Numerics;
    using CodeBrix.Imaging;
    using CodeBrix.Imaging.Drawing;
    using CodeBrix.Imaging.Drawing.Planes;
    using CodeBrix.Imaging.Drawing.Text;

    const int W = 1920, H = 1080;     // seconds = this frame's time; frameB = the next scene's frame

    // a night sky painted per pixel (rows in parallel; the function is pure)
    var scene = new DrawingPlaneImage(W, H).FillParallel((x, y) =>
    {
        float t = y / (float)H;
        return new Vector4(0.02f + 0.05f * t, 0.03f + 0.06f * t, 0.16f + 0.10f * t, 1f);
    });

    // a soft cloud layer from fractal noise, painted through as a mask
    var clouds = DrawingNoise.Fill(new DrawingPlane(W, H), DrawingNoiseKind.Gradient, seed: 7,
        frequency: 0.004, z: seconds * 0.05, octaves: 5).Multiply(0.5f).Add(0.5f).Clamp(0f, 1f);
    scene.BlendColor(Color.FromRgb(90, 100, 140), clouds.Multiply(0.35f));

    // lights may add past 1.0 ...
    var lights = new DrawingPlaneImage(W, H).Fill(0f, 0f, 0f, 1f);
    lights.Fill(new Rectangle(900, 700, 12, 18), Color.FromRgb(255, 200, 90));
    lights.Multiply(4f);
    scene.Add(lights, 0.15f);
    scene.Add(lights.CreateBloom(4, new[] { (3f, 0.8f), (14f, 0.9f) }, 0.6f));

    // a session draws the title and the sparks over transparency; its mask paints a halo
    using var session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(W, H), LayerOpacity = 255 });
    session.Fonts.RegisterFont("assets/Fonts/OpenSans-Regular.ttf");
    session.AddLayer("Title", Color.White);
    session.DrawText("Night", W / 2f, 300, 160, align: TextAlign.Center);
    session.ActiveLayer = session.AddLayer("Sparks", Color.White);
    var sparks = new DrawingParticleSystem(seed: 1);
    sparks.AddEmitter(new DrawingParticleEmitter { X = 906, Y = 700, Rate = 40, Lifetime = 1.5,
        Direction = -90, Spread = 34, Speed = 120, GravityY = 90, StartSize = 6, EndSize = 2 });
    session.DrawParticles(sparks, seconds);
    DrawingPlane halo = session.ExportMask(session.GetLayer("Title"), new Size(W, H)).GaussianBlur(12f);
    scene.BlendColor(Color.FromRgb(255, 210, 140), halo.Multiply(0.6f));
    scene.CompositeOver(session.ExportPlaneImage(new Size(W, H), includeBackground: false));

    // ... and a vignette and tone curve bring them back
    scene.ApplyVignette(W / 2.0, H * 0.48, W * 0.75, H * 0.62, strength: 0.38, minimum: 0.4)
         .ApplyToneCurve(0.15f, 1.08f);

    byte[] frameA = scene.ToRgba();                 // straight-alpha RGBA, ready for a window or ffmpeg
    byte[] frame = new byte[W * H * 4];
    DrawingTransition.Dissolve().Apply(frameA, frameB, frame, W, H, fraction: 0.25f);

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

- Animated scenes: element fades, groups, hidden layers, exports, camera
  moves and the look-ahead FrameCache are covered under "What things cost"
  in MOTION ALONG PATHS, COMBINED BINDINGS, FORMATIONS, LAYER VISIBILITY,
  FRAME CACHE.
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
  session.Render instead of committing shapes every frame - or give them a
  Transform on a layer of their own.
- Animated scenes: keep moving, fading or rebuilt content (transformed
  elements, animated shapes, bound groups) on its own layer, and add
  time-keyed marks in the order they appear - then the static layers and the
  growing ones stay incremental and only the moving layer redraws per frame.
  A camera change redraws every layer whose Parallax is not 0.
- Frame sequences: FrameSequenceRenderer with MaxDegreeOfParallelism above 1
  renders frames on several threads (each with its own session) and still
  delivers them in order.
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
- Referencing the NoSkia companion package
  (CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever) alongside this
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
- Expecting DrawingPlaneImage arithmetic (Add, Multiply, Clamp, tone curve,
  gamma, vignette) to touch alpha: it changes R, G and B only. Change
  coverage through image.A, and use CompositeOver / CompositeAdd /
  BlendColor / BlendImage for alpha-aware painting.
- Forgetting that same-size plane operations work IN PLACE:
  image.GaussianBlur(2f) changes image. Call Clone() first to keep the
  original. Resized / Downsampled / CreateBloom return new objects.
- Writing a straight mix (a * (1 - t) + b * t) between frames that have
  transparency: it darkens towards transparent black. DrawingTransition,
  Lerp and the motion-blur accumulator mix in premultiplied space for exactly
  this reason - use them.
- Calling FillParallel with a function that is not thread-safe or reads
  changing state: the determinism guarantee holds only for pure functions of
  (x, y).
- Passing a Slide or Push destination that is one of the input frames:
  rejected, because those transitions read pixels other than the one being
  written.
- Drawing particles from a stepped simulation of your own: GetParticles(t) is
  already a pure function of time - query it per frame instead of
  accumulating state.
- Expecting a ParticlesShape to move: it holds the particles of the moment it
  was created. Clear the layer and draw a new one for each frame.
- Treating CreateBloom sigmas as full-size pixels: they are in DOWNSAMPLED
  pixels.
- Expecting the default ExportMask to follow LayerOpacity or a fade: it is
  the COVERAGE of the ink itself (the element colour's own alpha included).
  Ask for MaskMeaning.VisibleAlpha when you want the mask as the viewer sees
  it (every opacity, time window, mask and effect applied).
- Expecting anti-aliased masks or drawn frames to match exactly between the
  two packages: only the hard-edged tools (MaskMeaning.HardEdged, ownership
  maps) are computed from geometry; build anything that must be identical on
  them and on plane arithmetic.
- A custom DrawingShape that does not override GetCoverageAreas covers
  nothing in hard-edged masks and ownership maps; a picture or sprite covers
  its whole destination rectangle, whatever its own transparency.
- Indexing per-element data by layer order when the map was made of several
  layers: map indexes run across the layers in order - use map.GetElement /
  map.IndexOf to translate.
- Forgetting FillUnowned before colour pickup: unowned pixels hold
  DrawingOwnershipMap.Nobody and are skipped by ApplyColors (left as they
  were), so gaps between brushstrokes show whatever was underneath.
- Passing radians where an angle is asked for: EVERY angle in this library
  is in DEGREES, clockwise on screen from +x (y down) - transforms, camera
  roll, path points (PathPoint.Angle), arcs, stars, stagger, brushstrokes,
  element axes and particle directions alike, so PathPoint.Angle can be
  passed straight to DrawBrushStroke or a particle Direction. Convert a
  Math.Atan2 result with * 180 / Math.PI.
- A KeyframePlayer paint function that touches state the caller changes
  meanwhile, or calls back into the player: it runs on the player's own
  worker thread, at any time after a request.
- Expecting session.Time to move by itself: it is a value you set (from a
  PlaybackController, a frame loop or the FrameSequenceRenderer). Elements
  with a StartTime after 0 are hidden until you set it.
- Easing on the wrong key: a key's easing shapes the segment ARRIVING at
  that key; the first key's easing is never used.
- Marking a counter's TextShape for the safe-area check inside an
  AnimatedShape factory: the check looks at the elements ON THE LAYER - mark
  the AnimatedShape (or the group) itself.
- Sharing elements, groups, animated shapes or a camera between sessions
  rendered on different threads (for example in a FrameSequenceRenderer
  factory that returns the same scene objects): build a complete scene per
  call; only read shared tracks, timelines, fills and font registries.
- Reading the camera's values as the picture's motion: they describe the
  camera - pan right and the scene slides left.
- Expecting the debug overlay in an export: it is a live-render aid; pass it
  to the export overloads that take a DebugOverlay to bake it into a still.
  With no font registered it silently draws nothing.
- Disposing an ImageSource (or a SpriteSheet that owns its frames) while a
  shape still uses it: the shape silently draws nothing from then on. Shapes
  never dispose their pictures; dispose them after the last render.
- Drawing an element for BindAlongPath somewhere other than around (0, 0)
  without setting its Transform.Anchor: the ANCHOR is what lands on the
  path, so the element appears offset by its own position.
- Expecting a mask to stay put while its layer or group moves: a mask is in
  the masked content's coordinates and moves with it. Use a separate layer,
  or assign a new mask each frame, for a clip fixed to the frame.
- Assuming GetOutline includes the stroke width: it is the geometry (the
  centre line of an outline).
- Reading an effect's blur radius as the Gaussian's sigma: sigma is half the
  radius (the CSS convention), so the visible spread is about the radius.
- Many elements with effects on one busy layer: each costs an offscreen render
  and its blur every time the layer redraws; one DrawingLayer.Effects on the
  whole layer is cheaper.
- Expecting appends to a masked layer (or one with effects) to stay
  incremental: such a layer is redrawn whole whenever it changes.
- Re-laying out text every frame when only its reveal or transform changes:
  lay it out once (TextLayoutEngine is the costly step) and animate the
  TextBlockShape's reveal, the pieces' transforms or the element's opacity.
- Reading a TextBlockShape's (x, y) as a baseline: it is the TOP-LEFT of the
  layout's box (a TextShape's y is its baseline).
- Counting characters by string length for a reveal: reveals count CLUSTERS
  (spaces included; a ligature such as "fi" is one) - use
  layout.ClusterCount / GetUnitCount.
- Stroking outlined lettering with the default (centred) stroke in a font
  with overlapping contours: the overlaps show inside the glyphs; use
  StrokePlacement = TextStrokePlacement.Outside.
- Expecting a stagger to keep an element's own translation, scale or
  rotation: when its options change them, the stagger's tracks set them.
- A caption style changed after creating its presenter (or DrawCaptions):
  the style is copied; build a new presenter for a new style.

WHAT THIS PACKAGE DOES NOT DO
=============================

- No UI controls: it never references a UI framework. You host a SkiaSharp
  view and forward events (the DrawingCanvas trick above is app code).
- No EXIF orientation handling when decoding photos.
- No bitmap-brush strokes: pictures are placed as ImageShape / SpriteShape
  elements, not painted along a stroke.
- No system fonts and no font fallback: text uses only font files you
  register. No hinting, no right-to-left reordering (bidi), no hyphenation
  beyond breaking after hyphens already in the text.
- No video, GIF or APNG assembly: it produces frames (ExportPixels); an
  encoder such as CodeBrix.VideoProcessing assembles them.
- Safe-area presets are advisory figures, not platform guarantees.
- No interactive selection, move or resize of committed elements (the edit
  model is append, undo-last, clear); programmatic animation of committed
  elements is available through their Transform, Opacity and time windows.
- No serialization format: persist strokes yourself from
  DrawingLayer.GetElements()/Stroke.GetPoints() if you need replay or
  storage (StrokePoint.TimeOffsetMs is there for replay).
- No SVG documents: SVG rendering, SVG elements in a session
  (DrawingSvgShape) and the managed rasterizer exist ONLY in the NoSkia
  companion package (AGENT-README-NOSKIA.txt); this package has no SVG
  types at all. It only reads and writes SVG PATH DATA (the d attribute):
  PathGeometry.Parse / TryParse / ToSvgPathData.
- No custom exception types; standard .NET exceptions only.
- A shape's own geometry is immutable: animate it with an AnimatedShape (a
  shape rebuilt from the time) or move it with its Transform (see ANIMATION
  MODEL), or animate a path's trim and morph.
- Path combining, stroke outlines and inflating give POLYGONS (curves
  flattened within a tolerance), never curves; GetOutline stays the centre
  line of an outline. Glyphs built from overlapping contours keep them unless
  you merge them (TextBlockShape.MergeOverlaps, PathGeometry.Simplify) or
  outline them with TextStrokePlacement.Outside; MergeOverlaps merges each
  glyph alone, not neighbouring glyphs with each other.
- Effects and masks are drawn by the session's renderer; a custom Draw that
  calls a group's or animated shape's Draw itself gets neither. No inner
  shadow, bevel, colour-adjustment or blend-mode effects.
- No real-time clock or playback thread: the PlaybackController is a state
  machine you feed with elapsed time; the FrameSequenceRenderer produces frames
  and never encodes them.
- No video, GIF or APNG assembly from the planes tools either: they produce
  frames (raw RGBA bytes, float images, CodeBrix.Imaging images); assemble
  them with a video or image-encoding library.
- No drawing of shapes or text straight into a plane: draw them in a session
  and bring them across with ExportPlaneImage (the drawn frame) or ExportMask
  (its coverage). That step is rasterized by the package's canvas, so its edge
  pixels carry the packages' usual small anti-aliasing difference; everything
  done to it afterwards with planes is bit-identical - or export a
  HARD-EDGED mask (MaskMeaning.HardEdged) or an ownership map, which are
  computed from geometry under one pixel rule and are identical in both
  packages.
- No hard-edged ORDINARY rendering: elements and layers always draw
  anti-aliased through the canvas (see PAINTERLY TOOLS for why, and for the
  hard-edged alternatives).
- Keyframe players call your paint function on their own background worker;
  they do not schedule real time (no clock, no playback thread).
- No colour management: plane values are plain numbers in whatever colour
  space you paint in; ApplyGamma is a plain power curve, not an sRGB transfer
  function.
- No region-limited (dirty-rectangle) redraw: a change on a layer redraws
  that layer, and every visible layer is composited each time anything
  changes. Exports never reuse the live caches, and a camera move redraws the
  layers it touches rather than shifting their pictures; render such
  stretches ahead with a FrameCache for live playback.
- Formations place and move objects; they never decide what an object looks
  like (your factories make the elements) and never simulate physics: every
  arrival is a pure function of the time, worked back from its resting pose.

WORKING EXAMPLES ON GITHUB
==========================

Test suite (xUnit v3; every behavioral guarantee above has a test):
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.Tests
    DrawingSessionTests.cs                 layers, pointer input, undo, clear, export sizes
    DrawingSessionImageFactoryTests.cs     the CreateForImage overloads + CalibrationSizing
    DrawingSessionRawPixelsTests.cs        BGRA raw-pixel factories, mirroring, ownership
    DrawingSessionNormalizedInputTests.cs  PointerPressedNormalized/PointerMovedNormalized
    DrawingSessionShapeTests.cs            Draw* primitives, colors vs layer opacity
    Models/DrawingLayerTests.cs, StrokeTests.cs, StrokePointTests.cs
    Shapes/DrawingShapeTests.cs            shape catalog + custom shapes
    Rendering/DrawingRendererTests.cs      pixel assertions (highlighter guarantee: two
                                           overlapping strokes on one layer == one stroke)
    Rendering/CanvasCalibrationTests.cs    coordinate math
    Extensions/GeometryBridgeExtensionsTests.cs, ImagingBridgeExtensionsTests.cs
    DrawingSessionPixelExportTests.cs      raw RGBA frames (layout, straight alpha)
    DrawingSessionOpacityTests.cs          whole-frame and per-layer fades
    Animation/, Fills/, Text/, SafeAreas/  easing, ramps, gradients, fonts, guides,
                                           checks and overlays
    Shapes/TextShapeTests.cs, Shapes/ShapeBoundsTests.cs
    DrawingSessionAnimationTests.cs        transforms, group fades, time windows, animated
                                           shapes and the camera, live and exported
    DrawingSessionPinnedOutputTests.cs     unanimated output pinned byte for byte
    FrameSequenceRendererTests.cs          in-order and parallel frame sequences
    Animation/, Models/, Layout/,          timelines, tracks, playback, transforms, camera,
    Rendering/DebugOverlayTests.cs         bindings, layout anchors, the debug overlay
    Planes/                                planes, filters, transitions, random, noise,
                                           particles, motion blur (exact values)
    DrawingSessionPlaneExportTests.cs      float-image and mask exports
    Shapes/ParticlesShapeTests.cs          particles drawn as soft dots
    Paths/                                 path geometry, SVG path data, trimming, dashes,
                                           flattening, morphs and stroke styles (exact values)
    Shapes/PathShapeTests.cs, ImageShapeTests.cs, ShapeOutlineTests.cs
                                           path, picture and sprite elements; outlines
    Images/, Effects/                      pictures and sprite sheets; masks and effects,
                                           live and exported
    Models/PathAnimationTests.cs           motion along a path, trim/morph/frame bindings
    DrawingSessionHardEdgedMaskTests.cs    the hard-edged pixel rule, exact pixels
    Painting/                              ownership maps, nearest fill (against brute force),
                                           colour pickup, brush texture, keyframe players
    Shapes/BrushStrokeShapeTests.cs        brushstroke geometry and drawing
    Paths/PathGeometryQueryTests.cs        inside tests, edge distances, nearest points,
                                           area distances, travel past a path's ends
    Models/PathTravelTests.cs, DrawingAnimationsCombineTests.cs
                                           rolling, trains, text along a path; Add/Multiply
    Formations/                            spots, arrival orders, arrival styles, schedules,
                                           the formation binding (live and fresh renders equal)
    DrawingLayerVisibilityTests.cs, FrameCacheTests.cs
                                           hidden layers everywhere; the look-ahead cache

Cross-backend parity (both packages rendering identical scenes; the planes
work is asserted byte for byte in PlaneParityTests.cs, the painterly tools in
PaintingParityTests.cs, the motion and formation tools in
MotionAndFormationParityTests.cs):
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.ParityTests

PainDiagram reference application (one view model behind every head; the
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
    // animation frames (ANIMATION FRAMES section)
    s.ExportPixels(rgbaBuffer, new Size(1920, 1080));   // raw straight-alpha RGBA
    s.Opacity = 0.5f; layer.Opacity = 0.5f;             // group fade / layer fade
    Easing.CubicInOut(t); Easing.Evaluate(EasingKind.BackOut, t); new ColorRamp(c0, c1, c2).At(t);
    s.DrawShape(new CircleShape(cx, cy, r, new RadialGradientFill(center, r, inner, outer)));
    s.Fonts.RegisterFont("Roboto-Regular.ttf"); TextShape t = s.DrawText("Title", x, y, 48, align: TextAlign.Center);
    layer.IsSafeAreaChecked = true; s.CheckSafeArea(SafeAreaGuides.InstagramReelsAd, new Size(1080, 1920));
    s.SafeAreaOverlay = new SafeAreaOverlay(SafeAreaGuides.TikTok);   // live preview only
    // pixel planes (namespace CodeBrix.Imaging.Drawing.Planes) - bit-identical in both packages
    var img = new DrawingPlaneImage(w, h).FillParallel((x, y) => new Vector4(r, g, b, a));  // straight alpha
    img.Add(light, 0.15f).Add(light.CreateBloom(4, new[] { (3f, 0.8f), (14f, 0.9f) }, 0.6f));
    img.GaussianBlur(2f).ApplyVignette(cx, cy, rx, ry, 0.38, 0.4).ApplyToneCurve(0.15f, 1.08f);
    img.CompositeOver(top); img.BlendColor(Color.Gold, maskPlane); img.Lerp(other, 0.5f);
    byte[] rgba = img.ToRgba(); DrawingPlaneImage.FromRgba(rgba, w, h); img.ToImagingImage();
    DrawingPlaneImage drawn = s.ExportPlaneImage(size, false); s.ExportPlaneImage(reusedImage);
    DrawingPlane mask = s.ExportMask(size); s.ExportMask(layer, size);   // coverage 0..1
    s.ExportMask(reusedPlane, MaskMeaning.VisibleAlpha);                 // what shows, into a plane
    // painterly tools (namespace CodeBrix.Imaging.Drawing.Painting) - computed, identical in both packages
    DrawingPlane hard = s.ExportMask(size, MaskMeaning.HardEdged);       // 0/1 by pixel centres
    s.DrawBrushStroke(cx, cy, length, width, angleDegrees, bend);        // BrushStrokeShape
    DrawingOwnershipMap map = s.ExportOwnershipMap(layer, size).FillUnowned();
    map.ApplyColors(painting, map.GetMeanColors(underpainting));         // colour pickup
    map.FillByElement(bristle, p => (float)(1 + 0.1 * Math.Sin(p.Across))); DrawingPlane seams = map.CreateBoundaryPlane(1.3f);
    using var player = new KeyframePlayer(w, h, 2.0, (k, t, rgba) => Paint(t, rgba)); player.RenderFrame(t, frame);
    a.Union(b); a.Subtract(b); g.Simplify(); g.ToStrokeOutline(style); g.Inflate(8f);   // paths as areas
    s.DrawParticles(particleSystem, t, softness: 1f);                     // soft dots, this moment
    DrawingTransition.Wipe(DrawingTransitionDirection.Right, 40f).Apply(a, b, dest, w, h, u);
    new DrawingRandom(seed).Normal(0, 1); DrawingNoise.Fill(plane, DrawingNoiseKind.Gradient, seed, 0.01, z: t * 0.2);
    particleSystem.GetParticles(t); DrawingMotionBlur.Average(subFrames, w, h);
    // animation model (ANIMATION MODEL section)
    var tl = new AnimationTimeline(8, 30); tl.AddCue("in", 0, 1, EasingKind.SmoothStep); tl.Progress("in", t);
    s.Animations.Bind(shape, AnimatedProperty.Position, new PointTrack { { 0, a }, { 2, b, EasingKind.CubicOut } });
    shape.Transform = DrawingTransform.Rotate(45, anchor); shape.Opacity = 0.5f; shape.StartTime = 1.5;
    var g = new DrawingGroup(); g.Add(new CircleShape(...)); g.Opacity = 0.5f; s.DrawShape(g);
    s.DrawShape(new AnimatedShape(t => new CircleShape(cx, cy, 10 + (float)t * 5)));
    s.Camera = new DrawingCamera { Pan = new PointF(100, 0), Zoom = 1.5f }; hudLayer.Parallax = 0;
    s.Time = t; s.ExportPixels(frame, size);                         // show the scene at t
    PointF p = new DrawingLayout(s.CalibrationSize, SafeAreaGuides.TikTok).Place(
        new LayoutPosition(LayoutAnchor.BottomCenter, LayoutReference.SafeArea, fractionY: -0.05f));
    new FrameSequenceRenderer(tl, BuildScene, size) { MaxDegreeOfParallelism = 4 }.Render(0, tl.FrameCount - 1, onFrame);
    var play = new PlaybackController(tl); play.Play(); play.Advance(elapsed); s.Time = play.Time;
    s.DebugOverlay = new DebugOverlay(s.Fonts, tl);                   // live render only
    // paths, pictures, sprites, masks and effects
    PathGeometry g = PathGeometry.Parse("M 0 0 C 50 -50 100 50 150 0"); g.GetLength(); g.GetPointAt(0.5f);
    PathShape p = s.DrawPath(g, 6); s.Animations.Bind(p, AnimatedProperty.TrimEnd, new FloatTrack { { 0, 0f }, { 2, 1f } });
    s.Animations.BindAlongPath(marker, g, progressTrack, orientToPath: true);
    s.DrawShape(new PathShape(new PathMorph(a, b), 0.5f, new PathStrokeStyle(4, dashPattern: new[] { 8f, 4f })));
    using var pic = ImageSource.FromEncoded(pngBytes); s.DrawImage(pic, new RectangleF(10, 10, 200, 120)).Tint = Color.Gold;
    using var sheet = SpriteSheet.FromGrid(pic, 4, 2); s.DrawSprite(sheet, rect, 12, SpritePlayback.PingPong);
    layer.Mask = DrawingMask.Inside(PathGeometry.Circle(c, 100)); group.Mask = DrawingMask.FromLayer(matteLayer);
    shape.Effects = DrawingEffects.DropShadow(new PointF(6, 8), 12, Color.Black).WithGlow(20, Color.Orange);
    // motion along paths, combined bindings, formations, visibility, frame cache
    bool inside = g.Contains(p); float depth = g.DistanceToEdge(p); float gap = a.Distance(b, new PointF(dx, 0));
    s.Animations.BindRollAlongPath(wheel, road, progressTrack, radius: 30, uprightAtProgress: 1, orientToPath: true);
    s.Animations.BindAlongPath(e, road, overshootTrack, new PathTravelOptions { RollRadius = 30, ExtendBeyondEnds = true });
    s.Animations.BindTextAlongPath(word, TextUnit.Character, route, progress, new PathTrainOptions { Stagger = 0.08, Order = StaggerOrder.Reverse, RollOnBaseline = true }).AddTo(layer);
    s.Animations.Bind(tail, AnimatedProperty.Rotation, t => 4f * MathF.Sin((float)t * 9), BindingMode.Add);
    layer.IsVisible = false; s.Animations.Bind(layer, AnimatedProperty.IsVisible, new BoolTrack { { 0, false }, { 4, true } });
    var spots = FormationTargets.Fill(shape, new FormationTargetOptions { Order = new HeapArrivalOrder(groundY) });
    s.Animations.BindFormation(FormationMember.FromTargets(spots, new ArrivalSchedule(1, 9).GetTimes(spots.Count), MakePath), rest, air, MakeResting, MakeTravelling);
    using var cache = new FrameCache(tl, BuildScene, size, capacity: 90, workerCount: 3); cache.SetPlayHead(f); cache.TryGetFrame(f, pixels);
    // text blocks, reveals, captions (TEXT BLOCKS, REVEALS AND CAPTIONS section)
    TextLayoutResult lay = s.LayoutText(text, 28, options: new TextLayoutOptions { MaxWidth = 600, Align = TextAlign.Justify });
    var tb = new TextBlockShape(lay, x, y, Color.White) { RevealUnit = TextUnit.Word, RevealSoftness = 2 }; s.DrawShape(tb);
    s.Animations.Bind(tb, AnimatedProperty.RevealFraction, new FloatTrack { { 0, 0f }, { 3, 1f } });
    s.Animations.BindStagger(tb.CreatePieceGroup(TextUnit.Word).GetChildren(), new StaggerOptions { FromOffset = new PointF(0, 30) });
    new TextBlockShape(lay, x, y, new PathStrokeStyle(6), Color.Black) { StrokePlacement = TextStrokePlacement.Outside };
    s.DrawShape(new PathShape(lay.GetOutlineAlongPath(curve, 10f), fill));   // text on a path
    s.DrawText("AV", x, y, 48, null, TextAlign.Left, 0f, null, kerning: false);
    s.DrawCaptions(CaptionTrack.ParseSrt(srtText), new DrawingLayout(size, SafeAreaGuides.BroadcastTitleSafe), new CaptionStyle { Size = 52 });
    // SkiaSharp-typed twins (core session surface only - see SKIASHARP-FREE BY DEFAULT):
    //   SKColor/SKSizeI/SKSize/SKPoint overloads, Set…(SKColor|SKSizeI) + Get…AsSkia() for
    //   the core properties; everywhere else convert with the bridge extensions
    //   ToSKColor/ToImagingColor, ToSKSizeI/ToImagingSize, ToSKSize/ToImagingSizeF,
    //   ToSKPointI/ToImagingPoint, ToSKPoint/ToImagingPointF, ToSKRect/ToImagingRectangleF.

Rules of thumb: match CalibrationSize to the background aspect (or derive
it); Render once before view-coordinate PointerPressed; opaque fill for JPEG;
one thread for Pointer* calls; the NoSkia companion package must never be
referenced alongside this one.
