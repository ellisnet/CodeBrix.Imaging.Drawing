using System;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// How an element travels along a path (<see cref="DrawingAnimations.BindAlongPath(DrawingElement, CodeBrix.Imaging.Drawing.Paths.PathGeometry, CodeBrix.Imaging.Drawing.Animation.KeyframeTrack{float}, PathTravelOptions)"/>):
/// whether it faces along the path, whether it ROLLS like a wheel as it goes, and what happens
/// to a progress below 0 or above 1. The defaults are exactly the plain
/// <c>BindAlongPath(element, path, progress)</c>: no facing, no roll, progress clamped.
/// <para>
/// FACING and ROLLING ADD: with both, the element's rotation is the path's direction there plus
/// <see cref="RotationOffset"/> plus the roll - a wheel rolling over a hill both tips with the
/// slope and turns with the distance.
/// </para>
/// </summary>
public sealed class PathTravelOptions
{
    /// <summary>Whether the element turns to face along the path (its rotation follows the path's direction); <c>false</c> by default.</summary>
    public bool OrientToPath { get; set; }

    /// <summary>
    /// Degrees added to the element's rotation - to the facing direction when
    /// <see cref="OrientToPath"/> is set (for content drawn facing another way than right), or to
    /// the roll; 0 by default.
    /// </summary>
    public float RotationOffset { get; set; }

    /// <summary>
    /// Whether progress below 0 or above 1 continues straight along the path's end directions (see
    /// <c>PathGeometry.GetPointAt(float, bool)</c>) instead of being clamped - what an
    /// overshoot-and-settle needs: a progress track that runs to 1.05 and eases back to 1 carries
    /// the element a little past the end and back. <c>false</c> (clamped) by default.
    /// </summary>
    public bool ExtendBeyondEnds { get; set; }

    /// <summary>
    /// The rolling radius, calibrated units: the element turns like a wheel of this radius rolling
    /// along the path - by (distance travelled / radius) radians, shown in degrees - clockwise when
    /// the path heads right with the ground below it. A negative radius rolls the other way (the
    /// ground on the path's other side). 0 (the default) means no roll.
    /// </summary>
    public float RollRadius { get; set; }

    /// <summary>
    /// The progress at which a rolling element is upright (its roll is 0) - 1 (the default) at the
    /// path's end; use the progress where it comes to rest when it rolls past its resting place and
    /// back (an overshoot).
    /// </summary>
    public float UprightAtProgress { get; set; } = 1f;

    internal void Validate()
    {
        if (!Single.IsFinite(RotationOffset)) { throw new ArgumentOutOfRangeException(nameof(RotationOffset)); }
        if (!Single.IsFinite(RollRadius)) { throw new ArgumentOutOfRangeException(nameof(RollRadius)); }
        if (!Single.IsFinite(UprightAtProgress)) { throw new ArgumentOutOfRangeException(nameof(UprightAtProgress)); }
    }
}
