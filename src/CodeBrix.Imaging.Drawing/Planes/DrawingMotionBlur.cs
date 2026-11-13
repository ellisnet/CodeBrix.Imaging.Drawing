using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// Motion blur as data: averages N sub-frames (rendered at moments spread across one frame's
/// shutter interval) into one frame, in premultiplied space, with the exact rules of
/// <see cref="DrawingFrameAccumulator"/> (which these methods use, and which avoids holding all
/// the sub-frames at once when they are rendered one after another).
/// </summary>
public static class DrawingMotionBlur
{
    /// <summary>
    /// Returns the evenly spaced sub-frame moments for one output frame: <paramref name="samples"/>
    /// times centred in equal slices of the shutter interval, which starts at
    /// <paramref name="frameTime"/> and lasts <c>shutter * frameDuration</c>. Sample i is at
    /// <c>frameTime + (i + 0.5) / samples * shutter * frameDuration</c>.
    /// </summary>
    /// <param name="frameTime">The start of the output frame, in seconds.</param>
    /// <param name="frameDuration">The output frame's duration, in seconds (1 / frame rate).</param>
    /// <param name="samples">The number of sub-frames, 1 or more.</param>
    /// <param name="shutter">The fraction of the frame the shutter is open (0.5 = the classic 180 degree shutter).</param>
    /// <returns>The sub-frame times, in increasing order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="samples"/> is less than 1.</exception>
    public static double[] GetSampleTimes(double frameTime, double frameDuration, int samples, double shutter = 0.5)
    {
        if (samples < 1) { throw new ArgumentOutOfRangeException(nameof(samples)); }
        var times = new double[samples];
        double open = shutter * frameDuration;
        for (int i = 0; i < samples; i++) { times[i] = frameTime + ((i + 0.5) / samples * open); }
        return times;
    }

    /// <summary>
    /// Averages float images (straight alpha, all the same size) into a new image.
    /// </summary>
    /// <param name="frames">The sub-frames.</param>
    /// <returns>A new image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the list or an image is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the list is empty or the sizes differ.</exception>
    public static DrawingPlaneImage Average(IReadOnlyList<DrawingPlaneImage> frames)
    {
        if (frames == null) { throw new ArgumentNullException(nameof(frames)); }
        if (frames.Count == 0) { throw new ArgumentException("At least one frame is required.", nameof(frames)); }
        if (frames[0] == null) { throw new ArgumentNullException(nameof(frames)); }
        var accumulator = new DrawingFrameAccumulator(frames[0].Width, frames[0].Height);
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i] == null) { throw new ArgumentNullException(nameof(frames)); }
            accumulator.Add(frames[i]);
        }
        return accumulator.GetAverageImage();
    }

    /// <summary>
    /// Averages 8-bit straight-alpha RGBA frames (all <c>width * height * 4</c> bytes or longer)
    /// into a destination frame, in exact integer arithmetic.
    /// </summary>
    /// <param name="frames">The sub-frames.</param>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="destination">Receives the averaged frame, at least <c>width * height * 4</c> bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when the list or a frame is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the list is empty or a buffer is too small.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1.</exception>
    public static void Average(IReadOnlyList<byte[]> frames, int width, int height, Span<byte> destination)
    {
        if (frames == null) { throw new ArgumentNullException(nameof(frames)); }
        if (frames.Count == 0) { throw new ArgumentException("At least one frame is required.", nameof(frames)); }
        var accumulator = new DrawingFrameAccumulator(width, height);
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i] == null) { throw new ArgumentNullException(nameof(frames)); }
            accumulator.Add(frames[i]);
        }
        accumulator.GetAverage(destination);
    }

    /// <summary>
    /// Averages 8-bit straight-alpha RGBA frames into a new frame (see
    /// <see cref="Average(IReadOnlyList{byte[]}, int, int, Span{byte})"/>).
    /// </summary>
    /// <param name="frames">The sub-frames.</param>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <returns>A new array of <c>width * height * 4</c> bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the list or a frame is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the list is empty or a frame is too small.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1.</exception>
    public static byte[] Average(IReadOnlyList<byte[]> frames, int width, int height)
    {
        DrawingPlane.ValidateSize(width, height);
        var destination = new byte[width * height * 4];
        Average(frames, width, height, destination);
        return destination;
    }
}
