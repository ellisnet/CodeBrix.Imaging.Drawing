using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Text;

namespace VenusRosetteAnimation.Animation;

/// <summary>
/// Where everything sits in one frame format. <see cref="Landscape" /> (1920x1080: the rosette centred, the title
/// bottom-left, the counters bottom-right) is the live window's format; <see cref="Portrait" /> (1080x1920: the
/// rosette and its title at the top, the counters at the bottom) is rendered alongside it.
/// </summary>
internal sealed class Layout
{
    /// <summary>The landscape frame, 1920x1080.</summary>
    public static readonly Layout Landscape = new Layout
    {
        Name = "landscape",
        Width = 1920,
        Height = 1080,
        Sun = new PointF(960, 468),
        EarthOrbitRadius = 398,
        SkyGlowRadius = 1026,
        TitleX = 88,
        TitleY = 992,
        TitleSize = 50,
        SubtitleX = 91,
        SubtitleY = 1034,
        SubtitleSize = 22,
        TextAlign = TextAlign.Left,
        RuleY = 912,
        RuleLeft = 90,
        RuleRight = 1830,
        CounterX = new float[] { 1300, 1520, 1730 },
        CounterLabelOffset = 2,
        CounterAlign = TextAlign.Left,
        LabelY = 968,
        LabelSize = 15,
        ValueY = 1030,
        ValueSize = 44,
        SafeArea = SafeAreaGuides.BroadcastActionSafe,
    };

    /// <summary>
    /// The portrait frame, 1080x1920, laid out for an ordinary (non-ad) Instagram Reel: everything sits inside
    /// x 120..960 and y 250..1500 (clear of the status bar and header at the top, the caption and action buttons at
    /// the bottom and right), which also keeps it inside the 4:5 feed-preview crop (y 285..1635). Its
    /// <see cref="SafeArea" /> guide is the library's feed-preview crop combined with its ordinary-post margins.
    /// </summary>
    public static readonly Layout Portrait = new Layout
    {
        Name = "portrait",
        Width = 1080,
        Height = 1920,
        Sun = new PointF(540, 700),
        EarthOrbitRadius = 400,
        SkyGlowRadius = 1100,
        TitleX = 540,
        TitleY = 1192,
        TitleSize = 60,
        SubtitleX = 540,
        SubtitleY = 1240,
        SubtitleSize = 24,
        TextAlign = TextAlign.Center,
        RuleY = 1300,
        RuleLeft = 160,
        RuleRight = 920,
        CounterX = new float[] { 262, 540, 818 },
        CounterLabelOffset = 0,
        CounterAlign = TextAlign.Center,
        LabelY = 1366,
        LabelSize = 19,
        ValueY = 1448,
        ValueSize = 62,
        SafeArea = SafeAreaGuide.Combine("Instagram Reel (ordinary post)",
            SafeAreaGuides.InstagramFeedPreview, SafeAreaGuides.OrdinaryPortraitPost),
    };

    /// <summary>The format's name (used in file and folder names).</summary>
    public string Name { get; private init; }

    /// <summary>The frame's width, pixels.</summary>
    public int Width { get; private init; }

    /// <summary>The frame's height, pixels.</summary>
    public int Height { get; private init; }

    /// <summary>The Sun, frame coordinates.</summary>
    public PointF Sun { get; private init; }

    /// <summary>Earth's orbital radius, pixels.</summary>
    public float EarthOrbitRadius { get; private init; }

    /// <summary>Venus' orbital radius, pixels.</summary>
    public float VenusOrbitRadius => (float)(EarthOrbitRadius * Timeline.VenusOrbitRatio);

    /// <summary>How much larger the rosette is than in the landscape frame (line widths and bodies scale with it).</summary>
    public float Scale => EarthOrbitRadius / 398f;

    /// <summary>How far the sky's glow reaches from the Sun.</summary>
    public float SkyGlowRadius { get; private init; }

    /// <summary>The title's anchor x.</summary>
    public float TitleX { get; private init; }

    /// <summary>The title's baseline.</summary>
    public float TitleY { get; private init; }

    /// <summary>The title's font size.</summary>
    public float TitleSize { get; private init; }

    /// <summary>The subtitle's anchor x.</summary>
    public float SubtitleX { get; private init; }

    /// <summary>The subtitle's baseline.</summary>
    public float SubtitleY { get; private init; }

    /// <summary>The subtitle's font size.</summary>
    public float SubtitleSize { get; private init; }

    /// <summary>How the title and subtitle align about their x.</summary>
    public TextAlign TextAlign { get; private init; }

    /// <summary>The hairline above the counters: its y.</summary>
    public float RuleY { get; private init; }

    /// <summary>The hairline's left end.</summary>
    public float RuleLeft { get; private init; }

    /// <summary>The hairline's right end.</summary>
    public float RuleRight { get; private init; }

    /// <summary>The three counters' anchor x (Earth years, Venus years, threads).</summary>
    public float[] CounterX { get; private init; } = Array.Empty<float>();

    /// <summary>How far a counter's label sits right of its value's anchor.</summary>
    public float CounterLabelOffset { get; private init; }

    /// <summary>How the counters align about their x.</summary>
    public TextAlign CounterAlign { get; private init; }

    /// <summary>The counter labels' baseline.</summary>
    public float LabelY { get; private init; }

    /// <summary>The counter labels' font size.</summary>
    public float LabelSize { get; private init; }

    /// <summary>The counter values' baseline.</summary>
    public float ValueY { get; private init; }

    /// <summary>The counter values' font size.</summary>
    public float ValueSize { get; private init; }

    /// <summary>The safe-area guide the format is checked against (and shown with, on request).</summary>
    public SafeAreaGuide SafeArea { get; private init; }

    /// <summary>Earth's position on a given day, frame coordinates.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The position.</returns>
    public PointF Earth(double day) => Timeline.OnOrbit(Sun, EarthOrbitRadius, Timeline.EarthYearDays, day);

    /// <summary>Venus' position on a given day, frame coordinates.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The position.</returns>
    public PointF Venus(double day) => Timeline.OnOrbit(Sun, VenusOrbitRadius, Timeline.VenusYearDays, day);
}
