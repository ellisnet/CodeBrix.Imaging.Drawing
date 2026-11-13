================================================================================
AGENT-README: CodeBrix.Imaging.Drawing.NoSkia
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever NuGet package
================================================================================

OVERVIEW
========

CodeBrix.Imaging.Drawing.NoSkia is a completely managed drawing stack for
.NET 10 or later with ZERO dependence on SkiaSharp or any native library.
Its only dependencies are the fully managed CodeBrix.Imaging (pixel
buffers, codecs, fonts), CodeBrix.SvgParse (SVG DOM) and CodeBrix.PolygonTools
(polygon clipping) packages. It
provides three things:

- The drop-in drawing-session API of CodeBrix.Imaging.Drawing -
  DrawingSession, DrawingSessionOptions, DrawingLayer, Stroke, all shapes,
  DrawingRenderer, CanvasCalibration, the bridge extensions, exports -
  compiled from the SAME source files as the SkiaSharp-backed package, so
  switching between the two is a package swap - including the
  animation-frame features (raw RGBA frames, easing and color ramps,
  gradient fills, text from registered fonts, shape bounds, whole-frame
  fades, safe-area guides) and the pixel planes (float images, filters,
  transitions, noise, particles, motion blur - bit-identical to the Skia
  package's results) and the animation model (timelines and cues, keyframe
  tracks bound to properties, transforms, groups, time windows, animated
  shapes, camera and parallax, layout anchors, the frame-sequence renderer,
  the playback controller and the debug overlay) and paths, pictures and
  effects (general paths with draw-on trimming, motion along a path and
  morphing, pictures and sprites, clipping and masks, drop shadow, glow and
  blur that follow scale and camera, paths combined as areas, stroke to
  outline and inflate) and text for animation (kerning, laid-out blocks with wrapping,
  alignment and trimming, reveals, per-character/word/line pieces and
  staggers, outlined and drawn-on text, text along a path, and burned-in
  SRT/WebVTT captions - laid out identically in both packages).
- A SkiaSharp-workalike 2D raster drawing engine (DrawingCanvas,
  DrawingBitmap, DrawingPaint, DrawingPath, gradients, blend modes,
  clipping, save-layers), renamed mechanically SK -> Drawing.
- A fully managed SVG renderer (DrawingSvg: SVG -> bitmap/PNG at any scale,
  with explicit font registration so output never depends on system fonts,
  plus the document's display list and scene tree for anything other than
  rasterizing) - and SVG documents as drawing-session elements
  (DrawingSvgShape) whose parts are animated by element id. SVG exists only
  in this package.

This package is a managed workalike for the SkiaSharp drawing API, with no
SkiaSharp dependency - which is why it reads two different ways:

  * If you have never used SkiaSharp, you never need to. No drawing type
    the package exposes is named after another vendor (only the package's
    own name - its NoSkia namespaces and NoSkiaFontRegistry - carries the
    word): the types are
    DrawingCanvas, DrawingPaint, DrawingPath, and the IntelliSense
    documentation talks about drawing rather than about Skia.
  * If you DO think in SkiaSharp names - porting existing code, or simply
    knowing that API and wanting only to be rid of the native dependency -
    switch on the opt-in aliases (OPT-IN SKIA TYPE NAMES, below) and write
    SKCanvas, SKPaint and SKPath against exactly the same types.

Both audiences compile against the same assembly; the setting changes only
what you are allowed to call things.

Choose between the two packages the CodeBrix.Imaging.Drawing repository
produces:

  * Want Skia's performance and the full non-drawing Skia surface?
    -> CodeBrix.Imaging.Drawing.ApacheLicenseForever (+ SkiaSharp);
       guide: AGENT-README-SKIA.txt.
  * Want no Skia/native dependence and can accept slower (still entirely
    correct) CPU rendering?
    -> CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever (THIS file).

Performance is deliberately NOT a goal of this package - typical drawings
render in tens of milliseconds, not Skia's single-digit milliseconds - and
there is no GPU and no on-screen hosting: render offscreen and export.

Provenance: the SVG scene compiler is VENDORED from CodeBrix.SkiaSvg (MIT;
Svg.Skia lineage) with its namespaces renamed to
CodeBrix.Imaging.Drawing.NoSkia.Svg.*; the Skia binding layer is replaced by
a managed backend that replays the compiled display list onto
DrawingCanvas. Do not reference CodeBrix.SkiaSvg or Svg.Skia namespaces
when using this package. The workalike engine itself is original managed
code that mirrors the API SHAPE of SkiaSharp; no Skia source is included.

INSTALLATION
============

NuGet Package: CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever

    dotnet add package CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever

- The package ID carries the ".ApacheLicenseForever" suffix - a permanent
  guarantee that this package ID will only ever be published under the
  Apache-2.0 license. Namespaces do NOT carry the suffix.
- License: Apache-2.0 (the vendored SVG pipeline is MIT-licensed; notices
  ship in THIRD-PARTY-NOTICES.txt inside the package).
- Requires .NET 10 or later.
- Trimming: trim-clean - a fully trimmed publish (PublishTrimmed, and
  Android Release builds) reports no trim warnings for this package.
- NuGet dependencies (restored automatically):
    CodeBrix.Imaging.ApacheLicenseForever
    CodeBrix.SvgParse.MsplLicenseForever
    CodeBrix.PolygonTools.MitLicenseForever   (MIT AND BSL-1.0; the managed,
        dependency-free clipping engine behind path combining, stroke
        outlines and inflating)
- No native libraries of any kind are required on any OS.
- The package carries two assemblies:
    CodeBrix.Imaging.Drawing.NoSkia      - the managed drawing engine and
                                           the drop-in drawing-session API
    CodeBrix.Imaging.Drawing.NoSkia.Svg  - the managed SVG renderer

WHICH ONE DO I REFERENCE
------------------------
  CodeBrix.Imaging.Drawing.ApacheLicenseForever
      Apache-2.0. SkiaSharp-backed original; GPU/on-screen hosting; fast.
      Guide: AGENT-README-SKIA.txt.
  CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever   (THIS file)
      Apache-2.0. Completely managed companion; offscreen rendering and
      export; SVG rendering; slower.

NEVER reference both packages from one application: they compile the SAME
drawing-session source files into the SAME CodeBrix.Imaging.Drawing
namespaces (deliberately, so switching is a package swap).

KEY NAMESPACES / USINGS
=======================

    using CodeBrix.Imaging.Drawing;             // DrawingSession, DrawingSessionOptions, CalibrationSizing,
                                                //   MaskMeaning, FrameSequenceRenderer, FrameCache
    using CodeBrix.Imaging.Drawing.Models;      // DrawingLayer, DrawingElement, Stroke, StrokePoint, ElementAxes,
                                                //   BindingMode, PathTravelOptions, PathTrain(Options), ...
    using CodeBrix.Imaging.Drawing.Shapes;      // DrawingShape + the shape catalog (incl. BrushStrokeShape)
    using CodeBrix.Imaging.Drawing.Rendering;   // DrawingRenderer, CanvasCalibration
    using CodeBrix.Imaging.Drawing.Extensions;  // CodeBrix.Imaging bridge extensions
    using CodeBrix.Imaging.Drawing.Animation;   // Easing, EasingKind, StepPosition, CubicBezierEasing, ColorRamp,
                                                //   AnimationTimeline, TimelineCue, keyframe tracks, PlaybackController
    using CodeBrix.Imaging.Drawing.Layout;      // DrawingLayout, LayoutAnchor, LayoutPosition, LayoutReference
    using CodeBrix.Imaging.Drawing.Fills;       // ShapeFill, GradientFill, Linear/RadialGradientFill, ColorStop, GradientSpread
    using CodeBrix.Imaging.Drawing.Text;        // FontRegistry, TextAlign, TextLayoutEngine, TextLayoutOptions,
                                                //   TextLayoutResult, TextLineMetrics, TextCluster, TextPiece,
                                                //   TextRunDescriptor, TextVerticalAlign, TextTrimming, TextUnit,
                                                //   TextStrokePlacement
    using CodeBrix.Imaging.Drawing.Captions;    // CaptionTrack, CaptionCue, CaptionStyle, CaptionPresenter,
                                                //   CaptionFormat
    using CodeBrix.Imaging.Drawing.SafeAreas;   // SafeAreaGuide(s), SafeAreaOverlay, CheckSafeArea results, ...
    using CodeBrix.Imaging.Drawing.Planes;      // float planes/images, filters, transitions, noise,
                                                //   seeded random, particles, motion blur
    using CodeBrix.Imaging.Drawing.Paths;       // PathGeometry, PathGeometryBuilder, PathMorph, PathStrokeStyle,
                                                //   PathBooleanOperation, ...
    using CodeBrix.Imaging.Drawing.Images;      // ImageSource, SpriteSheet, ImageSampling, SpritePlayback
    using CodeBrix.Imaging.Drawing.Effects;     // DrawingEffects, DrawingMask, DrawingMaskKind, ShadowDirection
    using CodeBrix.Imaging.Drawing.Painting;    // DrawingOwnershipMap, ElementPixel, KeyframePlayer,
                                                //   KeyframePaintCallback, PlaneKeyframePlayer
    using CodeBrix.Imaging.Drawing.Formations;  // FormationArea, FormationTargets, arrival orders and styles,
                                                //   FormationPose, ArrivalSchedule, FormationMember, ...
    using CodeBrix.Imaging.Drawing.NoSkia;      // the workalike engine: DrawingCanvas, DrawingBitmap,
                                                //   DrawingPaint, DrawingPath, DrawingColor(s), ...
    using CodeBrix.Imaging.Drawing.NoSkia.Svg;  // DrawingSvg (managed SVG renderer), DrawingSvgShape (an SVG
                                                //   as a session element) and session.DrawSvg

CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering holds the font registry type
(NoSkiaFontRegistry) that DrawingSvg.Fonts returns; you normally reach it
through that property without a using. That namespace plus
CodeBrix.Imaging.Drawing.NoSkia.Svg are the ENTIRE public surface of the SVG
assembly: DrawingSvg, DrawingSvgNode, DrawingSvgNodeKind, DrawingSvgScene,
DrawingSvgTextEmission, DrawingSvgWarning, DrawingSvgWarningKind,
DrawingSvgShape, DrawingSvgPart, DrawingSvgAspect and
DrawingSvgSessionExtensions in CodeBrix.Imaging.Drawing.NoSkia.Svg, and
NoSkiaFontRegistry in
CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering.
CodeBrix.Imaging.Drawing.NoSkia.Raster (the rasterizer) and the vendored
scene compiler under CodeBrix.Imaging.Drawing.NoSkia.Svg.* are internal, so
there is nothing there to reference even by accident.

As in the Skia package, the CodeBrix.Imaging `Color` type resolves as just
`Color` inside code using these namespaces; add `using CodeBrix.Imaging;`
when your own code lives in an unrelated namespace.

OPT-IN SKIA TYPE NAMES
======================

Off by default. Set one property in the consuming project and every
workalike type gains a second name - its SkiaSharp one:

    <PropertyGroup>
      <CodeBrixUseSkiaTypeNames>true</CodeBrixUseSkiaTypeNames>
    </PropertyGroup>

    // ... and now this compiles, with no using directive of its own:
    var info = new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var bitmap = new SKBitmap(info);
    using var canvas = new SKCanvas(bitmap);

The package ships a build/*.props file that NuGet imports automatically;
the property gates a set of C# global using ALIASES inside it. The
correspondence is the rename map under CORE API REFERENCE, read in the
other direction.

What it does NOT do, and what to watch for:

- It adds NAMES ONLY. The types, the assembly, the namespaces and the
  documentation are unchanged, so code written against the Drawing names
  keeps compiling with the setting on, and vice versa. There is no
  behavioral difference of any kind.
- It is PER-PROJECT, and it is a global using: the aliases apply to every
  file in the project that turns it on, and to no other project.
- NEVER turn it on in a project that also references SkiaSharp. A global
  using alias takes precedence over `using SkiaSharp;`, so SKCanvas, SKPaint,
  SKColor ... silently mean the Drawing types, and the mistake only shows up
  as type-mismatch errors where SkiaSharp APIs are called. (Referencing
  SkiaSharp beside this package is already a mistake for other reasons -
  see COMMON PITFALLS.)
- It does not cover the display-list surface. DrawingCommand, its command
  records, DrawingTextStyle, IDrawingCommandVisitor and
  IDrawingTextOutliner are this package's own design rather than workalikes
  of anything, so they keep their Drawing names either way. The one
  display-list type that IS aliased is DrawingPicture, because SKPicture is
  what it corresponds to.
- Types with no SkiaSharp counterpart likewise keep their Drawing names:
  DrawingShaderKind, DrawingColorFilterKind, DrawingPathSegment,
  DrawingBlendModeExtensions.

CORE API REFERENCE
==================

THE DRAWING-SESSION API (drop-in)
---------------------------------
Everything documented under CORE API REFERENCE in AGENT-README-SKIA.txt
exists identically in this package - DrawingSession, DrawingSessionOptions,
CalibrationSizing, DrawingLayer, DrawingElement, Stroke, StrokePoint,
DrawingShape and every built-in shape, DrawingRenderer, CanvasCalibration, the
bridge extensions, and every export method - compiled from the same (linked)
source files. Read that file for signatures, usage and pitfalls. Differences
a consumer sees when switching:

  1. Where the Skia package's API surfaces SkiaSharp types (SKColor,
     SKBitmap, SKCanvas, SKSizeI, ...), this package surfaces the workalike
     types instead - the mechanical rename is SK -> Drawing. Member NAMES
     follow the types: where the Skia package has GetColorAsSkia(),
     ToSKColor() or GetPointsAsSkia(), this package has GetColorAsDrawing(),
     ToDrawingColor() and GetPointsAsDrawing(). Same members, same order,
     same behavior - only the vendor word is gone, so a mechanical
     search-and-replace of "Skia"/"SK" for "Drawing" ports the call sites.
     The complete correspondence table - which is also exactly what the
     aliases of OPT-IN SKIA TYPE NAMES map, read in the other direction:

       Canvas and surfaces
         SKBitmap             -> DrawingBitmap
         SKCanvas             -> DrawingCanvas
         SKImage              -> DrawingImage
         SKPicture            -> DrawingPicture
         SKSurface            -> DrawingSurface
       Painting
         SKPaint              -> DrawingPaint
         SKPaintStyle         -> DrawingPaintStyle
         SKBlendMode          -> DrawingBlendMode
         SKColorFilter        -> DrawingColorFilter
         SKImageFilter        -> DrawingImageFilter
         SKPathEffect         -> DrawingPathEffect
         SKShader             -> DrawingShader
         SKShaderTileMode     -> DrawingShaderTileMode
         SKStrokeCap          -> DrawingStrokeCap
         SKStrokeJoin         -> DrawingStrokeJoin
       Geometry
         SKPath               -> DrawingPath
         SKPathBuilder        -> DrawingPathBuilder
         SKPathFillType       -> DrawingPathFillType
         SKPathVerb           -> DrawingPathVerb
         SKClipOperation      -> DrawingClipOperation
         SKPoint / SKPointI   -> DrawingPoint / DrawingPointI
         SKRect               -> DrawingRect
         SKSize / SKSizeI     -> DrawingSize / DrawingSizeI
       Pixels and sampling
         SKAlphaType          -> DrawingAlphaType
         SKColor / SKColors   -> DrawingColor / DrawingColors
         SKColorType          -> DrawingColorType
         SKImageInfo          -> DrawingImageInfo
         SKData               -> DrawingData
         SKEncodedImageFormat -> DrawingEncodedImageFormat
         SKCubicResampler     -> DrawingCubicResampler
         SKFilterMode         -> DrawingFilterMode
         SKMipmapMode         -> DrawingMipmapMode
         SKSamplingOptions    -> DrawingSamplingOptions
       Text
         SKFontStyleSlant     -> DrawingFontSlant
         SKFontStyleWeight    -> DrawingFontWeight
         SKFontStyleWidth     -> DrawingFontWidth
         SKTextAlign          -> DrawingTextAlign
  2. Custom shapes override Draw(DrawingCanvas, DrawingColor) instead of
     Draw(SKCanvas, SKColor); the drawing code inside is otherwise identical
     because the canvas API matches SkiaSharp's.
  3. There is no GPU and no on-screen hosting: render offscreen and export
     (ExportPng/ExportJpeg/ExportImagingImage), which is exactly the
     calibrated session model's sweet spot. session.Render(...) takes a
     DrawingSurface/DrawingCanvas plus DrawingImageInfo when you do want to
     drive it yourself.
  4. Performance is deliberately NOT a goal - typical drawings render in
     tens of milliseconds, not Skia's single-digit milliseconds.

Rendering parity with the Skia package is verified by the test suites (see
WORKING EXAMPLES ON GITHUB): identical scenes differ only in anti-aliased
edge pixels (mean channel delta ~0.01/255 across a frame).

ANIMATION FRAMES (drop-in, same sources)
----------------------------------------
The whole ANIMATION FRAMES section of AGENT-README-SKIA.txt applies to this
package unchanged; read it there. In short:
  - session.ExportPixels(Span<byte>, Size, includeBackground) writes a frame
    as plain RGBA bytes: R, G, B, A per pixel, rows top to bottom, no
    padding, STRAIGHT alpha (the Image<Rgba32> layout). DrawingRenderer has
    RenderToPixels. The backend-typed twins take DrawingSizeI.
  - Animation: Easing (every conventional curve, Steps, CubicBezier),
    EasingKind, CubicBezierEasing, ColorRamp (straight-sRGB interpolation).
  - Fills: LinearGradientFill / RadialGradientFill with ColorStop lists, as
    an optional extra on filled circles, ellipses, rectangles, polygons and
    text. Custom shapes get CreateFillPaint(DrawingColor, ShapeFill).
  - Text: TextShape / session.DrawText with fonts registered in
    session.Fonts (a FontRegistry; or share one through
    DrawingSessionOptions.Fonts). This registry is the drawing session's
    own and is separate from DrawingSvg.Fonts (NoSkiaFontRegistry), which
    serves SVG documents; register the font in each one that needs it.
    Text is KERNED when the font carries kerning data (tracked text too);
    kerning: false overloads switch it off.
  - Text blocks, reveals, staggers and captions: the section TEXT BLOCKS,
    REVEALS AND CAPTIONS below (TextLayoutEngine, TextBlockShape,
    BindStagger, CaptionTrack, DrawCaptions).
  - DrawingShape.GetBounds() / Stroke.GetBounds(); session.Opacity and
    DrawingLayer.Opacity fades; SafeAreaGuides presets, CheckSafeArea and
    SafeAreaOverlay (off by default; never in an export unless passed). The
    overlay's default look is a light tint, MaskColor = DefaultMaskColor
    rgba(255, 64, 112, 40), scaled by Opacity (0..1, default 1; drawn at
    round(MaskColor.A * Opacity)), plus a solid line just outside the safe
    rectangle, OutlineColor = DefaultOutlineColor rgba(255, 64, 112, 230),
    OutlineWidth = DefaultOutlineWidth = 2 frame pixels (0 = no line; the
    line is not affected by Opacity; a whole-frame zone gets no line).
    "Now" is one moment: CheckSafeArea evaluates elements AND the guide's
    time rules at session.Time (SafeAreaCheckOptions has only Duration, no
    time); SafeAreaOverlay.Time and DebugOverlay.Time default to null = the
    Time of the session or renderer drawing them. Only the guide's own
    methods (GetUnsafeZones, GetSafeRectangle, SafeAreaTimeRule.AppliesAt)
    treat a missing time as unknown, where every time rule applies.
  - Pixel planes: the whole next section, PIXEL PLANES, FILTERS,
    TRANSITIONS, NOISE, PARTICLES, including session.ExportPlaneImage,
    session.ExportMask and session.DrawParticles.
  - Painterly tools (hard-edged masks, ownership maps, colour pickup, brush
    textures, BrushStrokeShape, keyframe playback): the section PAINTERLY
    TOOLS below - bit-identical to the Skia package.
  - The animation model: the section ANIMATION MODEL below (session.Time,
    session.Animations, transforms, groups, time windows, the camera, layout
    anchors, FrameSequenceRenderer, PlaybackController, DebugOverlay).
  - Paths, pictures, sprites, masks and effects: the section of that name
    below (PathShape, ImageShape, SpriteShape, DrawingMask, DrawingEffects,
    GetOutline on every shape).
PARITY: everything these features COMPUTE in managed code - easing values,
ramp colors, text layout, glyph-outline geometry and widths, shape bounds,
safe-area zones and findings - is identical, value for value, in both
packages (the parity suite asserts it). What is drawn through the canvas
(gradients, glyph fills, fades, masks) matches within the usual
anti-aliased-edge tolerance.

PIXEL PLANES, FILTERS, TRANSITIONS, NOISE, PARTICLES
----------------------------------------------------
(namespace CodeBrix.Imaging.Drawing.Planes; the session bridge and
ParticlesShape are in the root and Shapes namespaces. The same text appears
in AGENT-README-SKIA.txt: the code is shared and names no backend type.)

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
    DrawingPlane ExportMask(Size outputSize, MaskMeaning meaning)   // Coverage | VisibleAlpha
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
----------------------------------------------------------------------------
(namespaces CodeBrix.Imaging.Drawing.Animation, .Models, .Shapes, .Layout,
.Rendering and the root namespace. The same text appears in
AGENT-README-SKIA.txt: the code is shared and names no backend type.)

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
    void Render(int firstFrame, int lastFrame, FrameRenderedCallback onFrame, CancellationToken token = default)
    int  RenderTimeRange(double startTime, double endTime, FrameRenderedCallback onFrame, CancellationToken token = default)
    void Render(int firstFrame, IReadOnlyList<byte[]> destinations, CancellationToken token = default)
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
        static FromDrawingPath(DrawingPath); DrawingPath ToDrawingPath() (caller disposes)
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
    PathShape(PathMorph morph, float fraction, float strokeThickness = 15, Color? color = null, bool isFilled = false)
    PathShape(PathMorph morph, float fraction, PathStrokeStyle strokeStyle, Color? strokeColor = null,
              Color? fillColor = null, ShapeFill fill = null)                // no gradient-only form for a morph
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
TrimStart, TrimEnd, TrimOffset, MorphFraction (fractions clamped to 0..1).
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
PathGeometry.ToStrokeOutline / PathShape.GetStrokeOutline, above). Flatten the result
for polygons.

Pictures (Images.ImageSource, Shapes.ImageShape)
-----------------------------------------------
    ImageSource (IDisposable): static FromEncoded(byte[]), FromFile(path), FromRgba(ReadOnlySpan<byte>, w, h),
        FromBgra(ReadOnlySpan<byte>, w, h), FromImagingImage(Image<Rgba32>), FromPlaneImage(DrawingPlaneImage),
        FromDrawingBitmap(DrawingBitmap) (copied, read as straight alpha); Width, Height, Size, IsDisposed, Crop(Rectangle) (a new source),
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

THE WORKALIKE DRAWING ENGINE (namespace CodeBrix.Imaging.Drawing.NoSkia)
-------------------------------------------------------------------------
Public, SkiaSharp-shaped 2D raster drawing types, renamed SK -> Drawing.
Their members follow the SkiaSharp member names and shapes; consult the
package's IntelliSense XML documentation for exact signatures:

    DrawingBitmap    mutable 32-bit pixel buffer (Rgba8888/Bgra8888,
                     straight alpha internally); GetPixels() pins for raw
                     interop; Decode(byte[]) via CodeBrix.Imaging codecs;
                     ScalePixels via CodeBrix.Imaging resamplers
    DrawingSurface   raster surface (Create/Canvas/Snapshot/Flush);
                     Create(info, IntPtr pixels, int rowBytes) over memory
                     YOU own (a window's or texture's locked pixels): the
                     managed engine draws into its own buffer, reads your
                     pixels in at Create and writes them back on Flush()
                     and Dispose() - call Flush before reading the memory
    DrawingImage     immutable snapshot; Encode to PNG/JPEG/BMP/GIF/WebP;
                     ReadPixels with BGRA/RGBA + premul conversion
    DrawingCanvas    Clear, Save/SaveLayer(paint)/Restore/RestoreToCount,
                     Scale/Translate/RotateDegrees/Concat/SetMatrix
                     (System.Numerics.Matrix3x2, row-vector convention),
                     ClipRect/ClipPath (Intersect/Difference; antialias
                     false by default, as in SkiaSharp - pass true for
                     anti-aliased coverage-mask clipping), DrawLine/Rect/RoundRect/
                     Oval/Circle/Path, DrawBitmap/DrawImage (with
                     source-rect overloads and nearest/bilinear sampling)
    DrawingPaint     Color, Style, StrokeWidth/Cap/Join/Miter, IsAntialias,
                     Shader, ColorFilter, BlendMode, PathEffect (dash),
                     ImageFilter (applied at SaveLayer restore)
    DrawingPath +    move/line/quad/cubic/close verbs, Winding/EvenOdd
    DrawingPathBuilder  fill rules - the current SkiaSharp builder pattern
                     (build with DrawingPathBuilder, then detach the path)
    DrawingShader    CreateColor / CreateLinearGradient /
                     CreateRadialGradient / CreateTwoPointConicalGradient
                     (tile modes Clamp/Repeat/Mirror/Decal, local matrix) /
                     CreatePicture (pattern fill tiled from a DrawingPicture;
                     read back through TileModeX, TileModeY, Picture and
                     TileRect)
    DrawingColorFilter  CreateColorMatrix / CreateTable / CreateBlendMode /
                     CreateLumaColor (SVG luminance masks)
    DrawingBlendMode all 12 Porter-Duff operators + the W3C separable and
                     non-separable blend modes
    value types      DrawingColor(s), DrawingPoint(I), DrawingSize(I),
                     DrawingRect, DrawingImageInfo + the enums in the
                     rename map above

BEHAVIORAL PARITY WITH SKIA
---------------------------
The engine matches SkiaSharp's SHAPE, and in the places where the shape
alone would leave a choice open it matches SkiaSharp's BEHAVIOR too. The
claims worth knowing, because code ported from SkiaSharp depends on them:

- DrawingPaint.StrokeMiter defaults to 4, the same default SkiaSharp uses,
  so a stroke ported without touching the miter limit joins identically.
- Stroke outlines are built as consistently wound polygons and filled with
  a single non-zero fill, so where a stroke's caps, joins and segments
  overlap they composite ONCE. A translucent stroke therefore has an even
  alpha along its whole length, exactly as Skia's does, rather than the
  darkened knuckles a naive per-piece stroker produces.
- DrawingBlendMode carries all 12 Porter-Duff operators and the W3C
  separable and non-separable blend modes, with the same numbering and the
  same formulas Skia uses; Plus and Modulate are neither Porter-Duff nor
  W3C blends, and DrawingBlendModeExtensions.IsPorterDuff() /
  IsSeparableBlend() / IsNonSeparableBlend() classify them accordingly.
- Gradients interpolate in straight (unpremultiplied) sRGB and clamp,
  repeat, mirror or decal outside their range on the same rules; color
  spaces are dropped, because the engine always works in sRGB.
- Sampling is straight-alpha in, straight-alpha out, but bilinear
  interpolation happens in PREMULTIPLIED space, so a transparent texel
  never bleeds a dark fringe into its neighbours - again matching Skia.

Where the packages differ measurably, they differ only at anti-aliased
edges: identical scenes come out with a mean channel delta around
0.01/255 across a frame (asserted by the parity suite).

Rendering internals (namespace CodeBrix.Imaging.Drawing.NoSkia.Raster,
internal): adaptive Bezier flattening, stroke-outline building (caps/joins/
miter/dashes as consistently wound polygons unioned by non-zero fill -
overlapping stroke pieces never double-blend), and an anti-aliased scanline
rasterizer (4x vertical supersampling + exact analytic horizontal span
coverage) compositing straight-alpha with any blend mode, clip mask, and
per-pixel paint source. Heavy lifting is delegated to CodeBrix.Imaging
wherever it exists (codecs, resamplers, Gaussian blur, font parsing); the
rasterizer, stroker, clipping, gradients, and blend stack are original to
this package because CodeBrix.Imaging has no general vector-drawing engine.

THE SVG RENDERER (assembly CodeBrix.Imaging.Drawing.NoSkia.Svg)
---------------------------------------------------------------
DrawingSvg is the WHOLE consumer story for a loaded document - pixels,
display list, scene tree, warnings and fonts - and every type it hands back
is a Drawing type. The vendored scene compiler behind it is internal: there
is no intermediate type to learn, and none to accidentally depend on.

    using CodeBrix.Imaging.Drawing.NoSkia.Svg;

    public sealed class DrawingSvg : IDisposable
    DrawingSvg()
    NoSkiaFontRegistry Fonts { get; }                 // register fonts here BEFORE Load
    DrawingSvgTextEmission TextEmission { get; set; } // set BEFORE Load; default Runs
    bool Load(Stream stream)                          // true when the document loaded
    bool Load(string path)
    bool FromSvg(string svgMarkup)
    bool IsLoaded { get; }
    DrawingPicture Picture { get; }                   // the display list; null before a load
    DrawingSvgScene Scene { get; }                    // the scene tree; null before a load
    DrawingRect Bounds { get; }                       // CSS px at 96 DPI; empty before load;
                                                      //   origin may be non-zero
    SvgUnit DeclaredWidth { get; }                    // what the document ASKED for, in its
    SvgUnit DeclaredHeight { get; }                   //   own unit (e.g. 80 Millimeter)
    IReadOnlyList<DrawingSvgWarning> Warnings { get; } // typed reasons, deduplicated
    void Render(DrawingCanvas canvas)                 // onto your own canvas, at its transform
    DrawingBitmap RasterizeToBitmap(float scale = 1f, DrawingColor? backgroundColor = null)
    byte[] RasterizeToPng(float scale = 1f, DrawingColor? backgroundColor = null)
    void Dispose()

    public sealed record DrawingSvgWarning(DrawingSvgWarningKind Kind, string Message)
    enum DrawingSvgWarningKind                        // UnsupportedFilterPrimitive,
                                                      //   TurbulenceDropped,
                                                      //   GlyphIdTextRunUnsupported,
                                                      //   TextOnPathUnsupported,
                                                      //   NoFontsRegistered
    enum DrawingSvgTextEmission                       // Runs (default), PositionedGlyphs

Load / FromSvg return a bool: false, with the instance left empty (Picture
and Scene null, IsLoaded false), when the markup parses but does not compile
as SVG. A missing file (FileNotFoundException), malformed XML
(XmlException) and a null or blank argument (ArgumentNullException /
ArgumentException) throw instead, and the previously loaded document then
stays in place. Loading a second document replaces everything, warnings
included.

Dispose() is a documented NO-OP. It exists only so that callers can keep
writing `using`, because of the lifetime guarantee below: there is nothing
a disposal could release.

LIFETIME: Picture and Scene stay valid after Dispose(), and stay valid for
as long as you hold them - the outliner the text commands carry keeps the
font registry alive. Hand a DrawingPicture to another component and forget
the DrawingSvg it came from; that is the intended use.

TextEmission decides how text runs are RECORDED, and must be set before the
load that records them:
    Runs             one DrawTextCommand per run, positioned at its origin -
                     compact, and what a text-aware consumer wants.
    PositionedGlyphs every run becomes a DrawPositionedTextCommand with one
                     position per code point, measured through the registry
                     with prefix measurement so kerning survives. Use it when
                     the consumer needs per-glyph placement (laying out a PDF
                     text run, for instance). It renders within the SVG
                     tolerance of Runs, not bit-identically.

Font registry (NoSkiaFontRegistry, reached through DrawingSvg.Fonts):
    string RegisterFont(string path)
    string RegisterFont(string path, string familyNameOverride)
    string RegisterFont(byte[] data)
    string RegisterFont(byte[] data, string familyNameOverride)
    string RegisterFont(Stream stream)
    string RegisterFont(Stream stream, string familyNameOverride)
    IReadOnlyList<string> GetRegisteredFamilyNames()
    bool TryGetFontData(string familyName, out byte[] data,
                        out string resolvedFamilyName)
    int Count { get; }
    // ... plus the parsed CodeBrix.Imaging.Fonts.FontFamily objects themselves,
    // for callers that want to measure or enumerate glyphs directly:
    IReadOnlyList<FontFamily> GetFamilies()
    bool TryFindFamily(string familyName, out FontFamily family)
    bool TryGetFirstFamily(out FontFamily family)
    // RegisterFont returns the family name the font was registered under.

TryGetFontData hands back the EXACT bytes that were registered for a family
(every RegisterFont overload - path, bytes or stream - funnels through one buffer), together with
the family the request actually resolved to - resolution follows the same
fallback rules rendering follows, so what you get is what was drawn with.
That is what lets a consumer embed the very same font file it rendered with
into a PDF, instead of guessing at a file on disk.

Font model: system fonts are NEVER consulted. Register every font file the
document's text needs (by path/bytes/stream, optional family-name override)
BEFORE Load - text is measured at compile time. Unregistered families fall
back to the first registered font; with no fonts registered, text renders as
nothing (safely).

Feature support: shapes, paths (including arcs), transforms, groups,
opacity, viewBox, use/defs/symbols, clipPath, masks (luminance),
linear/radial/focal gradients, <pattern> fills (tiled for real, in both
userSpaceOnUse and objectBoundingBox units, patternTransform included),
dashes, and text (via CodeBrix.Imaging.Fonts glyph outlines) render for
real. Filters are tiered: feGaussianBlur, feOffset, feMerge, feFlood,
feColorMatrix, feComponentTransfer, feBlend, feComposite (incl.
arithmetic), and feImage evaluate fully; exotic primitives (lighting,
displacement, morphology, convolution, turbulence, feTile) degrade
gracefully - the element still renders, minus that effect - and report
through DrawingSvg.Warnings AFTER A RENDER: a filter's primitives are
evaluated at render time, so a filter's warning is not yet in Warnings
immediately after Load. Also degraded: text-on-path and
glyph-id-positioned runs.

THE DISPLAY LIST (DrawingSvg.Picture)
-------------------------------------
DrawingPicture is an immutable display list: the commands the document
compiled down to, plus the CullRect they were recorded against. Replay it
with canvas.DrawPicture(picture), or walk it - that is the point of it
being public - to re-emit the document as something other than pixels
(PDF operators, a plotter path, a text index).

    public sealed class DrawingPicture
    DrawingRect CullRect { get; }
    IReadOnlyList<DrawingCommand> Commands { get; }
    void Accept(IDrawingCommandVisitor visitor)

    // one record per command, all immutable:
    SaveCommand / RestoreCommand / SaveLayerCommand
    SetMatrixCommand(Matrix3x2 Delta, Matrix3x2 Total)
    ClipRectCommand / ClipPathCommand
    DrawPathCommand / DrawImageCommand / DrawPictureCommand
    DrawTextCommand / DrawPositionedTextCommand / DrawTextOnPathCommand

STABILITY GUARANTEE: every method on IDrawingCommandVisitor is a default
interface method that does nothing. A visitor may implement one Visit
overload and ignore the rest, and it keeps compiling when a later release
adds a command kind. Accept() never throws, whatever the visitor does or
does not implement.

VISITORS MUST RECURSE. Accept() walks the TOP LEVEL only. When a visitor
meets a DrawPictureCommand it is the visitor's job to call
command.Picture?.Accept(this) if it wants the nested commands - the
sub-pictures an SVG <use> or a group produces live there. Sub-pictures are
converted once and shared by identity, so a visitor that recurses will meet
the same DrawingPicture instance more than once; key any per-picture work
off reference identity.

COORDINATE SPACE. The picture's own space is CSS pixels at 96 DPI, which is
also what Bounds reports (rounded to whole pixels); DeclaredWidth /
DeclaredHeight tell you what the document asked for in its own unit. A
document's viewBox transform arrives as the FIRST SetMatrixCommand rather
than being baked into the geometry. SetMatrixCommand carries both Delta
(concatenate this onto the current transform - which is what replay does)
and Total (the resulting transform), so a consumer can use whichever suits
it without having to multiply anything back out.

Text commands carry the family the compiler RESOLVED the run to, not the
family the document asked for: the compiler overwrites a paint's typeface
with the one it matched, so DrawingTextStyle.RequestedFamilyName is always
null for SVG-loaded text. Each text command also carries an
IDrawingTextOutliner and exposes GetOutline() (and, for positioned text,
GetOutlines() - one path per code point), which is how replay draws text
without the core assembly knowing anything about fonts.

DrawTextOnPathCommand is recorded but never drawn: replay skips it without
throwing, and the load raises TextOnPathUnsupported.

THE SCENE TREE (DrawingSvg.Scene)
---------------------------------
Read-only. It answers "what element is here" - hit testing, id lookup,
anchors - beside the display list's "what is drawn here". There is no
mutation API of any kind.

    public sealed class DrawingSvgScene
    DrawingSvgNode Root { get; }
    IEnumerable<DrawingSvgNode> Traverse()
    bool TryGetNodeById(string id, out DrawingSvgNode node)
    IEnumerable<DrawingSvgNode> HitTest(DrawingPoint point)
    IEnumerable<DrawingSvgNode> HitTest(DrawingRect rect)
    DrawingSvgNode HitTestTopmost(DrawingPoint point)

    public sealed class DrawingSvgNode
    DrawingSvgNodeKind Kind { get; }        // Fragment, Group, Anchor, Use, Switch,
                                            //   Image, Text, Marker, Path, Shape,
                                            //   Mask, Container, Unknown
    string Id { get; }                      // the element's id attribute
    string ElementName { get; }             // "rect", "path", "a", ...
    SvgElement Element { get; }             // the parsed CodeBrix.SvgParse element
    string Href / Target / Title { get; }   // populated for anchor nodes
    DrawingSvgNode Parent { get; }
    IReadOnlyList<DrawingSvgNode> Children { get; }
    DrawingRect GeometryBounds { get; }     // before this node's transform
    DrawingRect DocumentBounds { get; }     // after it - same space as Picture.CullRect
    Matrix3x2 Transform / TotalTransform { get; }
    bool IsVisible / IsRenderable { get; }

DocumentBounds IS AN AXIS-ALIGNED BOX, and it over-covers. A rotated child
contributes the bounding box of its rotated shape, so a group containing one
is reported larger than the ink inside it. It never under-covers, which is
what makes it safe for hit testing and for sizing a clickable region; do not
read it as a tight outline.

Node wrappers are cached per scene node, so the same element traversed twice
comes back as the same DrawingSvgNode instance and can be compared by
reference.

SVG ELEMENTS AND PER-PART ANIMATION (DrawingSvgShape)
-----------------------------------------------------
A loaded DrawingSvg can be put into a drawing session as an ordinary element
- this package only. It works with everything elements do: transforms,
opacity, groups, time windows, the camera, layout anchors, bounds and
safe-area checks, masks and effects. Its PARTS (every element id in the
document) can be moved, turned, scaled, faded, hidden and their strokes
trimmed at draw time, without changing the loaded document.

    using CodeBrix.Imaging.Drawing.NoSkia.Svg;
    using var svg = new DrawingSvg();
    svg.Fonts.RegisterFont("assets/Fonts/Roboto-Regular.ttf");     // BEFORE loading, as always
    svg.Load("assets/clock.svg");
    DrawingSvgShape clock = session.DrawSvg(svg, new RectangleF(840, 170, 380, 380));   // Meet by default
    clock.GetPart("second").Pivot = new PointF(150, 150);          // document coordinates
    clock.BindPart(session.Animations, "second", AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 60, 360f } });
    clock.BindPart(session.Animations, "ticks", AnimatedProperty.TrimEnd, new FloatTrack { { 0, 0f }, { 2, 1f } });
    session.Animations.Bind(clock, AnimatedProperty.Opacity, new FloatTrack { { 0, 0f }, { 1, 1f } });   // whole element

    new DrawingSvgShape(DrawingSvg svg, RectangleF destination, DrawingSvgAspect aspect = DrawingSvgAspect.Meet)
    session.DrawSvg(svg, destination, aspect = Meet)       // extension method (DrawingSvgSessionExtensions)
    DrawingSvgAspect: Meet (fit inside, centred), Slice (cover, centred, clipped to the destination),
                      Stretch (each axis to the destination), None (1 document unit = 1 calibrated unit)
    Svg, Destination, Aspect, DocumentBounds (svg.Bounds), ContentBounds (where they land, calibrated)
    IReadOnlyList<string> GetPartIds(); bool HasPart(string id); DrawingSvgPart GetPart(string id)
    IReadOnlyList<DrawingSvgPart> Parts; void ResetParts()
    RectangleF? GetPartBounds(string id)    // where the part is drawn now, calibrated units (AABB)
    PointF MapToCalibrated(PointF documentPoint)
    AnimationBinding BindPart(DrawingAnimations animations, string id, AnimatedProperty property, KeyframeTrack<float> track)
        // Rotation, Scale (both axes), ScaleX, ScaleY, Opacity, TrimStart, TrimEnd (0..1 values clamped)
    AnimationBinding BindPart(animations, id, property, KeyframeTrack<PointF> track)
        // Position (translation), Anchor (pivot), Scale (x, y)
    GetBounds(): the fitted document box (Slice: the destination); GetOutline(): that box as a path.
    DrawingSvgPart: Id, Node (DrawingSvgNode), DocumentBounds, Center, Translation, Rotation, ScaleX,
        ScaleY, PointF? Pivot, Opacity (0..1), IsVisible, TrimStart, TrimEnd, DrawingTransform Transform
        (get / set all at once), HasOverrides, Reset()
SPACE: a part's transform is in the DOCUMENT's coordinates (the space of
svg.Bounds and every DrawingSvgNode.DocumentBounds): translation in document
units, rotation (degrees, clockwise on screen) and scale about the Pivot -
by default the centre of each node's own box. It is applied as if the
element were wrapped in an extra group, so the part's own transform, clip,
mask and filter move with it. Visibility hides the part and everything in
it; opacity multiplies the part's own. TrimStart/TrimEnd cut every STROKED
path of the part and its subtree to that fraction of its length (each path
on its own, all at once): draw-on. Fills, images and text are not trimmed.
NESTING: a part inside another animated part rides along with it - its own
override applies inside its ancestors' (pivots stay in document
coordinates), opacities multiply, and the nearest trimmed ancestor wins.
WHAT AN ID REACHES: ids are matched on the compiled scene. An id on content
that a <use> instances is on EVERY instance (each turns about its own centre
unless a Pivot is set); give the <use> element an id of its own to animate
one instance. Mask content is drawn with its overrides (move a mask's shape
to move the window). Content that is not drawn as a scene node - inside
<defs> (except through <use>), <clipPath>, pattern tiles, and filter inputs
such as BackgroundImage - is not affected.
HOW: with no override, the shape draws the loaded picture directly. When a
part changes, the shape re-records the compiled scene with the overrides
applied at the nodes carrying those ids, and converts it to a picture once
(the parts' own drawings are shared with the loaded picture); that picture
is kept until a part changes again. Setting a part makes the shape dynamic,
so its layer redraws. The document is never modified: svg.Picture,
svg.Scene and RasterizeToPng stay exactly as loaded. Re-recordings of one
DrawingSvg are serialized (several shapes or sessions may share it).

Error model
-----------
Standard .NET exceptions only, as in the Skia package (ArgumentNullException
/ ArgumentException / ArgumentOutOfRangeException, ObjectDisposedException,
InvalidOperationException; FormatException for unparseable path data or
caption text; OperationCanceledException from a cancelled frame sequence).
No custom exception types. An SVG that parses but does not compile leaves
DrawingSvg.IsLoaded false (and Picture/Scene null); a missing file or
malformed XML throws (FileNotFoundException / XmlException) - see the SVG
section.

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
        string Ellipsis          "\u2026" by default ("..." for a font without that character)
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
- The managed engine keeps the outlines of recently stroked paths (in the
  path's own coordinates): a stroked path drawn again with the same geometry
  and stroke - only its translation or rotation changed, or nothing at all -
  is filled from the kept outline instead of being stroked again, with
  exactly the same pixels. A change of the path, the stroke or the scale
  strokes it afresh; for many outlined shapes that change every frame, an
  outline made once (PathGeometry.ToStrokeOutline, filled) is cheaper still.

COMPLETE EXAMPLES
=================

1. Rasterize an SVG to PNG (fonts registered explicitly)
--------------------------------------------------------
    using System.IO;
    using CodeBrix.Imaging.Drawing.NoSkia;        // DrawingBitmap, DrawingColors
    using CodeBrix.Imaging.Drawing.NoSkia.Svg;    // DrawingSvg

    using var svg = new DrawingSvg();
    svg.Fonts.RegisterFont(@"path\to\OpenSans-Regular.ttf");   // BEFORE Load
    if (!svg.Load(stream)) { /* not loadable as SVG */ }  // or Load(path) / FromSvg(markup)
    var bounds = svg.Bounds;             // CSS px at 96 DPI; origin may be non-zero
    byte[] png = svg.RasterizeToPng(scale: 2.0f);                       // transparent bg
    using DrawingBitmap bmp = svg.RasterizeToBitmap(2.0f, DrawingColors.White);
    foreach (DrawingSvgWarning w in svg.Warnings) { /* w.Kind, w.Message */ }
    File.WriteAllBytes("out.png", png);

2. Offscreen drawing session, exported without any native library
------------------------------------------------------------------
Identical to the Skia package's examples - only the package reference
differs:

    using System.IO;
    using CodeBrix.Imaging;
    using CodeBrix.Imaging.Drawing;

    var session = DrawingSession.CreateForImage(
        File.ReadAllBytes("photo.jpg"),
        CalibrationSizing.DeriveFromBackgroundImage,
        new DrawingSessionOptions { BackgroundFillColor = Color.White });
    session.AddLayer("Notes", Color.FromRgb(255, 30, 230));
    session.DrawCircle(500, 400, 120, thickness: 20);
    session.DrawArrow(200, 800, 480, 520);
    session.PointerPressedNormalized(0.1f, 0.1f);      // programmatic freehand stroke
    session.PointerMovedNormalized(0.4f, 0.3f);
    session.PointerReleased();
    File.WriteAllBytes("photo_annotated.png", session.ExportPng());   // original resolution

3. Walk the display list instead of rasterizing
-----------------------------------------------
    using System.Collections.Generic;
    using CodeBrix.Imaging.Drawing.NoSkia;        // DrawingPicture + the commands
    using CodeBrix.Imaging.Drawing.NoSkia.Svg;    // DrawingSvg

    // Implements ONE Visit overload; every other command is ignored, and stays
    // ignored when a later release adds a command kind.
    internal sealed class PathCollector : IDrawingCommandVisitor
    {
        public List<DrawingPath> Paths { get; } = new List<DrawingPath>();

        public void Visit(DrawPathCommand command) => Paths.Add(command.Path);

        public void Visit(DrawPictureCommand command)
            => command.Picture?.Accept(this);      // Accept() walks the top level only
    }

    using var svg = new DrawingSvg();
    svg.Fonts.RegisterFont(fontPath);
    if (!svg.Load(svgPath)) { return; }

    var collector = new PathCollector();
    svg.Picture.Accept(collector);                 // Picture stays valid after Dispose()

    // ... and the scene tree answers "what element is at this point"
    DrawingSvgNode hit = svg.Scene.HitTestTopmost(new DrawingPoint(120f, 80f));
    string link = hit?.Href;

4. A float-colour frame with glowing lights, sparks and a dissolve
------------------------------------------------------------------
Identical in both packages, and the frame bytes it produces are identical
too wherever no anti-aliased canvas drawing is involved:

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

A console app that rasterizes an SVG - no UI framework, no native assets:

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever" Version="..." />
      </ItemGroup>
    </Project>

    // Program.cs
    using System;
    using System.IO;
    using CodeBrix.Imaging.Drawing.NoSkia.Svg;

    using var svg = new DrawingSvg();
    svg.Fonts.RegisterFont(args[2]);                 // e.g. OpenSans-Regular.ttf
    if (!svg.Load(args[0])) { Console.Error.WriteLine("Not an SVG"); return; }
    File.WriteAllBytes(args[1], svg.RasterizeToPng(scale: 2f));
    foreach (var w in svg.Warnings) { Console.WriteLine($"degraded: {w.Kind} - {w.Message}"); }

PERFORMANCE TIPS
================

- Animated scenes: element fades, groups, hidden layers, exports, camera
  moves and the look-ahead FrameCache are covered under "What things cost"
  in MOTION ALONG PATHS, COMBINED BINDINGS, FORMATIONS, LAYER VISIBILITY,
  FRAME CACHE.
- Performance is a non-goal: expect tens of milliseconds per rendered
  drawing (CPU-only, managed rasterizer). Render offscreen and export; do
  not build interactive per-frame UIs on this package - use the Skia
  package for that.
- The session's cache model (incremental per-layer bitmaps, composited
  static scene) still applies, so repeated exports of a growing drawing are
  incremental rather than from-scratch on screen; exports themselves are
  full renders at the requested size.
- Output parity with the Skia package is at the anti-aliased-edge level
  (mean channel delta ~0.01/255), so there is no quality reason to prefer
  Skia - only speed and on-screen hosting.
- Register only the fonts an SVG actually needs; every registered font is
  parsed by CodeBrix.Imaging.Fonts at registration.

COMMON PITFALLS TO AVOID
========================

- Referencing this package AND CodeBrix.Imaging.Drawing.ApacheLicenseForever
  in one application: identical types in identical namespaces collide.
  Pick one.
- Registering fonts AFTER Load: text is measured at compile (load) time, so
  fonts registered later are ignored for that document. Register first.
- Expecting system fonts: they are never consulted. An unregistered family
  silently falls back to the first registered font, and with no fonts
  registered at all every text element renders as nothing.
- Expecting on-screen hosting or a GPU: there is none. Use the session's
  export methods (or RasterizeToBitmap/RasterizeToPng) and display the
  result yourself.
- Porting a custom DrawingShape from the Skia package: the override is
  Draw(DrawingCanvas canvas, DrawingColor color); the body is unchanged.
- Assuming every SVG filter renders: exotic primitives, text-on-path and
  glyph-id runs degrade - check DrawingSvg.Warnings, and check it AFTER
  rendering. A filter's primitives are evaluated at render time, so its
  warning is absent from Warnings immediately after Load; the load-time
  warnings (no fonts registered, text on a path, glyph-id runs) are there
  right away.
- Treating Accept() as a deep walk: it visits the TOP LEVEL only. A visitor
  that does not call command.Picture?.Accept(this) from Visit(DrawPictureCommand)
  silently misses everything inside a group or a <use>.
- Reading DrawingSvgNode.DocumentBounds as a tight outline: it is an
  axis-aligned box and over-covers a rotated child.
- Setting TextEmission after Load: text is recorded at load time, so the
  setting applies to the NEXT load. Set it first, like fonts.
- Turning on CodeBrixUseSkiaTypeNames in a project that also references
  SkiaSharp: the aliases silently shadow SkiaSharp's own types.
- Everything under COMMON PITFALLS in AGENT-README-SKIA.txt that concerns
  the session API (calibration aspect, EXIF, JPEG needs opaque fill,
  mirrored coordinates, one thread for Pointer* calls) applies here too.
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
- The animation-model pitfalls in AGENT-README-SKIA.txt apply here too
  (session.Time is set by you; a key's easing shapes the segment arriving at
  it; mark the AnimatedShape for safe-area checks; never share scene objects
  between sessions rendered in parallel; camera values describe the camera;
  the debug overlay is live-only unless passed to an export and needs a
  registered font). Animated scenes cost more here than in the Skia package:
  a moving layer is re-rasterized by the managed engine every frame, so the
  FrameSequenceRenderer's parallel rendering is the usual way to make frames
  quickly.
- The path, picture, mask and effect pitfalls in AGENT-README-SKIA.txt apply
  here too (shapes never dispose their ImageSource; BindAlongPath places the
  element's anchor; masks move with what they mask; GetOutline is the centre
  line; a blur radius is twice the Gaussian's sigma; masked layers and
  layers with effects redraw whole). Effects cost more here: the offscreen
  render and the managed rasterizer dominate, the blur itself is the same
  code in both packages.
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
- Giving an SVG part's pivot or translation in calibrated units: part
  overrides are in the DOCUMENT's coordinates (MapToCalibrated converts).
- Animating an id inside a <use>d element and expecting one instance to
  move: the id is on every instance; give the <use> element its own id.

WHAT THIS PACKAGE DOES NOT DO
=============================

- No GPU rendering and no on-screen hosting of any kind.
- No speed parity with Skia - tens of milliseconds, by design.
- SVG: no exotic filter primitives (lighting, displacement, morphology,
  convolution, turbulence, feTile), no SVG textPath, no
  glyph-id-positioned runs - all degrade gracefully and are reported in
  Warnings. SVG part overrides reach only drawn scene nodes (not clip paths,
  pattern tiles or filter inputs), and a part's trim cuts strokes only.
- No system-font lookup; fonts must be registered explicitly (for SVG in
  DrawingSvg.Fonts, for session text in DrawingSession.Fonts).
- No video, GIF or APNG assembly: the session produces frames
  (ExportPixels); an encoder assembles them.
- No custom exception types.
- No UI controls - the same UI-framework independence as the Skia package.
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
- A shape's own geometry is immutable: animate it with an AnimatedShape or
  move it with its Transform, or animate a path's trim and morph.
- Path combining, stroke outlines and inflating give POLYGONS (curves
  flattened within a tolerance), never curves; GetOutline stays the centre
  line of an outline. Glyphs built from overlapping contours keep them unless
  merged (TextBlockShape.MergeOverlaps per glyph, PathGeometry.Simplify) or
  outlined with TextStrokePlacement.Outside. No font
  fallback, hinting or right-to-left reordering in text layout. Effects and masks
  are drawn by the session's renderer only; no inner shadow, bevel,
  colour-adjustment or blend-mode effects.
- No real-time clock or playback thread: the PlaybackController is a state
  machine you feed with elapsed time; the FrameSequenceRenderer produces
  frames and never encodes them.
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

  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.NoSkia.Tests
      The complete session test suite of the Skia package compiled against
      this package (linked sources), so every behavioral guarantee holds
      here too. On top of that it carries its own authored test files, none
      of which needs Skia present:
        - the engine suite: DrawingPathTests, DrawingShaderTests,
          DrawingShaderPatternTests, DrawingColorFilterTests,
          DrawingBlendModeExtensionsTests, DrawingImageFilterScaleTests,
          SvgFilterScaleTests
        - Pictures/: DrawingCanvasPictureTests, DrawingPictureTests
        - Svg/: DrawingSvgTests, DrawingSvgSceneTests, DrawingSvgSurfaceTests,
          DrawingSvgFixtureTests, DrawingSvgWarningTests,
          DrawingPictureConverterTests, NoSkiaFontRegistryTests
        - StrokeOutlineCacheTests.cs (kept stroke outlines change no pixel)
          and DrawingSurfaceMemoryTests.cs (a surface over caller memory)
        - NoSkiaPublicSurfaceTests.cs, the exported-surface guard
        - SvgReferenceTests.cs - DrawingSvg rendering every sample SVG and
          comparing against the committed reference images.
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.ParityTests
      Both packages side by side: SessionParityTests.cs renders identical
      drawings through both and asserts near-identical pixels;
      PlaneParityTests.cs runs identical pixel-plane work through both and
      asserts byte-for-byte equality; PathParityTests.cs does the same for
      path measuring, trimming, dashing, flattening, morphing and outlines,
      and DrawingFeatureParityTests.cs compares paths, pictures, sprites,
      masks and effects as drawn; PaintingParityTests.cs asserts hard-edged
      masks, ownership maps, colour pickup, brush textures, brushstroke
      outlines and keyframe playback EXACTLY equal;
      MotionAndFormationParityTests.cs asserts path point queries, rolling,
      trains, combined bindings and every formation tool EXACTLY equal;
      SvgParityTests.cs renders every sample SVG through the SkiaSharp SVG
      stack and through DrawingSvg.
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/SvgAssets
      The sample SVGs (basic shapes, paths/curves, gradients, clipping,
      opacity/masking, blur filters, pattern fills, strokes/dashes, text,
      transforms, use/defs, viewBox scaling), their reference PNGs, and the
      OFL Open Sans font used for deterministic text.
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.Tests
      The session API tests as written for the Skia package - the same
      code compiles and passes against this package.

QUICK REFERENCE CARD
====================

    // session API: exactly AGENT-README-SKIA.txt's card, with SK* -> Drawing*
    using CodeBrix.Imaging.Drawing;
    var s = DrawingSession.CreateForImage(bytes, CalibrationSizing.DeriveFromBackgroundImage);
    s.AddLayer("Notes", Color.Red); s.DrawCircle(500, 400, 120); s.PointerPressedNormalized(nx, ny);
    s.PointerMovedNormalized(nx, ny); s.PointerReleased(); byte[] png = s.ExportPng();
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
    // animation model: build once, then show any moment
    s.Animations.Bind(shape, AnimatedProperty.Position, new PointTrack { { 0, a }, { 2, b, EasingKind.CubicOut } });
    shape.Transform = DrawingTransform.Rotate(45, anchor); shape.StartTime = 1.5; s.Camera = new DrawingCamera();
    s.Time = t; s.ExportPixels(frame, size);
    new FrameSequenceRenderer(timeline, BuildScene, size) { MaxDegreeOfParallelism = 4 }.Render(0, last, onFrame);
    // paths, pictures, sprites, masks and effects (same as AGENT-README-SKIA.txt's card)
    PathShape p = s.DrawPath(PathGeometry.Parse(svgPathData), 6); p.TrimEnd = 0.5f;
    s.Animations.BindAlongPath(marker, geometry, progressTrack, orientToPath: true);
    using var pic = ImageSource.FromEncoded(pngBytes); s.DrawImage(pic, new RectangleF(10, 10, 200, 120));
    layer.Mask = DrawingMask.Inside(PathGeometry.Circle(c, 100)); shape.Effects = DrawingEffects.Blur(6);
    // motion along paths, combined bindings, formations, visibility, frame cache (same as AGENT-README-SKIA.txt's card)
    // text blocks, reveals, captions (same as AGENT-README-SKIA.txt's card)
    TextLayoutResult lay = s.LayoutText(text, 28, options: new TextLayoutOptions { MaxWidth = 600, Align = TextAlign.Justify });
    var tb = new TextBlockShape(lay, x, y, Color.White); s.DrawShape(tb); tb.RevealFraction = 0.5f;
    s.Animations.BindStagger(tb.CreatePieceGroup(TextUnit.Word).GetChildren(), new StaggerOptions { FromOffset = new PointF(0, 30) });
    s.DrawCaptions(CaptionTrack.ParseSrt(srtText), new DrawingLayout(size, SafeAreaGuides.BroadcastTitleSafe));
    // workalike engine (SkiaSharp member shapes, SK -> Drawing names)
    using CodeBrix.Imaging.Drawing.NoSkia;      // DrawingSurface, DrawingCanvas, DrawingPaint,
                                                //   DrawingPath(Builder), DrawingBitmap, DrawingImage,
                                                //   DrawingShader, DrawingColorFilter, DrawingBlendMode
    // SVG
    using CodeBrix.Imaging.Drawing.NoSkia.Svg;
    using var svg = new DrawingSvg();
    svg.Fonts.RegisterFont(fontPath);                       // BEFORE Load; system fonts never used
    svg.Load(stream) / svg.Load(path) / svg.FromSvg(markup); // false -> not loadable
    svg.Bounds; svg.DeclaredWidth; svg.Warnings;            // Warnings are typed records
    byte[] png = svg.RasterizeToPng(scale: 2f);             // transparent background
    using DrawingBitmap bmp = svg.RasterizeToBitmap(2f, DrawingColors.White);
    svg.Render(canvas);                                     // onto your own DrawingCanvas
    svg.Picture.Accept(visitor);                            // display list; recurse into
                                                            //   DrawPictureCommand yourself
    svg.Scene.HitTestTopmost(point)?.Href;                  // scene tree, read-only
    DrawingSvgShape e = s.DrawSvg(svg, new RectangleF(0, 0, 600, 400));   // an SVG as a session element
    e.GetPart("hand").Pivot = new PointF(150, 150); e.GetPart("hand").Rotation = 30;   // document units
    e.BindPart(s.Animations, "gear", AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 4, 360f } });
    // opt-in SkiaSharp names (per project, never beside SkiaSharp itself):
    //   <CodeBrixUseSkiaTypeNames>true</CodeBrixUseSkiaTypeNames>

Rules of thumb: one package, never both; fonts before Load; offscreen and
export only; check Warnings for degraded SVG features; Picture and Scene
outlive the DrawingSvg that produced them.
