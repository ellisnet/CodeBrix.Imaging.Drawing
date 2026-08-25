namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// What an SVG element became in the compiled scene - the Drawing-named view of the scene
/// graph's node kinds.
/// </summary>
public enum DrawingSvgNodeKind
{
    /// <summary>An element the compiler does not classify further.</summary>
    Unknown,

    /// <summary>An <c>svg</c> fragment - the document root, or a nested viewport.</summary>
    Fragment,

    /// <summary>A <c>g</c> group.</summary>
    Group,

    /// <summary>An <c>a</c> anchor (hyperlink).</summary>
    Anchor,

    /// <summary>A <c>use</c> reference to another element.</summary>
    Use,

    /// <summary>A <c>switch</c> element.</summary>
    Switch,

    /// <summary>An <c>image</c> element.</summary>
    Image,

    /// <summary>A <c>text</c> element (or one of its text children).</summary>
    Text,

    /// <summary>A <c>marker</c> element.</summary>
    Marker,

    /// <summary>A <c>path</c> element.</summary>
    Path,

    /// <summary>A basic shape - <c>circle</c>, <c>ellipse</c>, <c>rect</c>, <c>line</c>, <c>polyline</c>, or <c>polygon</c>.</summary>
    Shape,

    /// <summary>A <c>mask</c> element.</summary>
    Mask,

    /// <summary>A container element that is none of the above.</summary>
    Container,
}
