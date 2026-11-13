using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Images;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// One frame of a <see cref="SpriteSheet"/> drawn into a destination rectangle - the frame
/// chosen by index, by time at a frame rate, or both. Like every element it takes a transform,
/// opacity, time window, effects and groups; the layer colour is not used.
/// <para>
/// WHICH FRAME. The running frame number at a render time t is
/// <c>n = FrameIndex + floor((t - PlaybackStartTime) x FramesPerSecond + 1e-6)</c> (just
/// <see cref="FrameIndex"/> when <see cref="FramesPerSecond"/> is 0), and the
/// <see cref="Playback"/> mode maps n onto the sheet's frames: loop, ping-pong or hold. The
/// render time is the session's (or renderer's) Time, so a sprite plays as the scene's time
/// moves, in exports and live, with no per-frame code; drive <see cref="FrameIndex"/> instead
/// (AnimatedProperty.FrameIndex, a float track whose values are rounded down) for frames chosen by
/// hand. A sprite is dynamic from birth: its layer redraws whenever its frame changes.
/// </para>
/// </summary>
public sealed class SpriteShape : DrawingShape
{
    private int _frameIndex;
    private double _playbackStart;
    private SKColor? _tint;

    /// <summary>Creates a sprite.</summary>
    /// <param name="sheet">The frames.</param>
    /// <param name="destination">Where each frame is drawn, calibrated units.</param>
    /// <param name="framesPerSecond">How fast the frames advance with time; 0 (the default) to show <see cref="FrameIndex"/> only.</param>
    /// <param name="playback">How the running frame number maps onto the frames; loop by default.</param>
    /// <param name="sampling">How pixels are sampled when scaled or turned; bilinear by default.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sheet"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame rate is negative or not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination is not finite.</exception>
    public SpriteShape(SpriteSheet sheet, RectangleF destination, double framesPerSecond = 0,
        SpritePlayback playback = SpritePlayback.Loop, ImageSampling sampling = ImageSampling.Linear)
        : base(1f, (Color?)null)
    {
        Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        if (!Double.IsFinite(framesPerSecond) || framesPerSecond < 0) { throw new ArgumentOutOfRangeException(nameof(framesPerSecond)); }
        if (!Single.IsFinite(destination.X) || !Single.IsFinite(destination.Y)
            || !Single.IsFinite(destination.Width) || !Single.IsFinite(destination.Height))
        {
            throw new ArgumentException("The destination must be finite.", nameof(destination));
        }
        Destination = destination;
        FramesPerSecond = framesPerSecond;
        Playback = playback;
        Sampling = sampling;
        MarkChanged();
    }

    /// <summary>The frames.</summary>
    public SpriteSheet Sheet { get; }

    /// <summary>Where each frame is drawn, calibrated units.</summary>
    public RectangleF Destination { get; }

    /// <summary>How fast the frames advance with time (0 = they do not).</summary>
    public double FramesPerSecond { get; }

    /// <summary>How the running frame number maps onto the frames.</summary>
    public SpritePlayback Playback { get; }

    /// <summary>How pixels are sampled.</summary>
    public ImageSampling Sampling { get; }

    /// <summary>The frame number added to the timed advance (the frame shown at <see cref="PlaybackStartTime"/>); 0 by default.</summary>
    public int FrameIndex
    {
        get => _frameIndex;
        set
        {
            if (value == _frameIndex) { return; }
            _frameIndex = value;
            MarkChanged();
        }
    }

    /// <summary>The time, seconds, at which the timed advance starts counting from <see cref="FrameIndex"/>; 0 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to NaN or infinity.</exception>
    public double PlaybackStartTime
    {
        get => _playbackStart;
        set
        {
            if (!Double.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (value == _playbackStart) { return; }
            _playbackStart = value;
            MarkChanged();
        }
    }

    /// <summary>A colour every pixel is multiplied by (alpha included); or <c>null</c> for none.</summary>
    public Color? Tint
    {
        get => GraphicsInterop.ToImaging(_tint);
        set
        {
            SKColor? tint = GraphicsInterop.ToGraphics(value);
            if (tint == _tint) { return; }
            _tint = tint;
            MarkChanged();
        }
    }

    /// <summary>The index in the sheet of the frame shown at a time.</summary>
    /// <param name="seconds">The render time.</param>
    /// <returns>A frame index, 0 to the sheet's frame count - 1.</returns>
    public int GetFrameAt(double seconds)
    {
        long n = _frameIndex;
        if (FramesPerSecond > 0 && Double.IsFinite(seconds))
        {
            n += (long)Math.Floor(((seconds - _playbackStart) * FramesPerSecond) + 1e-6);
        }
        return MapFrame(n, Sheet.FrameCount, Playback);
    }

    /// <summary>The destination rectangle.</summary>
    /// <returns>The bounds, calibrated units.</returns>
    public override RectangleF? GetBounds() => Destination;

    /// <summary>The destination rectangle as geometry.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline() => PathGeometry.Rectangle(Destination);

    /// <summary>The area the sprite covers: its whole destination rectangle (the frames' own transparency is not looked at).</summary>
    /// <param name="tolerance">Not used.</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance) => new[] { GetOutline() };

    /// <summary>Draws the frame for time 0 (renderers draw the frame for their own time).</summary>
    /// <param name="canvas">The canvas, already transformed to calibrated space.</param>
    /// <param name="color">Not used.</param>
    public override void Draw(SKCanvas canvas, SKColor color) => DrawAt(canvas, 0.0);

    internal void DrawAt(SKCanvas canvas, double seconds)
        => ImageShape.DrawPicture(canvas, Sheet.GetFrame(GetFrameAt(seconds)), Destination, null, Sampling, _tint);

    internal override long GetRenderStamp(double time) => (long)MixStamp((ulong)Stamp, (ulong)GetFrameAt(time));

    internal static int MapFrame(long n, int count, SpritePlayback playback)
    {
        if (count <= 1) { return 0; }
        switch (playback)
        {
            case SpritePlayback.Hold:
                return (int)Math.Clamp(n, 0L, count - 1);
            case SpritePlayback.PingPong:
            {
                long period = (2L * count) - 2L;
                long m = ((n % period) + period) % period;
                return (int)(m < count ? m : period - m);
            }
            default:
                return (int)(((n % count) + count) % count);
        }
    }
}
