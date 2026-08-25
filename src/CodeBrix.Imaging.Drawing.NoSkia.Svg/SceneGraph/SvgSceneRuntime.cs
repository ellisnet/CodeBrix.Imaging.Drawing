using System;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.SvgParse;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.Model.Services;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

// <summary>
// Provides high-level methods for compiling SVG fragments into scene documents and picture models.
// </summary>
internal static class SvgSceneRuntime
{
    // <summary>
    // Attempts to compile an SVG fragment into a scene document.
    // </summary>
    // <param name="sourceFragment">The SVG fragment to compile.</param>
    // <param name="assetLoader">The asset loader for resolving external resources.</param>
    // <param name="ignoreAttributes">The draw attributes to ignore during compilation.</param>
    // <param name="sceneDocument">When this method returns, contains the compiled scene document if successful.</param>
    // <returns><see langword="true"/> if compilation succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryCompile(
        SvgFragment sourceFragment,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        out SvgSceneDocument sceneDocument)
    {
        return TryCompile(sourceFragment, assetLoader, ignoreAttributes, SKRect.Empty, out sceneDocument);
    }

    // <summary>
    // Attempts to compile an SVG fragment into a scene document using the specified viewport.
    // </summary>
    // <param name="sourceFragment">The SVG fragment to compile.</param>
    // <param name="assetLoader">The asset loader for resolving external resources.</param>
    // <param name="ignoreAttributes">The draw attributes to ignore during compilation.</param>
    // <param name="standaloneDocumentViewport">The viewport rectangle for standalone document rendering.</param>
    // <param name="sceneDocument">When this method returns, contains the compiled scene document if successful.</param>
    // <returns><see langword="true"/> if compilation succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryCompile(
        SvgFragment sourceFragment,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SKRect standaloneDocumentViewport,
        out SvgSceneDocument sceneDocument)
    {
        sceneDocument = null;

        if (sourceFragment is null)
        {
            return false;
        }

        if (sourceFragment is SvgDocument sourceDocument)
        {
            var documentViewport = GetInitialViewport(sourceDocument, standaloneDocumentViewport);
            if (!SvgSceneCompiler.TryCompile(sourceDocument, documentViewport, assetLoader, ignoreAttributes, out sceneDocument) ||
                sceneDocument is null)
            {
                return false;
            }

            if (!NeedsViewportNormalization(sourceDocument, documentViewport))
            {
                return true;
            }

            var documentRenderableBounds = SvgSceneNodeBoundsService.GetPixelAlignedBounds(
                SvgSceneNodeBoundsService.GetRenderablePaintBounds(sceneDocument.Root));
            if (documentRenderableBounds.IsEmpty || documentRenderableBounds.Equals(documentViewport))
            {
                return true;
            }

            return SvgSceneCompiler.TryCompile(sourceDocument, documentRenderableBounds, assetLoader, ignoreAttributes, out sceneDocument);
        }

        var viewport = GetInitialViewport(sourceFragment, standaloneDocumentViewport);
        if (!SvgSceneCompiler.TryCompileFragment(sourceFragment, viewport, viewport, assetLoader, ignoreAttributes, out sceneDocument) ||
            sceneDocument is null)
        {
            return false;
        }

        if (!NeedsViewportNormalization(sourceFragment, viewport))
        {
            return true;
        }

        var renderableBounds = SvgSceneNodeBoundsService.GetPixelAlignedBounds(
            SvgSceneNodeBoundsService.GetRenderablePaintBounds(sceneDocument.Root));
        if (renderableBounds.IsEmpty || renderableBounds.Equals(viewport))
        {
            return true;
        }

        return SvgSceneCompiler.TryCompileFragment(sourceFragment, renderableBounds, renderableBounds, assetLoader, ignoreAttributes, out sceneDocument);
    }

    // <summary>
    // Creates an <see cref="SKPicture"/> model from the specified SVG fragment.
    // </summary>
    // <param name="sourceFragment">The SVG fragment to render.</param>
    // <param name="assetLoader">The asset loader for resolving external resources.</param>
    // <param name="ignoreAttributes">The draw attributes to ignore during rendering.</param>
    // <returns>An <see cref="SKPicture"/> representing the rendered SVG, or <see langword="null"/> if compilation fails.</returns>
    public static SKPicture CreateModel(
        SvgFragment sourceFragment,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        return CreateModel(sourceFragment, assetLoader, ignoreAttributes, SKRect.Empty);
    }

    // <summary>
    // Creates an <see cref="SKPicture"/> model from the specified SVG fragment using the given viewport.
    // </summary>
    // <param name="sourceFragment">The SVG fragment to render.</param>
    // <param name="assetLoader">The asset loader for resolving external resources.</param>
    // <param name="ignoreAttributes">The draw attributes to ignore during rendering.</param>
    // <param name="standaloneDocumentViewport">The viewport rectangle for standalone document rendering.</param>
    // <returns>An <see cref="SKPicture"/> representing the rendered SVG, or <see langword="null"/> if compilation fails.</returns>
    public static SKPicture CreateModel(
        SvgFragment sourceFragment,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SKRect standaloneDocumentViewport)
    {
        return TryCompile(sourceFragment, assetLoader, ignoreAttributes, standaloneDocumentViewport, out var sceneDocument) && sceneDocument is not null
            ? sceneDocument.CreateModel()
            : null;
    }

    private static SKRect GetInitialViewport(SvgFragment fragment, SKRect standaloneDocumentViewport)
    {
        var standaloneViewport = GetStandaloneViewport(fragment, standaloneDocumentViewport);
        var size = SvgService.GetDimensions(fragment, standaloneViewport);
        var bounds = SKRect.Create(size);
        if (!bounds.IsEmpty)
        {
            return bounds;
        }

        if (fragment.ViewBox.Width > 0f && fragment.ViewBox.Height > 0f)
        {
            return SKRect.Create(
                fragment.ViewBox.MinX,
                fragment.ViewBox.MinY,
                fragment.ViewBox.Width,
                fragment.ViewBox.Height);
        }

        if (fragment is not SvgDocument && fragment.OwnerDocument is { } ownerDocument)
        {
            var ownerSize = SvgService.GetDimensions(ownerDocument);
            var ownerBounds = SKRect.Create(ownerSize);
            if (!ownerBounds.IsEmpty)
            {
                return ownerBounds;
            }

            if (ownerDocument.ViewBox.Width > 0f && ownerDocument.ViewBox.Height > 0f)
            {
                return SKRect.Create(
                    ownerDocument.ViewBox.MinX,
                    ownerDocument.ViewBox.MinY,
                    ownerDocument.ViewBox.Width,
                    ownerDocument.ViewBox.Height);
            }
        }

        return SKRect.Create(0f, 0f, 1f, 1f);
    }

    private static SKRect GetStandaloneViewport(SvgFragment fragment, SKRect standaloneDocumentViewport)
    {
        if (fragment is not SvgDocument document ||
            (document.Width.Type != SvgUnitType.Percentage && document.Height.Type != SvgUnitType.Percentage) ||
            standaloneDocumentViewport.IsEmpty)
        {
            return SKRect.Empty;
        }

        return standaloneDocumentViewport;
    }

    private static bool NeedsViewportNormalization(SvgFragment fragment, SKRect viewport)
    {
        if (viewport.Width > 1f || viewport.Height > 1f)
        {
            return false;
        }

        if (!fragment.ViewBox.Equals(SvgViewBox.Empty))
        {
            return false;
        }

        if (fragment is SvgDocument)
        {
            return fragment.Width.Type == SvgUnitType.Percentage ||
                   fragment.Height.Type == SvgUnitType.Percentage;
        }

        return true;
    }
}
