using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.SvgParse;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model.Services;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

// <summary>Represents a compiled SVG scene graph with spatial indexing, resource management, and mutation support.</summary>
internal sealed class SvgSceneDocument
{
    private readonly Dictionary<string, List<SvgSceneNode>> _nodesByAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneNode> _nodesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneNode> _compilationRootsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _compilationRootsByDependentAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneResource> _resourcesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneResource> _resourcesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SvgSceneResource>> _resourcesByAddress = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolvingMaskResourceKeys = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, SvgSceneNode> _readOnlyNodesById;
    private readonly ReadOnlyDictionary<string, SvgSceneResource> _readOnlyResourcesById;

    internal SvgSceneDocument(
        SvgDocument sourceDocument,
        SKRect cullRect,
        SKRect compilationViewport,
        SvgSceneNode root,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        SourceDocument = sourceDocument;
        CullRect = cullRect;
        CompilationViewport = compilationViewport;
        Root = root ?? throw new ArgumentNullException(nameof(root));
        AssetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        IgnoreAttributes = ignoreAttributes;
        _readOnlyNodesById = new ReadOnlyDictionary<string, SvgSceneNode>(_nodesById);
        _readOnlyResourcesById = new ReadOnlyDictionary<string, SvgSceneResource>(_resourcesById);
        RebuildIndexesAndDependencies();
    }

    // <summary>Gets the source SVG document.</summary>
    public SvgDocument SourceDocument { get; }

    // <summary>Gets the cull rectangle of the scene.</summary>
    public SKRect CullRect { get; }

    // <summary>Gets the root node of the scene graph.</summary>
    public SvgSceneNode Root { get; }

    // <summary>Gets the scene nodes indexed by their element identifier.</summary>
    public IReadOnlyDictionary<string, SvgSceneNode> NodesById => _readOnlyNodesById;

    // <summary>Gets the scene resources indexed by their element identifier.</summary>
    public IReadOnlyDictionary<string, SvgSceneResource> ResourcesById => _readOnlyResourcesById;

    // <summary>Gets the revision number, incremented on each structural change.</summary>
    public long Revision { get; private set; }

    internal SKRect CompilationViewport { get; }

    internal ISvgAssetLoader AssetLoader { get; }

    internal DrawAttributes IgnoreAttributes { get; }

    // <summary>Traverses all nodes in the scene graph starting from the root.</summary>
    // <returns>An enumerable sequence of all scene nodes.</returns>
    public IEnumerable<SvgSceneNode> Traverse()
    {
        return Traverse(Root);
    }

    // <summary>Tries to get all scene nodes matching the specified address key.</summary>
    // <param name="addressKey">The element address key to search for.</param>
    // <param name="nodes">When this method returns, contains the matching nodes.</param>
    // <returns><c>true</c> if matching nodes were found; otherwise, <c>false</c>.</returns>
    public bool TryGetNodes(string addressKey, out IReadOnlyList<SvgSceneNode> nodes)
    {
        if (_nodesByAddress.TryGetValue(addressKey, out var sceneNodes))
        {
            nodes = sceneNodes;
            return true;
        }

        nodes = Array.Empty<SvgSceneNode>();
        return false;
    }

    // <summary>Tries to get the first scene node matching the specified address key.</summary>
    // <param name="addressKey">The element address key to search for.</param>
    // <param name="node">When this method returns, contains the matching node.</param>
    // <returns><c>true</c> if a matching node was found; otherwise, <c>false</c>.</returns>
    public bool TryGetNode(string addressKey, out SvgSceneNode node)
    {
        if (_nodesByAddress.TryGetValue(addressKey, out var sceneNodes) &&
            sceneNodes.Count > 0)
        {
            node = sceneNodes[0];
            return true;
        }

        node = null;
        return false;
    }

    // <summary>Tries to get the scene node for the specified SVG element.</summary>
    // <param name="element">The SVG element to look up.</param>
    // <param name="node">When this method returns, contains the matching node.</param>
    // <returns><c>true</c> if a matching node was found; otherwise, <c>false</c>.</returns>
    public bool TryGetNode(SvgElement element, out SvgSceneNode node)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return TryGetNode(SvgSceneCompiler.TryGetElementAddressKey(element) ?? string.Empty, out node);
    }

    // <summary>Tries to get a scene node by its SVG element identifier.</summary>
    // <param name="id">The SVG element identifier.</param>
    // <param name="node">When this method returns, contains the matching node.</param>
    // <returns><c>true</c> if a matching node was found; otherwise, <c>false</c>.</returns>
    public bool TryGetNodeById(string id, out SvgSceneNode node)
    {
        if (_nodesById.TryGetValue(id, out node))
        {
            return true;
        }

        node = null;
        return false;
    }

    // <summary>Tries to get a scene resource by its SVG element identifier.</summary>
    // <param name="id">The SVG element identifier.</param>
    // <param name="resource">When this method returns, contains the matching resource.</param>
    // <returns><c>true</c> if a matching resource was found; otherwise, <c>false</c>.</returns>
    public bool TryGetResourceById(string id, out SvgSceneResource resource)
    {
        if (_resourcesById.TryGetValue(id, out resource))
        {
            return true;
        }

        resource = null;
        return false;
    }

    // <summary>Tries to get a scene resource by its address key.</summary>
    // <param name="addressKey">The resource address key.</param>
    // <param name="resource">When this method returns, contains the matching resource.</param>
    // <returns><c>true</c> if a matching resource was found; otherwise, <c>false</c>.</returns>
    public bool TryGetResource(string addressKey, out SvgSceneResource resource)
    {
        if (_resourcesByKey.TryGetValue(addressKey, out resource))
        {
            return true;
        }

        resource = null;
        return false;
    }

    // <summary>Tries to get an SVG element by its address key.</summary>
    // <param name="addressKey">The element address key.</param>
    // <param name="element">When this method returns, contains the matching element.</param>
    // <returns><c>true</c> if a matching element was found; otherwise, <c>false</c>.</returns>
    public bool TryGetElement(string addressKey, out SvgElement element)
    {
        return TryResolveElement(addressKey, out element);
    }

    // <summary>Tries to get an SVG element by its identifier.</summary>
    // <param name="id">The SVG element identifier.</param>
    // <param name="element">When this method returns, contains the matching element.</param>
    // <returns><c>true</c> if a matching element was found; otherwise, <c>false</c>.</returns>
    public bool TryGetElementById(string id, out SvgElement element)
    {
        element = null;
        if (SourceDocument is null || string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        element = SourceDocument.GetElementById(id);
        return element is not null;
    }

    // <summary>Marks all nodes at the specified address key as dirty.</summary>
    // <param name="addressKey">The element address key.</param>
    // <param name="includeDescendants">Whether to also mark descendant nodes.</param>
    // <returns>The number of nodes marked dirty.</returns>
    public int MarkDirty(string addressKey, bool includeDescendants = false)
    {
        if (!_nodesByAddress.TryGetValue(addressKey, out var nodes))
        {
            return 0;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (includeDescendants)
            {
                nodes[i].MarkSubtreeDirty();
            }
            else
            {
                nodes[i].MarkDirty();
            }
        }

        Revision++;
        return nodes.Count;
    }

    // <summary>Applies a mutation to the scene graph for the specified SVG element.</summary>
    // <param name="element">The SVG element that was mutated.</param>
    // <param name="changedAttributes">The names of the changed attributes, or <c>null</c> to recompile fully.</param>
    // <returns>The result of the mutation operation.</returns>
    public SvgSceneMutationResult ApplyMutation(SvgElement element, IReadOnlyCollection<string> changedAttributes = null)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return SvgSceneCompiler.ApplyMutation(this, element, changedAttributes);
    }

    // <summary>Applies a mutation to the scene graph for the element at the specified address key.</summary>
    // <param name="addressKey">The element address key.</param>
    // <param name="changedAttributes">The names of the changed attributes, or <c>null</c> to recompile fully.</param>
    // <returns>The result of the mutation operation.</returns>
    public SvgSceneMutationResult ApplyMutation(string addressKey, IReadOnlyCollection<string> changedAttributes = null)
    {
        return TryResolveElement(addressKey, out var element) && element is not null
            ? ApplyMutation(element, changedAttributes)
            : new SvgSceneMutationResult(false, 0, 0);
    }

    // <summary>Applies a mutation to the scene graph for the element with the specified identifier.</summary>
    // <param name="id">The SVG element identifier.</param>
    // <param name="changedAttributes">The names of the changed attributes, or <c>null</c> to recompile fully.</param>
    // <returns>The result of the mutation operation.</returns>
    public SvgSceneMutationResult ApplyMutationById(string id, IReadOnlyCollection<string> changedAttributes = null)
    {
        return TryGetElementById(id, out var element) && element is not null
            ? ApplyMutation(element, changedAttributes)
            : new SvgSceneMutationResult(false, 0, 0);
    }

    // <summary>Clears the dirty flag on all nodes in the scene graph.</summary>
    public void ClearDirty()
    {
        Root.ClearDirty();
    }

    // <summary>Performs a hit test at the specified point.</summary>
    // <param name="point">The point in picture coordinates.</param>
    // <returns>An enumerable of scene nodes that intersect the point.</returns>
    public IEnumerable<SvgSceneNode> HitTest(SKPoint point)
    {
        return SvgSceneHitTestService.HitTest(this, point);
    }

    // <summary>Performs a hit test within the specified rectangle.</summary>
    // <param name="rect">The rectangle in picture coordinates.</param>
    // <returns>An enumerable of scene nodes that intersect the rectangle.</returns>
    public IEnumerable<SvgSceneNode> HitTest(SKRect rect)
    {
        return SvgSceneHitTestService.HitTest(this, rect);
    }

    // <summary>Performs a hit test and returns the topmost node at the specified point.</summary>
    // <param name="point">The point in picture coordinates.</param>
    // <returns>The topmost scene node, or <c>null</c> if none.</returns>
    public SvgSceneNode HitTestTopmostNode(SKPoint point)
    {
        return SvgSceneHitTestService.HitTestTopmostNode(this, point);
    }

    // <summary>Creates a drawing model from the current scene graph state.</summary>
    // <returns>The rendered picture model.</returns>
    public SKPicture CreateModel()
    {
        return SvgSceneRenderer.Render(this);
    }

    // <summary>Creates a drawing model for a specific scene node.</summary>
    // <param name="node">The scene node to render.</param>
    // <param name="clip">An optional clip rectangle.</param>
    // <returns>The rendered picture model for the node.</returns>
    public SKPicture CreateNodeModel(SvgSceneNode node, SKRect? clip = null)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return SvgSceneRenderer.RenderNodePicture(this, node, clip);
    }

    internal bool TryGetCompilationRoot(string compilationRootKey, out SvgSceneNode node)
    {
        if (_compilationRootsByKey.TryGetValue(compilationRootKey, out node))
        {
            return true;
        }

        node = null;
        return false;
    }

    internal bool TryEnterMaskResolution(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return false;
        }

        return _resolvingMaskResourceKeys.Add(resourceKey);
    }

    internal void ExitMaskResolution(string resourceKey)
    {
        if (!string.IsNullOrWhiteSpace(resourceKey))
        {
            _resolvingMaskResourceKeys.Remove(resourceKey);
        }
    }

    internal bool TryResolveElement(string addressKey, out SvgElement element)
    {
        element = null;
        if (SourceDocument is null || string.IsNullOrWhiteSpace(addressKey))
        {
            return false;
        }

        var indexes = addressKey.Split('/');
        var current = (SvgElement)SourceDocument;

        for (var i = 0; i < indexes.Length; i++)
        {
            if (!int.TryParse(indexes[i], out var childIndex) || childIndex < 0 || childIndex >= current.Children.Count)
            {
                return false;
            }

            current = current.Children[childIndex];
        }

        element = current;
        return true;
    }

    internal IReadOnlyList<SvgSceneResource> GetResourcesForAddress(string addressKey)
    {
        return _resourcesByAddress.TryGetValue(addressKey, out var resources)
            ? resources
            : Array.Empty<SvgSceneResource>();
    }

    internal IReadOnlyCollection<string> GetCompilationRootsForMutation(string addressKey)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            return results;
        }

        if (_compilationRootsByDependentAddress.TryGetValue(addressKey, out var directCompilationRoots))
        {
            foreach (var compilationRootKey in directCompilationRoots)
            {
                results.Add(compilationRootKey);
            }
        }

        if (_resourcesByAddress.TryGetValue(addressKey, out var resources))
        {
            var visitedResources = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < resources.Count; i++)
            {
                CollectResourceDependents(resources[i], results, visitedResources);
            }
        }

        return results;
    }

    internal void RebuildIndexesAndDependencies()
    {
        _nodesByAddress.Clear();
        _nodesById.Clear();
        _compilationRootsByKey.Clear();
        _compilationRootsByDependentAddress.Clear();
        _resourcesByKey.Clear();
        _resourcesById.Clear();
        _resourcesByAddress.Clear();

        ReindexNodes();
        RebuildResourceGraph();
        RegisterNodeDependencies();
        ResolveRuntimePayloads();
        Revision++;
    }

    private void ReindexNodes()
    {
        foreach (var node in Traverse())
        {
            if (!string.IsNullOrWhiteSpace(node.ElementAddressKey))
            {
                if (!_nodesByAddress.TryGetValue(node.ElementAddressKey!, out var list))
                {
                    list = new List<SvgSceneNode>();
                    _nodesByAddress.Add(node.ElementAddressKey!, list);
                }

                list.Add(node);
            }

            if (!string.IsNullOrWhiteSpace(node.ElementId) && !_nodesById.ContainsKey(node.ElementId!))
            {
                _nodesById.Add(node.ElementId!, node);
            }

            if (node.IsCompilationRootBoundary && !string.IsNullOrWhiteSpace(node.CompilationRootKey))
            {
                _compilationRootsByKey[node.CompilationRootKey!] = node;
            }
        }
    }

    private void RebuildResourceGraph()
    {
        if (SourceDocument is null)
        {
            return;
        }

        foreach (var element in TraverseElements(SourceDocument))
        {
            if (SvgSceneCompiler.TryGetResourceKind(element, out var resourceKind))
            {
                var addressKey = SvgSceneCompiler.TryGetElementAddressKey(element);
                if (string.IsNullOrWhiteSpace(addressKey))
                {
                    continue;
                }

                var resource = new SvgSceneResource(addressKey!, resourceKind, element, addressKey);
                _resourcesByKey[addressKey!] = resource;

                if (!string.IsNullOrWhiteSpace(resource.Id) && !_resourcesById.ContainsKey(resource.Id!))
                {
                    _resourcesById.Add(resource.Id!, resource);
                }

                foreach (var subtreeElement in TraverseElements(element))
                {
                    var subtreeAddressKey = SvgSceneCompiler.TryGetElementAddressKey(subtreeElement);
                    if (string.IsNullOrWhiteSpace(subtreeAddressKey))
                    {
                        continue;
                    }

                    resource.AddSubtreeAddress(subtreeAddressKey!);
                    if (!_resourcesByAddress.TryGetValue(subtreeAddressKey!, out var resources))
                    {
                        resources = new List<SvgSceneResource>();
                        _resourcesByAddress.Add(subtreeAddressKey!, resources);
                    }

                    resources.Add(resource);
                }
            }
        }

        foreach (var resource in _resourcesByKey.Values)
        {
            foreach (var subtreeElement in TraverseElements(resource.SourceElement))
            {
                foreach (var dependencyElement in SvgSceneCompiler.EnumerateReferencedElements(subtreeElement))
                {
                    var dependencyAddressKey = SvgSceneCompiler.TryGetElementAddressKey(dependencyElement);
                    if (string.IsNullOrWhiteSpace(dependencyAddressKey))
                    {
                        continue;
                    }

                    if (_resourcesByKey.TryGetValue(dependencyAddressKey!, out var dependencyResource))
                    {
                        resource.AddDependency(dependencyResource.Key);
                        dependencyResource.AddReverseDependency(resource.Key);
                    }
                }
            }
        }
    }

    private void RegisterNodeDependencies()
    {
        foreach (var node in Traverse())
        {
            if (string.IsNullOrWhiteSpace(node.CompilationRootKey))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(node.ElementAddressKey))
            {
                RegisterDependentAddress(node.ElementAddressKey!, node.CompilationRootKey!);
            }

            if (node.Element is null)
            {
                continue;
            }

            foreach (var subtreeElement in TraverseElements(node.Element))
            {
                var subtreeAddressKey = SvgSceneCompiler.TryGetElementAddressKey(subtreeElement);
                if (!string.IsNullOrWhiteSpace(subtreeAddressKey))
                {
                    RegisterDependentAddress(subtreeAddressKey!, node.CompilationRootKey!);
                }
            }

            foreach (var dependencyElement in SvgSceneCompiler.EnumerateReferencedElements(node.Element))
            {
                var dependencyAddressKey = SvgSceneCompiler.TryGetElementAddressKey(dependencyElement);
                if (string.IsNullOrWhiteSpace(dependencyAddressKey))
                {
                    continue;
                }

                if (_resourcesByKey.TryGetValue(dependencyAddressKey!, out var resource))
                {
                    resource.AddDependentCompilationRoot(node.CompilationRootKey!);
                }
                else
                {
                    RegisterDependentAddress(dependencyAddressKey!, node.CompilationRootKey!);
                }
            }
        }
    }

    private void ResolveRuntimePayloads()
    {
        ResolveRuntimePayloadTree(Root);
    }

    internal void ResolveRuntimePayloadTree(SvgSceneNode root)
    {
        if (root is null)
        {
            return;
        }

        foreach (var node in TraverseStructural(root))
        {
            ResolveRuntimePayload(node);
        }
    }

    private void ResolveRuntimePayload(SvgSceneNode node)
    {
        if (!node.IsRenderable || node.Element is not SvgElement element)
        {
            return;
        }

        SvgSceneCompiler.AssignRetainedVisualState(node, element);

        if (node.Kind == SvgSceneNodeKind.Marker)
        {
            node.IsVisible = true;
            node.IsDisplayNone = false;
        }

        if (element is SvgVisualElement visualElement)
        {
            node.ClipPath = ResolveClipPath(node);
            node.OpacityValue = IgnoreAttributes.HasFlag(DrawAttributes.Opacity)
                ? 1f
                : SvgScenePaintingService.AdjustSvgOpacity(visualElement.Opacity);
            node.Opacity = IgnoreAttributes.HasFlag(DrawAttributes.Opacity)
                ? null
                : SvgScenePaintingService.GetOpacityPaint(visualElement.Opacity);
            node.Fill = SvgScenePaintingService.IsValidFill(visualElement)
                ? SvgScenePaintingService.GetFillPaint(visualElement, node.GeometryBounds, AssetLoader, IgnoreAttributes)
                : null;
            node.Stroke = SvgScenePaintingService.IsValidStroke(visualElement, node.GeometryBounds)
                ? SvgScenePaintingService.GetStrokePaint(visualElement, node.GeometryBounds, AssetLoader, IgnoreAttributes)
                : null;
            node.StrokeWidth = node.Stroke?.StrokeWidth ?? 0f;
            node.SetMask(null);
            node.MaskPaint = null;
            node.MaskDstIn = null;
            node.Filter = null;
            node.FilterClip = null;
            node.SuppressSubtreeRendering = false;

            if (!IgnoreAttributes.HasFlag(DrawAttributes.Mask))
            {
                if (ResolveMaskPayload(node) is { } maskPayload)
                {
                    node.SetMask(maskPayload.MaskNode);
                    node.MaskPaint = maskPayload.MaskPaint.DeepClone();
                    node.MaskDstIn = maskPayload.MaskDstIn.DeepClone();
                }
            }

            if (!IgnoreAttributes.HasFlag(DrawAttributes.Filter))
            {
                if (visualElement.Filter is { } filter &&
                    !FilterEffectsService.IsNone(filter) &&
                    string.IsNullOrWhiteSpace(node.FilterResourceKey))
                {
                    node.Filter = null;
                    node.FilterClip = null;
                }
                else if (ResolveFilterPayload(node) is { } filterPayload)
                {
                    if (filterPayload.IsValid)
                    {
                        node.Filter = filterPayload.FilterPaint?.DeepClone();
                        node.FilterClip = filterPayload.FilterClip;
                    }
                    else
                    {
                        node.SuppressSubtreeRendering = true;
                    }
                }
            }
        }
    }

    private ClipPath ResolveClipPath(SvgSceneNode node)
    {
        if (IgnoreAttributes.HasFlag(DrawAttributes.ClipPath) || string.IsNullOrWhiteSpace(node.ClipResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.ClipResourceKey!, out var resource)
            ? resource.ResolveClipPayload(this, node)?.ClipPath
            : null;
    }

    private SvgSceneMaskPayload ResolveMaskPayload(SvgSceneNode node)
    {
        if (string.IsNullOrWhiteSpace(node.MaskResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.MaskResourceKey!, out var resource)
            ? resource.ResolveMaskPayload(this, node)
            : null;
    }

    private SvgSceneFilterPayload ResolveFilterPayload(SvgSceneNode node)
    {
        if (string.IsNullOrWhiteSpace(node.FilterResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.FilterResourceKey!, out var resource)
            ? resource.ResolveFilterPayload(this, node)
            : null;
    }

    private void RegisterDependentAddress(string addressKey, string compilationRootKey)
    {
        if (!_compilationRootsByDependentAddress.TryGetValue(addressKey, out var compilationRootKeys))
        {
            compilationRootKeys = new HashSet<string>(StringComparer.Ordinal);
            _compilationRootsByDependentAddress.Add(addressKey, compilationRootKeys);
        }

        compilationRootKeys.Add(compilationRootKey);
    }

    private void CollectResourceDependents(SvgSceneResource resource, HashSet<string> results, HashSet<string> visitedResources)
    {
        if (!visitedResources.Add(resource.Key))
        {
            return;
        }

        foreach (var compilationRootKey in resource.DependentCompilationRoots)
        {
            results.Add(compilationRootKey);
        }

        foreach (var reverseDependencyKey in resource.ReverseDependencyKeys)
        {
            if (_resourcesByKey.TryGetValue(reverseDependencyKey, out var reverseDependency))
            {
                CollectResourceDependents(reverseDependency, results, visitedResources);
            }
        }
    }

    private static IEnumerable<SvgSceneNode> Traverse(SvgSceneNode root)
    {
        var stack = new Stack<SvgSceneNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current.MaskNode is { } maskNode)
            {
                stack.Push(maskNode);
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private static IEnumerable<SvgSceneNode> TraverseStructural(SvgSceneNode root)
    {
        var stack = new Stack<SvgSceneNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private static IEnumerable<SvgElement> TraverseElements(SvgElement root)
    {
        var stack = new Stack<SvgElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }
}
