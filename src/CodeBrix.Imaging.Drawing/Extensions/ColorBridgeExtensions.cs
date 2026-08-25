#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Extensions;

#if NOSKIA
/// <summary>
/// Extension methods that convert between the CodeBrix.Imaging <see cref="Color"/> type
/// and the <see cref="DrawingColor"/> type used by this package's managed rendering
/// backend, so applications can work entirely with CodeBrix.Imaging colors while this
/// library renders them through the backend.
/// </summary>
public static class ColorBridgeExtensions
{
    /// <summary>
    /// Converts a CodeBrix.Imaging color to a backend color.
    /// </summary>
    /// <param name="color">The CodeBrix.Imaging color to convert.</param>
    /// <returns>The equivalent backend color.</returns>
    public static DrawingColor ToDrawingColor(this Color color) => GraphicsInterop.ToGraphics(color);

    /// <summary>
    /// Converts a backend color to a CodeBrix.Imaging color.
    /// </summary>
    /// <param name="color">The backend color to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging color.</returns>
    public static Color ToImagingColor(this DrawingColor color) => GraphicsInterop.ToImaging(color);
}
#else
/// <summary>
/// Extension methods that convert between the CodeBrix.Imaging <see cref="Color"/> type
/// and the SkiaSharp <see cref="SKColor"/> type, so applications can work entirely with
/// CodeBrix.Imaging colors while this library renders them through SkiaSharp.
/// </summary>
public static class ColorBridgeExtensions
{
    /// <summary>
    /// Converts a CodeBrix.Imaging color to a SkiaSharp color.
    /// </summary>
    /// <param name="color">The CodeBrix.Imaging color to convert.</param>
    /// <returns>The equivalent SkiaSharp color.</returns>
    public static SKColor ToSKColor(this Color color) => GraphicsInterop.ToGraphics(color);

    /// <summary>
    /// Converts a SkiaSharp color to a CodeBrix.Imaging color.
    /// </summary>
    /// <param name="color">The SkiaSharp color to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging color.</returns>
    public static Color ToImagingColor(this SKColor color) => GraphicsInterop.ToImaging(color);
}
#endif
