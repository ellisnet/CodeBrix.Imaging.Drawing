using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Rendering;

/// <summary>
/// A debugging read-out drawn over a preview frame: the timecode, the frame number and
/// (optionally) the names of the active timeline cues, on a translucent plate in a corner. A
/// preview aid, never part of a finished frame unless explicitly asked for - exactly like the
/// <see cref="CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaOverlay"/>: it is OFF by default, the
/// live <c>Render</c> shows it only while <see cref="DrawingSession.DebugOverlay"/> (or
/// <see cref="DrawingRenderer.DebugOverlay"/>) is set, and an export includes it only through
/// the export overloads that take a <see cref="DebugOverlay"/> argument. It is drawn last,
/// over everything (the safe-area mask included), in output pixels.
/// <para>
/// FONTS: the text uses a font from <see cref="Fonts"/> - a registry the caller fills, never
/// a system font. With no registry, no font registered, or an unknown
/// <see cref="FamilyName"/>, the overlay FAILS SOFT: it draws nothing at all and never
/// throws (<see cref="CanDrawText"/> says which). Mutable, so a live view can switch what it
/// shows.
/// </para>
/// </summary>
public sealed class DebugOverlay
{
    /// <summary>The default text color: white.</summary>
    public static readonly Color DefaultTextColor = Color.FromRgb(255, 255, 255);

    /// <summary>The default plate color: black at a little over half opacity.</summary>
    public static readonly Color DefaultPlateColor = Color.FromRgba(0, 0, 0, 150);

    private float _textSize;

    /// <summary>
    /// Creates an overlay.
    /// </summary>
    /// <param name="fonts">The registry whose font draws the text (for example the session's <c>Fonts</c>); or <c>null</c>, in which case the overlay draws nothing.</param>
    /// <param name="timeline">The timeline that gives the frame rate, frame count and cues; or <c>null</c> to show the time in seconds only.</param>
    public DebugOverlay(FontRegistry fonts, AnimationTimeline timeline = null)
    {
        Fonts = fonts;
        Timeline = timeline;
    }

    /// <summary>The registry whose font draws the text; <c>null</c> draws nothing.</summary>
    public FontRegistry Fonts { get; set; }

    /// <summary>The font family; or <c>null</c> (the default) for the registry's first font.</summary>
    public string FamilyName { get; set; }

    /// <summary>The timeline for timecode, frame numbers and cues; or <c>null</c> for seconds only.</summary>
    public AnimationTimeline Timeline { get; set; }

    /// <summary>
    /// The moment to show, in seconds; or <c>null</c> (the default) for the moment the frame
    /// shows - the <c>Time</c> of the session or renderer that draws the overlay (the same rule
    /// as <see cref="SafeAreas.SafeAreaOverlay.Time"/>).
    /// </summary>
    public double? Time { get; set; }

    /// <summary>Whether to show the time (a timecode with a timeline, seconds without); <c>true</c> by default.</summary>
    public bool ShowTimecode { get; set; } = true;

    /// <summary>Whether to show "frame N / COUNT" (needs a timeline); <c>true</c> by default.</summary>
    public bool ShowFrameNumber { get; set; } = true;

    /// <summary>Whether to show the names of the cues active at the moment (needs a timeline); <c>false</c> by default.</summary>
    public bool ShowCues { get; set; }

    /// <summary>The corner the read-out sits in; <see cref="DebugOverlayCorner.TopLeft"/> by default.</summary>
    public DebugOverlayCorner Corner { get; set; } = DebugOverlayCorner.TopLeft;

    /// <summary>
    /// The text size in output pixels; 0 (the default) picks one from the frame height (a
    /// thirty-sixth of it, at least 10).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative or non-finite value.</exception>
    public float TextSize
    {
        get => _textSize;
        set
        {
            if (!Single.IsFinite(value) || value < 0f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _textSize = value;
        }
    }

    /// <summary>The text color; <see cref="DefaultTextColor"/> by default.</summary>
    public Color TextColor { get; set; } = DefaultTextColor;

    /// <summary>The color of the plate behind the text (its alpha is its opacity); <see cref="DefaultPlateColor"/> by default.</summary>
    public Color PlateColor { get; set; } = DefaultPlateColor;

    /// <summary>
    /// Indicates whether the overlay can draw: a registry is set and holds the font it would
    /// use. When <c>false</c> the overlay draws nothing (and never throws).
    /// </summary>
    public bool CanDrawText
    {
        get
        {
            FontRegistry fonts = Fonts;
            if (fonts == null || fonts.Count < 1) { return false; }
            try
            {
                return fonts.Contains(FamilyName);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The lines the overlay shows at a moment, top to bottom - the text alone, whether or not a
    /// font is available.
    /// </summary>
    /// <param name="seconds">The moment.</param>
    /// <returns>The lines; empty when everything is switched off.</returns>
    public IReadOnlyList<string> GetLines(double seconds)
    {
        var lines = new List<string>();
        AnimationTimeline timeline = Timeline;
        string time = seconds.ToString("0.000", CultureInfo.InvariantCulture) + " s";
        if (ShowTimecode)
        {
            lines.Add(timeline == null ? time : timeline.FormatTimecode(seconds) + "   " + time);
        }
        if (timeline != null)
        {
            if (ShowFrameNumber)
            {
                lines.Add(String.Format(CultureInfo.InvariantCulture, "frame {0} / {1}", timeline.FrameAt(seconds), timeline.FrameCount));
            }
            if (ShowCues)
            {
                var names = new List<string>();
                foreach (TimelineCue cue in timeline.GetActiveCues(seconds)) { names.Add(cue.Name); }
                lines.Add(names.Count > 0 ? "cues: " + String.Join(", ", names) : "cues: none");
            }
        }
        return lines;
    }

    //Draws the read-out over a frame of the given size, at the canvas's current transform
    internal void Draw(SKCanvas canvas, SizeF frameSize, double rendererTime)
    {
        if (!CanDrawText || frameSize.Width < 1 || frameSize.Height < 1) { return; }
        IReadOnlyList<string> lines = GetLines(Time ?? rendererTime);
        if (lines.Count < 1) { return; }

        float size = _textSize > 0f ? _textSize : Math.Max(10f, frameSize.Height / 36f);
        float lineHeight = size * 1.3f;
        float pad = size * 0.45f;
        float margin = size * 0.6f;

        TextShape[] texts;
        try
        {
            texts = new TextShape[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                texts[i] = new TextShape(lines[i], 0f, 0f, size, Fonts, TextColor, TextAlign.Left, 0f, FamilyName);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            //Fail soft: a font that cannot lay the text out draws no read-out at all
            return;
        }

        float width = 0f;
        foreach (TextShape text in texts) { width = Math.Max(width, text.Width); }
        float plateWidth = width + (2f * pad);
        float plateHeight = (lines.Count * lineHeight) + (2f * pad) - (lineHeight - size);
        bool right = Corner == DebugOverlayCorner.TopRight || Corner == DebugOverlayCorner.BottomRight;
        bool bottom = Corner == DebugOverlayCorner.BottomLeft || Corner == DebugOverlayCorner.BottomRight;
        float left = right ? frameSize.Width - margin - plateWidth : margin;
        float top = bottom ? frameSize.Height - margin - plateHeight : margin;

        SKColor plate = GraphicsInterop.ToGraphics(PlateColor);
        if (plate.Alpha > 0)
        {
            using var platePaint = new SKPaint { Style = SKPaintStyle.Fill, Color = plate, IsAntialias = true };
            canvas.DrawRoundRect(SKRect.Create(left, top, plateWidth, plateHeight), pad * 0.6f, pad * 0.6f, platePaint);
        }

        SKColor ink = GraphicsInterop.ToGraphics(TextColor);
        for (int i = 0; i < texts.Length; i++)
        {
            canvas.Save();
            canvas.Translate(left + pad, top + pad + (size * 0.8f) + (i * lineHeight));
            texts[i].Draw(canvas, ink);
            canvas.Restore();
        }
    }
}
