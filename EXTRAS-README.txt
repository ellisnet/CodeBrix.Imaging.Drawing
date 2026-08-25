================================================================================
EXTRAS-README: CodeBrix.Imaging.Drawing
Samples, tools and other content in this repository that is not part of a NuGet package
================================================================================

This repository has one sample application and no tools/ folder. The only
other non-package content is the test tree (tests/), which is described in
MAINTAINER-README.txt.

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

There is no tools/ folder in this repository.

OPTIONAL TEST DATA
==================

tests/SvgAssets/ holds the sample SVG documents, their committed reference
PNGs and the OFL-licensed Open Sans font used to make SVG text rendering
deterministic. It is consumed by the test projects only and ships in no
package; see MAINTAINER-README.txt for how the references are generated and
refreshed.
