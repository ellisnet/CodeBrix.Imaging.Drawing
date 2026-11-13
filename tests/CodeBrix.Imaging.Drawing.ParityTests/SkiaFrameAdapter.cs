extern alias skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using SkiaColorRamp = skia::CodeBrix.Imaging.Drawing.Animation.ColorRamp;
using SkiaColorStop = skia::CodeBrix.Imaging.Drawing.Fills.ColorStop;
using SkiaCubicBezierEasing = skia::CodeBrix.Imaging.Drawing.Animation.CubicBezierEasing;
using SkiaEasing = skia::CodeBrix.Imaging.Drawing.Animation.Easing;
using SkiaEasingKind = skia::CodeBrix.Imaging.Drawing.Animation.EasingKind;
using SkiaGradientSpread = skia::CodeBrix.Imaging.Drawing.Fills.GradientSpread;
using SkiaLinearGradientFill = skia::CodeBrix.Imaging.Drawing.Fills.LinearGradientFill;
using SkiaParticleEmitter = skia::CodeBrix.Imaging.Drawing.Planes.DrawingParticleEmitter;
using SkiaParticleSystem = skia::CodeBrix.Imaging.Drawing.Planes.DrawingParticleSystem;
using SkiaPlaneImage = skia::CodeBrix.Imaging.Drawing.Planes.DrawingPlaneImage;
using SkiaRadialGradientFill = skia::CodeBrix.Imaging.Drawing.Fills.RadialGradientFill;
using SkiaSafeAreaFinding = skia::CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaFinding;
using SkiaSafeAreaGuide = skia::CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaGuide;
using SkiaSafeAreaGuides = skia::CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaGuides;
using SkiaSafeAreaOverlay = skia::CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaOverlay;
using SkiaSafeAreaZone = skia::CodeBrix.Imaging.Drawing.SafeAreas.SafeAreaZone;
using SkiaSession = skia::CodeBrix.Imaging.Drawing.DrawingSession;
using SkiaSessionOptions = skia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using SkiaShapes = skia::CodeBrix.Imaging.Drawing.Shapes;
using SkiaTextAlign = skia::CodeBrix.Imaging.Drawing.Text.TextAlign;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// The <see cref="IFrameAdapter"/> over the SkiaSharp-backed CodeBrix.Imaging.Drawing
/// library (referenced under the <c>skia</c> extern alias).
/// </summary>
internal sealed class SkiaFrameAdapter : IFrameAdapter
{
    private readonly SkiaSession _session;

    /// <summary>Creates the adapter over a new session.</summary>
    /// <param name="calibrationSize">The session's calibrated drawing space.</param>
    public SkiaFrameAdapter(Size calibrationSize)
    {
        _session = new SkiaSession(new SkiaSessionOptions { CalibrationSize = calibrationSize, LayerOpacity = 255 });
    }

    /// <inheritdoc />
    public string BackendName => "SkiaSharp";

    /// <inheritdoc />
    public void AddLayer(string name, Color color) => _session.ActiveLayer = _session.AddLayer(name, color);

    /// <inheritdoc />
    public void SetBackgroundFillColor(Color color) => _session.BackgroundFillColor = color;

    /// <inheritdoc />
    public void SetOpacity(float opacity) => _session.Opacity = opacity;

    /// <inheritdoc />
    public void SetLayerOpacity(string layerName, float opacity) => _session.GetLayer(layerName).Opacity = opacity;

    /// <inheritdoc />
    public void MarkLayerForSafeArea(string layerName) => _session.GetLayer(layerName).IsSafeAreaChecked = true;

    /// <inheritdoc />
    public void RegisterFont(string path) => _session.Fonts.RegisterFont(path);

    /// <inheritdoc />
    public void DrawRectangle(float x, float y, float width, float height, Color color)
        => _session.DrawRectangle(x, y, width, height, color: color, filled: true);

    /// <inheritdoc />
    public void DrawLinearGradientRectangle(float x, float y, float width, float height,
        PointF start, PointF end, IReadOnlyList<(float Position, Color Color)> stops, int spread)
        => _session.DrawShape(new SkiaShapes.RectangleShape(x, y, width, height,
            new SkiaLinearGradientFill(start, end, ToStops(stops), (SkiaGradientSpread)spread)));

    /// <inheritdoc />
    public void DrawRadialGradientCircle(float centerX, float centerY, float radius,
        PointF gradientCenter, float gradientRadius, IReadOnlyList<(float Position, Color Color)> stops)
        => _session.DrawShape(new SkiaShapes.CircleShape(centerX, centerY, radius,
            new SkiaRadialGradientFill(gradientCenter, gradientRadius, ToStops(stops))));

    /// <inheritdoc />
    public (float Width, RectangleF? Bounds) DrawText(string text, float x, float y, float size, Color color, int align, float tracking)
    {
        SkiaShapes.TextShape shape = _session.DrawText(text, x, y, size, color, (SkiaTextAlign)align, tracking);
        return (shape.Width, shape.GetBounds());
    }

    /// <inheritdoc />
    public IReadOnlyList<string> CheckSafeArea(string presetName, Size frameSize)
    {
        var lines = new List<string>();
        foreach (SkiaSafeAreaFinding finding in _session.CheckSafeArea(Preset(presetName), frameSize))
        {
            lines.Add(String.Format(CultureInfo.InvariantCulture, "{0} #{1} {2}/{3} {4} {5}", finding.Layer.Name,
                finding.ElementIndex, finding.GuideName, finding.ZoneName, finding.Overlap, finding.FrameOverlap));
        }
        return lines;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetZones(string presetName, Size frameSize)
    {
        var lines = new List<string>();
        foreach (SkiaSafeAreaZone zone in Preset(presetName).GetUnsafeZones(new SizeF(frameSize.Width, frameSize.Height)))
        {
            lines.Add(zone.ToString());
        }
        return lines;
    }

    /// <inheritdoc />
    public byte[] ExportPixels(Size outputSize, bool includeBackground) => _session.ExportPixels(outputSize, includeBackground);

    /// <inheritdoc />
    public byte[] ExportPng(Size outputSize, bool includeBackground) => _session.ExportPng(outputSize, includeBackground);

    /// <inheritdoc />
    public byte[] ExportPngWithOverlay(Size outputSize, bool includeBackground, string presetName, Color maskColor)
        => _session.ExportPng(outputSize, includeBackground, new SkiaSafeAreaOverlay(Preset(presetName)) { MaskColor = maskColor });

    /// <inheritdoc />
    public byte[] ExportPlaneImageRgba(Size outputSize, bool includeBackground)
        => _session.ExportPlaneImage(outputSize, includeBackground).ToRgba();

    /// <inheritdoc />
    public byte[] ExportIntoPlaneImageRgba(Size outputSize, bool includeBackground)
    {
        var image = new SkiaPlaneImage(outputSize).Fill(0.25f, 0.5f, 0.75f, 0.5f);
        _session.ExportPlaneImage(image, includeBackground);
        return image.ToRgba();
    }

    /// <inheritdoc />
    public float[] ExportMask(Size outputSize, string layerName)
        => layerName == null ? _session.ExportMask(outputSize).Data : _session.ExportMask(_session.GetLayer(layerName), outputSize).Data;

    /// <inheritdoc />
    public (int Count, RectangleF? Bounds) DrawParticleFountain(long seed, float x, float y, double time, float softness)
    {
        var system = new SkiaParticleSystem(seed);
        system.AddEmitter(new SkiaParticleEmitter
        {
            X = x, Y = y, Rate = 30f, Burst = 8, Lifetime = 2f, LifetimeVariation = 0.5f, Direction = -90f, Spread = 69f,
            Speed = 140f, SpeedVariation = 30f, GravityY = 90f, StartSize = 26f, EndSize = 6f, StartOpacity = 1f, EndOpacity = 0.1f,
            StartColor = Color.FromRgb(255, 220, 120), EndColor = Color.FromRgba(255, 80, 30, 160),
        });
        SkiaShapes.ParticlesShape shape = _session.DrawParticles(system, time, softness);
        return (shape.Particles.Count, shape.GetBounds());
    }

    /// <inheritdoc />
    public double[] EvaluateAllEasings(double t)
    {
        SkiaEasingKind[] kinds = Enum.GetValues<SkiaEasingKind>();
        var values = new double[kinds.Length];
        for (int i = 0; i < kinds.Length; i++) { values[i] = SkiaEasing.Evaluate(kinds[i], t); }
        return values;
    }

    /// <inheritdoc />
    public double EvaluateCubicBezier(double x1, double y1, double x2, double y2, double t)
        => new SkiaCubicBezierEasing(x1, y1, x2, y2).Evaluate(t);

    /// <inheritdoc />
    public Color RampAt(Color[] colors, double t) => new SkiaColorRamp(colors).At(t);

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();

    private static SkiaSafeAreaGuide Preset(string name)
        => (SkiaSafeAreaGuide)typeof(SkiaSafeAreaGuides).GetProperty(name).GetValue(null);

    private static SkiaColorStop[] ToStops(IReadOnlyList<(float Position, Color Color)> stops)
    {
        var result = new SkiaColorStop[stops.Count];
        for (int i = 0; i < stops.Count; i++) { result[i] = new SkiaColorStop(stops[i].Position, stops[i].Color); }
        return result;
    }
}
