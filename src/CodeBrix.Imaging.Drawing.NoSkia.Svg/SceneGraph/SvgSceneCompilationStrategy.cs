using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

/// <summary>
/// Specifies the strategy used for compiling an SVG scene graph.
/// </summary>
public enum SvgSceneCompilationStrategy
{
    /// <summary>
    /// Compiles the scene using a direct retained-mode approach.
    /// </summary>
    DirectRetained
}
