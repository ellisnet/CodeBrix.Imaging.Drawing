using CodeBrix.Imaging.Drawing.Models;

namespace CodeBrix.Imaging.Drawing.Painting;

/// <summary>
/// One pixel of an ownership map, measured in its owning element's own axes - what a brush
/// texture function receives (see <see cref="DrawingOwnershipMap.FillByElement"/>). The pixel's
/// CENTRE is taken back through the element's full transform (output fit, layer view, groups,
/// the element's own transform) into the element's own coordinates, then measured from the
/// axes' centre: <see cref="Along"/> on the direction <c>(cos angle, sin angle)</c> and
/// <see cref="Across"/> on <c>(-sin angle, cos angle)</c> (to the right of travel on screen,
/// y down), both in the element's own calibrated units.
/// </summary>
public readonly struct ElementPixel
{
    internal ElementPixel(int x, int y, int elementIndex, double along, double across, ElementAxes axes)
    {
        X = x;
        Y = y;
        ElementIndex = elementIndex;
        Along = along;
        Across = across;
        Axes = axes;
    }

    /// <summary>The pixel's column in the map.</summary>
    public int X { get; }

    /// <summary>The pixel's row in the map.</summary>
    public int Y { get; }

    /// <summary>The owning element's index in the map.</summary>
    public int ElementIndex { get; }

    /// <summary>How far along the element's direction the pixel centre lies from its axes' centre, element units.</summary>
    public double Along { get; }

    /// <summary>How far across the element's direction the pixel centre lies from its axes' centre, element units.</summary>
    public double Across { get; }

    /// <summary>The owning element's axes.</summary>
    public ElementAxes Axes { get; }

    /// <summary><see cref="Along"/> divided by half the axes' length: -1 at one end, 0 at the centre, +1 at the other end (0 when the length is 0).</summary>
    public double NormalizedAlong => Axes.Length > 0f ? Along / (Axes.Length / 2.0) : 0.0;

    /// <summary><see cref="Across"/> divided by half the axes' width: -1 at one side, +1 at the other (0 when the width is 0).</summary>
    public double NormalizedAcross => Axes.Width > 0f ? Across / (Axes.Width / 2.0) : 0.0;
}
