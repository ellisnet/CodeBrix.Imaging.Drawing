================================================================================
EXTRAS-README: CodeBrix.Imaging.Drawing
Samples, tools and other content in this repository that is not part of a NuGet package
================================================================================

The samples/ folder holds the sample applications described below - each
with its own solution, none of them part of the root solutions - and the
repository has no tools/ folder. The only other non-package content is the
test tree (tests/), which is described in MAINTAINER-README.txt.

  samples/PainDiagram             the reference application for hosting a
                                  drawing session in a UI (references the
                                  library by project path)
  samples/VenusRosetteAnimation   an animation rendered to stills and video
                                  with the animation-frame features
  samples/VanGoghPaintingDemo     a painterly animation: a C# port of a Python
                                  original, drawn with both packages
  samples/AnimationDemoApp        a scene-by-scene tour of library features,
                                  drawn with both packages side by side
  samples/AutumnAviana            a complete animation made with the NoSkia
                                  package alone: falling leaves heap into a
                                  word, raccoons roll in a second line; renders
                                  MP4 and transparent ProRes/WebM videos

PainDiagram references src/CodeBrix.Imaging.Drawing by project path; the
other samples consume the library as NuGet packages (PackageReference). To
run one of those against unpublished library changes, build the library in
Release (which packs the .nupkg files), copy the packages to a local folder,
set the sample's package versions to that build's version, and restore with
an extra --source for that folder. Never commit a nuget.config or a path to
such a folder.

THE PAINDIAGRAM SAMPLE APPLICATION (samples/PainDiagram)
========================================================

WHAT IT IS
----------
A complete reference application that replicates a clinical "draw your pain,
numbness and tingling on a body map" tablet workflow with the mouse. Three
highlighter layers - Pain (pink, RGB 255/30/230), Numbness (blue, RGB
30/128/204) and Tingling (yellow-gold, RGB 204/170/10) - are drawn over a
square 3100 x 3100 black-and-white body-map PNG. The toolbar offers Clear
(which asks for confirmation once more than two elements exist) and Save (a
file dialog, then a 1000 x 1000 PNG export, then an optional
clear-after-save prompt).

It exists to be copied: it is the template for building a new application on
CodeBrix.Imaging.Drawing, and it is the worked instance of the recipe in
AGENT-README-SKIA.txt (the MINIMUM VIABLE PROJECT and DrawingCanvas
sections). Every code-behind pattern quoted in that guide is taken from
here.

SOLUTIONS
---------
Two solutions, per the CodeBrix.Platform convention:

  samples/PainDiagram/PainDiagram.slnx
      Cross-platform. Builds with the plain .NET SDK on Linux, macOS and
      Windows, and carries all six CodeBrix.Platform Skia heads. It
      references the Skia library by project path
      (../../src/CodeBrix.Imaging.Drawing), not by package, so the sample
      always exercises the working tree.

  samples/PainDiagram/PainDiagram.Windows.slnx
      A superset that adds the native WinUI 3 and native WPF heads, which
      need Windows-only build tooling - open it on a Windows machine. Its
      solution platforms are restricted to x86/x64/ARM64 because the WinUI
      project declares no "Any CPU" platform, and Visual Studio would
      otherwise fail to map it.

The repository-root CodeBrix.Imaging.Drawing.Windows.slnx also carries the
sample's projects, for working on library and sample together on Windows.

PROJECT MAP (eight heads, ONE view model)
-----------------------------------------
  Shared/                              loose linked files, not a project:
    ViewModels/MainViewModel.cs        THE application logic, compiled into
                                       three different assemblies (Core,
                                       WinUI, Wpf)
    Drawing/DrawingCanvas.cs           the single-control-name trick file
                                       reproduced in AGENT-README-SKIA.txt
    Helpers/HostHelper.cs              IHostBuilderProvider over the Generic
                                       Host
    Helpers/FileDialogHelper.cs        RemoveEmptyPlaceholder, used by the
                                       single-overwrite-prompt save flow
    Assets/body_map_master.png         the 3100 x 3100 background image

  CodeBrixPlatform/PainDiagram.Core    class library: every common
                                       CodeBrix.Platform package reference,
                                       the linked Shared files, the embedded
                                       body map, and the ProjectReference to
                                       src/CodeBrix.Imaging.Drawing. It
                                       defines HAS_CODEBRIX,
                                       HAS_CODEBRIX_WINUI and
                                       HAS_CODEBRIXPLATFORM.
  CodeBrixPlatform/PainDiagram.UI      shared project (.shproj/.projitems):
                                       App.xaml(.cs) and
                                       Views/MainPage.xaml(.cs) - the XAML UI
                                       compiled into every Skia head
  CodeBrixPlatform/PainDiagram.<Head>  six thin executables: Win32Skia,
                                       WinWpfSkia, LinuxX11, LinuxWayland,
                                       LinuxFrameBuffer, MacOS. Each is about
                                       thirty lines: the XAML page glob, an
                                       Import of the shared .projitems, a
                                       ProjectReference to Core, and exactly
                                       ONE CodeBrix.Platform runtime package.
  PainDiagram.WinUI                    native WinUI 3 head with its own XAML
                                       copy and Views/Win32SaveFileDialog.cs
                                       (a COM IFileSaveDialog wrapper)
  PainDiagram.Wpf                      native WPF head with its own XAML copy

HOW TO RUN IT
-------------
On Linux, from samples/PainDiagram:

    dotnet run --project CodeBrixPlatform/PainDiagram.LinuxX11

Substitute PainDiagram.LinuxWayland, PainDiagram.LinuxFrameBuffer,
PainDiagram.MacOS, PainDiagram.Win32Skia or PainDiagram.WinWpfSkia for the
other Skia heads (each on its own OS). The two native heads build only on a
Windows host, from PainDiagram.Windows.slnx.

The frame-buffer head has no file dialogs, so its save path writes to a
default Pictures-folder location instead of prompting.

WHAT IT DEMONSTRATES
--------------------
- One DrawingSession owned by one view model, serving eight heads.
- The DrawingCanvas linked-source trick that gives every head's XAML the same
  <drawing:DrawingCanvas/> element and keeps `using SkiaSharp;` out of every
  code-behind.
- The critical wiring order: pages subscribe DataContextChanged BEFORE
  calling InitializeComponent(), because the XAML itself sets
  Page.DataContext during InitializeComponent. Subscribing afterwards means
  the handler never fires and none of the bridges get wired - the symptom is
  that drawing input is captured but nothing ever repaints.
- The two tiny bridge interfaces (IFileSaveBridge, ICanvasInvalidator) that
  keep UI types out of the view model, with RedrawRequested forwarded through
  InvalidateCanvas.
- The embedded-resource trick: the view model is compiled into three
  different assemblies, so all three embedding projects give the PNG the SAME
  explicit <LogicalName>, letting one line of view-model code find it
  everywhere.
- A save flow with exactly ONE overwrite prompt, owned by the view model,
  achieved by suppressing each head's native prompt: the CodeBrix.Platform
  picker's empty placeholder file is deleted when it is genuinely empty;
  native WinUI uses the COM dialog with FOS_OVERWRITEPROMPT cleared, because
  the WinRT picker's own prompt cannot be turned off; native WPF sets
  OverwritePrompt = false.
- The native WPF head targeting net10.0-windows10.0.19041.0 rather than bare
  net10.0-windows, because SkiaSharp.Views.WPF has no assets for the latter
  and would silently restore its .NET Framework assembly (NU1701).
- The WPF-hosted Skia head forcing the WPF host's software render surface to
  avoid airspace conflicts, not setting <UseWPF>, and using
  <EnableWindowsTargeting> so it still compiles inside the cross-platform
  solution on Linux and macOS.

The body-map image is the original intellectual property of the repository
author; see THIRD-PARTY-NOTICES.txt.

THE VENUS ROSETTE SAMPLE (samples/VenusRosetteAnimation)
========================================================

Earth and Venus circle the Sun and a thread joins them every three days; over
eight years the threads weave the five-petalled Venus rosette. One console
project (VenusRosetteAnimation.slnx) draws it with CodeBrix.Imaging.Drawing,
in a landscape and a portrait layout.

    dotnet run                                    # live, in an SDL window (CodeBrix.Sdl3)
    dotnet run -- --portrait                      # the portrait layout, live
    dotnet run -- --stills 0.4,6,13,20.97         # stills -> Output/stills
    dotnet run -- --stills 13 --portrait --safe-area   # a still with the safe-area mask
    dotnet run -- --render                        # both videos -> Output/*.mp4
    dotnet run -- --check                         # the layouts' safe-area check

It shows the animation-frame features in an ordinary animation: the sky's
radial gradient fill, the Sun and planets as radial-gradient glows, text from
the bundled Roboto font (assets/Fonts, SIL OFL), the thread colors from a
ColorRamp, and the fade-in as the session's whole-frame Opacity. It is built
on the animation model: the timing is a library AnimationTimeline with three
cues, every thread and conjunction ring is added once with a start time (so
seeking back needs no rebuilding and the live view draws only newly landed
threads while playing forward), the moving planets, the thread being drawn
and the counters are animated shapes, showing a moment is just setting the
session's Time, and the live window's play state is the library's
PlaybackController. Each video frame is exported as raw RGBA bytes and piped
straight into ffmpeg through CodeBrix.VideoProcessing (ffmpeg must be
installed and on the PATH). Each layout names a safe-area guide (broadcast action-safe for
landscape; the Instagram feed-preview crop combined with the ordinary-post
margins for portrait): the text is marked for checking, every run that makes
stills or videos prints the advisory findings, and --safe-area (or the S key
in the live window) shows the guide as a mask - never in a video.

THE VAN GOGH PAINTING SAMPLE (samples/VanGoghPaintingDemo)
==========================================================

A Van Gogh-style painted animation in portrait 1080x1920, ported to C# from a
Python script (kept in assets/Python). The solution VanGoghPaintingDemo.slnx
has two projects that compile the same painting code (Shared/Painting):

  VanGoghPaintingDemo          the application: the C# painting drawn with
                               CodeBrix.Imaging.Drawing, an SDL live window,
                               stills, and video through
                               CodeBrix.VideoProcessing; it can also run the
                               Python original in-process with CodeBrix.Python
  VanGoghPaintingDemo.NoSkia   the same painting code against
                               CodeBrix.Imaging.Drawing.NoSkia: a console
                               program that writes stills, or with --serve
                               answers frame requests over standard input and
                               output

One application cannot reference both drawing packages, so the main project
builds the NoSkia program and copies it into a "noskia" folder beside
itself, then starts it and asks it for frames. The default run shows the
Skia and NoSkia paintings side by side (Tab switches views); --engine takes
a comma-separated list of skia, noskia and python, and --stills and --render
work for any of them. The Python painter needs a CPython with numpy, scipy
and Pillow. The C# painting is built from the library's painterly tools: the
scene is painted per pixel into float plane images, its shapes come from
hard-edged session masks, the strokes are BrushStrokeShapes with an
ownership map (hard-edged, gaps filled from the nearest stroke) that gives
each stroke the mean colour beneath it and measures the bristle ridges in
each stroke's own axes, the seams, blur, bloom, vignette and tone curve are
plane operations, the paintings dissolve through a KeyframePlayer, the cue
sheet is an AnimationTimeline and the live window runs on a
PlaybackController. Only the scene's formulas, the palette and the stroke
layouts are the sample's own. Because nothing passes through a rasterizer,
the Skia and NoSkia frames are identical, every pixel.

THE ANIMATION DEMO SAMPLE (samples/AnimationDemoApp)
====================================================

A tour of library features scene by scene, in 1280x720, drawn with both
packages side by side. It has the same structure as the Van Gogh sample:

  Shared/Demo                  the scene code, written against the session
                               API and CodeBrix.Imaging types only, so it
                               compiles against either package; scenes live
                               in Shared/Demo/Scenes and are listed in
                               SceneCatalog.cs
  AnimationDemoApp             the application (CodeBrix.Imaging.Drawing,
                               CodeBrix.Sdl3 live window composed with
                               SkiaSharp, stills)
  AnimationDemoApp.NoSkia      the same scene code against the NoSkia
                               package: stills, or --serve for the main app

    dotnet run --project AnimationDemoApp                    # live: Skia | NoSkia
    dotnet run --project AnimationDemoApp -- --list          # the scenes and their times
    dotnet run --project AnimationDemoApp -- --stills 5,20   # stills -> Output/stills
    dotnet run --project AnimationDemoApp -- --scene text,fade --engine skia

Keys in the live window: Space pause/play, Left/Right seek one second (with
Shift: the previous or next scene), Home restart, D show/hide the debug
overlay (timecode, frame number, current scene), Tab switch view, Esc quit.
The play state is the library's PlaybackController over the catalog's
timeline (one cue per scene).
The scenes show every easing curve, color ramps beside gradient fills built
from the same stops, linear and radial gradient fills (spread modes, text
and polygons included), text alignment, tracking and measured widths,
safe-area presets as overlays (the default look, with the tint's opacity
turned up and down) with their check findings, the whole-frame
and per-layer fades over an opaque background and over transparency, and the
pixel planes: a per-pixel procedural fill, blur, bloom, vignette and tone
curve, session drawings exported as float images and as coverage and
visible-alpha masks, value,
gradient and fractal noise with seeded randomness, the frame transitions,
particles drawn with DrawParticles, and motion blur by sub-frame averaging;
then the animation model: a timeline with cues and markers, keyframe tracks
bound to properties (easing per segment, hold keys, loop, ping-pong),
element, group and layer transforms and opacity, time-keyed elements, layout
anchors serving a portrait and a landscape frame from one description, the
camera with per-layer parallax, the frame-sequence renderer on several
threads, and the playback controller with the debug overlay in an export;
then paths, pictures and effects: a flourish written on by trimming with a
pen riding its tip, motion along a path with tangent rotation, path and
glyph-outline morphing, sprites from sheets generated in code (loop,
ping-pong, hold, stepped by hand, nearest and bilinear, tinted), pictures
turned, faded, cropped and tinted, clips, mattes and plane masks,
shadows, glows and blurs on elements, groups and layers, effects following
a zooming, rolling camera and spinning cards whose shadows stay fixed in the
scene or turn with them, and paths combined as areas every frame (union,
intersection, difference, exclusive-or), a stroke turned into a fillable
outline as it draws on, a star inflated and deflated, and outlined text with
and without merged glyph contours; then text: kerned
and unkerned lines side by side with tracked text staying kerned, wrapped
paragraphs in all four alignments with their line metrics and a breathing
box trimmed with an ellipsis, a typewriter with a caret, words fading in and
words and lines arriving as staggered pieces, outlined, gradient and
drawn-on lettering and text riding a curve, and captions burned in from an
inline SRT text on a broadcast title-safe area; and, in the NoSkia package
only, SVG documents written in the sample (a gear train and a clock) drawn
as session elements, the gears and hands turned by element id and the
clock's ticks drawn on, the whole elements faded, moved and grown; and
the painterly tools: anti-aliased and hard-edged masks of one tiny drawing
magnified side by side with the pixels where they differ, an ownership map
of drifting brushstrokes before and after its gaps are filled, a moving
picture turned into a brushstroke mosaic by colour pickup, brush texture
built up step by step (flat colour, bristle ridges, a ramp along each
stroke, dark seams), and keyframe playback dissolving between paintings
made once a second; and the motion and formation tools: dots spiralling in
to fill a word centre first on a tapering schedule, leaves falling and
heaping up bottom-up into a heart and an arch, the letters of a word rolling
in along a swooping route and settling on their places beside a wheel that
both faces along its hilly road and rolls, motions that add or multiply
whatever their binding order, point queries (inside, edge distance, nearest
point, the gap between two shapes) over a moving field, layers switched on
and off by on/off tracks (a hidden matte still masking), and a frame cache
rendering ahead of the play head with its window drawn as a strip.
The plane panels come out byte-identical from both packages. It
bundles Roboto (assets/Fonts, SIL OFL) and has no video output. A scene
can declare that only one package can draw it; the other side then shows a
plain placeholder frame (the SVG scene's code is compiled only into the
NoSkia program, which defines the DEMO_NOSKIA symbol).

THE AUTUMN AVIANA SAMPLE (samples/AutumnAviana)
===============================================

Fall leaves drift down onto a white screen and heap up into piles that turn
out to spell AVIANA; the last leaf settles, the camera pans down onto three
raccoons already rolling the letters of "@ Øasys" in from the left - each
pushing a pair, the letters turning like wheels and settling upright - two
walk off to the right and the last runs off in a bounding lope, leaving both
lines centred. One console project (AutumnAviana.slnx) draws it with the
completely managed CodeBrix.Imaging.Drawing.NoSkia package only: no SkiaSharp
and no native drawing library (CodeBrix.Sdl3 opens the window, and
CodeBrix.VideoProcessing hands the frames to ffmpeg for the videos).

    dotnet run                                   # live, in an SDL window, in real time
    dotnet run -- --stills 5,20,40,50,58         # full-size stills -> Output/stills
    dotnet run -- --stills 47 --crop 900,560,700,330   # part of a frame (x,y,width,height)
    dotnet run -- --stills 50 --transparent      # on transparency instead of white
    dotnet run -- --sheet 2,14,26,40,52,64       # a contact sheet -> Output/sheets
    dotnet run -- --sheet 59,59.1,59.2 --crop 0,560,460,260 --follow 3
                                                 # a filmstrip whose crop moves with raccoon 3
    dotnet run -- --render ~/Temp                # the three videos (no folder: Output/videos)
    dotnet run -- --timing                       # frame times and a real-time playback test
    dotnet run -- --verify                       # every frame a pure function of its time
    dotnet run -- --sizes                        # paw reach check; tail and body extents
    dotnet run -- --lineup                       # the three raccoons side by side -> Output/stills

--render needs ffmpeg on the PATH. It draws the whole timeline at 1920x1080,
30 fps (in parallel, with the library's FrameSequenceRenderer) and pipes the
raw RGBA frames straight into ffmpeg - no image files - three times, into
files named AutumnAviana-<date-time>-1920x1080 plus: -white.mp4 (H.264,
yuv420p, on the white background), -alpha.mov (ProRes 4444 with alpha,
yuva444p10le) and -alpha.webm (VP9 with alpha, yuva420p). The transparent
ones are exported without the background, so the soft ground shadows come
through as semi-transparent dark pixels. To see a WebM's alpha with ffmpeg,
decode it with libvpx-vp9 (-c:v libvpx-vp9 before -i); the built-in VP9
decoder drops the alpha plane.

Keys in the live window: Space pause/play, Left/Right seek one second (with
Shift: five), Home restart, D show/hide the debug overlay (timecode, frame
number, active cues), Esc quit; the title shows the time.

It is a client animation of the 2D-animation toolkit rather than a feature
tour. All of its timing is one library AnimationTimeline of named cues
(Animation/Timeline.cs: the leaf fall, the still moment, the pan, each
raccoon's push, beat and exit, the hold), with the length one constant.
The leaves are paths with radial gradient fills (maple, oak, elm and birch
silhouettes made by uniting lobe shapes, their rims and veins turned into
fillable outlines once), seeded with DrawingRandom. Each settled leaf is a
time-keyed element on the piles layer, added in landing order, so the live
view draws only the leaves that have just landed; each falling leaf follows a
swaying, tumbling, flipping path worked backwards from where and when it
lands, carried by gusts of wind, so every moment is a pure function of the
time. The piles are letter areas built from thick round-capped strokes
(stroke-to-outline and path combining), spaced by their nearest points
(PathGeometry.Distance), and filled with the library's formation tools:
FormationTargets spots that arrive bottom-up the way a heap grows
(HeapArrivalOrder - a leg rises from its foot, a crossbar fills in from both
sides), an ArrivalSchedule that finishes exactly on the fall's last moment,
SwayingFallArrival paths, and BindFormation turning them into the settled
and falling elements - so the word emerges gradually. The camera is the
library camera with its pan bound to a track. The second line is laid out
by the text engine and each letter is its glyph outline from the layout; the
letters roll along straight paths with BindRollAlongPath (the turn always
matches the distance travelled, overshooting and rocking back upright). The
raccoons are articulated rigs of nested groups - body, head, eyes, a
segmented ringed tail, two-bone legs - posed each frame, each with its own
size and coat. Their fur is geometry made once: outlines cut into soft
tufts (never outside the old outline), a darker underfur between the tufts,
and short strands of lighter and darker hair. Footfalls are planted where
they land, driven by distance travelled, so planted feet never slide, and
each gait varies a little from step to step (seeded). The last raccoon's
bounding lope bends its spine: the torso is an AnimatedShape redrawn bent
each frame, with the head and tail on mounts that follow the bend. Head
turns, tail sway and flicks and blinks are bound tracks, and the tail's
stride-coupled follow-through is an additive binding on top of its sway. Layers with
nothing to show are hidden by on/off tracks (DrawingLayer.IsVisible). The
live view renders with the session's layer caches, and the camera pan, which
moves every pile leaf, is rendered ahead on background threads by the
library's FrameCache (the pan already shows the first raccoon at work; the
cache renders each frame at its own time, raccoons and all). It bundles
Roboto (assets/Fonts, SIL OFL).

RELATED SAMPLES IN OTHER REPOSITORIES
=====================================

WebcamPainter is not in this repository. It paints on a captured webcam still
with an open-palm hand gesture tracked through a webcam, and it is the
reference for driving this library from a computer-vision pipeline instead of
a mouse. It lives in the CodeBrix.Samples repository:

    https://github.com/ellisnet/CodeBrix.Samples

The recipe it demonstrates is written out in full, and is self-contained, in
the "Vision-driven painting" example of AGENT-README-SKIA.txt, so consuming
agents do not need the sample itself.

TOOLS
=====

There is no tools/ folder in this repository. The repository does carry one
maintenance script, next to the code it operates on:

  src/CodeBrix.Imaging.Drawing.NoSkia.Svg/internalize-surface.sh
      The re-vendor pass for the managed SVG assembly. It makes every
      top-level type in ShimSkiaSharp/, Model/, SceneGraph/ and Rendering/
      internal (NoSkiaFontRegistry is the one exception) and demotes their
      "///" doc comments to "//", so the packaged documentation file cannot
      leak vendored type names. It is idempotent. The full recipe for using
      it - and the test that guards the result - is in MAINTAINER-README.txt
      under PROVENANCE AND VENDORED SOURCES.

  src/CodeBrix.Imaging.Drawing.NoSkia.Svg/VENDORED-NOTICES.txt
      The notices carried forward with that vendored code. It is not a tool,
      but it lives in the same folder and is maintained with it.

OPTIONAL TEST DATA
==================

tests/SvgAssets/ holds the sample SVG documents, their committed reference
PNGs and the OFL-licensed Open Sans font used to make SVG text rendering
deterministic, plus the OFL-licensed Roboto font the text tests use for
kerning (Fonts/OFL-Roboto.txt is its licence). It is consumed by the test
projects only and ships in no package; see MAINTAINER-README.txt for how the references are generated and
refreshed.
