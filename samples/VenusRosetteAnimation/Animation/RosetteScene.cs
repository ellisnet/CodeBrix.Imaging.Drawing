using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;
using CodeBrix.Imaging.PixelFormats;
using SkiaSharp;

namespace VenusRosetteAnimation.Animation;

/// <summary>
/// The animation as a CodeBrix.Imaging.Drawing session with four layers, bottom to top:
/// <list type="bullet">
///   <item><b>Sky</b> - the backdrop (a radial gradient fill) and starfield.</item>
///   <item><b>Stage</b> - the orbits, the Sun, the title and the counter labels.</item>
///   <item><b>Threads</b> - every thread of the eight years plus a ring at each inferior conjunction, all added once,
///   each with a start time (the moment it lands). They are in landing order, so while playing forward the layer's
///   cache rasterizes just the newly landed ones each frame; seeking back needs no rebuilding.</item>
///   <item><b>Now</b> - the moving planets, the thread being drawn and the counter values: animated shapes, which
///   the session rebuilds for each moment.</item>
/// </list>
/// The fade-in is the session's whole-frame <see cref="DrawingSession.Opacity" /> over the deep-violet background
/// fill, driven by the timeline's "intro" cue through the session's animations. Everything is built once and is a
/// function of the session's time, so <see cref="Show" /> is just <c>Session.Time = seconds</c>; the session then
/// renders it live or exports it. The <see cref="Animation.Layout" /> decides the frame's size, where everything
/// sits and which safe-area guide the text is checked against.
/// </summary>
internal sealed class RosetteScene : IDisposable
{
    private static readonly Color EarthColor = Color.FromRgb(108, 198, 255);
    private static readonly Color VenusColor = Color.FromRgb(255, 184, 150);
    private static readonly Color SunColor = Color.FromRgb(255, 218, 160);
    private static readonly Color TitleColor = Color.FromRgb(242, 228, 242);
    private static readonly Color DimColor = Color.FromRgb(160, 140, 178);
    private static readonly Color VeilColor = Color.FromRgb(4, 3, 9);


    /// <summary>Builds the scene (at time 0).</summary>
    /// <param name="layout">The frame format.</param>
    public RosetteScene(Layout layout)
    {
        Layout = layout;
        Session = new DrawingSession(new DrawingSessionOptions
        {
            CalibrationSize = new Size(layout.Width, layout.Height),
            LayerOpacity = 255,
            ActiveStrokeOpacity = 255,
            BackgroundFillColor = VeilColor,
            SurfaceClearColor = Color.Black,
        });
        Session.Fonts.RegisterFont(Path.Combine(AppContext.BaseDirectory, "assets", "Fonts", "Roboto-Regular.ttf"));

        DrawingLayer sky = Session.AddLayer("Sky", Color.Black);
        BuildSky(sky);

        DrawingLayer stage = Session.AddLayer("Stage", Color.White);
        BuildStage(stage);

        DrawingLayer threads = Session.AddLayer("Threads", Color.White);
        BuildThreads(threads);

        DrawingLayer now = Session.AddLayer("Now", Color.White);
        BuildNow(now);

        // The fade-in from the deep-violet background: the whole drawing, as one group.
        Session.Animations.Bind(t => Session.Opacity = (float)Timeline.Intro.Progress(t));
        Show(0);
    }

    /// <summary>The frame format.</summary>
    public Layout Layout { get; }

    /// <summary>The drawing session holding the scene.</summary>
    public DrawingSession Session { get; }

    /// <summary>The animation time last shown, seconds.</summary>
    public double Seconds { get; private set; }

    /// <summary>
    /// The safe-area overlay shown over the frame (the layout's guide), or <c>null</c> for none (the default).
    /// It shows in the live view and in stills; videos never carry it.
    /// </summary>
    public SafeAreaOverlay Overlay
    {
        get => Session.SafeAreaOverlay;
        set => Session.SafeAreaOverlay = value;
    }

    /// <summary>Puts the scene at an animation time.</summary>
    /// <param name="seconds">The time, clamped to 0..<see cref="Timeline.TotalSeconds" />.</param>
    public void Show(double seconds)
    {
        Seconds = Timeline.Clock.Clamp(seconds);
        Session.Time = Seconds;
    }

    /// <summary>
    /// Checks the scene's text against the layout's safe-area guide, at the finished frame (the longest
    /// counter values). Advisory only: it reports, it never changes the layout.
    /// </summary>
    /// <returns>The findings; empty when every line of text is clear.</returns>
    public IReadOnlyList<SafeAreaFinding> CheckSafeArea()
    {
        double seconds = Seconds;
        Show(Timeline.TotalSeconds);
        IReadOnlyList<SafeAreaFinding> findings = Session.CheckSafeArea(Layout.SafeArea, new Size(Layout.Width, Layout.Height));
        Show(seconds);
        return findings;
    }

    /// <summary>Writes the current scene as a full-size PNG (with the overlay, when one is set).</summary>
    /// <param name="path">The file to write.</param>
    public void SavePng(string path)
    {
        Size size = new Size(Layout.Width, Layout.Height);
        File.WriteAllBytes(path, Overlay == null
            ? Session.ExportPng(size, includeBackground: true)
            : Session.ExportPng(size, true, Overlay));
    }

    /// <summary>Writes the current scene into a raw frame: straight-alpha RGBA, full size, never with the overlay.</summary>
    /// <param name="rgba">The frame buffer, <see cref="Layout.Width" /> x <see cref="Layout.Height" /> x 4 bytes.</param>
    public void WriteFrame(Span<byte> rgba) => Session.ExportPixels(rgba, new Size(Layout.Width, Layout.Height));

    /// <summary>Renders the current scene onto a canvas (the live window's frame).</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="info">The canvas' size and format.</param>
    public void Render(SKCanvas canvas, SKImageInfo info) => Session.Render(canvas, info);

    /// <summary>Releases the session.</summary>
    public void Dispose() => Session.Dispose();

    /// <summary>
    /// A glowing body: a soft radial-gradient halo fading to nothing around a solid core with a pale, off-centre
    /// highlight, as a group of the two. Used for the Sun and the two planets.
    /// </summary>
    private static DrawingGroup Glow(PointF center, float coreRadius, float haloRadius, Color color, byte haloAlpha = 120)
    {
        Rgba32 c = color.ToPixel<Rgba32>();
        DrawingGroup glow = new DrawingGroup();
        glow.Add(new CircleShape(center.X, center.Y, haloRadius, new RadialGradientFill(center, haloRadius, new[]
        {
            new ColorStop(coreRadius / haloRadius, Color.FromRgba(c.R, c.G, c.B, haloAlpha)),
            new ColorStop(0.45f, Color.FromRgba(c.R, c.G, c.B, (byte)(haloAlpha / 4))),
            new ColorStop(1f, Color.FromRgba(c.R, c.G, c.B, 0)),
        })));

        Color pale = Color.FromRgb((byte)((c.R + 255) / 2), (byte)((c.G + 255) / 2), (byte)((c.B + 255) / 2));
        PointF highlight = new PointF(center.X - (coreRadius * 0.3f), center.Y - (coreRadius * 0.3f));
        glow.Add(new CircleShape(center.X, center.Y, coreRadius,
            new RadialGradientFill(highlight, coreRadius * 1.3f, pale, Color.FromRgb(c.R, c.G, c.B))));
        return glow;
    }

    private void AddText(DrawingLayer layer, string text, float x, float y, float size, Color color,
        TextAlign align, float tracking = 0)
    {
        // Every line of text is what the safe-area check measures.
        layer.AddShape(new TextShape(text, x, y, size, Session.Fonts, color, align, tracking) { IsSafeAreaChecked = true });
    }

    /// <summary>
    /// The backdrop - a deep plum glow around the Sun fading to near-black indigo at the edges - and a dim, fixed
    /// starfield (seeded, so every run and every frame shows the same stars).
    /// </summary>
    private void BuildSky(DrawingLayer sky)
    {
        Layout l = Layout;
        sky.AddShape(new RectangleShape(0, 0, l.Width, l.Height, new RadialGradientFill(l.Sun, l.SkyGlowRadius, new[]
        {
            new ColorStop(0f, Color.FromRgb(44, 20, 58)),
            new ColorStop(0.45f, Color.FromRgb(26, 13, 42)),
            new ColorStop(1f, Color.FromRgb(7, 6, 16)),
        })));

        const int starCount = 620;
        Random random = new Random(8131);
        Rgba32[] tints = { new Rgba32(232, 232, 255), new Rgba32(255, 222, 236), new Rgba32(206, 220, 255), new Rgba32(255, 238, 214) };
        for (int i = 0; i < starCount; i++)
        {
            double u = random.NextDouble();
            double v = random.NextDouble();
            float x = (float)(random.NextDouble() * l.Width);
            float y = (float)(random.NextDouble() * l.Height);
            float radius = 0.45f + (float)(1.05 * u * u);
            byte alpha = (byte)(22 + (118 * v * v * v));
            Rgba32 tint = tints[random.Next(tints.Length)];
            if (u > 0.965)
            {
                sky.AddShape(new CircleShape(x, y, radius * 3.2f, 1, Color.FromRgba(tint.R, tint.G, tint.B, (byte)(alpha / 5)), isFilled: true));
            }

            sky.AddShape(new CircleShape(x, y, radius, 1, Color.FromRgba(tint.R, tint.G, tint.B, alpha), isFilled: true));
        }
    }

    private void BuildStage(DrawingLayer stage)
    {
        Layout l = Layout;
        PointF sun = l.Sun;
        float scale = l.Scale;
        stage.AddShape(new CircleShape(sun.X, sun.Y, l.EarthOrbitRadius, 1.3f * scale, Color.FromRgba(140, 176, 240, 92)));
        stage.AddShape(new CircleShape(sun.X, sun.Y, l.VenusOrbitRadius, 1.3f * scale, Color.FromRgba(246, 160, 196, 92)));
        stage.AddShape(Glow(sun, 10f * scale, 58f * scale, SunColor, haloAlpha: 140));

        stage.AddShape(new LineShape(l.RuleLeft, l.RuleY, l.RuleRight, l.RuleY, 1f, Color.FromRgba(150, 120, 180, 70)));
        AddText(stage, "Venus Rosette", l.TitleX, l.TitleY, l.TitleSize, TitleColor, l.TextAlign, tracking: 0.02f);
        AddText(stage, "Earth and Venus, joined by a thread every three days, for eight years", l.SubtitleX, l.SubtitleY, l.SubtitleSize, DimColor, l.TextAlign);
        string[] labels = { "EARTH YEARS", "VENUS YEARS", "THREADS" };
        for (int i = 0; i < labels.Length; i++)
        {
            AddText(stage, labels[i], l.CounterX[i] + l.CounterLabelOffset, l.LabelY, l.LabelSize, DimColor, l.CounterAlign, tracking: 0.2f);
        }
    }

    /// <summary>
    /// What changes every moment, as animated shapes: each is rebuilt from the time alone. Their order is the order
    /// they draw in: the thread being drawn, Venus, Earth, then the three counter values.
    /// </summary>
    private void BuildNow(DrawingLayer now)
    {
        float scale = Layout.Scale;

        // The thread being drawn (a soft wide line under a fine one), fading away once the eight years are complete.
        now.AddShape(new AnimatedShape(t =>
        {
            double live = 1 - Timeline.Settle.Progress(t);
            if (live <= 0) return null;
            double day = Timeline.DayAt(t);
            double fraction = day / Timeline.TotalDays;
            PointF earth = Layout.Earth(day);
            PointF venus = Layout.Venus(day);
            DrawingGroup thread = new DrawingGroup();
            thread.Add(new LineShape(earth.X, earth.Y, venus.X, venus.Y, 7f * scale, Timeline.ThreadRamp.At(fraction, (byte)(40 * live))));
            thread.Add(new LineShape(earth.X, earth.Y, venus.X, venus.Y, 2f * scale, Timeline.ThreadRamp.At(fraction, (byte)(235 * live))));
            return thread;
        }));

        now.AddShape(new AnimatedShape(t => Glow(Layout.Venus(Timeline.DayAt(t)), 7f * scale, 30f * scale, VenusColor)));
        now.AddShape(new AnimatedShape(t => Glow(Layout.Earth(Timeline.DayAt(t)), 8f * scale, 34f * scale, EarthColor)));

        float[] x = Layout.CounterX;
        AddCounter(now, t => (Timeline.DayAt(t) / Timeline.EarthYearDays).ToString("0.00", CultureInfo.InvariantCulture), x[0], t => EarthColor);
        AddCounter(now, t => (Timeline.DayAt(t) / Timeline.VenusYearDays).ToString("0.00", CultureInfo.InvariantCulture), x[1], t => VenusColor);
        AddCounter(now, t => Timeline.ThreadsBy(Timeline.DayAt(t)).ToString(CultureInfo.InvariantCulture), x[2],
            t => Timeline.ThreadRamp.At(Timeline.DayAt(t) / Timeline.TotalDays, 255));
    }

    private void AddCounter(DrawingLayer layer, Func<double, string> text, float x, Func<double, Color> color)
    {
        // A counter's value is text the safe-area check measures, like every other line of text.
        layer.AddShape(new AnimatedShape(t => new TextShape(text(t), x, Layout.ValueY, Layout.ValueSize, Session.Fonts, color(t), Layout.CounterAlign))
        {
            IsSafeAreaChecked = true,
        });
    }

    /// <summary>
    /// Every thread (one every <see cref="Timeline.ThreadEveryDays" /> days) and a ring at every inferior conjunction,
    /// in the order they land, each starting to exist at the moment its day comes.
    /// </summary>
    private void BuildThreads(DrawingLayer threads)
    {
        List<(double Day, DrawingShape Shape)> events = new List<(double, DrawingShape)>();
        float scale = Layout.Scale;
        for (int k = 0; k < Timeline.TotalThreads; k++)
        {
            double day = k * Timeline.ThreadEveryDays;
            PointF earth = Layout.Earth(day);
            PointF venus = Layout.Venus(day);
            Color color = Timeline.ThreadRamp.At(day / Timeline.TotalDays, 40);
            events.Add((day, new LineShape(earth.X, earth.Y, venus.X, venus.Y, 1.1f * scale, color)));
        }

        // A small ring where Venus overtakes Earth (every 583.9 days): five of them, nearly a pentagram, before the
        // sixth lands almost on top of the first.
        for (int k = 0; k * Timeline.SynodicDays <= Timeline.TotalDays; k++)
        {
            double day = k * Timeline.SynodicDays;
            PointF venus = Layout.Venus(day);
            events.Add((day + 1e-6, new CircleShape(venus.X, venus.Y, 9f * scale, 1.6f * scale, Color.FromRgba(255, 208, 230, 190))));
        }

        events.Sort((a, b) => a.Day.CompareTo(b.Day));
        foreach ((double day, DrawingShape shape) in events)
        {
            shape.StartTime = Timeline.FirstTimeAt(day);
            threads.AddShape(shape);
        }
    }
}
