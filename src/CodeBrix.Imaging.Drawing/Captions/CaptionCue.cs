using System;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.Captions;

/// <summary>
/// One caption: a span of time [<see cref="StartTime"/>, <see cref="EndTime"/>) and the text shown during
/// it, with an optional position, alignment and style of its own. Immutable (the optional values
/// are set with an object initializer).
/// </summary>
public sealed class CaptionCue
{
    /// <summary>
    /// Creates a cue.
    /// </summary>
    /// <param name="startTime">When the cue appears, in seconds; finite.</param>
    /// <param name="endTime">When it disappears, in seconds; finite and not before <paramref name="startTime"/>.</param>
    /// <param name="text">The text shown (line breaks kept); null is treated as empty.</param>
    /// <param name="identifier">The cue's identifier (the SRT number or WebVTT cue id); or <c>null</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a time is not finite or <paramref name="endTime"/> is before <paramref name="startTime"/>.</exception>
    public CaptionCue(double startTime, double endTime, string text, string identifier = null)
    {
        if (!Double.IsFinite(startTime)) { throw new ArgumentOutOfRangeException(nameof(startTime)); }
        if (!Double.IsFinite(endTime) || endTime < startTime) { throw new ArgumentOutOfRangeException(nameof(endTime)); }
        StartTime = startTime;
        EndTime = endTime;
        Text = text ?? String.Empty;
        Identifier = identifier;
    }

    /// <summary>When the cue appears, in seconds.</summary>
    public double StartTime { get; }

    /// <summary>When the cue disappears, in seconds (it shows while StartTime &lt;= t &lt; EndTime).</summary>
    public double EndTime { get; }

    /// <summary>How long the cue shows, in seconds.</summary>
    public double Duration => EndTime - StartTime;

    /// <summary>The text shown.</summary>
    public string Text { get; }

    /// <summary>The cue's identifier; <c>null</c> when it has none.</summary>
    public string Identifier { get; }

    /// <summary>
    /// Where this cue's box goes instead of the style's position (a WebVTT <c>line:</c> or
    /// <c>position:</c> percentage sets it); <c>null</c> (the default) for the style's.
    /// </summary>
    public LayoutPosition? Position { get; init; }

    /// <summary>How this cue's lines align instead of the style's (a WebVTT <c>align:</c> setting sets it); <c>null</c> for the style's.</summary>
    public TextAlign? Align { get; init; }

    /// <summary>A style for this cue instead of the presenter's; <c>null</c> (the default) for the presenter's.</summary>
    public CaptionStyle Style { get; init; }

    /// <summary>The cue's WebVTT settings exactly as written after the timing (for example "align:start line:10%"); <c>null</c> for none.</summary>
    public string Settings { get; init; }

    /// <summary>Indicates whether the cue shows at a time: <see cref="StartTime"/> &lt;= t &lt; <see cref="EndTime"/>.</summary>
    /// <param name="seconds">The time.</param>
    /// <returns><c>true</c> while it shows.</returns>
    public bool IsActiveAt(double seconds) => seconds >= StartTime && seconds < EndTime;

    /// <inheritdoc />
    public override string ToString() => $"{StartTime:0.###} --> {EndTime:0.###}: {Text}";
}
