using System;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Rendering;

/// <summary>
/// Coordinate-mapping helpers that translate between view coordinates (the size of the
/// on-screen control), canvas pixel coordinates (the size of the Skia surface), and
/// calibrated drawing coordinates (the fixed logical space that strokes are stored in).
/// Because strokes live in the calibrated space, a drawing survives any change of control
/// size, pixel density, or window orientation.
/// </summary>
public static class CanvasCalibration
{
    /// <summary>
    /// Computes the rectangle, in canvas pixel coordinates, that the drawing occupies on a
    /// canvas of the given size: the largest rectangle with the calibration space's aspect
    /// ratio that fits the canvas, centered (i.e. an aspect-fit rectangle).
    /// </summary>
    /// <param name="canvasSize">The size of the canvas, in pixels.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <returns>
    /// The centered aspect-fit drawing rectangle; or <see cref="SKRect.Empty"/> when either
    /// size has a zero or negative dimension.
    /// </returns>
    public static SKRect GetDrawingRect(SKSizeI canvasSize, SKSizeI calibrationSize)
    {
        if (canvasSize.Width < 1 || canvasSize.Height < 1
            || calibrationSize.Width < 1 || calibrationSize.Height < 1)
        {
            return SKRect.Empty;
        }

        float scale = Math.Min(
            canvasSize.Width / (float)calibrationSize.Width,
            canvasSize.Height / (float)calibrationSize.Height);

        float width = calibrationSize.Width * scale;
        float height = calibrationSize.Height * scale;
        float left = (canvasSize.Width - width) / 2f;
        float top = (canvasSize.Height - height) / 2f;

        return new SKRect(left, top, left + width, top + height);
    }

    /// <summary>
    /// Computes the centered aspect-fit drawing rectangle for a view (or canvas) of the
    /// given fractional size - the floating-point companion of
    /// <see cref="GetDrawingRect(SKSizeI, SKSizeI)"/> for callers holding logical view
    /// sizes rather than integer pixel sizes.
    /// </summary>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <returns>
    /// The centered aspect-fit drawing rectangle; or <see cref="SKRect.Empty"/> when either
    /// size has a zero or negative dimension.
    /// </returns>
    public static SKRect GetDrawingRect(SKSize viewSize, SKSizeI calibrationSize)
    {
        if (viewSize.Width <= 0 || viewSize.Height <= 0
            || calibrationSize.Width < 1 || calibrationSize.Height < 1)
        {
            return SKRect.Empty;
        }

        float scale = Math.Min(
            viewSize.Width / calibrationSize.Width,
            viewSize.Height / calibrationSize.Height);

        float width = calibrationSize.Width * scale;
        float height = calibrationSize.Height * scale;
        float left = (viewSize.Width - width) / 2f;
        float top = (viewSize.Height - height) / 2f;

        return new SKRect(left, top, left + width, top + height);
    }

    /// <summary>
    /// Computes the centered aspect-fit drawing rectangle for a view (or canvas) of the
    /// given fractional size - the CodeBrix.Imaging-typed companion of
    /// <see cref="GetDrawingRect(SKSize, SKSizeI)"/>.
    /// </summary>
    /// <param name="viewSize">The size of the view or canvas.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <returns>
    /// The centered aspect-fit drawing rectangle; or an empty rectangle when either size
    /// has a zero or negative dimension.
    /// </returns>
    public static RectangleF GetDrawingRect(SizeF viewSize, Size calibrationSize)
        => SkiaInterop.ToImaging(GetDrawingRect(SkiaInterop.ToSK(viewSize), SkiaInterop.ToSK(calibrationSize)));

    /// <summary>
    /// Maps a point in view coordinates (relative to an on-screen control of the given
    /// logical size) to calibrated drawing coordinates.
    /// </summary>
    /// <param name="viewPoint">The point, in the control's logical coordinates.</param>
    /// <param name="viewSize">The logical size of the control.</param>
    /// <param name="canvasSize">The pixel size of the Skia canvas that the control hosts.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <param name="clampToDrawingArea">
    /// When <c>true</c>, a point outside the drawing rectangle is clamped to its nearest
    /// edge (useful while a stroke is in progress and the pointer leaves the drawing area);
    /// when <c>false</c>, such a point returns <c>null</c>.
    /// </param>
    /// <returns>
    /// The calibrated point; or <c>null</c> when the sizes are unusable or the point falls
    /// outside the drawing rectangle and <paramref name="clampToDrawingArea"/> is <c>false</c>.
    /// </returns>
    public static SKPointI? ViewPointToCalibrated(
        SKPoint viewPoint,
        SKSize viewSize,
        SKSizeI canvasSize,
        SKSizeI calibrationSize,
        bool clampToDrawingArea = false)
    {
        if (viewSize.Width <= 0 || viewSize.Height <= 0) { return null; }

        SKRect drawingRect = GetDrawingRect(canvasSize, calibrationSize);
        if (drawingRect.IsEmpty) { return null; }

        //Scale from view (logical) coordinates to canvas (pixel) coordinates
        var canvasPoint = new SKPoint(
            (viewPoint.X / viewSize.Width) * canvasSize.Width,
            (viewPoint.Y / viewSize.Height) * canvasSize.Height);

        if (!clampToDrawingArea && !drawingRect.Contains(canvasPoint))
        {
            return null;
        }

        float relativeX = (canvasPoint.X - drawingRect.Left) / drawingRect.Width;
        float relativeY = (canvasPoint.Y - drawingRect.Top) / drawingRect.Height;

        int calibratedX = (int)Math.Round(relativeX * calibrationSize.Width, MidpointRounding.AwayFromZero);
        int calibratedY = (int)Math.Round(relativeY * calibrationSize.Height, MidpointRounding.AwayFromZero);

        return new SKPointI(
            Math.Clamp(calibratedX, 0, calibrationSize.Width),
            Math.Clamp(calibratedY, 0, calibrationSize.Height));
    }

    /// <summary>
    /// Maps a calibrated drawing point to canvas pixel coordinates within the given
    /// drawing rectangle.
    /// </summary>
    /// <param name="calibratedPoint">The point, in calibrated drawing coordinates.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <param name="drawingRect">
    /// The rectangle, in canvas pixel coordinates, that the drawing occupies - typically
    /// obtained from <see cref="GetDrawingRect(SKSizeI, SKSizeI)"/>.
    /// </param>
    /// <returns>The canvas pixel position of the point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="calibrationSize"/> has a zero or negative dimension.
    /// </exception>
    public static SKPoint CalibratedToCanvas(SKPointI calibratedPoint, SKSizeI calibrationSize, SKRect drawingRect)
    {
        if (calibrationSize.Width < 1 || calibrationSize.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(calibrationSize));
        }

        return new SKPoint(
            drawingRect.Left + ((calibratedPoint.X / (float)calibrationSize.Width) * drawingRect.Width),
            drawingRect.Top + ((calibratedPoint.Y / (float)calibrationSize.Height) * drawingRect.Height));
    }

    /// <summary>
    /// Scales a stroke width from calibrated drawing units to canvas pixels, proportional to
    /// the width of the drawing rectangle.
    /// </summary>
    /// <param name="calibratedWidth">The stroke width, in calibrated drawing units.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <param name="drawingRect">The drawing rectangle, in canvas pixel coordinates.</param>
    /// <returns>The stroke width in canvas pixels; never less than 1 pixel for a positive input width.</returns>
    public static float ScaleStrokeWidth(float calibratedWidth, SKSizeI calibrationSize, SKRect drawingRect)
    {
        if (calibratedWidth <= 0 || calibrationSize.Width < 1 || drawingRect.Width <= 0)
        {
            return calibratedWidth;
        }

        return Math.Max(1f, calibratedWidth * (drawingRect.Width / calibrationSize.Width));
    }

    /// <summary>
    /// Computes the centered aspect-fit drawing rectangle for a canvas of the given size -
    /// the CodeBrix.Imaging-typed companion of
    /// <see cref="GetDrawingRect(SKSizeI, SKSizeI)"/>.
    /// </summary>
    /// <param name="canvasSize">The size of the canvas, in pixels.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <returns>
    /// The centered aspect-fit drawing rectangle; or an empty rectangle when either size has
    /// a zero or negative dimension.
    /// </returns>
    public static RectangleF GetDrawingRect(Size canvasSize, Size calibrationSize)
        => SkiaInterop.ToImaging(GetDrawingRect(SkiaInterop.ToSK(canvasSize), SkiaInterop.ToSK(calibrationSize)));

    /// <summary>
    /// Maps a point in view coordinates to calibrated drawing coordinates - the
    /// CodeBrix.Imaging-typed companion of
    /// <see cref="ViewPointToCalibrated(SKPoint, SKSize, SKSizeI, SKSizeI, bool)"/>.
    /// </summary>
    /// <param name="viewPoint">The point, in the control's logical coordinates.</param>
    /// <param name="viewSize">The logical size of the control.</param>
    /// <param name="canvasSize">The pixel size of the Skia canvas that the control hosts.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <param name="clampToDrawingArea">
    /// When <c>true</c>, a point outside the drawing rectangle is clamped to its nearest edge;
    /// when <c>false</c>, such a point returns <c>null</c>.
    /// </param>
    /// <returns>
    /// The calibrated point; or <c>null</c> when the sizes are unusable or the point falls
    /// outside the drawing rectangle and <paramref name="clampToDrawingArea"/> is <c>false</c>.
    /// </returns>
    public static Point? ViewPointToCalibrated(
        PointF viewPoint,
        SizeF viewSize,
        Size canvasSize,
        Size calibrationSize,
        bool clampToDrawingArea = false)
    {
        SKPointI? calibrated = ViewPointToCalibrated(
            SkiaInterop.ToSK(viewPoint), SkiaInterop.ToSK(viewSize),
            SkiaInterop.ToSK(canvasSize), SkiaInterop.ToSK(calibrationSize), clampToDrawingArea);
        return calibrated.HasValue ? SkiaInterop.ToImaging(calibrated.Value) : (Point?)null;
    }

    /// <summary>
    /// Maps a calibrated drawing point to canvas pixel coordinates - the CodeBrix.Imaging-typed
    /// companion of <see cref="CalibratedToCanvas(SKPointI, SKSizeI, SKRect)"/>.
    /// </summary>
    /// <param name="calibratedPoint">The point, in calibrated drawing coordinates.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <param name="drawingRect">
    /// The rectangle, in canvas pixel coordinates, that the drawing occupies - typically
    /// obtained from <see cref="GetDrawingRect(Size, Size)"/>.
    /// </param>
    /// <returns>The canvas pixel position of the point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="calibrationSize"/> has a zero or negative dimension.
    /// </exception>
    public static PointF CalibratedToCanvas(Point calibratedPoint, Size calibrationSize, RectangleF drawingRect)
        => SkiaInterop.ToImaging(CalibratedToCanvas(
            SkiaInterop.ToSK(calibratedPoint), SkiaInterop.ToSK(calibrationSize), SkiaInterop.ToSK(drawingRect)));

    /// <summary>
    /// Scales a stroke width from calibrated drawing units to canvas pixels - the
    /// CodeBrix.Imaging-typed companion of
    /// <see cref="ScaleStrokeWidth(float, SKSizeI, SKRect)"/>.
    /// </summary>
    /// <param name="calibratedWidth">The stroke width, in calibrated drawing units.</param>
    /// <param name="calibrationSize">The size of the calibrated drawing space.</param>
    /// <param name="drawingRect">The drawing rectangle, in canvas pixel coordinates.</param>
    /// <returns>The stroke width in canvas pixels; never less than 1 pixel for a positive input width.</returns>
    public static float ScaleStrokeWidth(float calibratedWidth, Size calibrationSize, RectangleF drawingRect)
        => ScaleStrokeWidth(calibratedWidth, SkiaInterop.ToSK(calibrationSize), SkiaInterop.ToSK(drawingRect));
}
