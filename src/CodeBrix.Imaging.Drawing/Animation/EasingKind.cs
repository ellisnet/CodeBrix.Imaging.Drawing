namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// The named easing curves of <see cref="Easing"/>, for choosing a curve by value - from
/// configuration, a list in a UI, or a loop over every curve - through
/// <see cref="Easing.Evaluate(EasingKind, double)"/> or <see cref="Easing.GetFunction(EasingKind)"/>.
/// The parameterised curves (<see cref="Easing.Steps(double, int, StepPosition)"/> and
/// <see cref="CubicBezierEasing"/>) are not listed because they need more than a name.
/// The "In" curves start slowly, the "Out" curves end slowly, and the "InOut" curves do
/// both; the conventional formulas are documented on each <see cref="Easing"/> method.
/// </summary>
public enum EasingKind
{
    /// <summary>No easing: <see cref="Easing.Linear(double)"/>.</summary>
    Linear = 0,

    /// <summary><see cref="Easing.SmoothStep(double)"/> - the classic Hermite ramp.</summary>
    SmoothStep = 1,

    /// <summary><see cref="Easing.SmootherStep(double)"/> - a ramp that is also flat in its second derivative.</summary>
    SmootherStep = 2,

    /// <summary><see cref="Easing.QuadIn(double)"/>.</summary>
    QuadIn = 10,

    /// <summary><see cref="Easing.QuadOut(double)"/>.</summary>
    QuadOut = 11,

    /// <summary><see cref="Easing.QuadInOut(double)"/>.</summary>
    QuadInOut = 12,

    /// <summary><see cref="Easing.CubicIn(double)"/>.</summary>
    CubicIn = 20,

    /// <summary><see cref="Easing.CubicOut(double)"/>.</summary>
    CubicOut = 21,

    /// <summary><see cref="Easing.CubicInOut(double)"/>.</summary>
    CubicInOut = 22,

    /// <summary><see cref="Easing.QuartIn(double)"/>.</summary>
    QuartIn = 30,

    /// <summary><see cref="Easing.QuartOut(double)"/>.</summary>
    QuartOut = 31,

    /// <summary><see cref="Easing.QuartInOut(double)"/>.</summary>
    QuartInOut = 32,

    /// <summary><see cref="Easing.QuintIn(double)"/>.</summary>
    QuintIn = 40,

    /// <summary><see cref="Easing.QuintOut(double)"/>.</summary>
    QuintOut = 41,

    /// <summary><see cref="Easing.QuintInOut(double)"/>.</summary>
    QuintInOut = 42,

    /// <summary><see cref="Easing.SineIn(double)"/>.</summary>
    SineIn = 50,

    /// <summary><see cref="Easing.SineOut(double)"/>.</summary>
    SineOut = 51,

    /// <summary><see cref="Easing.SineInOut(double)"/>.</summary>
    SineInOut = 52,

    /// <summary><see cref="Easing.ExpoIn(double)"/>.</summary>
    ExpoIn = 60,

    /// <summary><see cref="Easing.ExpoOut(double)"/>.</summary>
    ExpoOut = 61,

    /// <summary><see cref="Easing.ExpoInOut(double)"/>.</summary>
    ExpoInOut = 62,

    /// <summary><see cref="Easing.CircIn(double)"/>.</summary>
    CircIn = 70,

    /// <summary><see cref="Easing.CircOut(double)"/>.</summary>
    CircOut = 71,

    /// <summary><see cref="Easing.CircInOut(double)"/>.</summary>
    CircInOut = 72,

    /// <summary><see cref="Easing.BackIn(double)"/> - pulls back below 0 before moving (overshoot 1.70158).</summary>
    BackIn = 80,

    /// <summary><see cref="Easing.BackOut(double)"/> - overshoots past 1 before settling.</summary>
    BackOut = 81,

    /// <summary><see cref="Easing.BackInOut(double)"/>.</summary>
    BackInOut = 82,

    /// <summary><see cref="Easing.ElasticIn(double)"/> - a growing spring oscillation.</summary>
    ElasticIn = 90,

    /// <summary><see cref="Easing.ElasticOut(double)"/> - a decaying spring oscillation about 1.</summary>
    ElasticOut = 91,

    /// <summary><see cref="Easing.ElasticInOut(double)"/>.</summary>
    ElasticInOut = 92,

    /// <summary><see cref="Easing.BounceIn(double)"/>.</summary>
    BounceIn = 100,

    /// <summary><see cref="Easing.BounceOut(double)"/> - a ball dropped onto 1, bouncing to rest.</summary>
    BounceOut = 101,

    /// <summary><see cref="Easing.BounceInOut(double)"/>.</summary>
    BounceInOut = 102,
}
