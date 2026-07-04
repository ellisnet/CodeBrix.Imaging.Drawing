using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Shapes;
using SkiaSharp;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// A named collection of drawing elements - freehand <see cref="Stroke"/> values and
/// geometric <see cref="DrawingShape"/> values - that are rendered together in the layer's
/// color (unless a shape carries its own). Layers are composited independently, so
/// overlapping elements within one layer never darken each other when the layer is
/// rendered translucently (the "highlighter" effect).
/// </summary>
public sealed class DrawingLayer
{
    private readonly List<DrawingElement> _elements = new List<DrawingElement>();
    private readonly object _elementsLocker = new object();

    private SKColor _color;

    /// <summary>
    /// The display name of the layer (for example <c>"Pain"</c>). Layer names are treated
    /// as case-sensitive identifiers and never change after the layer is created.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The color that the layer's elements are drawn with (except shapes that carry their
    /// own color). Changing the color causes the layer to be fully re-rendered on the
    /// next draw.
    /// </summary>
    public SKColor Color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                BumpResetVersion();
            }
        }
    }

    /// <summary>
    /// The total number of completed elements (strokes and shapes) currently on the layer.
    /// </summary>
    public int ElementCount
    {
        get
        {
            lock (_elementsLocker) { return _elements.Count; }
        }
    }

    /// <summary>
    /// A version counter that increases every time the layer changes in a way that
    /// invalidates previously rendered content (an element was removed, the layer was
    /// cleared, or the color changed). Renderers compare this value to decide whether
    /// their cached layer bitmap must be discarded and fully redrawn.
    /// </summary>
    public int ResetVersion { get; private set; }

    /// <summary>
    /// Creates a new, empty drawing layer.
    /// </summary>
    /// <param name="name">The display name of the layer.</param>
    /// <param name="color">The color that the layer's elements are drawn with.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public DrawingLayer(string name, SKColor color)
    {
        if (String.IsNullOrWhiteSpace(name)) { throw new ArgumentException("A layer name is required.", nameof(name)); }
        Name = name.Trim();
        _color = color;
    }

    /// <summary>
    /// Adds a completed stroke to the end of the layer's element list.
    /// </summary>
    /// <param name="stroke">The stroke to add; empty strokes (no points) are ignored.</param>
    /// <returns><c>true</c> when the stroke was added; <c>false</c> when it was null or empty.</returns>
    public bool AddStroke(Stroke stroke)
    {
        if (stroke == null || stroke.PointCount < 1) { return false; }
        lock (_elementsLocker)
        {
            _elements.Add(stroke);
        }
        return true;
    }

    /// <summary>
    /// Adds a geometric shape to the end of the layer's element list.
    /// </summary>
    /// <param name="shape">The shape to add.</param>
    /// <returns><c>true</c> when the shape was added; <c>false</c> when it was null.</returns>
    public bool AddShape(DrawingShape shape)
    {
        if (shape == null) { return false; }
        lock (_elementsLocker)
        {
            _elements.Add(shape);
        }
        return true;
    }

    /// <summary>
    /// Removes the most recently added element (stroke or shape) from the layer - i.e. a
    /// simple "undo".
    /// </summary>
    /// <returns><c>true</c> when an element was removed; <c>false</c> when the layer was already empty.</returns>
    public bool RemoveLastElement()
    {
        lock (_elementsLocker)
        {
            if (_elements.Count < 1) { return false; }
            _elements.RemoveAt(_elements.Count - 1);
        }
        BumpResetVersion();
        return true;
    }

    /// <summary>
    /// Removes all elements from the layer.
    /// </summary>
    public void Clear()
    {
        bool changed;
        lock (_elementsLocker)
        {
            changed = _elements.Count > 0;
            _elements.Clear();
        }
        if (changed) { BumpResetVersion(); }
    }

    /// <summary>
    /// Returns a snapshot copy of the layer's elements (strokes and shapes), in the order
    /// they were added - which is the order they render in.
    /// </summary>
    /// <returns>A new array holding the layer's elements.</returns>
    public DrawingElement[] GetElements()
    {
        lock (_elementsLocker) { return _elements.ToArray(); }
    }

    /// <summary>
    /// Returns a snapshot copy of only the freehand strokes on the layer, in the order
    /// they were added.
    /// </summary>
    /// <returns>A new array holding the layer's strokes.</returns>
    public Stroke[] GetStrokes()
    {
        lock (_elementsLocker)
        {
            var strokes = new List<Stroke>(_elements.Count);
            foreach (DrawingElement element in _elements)
            {
                if (element is Stroke stroke) { strokes.Add(stroke); }
            }
            return strokes.ToArray();
        }
    }

    private void BumpResetVersion() => ResetVersion = unchecked(ResetVersion + 1);

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({ElementCount} elements)";
}
