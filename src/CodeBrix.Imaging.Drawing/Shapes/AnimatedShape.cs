using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Rendering;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// A shape rebuilt for each moment by a factory - the way to animate a shape's OWN properties
/// (a line's end points, a circle's radius, a text's words or color, a polygon's points)
/// while it stays one element on its layer. The factory receives the time and returns the
/// shape to draw then (any shape, including a <see cref="DrawingGroup"/>; or <c>null</c> to
/// draw nothing). The session calls <see cref="Update(double)"/> whenever its
/// <see cref="CodeBrix.Imaging.Drawing.DrawingSession.Time"/> is set; the shape it returned
/// is drawn with its own color, transform and opacity, inside this element's own transform
/// and opacity.
/// <para>
/// Write the factory as a pure function of the time (for example from keyframe tracks or a
/// timeline cue) - then every frame, rendered in any order or on any thread, is the same.
/// Returning the SAME shape instance as last time tells the renderer nothing changed. The
/// factory is first called at construction, for <c>initialTime</c>.
/// </para>
/// </summary>
public sealed class AnimatedShape : DrawingShape
{
    private readonly Func<double, DrawingShape> _factory;
    private DrawingShape _current;

    /// <summary>
    /// Creates the shape and builds its first state.
    /// </summary>
    /// <param name="factory">Returns the shape to draw at a time (or <c>null</c> for nothing).</param>
    /// <param name="initialTime">The time of the first state, seconds; 0 by default.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the factory returns this shape itself.</exception>
    public AnimatedShape(Func<double, DrawingShape> factory, double initialTime = 0)
        : base(1f, (Color?)null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        MarkChanged();
        Update(initialTime);
    }

    /// <summary>The factory that builds the shape for a time.</summary>
    public Func<double, DrawingShape> Factory => _factory;

    /// <summary>The shape built for the last updated time; <c>null</c> when the factory returned none.</summary>
    public DrawingShape Current => _current;

    /// <summary>The time the shape was last updated for, seconds.</summary>
    public double Time { get; private set; }

    /// <summary>
    /// Rebuilds the shape for a time (the session does this whenever its time is set).
    /// </summary>
    /// <param name="seconds">The time, seconds.</param>
    /// <exception cref="InvalidOperationException">Thrown when the factory returns this shape itself.</exception>
    public void Update(double seconds)
    {
        DrawingShape next = _factory(seconds);
        if (ReferenceEquals(next, this)) { throw new InvalidOperationException("An animated shape's factory cannot return the animated shape itself."); }
        Time = seconds;
        if (!ReferenceEquals(next, _current))
        {
            _current = next;
            MarkChanged();
        }
    }

    /// <summary>The current shape's outline after its own transform (<see cref="PathGeometry.Empty"/> when there is no current shape; <c>null</c> when unknown).</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline() => _current == null ? PathGeometry.Empty : _current.GetTransformedOutline();

    /// <summary>The area the current shape covers, through its own transform; none when there is no current shape.</summary>
    /// <param name="tolerance">How closely outline bands are followed, calibrated units.</param>
    /// <returns>The current shape's areas, transformed; none when unknown or absent.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
        => _current == null ? Array.Empty<PathGeometry>() : DrawingGroup.TransformedAreas(_current, tolerance) ?? Array.Empty<PathGeometry>();

    /// <summary>
    /// The current shape's bounds after its own transform (<see cref="RectangleF.Empty"/> when
    /// there is no current shape; <c>null</c> when its bounds are unknown).
    /// </summary>
    /// <returns>The bounds.</returns>
    public override RectangleF? GetBounds() => _current == null ? RectangleF.Empty : _current.GetTransformedBounds();

    /// <summary>Draws the current shape (with its own color, transform and opacity).</summary>
    /// <param name="canvas">The canvas, already transformed to calibrated space.</param>
    /// <param name="color">The color used when the current shape has none of its own.</param>
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        DrawingShape current = _current;
        if (current != null) { ElementPainter.Draw(canvas, current, color, null); }
    }

    internal override long GetRenderStamp(double time)
    {
        DrawingShape current = _current;
        ulong inner = current == null ? 0UL : (ulong)current.GetRenderStamp(time);
        return (long)MixStamp((ulong)Stamp, inner);
    }
}
