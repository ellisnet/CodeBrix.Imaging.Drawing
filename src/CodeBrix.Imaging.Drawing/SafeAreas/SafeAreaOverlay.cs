using System;
using System.Collections.Generic;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// Draws a guide's unsafe zones over a frame as a light tint with a thin outline along the
/// edge of the safe area - a preview aid, never
/// part of a finished frame unless explicitly asked for. It is OFF everywhere by default:
/// the live <c>Render</c> shows it only while <see cref="DrawingSession.SafeAreaOverlay"/>
/// is set, and an export includes it only through the export overloads that take a
/// <see cref="SafeAreaOverlay"/> argument. Overlapping zones are drawn as one union, so an
/// area two zones share is no darker than either. The zones are laid out on the whole
/// output frame (the canvas, or the export size). Guides with a time rule are evaluated at the
/// moment the frame shows: the renderer's (session's) <c>Time</c>, unless <see cref="Time"/> is
/// set to a moment of its own - the same rule as <see cref="Rendering.DebugOverlay.Time"/>.
/// Mutable, so a live view can switch <see cref="Guide"/> or change the look while it plays.
/// <para>
/// THE LOOK: the zones are tinted in <see cref="MaskColor"/> (default <see cref="DefaultMaskColor"/>,
/// a light rose wash that leaves the picture underneath easy to read on dark and light frames)
/// at its alpha times <see cref="Opacity"/> (1 by default; 0.5 = half as strong, 0 = no tint),
/// and a solid line of <see cref="OutlineWidth"/> frame pixels (default
/// <see cref="DefaultOutlineWidth"/>; 0 = none) in <see cref="OutlineColor"/> (default
/// <see cref="DefaultOutlineColor"/>) runs just OUTSIDE the safe area - on the unsafe side, so it
/// never covers safe content - marking the edge unmistakably even where the tint is faint.
/// <see cref="Opacity"/> scales the tint only, not the outline. A guide whose zone is the
/// whole frame (nothing safe) gets the tint and no outline.
/// </para>
/// </summary>
public sealed class SafeAreaOverlay
{
    /// <summary>
    /// The default mask color: a light rose wash, rgba(255, 64, 112, 40) - about a sixth opaque,
    /// so the unsafe area is plainly marked while the picture under it stays easy to read over
    /// dark and light frames alike (the outline marks the edge itself).
    /// </summary>
    public static readonly Color DefaultMaskColor = Color.FromRgba(255, 64, 112, 40);

    /// <summary>The default outline color: a strong rose, rgba(255, 64, 112, 230), visible on dark and light frames.</summary>
    public static readonly Color DefaultOutlineColor = Color.FromRgba(255, 64, 112, 230);

    /// <summary>The default outline width, frame pixels.</summary>
    public const float DefaultOutlineWidth = 2f;

    private float _opacity = 1f;
    private float _outlineWidth = DefaultOutlineWidth;

    private SafeAreaGuide _guide;

    /// <summary>
    /// Creates an overlay for a guide.
    /// </summary>
    /// <param name="guide">The guide whose unsafe zones are shown.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="guide"/> is null.</exception>
    public SafeAreaOverlay(SafeAreaGuide guide)
    {
        _guide = guide ?? throw new ArgumentNullException(nameof(guide));
    }

    /// <summary>The guide whose unsafe zones are shown.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    public SafeAreaGuide Guide
    {
        get => _guide;
        set => _guide = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// The mask's color; its ALPHA is the mask's opacity (straight alpha, 0..255). Defaults
    /// to <see cref="DefaultMaskColor"/>.
    /// </summary>
    public Color MaskColor { get; set; } = DefaultMaskColor;

    /// <summary>
    /// How strong the tint is, 0..1 (default 1): the mask is drawn at
    /// <c>round(MaskColor.A * Opacity)</c> alpha (halves rounded away from zero; 1 = exactly the
    /// colour's alpha, 0 = no tint). The outline is not affected.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0..1 or to NaN.</exception>
    public float Opacity
    {
        get => _opacity;
        set
        {
            if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _opacity = value;
        }
    }

    /// <summary>
    /// The colour of the line along the safe area's edge; its alpha is the line's opacity.
    /// Defaults to <see cref="DefaultOutlineColor"/>; transparent = no line.
    /// </summary>
    public Color OutlineColor { get; set; } = DefaultOutlineColor;

    /// <summary>
    /// How wide the line along the safe area's edge is, frame pixels (default
    /// <see cref="DefaultOutlineWidth"/>; 0 = no line). It is drawn outside the safe rectangle,
    /// on the unsafe side, as solid (not anti-aliased) bands.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative or non-finite value.</exception>
    public float OutlineWidth
    {
        get => _outlineWidth;
        set
        {
            if (!Single.IsFinite(value) || value < 0f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _outlineWidth = value;
        }
    }

    /// <summary>
    /// The moment, in seconds, at which the guide's time rules are evaluated; or <c>null</c>
    /// (the default) for the moment the frame shows - the renderer's (session's) <c>Time</c>.
    /// </summary>
    public double? Time { get; set; }

    /// <summary>The video's total length, for "final N seconds" rules; or <c>null</c> (the default) when unknown, in which case such rules apply.</summary>
    public double? Duration { get; set; }

    /// <summary>
    /// The zones the overlay draws on a frame of the given size at a moment - what it draws over
    /// a frame showing <paramref name="seconds"/> while <see cref="Time"/> is <c>null</c>.
    /// </summary>
    /// <param name="frameSize">The frame size, in pixels.</param>
    /// <param name="seconds">The moment, in seconds, for guides with a time rule.</param>
    /// <returns>The zones, in frame pixels.</returns>
    public IReadOnlyList<SafeAreaZone> GetZones(SizeF frameSize, double seconds) => _guide.GetUnsafeZones(frameSize, seconds, Duration);

    //Draws the mask over the whole canvas area of the given size, at the canvas's current
    //  transform, for a frame that shows the renderer's time
    internal void Draw(SKCanvas canvas, SizeF frameSize, double rendererTime)
    {
        double seconds = Time ?? rendererTime;
        IReadOnlyList<SafeAreaZone> zones = GetZones(frameSize, seconds);
        if (zones.Count < 1) { return; }
        SKColor color = GraphicsInterop.ToGraphics(MaskColor);
        byte alpha = Rendering.DrawingRenderer.ScaleAlpha(color.Alpha, _opacity);

        if (alpha > 0)
        {
            //The zones are painted opaque into a layer that is composited once at the mask's
            //  alpha - the same highlighter trick the layers use - so overlaps never darken
            using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha(alpha) };
            canvas.SaveLayer(layerPaint);
            using var zonePaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = color.WithAlpha(255),
                IsAntialias = false,
            };
            foreach (SafeAreaZone zone in zones)
            {
                canvas.DrawRect(GraphicsInterop.ToGraphics(zone.Bounds), zonePaint);
            }
            canvas.Restore();
        }

        SKColor outline = GraphicsInterop.ToGraphics(OutlineColor);
        float width = _outlineWidth;
        if (width <= 0f || outline.Alpha == 0) { return; }
        RectangleF safe = _guide.GetSafeRectangle(frameSize, seconds, Duration);
        if (safe.Width <= 0f || safe.Height <= 0f) { return; }

        //A ring just outside the safe rectangle, as four opaque bands composited once at the
        //  outline's alpha (the corners are never painted twice)
        float left = safe.Left, top = safe.Top, right = safe.Right, bottom = safe.Bottom;
        using var ringPaint = new SKPaint { Color = SKColors.White.WithAlpha(outline.Alpha) };
        canvas.SaveLayer(ringPaint);
        using var bandPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = outline.WithAlpha(255),
            IsAntialias = false,
        };
        canvas.DrawRect(new SKRect(left - width, top - width, right + width, top), bandPaint);
        canvas.DrawRect(new SKRect(left - width, bottom, right + width, bottom + width), bandPaint);
        canvas.DrawRect(new SKRect(left - width, top, left, bottom), bandPaint);
        canvas.DrawRect(new SKRect(right, top, right + width, bottom), bandPaint);
        canvas.Restore();
    }
}
