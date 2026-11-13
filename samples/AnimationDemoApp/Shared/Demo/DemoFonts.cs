using System;
using System.IO;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo;

/// <summary>
/// The one font registry every scene's sessions share (through DrawingSessionOptions.Fonts): the bundled Roboto
/// (SIL OFL 1.1, see assets/Fonts/OFL.txt). Text never comes from a system font.
/// </summary>
internal static class DemoFonts
{
    private static readonly Lazy<FontRegistry> Shared = new Lazy<FontRegistry>(Load);

    /// <summary>The shared registry.</summary>
    public static FontRegistry Registry => Shared.Value;

    private static FontRegistry Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "Fonts", "Roboto-Regular.ttf");
        if (!File.Exists(path)) throw new FileNotFoundException("The bundled Roboto font is missing.", path);
        FontRegistry registry = new FontRegistry();
        registry.RegisterFont(path);
        return registry;
    }
}
