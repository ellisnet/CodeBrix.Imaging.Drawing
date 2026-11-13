using System;
using CodeBrix.Imaging.Drawing.Shapes;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>The session's SVG helper (NoSkia package only): <c>session.DrawSvg(svg, destination)</c>.</summary>
public static class DrawingSvgSessionExtensions
{
    /// <summary>
    /// Adds a loaded SVG document to the session's active layer as a <see cref="DrawingSvgShape"/>,
    /// fitted into a destination rectangle.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="svg">A loaded document; keep it alive while the session may render it.</param>
    /// <param name="destination">Where the document is fitted, calibrated units.</param>
    /// <param name="aspect">How it is fitted; <see cref="DrawingSvgAspect.Meet"/> by default.</param>
    /// <returns>The shape that was added (animate its parts through it).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> or <paramref name="svg"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no document has been loaded.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no active layer.</exception>
    public static DrawingSvgShape DrawSvg(this DrawingSession session, DrawingSvg svg, RectangleF destination,
        DrawingSvgAspect aspect = DrawingSvgAspect.Meet)
    {
        if (session == null) { throw new ArgumentNullException(nameof(session)); }
        var shape = new DrawingSvgShape(svg, destination, aspect);
        session.DrawShape(shape);
        return shape;
    }
}
