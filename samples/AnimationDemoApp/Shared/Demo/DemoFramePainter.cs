using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo;

/// <summary>
/// The demo's scenes drawn by whichever drawing package this build of the shared code references: the main
/// application builds it against CodeBrix.Imaging.Drawing ("skia"), the NoSkia program against
/// CodeBrix.Imaging.Drawing.NoSkia ("noskia"). A scene that the package cannot draw is replaced by a plain
/// placeholder frame naming the package that can.
/// </summary>
internal sealed class DemoFramePainter : IFramePainter
{
    private readonly SceneCatalog catalog;

    /// <summary>Creates the painter and its scenes.</summary>
    /// <param name="name">Which drawing package this build of the code uses: "skia" or "noskia".</param>
    /// <param name="only">The scenes to play (comma-separated short names); or null for all.</param>
    public DemoFramePainter(string name, string only = null)
    {
        Name = name;
        catalog = new SceneCatalog(only);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>The scenes this painter plays.</summary>
    public SceneCatalog Catalog => catalog;

    /// <inheritdoc />
    public void RenderFrame(double seconds, Span<byte> rgba)
    {
        (int index, double local) = catalog.At(seconds);
        DemoScene scene = catalog.Scenes[index];
        bool available = scene.Availability switch
        {
            SceneAvailability.SkiaOnly => Name == "skia",
            SceneAvailability.NoSkiaOnly => Name == "noskia",
            _ => true,
        };

        if (available)
        {
            scene.Render(local, rgba);
        }
        else
        {
            RenderPlaceholder(scene, rgba);
        }
    }

    /// <summary>Releases the scenes.</summary>
    public void Dispose() => catalog.Dispose();

    private void RenderPlaceholder(DemoScene scene, Span<byte> rgba)
    {
        using DrawingSession session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(DemoFrame.Width, DemoFrame.Height),
            LayerOpacity = 255,
            BackgroundFillColor = Color.FromRgb(44, 46, 54),
            Fonts = DemoFonts.Registry,
        });
        session.AddLayer("Placeholder", Color.FromRgb(200, 200, 210));
        string package = scene.Availability == SceneAvailability.NoSkiaOnly ? "the NoSkia package" : "the Skia package";
        session.DrawText(scene.Title, DemoFrame.Width / 2f, 330, 36, null, TextAlign.Center);
        session.DrawText("This scene is drawn only by " + package + ".", DemoFrame.Width / 2f, 390, 20, Color.FromRgb(150, 152, 166), TextAlign.Center);
        session.ExportPixels(rgba, new Size(DemoFrame.Width, DemoFrame.Height));
    }
}
