namespace CodeBrix.Imaging.Drawing.NoSkia.Svg;

/// <summary>
/// How a <see cref="DrawingSvgShape"/> fits the document's bounds (<see cref="DrawingSvg.Bounds"/>)
/// into its destination rectangle.
/// </summary>
public enum DrawingSvgAspect
{
    /// <summary>Scaled uniformly to fit entirely inside the destination, centred (the default; SVG "meet").</summary>
    Meet = 0,

    /// <summary>Scaled uniformly to cover the whole destination, centred, and clipped to it (SVG "slice").</summary>
    Slice = 1,

    /// <summary>Stretched to the destination's width and height independently.</summary>
    Stretch = 2,

    /// <summary>Not scaled: one document unit is one calibrated unit, the document's top-left at the destination's top-left.</summary>
    None = 3,
}
