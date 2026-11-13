using System;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Imaging.Drawing.SafeAreas;

namespace CodeBrix.Imaging.Drawing.Layout;

/// <summary>
/// Places things relative to the frame or to a safe-area guide's safe rectangle, in
/// calibrated drawing units - so ONE scene description (anchors, offsets, fractions) serves a
/// portrait and a landscape frame alike. Create one per frame format:
/// <c>new DrawingLayout(session.CalibrationSize, SafeAreaGuides.InstagramReelsAd)</c>, then
/// <see cref="Place(LayoutPosition)"/> points and <see cref="PlaceBox(SizeF, LayoutPosition)"/>
/// boxes. Immutable and thread-safe.
/// <para>
/// The safe rectangle is the guide's <see cref="SafeAreaGuide.GetSafeRectangle(SizeF, double?, double?)"/>
/// on the export frame, mapped back into calibrated units through the drawing's centered
/// aspect-fit rectangle (exactly as an export places the drawing); with no guide it is the
/// whole frame.
/// </para>
/// </summary>
public sealed class DrawingLayout
{
    /// <summary>
    /// Creates a layout with no safe-area guide (the safe area is the whole frame).
    /// </summary>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1.</exception>
    public DrawingLayout(Size calibrationSize)
        : this(calibrationSize, null)
    {
    }

    /// <summary>
    /// Creates a layout against a safe-area guide.
    /// </summary>
    /// <param name="calibrationSize">The calibrated drawing space; both dimensions positive.</param>
    /// <param name="guide">The guide whose safe rectangle <see cref="LayoutReference.SafeArea"/> means; or <c>null</c> for the whole frame.</param>
    /// <param name="frameSize">The export frame size the guide is laid out on; or <c>null</c> for the calibration size.</param>
    /// <param name="seconds">The moment, for guides with a time rule; or <c>null</c> (every rule applies).</param>
    /// <param name="duration">The video's length, for "final seconds" rules; or <c>null</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension of either size is less than 1.</exception>
    public DrawingLayout(Size calibrationSize, SafeAreaGuide guide, Size? frameSize = null, double? seconds = null, double? duration = null)
    {
        if (calibrationSize.Width < 1 || calibrationSize.Height < 1) { throw new ArgumentOutOfRangeException(nameof(calibrationSize)); }
        Size frame = frameSize ?? calibrationSize;
        if (frame.Width < 1 || frame.Height < 1) { throw new ArgumentOutOfRangeException(nameof(frameSize)); }

        CalibrationSize = calibrationSize;
        FrameSize = frame;
        Guide = guide;
        Frame = new RectangleF(0, 0, calibrationSize.Width, calibrationSize.Height);

        if (guide == null)
        {
            SafeArea = Frame;
            return;
        }

        RectangleF safe = guide.GetSafeRectangle(new SizeF(frame.Width, frame.Height), seconds, duration);
        RectangleF drawingRect = CanvasCalibration.GetDrawingRect(frame, calibrationSize);
        float scale = drawingRect.Width / calibrationSize.Width;
        SafeArea = safe.Width <= 0 || safe.Height <= 0 || !(scale > 0f)
            ? RectangleF.Empty
            : new RectangleF((safe.X - drawingRect.X) / scale, (safe.Y - drawingRect.Y) / scale, safe.Width / scale, safe.Height / scale);
    }

    /// <summary>The calibrated drawing space.</summary>
    public Size CalibrationSize { get; }

    /// <summary>The export frame size the guide is laid out on.</summary>
    public Size FrameSize { get; }

    /// <summary>The safe-area guide; or <c>null</c> when there is none.</summary>
    public SafeAreaGuide Guide { get; }

    /// <summary>The whole frame in calibrated units: (0, 0, width, height).</summary>
    public RectangleF Frame { get; }

    /// <summary>
    /// The safe rectangle in calibrated units (the whole frame without a guide;
    /// <see cref="RectangleF.Empty"/> when the guide leaves nothing safe).
    /// </summary>
    public RectangleF SafeArea { get; }

    /// <summary>Indicates whether the frame is taller than it is wide.</summary>
    public bool IsPortrait => CalibrationSize.Height > CalibrationSize.Width;

    /// <summary>The shorter side of the frame, calibrated units - a size unit that suits both orientations.</summary>
    public float ShortSide => Math.Min(CalibrationSize.Width, CalibrationSize.Height);

    /// <summary>The rectangle a reference names.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns><see cref="Frame"/> or <see cref="SafeArea"/>.</returns>
    public RectangleF GetRectangle(LayoutReference reference) => reference == LayoutReference.SafeArea ? SafeArea : Frame;

    /// <summary>
    /// Resolves a position to a point in calibrated units: the anchor point of the reference
    /// rectangle, plus the fraction offset (of that rectangle's size), plus the unit offset.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns>The point.</returns>
    public PointF Place(LayoutPosition position)
    {
        RectangleF reference = GetRectangle(position.Reference);
        PointF anchor = position.Anchor.PointIn(reference);
        return new PointF(
            anchor.X + (position.FractionOffset.X * reference.Width) + position.Offset.X,
            anchor.Y + (position.FractionOffset.Y * reference.Height) + position.Offset.Y);
    }

    /// <summary>Resolves an anchor of a reference rectangle, moved by calibrated units.</summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="reference">The rectangle; the frame by default.</param>
    /// <param name="offsetX">A horizontal move, calibrated units.</param>
    /// <param name="offsetY">A vertical move, calibrated units.</param>
    /// <returns>The point.</returns>
    public PointF Place(LayoutAnchor anchor, LayoutReference reference = LayoutReference.Frame, float offsetX = 0f, float offsetY = 0f)
        => Place(new LayoutPosition(anchor, reference, offsetX, offsetY));

    /// <summary>
    /// Places a box so that its OWN anchor point - the same fractions of its own size as the
    /// position's anchor - lands on the position: a box placed at the bottom-centre of the safe
    /// area sits on the safe bottom edge, centred; at the top-left it hangs from the corner.
    /// </summary>
    /// <param name="size">The box's size, calibrated units.</param>
    /// <param name="position">Where its anchor point goes.</param>
    /// <returns>The box, calibrated units.</returns>
    public RectangleF PlaceBox(SizeF size, LayoutPosition position)
    {
        PointF point = Place(position);
        return new RectangleF(point.X - (position.Anchor.X * size.Width), point.Y - (position.Anchor.Y * size.Height), size.Width, size.Height);
    }
}
