using SkiaSharp;
using CodeBrix.Imaging.Drawing.Models;

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// Initial settings for a <see cref="DrawingSession"/>. Every property has a sensible
/// default, so an options instance is only needed to override specific values.
/// </summary>
public sealed class DrawingSessionOptions
{
    /// <summary>
    /// The default size of the calibrated drawing space: 1000 x 1000.
    /// </summary>
    public static readonly SKSizeI DefaultCalibrationSize = new SKSizeI(1000, 1000);

    /// <summary>
    /// The size of the calibrated (device-independent) drawing space that strokes are
    /// stored in. Choose an aspect ratio matching the drawing's background image; the
    /// default square space suits square images. This value cannot change after the
    /// session is created.
    /// </summary>
    public SKSizeI CalibrationSize { get; set; } = DefaultCalibrationSize;

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
    public SKColor BackgroundFillColor { get; set; } = SKColors.Transparent;

    /// <summary>
    /// The color that the whole canvas is cleared to at the start of every render;
    /// transparent by default so the drawing can overlay other content (for example a
    /// live video frame drawn by the host application).
    /// </summary>
    public SKColor SurfaceClearColor { get; set; } = SKColors.Transparent;

    /// <summary>
    /// The width of newly drawn strokes, in calibrated drawing units.
    /// </summary>
    public float StrokeWidth { get; set; } = Stroke.DefaultWidth;
}
