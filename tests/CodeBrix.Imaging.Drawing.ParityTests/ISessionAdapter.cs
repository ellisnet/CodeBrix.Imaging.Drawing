using System;
using System.Collections.Generic;
using CodeBrix.Imaging;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// A backend-neutral wrapper around one DrawingSession, expressed entirely in
/// CodeBrix.Imaging value types (which both backends share). Each parity scenario is
/// written once against this interface and executed against both backends.
/// </summary>
internal interface ISessionAdapter : IDisposable
{
    /// <summary>The backend's display name (for failure messages).</summary>
    string BackendName { get; }

    /// <summary>Adds a layer and makes it active when it is the first.</summary>
    /// <param name="name">The layer name.</param>
    /// <param name="color">The layer color.</param>
    void AddLayer(string name, Color color);

    /// <summary>Makes a previously added layer the active one.</summary>
    /// <param name="name">The layer name.</param>
    void SetActiveLayer(string name);

    /// <summary>Sets the width of newly drawn strokes, in calibrated units.</summary>
    /// <param name="width">The stroke width.</param>
    void SetStrokeWidth(float width);

    /// <summary>Sets the layer compositing opacity.</summary>
    /// <param name="opacity">The alpha, 0-255.</param>
    void SetLayerOpacity(byte opacity);

    /// <summary>Sets the drawing-rectangle background fill color.</summary>
    /// <param name="color">The fill color.</param>
    void SetBackgroundFillColor(Color color);

    /// <summary>Decodes and sets the background image.</summary>
    /// <param name="encodedImage">The encoded image bytes.</param>
    void SetBackgroundImage(byte[] encodedImage);

    /// <summary>Draws a freehand stroke through the given normalized (0..1) positions.</summary>
    /// <param name="normalizedPoints">The stroke's positions.</param>
    void StrokeThrough(IReadOnlyList<(float X, float Y)> normalizedPoints);

    /// <summary>Draws a line shape.</summary>
    void DrawLine(float x1, float y1, float x2, float y2, float thickness, Color? color = null);

    /// <summary>Draws an arrow shape.</summary>
    void DrawArrow(float x1, float y1, float x2, float y2, float thickness, Color? color = null);

    /// <summary>Draws a circle shape.</summary>
    void DrawCircle(float centerX, float centerY, float radius, float thickness, Color? color = null, bool filled = false);

    /// <summary>Draws an ellipse shape.</summary>
    void DrawEllipse(float centerX, float centerY, float radiusX, float radiusY, float thickness, Color? color = null, bool filled = false);

    /// <summary>Draws a rectangle shape.</summary>
    void DrawRectangle(float x, float y, float width, float height, float thickness, Color? color = null, bool filled = false, float cornerRadius = 0f);

    /// <summary>Draws a polyline shape.</summary>
    void DrawPolyline(IReadOnlyList<(float X, float Y)> points, float thickness, Color? color = null, bool closed = false, bool filled = false);

    /// <summary>Exports the drawing as PNG bytes.</summary>
    /// <param name="outputSize">The output pixel size.</param>
    /// <param name="includeBackground">Whether the background renders behind the layers.</param>
    /// <returns>The PNG bytes.</returns>
    byte[] ExportPng(Size outputSize, bool includeBackground = true);

    /// <summary>Exports the drawing as JPEG bytes.</summary>
    /// <param name="outputSize">The output pixel size.</param>
    /// <param name="quality">The JPEG quality.</param>
    /// <returns>The JPEG bytes.</returns>
    byte[] ExportJpeg(Size outputSize, int quality);
}
