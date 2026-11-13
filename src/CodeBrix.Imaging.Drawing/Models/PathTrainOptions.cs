using System;
using CodeBrix.Imaging.Drawing.Animation;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// The settings of a TRAIN along a path (<see cref="DrawingAnimations.BindTrainAlongPath"/> and
/// <see cref="DrawingAnimations.BindTextAlongPath"/>): several elements travelling one path one
/// behind the other, each optionally facing along it, rolling, scaling and fading as it goes.
/// <para>
/// Element k (by its rank in <see cref="Order"/>; rank 0 leads) is at distance
/// <c>progress(t - rank x Stagger) x length - rank x Spacing x SpacingScale(t)</c> along the path:
/// <see cref="Spacing"/> keeps the elements apart along the path (and <see cref="SpacingScale"/>
/// lets the gaps open and close over time), <see cref="Stagger"/> makes each one follow the
/// progress track a little later than the one ahead. The defaults put every element at the same
/// spot - set at least one of them.
/// </para>
/// </summary>
public sealed class PathTrainOptions
{
    /// <summary>The distance between neighbouring elements along the path, calibrated units (0 or more); 0 by default.</summary>
    public float Spacing { get; set; }

    /// <summary>
    /// A factor on every gap over time - from 2 to 1, say, to let a spread-out train close up as it
    /// arrives; <c>null</c> (the default) keeps the gaps at <see cref="Spacing"/>. Gaps at rest use
    /// the track's last value.
    /// </summary>
    public KeyframeTrack<float> SpacingScale { get; set; }

    /// <summary>How much later, in seconds, each element follows the progress track than the one ahead of it (0 or more); 0 by default.</summary>
    public double Stagger { get; set; }

    /// <summary>Which element leads: <see cref="StaggerOrder.Forward"/> (the default) - the first; <see cref="StaggerOrder.Reverse"/> - the last; <see cref="StaggerOrder.CenterOut"/> - the middle ones.</summary>
    public StaggerOrder Order { get; set; } = StaggerOrder.Forward;

    /// <summary>Whether each element turns to face along the path; <c>false</c> by default.</summary>
    public bool OrientToPath { get; set; }

    /// <summary>Degrees added to each element's rotation (facing or roll); 0 by default.</summary>
    public float RotationOffset { get; set; }

    /// <summary>Whether distances before the start or past the end continue straight along the path's end directions instead of stopping there; <c>false</c> by default.</summary>
    public bool ExtendBeyondEnds { get; set; }

    /// <summary>
    /// The rolling radius of every element, calibrated units (negative rolls the other way; 0, the
    /// default, no roll) - see <see cref="PathTravelOptions.RollRadius"/>. Each element's own
    /// <see cref="PathTrainCar.RollRadius"/> can be changed afterwards.
    /// </summary>
    public float RollRadius { get; set; }

    /// <summary>
    /// For <see cref="DrawingAnimations.BindTextAlongPath"/> only: each text piece rolls on its own
    /// line's baseline - its radius is the height of its centre above that baseline - instead of
    /// at <see cref="RollRadius"/>; <c>false</c> by default.
    /// </summary>
    public bool RollOnBaseline { get; set; }

    /// <summary>
    /// The lead's progress at which every element is upright (rolled 0) - 1 (the default) at the end;
    /// each element is upright where it is when the lead is there and the gaps are at rest.
    /// </summary>
    public float UprightAtProgress { get; set; } = 1f;

    /// <summary>
    /// A scale for each element as a function of ITS OWN progress along the path (the track's
    /// "time" is the fraction 0..1 of the length the element has reached): pulse as it passes the
    /// middle, grow as it arrives. <c>null</c> (the default) leaves the scale alone.
    /// </summary>
    public KeyframeTrack<float> ScaleAlongPath { get; set; }

    /// <summary>
    /// An opacity (0..1, clamped) for each element as a function of its own progress along the path
    /// - fade in as it sets off, out near the end. <c>null</c> (the default) leaves the opacity alone.
    /// </summary>
    public KeyframeTrack<float> OpacityAlongPath { get; set; }

    /// <summary>
    /// Whether each element's transform anchor is moved to the centre of its own bounds when the
    /// train is bound (the point that rides the path, and that it turns and scales about); <c>true</c>
    /// by default. <c>false</c> keeps each element's anchor.
    /// </summary>
    public bool AnchorAtCenter { get; set; } = true;

    /// <summary>
    /// Whether each element ends up WHERE IT WAS DRAWN: it travels a copy of the path moved so the
    /// path's end lies on the element's own anchor (so a word drawn in place arrives in place,
    /// every piece along the same-shaped route). <c>false</c> (the default) puts every anchor on the
    /// path itself. <see cref="DrawingAnimations.BindTextAlongPath"/> always settles in place.
    /// </summary>
    public bool SettleInPlace { get; set; }

    internal PathTrainOptions Copy() => (PathTrainOptions)MemberwiseClone();

    internal void Validate()
    {
        if (!Single.IsFinite(Spacing) || Spacing < 0f) { throw new ArgumentOutOfRangeException(nameof(Spacing)); }
        if (!Double.IsFinite(Stagger) || Stagger < 0) { throw new ArgumentOutOfRangeException(nameof(Stagger)); }
        if (!Enum.IsDefined(Order)) { throw new ArgumentOutOfRangeException(nameof(Order)); }
        if (!Single.IsFinite(RotationOffset)) { throw new ArgumentOutOfRangeException(nameof(RotationOffset)); }
        if (!Single.IsFinite(RollRadius)) { throw new ArgumentOutOfRangeException(nameof(RollRadius)); }
        if (!Single.IsFinite(UprightAtProgress)) { throw new ArgumentOutOfRangeException(nameof(UprightAtProgress)); }
    }
}
