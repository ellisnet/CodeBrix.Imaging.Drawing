namespace CodeBrix.Imaging.Drawing.Effects;

/// <summary>What a <see cref="DrawingMask"/> keeps of the content it is applied to.</summary>
public enum DrawingMaskKind
{
    /// <summary>Only what lies inside a path (a clip).</summary>
    Inside = 0,

    /// <summary>Only what lies outside a path (a cut-out).</summary>
    Outside = 1,

    /// <summary>The content where another layer's elements cover the frame (a matte), or where they do not when inverted.</summary>
    Layer = 2,

    /// <summary>The content scaled by the values of a pixel plane (an alpha mask), or by one minus them when inverted.</summary>
    Plane = 3,
}
