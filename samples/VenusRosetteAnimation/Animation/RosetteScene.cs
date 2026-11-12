using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using SkiaSharp;
using VenusRosetteAnimation.Shapes;

namespace VenusRosetteAnimation.Animation;

/// <summary>
/// The animation as a CodeBrix.Imaging.Drawing session with four layers, bottom to top:
/// <list type="bullet">
///   <item><b>Sky</b> - the backdrop and starfield (drawn once).</item>
///   <item><b>Stage</b> - the orbits, the Sun, the title and the counter labels (drawn once).</item>
///   <item><b>Threads</b> - every thread drawn so far, plus a ring at each inferior conjunction. Threads are only
///   ever APPENDED while playing forward, so the layer's cache rasterizes just the new ones each frame.</item>
///   <item><b>Now</b> - the moving planets, the thread being drawn, the counter values and the fade-in veil
///   (rebuilt every frame).</item>
/// </list>
/// <see cref="Show" /> puts the scene at an animation time; the session then renders it live or exports it. The
/// <see cref="Animation.Layout" /> decides the frame's size and where everything sits.
/// </summary>
internal sealed class RosetteScene : IDisposable
{
    private static readonly Color EarthColor = Color.FromRgb(108, 198, 255);
    private static readonly Color VenusColor = Color.FromRgb(255, 184, 150);
    private static readonly Color SunColor = Color.FromRgb(255, 218, 160);
    private static readonly Color TitleColor = Color.FromRgb(242, 228, 242);
    private static readonly Color DimColor = Color.FromRgb(160, 140, 178);

    private readonly DrawingLayer threads;
    private readonly DrawingLayer now;
    private readonly List<(double Day, DrawingShape Shape)> events = new List<(double, DrawingShape)>();

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
            BackgroundFillColor = Color.Black,
            SurfaceClearColor = Color.Black,
        });

        DrawingLayer sky = Session.AddLayer("Sky", Color.Black);
        sky.AddShape(new SkyShape(layout.Width, layout.Height, layout.Sun.X, layout.Sun.Y, layout.SkyGlowRadius));

        DrawingLayer stage = Session.AddLayer("Stage", Color.White);
        BuildStage(stage);

        threads = Session.AddLayer("Threads", Color.White);
        BuildEvents();

        now = Session.AddLayer("Now", Color.White);
        Show(0);
    }

    /// <summary>The frame format.</summary>
    public Layout Layout { get; }

    /// <summary>The drawing session holding the scene.</summary>
    public DrawingSession Session { get; }

    /// <summary>The animation time last shown, seconds.</summary>
    public double Seconds { get; private set; }

    /// <summary>Puts the scene at an animation time.</summary>
    /// <param name="seconds">The time, clamped to 0..<see cref="Timeline.TotalSeconds" />.</param>
    public void Show(double seconds)
    {
        seconds = Math.Clamp(seconds, 0, Timeline.TotalSeconds);
        Seconds = seconds;
        double day = Timeline.DayAt(seconds);
        float scale = Layout.Scale;

        // The threads (and conjunction rings) up to this day: appended going forward, rebuilt after a seek back.
        int due = 0;
        while (due < events.Count && events[due].Day <= day + 1e-9) due++;
        if (due < threads.ElementCount) threads.Clear();
        for (int i = threads.ElementCount; i < due; i++) threads.AddShape(events[i].Shape);

        now.Clear();
        double fraction = day / Timeline.TotalDays;
        PointF earth = Layout.Earth(day);
        PointF venus = Layout.Venus(day);

        // The thread being drawn, fading away once the eight years are complete.
        double live = 1 - Timeline.SmoothStep((seconds - Timeline.DrawEndSeconds) / 0.4);
        if (live > 0)
        {
            now.AddShape(new LineShape(earth.X, earth.Y, venus.X, venus.Y, 7f * scale, Timeline.ThreadColor(fraction, (byte)(40 * live))));
            now.AddShape(new LineShape(earth.X, earth.Y, venus.X, venus.Y, 2f * scale, Timeline.ThreadColor(fraction, (byte)(235 * live))));
        }

        now.AddShape(new GlowShape(venus, 7f * scale, 30f * scale, VenusColor));
        now.AddShape(new GlowShape(earth, 8f * scale, 34f * scale, EarthColor));

        string earthYears = (day / Timeline.EarthYearDays).ToString("0.00", CultureInfo.InvariantCulture);
        string venusYears = (day / Timeline.VenusYearDays).ToString("0.00", CultureInfo.InvariantCulture);
        string threadCount = Timeline.ThreadsBy(day).ToString(CultureInfo.InvariantCulture);
        float[] x = Layout.CounterX;
        now.AddShape(new TextShape(earthYears, x[0], Layout.ValueY, Layout.ValueSize, EarthColor, Layout.CounterAlign));
        now.AddShape(new TextShape(venusYears, x[1], Layout.ValueY, Layout.ValueSize, VenusColor, Layout.CounterAlign));
        now.AddShape(new TextShape(threadCount, x[2], Layout.ValueY, Layout.ValueSize, Timeline.ThreadColor(fraction, 255), Layout.CounterAlign));

        double veil = 1 - Timeline.SmoothStep(seconds / Timeline.IntroSeconds);
        if (veil > 0)
        {
            Color shade = Color.FromRgba(4, 3, 9, (byte)Math.Round(255 * veil));
            now.AddShape(new RectangleShape(0, 0, Layout.Width, Layout.Height, 1, shade, isFilled: true));
        }
    }

    /// <summary>Writes the current scene as a full-size PNG.</summary>
    /// <param name="path">The file to write.</param>
    public void SavePng(string path)
    {
        using FileStream stream = File.Create(path);
        Session.ExportPng(stream, new Size(Layout.Width, Layout.Height), includeBackground: true);
    }

    /// <summary>Renders the current scene onto a canvas (the live window's frame).</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="info">The canvas' size and format.</param>
    public void Render(SKCanvas canvas, SKImageInfo info) => Session.Render(canvas, info);

    /// <summary>Releases the session.</summary>
    public void Dispose() => Session.Dispose();

    private void BuildStage(DrawingLayer stage)
    {
        Layout l = Layout;
        PointF sun = l.Sun;
        float scale = l.Scale;
        stage.AddShape(new CircleShape(sun.X, sun.Y, l.EarthOrbitRadius, 1.3f * scale, Color.FromRgba(140, 176, 240, 92)));
        stage.AddShape(new CircleShape(sun.X, sun.Y, l.VenusOrbitRadius, 1.3f * scale, Color.FromRgba(246, 160, 196, 92)));
        stage.AddShape(new GlowShape(sun, 10f * scale, 58f * scale, SunColor, haloAlpha: 140));

        stage.AddShape(new LineShape(l.RuleLeft, l.RuleY, l.RuleRight, l.RuleY, 1f, Color.FromRgba(150, 120, 180, 70)));
        stage.AddShape(new TextShape("Venus Rosette", l.TitleX, l.TitleY, l.TitleSize, TitleColor, l.TextAlign, tracking: 0.02f));
        stage.AddShape(new TextShape("Earth and Venus, joined by a thread every three days, for eight years", l.SubtitleX, l.SubtitleY, l.SubtitleSize, DimColor, l.TextAlign));
        string[] labels = { "EARTH YEARS", "VENUS YEARS", "THREADS" };
        for (int i = 0; i < labels.Length; i++)
        {
            stage.AddShape(new TextShape(labels[i], l.CounterX[i] + l.CounterLabelOffset, l.LabelY, l.LabelSize, DimColor, l.CounterAlign, tracking: 0.2f));
        }
    }

    private void BuildEvents()
    {
        float scale = Layout.Scale;
        for (int k = 0; k < Timeline.TotalThreads; k++)
        {
            double day = k * Timeline.ThreadEveryDays;
            PointF earth = Layout.Earth(day);
            PointF venus = Layout.Venus(day);
            Color color = Timeline.ThreadColor(day / Timeline.TotalDays, 40);
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
    }
}
