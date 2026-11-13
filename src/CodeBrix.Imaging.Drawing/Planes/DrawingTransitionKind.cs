namespace CodeBrix.Imaging.Drawing.Planes;

/// <summary>
/// The kinds of frame-to-frame transition a <see cref="DrawingTransition"/> performs.
/// </summary>
public enum DrawingTransitionKind
{
    /// <summary>A cross-fade: every pixel mixes from the outgoing to the incoming frame.</summary>
    Dissolve = 0,

    /// <summary>A moving edge (hard or soft) replaces the outgoing frame with the incoming one; nothing moves.</summary>
    Wipe = 1,

    /// <summary>The incoming frame slides in over the outgoing frame, which stays still.</summary>
    Slide = 2,

    /// <summary>The incoming frame slides in and pushes the outgoing frame out ahead of it.</summary>
    Push = 3,

    /// <summary>The outgoing frame fades to a colour over the first half, then the colour fades to the incoming frame.</summary>
    FadeThroughColor = 4,
}
