using CodeBrix.Imaging.PixelFormats;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Extensions;

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
    public static SKColor ToSKColor(this Color color)
    {
        Rgba32 rgba = color.ToPixel<Rgba32>();
        return new SKColor(rgba.R, rgba.G, rgba.B, rgba.A);
    }

    /// <summary>
    /// Converts a SkiaSharp color to a CodeBrix.Imaging color.
    /// </summary>
    /// <param name="color">The SkiaSharp color to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging color.</returns>
    public static Color ToImagingColor(this SKColor color)
        => Color.FromRgba(color.Red, color.Green, color.Blue, color.Alpha);
}
