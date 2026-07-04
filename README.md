# CodeBrix.Imaging.Drawing

A stroke-based drawing, painting and highlighting library for SkiaSharp canvases in .NET applications. CodeBrix.Imaging.Drawing captures pointer (mouse, pen, or touch) input as resolution-independent calibrated strokes on named, colored layers, renders them with translucent "highlighter" compositing over a background image — or over a transparent canvas above live content such as a webcam video feed — and exports finished drawings as PNG/JPEG images or CodeBrix.Imaging images. It works with any UI framework that can host a SkiaSharp drawing surface, including CodeBrix.Platform (all Skia heads), native WinUI 3, WPF, and .NET MAUI.
CodeBrix.Imaging.Drawing depends only on SkiaSharp and the CodeBrix.Imaging package, and is provided as a .NET 10 library and associated `CodeBrix.Imaging.Drawing.ApacheLicenseForever` NuGet package.

CodeBrix.Imaging.Drawing supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## CodeBrix.Imaging.Drawing supports:

* Interactive stroke drawing driven by simple pointer events (`PointerPressed` / `PointerMoved` / `PointerReleased`) forwarded from any UI framework's canvas control
* Resolution-independent stroke storage in a calibrated logical space, so drawings survive window resizing, DPI changes, and orientation flips
* Named, colored drawing layers (for example Pain / Numbness / Tingling) with translucent highlighter compositing — overlapping strokes within a layer never double-darken
* Annotating photos and images of any aspect ratio: `DrawingSession.CreateForImage(...)` takes an explicit calibration size — or an explicit `CalibrationSizing.DeriveFromBackgroundImage` choice that computes it from the image — and the parameterless export methods produce the annotated image at its original resolution with no distortion
* Drawing over a background image (such as a black-and-white line diagram), over a solid fill, or over a fully transparent canvas above externally rendered content such as a live video frame
* Incremental rendering with per-layer bitmap caches, so live drawing stays fast at large stroke counts
* Programmatic drawing primitives — lines, arrows, circles, ellipses, rectangles, and polylines/polygons (outline or filled) — using plain coordinates and CodeBrix.Imaging colors, with no SkiaSharp knowledge required; ideal for computer-vision-driven annotation of a live video feed
* Clear, per-layer clear, and undo-last-stroke operations (shapes and strokes share one undo history)
* Export to PNG or JPEG bytes, to a SkiaSharp `SKImage`, or to a CodeBrix.Imaging `Image<Rgba32>` for further processing

## Sample Code

### Drawing on a SkiaSharp canvas with highlighter layers

```csharp
using CodeBrix.Imaging;          // Color, Size
using CodeBrix.Imaging.Drawing;  // DrawingSession, DrawingLayer

var session = new DrawingSession();
session.SetBackgroundImage(File.ReadAllBytes("body_map.png"));
session.BackgroundFillColor = Color.White;

DrawingLayer pain = session.AddLayer("Pain", Color.FromRgb(255, 30, 230));
DrawingLayer numbness = session.AddLayer("Numbness", Color.FromRgb(30, 128, 204));
session.ActiveLayer = pain;

// Hook the session to your UI framework's Skia canvas control:
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

## License

The project is licensed under the Apache 2.0 License. see: https://en.wikipedia.org/wiki/Apache_License
