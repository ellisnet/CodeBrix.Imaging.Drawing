# CodeBrix.Imaging.Drawing

A stroke-based drawing, painting and highlighting library for .NET applications, built on SkiaSharp. CodeBrix.Imaging.Drawing captures pointer (mouse, pen, or touch) input as resolution-independent calibrated strokes on named, colored layers, renders them with translucent "highlighter" compositing over a background image — or over a transparent canvas above live content such as a webcam video feed — and exports finished drawings as PNG/JPEG images or CodeBrix.Imaging images. It works with any UI framework that can host a Skia drawing surface, including CodeBrix.Platform (all Skia heads), native WinUI 3, WPF, and .NET MAUI. CodeBrix.Imaging.Drawing is provided as a .NET 10 library and associated `CodeBrix.Imaging.Drawing.ApacheLicenseForever` NuGet package.

A companion package, `CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever`, is **not yet published**. It offers the identical drawing-session API with no native libraries at all, adding a fully managed 2D drawing engine (`DrawingCanvas`, `DrawingBitmap`, `DrawingPaint`, `DrawingPath`, gradients, pattern fills, blend modes, clipping, save-layers) in the `CodeBrix.Imaging.Drawing.NoSkia` namespace and a fully managed SVG renderer (`DrawingSvg`, in `CodeBrix.Imaging.Drawing.NoSkia.Svg`) that rasterizes an SVG document at any scale with explicit font registration, so output never depends on system fonts. The two packages are either/or alternatives — they compile the same drawing-session sources into the same namespaces — so an application references one or the other, never both.

CodeBrix.Imaging.Drawing supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.Imaging.Drawing.ApacheLicenseForever
```

Note that the NuGet package ID and the namespaces are different - there is no package named plain `CodeBrix.Imaging.Drawing`:

* NuGet package ID: `CodeBrix.Imaging.Drawing.ApacheLicenseForever`
* Assembly and primary namespace: `CodeBrix.Imaging.Drawing` - i.e. `using CodeBrix.Imaging.Drawing;` - which holds `DrawingSession`, the entry point
* Additional namespaces: `CodeBrix.Imaging.Drawing.Models` (`DrawingLayer`, `Stroke` and the other model types), `CodeBrix.Imaging.Drawing.Shapes` (the geometric primitives), `CodeBrix.Imaging.Drawing.Rendering` (`DrawingRenderer`, `CanvasCalibration`) and `CodeBrix.Imaging.Drawing.Extensions` (the color, geometry and image bridge extensions)

XML documentation (IntelliSense) ships alongside the assembly.

The package pulls in the following automatically; no version pinning is needed in the consuming project:

* `SkiaSharp` - the rendering engine
* `CodeBrix.Imaging.ApacheLicenseForever` - colors, sizes, image decoding and encoding, and the `Image<Rgba32>` export type

## CodeBrix.Imaging.Drawing supports:

* Interactive stroke drawing driven by simple pointer events (`PointerPressed` / `PointerMoved` / `PointerReleased`) forwarded from any UI framework's canvas control
* Resolution-independent stroke storage in a calibrated logical space, so drawings survive window resizing, DPI changes, and orientation flips
* Named, colored drawing layers (for example Pain / Numbness / Tingling) with translucent highlighter compositing — overlapping strokes within a layer never double-darken
* Annotating photos and images of any aspect ratio: `DrawingSession.CreateForImage(...)` takes an explicit calibration size — or an explicit `CalibrationSizing.DeriveFromBackgroundImage` choice that computes it from the image — and the parameterless export methods produce the annotated image at its original resolution with no distortion
* Drawing over a background image (such as a black-and-white line diagram), over a solid fill, or over a fully transparent canvas above externally rendered content such as a live video frame
* Incremental rendering with per-layer bitmap caches, so live drawing stays fast at large stroke counts
* Programmatic drawing primitives — lines, arrows, circles, ellipses, rectangles, and polylines/polygons (outline or filled) — using plain coordinates and CodeBrix.Imaging colors, with no low-level graphics knowledge required; ideal for computer-vision-driven annotation of a live video feed
* Clear, per-layer clear, and undo-last-stroke operations (shapes and strokes share one undo history)
* Export to PNG or JPEG bytes, to an `SKImage`, or to a CodeBrix.Imaging `Image<Rgba32>` for further processing

## Native assets: one package your application must add

The SkiaSharp native binaries do not arrive transitively. An application built on CodeBrix.Platform gets them from its platform head package; a plain .NET application adds the SkiaSharp native-asset package for each platform it runs on - for example, a Linux console or service app adds:

```
dotnet add package SkiaSharp.NativeAssets.Linux
```

Use the `SkiaSharp.NativeAssets.macOS` or `SkiaSharp.NativeAssets.Win32` variant per platform. Without the matching package the project still compiles, and then fails at run time on the first drawing call with a native-library load error.

## Sample Code

### Drawing on a canvas with highlighter layers

```csharp
using CodeBrix.Imaging;                 // Color, Size
using CodeBrix.Imaging.Drawing;         // DrawingSession
using CodeBrix.Imaging.Drawing.Models;  // DrawingLayer

var session = new DrawingSession();
session.SetBackgroundImage(File.ReadAllBytes("body_map.png"));
session.BackgroundFillColor = Color.White;

DrawingLayer pain = session.AddLayer("Pain", Color.FromRgb(255, 30, 230));
DrawingLayer numbness = session.AddLayer("Numbness", Color.FromRgb(30, 128, 204));
session.ActiveLayer = pain;

// Hook the session to your UI framework's canvas control:
session.RedrawRequested += (s, e) => canvasControl.Invalidate();
// In the control's PaintSurface handler:
//   session.Render(e.Surface, e.Info);
// In the control's pointer handlers (left button):
//   session.PointerPressed(point, viewSize); session.PointerMoved(point, viewSize); session.PointerReleased();

// Save the finished drawing:
byte[] png = session.ExportPng(new Size(1000, 1000));
File.WriteAllBytes("highlighted_body_map.png", png);
```

### Annotating a photo (any aspect ratio)

```csharp
using CodeBrix.Imaging;          // Color, Size
using CodeBrix.Imaging.Drawing;  // DrawingSession, CalibrationSizing

// You explicitly choose the calibrated drawing space: pass a size, or ask for it
// to be derived from the photo's aspect ratio
var session = DrawingSession.CreateForImage(
    File.ReadAllBytes("car_photo.jpg"),
    CalibrationSizing.DeriveFromBackgroundImage);
session.AddLayer("Damage", Color.Red);

// ...wire pointer events and PaintSurface as above, draw on the photo...

// Exports at the photo's original resolution, never distorted
File.WriteAllBytes("car_photo_annotated.png", session.ExportPng());
```

The `samples/PainDiagram` folder of this repository contains a complete reference application that runs on every CodeBrix.Platform Skia head (Windows Win32 and WPF-hosted, Linux X11 / Wayland / framebuffer, macOS) plus native WinUI 3 and WPF heads, all sharing one view model.

## Documentation

The NuGet package includes `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against this library.

Additional sample code and usage examples are available in the `CodeBrix.Imaging.Drawing.Tests` project:
https://github.com/ellisnet/CodeBrix.Imaging.Drawing/tree/main/tests/CodeBrix.Imaging.Drawing.Tests

## License

CodeBrix.Imaging.Drawing is licensed under the Apache License 2.0 - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.Imaging.Drawing/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.Imaging.Drawing/blob/main/THIRD-PARTY-NOTICES.txt).
