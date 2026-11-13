using System;
using System.Collections.Generic;
using CodeBrix.Imaging;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// A backend-neutral wrapper around one DrawingSession for the animation-frame features
/// (raw RGBA frames, plane-image and mask exports, gradient fills, text, particles, whole-frame
/// fades, safe areas, easing and color ramps), expressed in CodeBrix.Imaging value types and plain values so each scenario is
/// written once and executed against both backends.
/// </summary>
internal interface IFrameAdapter : IDisposable
{
    /// <summary>The backend's display name (for failure messages).</summary>
    string BackendName { get; }

    /// <summary>Adds a layer (opaque compositing) and makes it active.</summary>
    /// <param name="name">The layer name.</param>
    /// <param name="color">The layer color.</param>
    void AddLayer(string name, Color color);

    /// <summary>Sets the background fill color.</summary>
    /// <param name="color">The fill color.</param>
    void SetBackgroundFillColor(Color color);

    /// <summary>Sets the whole-drawing opacity.</summary>
    /// <param name="opacity">The opacity, 0..1.</param>
    void SetOpacity(float opacity);

    /// <summary>Sets one layer's opacity multiplier.</summary>
    /// <param name="layerName">The layer.</param>
    /// <param name="opacity">The opacity, 0..1.</param>
    void SetLayerOpacity(string layerName, float opacity);

    /// <summary>Marks a layer for safe-area checks.</summary>
    /// <param name="layerName">The layer.</param>
    void MarkLayerForSafeArea(string layerName);

    /// <summary>Registers a font file with the session.</summary>
    /// <param name="path">The font file.</param>
    void RegisterFont(string path);

    /// <summary>Draws a filled rectangle in a flat color.</summary>
    void DrawRectangle(float x, float y, float width, float height, Color color);

    /// <summary>Draws a rectangle filled with a linear gradient.</summary>
    void DrawLinearGradientRectangle(float x, float y, float width, float height,
        PointF start, PointF end, IReadOnlyList<(float Position, Color Color)> stops, int spread);

    /// <summary>Draws a circle filled with a radial gradient.</summary>
    void DrawRadialGradientCircle(float centerX, float centerY, float radius,
        PointF gradientCenter, float gradientRadius, IReadOnlyList<(float Position, Color Color)> stops);

    /// <summary>Draws a line of text and reports its measured width and bounds.</summary>
    (float Width, RectangleF? Bounds) DrawText(string text, float x, float y, float size, Color color, int align, float tracking);

    /// <summary>Checks the marked layers against a preset guide, as comparable strings.</summary>
    IReadOnlyList<string> CheckSafeArea(string presetName, Size frameSize);

    /// <summary>The unsafe zones of a preset guide on a frame, as comparable strings.</summary>
    IReadOnlyList<string> GetZones(string presetName, Size frameSize);

    /// <summary>Exports the drawing as raw straight-alpha RGBA bytes.</summary>
    byte[] ExportPixels(Size outputSize, bool includeBackground);

    /// <summary>Exports the drawing as PNG bytes.</summary>
    byte[] ExportPng(Size outputSize, bool includeBackground);

    /// <summary>Exports the drawing as PNG bytes with a preset guide's overlay.</summary>
    byte[] ExportPngWithOverlay(Size outputSize, bool includeBackground, string presetName, Color maskColor);

    /// <summary>Exports the drawing as a float plane image and returns that image's 8-bit RGBA bytes.</summary>
    byte[] ExportPlaneImageRgba(Size outputSize, bool includeBackground);

    /// <summary>Exports the drawing into an existing, pre-filled plane image and returns its 8-bit RGBA bytes.</summary>
    byte[] ExportIntoPlaneImageRgba(Size outputSize, bool includeBackground);

    /// <summary>Exports the coverage mask of every layer, or of one named layer.</summary>
    /// <param name="outputSize">The mask size.</param>
    /// <param name="layerName">One layer; or <c>null</c> for the whole drawing.</param>
    float[] ExportMask(Size outputSize, string layerName);

    /// <summary>Draws a fixed particle fountain at one moment; reports the particle count and bounds.</summary>
    (int Count, RectangleF? Bounds) DrawParticleFountain(long seed, float x, float y, double time, float softness);

    /// <summary>Evaluates every named easing curve at a progress value, in enum order.</summary>
    double[] EvaluateAllEasings(double t);

    /// <summary>Evaluates a cubic-bezier easing.</summary>
    double EvaluateCubicBezier(double x1, double y1, double x2, double y2, double t);

    /// <summary>Evaluates an evenly spaced color ramp.</summary>
    Color RampAt(Color[] colors, double t);
}
