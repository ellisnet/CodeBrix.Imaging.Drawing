using CodeBrix.Imaging.Drawing.Animation;

namespace AutumnAviana.Animation;

/// <summary>
/// ALL OF THE ANIMATION'S TIMING, in one place: the library timeline <see cref="Clock" /> and its cues. Every
/// moving part reads its times from these cues, so retiming the piece means editing only the numbers below. Each
/// section is placed relative to the one before it, and the finished picture holds from the last raccoon's exit
/// to <see cref="TotalSeconds" />.
/// <para>
/// The story: the white screen; leaves fall (lazily at first, then more, then fewer and fewer) and heap up into
/// piles that become the word AVIANA; the last leaf settles and everything is still; the camera pans down, and as
/// the ground below comes into view the first raccoon is already rolling its letters in from the left; three
/// raccoons roll the letters of "@ Øasys" in, one pair of letters each - the first two walk off to the right, the
/// last runs off in a bounding lope; the finished words hold.
/// </para>
/// </summary>
internal static class Timeline
{
    /// <summary>THE animation's length, seconds - the one constant for the length of the piece.</summary>
    public const double TotalSeconds = 66.2;

    /// <summary>Frames per second.</summary>
    public const int Fps = 30;

    /// <summary>When the first leaf leaves the top edge (the screen is white until then).</summary>
    private const double FallStart = 0.4;

    /// <summary>
    /// When the last leaf comes to rest. The leaf fall is the longest section: long enough that at its busiest
    /// the air still holds separate leaves with white space between them (a lazy fall, not a storm).
    /// </summary>
    private const double FallEnd = 37.0;

    /// <summary>The still moment after the last leaf settles, before the camera moves.</summary>
    private const double StillSeconds = 1.4;

    /// <summary>The camera's move down (eased in and out).</summary>
    private const double PanSeconds = 2.6;

    /// <summary>
    /// How long BEFORE the camera starts moving the first raccoon starts rolling its letters in. They start off the
    /// left edge (and the second line is hidden until the camera moves), so nothing shows yet: the first letter
    /// rolls into the corner just as the camera starts down, and the raccoon pushing its pair comes into view
    /// before the camera stops - the pan reveals the rolling already begun, with most of it still to come.
    /// </summary>
    private const double FirstEntranceBeforePanStart = 0.4;

    /// <summary>Each raccoon's push (entering from the left edge to the letters at rest), seconds.</summary>
    private static readonly double[] PushSeconds = { 8.0, 8.0, 8.0 };

    /// <summary>When each raccoon starts, after the one before it starts (the first: see above), seconds.</summary>
    private static readonly double[] EntranceGaps = { 0, 4.5, 4.5 };

    /// <summary>Each raccoon's beat after its letters settle (letting go, a look, a tail flick), seconds.</summary>
    private static readonly double[] BeatSeconds = { 0.8, 0.8, 0.9 };

    /// <summary>Each raccoon's way off the right edge, seconds: the first two walk, the last runs (a bounding lope).</summary>
    private static readonly double[] ExitSeconds = { 4.4, 6.2, 7.0 };

    /// <summary>The library timeline holding every cue below.</summary>
    public static readonly AnimationTimeline Clock = CreateClock();

    /// <summary>
    /// "fall": from the first leaf leaving the top of the screen to the moment the last leaf comes to rest on its
    /// pile. Landings are scheduled inside it (lazily at first, thickest in the middle, tapering at the end).
    /// </summary>
    public static readonly TimelineCue Fall = Clock.GetCue("fall");

    /// <summary>"still": the finished word with nothing moving, before the camera moves.</summary>
    public static readonly TimelineCue Still = Clock.GetCue("still");

    /// <summary>"pan": the camera moves down so the piles rise in the frame (eased).</summary>
    public static readonly TimelineCue Pan = Clock.GetCue("pan");

    /// <summary>The raccoons' cues, first to last (each pushes one pair of letters, then walks off).</summary>
    public static readonly RaccoonCues[] Raccoons =
    {
        new RaccoonCues(Clock.GetCue("raccoon 1 push"), Clock.GetCue("raccoon 1 beat"), Clock.GetCue("raccoon 1 exit")),
        new RaccoonCues(Clock.GetCue("raccoon 2 push"), Clock.GetCue("raccoon 2 beat"), Clock.GetCue("raccoon 2 exit")),
        new RaccoonCues(Clock.GetCue("raccoon 3 push"), Clock.GetCue("raccoon 3 beat"), Clock.GetCue("raccoon 3 exit")),
    };

    /// <summary>"hold": the finished picture, nothing moving, from the last raccoon's exit to the end (3.3 s).</summary>
    public static readonly TimelineCue Hold = Clock.GetCue("hold");

    /// <summary>The number of frames.</summary>
    public static int FrameCount => Clock.FrameCount;

    private static AnimationTimeline CreateClock()
    {
        AnimationTimeline clock = new AnimationTimeline(TotalSeconds, Fps);
        clock.AddCue("fall", FallStart, FallEnd);
        double panStart = FallEnd + StillSeconds;
        clock.AddCue("still", FallEnd, panStart);
        clock.AddCue("pan", panStart, panStart + PanSeconds, EasingKind.SineInOut);

        // Each raccoon: "push" from entering at the left edge (pushing its letters) to the letters coming to rest;
        // "beat" lets go and looks; "exit" walks off the right edge. Each raccoon stands in the spot the next pair
        // of letters will fill, so it must be walking away before that pair comes close: the entrances are spaced
        // so that one raccoon's exit begins while the next pair is still a few lengths away.
        double start = panStart - FirstEntranceBeforePanStart;
        double lastExit = start;
        for (int i = 0; i < PushSeconds.Length; i++)
        {
            start += EntranceGaps[i];
            double rest = start + PushSeconds[i];
            double walk = rest + BeatSeconds[i];
            clock.AddCue("raccoon " + (i + 1) + " push", start, rest);
            clock.AddCue("raccoon " + (i + 1) + " beat", rest, walk);
            clock.AddCue("raccoon " + (i + 1) + " exit", walk, walk + ExitSeconds[i]);
            if (walk + ExitSeconds[i] > lastExit) lastExit = walk + ExitSeconds[i];
        }

        clock.AddCue("hold", lastExit, TotalSeconds);
        return clock;
    }
}

/// <summary>One raccoon's three cues.</summary>
/// <param name="Push">Entering from the left, pushing its letters, until they come to rest.</param>
/// <param name="Beat">Letting go and looking at the work.</param>
/// <param name="Exit">Walking off the right edge.</param>
internal sealed record RaccoonCues(TimelineCue Push, TimelineCue Beat, TimelineCue Exit);
