using SkiaSharp;
using CodeBrix.Imaging.Drawing.Models;

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// Initial settings for a <see cref="DrawingSession"/>. Every property has a sensible
/// default, so an options instance is only needed to override specific values. Colors and
/// sizes are expressed with the CodeBrix.Imaging <see cref="Color"/> and <see cref="Size"/>
/// types; callers holding SkiaSharp values can use the fluent <c>Set…</c> helpers instead
/// (and read the SkiaSharp equivalents via the <c>Get…AsSkia</c> methods).
/// </summary>
public sealed class DrawingSessionOptions
{
    /// <summary>
    /// The default size of the calibrated drawing space: 1000 x 1000.
    /// </summary>
    public static readonly Size DefaultCalibrationSize = new Size(1000, 1000);

    /// <summary>
    /// The size of the calibrated (device-independent) drawing space that strokes are
    /// stored in. Choose an aspect ratio matching the drawing's background image; the
    /// default square space suits square images. This value cannot change after the
    /// session is created.
    /// </summary>
    public Size CalibrationSize { get; set; } = DefaultCalibrationSize;

    /// <summary>
    /// The alpha (0-255) that completed layers are composited with; the default of 100
    /// produces the translucent highlighter effect. Use 255 for fully opaque painting.
    /// </summary>
    public byte LayerOpacity { get; set; } = 100;

    /// <summary>
    /// The alpha (0-255) that the in-progress stroke is drawn with; the default of 200 is
    /// slightly more vivid than settled layer content.
    /// </summary>
    public byte ActiveStrokeOpacity { get; set; } = 200;

    /// <summary>
    /// The color that the drawing rectangle is filled with before the background image and
    /// layers are drawn; transparent by default.
    /// </summary>
    public Color BackgroundFillColor { get; set; } = Color.Transparent;

    /// <summary>
    /// The color that the whole canvas is cleared to at the start of every render;
    /// transparent by default so the drawing can overlay other content (for example a
    /// live video frame drawn by the host application).
    /// </summary>
    public Color SurfaceClearColor { get; set; } = Color.Transparent;

    /// <summary>
    /// The width of newly drawn strokes, in calibrated drawing units.
    /// </summary>
    public float StrokeWidth { get; set; } = Stroke.DefaultWidth;

    /// <summary>
    /// Sets <see cref="CalibrationSize"/> from a SkiaSharp <see cref="SKSizeI"/>, for callers
    /// working in SkiaSharp types.
    /// </summary>
    /// <param name="calibrationSize">The calibrated drawing space.</param>
    /// <returns>This same options instance, so the call can be chained.</returns>
    public DrawingSessionOptions SetCalibrationSize(SKSizeI calibrationSize)
    {
        CalibrationSize = SkiaInterop.ToImaging(calibrationSize);
        return this;
    }

    /// <summary>
    /// Sets <see cref="BackgroundFillColor"/> from a SkiaSharp <see cref="SKColor"/>, for
    /// callers working in SkiaSharp types.
    /// </summary>
    /// <param name="color">The background fill color.</param>
    /// <returns>This same options instance, so the call can be chained.</returns>
    public DrawingSessionOptions SetBackgroundFillColor(SKColor color)
    {
        BackgroundFillColor = SkiaInterop.ToImaging(color);
        return this;
    }

    /// <summary>
    /// Sets <see cref="SurfaceClearColor"/> from a SkiaSharp <see cref="SKColor"/>, for
    /// callers working in SkiaSharp types.
    /// </summary>
    /// <param name="color">The surface clear color.</param>
    /// <returns>This same options instance, so the call can be chained.</returns>
    public DrawingSessionOptions SetSurfaceClearColor(SKColor color)
    {
        SurfaceClearColor = SkiaInterop.ToImaging(color);
        return this;
    }

    /// <summary>Gets <see cref="CalibrationSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The calibration size as a SkiaSharp size.</returns>
    public SKSizeI GetCalibrationSizeAsSkia() => SkiaInterop.ToSK(CalibrationSize);

    /// <summary>Gets <see cref="BackgroundFillColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The background fill color as a SkiaSharp color.</returns>
    public SKColor GetBackgroundFillColorAsSkia() => SkiaInterop.ToSK(BackgroundFillColor);

    /// <summary>Gets <see cref="SurfaceClearColor"/> as a SkiaSharp <see cref="SKColor"/>.</summary>
    /// <returns>The surface clear color as a SkiaSharp color.</returns>
    public SKColor GetSurfaceClearColorAsSkia() => SkiaInterop.ToSK(SurfaceClearColor);

    /// <summary>Gets <see cref="DefaultCalibrationSize"/> as a SkiaSharp <see cref="SKSizeI"/>.</summary>
    /// <returns>The default calibration size as a SkiaSharp size.</returns>
    public static SKSizeI GetDefaultCalibrationSizeAsSkia() => SkiaInterop.ToSK(DefaultCalibrationSize);
}
