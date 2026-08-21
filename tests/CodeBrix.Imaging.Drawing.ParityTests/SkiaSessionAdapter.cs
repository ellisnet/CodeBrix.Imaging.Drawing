extern alias skia;

using System.Collections.Generic;
using CodeBrix.Imaging;
using SkiaSession = skia::CodeBrix.Imaging.Drawing.DrawingSession;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// The <see cref="ISessionAdapter"/> over the SkiaSharp-backed CodeBrix.Imaging.Drawing
/// library (referenced under the <c>skia</c> extern alias).
/// </summary>
internal sealed class SkiaSessionAdapter : ISessionAdapter
{
    private readonly SkiaSession _session = new SkiaSession();

    /// <inheritdoc />
    public string BackendName => "SkiaSharp";

    /// <inheritdoc />
    public void AddLayer(string name, Color color) => _session.AddLayer(name, color);

    /// <inheritdoc />
    public void SetActiveLayer(string name) => _session.ActiveLayer = _session.GetLayer(name);

    /// <inheritdoc />
    public void SetStrokeWidth(float width) => _session.StrokeWidth = width;

    /// <inheritdoc />
    public void SetLayerOpacity(byte opacity) => _session.LayerOpacity = opacity;

    /// <inheritdoc />
    public void SetBackgroundFillColor(Color color) => _session.BackgroundFillColor = color;

    /// <inheritdoc />
    public void SetBackgroundImage(byte[] encodedImage) => _session.SetBackgroundImage(encodedImage);

    /// <inheritdoc />
    public void StrokeThrough(IReadOnlyList<(float X, float Y)> normalizedPoints)
    {
        _session.PointerPressedNormalized(normalizedPoints[0].X, normalizedPoints[0].Y);
        for (int i = 1; i < normalizedPoints.Count; i++)
        {
            _session.PointerMovedNormalized(normalizedPoints[i].X, normalizedPoints[i].Y);
        }
        _session.PointerReleased();
    }

    /// <inheritdoc />
    public void DrawLine(float x1, float y1, float x2, float y2, float thickness, Color? color = null)
        => _session.DrawLine(x1, y1, x2, y2, thickness, color);

    /// <inheritdoc />
    public void DrawArrow(float x1, float y1, float x2, float y2, float thickness, Color? color = null)
        => _session.DrawArrow(x1, y1, x2, y2, thickness, color);

    /// <inheritdoc />
    public void DrawCircle(float centerX, float centerY, float radius, float thickness, Color? color = null, bool filled = false)
        => _session.DrawCircle(centerX, centerY, radius, thickness, color, filled);

    /// <inheritdoc />
    public void DrawEllipse(float centerX, float centerY, float radiusX, float radiusY, float thickness, Color? color = null, bool filled = false)
        => _session.DrawEllipse(centerX, centerY, radiusX, radiusY, thickness, color, filled);

    /// <inheritdoc />
    public void DrawRectangle(float x, float y, float width, float height, float thickness, Color? color = null, bool filled = false, float cornerRadius = 0f)
        => _session.DrawRectangle(x, y, width, height, thickness, color, filled, cornerRadius);

    /// <inheritdoc />
    public void DrawPolyline(IReadOnlyList<(float X, float Y)> points, float thickness, Color? color = null, bool closed = false, bool filled = false)
        => _session.DrawPolyline(points, thickness, color, closed, filled);

    /// <inheritdoc />
    public byte[] ExportPng(Size outputSize, bool includeBackground = true)
        => _session.ExportPng(outputSize, includeBackground);

    /// <inheritdoc />
    public byte[] ExportJpeg(Size outputSize, int quality) => _session.ExportJpeg(outputSize, quality);

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();
}
