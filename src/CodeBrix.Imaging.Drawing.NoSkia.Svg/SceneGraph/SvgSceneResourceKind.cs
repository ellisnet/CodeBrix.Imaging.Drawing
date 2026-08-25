using CodeBrix.SvgParse;
namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

// <summary>Identifies the kind of SVG scene resource.</summary>
internal enum SvgSceneResourceKind
{
    // <summary>An unknown resource type.</summary>
    Unknown,
    // <summary>A clip path resource.</summary>
    ClipPath,
    // <summary>A mask resource.</summary>
    Mask,
    // <summary>A filter resource.</summary>
    Filter,
    // <summary>A gradient resource.</summary>
    Gradient,
    // <summary>A pattern resource.</summary>
    Pattern,
    // <summary>A marker resource.</summary>
    Marker,
    // <summary>A symbol resource.</summary>
    Symbol,
    // <summary>A paint server resource.</summary>
    PaintServer
}
