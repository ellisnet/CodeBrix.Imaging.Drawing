namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// The property a keyframe track drives through <see cref="DrawingAnimations"/>. Which track
/// type each takes, and what it sets on an element, a layer and the camera:
/// <list type="table">
///   <item><term><see cref="Position"/></term><description>point track: Transform.Translation (camera: Pan)</description></item>
///   <item><term><see cref="Anchor"/></term><description>point track: Transform.Anchor (camera: Pivot)</description></item>
///   <item><term><see cref="Rotation"/></term><description>float track, degrees: Transform.Rotation (camera: Rotation)</description></item>
///   <item><term><see cref="Scale"/></term><description>float track (both ways) or point track (X, Y): Transform scale (camera: Zoom, float only)</description></item>
///   <item><term><see cref="ScaleX"/>, <see cref="ScaleY"/></term><description>float track: one axis of the Transform scale (not the camera)</description></item>
///   <item><term><see cref="Opacity"/></term><description>float track, clamped to 0..1: Opacity (not the camera)</description></item>
///   <item><term><see cref="Color"/></term><description>color track: a layer's Color, or a group's ContentColor</description></item>
///   <item><term><see cref="TrimStart"/>, <see cref="TrimEnd"/>, <see cref="TrimOffset"/>, <see cref="MorphFraction"/></term><description>float track: a path shape's trim range and morph (clamped to 0..1 except the offset)</description></item>
///   <item><term><see cref="FrameIndex"/></term><description>float track, rounded down: a sprite's frame index</description></item>
///   <item><term><see cref="Tint"/></term><description>color track: an image's or sprite's tint</description></item>
///   <item><term><see cref="BlurRadius"/>, <see cref="ShadowBlurRadius"/>, <see cref="GlowRadius"/></term><description>float track (negative values count as 0): the element's or layer's effects</description></item>
///   <item><term><see cref="ShadowOffset"/></term><description>point track: the element's or layer's shadow offset</description></item>
///   <item><term><see cref="ShadowColor"/>, <see cref="GlowColor"/></term><description>color track: the element's, group's or layer's shadow or glow colour</description></item>
/// </list>
/// </summary>
public enum AnimatedProperty
{
    /// <summary>The translation (an element's or layer's), or the camera's pan.</summary>
    Position = 0,

    /// <summary>The pivot of rotation and scale (an element's or layer's), or the camera's pivot.</summary>
    Anchor = 1,

    /// <summary>The rotation, degrees.</summary>
    Rotation = 2,

    /// <summary>The scale (both ways from a float track, each way from a point track), or the camera's zoom.</summary>
    Scale = 3,

    /// <summary>The horizontal scale only.</summary>
    ScaleX = 4,

    /// <summary>The vertical scale only.</summary>
    ScaleY = 5,

    /// <summary>The opacity, 0..1.</summary>
    Opacity = 6,

    /// <summary>The color: a layer's <see cref="DrawingLayer.Color"/> or a group's content color.</summary>
    Color = 7,

    /// <summary>Where a path shape's drawn part starts (its TrimStart), 0..1.</summary>
    TrimStart = 8,

    /// <summary>Where a path shape's drawn part ends (its TrimEnd), 0..1 - animate it 0 to 1 to draw the path on.</summary>
    TrimEnd = 9,

    /// <summary>A shift of a path shape's drawn part along the path (its TrimOffset), in fractions of its length.</summary>
    TrimOffset = 10,

    /// <summary>How far a morphing path shape has gone (its MorphFraction), 0..1.</summary>
    MorphFraction = 11,

    /// <summary>A sprite's frame index (the track's value rounded down).</summary>
    FrameIndex = 12,

    /// <summary>An image's or sprite's tint colour.</summary>
    Tint = 13,

    /// <summary>The blur radius of the content's effects.</summary>
    BlurRadius = 14,

    /// <summary>The offset of the drop shadow.</summary>
    ShadowOffset = 15,

    /// <summary>The softness (blur radius) of the drop shadow.</summary>
    ShadowBlurRadius = 16,

    /// <summary>The colour (and opacity) of the drop shadow.</summary>
    ShadowColor = 17,

    /// <summary>How far the glow spreads.</summary>
    GlowRadius = 18,

    /// <summary>The colour (and opacity) of the glow.</summary>
    GlowColor = 19,

    /// <summary>
    /// A text block's reveal as a fraction 0..1 of its characters, words or lines
    /// (<see cref="CodeBrix.Imaging.Drawing.Shapes.TextBlockShape.RevealFraction"/>; clamped).
    /// </summary>
    RevealFraction = 20,

    /// <summary>
    /// A text block's reveal as a count of characters, words or lines
    /// (<see cref="CodeBrix.Imaging.Drawing.Shapes.TextBlockShape.RevealCount"/>; negative counts as 0).
    /// </summary>
    RevealCount = 21,

    /// <summary>
    /// Whether a layer is shown (<see cref="DrawingLayer.IsVisible"/>) - a step value, driven by a
    /// <see cref="CodeBrix.Imaging.Drawing.Animation.BoolTrack"/>.
    /// </summary>
    IsVisible = 22,
}
