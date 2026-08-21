using CodeBrix.SvgParse;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg; //Was previously: namespace Svg.Skia;

/// <summary>Identifies the kind of SVG scene node.</summary>
public enum SvgSceneNodeKind
{
    /// <summary>An unknown node type.</summary>
    Unknown,
    /// <summary>An SVG fragment (root) element.</summary>
    Fragment,
    /// <summary>A group element.</summary>
    Group,
    /// <summary>An anchor element.</summary>
    Anchor,
    /// <summary>A use (reference) element.</summary>
    Use,
    /// <summary>A switch element.</summary>
    Switch,
    /// <summary>An image element.</summary>
    Image,
    /// <summary>A text element.</summary>
    Text,
    /// <summary>A marker element.</summary>
    Marker,
    /// <summary>A path element.</summary>
    Path,
    /// <summary>A geometric shape element.</summary>
    Shape,
    /// <summary>A mask element.</summary>
    Mask,
    /// <summary>A generic container element.</summary>
    Container
}

internal static class SvgSceneNodeKindExtensions
{
    public static SvgSceneNodeKind FromElement(SvgElement element)
    {
        return element switch
        {
            SvgFragment => SvgSceneNodeKind.Fragment,
            SvgGroup => SvgSceneNodeKind.Group,
            SvgAnchor => SvgSceneNodeKind.Anchor,
            SvgUse => SvgSceneNodeKind.Use,
            SvgSwitch => SvgSceneNodeKind.Switch,
            SvgImage => SvgSceneNodeKind.Image,
            SvgTextBase => SvgSceneNodeKind.Text,
            SvgMarker => SvgSceneNodeKind.Marker,
            SvgPath => SvgSceneNodeKind.Path,
            SvgCircle or SvgEllipse or SvgRectangle or SvgLine or SvgPolyline or SvgPolygon => SvgSceneNodeKind.Shape,
            SvgMask => SvgSceneNodeKind.Mask,
            _ => SvgSceneNodeKind.Unknown
        };
    }
}
