using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.SvgParse;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// The structure behind a loaded document: which element ended up where, what each one
/// links to, and what sits under a point. A scene is the companion of
/// <see cref="DrawingSvg.Picture"/> - the picture says what to draw, the scene says what
/// the drawing MEANS - and the two share one coordinate space, so a node's
/// <see cref="DrawingSvgNode.DocumentBounds"/> can be turned straight into a hit box, a PDF
/// link annotation, or a tooltip region over the rendered output.
/// <para>
/// The scene is read-only. It stays usable after the <see cref="DrawingSvg"/> that produced
/// it has been disposed.
/// </para>
/// </summary>
public sealed class DrawingSvgScene
{
    private static readonly Dictionary<Type, string> ElementNames = new Dictionary<Type, string>();

    private readonly object _syncRoot = new object();
    private readonly SvgSceneDocument _document;
    private readonly Dictionary<SvgSceneNode, DrawingSvgNode> _nodes =
        new Dictionary<SvgSceneNode, DrawingSvgNode>(System.Collections.Generic.ReferenceEqualityComparer.Instance);

    internal DrawingSvgScene(SvgSceneDocument document)
    {
        _document = document;
    }

    /// <summary>The document root - the node the whole scene hangs from.</summary>
    public DrawingSvgNode Root => Wrap(_document.Root);

    /// <summary>
    /// Walks the whole scene, parents before children, in document order.
    /// </summary>
    /// <returns>Every node in the scene.</returns>
    public IEnumerable<DrawingSvgNode> Traverse()
    {
        foreach (SvgSceneNode node in _document.Traverse())
        {
            yield return Wrap(node);
        }
    }

    /// <summary>
    /// Finds the node compiled from the element carrying the given <c>id</c>.
    /// </summary>
    /// <param name="id">The element id to look for.</param>
    /// <param name="node">When this method returns <c>true</c>, receives the matching node.</param>
    /// <returns><c>true</c> when an element with that id was compiled into the scene.</returns>
    public bool TryGetNodeById(string id, out DrawingSvgNode node)
    {
        if (_document.TryGetNodeById(id, out SvgSceneNode sceneNode))
        {
            node = Wrap(sceneNode);
            return true;
        }

        node = null;
        return false;
    }

    /// <summary>
    /// Finds every node whose geometry contains a point, in the document's coordinate
    /// space, topmost first.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns>The nodes under the point.</returns>
    public IEnumerable<DrawingSvgNode> HitTest(DrawingPoint point)
    {
        foreach (SvgSceneNode node in _document.HitTest(new SKPoint(point.X, point.Y)))
        {
            yield return Wrap(node);
        }
    }

    /// <summary>
    /// Finds every node whose geometry intersects a rectangle, in the document's coordinate
    /// space, topmost first.
    /// </summary>
    /// <param name="rect">The rectangle to test.</param>
    /// <returns>The nodes the rectangle touches.</returns>
    public IEnumerable<DrawingSvgNode> HitTest(DrawingRect rect)
    {
        foreach (SvgSceneNode node in _document.HitTest(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom)))
        {
            yield return Wrap(node);
        }
    }

    /// <summary>
    /// Finds the topmost node under a point - what a click at that position would land on.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns>The topmost node under the point; or <c>null</c> when nothing is there.</returns>
    public DrawingSvgNode HitTestTopmost(DrawingPoint point)
        => Wrap(_document.HitTestTopmostNode(new SKPoint(point.X, point.Y)));

    internal DrawingSvgNode Wrap(SvgSceneNode node)
    {
        if (node == null) { return null; }

        //Reading the scene fills this cache, so even the read-only walks have to be safe to
        //  run from more than one thread (hit-testing on one, rendering on another)
        lock (_syncRoot)
        {
            if (!_nodes.TryGetValue(node, out DrawingSvgNode wrapper))
            {
                wrapper = new DrawingSvgNode(this, node);
                _nodes[node] = wrapper;
            }
            return wrapper;
        }
    }

    internal static string GetElementName(SvgElement element)
    {
        if (element == null) { return null; }

        Type type = element.GetType();
        lock (ElementNames)
        {
            if (!ElementNames.TryGetValue(type, out string name))
            {
                //The parser keeps SvgElement.ElementName to itself, but the very attribute
                //  it reads the tag name from is public
                object[] attributes = type.GetCustomAttributes(typeof(SvgElementAttribute), inherit: true);
                name = attributes.Length > 0 ? ((SvgElementAttribute)attributes[0]).ElementName : null;
                ElementNames[type] = name;
            }
            return name;
        }
    }

    internal static DrawingRect ToRect(SKRect rect)
        => new DrawingRect(rect.Left, rect.Top, rect.Right, rect.Bottom);

    internal static Matrix3x2 ToMatrix(SKMatrix matrix)
        => new Matrix3x2(
            matrix.ScaleX, matrix.SkewY,
            matrix.SkewX, matrix.ScaleY,
            matrix.TransX, matrix.TransY);
}
