using CodeBrix.Imaging.PixelFormats;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// Internal, single-source conversions between the CodeBrix.Imaging value types
/// (<see cref="Color"/>, <see cref="Size"/>, <see cref="SizeF"/>, <see cref="Point"/>,
/// <see cref="PointF"/>, <see cref="RectangleF"/>) and their SkiaSharp counterparts. The
/// public bridge extension methods (<c>ColorBridgeExtensions</c>,
/// <c>GeometryBridgeExtensions</c>) delegate here so there is exactly one implementation of
/// each mapping, and the library's lower layers (models, shapes, rendering) use it directly
/// to avoid depending on the extensions namespace.
/// </summary>
internal static class SkiaInterop
{
    internal static SKColor ToSK(Color color)
    {
        Rgba32 rgba = color.ToPixel<Rgba32>();
        return new SKColor(rgba.R, rgba.G, rgba.B, rgba.A);
    }

    internal static SKColor? ToSK(Color? color) => color.HasValue ? ToSK(color.Value) : (SKColor?)null;

    internal static Color ToImaging(SKColor color) => Color.FromRgba(color.Red, color.Green, color.Blue, color.Alpha);

    internal static Color? ToImaging(SKColor? color) => color.HasValue ? ToImaging(color.Value) : (Color?)null;

    internal static SKSizeI ToSK(Size size) => new SKSizeI(size.Width, size.Height);

    internal static Size ToImaging(SKSizeI size) => new Size(size.Width, size.Height);

    internal static SKSize ToSK(SizeF size) => new SKSize(size.Width, size.Height);

    internal static SizeF ToImaging(SKSize size) => new SizeF(size.Width, size.Height);

    internal static SKPointI ToSK(Point point) => new SKPointI(point.X, point.Y);

    internal static Point ToImaging(SKPointI point) => new Point(point.X, point.Y);

    internal static SKPoint ToSK(PointF point) => new SKPoint(point.X, point.Y);

    internal static PointF ToImaging(SKPoint point) => new PointF(point.X, point.Y);

    //SKRect stores Left/Top/Right/Bottom; RectangleF stores X/Y/Width/Height - so the
    //  mapping is NOT a field-for-field copy
    internal static SKRect ToSK(RectangleF rect) => SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height);

    internal static RectangleF ToImaging(SKRect rect) => new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height);
}
