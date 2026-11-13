using System;
using CodeBrix.Imaging.Drawing.Animation;

namespace VanGoghPaintingDemo.Painting;

/// <summary>
/// The animation's frame, clock and cue sheet. Everything painted is a pure function of the animation time, so
/// the live window, the stills and the rendered video always agree. The clock and its cues are a
/// CodeBrix.Imaging.Drawing <see cref="AnimationTimeline" />; every fade and cue eases with a smooth step.
/// <para>
/// The video runs eight seconds ahead of its soundtrack (an eight-second lead for a title card), so every cue
/// below is in video time.
/// </para>
/// </summary>
internal static class Timeline
{
    /// <summary>The frame's width, pixels.</summary>
    public const int Width = 1080;

    /// <summary>The frame's height, pixels.</summary>
    public const int Height = 1920;

    /// <summary>The underpainting's width: the scene is computed at half the frame's size.</summary>
    public const int HalfWidth = Width / 2;

    /// <summary>The underpainting's height.</summary>
    public const int HalfHeight = Height / 2;

    /// <summary>The horizon, frame pixels from the top.</summary>
    public const double Horizon = 824.0;

    /// <summary>The foreground quay's edge, frame pixels from the top.</summary>
    public const double QuayEdge = 1655.0;

    /// <summary>The animation's length, seconds.</summary>
    public const double TotalSeconds = 305.56;

    /// <summary>Frames per second of the video.</summary>
    public const int Fps = 30;

    /// <summary>Seconds between paintings: every video frame dissolves between the two nearest ones.</summary>
    public const double KeyframeSeconds = 2.0;

    /// <summary>The number of brushstroke layouts; successive paintings use them in turn.</summary>
    public const int LayoutCount = 3;

    /// <summary>The animation's clock: its length, frame rate and the cues below.</summary>
    public static readonly AnimationTimeline Clock = new AnimationTimeline(TotalSeconds, Fps);

    /// <summary>The fade in from black.</summary>
    public static readonly TimelineCue FadeIn = Clock.AddCue("fade-in", 8.0, 17.0, EasingKind.SmoothStep);

    /// <summary>The boat fades up out of the dark.</summary>
    public static readonly TimelineCue BoatAppears = Clock.AddCue("boat-appears", 14.0, 28.0, EasingKind.SmoothStep);

    /// <summary>The boat's approach, from far off to docked at the quay (the boat eases its own speed).</summary>
    public static readonly TimelineCue Approach = Clock.AddCue("approach", 14.0, 184.0);

    /// <summary>The Octaquetra's cue.</summary>
    public static readonly TimelineCue Octaquetra = Clock.AddCue("octaquetra", 96.0, 114.0, EasingKind.SmoothStep);

    /// <summary>The Octaquetra itself grows over the last twelve seconds of its cue.</summary>
    public static readonly TimelineCue StarGrows = Clock.AddCue("star-grows", Octaquetra.StartTime + 6, Octaquetra.EndTime, EasingKind.SmoothStep);

    /// <summary>The temple doors open to the sky.</summary>
    public static readonly TimelineCue Doors = Clock.AddCue("doors", 186.0, 198.0, EasingKind.SmoothStep);

    /// <summary>Night turns to dawn.</summary>
    public static readonly TimelineCue Dawn = Clock.AddCue("dawn", 218.0, 272.0, EasingKind.SmoothStep);

    /// <summary>The sun's disc rises over the second half of the dawn.</summary>
    public static readonly TimelineCue SunRises = Clock.AddCue("sun-rises", Dawn.StartTime + 22, Dawn.EndTime, EasingKind.SmoothStep);

    /// <summary>The fade out to black.</summary>
    public static readonly TimelineCue FadeOut = Clock.AddCue("fade-out", 302.5, 305.56, EasingKind.SmoothStep);

    /// <summary>When each torch is lit: the two on the quay, then the three at the far temple.</summary>
    public static readonly double[] TorchesLit = { 9.5, 13.0, 16.0, 18.5, 20.5 };

    /// <summary>How far a torch has caught, 0..1: a smooth step over the 0.8 s after it is lit.</summary>
    /// <param name="torch">The torch's number.</param>
    /// <param name="seconds">The animation time.</param>
    /// <returns>0 before it is lit, 1 once it burns fully.</returns>
    public static double TorchCaught(int torch, double seconds) => Easing.SmoothStep(TorchesLit[torch], TorchesLit[torch] + 0.8, seconds);

    /// <summary>The quay's slightly uneven edge at a frame x.</summary>
    /// <param name="x">The frame x.</param>
    /// <returns>The edge's frame y.</returns>
    public static double QuayY(double x) => QuayEdge + (5 * Math.Sin(x / 41.0)) + (3 * Math.Sin((x / 13.0) + 1.0));
}
