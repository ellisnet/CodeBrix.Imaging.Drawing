namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// Where the jumps of <see cref="Easing.Steps(double, int, StepPosition)"/> happen - the
/// four positions of the CSS <c>steps()</c> timing function.
/// </summary>
public enum StepPosition
{
    /// <summary>
    /// Each jump happens at the END of its interval (CSS <c>jump-end</c>, the default):
    /// the output starts at 0 and reaches 1 only at t = 1.
    /// </summary>
    End = 0,

    /// <summary>
    /// Each jump happens at the START of its interval (CSS <c>jump-start</c>): the output
    /// leaves 0 immediately and holds 1 for the final interval.
    /// </summary>
    Start = 1,

    /// <summary>
    /// No jump at either end (CSS <c>jump-none</c>): the output holds 0 for the first
    /// interval and 1 for the last. Needs at least two steps.
    /// </summary>
    None = 2,

    /// <summary>
    /// Jumps at both ends (CSS <c>jump-both</c>): neither 0 nor 1 is held for a whole
    /// interval, giving one more level than steps.
    /// </summary>
    Both = 3,
}
