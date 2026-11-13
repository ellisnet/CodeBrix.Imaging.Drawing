namespace CodeBrix.Imaging.Drawing.Animation;

/// <summary>
/// What a <see cref="KeyframeTrack{T}"/> does outside the span from its first key to its last.
/// </summary>
public enum TrackLoopMode
{
    /// <summary>The first value holds before the first key and the last value after the last key.</summary>
    Hold = 0,

    /// <summary>The span repeats forever, in both directions (the last key's time is the next cycle's start).</summary>
    Loop = 1,

    /// <summary>The span plays forward, then backward, then forward again, forever, in both directions.</summary>
    PingPong = 2,
}
