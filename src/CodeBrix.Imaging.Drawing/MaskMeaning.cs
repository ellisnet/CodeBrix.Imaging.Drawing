namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// What a mask exported by <c>DrawingSession.ExportMask</c> or
/// <c>DrawingRenderer.RenderToMask</c> measures: where ink is, or what a viewer would see.
/// </summary>
public enum MaskMeaning
{
    /// <summary>
    /// COVERAGE (the default): where each layer's ink is, as drawn into the layer's own
    /// bitmap - anti-aliased edges, an element colour's alpha, a fill's stop alpha, time
    /// windows, element and group opacity, masks and effects all count, because they are part
    /// of what the layer draws; the LAYER and SESSION opacities (<c>LayerOpacity</c>,
    /// <c>DrawingLayer.Opacity</c>, <c>Opacity</c>) do not, so a layer faded to 0 still shows
    /// fully. Layers combine as a union, bottom to top: <c>m = m + a * (1 - m)</c>. The mask a
    /// painterly tool wants: "where did I draw".
    /// </summary>
    Coverage = 0,

    /// <summary>
    /// VISIBLE ALPHA: the alpha of the frame exactly as it would be exported over transparency
    /// (no background) - every opacity counts (element, group, layer, <c>LayerOpacity</c>,
    /// session), as do time windows, masks, clips and effects. Each value is that exported alpha
    /// byte divided by 255. The mask a compositor wants: "how much of the scene shows here".
    /// </summary>
    VisibleAlpha = 1,

    /// <summary>
    /// HARD-EDGED COVERAGE: where each layer's elements are, answered 0 or 1 per pixel by ONE
    /// written-down rule - a pixel is covered when its centre lies inside an element's area
    /// (see <c>DrawingShape.GetCoverageAreas</c>) under that area's fill rule; a centre exactly
    /// on an edge is inside on a left or top edge and outside on a right or bottom edge. It is
    /// computed from the elements' geometry in shared managed code, never by a rasterizer, so
    /// both packages give IDENTICAL masks. Elements count when they would draw at the render
    /// time (inside their time window, opacity above 0); transforms, groups, the layer view and
    /// the camera apply; colours and their alpha, fills, effects and masks do not, nor do the
    /// layer and session opacities (a layer faded to 0 still shows, as with
    /// <see cref="Coverage"/>). Layers combine as a union. Custom shapes that do not report a
    /// coverage area cover nothing.
    /// </summary>
    HardEdged = 2,
}
