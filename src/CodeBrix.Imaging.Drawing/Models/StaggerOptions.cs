using System;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// The settings of a staggered entrance (<see cref="DrawingAnimations.BindStagger"/>): each element
/// starts <see cref="Interval"/> seconds after the one before it (in <see cref="Order"/>) and takes
/// <see cref="Duration"/> seconds to go from its "from" state - faded, moved by an offset, scaled,
/// turned - to where it was drawn. The defaults fade each element in over 0.4 s, 0.05 s apart.
/// </summary>
public sealed class StaggerOptions
{
    /// <summary>When the first element starts, in seconds; 0 by default.</summary>
    public double StartTime { get; set; }

    /// <summary>The time between one element's start and the next one's, in seconds (0 or more); 0.05 by default.</summary>
    public double Interval { get; set; } = 0.05;

    /// <summary>How long each element's entrance takes, in seconds (0 or more); 0.4 by default.</summary>
    public double Duration { get; set; } = 0.4;

    /// <summary>The easing of each element's entrance; <see cref="EasingKind.CubicOut"/> by default.</summary>
    public EasingKind Easing { get; set; } = EasingKind.CubicOut;

    /// <summary>The order the elements start in; <see cref="StaggerOrder.Forward"/> by default.</summary>
    public StaggerOrder Order { get; set; } = StaggerOrder.Forward;

    /// <summary>The opacity each element starts at, 0..1; 0 by default.</summary>
    public float FromOpacity { get; set; }

    /// <summary>The opacity each element ends at, 0..1; 1 by default.</summary>
    public float ToOpacity { get; set; } = 1f;

    /// <summary>
    /// How far from where it was drawn each element starts, calibrated units (it slides to no
    /// offset); none by default. When set, the stagger drives the elements' translation.
    /// </summary>
    public PointF FromOffset { get; set; }

    /// <summary>The scale each element starts at (it grows or shrinks to 1); 1 (no change) by default.</summary>
    public float FromScale { get; set; } = 1f;

    /// <summary>The rotation each element starts at, degrees clockwise (it turns to 0); 0 by default.</summary>
    public float FromRotation { get; set; }

    /// <summary>
    /// Whether each element scales and turns about the centre of its own bounds (the default) - its
    /// transform's anchor is set there when the stagger is bound; <c>false</c> keeps each anchor.
    /// </summary>
    public bool AnchorAtCenter { get; set; } = true;

    //Rejects values no stagger can honour, naming the property at fault
    internal void Validate()
    {
        if (!Double.IsFinite(StartTime)) { throw new ArgumentOutOfRangeException(nameof(StartTime)); }
        if (!Double.IsFinite(Interval) || Interval < 0) { throw new ArgumentOutOfRangeException(nameof(Interval)); }
        if (!Double.IsFinite(Duration) || Duration < 0) { throw new ArgumentOutOfRangeException(nameof(Duration)); }
        if (!Enum.IsDefined(Easing)) { throw new ArgumentOutOfRangeException(nameof(Easing)); }
        if (!Enum.IsDefined(Order)) { throw new ArgumentOutOfRangeException(nameof(Order)); }
        if (Single.IsNaN(FromOpacity) || FromOpacity < 0f || FromOpacity > 1f) { throw new ArgumentOutOfRangeException(nameof(FromOpacity)); }
        if (Single.IsNaN(ToOpacity) || ToOpacity < 0f || ToOpacity > 1f) { throw new ArgumentOutOfRangeException(nameof(ToOpacity)); }
        if (!Single.IsFinite(FromOffset.X) || !Single.IsFinite(FromOffset.Y)) { throw new ArgumentOutOfRangeException(nameof(FromOffset)); }
        if (!Single.IsFinite(FromScale)) { throw new ArgumentOutOfRangeException(nameof(FromScale)); }
        if (!Single.IsFinite(FromRotation)) { throw new ArgumentOutOfRangeException(nameof(FromRotation)); }
    }
}
