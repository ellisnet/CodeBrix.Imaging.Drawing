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

Status of the NoSkia companion: it builds green with the full test battery,
but it has NOT been published to nuget.org yet. The Skia package is
published. Do not describe the companion package as available on nuget.org
in consumer-facing text until it ships.

REPOSITORY LAYOUT
=================

src/CodeBrix.Imaging.Drawing/          the SkiaSharp-backed library; the
                                       CANONICAL copy of the drawing-session
                                       sources (the NoSkia project links them)
  DrawingSession.cs                    main entry point (root namespace)
  DrawingSessionOptions.cs             session construction options
  CalibrationSizing.cs                 explicit factory sizing choice (enum)
  InternalsVisibleTo.cs                grants internals to BOTH test
                                       assemblies (CodeBrix.Imaging.Drawing.Tests
                                       and CodeBrix.Imaging.Drawing.NoSkia.Tests,
                                       because the same sources compile into both)
  Models/                              DrawingElement, Stroke, StrokePoint,
                                       DrawingLayer
  Shapes/                              DrawingShape + Line/Arrow/Circle/Ellipse/
                                       Rectangle/Polyline shapes
  Rendering/                           DrawingRenderer, CanvasCalibration
  Extensions/                          ImagingBridgeExtensions,
                                       ColorBridgeExtensions,
                                       GeometryBridgeExtensions

src/CodeBrix.Imaging.Drawing.NoSkia/   the managed workalike engine plus the
                                       LINKED drawing-session sources
  Drawing*.cs                          the SkiaSharp-shaped public types
                                       (DrawingCanvas, DrawingBitmap,
                                       DrawingPaint, DrawingPath, ... )
  Pictures/                            the public display-list surface:
                                       DrawingPicture, DrawingCommand and its
                                       11 command records, DrawingTextStyle,
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
                                       shader/paint sources
  NoSkiaTypeAliases.cs                 the global using aliases (see below)
  NoSkiaInternalsVisibleTo.cs          grants internals to the .NoSkia.Svg
                                       assembly

src/CodeBrix.Imaging.Drawing.NoSkia.Svg/   the managed SVG renderer
  ShimSkiaSharp/, Model/, SceneGraph/  the VENDORED scene compiler (display
                                       list + model + scene graph)
  DrawingSvg.cs                        the public facade
  Rendering/                           the managed backend: NoSkia display-list
                                       replayer, ImagingSvgAssetLoader,
                                       NoSkiaFontRegistry, NoSkiaImageFilterFactory,
                                       NoSkiaTextRenderer

src/CodeBrix.Imaging.Drawing.NoSkia.Package/   packaging-only project (no code)

tests/                                 see TESTING below
samples/PainDiagram/                   reference application; see EXTRAS-README.txt

Root solutions:
  CodeBrix.Imaging.Drawing.slnx          cross-platform: the four src projects
                                         and the three test projects; builds with
                                         the plain .NET SDK on Linux/macOS/Windows
  CodeBrix.Imaging.Drawing.Windows.slnx  a Windows-host superset that additionally
                                         carries the sample's native WinUI 3 and
                                         WPF heads

The library layers cleanly: Models + Shapes -> Rendering -> DrawingSession
(facade) -> Extensions (the CodeBrix.Imaging bridge). Nothing in src/
references any UI framework, and nothing may start to.

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
- global.json at the root selects the Microsoft.Testing.Platform test runner;
  it is not an SDK pin.
- GeneratePackageOnBuild is TRUE on both packable projects, so an ordinary
  build produces .nupkg files (see PACKAGING AND PUBLISHING).
- The sample application is not in the root cross-platform solution; it has
  its own solutions under samples/PainDiagram (EXTRAS-README.txt).
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
          SvgFilterScaleTests
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
      and ParityTests projects. Not a project of its own.

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
to AGENT-README.txt. Package dependencies are SkiaSharp and
CodeBrix.Imaging.ApacheLicenseForever.

What the NoSkia package ships. src/CodeBrix.Imaging.Drawing.NoSkia and
src/CodeBrix.Imaging.Drawing.NoSkia.Svg both set IsPackable=false; the
packaging-only project src/CodeBrix.Imaging.Drawing.NoSkia.Package sets
IncludeBuildOutput=false (its own assembly is empty and excluded) and folds
the two referenced assemblies plus their XML documentation into
lib/<tfm> through the IncludeNoSkiaAssembliesInPackage target hooked onto
TargetsForTfmSpecificContentInPackage. The two ProjectReferences carry
PrivateAssets="all" so they do not surface as package dependencies; the
genuine dependencies (CodeBrix.Imaging.ApacheLicenseForever and
CodeBrix.SvgParse.MsplLicenseForever) are declared explicitly on the
packaging project so consumers restore them. It packs the same four root
files, with AGENT-README-NOSKIA.txt renamed inside the package to
AGENT-README.txt.

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

- tests/SvgAssets/Fonts/OpenSans-Regular.ttf is SIL OFL 1.1 (OFL.txt sits
  beside it) and is used solely to make SVG text rendering deterministic.

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
