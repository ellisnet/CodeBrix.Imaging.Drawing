using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.SvgParse;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model.Services;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

// <summary>Represents a node in the SVG scene graph.</summary>
internal sealed class SvgSceneNode
{
    private readonly List<SvgSceneNode> _children = new();

    internal SvgSceneNode(
        SvgSceneNodeKind kind,
        SvgElement element,
        string elementAddressKey,
        string elementTypeName,
        string compilationRootKey,
        bool isCompilationRootBoundary)
    {
        Kind = kind;
        Element = element;
        ElementAddressKey = elementAddressKey;
        ElementTypeName = elementTypeName;
        ElementId = element?.ID;
        CompilationRootKey = compilationRootKey;
        IsCompilationRootBoundary = isCompilationRootBoundary;
    }

    // <summary>Gets the kind of scene node.</summary>
    public SvgSceneNodeKind Kind { get; private set; }

    // <summary>Gets the SVG element associated with this node.</summary>
    public SvgElement Element { get; private set; }

    // <summary>Gets the address key used to identify this element in the scene graph.</summary>
    public string ElementAddressKey { get; private set; }

    // <summary>Gets the identifier of the SVG element.</summary>
    public string ElementId { get; private set; }

    // <summary>Gets the type name of the SVG element.</summary>
    public string ElementTypeName { get; private set; }

    // <summary>Gets the element used for hit testing, which may differ from the visual element.</summary>
    public SvgElement HitTestTargetElement { get; internal set; }

    // <summary>Gets the pointer events mode for this node.</summary>
    public SvgPointerEvents PointerEvents { get; internal set; } = SvgPointerEvents.VisiblePainted;

    // <summary>Gets a value indicating whether this node is visible.</summary>
    public bool IsVisible { get; internal set; } = true;

    // <summary>Gets a value indicating whether this node has display set to none.</summary>
    public bool IsDisplayNone { get; internal set; }

    // <summary>Gets the cursor to display when hovering over this node.</summary>
    public string Cursor { get; internal set; }

    // <summary>Gets a value indicating whether this node creates a background compositing layer.</summary>
    public bool CreatesBackgroundLayer { get; internal set; }

    // <summary>Gets the clip rectangle for the background layer.</summary>
    public SKRect? BackgroundClip { get; internal set; }

    // <summary>Gets the resource key for the clip definition.</summary>
    public string ClipResourceKey { get; internal set; }

    // <summary>Gets the resource key for the mask definition.</summary>
    public string MaskResourceKey { get; internal set; }

    // <summary>Gets the resource key for the filter definition.</summary>
    public string FilterResourceKey { get; internal set; }

    // <summary>Gets the compilation root key for this node.</summary>
    public string CompilationRootKey { get; private set; }

    // <summary>Gets a value indicating whether this node is a compilation root boundary.</summary>
    public bool IsCompilationRootBoundary { get; private set; }

    // <summary>Gets the compilation strategy for this node.</summary>
    public SvgSceneCompilationStrategy CompilationStrategy { get; internal set; } = SvgSceneCompilationStrategy.DirectRetained;

    // <summary>Gets the parent node in the scene graph.</summary>
    public SvgSceneNode Parent { get; private set; }

    // <summary>Gets the child nodes of this scene node.</summary>
    public IReadOnlyList<SvgSceneNode> Children => _children;

    // <summary>Gets the mask node applied to this scene node.</summary>
    public SvgSceneNode MaskNode { get; private set; }

    // <summary>Gets the local drawing model for this node.</summary>
    public SKPicture LocalModel { get; internal set; }

    // <summary>Gets the path used for hit testing.</summary>
    public SKPath HitTestPath { get; internal set; }

    // <summary>Gets the bounds of the geometry in local coordinates.</summary>
    public SKRect GeometryBounds { get; internal set; }

    // <summary>Gets the bounds after applying the transform.</summary>
    public SKRect TransformedBounds { get; internal set; }

    // <summary>Gets the local transform matrix for this node.</summary>
    public SKMatrix Transform { get; internal set; }

    // <summary>Gets the accumulated transform from the root to this node.</summary>
    public SKMatrix TotalTransform { get; internal set; }

    // <summary>Gets the overflow clip rectangle, if any.</summary>
    public SKRect? Overflow { get; internal set; }

    // <summary>Gets the clip rectangle, if any.</summary>
    public SKRect? Clip { get; internal set; }

    // <summary>Gets the inner clip rectangle, if any.</summary>
    public SKRect? InnerClip { get; internal set; }

    // <summary>Gets the clip path applied to this node.</summary>
    public ClipPath ClipPath { get; internal set; }

    // <summary>Gets the paint used to render the mask.</summary>
    public SKPaint MaskPaint { get; internal set; }

    // <summary>Gets the destination-in paint used for mask compositing.</summary>
    public SKPaint MaskDstIn { get; internal set; }

    // <summary>Gets the paint used for opacity compositing.</summary>
    public SKPaint Opacity { get; internal set; }

    // <summary>Gets the opacity value for this node.</summary>
    public float OpacityValue { get; internal set; } = 1f;

    // <summary>Gets the paint used for filter effects.</summary>
    public SKPaint Filter { get; internal set; }

    // <summary>Gets the clip rectangle for filter effects, if any.</summary>
    public SKRect? FilterClip { get; internal set; }

    // <summary>Gets the paint used for fill rendering.</summary>
    public SKPaint Fill { get; internal set; }

    // <summary>Gets the paint used for stroke rendering.</summary>
    public SKPaint Stroke { get; internal set; }

    // <summary>Gets a value indicating whether this node supports fill-based hit testing.</summary>
    public bool SupportsFillHitTest { get; internal set; }

    // <summary>Gets a value indicating whether this node supports stroke-based hit testing.</summary>
    public bool SupportsStrokeHitTest { get; internal set; }

    // <summary>Gets the stroke width.</summary>
    public float StrokeWidth { get; internal set; }

    // <summary>Gets a value indicating whether this node is renderable.</summary>
    public bool IsRenderable { get; internal set; }

    // <summary>Gets a value indicating whether antialiasing is enabled.</summary>
    public bool IsAntialias { get; internal set; }

    // <summary>Gets a value indicating whether subtree rendering is suppressed.</summary>
    public bool SuppressSubtreeRendering { get; internal set; }

    // <summary>Gets a value indicating whether this node has been modified since the last clear.</summary>
    public bool IsDirty { get; private set; }

    // <summary>Gets the version number, incremented each time the node is marked dirty.</summary>
    public long Version { get; private set; }

    // <summary>Gets a value indicating whether this node has local drawing commands.</summary>
    public bool HasLocalVisuals => LocalModel?.Commands is { Count: > 0 };

    internal void AddChild(SvgSceneNode child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    internal void SetMask(SvgSceneNode maskNode)
    {
        MaskNode = maskNode;
        if (maskNode is not null)
        {
            maskNode.Parent = this;
        }
    }

    internal void ReplaceWith(SvgSceneNode replacement)
    {
        Kind = replacement.Kind;
        Element = replacement.Element;
        ElementAddressKey = replacement.ElementAddressKey;
        ElementId = replacement.ElementId;
        ElementTypeName = replacement.ElementTypeName;
        HitTestTargetElement = replacement.HitTestTargetElement;
        PointerEvents = replacement.PointerEvents;
        IsVisible = replacement.IsVisible;
        IsDisplayNone = replacement.IsDisplayNone;
        Cursor = replacement.Cursor;
        CreatesBackgroundLayer = replacement.CreatesBackgroundLayer;
        BackgroundClip = replacement.BackgroundClip;
        ClipResourceKey = replacement.ClipResourceKey;
        MaskResourceKey = replacement.MaskResourceKey;
        FilterResourceKey = replacement.FilterResourceKey;
        CompilationRootKey = replacement.CompilationRootKey;
        IsCompilationRootBoundary = replacement.IsCompilationRootBoundary;
        LocalModel = replacement.LocalModel;
        HitTestPath = replacement.HitTestPath?.DeepClone();
        GeometryBounds = replacement.GeometryBounds;
        TransformedBounds = replacement.TransformedBounds;
        Transform = replacement.Transform;
        TotalTransform = replacement.TotalTransform;
        Overflow = replacement.Overflow;
        Clip = replacement.Clip;
        InnerClip = replacement.InnerClip;
        ClipPath = replacement.ClipPath;
        MaskPaint = replacement.MaskPaint;
        MaskDstIn = replacement.MaskDstIn;
        Opacity = replacement.Opacity;
        OpacityValue = replacement.OpacityValue;
        Filter = replacement.Filter;
        FilterClip = replacement.FilterClip;
        Fill = replacement.Fill;
        Stroke = replacement.Stroke;
        SupportsFillHitTest = replacement.SupportsFillHitTest;
        SupportsStrokeHitTest = replacement.SupportsStrokeHitTest;
        StrokeWidth = replacement.StrokeWidth;
        IsRenderable = replacement.IsRenderable;
        IsAntialias = replacement.IsAntialias;
        SuppressSubtreeRendering = replacement.SuppressSubtreeRendering;

        _children.Clear();
        for (var i = 0; i < replacement.Children.Count; i++)
        {
            AddChild(replacement.Children[i]);
        }

        MaskNode = null;
        SetMask(replacement.MaskNode);
        MarkDirty();
    }

    internal void RefreshElementIdentity(string elementAddressKey)
    {
        ElementAddressKey = elementAddressKey;
        ElementId = Element?.ID;
        ElementTypeName = Element?.GetType().Name ?? ElementTypeName;
    }

    // <summary>Marks this node as dirty and increments its version.</summary>
    public void MarkDirty()
    {
        IsDirty = true;
        Version++;
    }

    // <summary>Marks this node and all its descendants as dirty.</summary>
    public void MarkSubtreeDirty()
    {
        MarkDirty();

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].MarkSubtreeDirty();
        }

        MaskNode?.MarkSubtreeDirty();
    }

    // <summary>Clears the dirty flag on this node and all its descendants.</summary>
    public void ClearDirty()
    {
        IsDirty = false;

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].ClearDirty();
        }

        MaskNode?.ClearDirty();
    }
}
