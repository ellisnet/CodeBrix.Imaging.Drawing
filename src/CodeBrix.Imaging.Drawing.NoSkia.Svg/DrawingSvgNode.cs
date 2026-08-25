using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.SvgParse;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// One element of a loaded document as the compiler placed it: what it is, where it landed,
/// and what it links to. Nodes are read-only views onto the compiled scene - there is no
/// mutation API - and one node is handed out per compiled element, so two lookups that
/// reach the same element return the same instance.
/// </summary>
public sealed class DrawingSvgNode
{
    private readonly object _syncRoot = new object();
    private readonly DrawingSvgScene _scene;
    private readonly SvgSceneNode _node;
    private IReadOnlyList<DrawingSvgNode> _children;

    internal DrawingSvgNode(DrawingSvgScene scene, SvgSceneNode node)
    {
        _scene = scene;
        _node = node;
    }

    /// <summary>What the element became in the compiled scene.</summary>
    public DrawingSvgNodeKind Kind => (DrawingSvgNodeKind)(int)_node.Kind;

    /// <summary>The element's <c>id</c> attribute; <c>null</c> when it has none.</summary>
    public string Id => _node.ElementId;

    /// <summary>The element's tag name (<c>rect</c>, <c>g</c>, <c>text</c>, ...).</summary>
    public string ElementName => DrawingSvgScene.GetElementName(_node.Element);

    /// <summary>
    /// The parsed SVG element itself, for anything this node does not surface directly -
    /// arbitrary attributes, presentation properties, the element's own children.
    /// </summary>
    public SvgElement Element => _node.Element;

    /// <summary>
    /// The link target of an <see cref="DrawingSvgNodeKind.Anchor"/> node (the <c>a</c>
    /// element's <c>xlink:href</c>); <c>null</c> for every other kind.
    /// </summary>
    public string Href => (_node.Element as SvgAnchor)?.Href;

    /// <summary>
    /// The browsing context an <see cref="DrawingSvgNodeKind.Anchor"/> node asks for (the
    /// <c>a</c> element's <c>target</c>); <c>null</c> for every other kind.
    /// </summary>
    public string Target => (_node.Element as SvgAnchor)?.Target;

    /// <summary>
    /// The tooltip of an <see cref="DrawingSvgNodeKind.Anchor"/> node (the <c>a</c>
    /// element's <c>xlink:title</c>); <c>null</c> for every other kind.
    /// </summary>
    public string Title => (_node.Element as SvgAnchor)?.Title;

    /// <summary>The node this one hangs from; <c>null</c> for the scene root.</summary>
    public DrawingSvgNode Parent => _scene.Wrap(_node.Parent);

    /// <summary>The node's children, in document order.</summary>
    public IReadOnlyList<DrawingSvgNode> Children
    {
        get
        {
            lock (_syncRoot)
            {
                if (_children == null)
                {
                    var children = new List<DrawingSvgNode>(_node.Children.Count);
                    foreach (SvgSceneNode child in _node.Children)
                    {
                        children.Add(_scene.Wrap(child));
                    }
                    _children = children;
                }
                return _children;
            }
        }
    }

    /// <summary>
    /// The node's own geometry, in its own local coordinate space - before
    /// <see cref="Transform"/> is applied.
    /// </summary>
    public DrawingRect GeometryBounds => DrawingSvgScene.ToRect(_node.GeometryBounds);

    /// <summary>
    /// The area the node occupies in the document's coordinate space - the same space
    /// <c>DrawingSvg.Picture</c>'s cull rectangle is expressed in, so a hit box drawn from
    /// this rectangle lines up with the rendered picture. It is an axis-aligned bounding
    /// box: a rotated or skewed node's box covers more than the node's ink does.
    /// </summary>
    public DrawingRect DocumentBounds => DrawingSvgScene.ToRect(_node.TransformedBounds);

    /// <summary>The transform the element itself contributes, relative to its parent.</summary>
    public Matrix3x2 Transform => DrawingSvgScene.ToMatrix(_node.Transform);

    /// <summary>The accumulated transform from the document root down to this node.</summary>
    public Matrix3x2 TotalTransform => DrawingSvgScene.ToMatrix(_node.TotalTransform);

    /// <summary>
    /// Whether the element is visible (<c>visibility</c> is not <c>hidden</c> and
    /// <c>display</c> is not <c>none</c>).
    /// </summary>
    public bool IsVisible => _node.IsVisible && !_node.IsDisplayNone;

    /// <summary>
    /// Whether the element paints anything of its own. Containers that only position their
    /// children, and elements the compiler resolved away, are not renderable.
    /// </summary>
    public bool IsRenderable => _node.IsRenderable;

    internal SvgSceneNode SceneNode => _node;
}
