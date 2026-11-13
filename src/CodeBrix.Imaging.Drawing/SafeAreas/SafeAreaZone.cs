namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// One unsafe rectangle of a frame, as a <see cref="SafeAreaGuide"/> lays it out for a
/// particular frame size: an edge band ("top margin"), the area a crop cuts away, or the
/// whole frame. Zones of one guide may overlap.
/// </summary>
public sealed class SafeAreaZone
{
    internal SafeAreaZone(string guideName, string zoneName, RectangleF bounds)
    {
        GuideName = guideName;
        ZoneName = zoneName;
        Bounds = bounds;
    }

    /// <summary>The name of the guide the zone belongs to (for a combined guide, the part's own name).</summary>
    public string GuideName { get; }

    /// <summary>
    /// The zone's name: "top margin", "bottom margin", "left margin", "right margin",
    /// "whole frame", or the name of the <see cref="SafeAreaCrop"/> that cut it away.
    /// </summary>
    public string ZoneName { get; }

    /// <summary>The zone, in frame pixels (origin at the frame's top-left).</summary>
    public RectangleF Bounds { get; }

    /// <inheritdoc />
    public override string ToString() => $"{GuideName}: {ZoneName} {Bounds}";
}
