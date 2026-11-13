using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Painting;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// A stroke layout turned into per-pixel data, built once and reused by every painting on that layout: which
/// stroke owns each frame pixel, the bristle texture there, and how close the pixel is to a stroke's outline.
/// <para>
/// All three come from a CodeBrix.Imaging.Drawing ownership map: every stroke is drawn on a layer, back to
/// front, and the map gives each pixel to the last stroke covering its centre - hard-edged, by the library's
/// one pixel rule, so both drawing packages make the same map. Pixels no stroke covers go to the nearest
/// stroke. The bristle ridges are a function of where a pixel lies across and along its own stroke; the
/// outlines are the seams between strokes, blurred.
/// </para>
/// </summary>
internal sealed class StrokeMap
{
    private const int W = Timeline.Width;
    private const int H = Timeline.Height;

    private StrokeMap(DrawingOwnershipMap map, DrawingPlane bristle, DrawingPlane outline)
    {
        Map = map;
        Bristle = bristle;
        Outline = outline;
    }

    /// <summary>The number of strokes.</summary>
    public int StrokeCount => Map.ElementCount;

    /// <summary>Which stroke owns each frame pixel (every pixel has one).</summary>
    public DrawingOwnershipMap Map { get; }

    /// <summary>For each frame pixel, the brightness of the bristle ridges there (around 1).</summary>
    public DrawingPlane Bristle { get; }

    /// <summary>For each frame pixel, 0 away from stroke outlines rising to 1 on them.</summary>
    public DrawingPlane Outline { get; }

    /// <summary>Builds the map of a layout.</summary>
    /// <param name="layout">The strokes, in painting order.</param>
    /// <returns>The map.</returns>
    public static StrokeMap Build(StrokeLayout layout)
    {
        DrawingOwnershipMap map;
        using (DrawingSession session = new DrawingSession(new DrawingSessionOptions { CalibrationSize = new Size(W, H) }))
        {
            DrawingLayer layer = session.AddLayer("Strokes", Color.White);
            foreach (BrushStrokeShape stroke in layout.Strokes) layer.AddShape(stroke);
            map = session.ExportOwnershipMap(layer, new Size(W, H)).FillUnowned();
        }

        // Bristle ridges run across each stroke, with a slight ramp along it.
        float[] phases = layout.Phases;
        DrawingPlane bristle = new DrawingPlane(W, H);
        map.FillByElement(bristle, p =>
        {
            double phase = phases[p.ElementIndex];
            return (float)(1 + (1.9 * ((0.07 * Math.Sin((p.Across * 0.9) + phase)) + (0.045 * Math.Sin((p.Across * 0.35) + (2 * phase)))))
                + (0.07 * 1.9 * (p.Along / p.Axes.Length)));
        }, 1f);

        // Dark outlines where one stroke meets another.
        DrawingPlane outline = map.CreateBoundaryPlane(1.3f).Multiply(1.6f).Clamp(0f, 1f);
        return new StrokeMap(map, bristle, outline);
    }
}
