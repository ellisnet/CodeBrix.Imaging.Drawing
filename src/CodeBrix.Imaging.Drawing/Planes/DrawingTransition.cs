using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.PixelFormats;

namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// A frame-to-frame transition - dissolve, wipe, slide, push or fade through a colour - applied
/// at a fraction from 0 (all outgoing frame) to 1 (all incoming frame). It works on 8-bit
/// straight-alpha RGBA frames and on <see cref="DrawingPlaneImage"/> values, with the same rules:
/// <list type="bullet">
///   <item>A fraction of 0 gives the outgoing frame and 1 the incoming frame, EXACTLY; fractions
///   outside 0..1 are clamped.</item>
///   <item>Pixels are mixed with the premultiplied rule of <see cref="DrawingPlaneImage.Lerp(DrawingPlaneImage, float)"/>,
///   so a transparent pixel contributes no colour. Where the two alphas are equal (opaque frames)
///   a byte channel is <c>(byte)(a * (1 - t) + b * t + 0.5)</c>; otherwise colour is
///   premultiplied-mixed, divided by the mixed alpha and rounded half up.</item>
///   <item>Slides and pushes move whole pixels: the offset is <c>floor(fraction * extent + 0.5)</c>
///   pixels, so every output pixel is an exact copy of one input pixel.</item>
/// </list>
/// Transitions are immutable and safe to share between threads. Results are bit-identical in
/// both drawing packages.
/// </summary>
public sealed class DrawingTransition
{
    private readonly Vector4 _colorValue;

    private DrawingTransition(DrawingTransitionKind kind, DrawingTransitionDirection direction, float softness, Color color)
    {
        Kind = kind;
        Direction = direction;
        Softness = softness;
        Color = color;
        _colorValue = DrawingPlaneImage.ToVector(color);
    }

    /// <summary>The kind of transition.</summary>
    public DrawingTransitionKind Kind { get; }

    /// <summary>The direction of travel (wipes, slides and pushes; <see cref="DrawingTransitionDirection.Right"/> otherwise).</summary>
    public DrawingTransitionDirection Direction { get; }

    /// <summary>The width, in pixels, of a wipe's soft edge (0 = a hard edge; 0 for the other kinds).</summary>
    public float Softness { get; }

    /// <summary>The colour a fade passes through (transparent black for the other kinds).</summary>
    public Color Color { get; }

    /// <summary>
    /// A cross-fade: every pixel mixes from the outgoing to the incoming frame by the fraction.
    /// </summary>
    /// <returns>The transition.</returns>
    public static DrawingTransition Dissolve()
        => new DrawingTransition(DrawingTransitionKind.Dissolve, DrawingTransitionDirection.Right, 0f, Color.Transparent);

    /// <summary>
    /// A wipe: an edge travels across the picture in <paramref name="direction"/>, leaving the
    /// incoming frame behind it. Each pixel's weight depends on the distance <c>c</c> of its centre
    /// from the side the edge starts at (for <see cref="DrawingTransitionDirection.Right"/>,
    /// <c>c = x + 0.5</c>). Hard edge: weight 1 where <c>c &lt; fraction * extent</c>, else 0.
    /// Soft edge of width s: <c>weight = clamp((fraction * (extent + s) - c) / s, 0, 1)</c>, so the
    /// soft band enters fully before the edge and leaves fully at the end.
    /// </summary>
    /// <param name="direction">The way the edge travels.</param>
    /// <param name="softness">The soft edge's width in pixels; 0 = a hard edge.</param>
    /// <returns>The transition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction is unknown or <paramref name="softness"/> is negative, NaN or infinite.</exception>
    public static DrawingTransition Wipe(DrawingTransitionDirection direction, float softness = 0f)
    {
        ValidateDirection(direction);
        if (!(softness >= 0f) || float.IsInfinity(softness)) { throw new ArgumentOutOfRangeException(nameof(softness)); }
        return new DrawingTransition(DrawingTransitionKind.Wipe, direction, softness, Color.Transparent);
    }

    /// <summary>
    /// A slide: the incoming frame travels in <paramref name="direction"/> and covers the outgoing
    /// frame, which stays still.
    /// </summary>
    /// <param name="direction">The way the incoming frame travels.</param>
    /// <returns>The transition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction is unknown.</exception>
    public static DrawingTransition Slide(DrawingTransitionDirection direction)
    {
        ValidateDirection(direction);
        return new DrawingTransition(DrawingTransitionKind.Slide, direction, 0f, Color.Transparent);
    }

    /// <summary>
    /// A push: the incoming frame travels in <paramref name="direction"/> and pushes the outgoing
    /// frame out of the picture ahead of it.
    /// </summary>
    /// <param name="direction">The way both frames travel.</param>
    /// <returns>The transition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction is unknown.</exception>
    public static DrawingTransition Push(DrawingTransitionDirection direction)
    {
        ValidateDirection(direction);
        return new DrawingTransition(DrawingTransitionKind.Push, direction, 0f, Color.Transparent);
    }

    /// <summary>
    /// A fade through a colour: for fractions below 0.5 the outgoing frame mixes towards
    /// <paramref name="color"/> by <c>2 * fraction</c>; from 0.5 the colour mixes towards the
    /// incoming frame by <c>2 * fraction - 1</c>. At exactly 0.5 the frame is the colour. A
    /// transparent colour fades through transparency.
    /// </summary>
    /// <param name="color">The colour in the middle (straight alpha; float images use <c>byte / 255f</c>).</param>
    /// <returns>The transition.</returns>
    public static DrawingTransition FadeThroughColor(Color color)
        => new DrawingTransition(DrawingTransitionKind.FadeThroughColor, DrawingTransitionDirection.Right, 0f, color);

    /// <summary>
    /// Applies the transition to two 8-bit straight-alpha RGBA frames (tightly packed, row-major).
    /// For Dissolve, Wipe and FadeThroughColor the destination may be the same memory as either
    /// frame; for Slide and Push it must not overlap them.
    /// </summary>
    /// <param name="from">The outgoing frame, at least <c>width * height * 4</c> bytes.</param>
    /// <param name="to">The incoming frame, at least <c>width * height * 4</c> bytes.</param>
    /// <param name="destination">Receives the frame, at least <c>width * height * 4</c> bytes.</param>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="fraction">0 = outgoing, 1 = incoming; clamped to 0..1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is less than 1 or <paramref name="fraction"/> is NaN.</exception>
    /// <exception cref="ArgumentException">Thrown when a buffer is too small, or a slide or push destination overlaps a frame.</exception>
    public void Apply(ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, Span<byte> destination, int width, int height, float fraction)
    {
        DrawingPlane.ValidateSize(width, height);
        int length = width * height * 4;
        if (from.Length < length) { throw new ArgumentException("The outgoing frame is too small.", nameof(from)); }
        if (to.Length < length) { throw new ArgumentException("The incoming frame is too small.", nameof(to)); }
        if (destination.Length < length) { throw new ArgumentException("The destination is too small.", nameof(destination)); }
        float f = ClampFraction(fraction);
        from = from.Slice(0, length);
        to = to.Slice(0, length);
        destination = destination.Slice(0, length);

        switch (Kind)
        {
            case DrawingTransitionKind.Dissolve:
                for (int o = 0; o < length; o += 4) { PlaneKernels.MixBytes(from, to, destination, o, f); }
                break;
            case DrawingTransitionKind.FadeThroughColor:
                FadeBytes(from, to, destination, f);
                break;
            case DrawingTransitionKind.Wipe:
                WipeBytes(from, to, destination, width, height, f);
                break;
            default:
                if (destination.Overlaps(from) || destination.Overlaps(to))
                {
                    throw new ArgumentException("A slide or push needs a destination separate from both frames.", nameof(destination));
                }
                MoveBytes(from, to, destination, width, height, f);
                break;
        }
    }

    /// <summary>
    /// Applies the transition to two float images and returns the result as a new image.
    /// </summary>
    /// <param name="from">The outgoing image.</param>
    /// <param name="to">The incoming image (same size).</param>
    /// <param name="fraction">0 = outgoing, 1 = incoming; clamped to 0..1.</param>
    /// <returns>A new image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an image is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="fraction"/> is NaN.</exception>
    public DrawingPlaneImage Apply(DrawingPlaneImage from, DrawingPlaneImage to, float fraction)
    {
        if (from == null) { throw new ArgumentNullException(nameof(from)); }
        var destination = new DrawingPlaneImage(from.Width, from.Height);
        Apply(from, to, destination, fraction);
        return destination;
    }

    /// <summary>
    /// Applies the transition to two float images, writing into an existing image (no allocation
    /// per frame). For Dissolve, Wipe and FadeThroughColor the destination may be
    /// <paramref name="from"/> or <paramref name="to"/>; for Slide and Push it must be a third image.
    /// </summary>
    /// <param name="from">The outgoing image.</param>
    /// <param name="to">The incoming image (same size).</param>
    /// <param name="destination">Receives the result (same size).</param>
    /// <param name="fraction">0 = outgoing, 1 = incoming; clamped to 0..1.</param>
    /// <exception cref="ArgumentNullException">Thrown when an image is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sizes differ, or a slide or push destination is one of the inputs.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="fraction"/> is NaN.</exception>
    public void Apply(DrawingPlaneImage from, DrawingPlaneImage to, DrawingPlaneImage destination, float fraction)
    {
        if (from == null) { throw new ArgumentNullException(nameof(from)); }
        from.RequireSameSize(to, nameof(to));
        from.RequireSameSize(destination, nameof(destination));
        float f = ClampFraction(fraction);

        switch (Kind)
        {
            case DrawingTransitionKind.Dissolve:
                DrawingPlaneImage.MixInto(from, to, destination, f, null);
                break;
            case DrawingTransitionKind.FadeThroughColor:
                FadeImages(from, to, destination, f);
                break;
            case DrawingTransitionKind.Wipe:
                DrawingPlaneImage.MixInto(from, to, destination, 0f, WipeWeights(from.Width, from.Height, f));
                break;
            default:
                if (ReferenceEquals(destination, from) || ReferenceEquals(destination, to))
                {
                    throw new ArgumentException("A slide or push needs a destination separate from both images.", nameof(destination));
                }
                MoveImages(from, to, destination, f);
                break;
        }
    }

    //---------------------------------------------------------------------------------------------

    private static void ValidateDirection(DrawingTransitionDirection direction)
    {
        if (direction < DrawingTransitionDirection.Left || direction > DrawingTransitionDirection.Down)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    private static float ClampFraction(float fraction)
    {
        if (float.IsNaN(fraction)) { throw new ArgumentOutOfRangeException(nameof(fraction)); }
        return fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
    }

    private bool IsHorizontal => Direction == DrawingTransitionDirection.Left || Direction == DrawingTransitionDirection.Right;

    //Weight of the incoming frame for a pixel whose centre is `centre` pixels from the start side
    private float WipeWeight(float centre, int extent, float fraction)
    {
        if (Softness > 0f)
        {
            float lead = fraction * (extent + Softness);
            return PlaneKernels.Clamp01((lead - centre) / Softness);
        }
        return centre < fraction * extent ? 1f : 0f;
    }

    //One weight per column (horizontal wipes) or per row (vertical wipes)
    private float[] WipeLine(int width, int height, float fraction)
    {
        int extent = IsHorizontal ? width : height;
        var weights = new float[extent];
        for (int i = 0; i < extent; i++)
        {
            bool fromStart = Direction == DrawingTransitionDirection.Right || Direction == DrawingTransitionDirection.Down;
            float centre = fromStart ? i + 0.5f : (extent - i) - 0.5f;
            weights[i] = WipeWeight(centre, extent, fraction);
        }
        return weights;
    }

    private DrawingPlane WipeWeights(int width, int height, float fraction)
    {
        float[] line = WipeLine(width, height, fraction);
        var plane = new DrawingPlane(width, height);
        bool horizontal = IsHorizontal;
        for (int y = 0; y < height; y++)
        {
            Span<float> row = plane.GetRow(y);
            if (horizontal) { line.AsSpan().CopyTo(row); }
            else { row.Fill(line[y]); }
        }
        return plane;
    }

    private void WipeBytes(ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, Span<byte> destination, int width, int height, float fraction)
    {
        float[] line = WipeLine(width, height, fraction);
        bool horizontal = IsHorizontal;
        for (int y = 0, o = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++, o += 4)
            {
                PlaneKernels.MixBytes(from, to, destination, o, horizontal ? line[x] : line[y]);
            }
        }
    }

    private void FadeBytes(ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, Span<byte> destination, float fraction)
    {
        Rgba32 c = Color.ToRgba32();
        bool first = fraction < 0.5f;
        float t = first ? fraction * 2f : (fraction * 2f) - 1f;
        for (int o = 0; o < destination.Length; o += 4)
        {
            if (first)
            {
                PlaneKernels.MixBytes(from[o], from[o + 1], from[o + 2], from[o + 3], c.R, c.G, c.B, c.A, t, destination, o);
            }
            else
            {
                PlaneKernels.MixBytes(c.R, c.G, c.B, c.A, to[o], to[o + 1], to[o + 2], to[o + 3], t, destination, o);
            }
        }
    }

    private void FadeImages(DrawingPlaneImage from, DrawingPlaneImage to, DrawingPlaneImage destination, float fraction)
    {
        var color = new DrawingPlaneImage(from.Width, from.Height).Fill(_colorValue.X, _colorValue.Y, _colorValue.Z, _colorValue.W);
        if (fraction < 0.5f) { DrawingPlaneImage.MixInto(from, color, destination, fraction * 2f, null); }
        else { DrawingPlaneImage.MixInto(color, to, destination, (fraction * 2f) - 1f, null); }
    }

    //A copy instruction for slides and pushes: `length` pixels from (sourceX, sourceY) of the
    //  incoming (FromIncoming) or outgoing frame to (destinationX, destinationY)
    private readonly struct Segment
    {
        public Segment(bool fromIncoming, int sourceX, int sourceY, int destinationX, int destinationY, int length)
        {
            FromIncoming = fromIncoming;
            SourceX = sourceX;
            SourceY = sourceY;
            DestinationX = destinationX;
            DestinationY = destinationY;
            Length = length;
        }

        public bool FromIncoming { get; }

        public int SourceX { get; }

        public int SourceY { get; }

        public int DestinationX { get; }

        public int DestinationY { get; }

        public int Length { get; }
    }

    private List<Segment> MoveSegments(int width, int height, float fraction)
    {
        var segments = new List<Segment>();
        bool push = Kind == DrawingTransitionKind.Push;
        if (IsHorizontal)
        {
            int offset = Offset(fraction, width);
            for (int y = 0; y < height; y++)
            {
                if (Direction == DrawingTransitionDirection.Right)
                {
                    //incoming enters from the left edge
                    AddSegment(segments, true, width - offset, y, 0, y, offset);
                    AddSegment(segments, false, push ? 0 : offset, y, offset, y, width - offset);
                }
                else
                {
                    //incoming enters from the right edge
                    AddSegment(segments, false, push ? offset : 0, y, 0, y, width - offset);
                    AddSegment(segments, true, 0, y, width - offset, y, offset);
                }
            }
        }
        else
        {
            int offset = Offset(fraction, height);
            for (int y = 0; y < height; y++)
            {
                if (Direction == DrawingTransitionDirection.Down)
                {
                    //incoming enters from the top edge
                    if (y < offset) { AddSegment(segments, true, 0, y + height - offset, 0, y, width); }
                    else { AddSegment(segments, false, 0, push ? y - offset : y, 0, y, width); }
                }
                else
                {
                    //incoming enters from the bottom edge
                    if (y >= height - offset) { AddSegment(segments, true, 0, y - (height - offset), 0, y, width); }
                    else { AddSegment(segments, false, 0, push ? y + offset : y, 0, y, width); }
                }
            }
        }
        return segments;
    }

    private static void AddSegment(List<Segment> segments, bool fromIncoming, int sourceX, int sourceY, int destinationX, int destinationY, int length)
    {
        if (length > 0) { segments.Add(new Segment(fromIncoming, sourceX, sourceY, destinationX, destinationY, length)); }
    }

    private static int Offset(float fraction, int extent)
    {
        int offset = (int)MathF.Floor((fraction * extent) + 0.5f);
        return offset < 0 ? 0 : (offset > extent ? extent : offset);
    }

    private void MoveBytes(ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, Span<byte> destination, int width, int height, float fraction)
    {
        foreach (Segment s in MoveSegments(width, height, fraction))
        {
            ReadOnlySpan<byte> source = s.FromIncoming ? to : from;
            source.Slice(((s.SourceY * width) + s.SourceX) * 4, s.Length * 4)
                .CopyTo(destination.Slice(((s.DestinationY * width) + s.DestinationX) * 4, s.Length * 4));
        }
    }

    private void MoveImages(DrawingPlaneImage from, DrawingPlaneImage to, DrawingPlaneImage destination, float fraction)
    {
        int width = from.Width;
        foreach (Segment s in MoveSegments(width, from.Height, fraction))
        {
            DrawingPlaneImage source = s.FromIncoming ? to : from;
            int sourceIndex = (s.SourceY * width) + s.SourceX;
            int destinationIndex = (s.DestinationY * width) + s.DestinationX;
            Array.Copy(source.R.Data, sourceIndex, destination.R.Data, destinationIndex, s.Length);
            Array.Copy(source.G.Data, sourceIndex, destination.G.Data, destinationIndex, s.Length);
            Array.Copy(source.B.Data, sourceIndex, destination.B.Data, destinationIndex, s.Length);
            Array.Copy(source.A.Data, sourceIndex, destination.A.Data, destinationIndex, s.Length);
        }
    }
}
