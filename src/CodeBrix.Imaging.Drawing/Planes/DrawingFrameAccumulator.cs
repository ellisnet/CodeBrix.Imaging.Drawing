using System;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// Averages any number of frames into one, a frame at a time - the motion-blur helper: render a
/// few sub-frames spread across a frame's shutter interval, add each, and take the average. Only
/// the running sums are kept, never the frames.
/// <para>
/// Frames are averaged in PREMULTIPLIED space, so a pixel that is transparent in some sub-frames
/// takes its colour only from the sub-frames where it is covered, and its alpha is the mean alpha
/// - a moving object over a transparent background blurs into a correctly fading streak.
/// </para>
/// <para>
/// 8-BIT FRAMES (straight-alpha RGBA bytes) are summed in exact integers:
/// <c>SA = sum of A</c> and <c>SC = sum of C * A</c> per channel. The averaged byte alpha is
/// <c>round(SA / N)</c> and the averaged colour <c>round(SC / SA)</c> (0 where SA is 0), both
/// rounded half up in integer arithmetic. For opaque frames this is simply the rounded mean.
/// FLOAT IMAGES are summed in double: <c>A = sum(A) / N</c>, <c>C = sum(C * A) / sum(A)</c> (0 where
/// the alpha sum is 0), stored as float. One accumulator takes one kind of input: the first
/// <c>Add</c> decides which.
/// </para>
/// The accumulator is not safe to use from two threads at once.
/// </summary>
public sealed class DrawingFrameAccumulator
{
    //Each byte frame adds at most 255 * 255 per channel sum; this keeps the int sums exact
    private const int MaxByteFrames = int.MaxValue / (255 * 255);

    private int[] _byteSums;
    private double[] _floatSums;

    /// <summary>
    /// Creates an empty accumulator for frames of one size.
    /// </summary>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1.</exception>
    public DrawingFrameAccumulator(int width, int height)
    {
        DrawingPlane.ValidateSize(width, height);
        Width = width;
        Height = height;
    }

    /// <summary>The frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>The frame height in pixels.</summary>
    public int Height { get; }

    /// <summary>The number of frames added since creation or the last <see cref="Reset"/>.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Forgets every frame added so far (and the input kind), ready for the next output frame.
    /// </summary>
    public void Reset()
    {
        Count = 0;
        if (_byteSums != null) { Array.Clear(_byteSums); }
        if (_floatSums != null) { Array.Clear(_floatSums); }
    }

    /// <summary>
    /// Adds an 8-bit straight-alpha RGBA frame (tightly packed, row-major).
    /// </summary>
    /// <param name="rgba">At least <c>Width * Height * 4</c> bytes.</param>
    /// <exception cref="ArgumentException">Thrown when there are too few bytes.</exception>
    /// <exception cref="InvalidOperationException">Thrown when float images were already added, or more frames are added than the integer sums can hold.</exception>
    public void Add(ReadOnlySpan<byte> rgba)
    {
        int pixels = Width * Height;
        if (rgba.Length < pixels * 4L) { throw new ArgumentException("There are fewer bytes than pixels * 4.", nameof(rgba)); }
        if (Count > 0 && _byteSums == null) { throw new InvalidOperationException("This accumulator already holds float images."); }
        if (Count >= MaxByteFrames) { throw new InvalidOperationException("Too many frames for one accumulator."); }
        _floatSums = null;
        _byteSums ??= new int[pixels * 4];
        int[] sums = _byteSums;
        for (int o = 0; o < pixels * 4; o += 4)
        {
            int a = rgba[o + 3];
            sums[o] += rgba[o] * a;
            sums[o + 1] += rgba[o + 1] * a;
            sums[o + 2] += rgba[o + 2] * a;
            sums[o + 3] += a;
        }
        Count++;
    }

    /// <summary>
    /// Adds a float image (straight alpha).
    /// </summary>
    /// <param name="image">The image (same size as the accumulator).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the size differs.</exception>
    /// <exception cref="InvalidOperationException">Thrown when 8-bit frames were already added.</exception>
    public void Add(DrawingPlaneImage image)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        if (image.Width != Width || image.Height != Height) { throw new ArgumentException("The image size differs from the accumulator's.", nameof(image)); }
        if (Count > 0 && _floatSums == null) { throw new InvalidOperationException("This accumulator already holds 8-bit frames."); }
        _byteSums = null;
        int pixels = Width * Height;
        _floatSums ??= new double[pixels * 4];
        double[] sums = _floatSums;
        float[] r = image.R.Data, g = image.G.Data, b = image.B.Data, alpha = image.A.Data;
        for (int i = 0, o = 0; i < pixels; i++, o += 4)
        {
            double a = alpha[i];
            sums[o] += r[i] * a;
            sums[o + 1] += g[i] * a;
            sums[o + 2] += b[i] * a;
            sums[o + 3] += a;
        }
        Count++;
    }

    /// <summary>
    /// Writes the average as 8-bit straight-alpha RGBA. For 8-bit input the exact integer rule in
    /// the class description applies; for float input the float average is converted with the
    /// rounding rule of <see cref="DrawingPlaneImage.ToRgba(Span{byte})"/>.
    /// </summary>
    /// <param name="destination">At least <c>Width * Height * 4</c> bytes.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no frame has been added.</exception>
    public void GetAverage(Span<byte> destination)
    {
        int pixels = Width * Height;
        if (destination.Length < pixels * 4L) { throw new ArgumentException("The destination is too small.", nameof(destination)); }
        RequireFrames();
        if (_floatSums != null)
        {
            GetAverageImage().ToRgba(destination);
            return;
        }
        int[] sums = _byteSums;
        long n = Count;
        for (int o = 0; o < pixels * 4; o += 4)
        {
            long sa = sums[o + 3];
            destination[o + 3] = (byte)(((2 * sa) + n) / (2 * n));
            if (sa == 0)
            {
                destination[o] = 0;
                destination[o + 1] = 0;
                destination[o + 2] = 0;
                continue;
            }
            destination[o] = (byte)(((2L * sums[o]) + sa) / (2 * sa));
            destination[o + 1] = (byte)(((2L * sums[o + 1]) + sa) / (2 * sa));
            destination[o + 2] = (byte)(((2L * sums[o + 2]) + sa) / (2 * sa));
        }
    }

    /// <summary>
    /// Returns the average as 8-bit straight-alpha RGBA (see <see cref="GetAverage(Span{byte})"/>).
    /// </summary>
    /// <returns>A new array of <c>Width * Height * 4</c> bytes.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no frame has been added.</exception>
    public byte[] GetAverage()
    {
        var bytes = new byte[Width * Height * 4];
        GetAverage(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns the average as a float image. For float input the rule in the class description
    /// applies; for 8-bit input alpha is <c>SA / (255 N)</c> and colour <c>SC / (255 SA)</c>,
    /// computed in double.
    /// </summary>
    /// <returns>A new image.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no frame has been added.</exception>
    public DrawingPlaneImage GetAverageImage()
    {
        RequireFrames();
        var image = new DrawingPlaneImage(Width, Height);
        float[] r = image.R.Data, g = image.G.Data, b = image.B.Data, alpha = image.A.Data;
        int pixels = Width * Height;
        double n = Count;
        for (int i = 0, o = 0; i < pixels; i++, o += 4)
        {
            double sa, sr, sg, sb, scale;
            if (_floatSums != null)
            {
                sa = _floatSums[o + 3];
                sr = _floatSums[o];
                sg = _floatSums[o + 1];
                sb = _floatSums[o + 2];
                alpha[i] = (float)(sa / n);
                scale = 1.0;
            }
            else
            {
                sa = _byteSums[o + 3];
                sr = _byteSums[o];
                sg = _byteSums[o + 1];
                sb = _byteSums[o + 2];
                alpha[i] = (float)(sa / (255.0 * n));
                scale = 255.0;
            }
            if (sa == 0) { continue; }
            r[i] = (float)(sr / (scale * sa));
            g[i] = (float)(sg / (scale * sa));
            b[i] = (float)(sb / (scale * sa));
        }
        return image;
    }

    private void RequireFrames()
    {
        if (Count == 0) { throw new InvalidOperationException("No frame has been added."); }
    }
}
