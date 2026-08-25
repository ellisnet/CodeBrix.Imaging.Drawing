using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

// <summary>
// Builds a concrete <see cref="DrawingImageFilter"/> evaluator from a shim
// <see cref="SKImageFilter"/> graph (the compiled form of an SVG filter chain). The
// display-list replayer (<see cref="NoSkiaModel"/>) attaches the built filter to
// save-layer paints so the managed canvas applies it when the layer is restored. When no
// factory is provided - or the factory returns <c>null</c> for a graph it cannot
// evaluate - the save-layer proceeds without the image filter (graceful degradation).
// </summary>
internal interface INoSkiaImageFilterFactory
{
    // <summary>
    // Builds an evaluator for the given shim image-filter graph.
    // </summary>
    // <param name="filter">The shim image-filter graph.</param>
    // <returns>The evaluator; or <c>null</c> when the graph cannot be evaluated.</returns>
    DrawingImageFilter Create(SKImageFilter filter);
}
