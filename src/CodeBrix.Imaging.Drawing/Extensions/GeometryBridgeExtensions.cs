using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Extensions;

/// <summary>
/// Extension methods that convert between the CodeBrix.Imaging geometry value types
/// (<see cref="Size"/>, <see cref="SizeF"/>, <see cref="Point"/>, <see cref="PointF"/>,
/// <see cref="RectangleF"/>) and the equivalent SkiaSharp types, so applications can work
/// entirely with CodeBrix.Imaging types while this library renders and calibrates through
/// SkiaSharp. These are the companions of the color conversions in
/// <see cref="ColorBridgeExtensions"/>.
/// </summary>
public static class GeometryBridgeExtensions
{
    /// <summary>Converts a CodeBrix.Imaging integer size to a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <param name="size">The CodeBrix.Imaging size to convert.</param>
    /// <returns>The equivalent SkiaSharp size.</returns>
    public static SKSizeI ToSKSizeI(this Size size) => SkiaInterop.ToSK(size);

    /// <summary>Converts a SkiaSharp <see cref="SKSizeI"/> to a CodeBrix.Imaging <see cref="Size"/>.</summary>
    /// <param name="size">The SkiaSharp size to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging size.</returns>
    public static Size ToImagingSize(this SKSizeI size) => SkiaInterop.ToImaging(size);

    /// <summary>Converts a CodeBrix.Imaging floating-point size to a SkiaSharp <see cref="SKSize"/>.</summary>
    /// <param name="size">The CodeBrix.Imaging size to convert.</param>
    /// <returns>The equivalent SkiaSharp size.</returns>
    public static SKSize ToSKSize(this SizeF size) => SkiaInterop.ToSK(size);

    /// <summary>Converts a SkiaSharp <see cref="SKSize"/> to a CodeBrix.Imaging <see cref="SizeF"/>.</summary>
    /// <param name="size">The SkiaSharp size to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging size.</returns>
    public static SizeF ToImagingSizeF(this SKSize size) => SkiaInterop.ToImaging(size);

    /// <summary>Converts a CodeBrix.Imaging integer point to a SkiaSharp <see cref="SKPointI"/>.</summary>
    /// <param name="point">The CodeBrix.Imaging point to convert.</param>
    /// <returns>The equivalent SkiaSharp point.</returns>
    public static SKPointI ToSKPointI(this Point point) => SkiaInterop.ToSK(point);

    /// <summary>Converts a SkiaSharp <see cref="SKPointI"/> to a CodeBrix.Imaging <see cref="Point"/>.</summary>
    /// <param name="point">The SkiaSharp point to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging point.</returns>
    public static Point ToImagingPoint(this SKPointI point) => SkiaInterop.ToImaging(point);

    /// <summary>Converts a CodeBrix.Imaging floating-point point to a SkiaSharp <see cref="SKPoint"/>.</summary>
    /// <param name="point">The CodeBrix.Imaging point to convert.</param>
    /// <returns>The equivalent SkiaSharp point.</returns>
    public static SKPoint ToSKPoint(this PointF point) => SkiaInterop.ToSK(point);

    /// <summary>Converts a SkiaSharp <see cref="SKPoint"/> to a CodeBrix.Imaging <see cref="PointF"/>.</summary>
    /// <param name="point">The SkiaSharp point to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging point.</returns>
    public static PointF ToImagingPointF(this SKPoint point) => SkiaInterop.ToImaging(point);

    /// <summary>
    /// Converts a CodeBrix.Imaging <see cref="RectangleF"/> (X/Y/Width/Height) to a SkiaSharp
    /// <see cref="SKRect"/> (Left/Top/Right/Bottom).
    /// </summary>
    /// <param name="rect">The CodeBrix.Imaging rectangle to convert.</param>
    /// <returns>The equivalent SkiaSharp rectangle.</returns>
    public static SKRect ToSKRect(this RectangleF rect) => SkiaInterop.ToSK(rect);

    /// <summary>
    /// Converts a SkiaSharp <see cref="SKRect"/> (Left/Top/Right/Bottom) to a CodeBrix.Imaging
    /// <see cref="RectangleF"/> (X/Y/Width/Height).
    /// </summary>
    /// <param name="rect">The SkiaSharp rectangle to convert.</param>
    /// <returns>The equivalent CodeBrix.Imaging rectangle.</returns>
    public static RectangleF ToImagingRectangleF(this SKRect rect) => SkiaInterop.ToImaging(rect);
}
