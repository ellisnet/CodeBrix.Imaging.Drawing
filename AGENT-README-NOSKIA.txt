================================================================================
AGENT-README: CodeBrix.Imaging.Drawing.NoSkia
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever NuGet package
================================================================================

OVERVIEW
========

CodeBrix.Imaging.Drawing.NoSkia is a completely managed drawing stack for
.NET 10 or later with ZERO dependence on SkiaSharp or any native library.
Its only dependencies are the fully managed CodeBrix.Imaging (pixel
buffers, codecs, fonts) and CodeBrix.SvgParse (SVG DOM) packages. It
provides three things:

- The drop-in drawing-session API of CodeBrix.Imaging.Drawing -
  DrawingSession, DrawingSessionOptions, DrawingLayer, Stroke, all shapes,
  DrawingRenderer, CanvasCalibration, the bridge extensions, exports -
  compiled from the SAME source files as the SkiaSharp-backed package, so
  switching between the two is a package swap.
- A SkiaSharp-workalike 2D raster drawing engine (DrawingCanvas,
  DrawingBitmap, DrawingPaint, DrawingPath, gradients, blend modes,
  clipping, save-layers), renamed mechanically SK -> Drawing.
- A fully managed SVG renderer (DrawingSvg: SVG -> bitmap/PNG at any scale,
  with explicit font registration so output never depends on system fonts,
  plus the document's display list and scene tree for anything other than
  rasterizing).

This package is a managed workalike for the SkiaSharp drawing API, with no
SkiaSharp dependency - which is why it reads two different ways:

  * If you have never used SkiaSharp, you never need to. Nothing the
    package exposes is named after another vendor: the types are
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
- NuGet dependencies (restored automatically):
    CodeBrix.Imaging.ApacheLicenseForever
    CodeBrix.SvgParse.MsplLicenseForever
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

    using CodeBrix.Imaging.Drawing;             // DrawingSession, DrawingSessionOptions, CalibrationSizing
    using CodeBrix.Imaging.Drawing.Models;      // DrawingLayer, DrawingElement, Stroke, StrokePoint
    using CodeBrix.Imaging.Drawing.Shapes;      // DrawingShape + the shape catalog
    using CodeBrix.Imaging.Drawing.Rendering;   // DrawingRenderer, CanvasCalibration
    using CodeBrix.Imaging.Drawing.Extensions;  // CodeBrix.Imaging bridge extensions
    using CodeBrix.Imaging.Drawing.NoSkia;      // the workalike engine: DrawingCanvas, DrawingBitmap,
                                                //   DrawingPaint, DrawingPath, DrawingColor(s), ...
    using CodeBrix.Imaging.Drawing.NoSkia.Svg;  // DrawingSvg (managed SVG renderer)

CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering holds the font registry type
(NoSkiaFontRegistry) that DrawingSvg.Fonts returns; you normally reach it
through that property without a using. Those two namespaces plus
CodeBrix.Imaging.Drawing.NoSkia.Svg are the ENTIRE public surface of the SVG
assembly - eight types. CodeBrix.Imaging.Drawing.NoSkia.Raster (the
rasterizer) and the vendored scene compiler under
CodeBrix.Imaging.Drawing.NoSkia.Svg.* are internal, so there is nothing
there to reference even by accident.

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
- NEVER turn it on in a project that also references SkiaSharp. Both would
  define the same names and every use becomes ambiguous. (Referencing
  SkiaSharp beside this package is already a mistake for other reasons -
  see COMMON PITFALLS - and this makes it a compile error.)
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
DrawingShape and the six shapes, DrawingRenderer, CanvasCalibration, the
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

THE WORKALIKE DRAWING ENGINE (namespace CodeBrix.Imaging.Drawing.NoSkia)
-------------------------------------------------------------------------
Public, SkiaSharp-shaped 2D raster drawing types, renamed SK -> Drawing.
Their members follow the SkiaSharp member names and shapes; consult the
package's IntelliSense XML documentation for exact signatures:

    DrawingBitmap    mutable 32-bit pixel buffer (Rgba8888/Bgra8888,
                     straight alpha internally); GetPixels() pins for raw
                     interop; Decode(byte[]) via CodeBrix.Imaging codecs;
                     ScalePixels via CodeBrix.Imaging resamplers
    DrawingSurface   raster surface (Create/Canvas/Snapshot)
    DrawingImage     immutable snapshot; Encode to PNG/JPEG/BMP/GIF/WebP;
                     ReadPixels with BGRA/RGBA + premul conversion
    DrawingCanvas    Clear, Save/SaveLayer(paint)/Restore/RestoreToCount,
                     Scale/Translate/RotateDegrees/Concat/SetMatrix
                     (System.Numerics.Matrix3x2, row-vector convention),
                     ClipRect/ClipPath (Intersect/Difference, antialiased
                     coverage-mask clipping), DrawLine/Rect/RoundRect/
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
                     (tile modes Clamp/Repeat/Mirror/Decal, local matrix)
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

Load / FromSvg return a bool, and a FAILED load leaves the instance empty
(Picture and Scene null, IsLoaded false) rather than throwing. Loading a
second document replaces everything, warnings included.

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
(all three RegisterFont overloads funnel through one buffer), together with
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
through DrawingSvg.Warnings. Also degraded: text-on-path and
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

Error model
-----------
Standard .NET exceptions only, as in the Skia package (ArgumentNullException
/ ArgumentException / ArgumentOutOfRangeException, ObjectDisposedException,
InvalidOperationException). No custom exception types. An SVG that cannot be
loaded leaves DrawingSvg.IsLoaded false (and Picture/Scene null) rather than
throwing.

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
  glyph-id runs degrade - check DrawingSvg.Warnings.
- Treating Accept() as a deep walk: it visits the TOP LEVEL only. A visitor
  that does not call command.Picture?.Accept(this) from Visit(DrawPictureCommand)
  silently misses everything inside a group or a <use>.
- Reading DrawingSvgNode.DocumentBounds as a tight outline: it is an
  axis-aligned box and over-covers a rotated child.
- Setting TextEmission after Load: text is recorded at load time, so the
  setting applies to the NEXT load. Set it first, like fonts.
- Turning on CodeBrixUseSkiaTypeNames in a project that also references
  SkiaSharp: every aliased name becomes ambiguous.
- Everything under COMMON PITFALLS in AGENT-README-SKIA.txt that concerns
  the session API (calibration aspect, EXIF, JPEG needs opaque fill,
  mirrored coordinates, one thread for Pointer* calls) applies here too.

WHAT THIS PACKAGE DOES NOT DO
=============================

- No GPU rendering and no on-screen hosting of any kind.
- No speed parity with Skia - tens of milliseconds, by design.
- SVG: no exotic filter primitives (lighting, displacement, morphology,
  convolution, turbulence, feTile), no text-on-path, no
  glyph-id-positioned runs - all degrade gracefully and are reported in
  Warnings.
- No system-font lookup; fonts must be registered explicitly.
- No custom exception types.
- No UI controls - the same UI-framework independence as the Skia package.

WORKING EXAMPLES ON GITHUB
==========================

  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.NoSkia.Tests
      The complete session test suite of the Skia package compiled against
      this package (linked sources), so every behavioral guarantee holds
      here too; plus SvgReferenceTests.cs - DrawingSvg rendering every
      sample SVG and comparing against committed reference images, with no
      Skia present.
  https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.ParityTests
      Both packages side by side: SessionParityTests.cs renders identical
      drawings through both and asserts near-identical pixels;
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
    // opt-in SkiaSharp names (per project, never beside SkiaSharp itself):
    //   <CodeBrixUseSkiaTypeNames>true</CodeBrixUseSkiaTypeNames>

Rules of thumb: one package, never both; fonts before Load; offscreen and
export only; check Warnings for degraded SVG features; Picture and Scene
outlive the DrawingSvg that produced them.
