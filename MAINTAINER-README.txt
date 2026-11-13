================================================================================
MAINTAINER-README: CodeBrix.Imaging.Drawing
Notes for people and agents MAINTAINING this repository — not for package consumers
================================================================================

If you are consuming one of this repository's NuGet packages, read the
AGENT-README file for that package instead (see README-INDEX.txt). Everything
below is about building, testing, packaging and evolving the repository
itself.

PURPOSE AND SCOPE
=================

The repository produces TWO published NuGet packages out of FOUR projects
under src/. The two packages are deliberate EITHER/OR alternatives: they
compile the same drawing-session source files into the same
CodeBrix.Imaging.Drawing namespaces, so an application references one or the
other, never both.

  CodeBrix.Imaging.Drawing.ApacheLicenseForever
      Packed by      src/CodeBrix.Imaging.Drawing/CodeBrix.Imaging.Drawing.csproj
      Assembly       CodeBrix.Imaging.Drawing
      Backend        SkiaSharp
      Consumer guide AGENT-README-SKIA.txt

  CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever
      Packed by      src/CodeBrix.Imaging.Drawing.NoSkia.Package/
                     CodeBrix.Imaging.Drawing.NoSkia.Package.csproj
      Assemblies     CodeBrix.Imaging.Drawing.NoSkia
                     CodeBrix.Imaging.Drawing.NoSkia.Svg
      Backend        fully managed (no native libraries at all)
      Consumer guide AGENT-README-NOSKIA.txt

Both packages are published on nuget.org.

REPOSITORY LAYOUT
=================

src/CodeBrix.Imaging.Drawing/          the SkiaSharp-backed library; the
                                       CANONICAL copy of the drawing-session
                                       sources (the NoSkia project links them)
  DrawingSession.cs                    main entry point (root namespace)
  DrawingSessionOptions.cs             session construction options
  FrameSequenceRenderer.cs             frames N..M of a timeline-driven scene,
                                       optionally on several threads (and the
                                       FrameRenderedCallback delegate); its
                                       KeepSessions pool reuses sessions
  FrameCache.cs                        renders ahead of a moving play head on
                                       background workers (one kept session
                                       each) into a bounded buffer
  CalibrationSizing.cs                 explicit factory sizing choice (enum)
  MaskMeaning.cs                       what an exported mask measures:
                                       coverage or visible alpha (enum)
  InternalsVisibleTo.cs                grants internals to BOTH test
                                       assemblies (CodeBrix.Imaging.Drawing.Tests
                                       and CodeBrix.Imaging.Drawing.NoSkia.Tests,
                                       because the same sources compile into both)
  Models/                              DrawingElement, Stroke, StrokePoint,
                                       DrawingLayer, and the animation model's
                                       DrawingTransform, DrawingCamera,
                                       DrawingAnimations, AnimationBinding,
                                       AnimatedProperty, StaggerOptions,
                                       StaggerOrder, ElementAxes, BindingMode,
                                       PathTravelOptions, PathTrain(+Car,
                                       Options), and the internal AngleUnits
                                       (the one degrees/radians conversion)
  Shapes/                              DrawingShape + Line/Arrow/Circle/Ellipse/
                                       Rectangle/Polyline/Text/Particles shapes,
                                       DrawingGroup, AnimatedShape, PathShape,
                                       ImageShape, SpriteShape, TextBlockShape,
                                       BrushStrokeShape
  Paths/                               PathGeometry (+Builder, Contour, Segment,
                                       Point, Polyline, FillRule), PathMorph,
                                       PathStrokeStyle (+Cap, Join),
                                       PathBooleanOperation, the internal
                                       PathMath (Bezier arithmetic, Wang's
                                       formula, de Casteljau),
                                       SvgPathDataParser and PathClipper (path
                                       combining, simplify, stroke-to-outline
                                       and inflate through CodeBrix.PolygonTools
                                       - see PATH COMBINING below), and
                                       PathAreaQuery (the point queries: a
                                       geometry flattened once, kept per
                                       tolerance, float arithmetic). Geometry in
                                       shared managed double arithmetic; the
                                       only backend types are the path
                                       converters (ToBackendPath and the
                                       From/To escape hatches)
  Images/                              ImageSource (pictures, stored as
                                       unpremultiplied backend bitmaps),
                                       SpriteSheet, ImageSampling, SpritePlayback
  Effects/                             DrawingEffects (shadow/glow/blur values),
                                       ShadowDirection and DrawingMask (+Kind)
  Fills/                               ShapeFill, GradientFill, Linear/Radial
                                       gradient fills, ColorStop, GradientSpread
  Animation/                           Easing, EasingKind, StepPosition,
                                       CubicBezierEasing, ColorRamp,
                                       AnimationTimeline, TimelineCue,
                                       Keyframe, KeyframeTrack and the
                                       Float/Point/Color/Bool tracks, TrackLoopMode,
                                       PlaybackController (no session
                                       knowledge)
  Layout/                              DrawingLayout, LayoutAnchor,
                                       LayoutPosition, LayoutReference
  Text/                                FontRegistry, TextAlign, the text-block
                                       engine (TextLayoutEngine, its options,
                                       run descriptors and result types:
                                       lines, clusters, pieces), the text
                                       enums, and the internals TextShaping
                                       (one run shaped by CodeBrix.Imaging.Fonts,
                                       mapped back to text clusters) and
                                       TextOutline (single-line TextShape
                                       layout, glyph outline storage)
  Captions/                            CaptionTrack/Cue/Style/Format, the
                                       internal SRT/WebVTT CaptionParser, and
                                       CaptionPresenter (cues -> text blocks
                                       on boxes, placed with DrawingLayout)
  SafeAreas/                           safe-area guides, presets, check results
                                       and the preview overlay
  Rendering/                           DrawingRenderer, CanvasCalibration,
                                       DebugOverlay(+Corner), and the internal
                                       ElementPainter (draws transformed,
                                       faded, grouped, masked and animated
                                       elements, and elements with effects),
                                       EffectProcessor (shadow/glow/blur from
                                       read-back pixels via the planes
                                       arithmetic), PaintContext, and the
                                       hard-edged pair: HardEdgeRasterizer
                                       (THE pixel rule, plus the double
                                       HardEdgeMatrix) and CoverageCollector
                                       (elements -> areas in output pixels)
  Painting/                            the painterly tools: DrawingOwnershipMap
                                       (with the exact nearest fill),
                                       ElementPixel, KeyframePlayer,
                                       KeyframePaintCallback,
                                       PlaneKeyframePlayer and their internal
                                       KeyframeCache (one background worker,
                                       bounded cache) - shared managed code
                                       on Planes types; no backend type
  Formations/                          many objects filling a shape: FormationArea
                                       (geometry or mask; the internal
                                       MaskDistance exact distance transform),
                                       FormationTargets and options, the
                                       IArrivalOrder family, FormationPose, the
                                       IArrivalPath styles (ArrivalMath holds
                                       their shared checks), ArrivalSchedule +
                                       ArrivalRateProfile, FormationMember and
                                       FormationElements (made by
                                       DrawingAnimations.BindFormation)
  Extensions/                          ImagingBridgeExtensions,
                                       ColorBridgeExtensions,
                                       GeometryBridgeExtensions
  Planes/                              float planes and images (DrawingPlane,
                                       DrawingPlaneImage), their filters,
                                       DrawingTransition, DrawingRandom,
                                       DrawingNoise, the particle system and
                                       the motion-blur accumulator;
                                       PlaneKernels holds the shared internal
                                       arithmetic. Pure managed code on
                                       CodeBrix.Imaging value types - it must
                                       never reference SkiaSharp or the NoSkia
                                       engine, which is what makes its results
                                       bit-identical in both packages

src/CodeBrix.Imaging.Drawing.NoSkia/   the managed workalike engine plus the
                                       LINKED drawing-session sources
  Drawing*.cs                          the SkiaSharp-shaped public types
                                       (DrawingCanvas, DrawingBitmap,
                                       DrawingPaint, DrawingPath, ... )
  Pictures/                            the public display-list surface:
                                       DrawingPicture, DrawingCommand and its
                                       command records, DrawingTextStyle,
                                       IDrawingCommandVisitor,
                                       IDrawingTextOutliner,
                                       DrawingFontSlant/Weight/Width,
                                       DrawingTextAlign - a grouping folder
                                       whose files stay in the root
                                       CodeBrix.Imaging.Drawing.NoSkia
                                       namespace
  Raster/                              internal rasterizer: Bezier flattening,
                                       stroke outlining, dash splitting,
                                       scanline polygon filling, blending,
                                       shader/paint sources, and
                                       StrokeOutlineCache (recent stroke
                                       outlines - see THE ANIMATION MODEL)
  NoSkiaTypeAliases.cs                 the global using aliases (see below)
  NoSkiaInternalsVisibleTo.cs          grants internals to the .NoSkia.Svg
                                       assembly

src/CodeBrix.Imaging.Drawing.NoSkia.Svg/   the managed SVG renderer
  ShimSkiaSharp/, Model/, SceneGraph/  the VENDORED scene compiler (display
                                       list + model + scene graph)
  DrawingSvg.cs                        the public facade
  DrawingSvgShape.cs, DrawingSvgPart.cs, DrawingSvgAspect.cs,
  DrawingSvgSessionExtensions.cs       the SVG session element and its
                                       per-part overrides (public, at the
                                       assembly root like the facade)
  Rendering/                           the managed backend: NoSkia display-list
                                       replayer, ImagingSvgAssetLoader,
                                       NoSkiaFontRegistry, NoSkiaImageFilterFactory,
                                       NoSkiaTextRenderer, and SvgPartRenderer
                                       (re-records the scene with part
                                       overrides - see SVG ELEMENTS below)

src/CodeBrix.Imaging.Drawing.NoSkia.Package/   packaging-only project (no code)

tests/                                 see TESTING below
samples/                               sample applications, each with its own
                                       solution; see EXTRAS-README.txt

Root solutions:
  CodeBrix.Imaging.Drawing.slnx          cross-platform: the four src projects
                                         and the three test projects; builds with
                                         the plain .NET SDK on Linux/macOS/Windows
  CodeBrix.Imaging.Drawing.Windows.slnx  a Windows-host superset that additionally
                                         carries the sample's native WinUI 3 and
                                         WPF heads

The library layers cleanly: Animation, Fills, Text, Planes, Paths (no session
knowledge; Paths uses CodeBrix.PolygonTools) -> Images, Effects -> Models +
Shapes (DrawingAnimations binds Animation tracks to them) -> Rendering,
SafeAreas, Layout -> Captions (builds shapes, places them with Layout) ->
DrawingSession and FrameSequenceRenderer (facade, plus the SafeAreas check)
-> Extensions (the CodeBrix.Imaging bridge). Formations sits beside Models: its
pure parts (areas, targets, orders, poses, arrival styles, schedules) use only
Paths and Planes; DrawingAnimations.BindFormation turns them into session
elements. Painting sits beside Rendering: its maps are made
by DrawingRenderer.RenderToOwnershipMap and its players use only Planes. Planes
depends only on System and CodeBrix.Imaging; the session side uses it
(DrawingRenderer.RenderToPlaneImage/RenderToMask, ParticlesShape) and never
the other way round. Nothing in src/ references any UI framework, and
nothing may start to.

THE LINKED-SOURCE / NOSKIA-SYMBOL MECHANISM (three assemblies, one code copy)
============================================================================

This is the single most important thing to understand before editing
anything under src/.

1. There is exactly ONE copy of the drawing-session code, and it lives in
   src/CodeBrix.Imaging.Drawing/. The NoSkia project pulls it in with

       <Compile Include="..\CodeBrix.Imaging.Drawing\**\*.cs"
                Exclude="..\CodeBrix.Imaging.Drawing\bin\**;..\CodeBrix.Imaging.Drawing\obj\**"
                LinkBase="Shared" />

   so a change to DrawingSession.cs changes both packages at once. Never
   copy a shared source file into the NoSkia project.

2. The NoSkia project defines the NOSKIA compile symbol. In the shared
   sources that symbol switches their `using SkiaSharp;` directives to
   `using CodeBrix.Imaging.Drawing.NoSkia;`, where the workalike types live.

3. NoSkiaTypeAliases.cs is the ONE deliberate exception to the family's
   "no global usings" convention. It carries a global using alias per
   SkiaSharp type name (SKCanvas -> DrawingCanvas, SKColor -> DrawingColor,
   and so on), which is what lets the shared sources keep spelling the SK
   names and stay byte-identical between the two packages. The consumer-facing
   rename map in AGENT-README-NOSKIA.txt is generated from this file - when
   you add an alias here, update that map.

4. The SVG renderer is a SEPARATE assembly on purpose: the vendored shim
   types are literally named SKPaint/SKCanvas/..., and they would collide
   with the global aliases if they were compiled into the core NoSkia
   assembly. Because that Svg assembly references the core assembly, neither
   project can pack the pair on its own - hence the packaging-only project.

5. GenerateDocumentationFile is ON for every project that has code,
   including src/CodeBrix.Imaging.Drawing.NoSkia.Svg
   (CodeBrix.Imaging.Drawing.NoSkia.Svg.csproj sets it to true, and the
   packaging project packs the resulting
   CodeBrix.Imaging.Drawing.NoSkia.Svg.xml). The vendored code does not
   carry complete XML documentation, and suppressing CS1591 is never done
   in this repository - what keeps that assembly CS1591-clean is the
   internalize pass (internalize-surface.sh, see the re-vendoring recipe):
   it makes the vendored types internal and demotes their "///" doc
   comments to "//", so only the documented facade remains public. New code
   written for that assembly still documents every public member. The
   fourth project, CodeBrix.Imaging.Drawing.NoSkia.Package, has no code of
   its own and therefore generates no documentation file.

BUILDING
========

    dotnet restore CodeBrix.Imaging.Drawing.slnx
    dotnet build CodeBrix.Imaging.Drawing.slnx

- Everything targets net10.0 and nothing else. netstandard targets are not
  used anywhere in this repository.
- global.json at the root pins the SDK: version 10.0.404 with rollForward
  latestFeature and allowPrerelease false, so the newest installed 10.0 SDK
  at or above 10.0.404 builds the repository (never a later major or minor
  release, never a preview). It also selects the Microsoft.Testing.Platform
  test runner.
- GeneratePackageOnBuild is TRUE on both packable projects, so an ordinary
  build produces .nupkg files (see PACKAGING AND PUBLISHING).
- No sample is in the root cross-platform solution; each sample has its own
  solution under samples/ (EXTRAS-README.txt). Samples other than
  PainDiagram consume the library by PackageReference; to try unpublished
  library changes in one, restore it with an extra --source pointing at a
  local folder holding this build's .nupkg files - never add that path or a
  nuget.config to the repository.
- The Windows-only solution needs Windows build tooling for the sample's
  native WinUI 3 head; the four src projects and the test projects build
  everywhere.

TESTING
=======

    dotnet test CodeBrix.Imaging.Drawing.slnx

Three test projects, plus a folder of linked helper sources:

  tests/CodeBrix.Imaging.Drawing.Tests
      The canonical suite, against the SkiaSharp backend. References the
      SkiaSharp.NativeAssets.{Linux,macOS,Win32} packages so it runs
      anywhere. Contains the pixel-level rendering assertions.

  tests/CodeBrix.Imaging.Drawing.NoSkia.Tests
      The SAME session suite - linked sources, NOSKIA symbol, plus a link to
      NoSkiaTypeAliases.cs - run against the managed backend, so both
      backends must satisfy every behavioral guarantee. Needs NO native
      library. On top of the linked session suite it carries its own
      authored files:
        - the engine suite: DrawingPathTests, DrawingShaderTests,
          DrawingShaderPatternTests, DrawingColorFilterTests,
          DrawingBlendModeExtensionsTests, DrawingImageFilterScaleTests,
          SvgFilterScaleTests, StrokeOutlineCacheTests (+
          DrawingSurfaceMemoryTests in the same file)
        - Pictures/: DrawingCanvasPictureTests, DrawingPictureTests
        - Svg/: DrawingSvgTests, DrawingSvgSceneTests, DrawingSvgSurfaceTests,
          DrawingSvgFixtureTests, DrawingSvgWarningTests,
          DrawingPictureConverterTests, NoSkiaFontRegistryTests
        - NoSkiaPublicSurfaceTests.cs, the exported-surface guard described
          under the re-vendoring recipe
        - SvgReferenceTests.cs, which renders every sample SVG through
          DrawingSvg and compares against the committed reference PNGs.

  tests/CodeBrix.Imaging.Drawing.ParityTests
      References BOTH drawing libraries at once through extern aliases
      "skia" and "noskia" (the two assemblies declare the same types in the
      same namespaces, so aliases are the only way to hold them together).
      SessionParityTests.cs renders identical scenes through both backends
      and asserts near-identical pixels. SvgParityTests.cs renders every
      tests/SvgAssets/*.svg through the SkiaSharp SVG stack
      (CodeBrix.SkiaSvg, referenced ONLY by this test project) and through
      the NoSkia SVG renderer.

  tests/Shared/
      ImageComparison.cs and SvgTestAssets.cs, linked into the NoSkia.Tests
      and ParityTests projects (ImageComparison.cs also into the canonical
      Tests project, for the pinned-output test). Not a project of its own.

  The animation-frame features have their own files in the canonical
  suite (DrawingSessionPixelExportTests, DrawingSessionOpacityTests,
  DrawingSessionPlaneExportTests, and the Animation/, Fills/, Text/,
  SafeAreas/ folders plus Shapes/TextShapeTests, Shapes/ShapeBoundsTests and
  Shapes/ParticlesShapeTests); they link into NoSkia.Tests like the rest.
  Planes/ holds the plane, filter, transition, random, noise, particle and
  accumulator tests (linked into the NoSkia suite like every other file
  there); they pin exact values derived from the documented arithmetic, and
  PlaneKernelsTests pins thread-count independence through an internal,
  thread-local MaxDegreeOfParallelism hook.
  Text tests use the committed tests/SvgAssets/Fonts/OpenSans-Regular.ttf,
  located from the source file's own path by TestSupport/TestAssets.cs, and
  the committed tests/SvgAssets/Fonts/Roboto-Regular.ttf (OFL-Roboto.txt
  beside it) - the KERNING TEST FONT: Open Sans carries no kerning data,
  Roboto carries GPOS kern pairs and fi/ffi ligatures. Text/KerningTests,
  Text/TextLayoutEngineTests, Shapes/TextBlockShapeTests, Captions/ and
  Models/DrawingAnimationsStaggerTests cover the text blocks; they link into
  the NoSkia suite like everything else. ParityTests/TextParityTests asserts
  every layout value (line metrics, cluster positions, glyph outlines as SVG
  path data, text along a path, caption placement, stagger states) EQUAL
  across the two assemblies, and the drawn text within the image tolerance.
  The NoSkia suite's Svg/DrawingSvgShapeTests cover the SVG session element
  with inline documents (not in tests/SvgAssets, whose every .svg is a
  reference-image fixture).
  ParityTests/FrameParityTests.cs (through IFrameAdapter and its two
  implementations) asserts that what these features COMPUTE in managed code
  - easing values, ramp colors, text widths and outline bounds, safe-area
  zones and findings, particle data - is identical between the backends,
  and that what they draw (gradients, text, fades, overlays, raw frames,
  plane-image and mask exports, particle dots) is within the image
  tolerance; it also asserts that each backend's plane-image export holds
  exactly its own raw frame and a one-layer mask exactly its own alpha.
  ParityTests/PlaneParityTests.cs runs identical plane work through both
  assemblies and asserts BYTE-FOR-BYTE equality - no tolerance, because
  nothing there goes through a rasterizer.
  The animation model has its own files in the canonical suite
  (DrawingSessionAnimationTests, FrameSequenceRendererTests, Animation/
  timeline, track and playback tests, Models/ transform, camera and binding
  tests, Shapes/DrawingGroupTests, Layout/, SafeAreas/
  SafeAreaTransformedCheckTests, Rendering/DebugOverlayTests).
  DrawingSessionAnimationTests compares the live render after any sequence
  of seeks with a fresh render (byte for byte), and reads the renderer's
  internal LayerFullRedrawCount to prove that time-keyed elements appearing
  in order stay incremental. ParityTests/AnimationParityTests.cs asserts the
  computed values equal across the two assemblies and the drawn frames
  (including multi-threaded frame sequences) within the image tolerance.
  DrawingSessionPinnedOutputTests pins a scene that uses none of the
  animation model - its export AND its live, cached render - to PNGs in
  tests/CodeBrix.Imaging.Drawing.Tests/References, one pair per package,
  rendered on Linux x64 by the library before the animation model existed.
  The NoSkia suite compares byte for byte (the managed rasterizer is the
  same code on every OS). The SkiaSharp suite compares through the shared
  ImageComparison tolerance (tests/Shared/ImageComparison.cs, linked into
  that project for this test), because Skia's native rasterizer may
  anti-alias a few edge pixels differently on Windows or macOS; the exact
  guarantee is pinned instead by two tests that run in BOTH suites within
  one run: the cached live render (three renders while the scene grows)
  equals one fresh render byte for byte, and merely constructing animation
  objects (timeline, transform, group, camera, track; Time = 0, Camera =
  null) changes no exported or live byte. Together they are the guard that
  unanimated drawings keep their pixels. The references are raw bytes stored
  as PNG (the live one holds the canvas's own bytes, so premultiplied for
  Skia) and are never regenerated as part of a change; a missing file is
  written only when CREATE_PINNED_REFERENCES=1 is set.
  Paths, pictures, masks and effects have their own files: Paths/ (exact
  geometry values; PathGeometryCombiningTests for combining, stroke
  outlines and inflating, by exact areas and bounds), Images/, Effects/,
  Shapes/PathShapeTests, ImageShapeTests (pictures and sprites), ShapeOutlineTests and
  Models/PathAnimationTests; the masked/effect tests also compare a live
  render after changes with a fresh one byte for byte.
  Rendering/EffectProcessorTests pins the effect-size rule (radius scale,
  scene-fixed and turning offsets) exactly; Effects/DrawingEffectsTests
  checks it in pixels and renders one turned, scaled, zoomed scene at 1x and
  2x, comparing after a 2x downsample (resolution independence).
  ParityTests/PathCombiningParityTests.cs asserts combined paths, stroke
  outlines, inflated shapes and merged glyph outlines EQUAL across the two
  assemblies (SVG path data of every result), and draws them, transformed
  effects and both mask meanings within the image tolerance.
  The painterly tools: DrawingSessionHardEdgedMaskTests (exact pixels of
  the rule, ties included), Painting/DrawingOwnershipMapTests (numbering,
  counts, bounds, the nearest fill against a brute-force search and its tie
  rule, seams, colour pickup incl. half-size and transparent sources, brush
  textures, thread-count independence), Painting/KeyframePlayerTests (frames
  equal DrawingTransition on the spot after seeks, painting ahead, one
  worker, failures, disposal, the banded parallel dissolve) and
  Shapes/BrushStrokeShapeTests. ParityTests/PaintingParityTests.cs reduces
  every result of a scene that touches each route into the rule (filled and
  outlined shapes, glyphs, freehand stroke, brushstrokes, a turned group,
  a camera with parallax, a letterboxed non-integral fit, both backgrounds)
  to a SHA-256 and asserts the two assemblies EQUAL.
  The motion and formation tools: Paths/PathGeometryQueryTests,
  Animation/KeyframeDerivationTests, Models/DrawingAnimationsCombineTests,
  Models/PathTravelTests, Formations/FormationTests (including a live render
  of a formation that stays incremental and equals a fresh one),
  DrawingLayerVisibilityTests, FrameCacheTests (frames equal the sequence
  renderer's, sessions kept across seeks, eviction, threads, failure,
  disposal, KeepSessions) and Shapes/PathShapeFillTests, linked into the
  NoSkia suite; the NoSkia suite's own StrokeOutlineCacheTests and
  DrawingSurfaceMemoryTests; ParityTests/MotionAndFormationParityTests.cs
  (exact).
  ParityTests/PathParityTests.cs asserts that both assemblies compute the
  SAME geometry (lengths, points and tangents at fractions, bounds, trims,
  dashes, flattening, morphs, transforms, outlines, motion along a path,
  sprite frames) - compared through round-trip text of every float, no
  tolerance. ParityTests/DrawingFeatureParityTests.cs compares paths,
  pictures, sprites, clips, mattes, plane masks and effects as drawn, over a
  background and over transparency, within the image tolerance; and shows
  that effects on content both canvases draw identically (a picture copied
  1:1) differ by at most 2 levels.

Test conventions: xUnit v3, the Microsoft.Testing.Platform runner, and
SilverAssertions fluent assertions.

SVG reference images: tests/SvgAssets/References/*.png are produced by the
SKIA stack from the ParityTests suite - any missing reference is generated
automatically, and setting the environment variable
REGENERATE_SVG_REFERENCES=1 forces a full refresh - and they are checked into
the repository. The NoSkia.Tests suite then compares the managed renderer's
output against those committed references on any machine, with no Skia
present. SVG text renders with the committed
tests/SvgAssets/Fonts/OpenSans-Regular.ttf (OFL), never system fonts, so
output is machine-independent.

Cross-engine comparisons are tolerance-based - two independent rasterizers
never produce bit-identical anti-aliased edges. The metrics are premultiplied
per-channel mean delta and the fraction of noticeably different pixels, with
per-sample limits in SvgTestAssets.GetTolerance.

Rendering tests assert on actual pixels (for example the highlighter
guarantee: two overlapping strokes on one layer produce pixel-identical
output to one stroke; a blue shape on a red layer renders blue). When
changing either renderer, keep the pixel tests passing, and never reintroduce
per-frame rescaling of the background image - that was the cause of a 17x
drawing-lag bug (66 ms -> 4 ms per frame once fixed).

PACKAGING AND PUBLISHING
========================

Versioning. Both packable projects compute the version in MSBuild at build
time as 1.<years since the base year>.<day of year>.<minute of day>, all from
System.DateTime.UtcNow, with the base year set by the _VersionBaseYear
property. Never hardcode a <Version>. Consequences to remember: every build
produces a new version; two builds within the same UTC minute produce the
SAME version, so do not publish two packages from within one minute; and this
is date-stamp versioning, not SemVer - major/minor do not signal API
compatibility.

What the Skia package ships. src/CodeBrix.Imaging.Drawing packs its own
assembly plus, from the repository root: icon-codebrix-128.png, README.md,
THIRD-PARTY-NOTICES.txt, and AGENT-README-SKIA.txt renamed inside the package
to AGENT-README.txt. Package dependencies are SkiaSharp,
CodeBrix.Imaging.ApacheLicenseForever and
CodeBrix.PolygonTools.MitLicenseForever.

What the NoSkia package ships. src/CodeBrix.Imaging.Drawing.NoSkia and
src/CodeBrix.Imaging.Drawing.NoSkia.Svg both set IsPackable=false; the
packaging-only project src/CodeBrix.Imaging.Drawing.NoSkia.Package sets
IncludeBuildOutput=false (its own assembly is empty and excluded) and folds
the two referenced assemblies plus their XML documentation into
lib/<tfm> through the IncludeNoSkiaAssembliesInPackage target hooked onto
TargetsForTfmSpecificContentInPackage. The two ProjectReferences carry
PrivateAssets="all" so they do not surface as package dependencies; the
genuine dependencies (CodeBrix.Imaging.ApacheLicenseForever,
CodeBrix.PolygonTools.MitLicenseForever and
CodeBrix.SvgParse.MsplLicenseForever) are declared explicitly on the
packaging project so consumers restore them. CodeBrix.PolygonTools is
referenced by src/CodeBrix.Imaging.Drawing (shared Paths/ code) and by
src/CodeBrix.Imaging.Drawing.NoSkia (which compiles those sources), and
declared again on the packaging project; keep the three versions equal.
The packaging project packs the same four root files, with
AGENT-README-NOSKIA.txt renamed inside the package to AGENT-README.txt.

So each package contains exactly one file named AGENT-README.txt, and it is
the right one for that package. If you rename either root AGENT-README file,
fix the matching <None Include=...> line in the packing csproj.

Publishing checklist: build the solution (which packs), confirm both .nupkg
files carry the expected AGENT-README.txt, and tag the repository with the
version that was pushed. Both packages carry
PackageRequireLicenseAcceptance=true and the Apache-2.0 license expression;
the ".ApacheLicenseForever" suffix on both package IDs is a permanent promise
that those IDs are only ever published under Apache-2.0.

PROVENANCE AND VENDORED SOURCES
===============================

THIRD-PARTY-NOTICES.txt at the repository root is authoritative; keep it in
sync with anything below that changes.

- src/CodeBrix.Imaging.Drawing (the Skia library) incorporates no
  third-party source. Its drawing-primitives geometry is original code
  rendered through the SkiaSharp NuGet dependency (BSD-3-Clause, consumed as
  a package only). Portions were adapted from the NuraPad application and the
  CodeBrix.Prism libraries, earlier projects by the same author, as is the
  body-map image used by the sample.

- src/CodeBrix.Imaging.Drawing.NoSkia (the workalike engine) is original
  managed code that mirrors the API SHAPE of SkiaSharp (MIT) and of Skia
  (BSD-3-Clause) so that SkiaSharp code can be retargeted mechanically. No
  SkiaSharp or Skia source is incorporated. Heavy lifting is delegated to
  CodeBrix.Imaging wherever that library already has it (codecs, resamplers,
  Gaussian blur, font parsing); the rasterizer, stroker, clipping, gradients
  and blend stack are original to this repository because CodeBrix.Imaging
  has no general vector-drawing engine.

- src/CodeBrix.Imaging.Drawing.NoSkia.Svg VENDORS the scene compiler from
  CodeBrix.SkiaSvg (MIT, Svg.Skia lineage - Svg.Skia is MIT, Copyright (c)
  Wiesław Šoltés), including its ShimSkiaSharp display-list model and its
  Svg.Model scene compiler, with namespaces renamed to
  CodeBrix.Imaging.Drawing.NoSkia.Svg.*. Keep the vendored code byte-faithful
  to upstream where possible so future refreshes stay mechanical: put new
  behavior in Rendering/ (the managed backend), not in the vendored folders.
  The carried-forward notices live in
  src/CodeBrix.Imaging.Drawing.NoSkia.Svg/VENDORED-NOTICES.txt.

  RE-VENDORING RECIPE. After copying a refreshed compiler in:
    1. Rename the namespaces to CodeBrix.Imaging.Drawing.NoSkia.Svg.*.
    2. Run src/CodeBrix.Imaging.Drawing.NoSkia.Svg/internalize-surface.sh.
       It makes every top-level type in ShimSkiaSharp/, Model/, SceneGraph/
       and Rendering/ internal (NoSkiaFontRegistry is the one exception) and
       demotes their "///" doc comments to "//". Both halves are required.
       Internalizing alone is NOT enough: the compiler writes .xml entries
       for internal types too, and an entry's name attribute carries the
       member's parameter and return types - so one documented
       ToDrawingShader(SKShader) would put a Skia-named type into the
       packaged documentation file however its prose reads. The script is
       idempotent, so re-run it freely; a partial refresh is fine.
    3. Build and run the suites. NoSkiaPublicSurfaceTests is the guard: it
       asserts the exported-type list of the SVG assembly exactly, and scans
       both packaged .xml files for any "Skia" that is not part of "NoSkia".
       A re-vendor that re-publicises a type or reintroduces a doc comment
       fails there. Fix it at the call site or by re-running the script -
       never by re-publicising a type or by post-processing the .xml.

- Opt-in Skia type names. src/CodeBrix.Imaging.Drawing.NoSkia.Package/build/
  CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever.props carries the
  global-using aliases that a consumer turns on with
  CodeBrixUseSkiaTypeNames. NuGet imports build/$(PackageId).props into every
  consuming project automatically, so the file's NAME must keep matching the
  package id. SYNC RULE: that file and
  src/CodeBrix.Imaging.Drawing.NoSkia/NoSkiaTypeAliases.cs are two lists of
  the same correspondence - the .cs file covers what the linked shared
  sources compile against, the .props file covers everything a consumer can
  name. Adding a workalike type means adding it to the .props file (and to
  the .cs file if the shared sources need it) and to the correspondence
  table in AGENT-README-NOSKIA.txt. The props file is the ONLY place in the
  package where Skia type names appear.

- The SVG parsing dependency, CodeBrix.SvgParse.MsplLicenseForever (Ms-PL,
  SVG.NET lineage), is consumed as a NuGet package only.

- The polygon clipping dependency of both packages,
  CodeBrix.PolygonTools.MitLicenseForever (MIT AND BSL-1.0 - the CodeBrix
  family's managed library derived from the Clipper library by Angus
  Johnson), is consumed as a NuGet package only; no source of it is in this
  repository. It is the one dependency added to the drawing packages for the
  animation work, by the owner's explicit decision.

- tests/SvgAssets/Fonts/OpenSans-Regular.ttf is SIL OFL 1.1 (OFL.txt sits
  beside it) and is used solely to make SVG text rendering deterministic.
  Never replace it: the committed SVG reference images depend on it.
- tests/SvgAssets/Fonts/Roboto-Regular.ttf is SIL OFL 1.1 (OFL-Roboto.txt
  sits beside it), a test-only asset used because it carries kerning data.

CODING CONVENTIONS
==================

CodeBrix family conventions, all of which apply here:

- Nullable reference types are OFF family-wide: no "?" annotations on
  reference types (string?, MyClass?), no null-forgiveness operator (!).
  Value-type nullables (int?, SKPointI?, Color?) are fine.
- No global usings and no ImplicitUsings - with the single documented
  exception of NoSkiaTypeAliases.cs. All using directives sit at the top of
  each file, System.* first.
- File-scoped namespaces only (namespace X; - never braced blocks).
- XML documentation comments are REQUIRED on every public type and member.
  GenerateDocumentationFile is on in every library project, including the
  vendored SVG assembly, and CS1591 is fixed at source, never suppressed.
  The vendored assembly can afford that because it exports only the facade
  (see the re-vendoring recipe below): everything else is internal and its
  doc comments are demoted to plain comments, so there is nothing
  undocumented for CS1591 to find.
- No project-level warning suppression: no <NoWarn>, no pragma disables.
- Tests: xUnit v3 plus SilverAssertions fluent assertions
  (value.Should().Be(expected)); test classes named <ClassUnderTest>Tests;
  methods in Member_snake_case or snake_case form; //Arrange //Act //Assert
  comments in multi-statement tests.
- The date-stamped package version is computed by MSBuild at build time -
  never hardcode <Version>.
- Source organization: entry-point types at the project root, everything else
  in sub-folders whose names match their namespace suffix (Models, Shapes,
  Rendering, Extensions, Raster). The one deliberate exception is
  src/CodeBrix.Imaging.Drawing.NoSkia/Pictures/, a grouping folder for the
  display-list types: all of its files declare the root
  CodeBrix.Imaging.Drawing.NoSkia namespace, because the display list is
  part of the engine's top-level surface.

Shared-source rules learned adding the animation-frame features:
- No unsafe code in the shared sources: RenderToPixels reads the frame into
  a pinned pooled array and copies it to the caller's span.
- Text is laid out with CodeBrix.Imaging.Fonts in managed code at TextShape
  construction and stored as verbs + points, so both backends draw the same
  outline. TextMeasurer.Measure rounds its box up to whole units; advances
  are therefore summed from TryMeasureCharacterDimensions instead.
- KERNING: CodeBrix.Imaging applies GPOS kern / legacy kern tables by default
  (KerningMode.Normal; None switches it off). A whole run must be shaped at
  once for kerning to apply, so tracked text and text blocks shape each run
  ONCE (TextShaping.Shape: TryMeasureCharacterDimensions over the run) and map
  the shaped glyphs back to text clusters by code point (characters a glyph
  does not name - ligature components, combining marks folded into a
  precomposed glyph - join the previous cluster; a default-ignorable code
  point such as U+200B advances by 0, because the font stack reports a bogus
  width for it but places nothing). Each cluster is then OUTLINED ON ITS OWN
  at the pen position the run gave it. For the tracked TextShape the pen is
  `pen + (count * trackingUnits)` exactly as before, so for a font without
  kerning (Open Sans) tracked text is bit-for-bit what it always was - the
  pinned-output test's tracked line proves it. Untracked TextShape keeps the
  old whole-run render (kerned by the font stack's default).
- TEXT BLOCKS: line breaking, alignment, justification, vertical alignment
  and trimming are this repository's managed code over the shaped clusters
  (TextLayoutEngine.Builder); a paragraph is shaped once per run, never per
  line, so a kern pair across a wrap point stays in the earlier cluster's
  advance. Glyph points are produced by the font stack at their final
  position (no translation arithmetic afterwards), which is what makes the
  geometry identical in both assemblies. Bidi reordering is not done.
- TextBlockShape draws fully revealed glyphs one path per paint (runs with
  their own colour or fill get their own), and a partly revealed glyph in a
  save-layer at its alpha. StrokePlacement.Outside draws the stroke in a
  save-layer and cuts the glyph shape out of it with a DstOut fill (glyph
  contours often overlap). MergeOverlaps instead draws each glyph's contours
  united (PathGeometry.Simplify at TextBlockShape.MergeTolerance), cached per
  cluster in the shape.
- Never write "Skia" (other than inside "NoSkia") in shared XML
  documentation: NoSkiaPublicSurfaceTests scans the packaged .xml files.
- Every new opacity path keeps the default bit-identical: a factor of 1
  composites with exactly the old alpha and no save-layer is opened.
- ONE VOCABULARY for the animation-era API (keep new members in step):
  * Angles: DEGREES everywhere in the public surface, clockwise on screen
    from +x (y down) - parameters, properties and animated track values.
    Trig works in radians inside; convert with Models/AngleUnits (degrees x
    pi / 180 in double). Planes/ may not depend on Models, so the particle
    system writes the same expression inline. What path geometry reports
    (PathPoint.Angle) must be passable straight to anything that takes an
    angle.
  * Time: double seconds. Moments are named ...Time (Time, StartTime,
    EndTime, BirthTime, PlaybackStartTime), spans Duration / Interval /
    MaxStep - never a "Seconds" suffix and never StopTime. A method's moment
    parameter is `seconds`; a constructor (or Add/Draw factory) parameter that
    becomes a Time property takes that property's name (time, startTime).
  * "Now": wherever a session or renderer supplies the clock, an unset time
    means its current Time (CheckSafeArea uses session.Time, full stop;
    SafeAreaOverlay.Time and DebugOverlay.Time default to the renderer's).
    Only clock-less geometry (SafeAreaGuide methods, SafeAreaTimeRule,
    DrawingLayout) treats a missing time as unknown, where every rule applies.
  * Names follow the published vocabulary: Align (not Alignment),
    ...ImagingImage for CodeBrix.Imaging's Image<Rgba32> (FromImagingImage,
    ToImagingImage), TextIndex/TextLength for text ranges, IsActiveAt(t) for
    "is this span live at t", Hold for "stay on the end value".
  * Parameter order: a session method that forwards to an engine type or a
    shape constructor takes that callee's parameters in the callee's order,
    minus what the session supplies (fonts, the layout default).
  * Enums: the zero value is the API's default (TrackLoopMode.Hold,
    ImageSampling.Linear, DrawingPlaneResampling.Bilinear, PathStrokeCap.Round,
    PathStrokeJoin.Round, ...). Nothing persists enum numbers.
  * Backend resources: an added type that keeps a backend object is
    IDisposable (DrawingMask for a plane mask's bitmap, ImageSource,
    SpriteSheet) and documents who owns it; otherwise it makes the object per
    draw (ShapeFill creates its shader for each paint, which keeps its own
    reference) so nothing needs disposing.

THE ANIMATION MODEL AND THE RENDER CACHES
=========================================

The live Render keeps one cache bitmap per layer (LayerCache) and one
composited scene bitmap; exports never use either (from-scratch every call).
The animation model lets elements change after they are drawn, so the layer
cache no longer assumes "a prefix of immutable marks". How it stays correct
AND keeps old drawings on the old code path:

- "Dynamic" elements. DrawingElement has an internal IsDynamic flag, set
  (and never cleared) by any setter of the new properties (Transform,
  Opacity, StartTime, EndTime) and from birth for DrawingGroup and
  AnimatedShape; appearance changes also take a fresh value of one global
  Interlocked counter (the element's Stamp). GetRenderStamp(time) is the
  state a cache compares: the stamp, or for a group a 64-bit hash over its
  own stamp and each child's (visibility, stamp), or for an animated shape
  its stamp mixed with its current shape's.
- The layer cache records how many leading entries it has PROCESSED
  (DrawnElementCount - each drawn, or skipped because it did not exist at
  the render time) and, once the layer holds a dynamic element, an
  ElementState per processed entry (tracked?, visible?, stamp). Static layers
  never allocate the states.
- Each Render: a full redraw when the bitmap size, ResetVersion, or the
  layer's VIEW (camera with parallax, then layer transform, as a Matrix3x2)
  changed. Otherwise TryKeepProcessedPrefix walks the processed entries,
  looking only at dynamic ones; at the first one whose visibility or stamp
  differs (or that became dynamic after it was drawn), the tail from there
  is forgotten if every entry in it drew nothing, else the layer is fully
  redrawn. Then the unprocessed tail is processed in order. Result: appends
  and in-order appearances are incremental; any change inside drawn content
  redraws that one layer.
- ContentVersion increments whenever a cache bitmap changes; the scene cache
  compares it (besides the old checks), because a truncate-and-redraw can
  leave the element count unchanged.
- DrawElement keeps the ORIGINAL drawing code for a plain element (identity
  transform, opacity 1, not a group or animated shape) on a layer with no
  view; strokes then keep their canvas-pixel path. Everything else goes
  through ElementPainter on a canvas mapped to calibrated units: the
  layer's view, then the element's transform, then a SaveLayer at the
  element's opacity, then the content (groups recurse, animated shapes draw
  their current shape). Transforms are canvas operations
  (Translate/RotateDegrees/Scale, which both canvases implement with the
  same pre-concatenation semantics) - never matrix types, so the shared
  sources stay identical; the managed Matrix3x2 copies exist only for
  bounds, cache comparison and the public ToMatrix/GetMatrix.
- Element visibility (time windows) is evaluated by the renderer at its
  Time; DrawingSession.Time also applies DrawingAnimations and updates
  every AnimatedShape (walking groups, skipping non-dynamic elements).
- Debug overlays follow the safe-area overlay's pattern exactly: a renderer
  property drawn only by Render, and explicit export overloads.
- Masks have two meanings (MaskMeaning): Coverage reads each layer's
  private bitmap alpha and unions them (the original rule, the default);
  VisibleAlpha renders the frame through RenderToPixelsCore without the
  background and keeps alpha / 255 - so it can never disagree with an
  export. The DrawingPlane overloads write into the caller's plane.
- The safe-area overlay draws its tint (MaskColor alpha x Opacity, via
  ScaleAlpha) as opaque zone rects in one save-layer, then the outline as
  four opaque bands just outside SafeAreaGuide.GetSafeRectangle in a second
  save-layer at the outline colour's alpha - both non-anti-aliased, so the
  two packages draw them alike.
- Paths, pictures, masks and effects (shared sources):
  * New element kinds are ordinary DrawingShape subclasses. PathShape and
    ImageShape are static until a changeable value is set (it calls
    MarkChanged); SpriteShape is dynamic from birth and folds the frame it
    shows at the render time into GetRenderStamp, so a frame change redraws
    its layer; ElementPainter routes it (with the time) like a group.
  * Effects: DrawingElement.Effects routes an element through ElementPainter
    (NeedsCalibratedPath) with a PaintContext (device size, calibrated ->
    device scale) that the renderer builds per layer bitmap. EffectProcessor
    draws the element into a device-sized offscreen whose canvas takes the
    current canvas's TotalMatrix (Concat - both canvases accept their own
    matrix type, so the shared code names no matrix type), reads it back as
    straight RGBA through SKImage.ReadPixels (Skia stores premultiplied
    bytes, the managed engine straight - never read SKBitmap.Bytes of a
    premultiplied bitmap in shared code), crops to the coverage box grown by
    the effects' reach, computes with DrawingPlane/DrawingPlaneImage, writes
    an UNPREMULTIPLIED bitmap (both engines store those bytes as given) and
    composites it 1:1 with ResetMatrix. A canvas that is not drawn by the
    renderer (no PaintContext) skips effects.
  * Effect SIZES follow the content: EffectProcessor.Measure takes the
    element's full linear map to device pixels (its own transform times the
    canvas TotalMatrix - read as a Matrix3x2, the one #if NOSKIA in that
    file, because SKCanvas.TotalMatrix is an SKMatrix and the managed canvas
    keeps a Matrix3x2) and the scene's rotation (the camera roll for that
    layer, carried in PaintContext.SceneRotation, built per layer bitmap):
    radii x sqrt|det F|; the offset through F (RotatesWithElement) or through
    the left stretch sqrt(F^T F) after the scene rotation (FixedInScene, the
    default). A layer's effects use layer transform x camera x calibration
    scale. For F = s I and no camera the arithmetic is exactly the old
    "radius x scale, offset x scale" (the square roots of exact squares), so
    untransformed content keeps its pixels.
  * Masks: content into a save-layer, then the mask's coverage drawn into a
    second save-layer composited DstIn (keep) or DstOut (cut). Canvas clips
    are deliberately not used - both engines apply a clip to every draw
    inside a save-layer, which would double the edge coverage where
    elements overlap. SKBlendMode and SKColorFilter were added to
    NoSkiaTypeAliases.cs for this (the .props and the README table already
    carried them).
  * A layer with a Mask or Effects is post-processed in DrawLayerBitmap
    (exports and live alike): the live cache keeps a 64-bit stamp of
    everything that went in (element count, reset version, dynamic elements'
    states, the mask's stamp including a mask layer's content, the effects'
    values) and redraws the layer whole when it changes - no incremental
    appends on such a layer.
  * Pictures are kept as unpremultiplied backend bitmaps filled through
    GetPixels + Marshal.Copy; decoding is CodeBrix.Imaging's, so both
    packages hold identical pixels. Tint is a colour-matrix colour filter.
- Combined bindings (BindingMode.Add / Multiply): an AnimationBinding
  carries its Mode; a combining binding registers, per (target, slot) it
  touches - TranslationX/Y, Rotation, ScaleX, ScaleY, Opacity; camera
  PanX/Y, Rotation, Zoom - a RESTING value taken at the first combining bind
  of that slot (reference-counted, dropped with the last such binding).
  DrawingAnimations.Apply with no combining binding is the old loop exactly;
  otherwise: restore every registered slot, run Replace (and callback)
  bindings in order, then Multiply, then Add. That order is what makes the
  result independent of binding order and every frame a function of its time
  (no accumulation from one apply to the next).
- Travel along a path (BindAlongPath with PathTravelOptions): the roll is
  (progress - upright) x (length / radius x 180 / pi) in that operation
  order; for a FloatTrack it is evaluated by KeyframeTrack.EvaluateMapped
  (each segment interpolated between the CONVERTED key values with the same
  easing), so it equals a rotation track mirrored key by key - the
  AutumnAviana sample's roll stayed byte-identical on the switch. Facing adds
  point.Angle + offset before the roll. With neither roll nor extension the
  call forwards to the plain BindAlongPath. PathTrain applies all its cars
  in one binding (double arithmetic for distances and rolls).
- DrawingLayer.IsVisible: every renderer loop over layers (live cache
  update, scene composite, exports, all three mask meanings, ownership maps)
  and CheckSafeArea skip a hidden layer; SceneLayerState records Visible so
  showing/hiding recomposites; a hidden layer's cache is neither updated nor
  pruned (it is still in the list), so showing it again is at most one
  redraw. A matte (DrawingMask.FromLayer) reads the layer's elements
  directly and ignores visibility, as it ignores opacity.
- NoSkia StrokeOutlineCache (engine only; the Skia package strokes natively):
  DrawingCanvas.DrawPath's stroke branch asks the cache for the outline in
  the path's own coordinates, keyed by the path's verbs and points
  (compared value for value, bit for bit), stroke width, miter limit, cap,
  join, dash intervals and phase, and the matrix's largest scale (which sets
  the flattening and arc tolerances); a miss builds it exactly as before
  (Flatten, DashSplitter, StrokeBuilder). The canvas matrix is applied to a
  fresh copy every draw, so a hit fills the very polygons a miss would - the
  pixels cannot change (StrokeOutlineCacheTests compares cached with
  StrokeOutlineCache.Enabled = false, exported and live). Global, locked,
  cleared whole when it holds too many points. Strokes drawn by other canvas
  calls (lines, ovals) are not cached.
- FrameCache: workers are plain threads (below normal priority) started by
  the first SetPlayHead; each builds its session on its own thread and keeps
  it. One lock guards the frame map, the in-flight set, the spare buffers and
  the play head; the window end (the Capacity-th wanted frame) is computed
  when the head moves. Frames are exactly FrameSequenceRenderer's
  (RenderInto's three steps). Frame buffers may be reused after eviction
  (TryGetFrame copies under the lock, so a reader never sees one reused).
- Rules: keep the pinned-output test passing unmodified; a new element
  property that changes pixels must call MarkChanged (and so make the element
  dynamic); never make the plain path depend on the new state; never share
  cache state between renderers.

SVG ELEMENTS (NoSkia only): DrawingSvgShape AND THE PART RENDERER
=================================================================

DrawingSvgShape (Svg assembly root) is an ordinary DrawingShape subclass, so
the shared session code treats it like any element; the Svg assembly reaches
DrawingShape's internals (MarkChanged) through NoSkiaInternalsVisibleTo.cs.
With no part override it draws DrawingSvg.Picture as loaded. With overrides
it calls Rendering/SvgPartRenderer, which re-records the compiled scene
(SvgSceneDocument, kept by DrawingSvg after a load together with its
converter and model) into a shim picture: the walk MIRRORS
SceneGraph/SvgSceneRenderer.RenderNodeToCanvas step for step, adding at each
node whose ElementId has an override (1) a Save + SetMatrix(P E P^-1) where P
is the node's parent's matrix in the scene as authored and E the override in
document space (so the content lands at E applied after P), and an opacity
save-layer, before the node's own Save; (2) skipping hidden parts; (3)
replacing the node's LocalModel by a copy whose stroked DrawPath commands
carry trimmed geometry (shim path -> DrawingPath via the model ->
PathGeometry.FromDrawingPath -> Trim -> shim path). Mask nodes are walked the
same way. The result is converted with
DrawingPictureConverter.ConvertTransient (added in Rendering/): the root and
the trimmed copies are converted WITHOUT entering the converter's identity
cache (they are recorded afresh on every change), while the nodes' own
LocalModel pictures go through the cache and are shared with the loaded
picture. DrawingSvg.RenderLock serializes re-recordings (the converter is
not thread-safe). No vendored file was changed for this.
RE-VENDORING: after refreshing the scene compiler, compare
SvgSceneRenderer.RenderNodeToCanvas with SvgPartRenderer's walk and carry any
change across (the NoSkia Svg/DrawingSvgShapeTests test
A_re_recorded_scene_without_visible_change_draws_the_loaded_picture_byte_for_byte
catches a walk that drifted). The new
public types live in the assembly root, which internalize-surface.sh never
walks; SvgPartRenderer uses "//" comments, so the script leaves it as it is.
NoSkiaPublicSurfaceTests lists the exported types; add new public types to
it deliberately.

PLANES: THE BIT-IDENTICAL GUARANTEE
===================================

What is promised: everything in src/CodeBrix.Imaging.Drawing/Planes/ gives
the SAME BITS in both packages on the same machine and runtime, for any
thread count. It holds because the folder is ordinary shared C# on float,
double and integer arrays (one compiled copy of the same source in each
assembly), every output value is computed by exactly one thread in a fixed
operation order (PlaneKernels.ForRows splits rows only), and no rasterizer
is involved. PlaneParityTests is the proof; keep it byte-for-byte.

What is NOT promised: identical bits across operating systems or
processors for the few operations that call the runtime's math library -
Math.Exp (Gaussian kernel weights), MathF.Pow (ApplyGamma), Math.Log and
Math.Cos (DrawingRandom.Normal), Math.Cos and Math.Sin (particle directions
and spawn offsets). Those go to the platform's math library and may differ
in the last bit; a value near a rounding boundary can then move one 8-bit
level. Plain +, -, *, /, Math.Sqrt and Math.Floor are IEEE-exact
everywhere, so everything built only from them (fills, mixing, compositing,
resampling, downsampling, vignettes, tone curves, transitions, the integer
random sequence, noise, motion blur) is identical across machines too.
Anything a session RASTERIZES (ExportPlaneImage, ExportMask with Coverage or
VisibleAlpha, ParticlesShape dots) is outside the guarantee: it goes through
each backend's anti-aliased canvas and matches within the image tolerance
only. The hard-edged tools (MaskMeaning.HardEdged, ownership maps) are NOT
rasterized - see THE HARD-EDGED PIXEL RULE below - and are inside it.

Rules that keep the guarantee true:
- Never reference SK*/Drawing* engine types from Planes/.
- No System.Numerics.Vector<T>/intrinsics, Math.FusedMultiplyAdd or
  reductions whose order depends on scheduling; a parallel loop may only
  write values that one thread computes alone.
- A new call into the math library is a documented exception: list it here
  and in the consumer guides' bit-identical paragraph.
- Paths/ follows the same discipline for MEASURING (only +, -, *, /,
  Math.Sqrt, Math.Floor, Math.Ceiling; fixed operation order; no
  intrinsics), so path geometry is identical across machines; building arcs
  (ArcTo, AddArc), stars, regular and rounded polygons uses Math.Sin,
  Math.Cos, Math.Tan, Math.Atan2 and Math.Acos - the documented exception.
  Effects use the planes Gaussian (Math.Exp) on rasterized pixels.
- PATH COMBINING (Paths/PathClipper.cs): curves are flattened in double
  (Wang's formula at the caller's tolerance), every point rounded ONCE to a
  grid of 1/1024 unit (Math.Round, away from zero; GridScale, a power of two
  so grid points divide back to exact floats), checked against +/- a
  million units (MaxCoordinate; keeps CodeBrix.PolygonTools in its fast
  64-bit range), then combined by PolyClip in exact integer arithmetic.
  Results are closed line polygons, NonZero. The stroker builds a stroke as
  positively wound pieces (a hexagon per segment whose end edges pass
  through the contour point's own grid point, an outer-side join wedge per
  corner with that grid point as apex, caps through it too) and unites them
  with the NonZero rule; pieces that touch share EXACT grid edges, which is
  what keeps the union free of hairline slits (an earlier version without
  the shared centre points left them on curves). Inflate/Deflate is the
  region united with / less its boundary stroked to twice the distance.
  Round joins, caps and dots call Math.Acos/Cos/Sin/Atan2 - the documented
  exception; everything else there is +, -, *, /, sqrt and integer work.
  PathCombiningParityTests compares results as text, exactly.
- Formations/ and the path point queries follow the same discipline: the
  queries are float +, -, *, /, MathF.Sqrt in a fixed order on the flattened
  polylines (both packages share the flattening); MaskDistance is double +,
  -, *, /, Math.Sqrt. Documented math-library exceptions: SwayingFallArrival
  (Math.Sin, Math.Cos, Math.Exp), SpiralArrival and DirectionalArrivalOrder
  (Math.Cos, Math.Sin), ArrivalRateProfile (Math.Pow), HeapArrivalOrder's
  lumps (DrawingNoise). MotionAndFormationParityTests compares every result
  as text, exactly.
- The Gaussian kernel is built entirely in double (weights, sum, division)
  and rounded to float once per weight; the documented arithmetic in the
  AGENT-READMEs and the XML comments must change with the code.

THE HARD-EDGED PIXEL RULE AND THE PAINTERLY TOOLS
=================================================

One rule, implemented once in Rendering/HardEdgeRasterizer.cs and used by
MaskMeaning.HardEdged and by every ownership map:

  A pixel belongs to a shape when its CENTRE (x + 0.5, y + 0.5), in output
  pixels, is inside the shape under the shape's own fill rule (non-zero:
  winding number not 0; even-odd: odd). A centre exactly on an edge follows
  the top-left rule: it is inside on a left or top edge and outside on a right
  or bottom edge (an edge counts for a row when yTop <= cy < yBottom, a
  crossing counts for a centre when crossingX <= cx; horizontal edges never
  count) - so two shapes sharing an edge never both take, or both miss, a
  centre lying on it.

How it is kept identical in both packages: CoverageCollector turns each
element into AREAS (DrawingShape.GetCoverageAreas; Stroke handled inside the
collector) and maps them by a double-precision HardEdgeMatrix composed from
the element transform, group transforms, the layer view
(DrawingRenderer.GetLayerViewMatrix) and the same aspect-fit the exports use
(CanvasCalibration.GetDrawingRect: plain float arithmetic, shared). Curves are
flattened in the element's own units at 0.1 output pixel divided by the
matrix's largest stretch (CoverageCollector.FrameTolerance); outline bands
come from PathGeometry.ToStrokeOutline (PolygonTools, exact integers). The
rasterizer maps every point once, builds edges, crosses each row at
x = xt + (cy - yt) * ((xb - xt) / (yb - yt)) and compares centres with
FirstCentreAtOrAfter (p + 0.5 >= value, exactly in double). Nothing calls a
canvas, so there is no backend type in the path at all. Elements are skipped
exactly when the renderer would skip them (time window; ScaleAlpha(255,
Opacity) == 0); paint (colour alpha, fills, effects, masks) never counts.
An ownership map rasterizes element by element in painting order and simply
overwrites (later wins); a hard-edged mask writes 1.

DrawingOwnershipMap.NearestFill is a separable squared-distance transform in
integers: per column the nearest owned row (ties: the upper); per row the
lower envelope of (x - q)^2 + d(q)^2 with every breakpoint kept as an exact
fraction (numerator, denominator; compared by cross-multiplication in long),
ties to the smaller column - which makes the documented tie rule (smallest
x, then smallest y) exact. Brute-force agreement is tested. Colour pickup
sums in double (or long for bytes), row-major, on ONE thread, so the order of
additions is fixed. FillByElement and ApplyColors are row-parallel through
PlaneKernels.ForRows (each value computed once by one thread).
Math-library calls (the documented cross-OS exception): Math.Cos/Math.Sin of
an element's axes angle in FillByElement, and Math.Cos, Math.Sin, Math.Pow
and Math.Sqrt in BrushStrokeShape's outline. Within one machine and runtime
both packages give the same bits.

Rules: never route the hard-edged path through a canvas; keep
GetCoverageAreas in step with what Draw paints (a new built-in shape must
override it, and GetAxes where it has a natural frame); never reuse a
keyframe buffer after it was handed out (KeyframeCache allocates per
keyframe precisely so readers may keep a dropped one); the keyframe worker
stays single (the paint function is documented as never entered twice).
Hard-edged ORDINARY rendering of elements or layers was deliberately not
added: the backends' aliased fills follow no shared rule, and a managed fill
inside the layer caches would disturb the default path.

SkiaSharp API notes for code in src/CodeBrix.Imaging.Drawing (and therefore
for the workalike engine that has to match it): SKPath.MoveTo/LineTo are
obsolete - build a path with SKPathBuilder and then detach it - and the
DrawBitmap overloads require an SKSamplingOptions argument. The managed
engine mirrors both shapes (DrawingPathBuilder, DrawingSamplingOptions).

NOTES
=====

- Both root .slnx solutions list the repository's plain-text documents under
  a "Solution Items" folder, and both already name the split files:
  AGENT-README-SKIA.txt, AGENT-README-NOSKIA.txt, MAINTAINER-README.txt,
  EXTRAS-README.txt, README-INDEX.txt, README.md, THIRD-PARTY-NOTICES.txt,
  LICENSE and icon-codebrix-128.png (the cross-platform solution adds
  .gitignore and global.json). There is no combined AGENT-README.txt in this
  repository. Add any new root document to both lists.
- The eight AI-agent pointer files at the root (AGENTS.md, CLAUDE.md,
  .clinerules, .cursorrules, .cursor/rules/agent-readme.mdc, .windsurfrules,
  .github/copilot-instructions.md, .junie/guidelines.md) are pointer-only
  stubs that route a reader to README-INDEX.txt. They are maintained centrally
  across the CodeBrix family from a canonical copy - do not hand-edit them
  here.
- README.md is the human-facing overview shown on GitHub and on nuget.org,
  and both packages embed it. Keep it short and keep it consistent with the
  AGENT-README files.
- The two packages must be shipped as a matched pair once the companion is
  published, since they carry the same session sources.
- TRIMMING: all three assemblies are trim-clean. A fully trimmed publish of
  a small console app referencing the three src projects, with a
  <TrimmerRootAssembly> item for each so every type is analysed
  (PublishTrimmed=true, TrimmerSingleWarn=false, -r linux-x64), reports no
  ILLink warnings of their own. The only reflection is
  SvgParseInternals.ParentField (SvgElement._parent looked up by a constant
  name, which the trimmer sees and keeps); an SVG using <use> renders
  byte-identically trimmed and untrimmed. CodeBrix.PolygonTools (rooted the
  same way in that check) reports no ILLink warnings either.
  CodeBrix.Imaging builds before 1.0.285.619 reported an IL2075 of their own that consumers of this package
  inherited. Re-run that trimmed publish after adding reflection.
