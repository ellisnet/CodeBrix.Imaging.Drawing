# PainDiagram

The reference application for the **CodeBrix.Imaging.Drawing** library. It replicates the "draw your pain, numbness and tingling on a body map" workflow of an earlier clinical tablet application — except that drawing is done with the mouse (left button) instead of a finger.

Pick **Pain**, **Numbness**, or **Tingling**, draw on the body map (each type renders as a different translucent highlighter color, and overlapping strokes of one type never double-darken), then **Save** the finished diagram as a PNG image via the platform's file-save dialog. **Clear** starts over (with a confirmation once a few strokes exist).

All heads share one `MainViewModel` (the CodeBrix "Simple" MVVM API), one `DrawingSession` from the library, and the same embedded body-map image.

## Solutions

- **`PainDiagram.slnx`** — the cross-platform solution: the CodeBrix.Platform heads (Windows Win32, Windows WPF-hosted, Linux X11, Linux native Wayland, Linux framebuffer, macOS). Builds with the plain .NET 10 SDK on any OS.
- **`PainDiagram.Windows.slnx`** — everything above plus the native **WinUI 3** and native **WPF** heads, which need Windows-host-only build tooling. Open this one on a Windows machine.

## Projects

| Project | What it is |
|---|---|
| `Shared/` | File-linked sources: `MainViewModel`, helpers, and the body-map image |
| `CodeBrixPlatform/PainDiagram.Core` | Class library holding the common CodeBrix.Platform package references and the linked shared code |
| `CodeBrixPlatform/PainDiagram.UI` | Shared project (.shproj) holding the XAML UI compiled into every Skia head |
| `CodeBrixPlatform/PainDiagram.<Head>` | Six thin per-platform executables (Win32Skia, WinWpfSkia, LinuxX11, LinuxWayland, LinuxFrameBuffer, MacOS) |
| `PainDiagram.WinUI` | Native WinUI 3 / Windows App SDK head (uses `SkiaSharp.Views.WinUI`) |
| `PainDiagram.Wpf` | Native WPF head (uses `SkiaSharp.Views.WPF`) |

## Running

```bash
# Linux (X11 or XWayland)
dotnet run --project CodeBrixPlatform/PainDiagram.LinuxX11

# Linux (native Wayland)
dotnet run --project CodeBrixPlatform/PainDiagram.LinuxWayland

# macOS
dotnet run --project CodeBrixPlatform/PainDiagram.MacOS

# Windows (from a Windows machine)
dotnet run --project CodeBrixPlatform/PainDiagram.Win32Skia
```

Note: the Linux framebuffer head has no file-save dialog; it saves diagrams to your Pictures folder automatically.
