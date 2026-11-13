using CodeBrix.Imaging.Drawing.Models;

namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// One result of a safe-area check: a marked element that reaches into one unsafe zone
/// (an element overlapping several zones gives one finding per zone), or a marked element
/// that could not be checked. Findings are advisory information - a check never throws
/// because of what it finds.
/// </summary>
public sealed class SafeAreaFinding
{
    internal SafeAreaFinding(SafeAreaFindingKind kind, DrawingLayer layer, DrawingElement element, int elementIndex,
        SafeAreaZone zone, RectangleF? overlap, RectangleF? frameOverlap)
    {
        Kind = kind;
        Layer = layer;
        Element = element;
        ElementIndex = elementIndex;
        Zone = zone;
        Overlap = overlap;
        FrameOverlap = frameOverlap;
    }

    /// <summary>What the finding reports.</summary>
    public SafeAreaFindingKind Kind { get; }

    /// <summary>The layer holding the element.</summary>
    public DrawingLayer Layer { get; }

    /// <summary>The element (a stroke or a shape).</summary>
    public DrawingElement Element { get; }

    /// <summary>The element's position in its layer's <see cref="DrawingLayer.GetElements"/> at the time of the check.</summary>
    public int ElementIndex { get; }

    /// <summary>The unsafe zone the element reaches into; <c>null</c> for <see cref="SafeAreaFindingKind.CouldNotCheck"/>.</summary>
    public SafeAreaZone Zone { get; }

    /// <summary>The name of the guide the zone belongs to; <c>null</c> for <see cref="SafeAreaFindingKind.CouldNotCheck"/>.</summary>
    public string GuideName => Zone?.GuideName;

    /// <summary>The zone's name (for example "bottom margin"); <c>null</c> for <see cref="SafeAreaFindingKind.CouldNotCheck"/>.</summary>
    public string ZoneName => Zone?.ZoneName;

    /// <summary>
    /// The part of the element's bounds inside the zone, in CALIBRATED drawing units (the
    /// units the element was drawn in); <c>null</c> for <see cref="SafeAreaFindingKind.CouldNotCheck"/>.
    /// </summary>
    public RectangleF? Overlap { get; }

    /// <summary>
    /// The same overlap in FRAME pixels of the checked frame size; <c>null</c> for
    /// <see cref="SafeAreaFindingKind.CouldNotCheck"/>.
    /// </summary>
    public RectangleF? FrameOverlap { get; }

    /// <inheritdoc />
    public override string ToString()
        => Kind == SafeAreaFindingKind.CouldNotCheck
            ? $"{Layer?.Name} #{ElementIndex} ({Element?.GetType().Name}): could not check (bounds unknown)"
            : $"{Layer?.Name} #{ElementIndex} ({Element?.GetType().Name}): in {GuideName} {ZoneName}, overlap {Overlap}";
}
