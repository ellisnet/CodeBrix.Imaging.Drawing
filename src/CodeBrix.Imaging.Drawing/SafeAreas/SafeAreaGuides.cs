namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// Built-in <see cref="SafeAreaGuide"/> presets. EVERY ONE IS ADVISORY: platforms move their
/// buttons, captions and crops without notice, and the social-media figures are not
/// published by the platforms as plain numbers - they come from third parties reading the
/// platforms' templates or measuring the apps. Each preset's <see cref="SafeAreaGuide.Source"/>
/// says which kind of figure it is (FACT: a published standard; REPORTED: third parties
/// quoting a platform; MEASURED: third parties measuring the app) and what it was taken from,
/// as gathered in November 2026. Check against the platform's own template before relying on
/// a preset, and define your own guide when you know better numbers.
/// <para>
/// The margins are fractions of the frame, worked out from the reported pixel figures on a
/// 1080 x 1920 portrait frame (or stated directly as percentages), so they fit any size of
/// the same format. Combine presets with <see cref="SafeAreaGuide.Combine(string, SafeAreaGuide[])"/>
/// for a video meant for several platforms.
/// </para>
/// </summary>
public static class SafeAreaGuides
{
    private const string MetaAdSource =
        "REPORTED: the Meta Ads Guide figures for Reels and Stories ads as repeated by third-party guides " +
        "(November 2026): keep the top 14% (about 270 px of 1920), the bottom 35% (672 px) and 6% of each side " +
        "(about 65 px of 1080) clear. Reels ads carrying a disclaimer should keep the bottom 40% clear. Other " +
        "sites report other, mutually inconsistent figures; Meta Ads Manager shows its own safe-zone overlay. Advisory.";

    private static readonly SafeAreaMargins MetaAdMargins = new SafeAreaMargins(0.14f, 0.35f, 0.06f, 0.06f);

    /// <summary>Instagram Reels ads, portrait 9:16 (REPORTED, Meta Ads Guide): top 14%, bottom 35%, sides 6%.</summary>
    public static SafeAreaGuide InstagramReelsAd { get; } = new SafeAreaGuide("Instagram Reels ad", MetaAdMargins, MetaAdSource);

    /// <summary>Instagram Stories ads, portrait 9:16 (REPORTED, Meta Ads Guide): top 14%, bottom 35%, sides 6%.</summary>
    public static SafeAreaGuide InstagramStoriesAd { get; } = new SafeAreaGuide("Instagram Stories ad", MetaAdMargins, MetaAdSource);

    /// <summary>Facebook Reels ads, portrait 9:16 (REPORTED, Meta Ads Guide): top 14%, bottom 35%, sides 6%.</summary>
    public static SafeAreaGuide FacebookReelsAd { get; } = new SafeAreaGuide("Facebook Reels ad", MetaAdMargins, MetaAdSource);

    /// <summary>Facebook Stories ads, portrait 9:16 (REPORTED, Meta Ads Guide): top 14%, bottom 35%, sides 6%.</summary>
    public static SafeAreaGuide FacebookStoriesAd { get; } = new SafeAreaGuide("Facebook Stories ad", MetaAdMargins, MetaAdSource);

    /// <summary>
    /// YouTube Shorts ads, portrait 9:16 (REPORTED, Google's universal video ad safe-zone
    /// template): top 288 px, bottom 672 px, left 48 px, right 192 px of a 1080 x 1920 frame.
    /// </summary>
    public static SafeAreaGuide YouTubeShortsAd { get; } = new SafeAreaGuide("YouTube Shorts ad",
        new SafeAreaMargins(288f / 1920f, 672f / 1920f, 48f / 1080f, 192f / 1080f),
        "REPORTED: Google's universal video ad safe-zone template as repeated by third-party guides (November 2026): " +
        "on 1080 x 1920 keep the top 288 px (15%), the bottom 672 px (35%), the left 48 px (4.4%) and the right " +
        "192 px (17.8%) clear, leaving x 48..888, y 288..1248. Google publishes no separate zone for ordinary Shorts. Advisory.");

    /// <summary>
    /// TikTok, portrait 9:16 (MEASURED from ad previews, lower confidence): top about 288 px,
    /// bottom about 461 px, left about 86 px, right about 216 px of a 1080 x 1920 frame.
    /// </summary>
    public static SafeAreaGuide TikTok { get; } = new SafeAreaGuide("TikTok",
        new SafeAreaMargins(288f / 1920f, 461f / 1920f, 86f / 1080f, 216f / 1080f),
        "MEASURED by third parties from TikTok ad previews (reported March 2026; lower confidence): on 1080 x 1920 " +
        "about 288 px top (15%), 461 px bottom (24%), 86 px left (8%) and 216 px right (20%). TikTok publishes its " +
        "zone as a downloadable overlay template rather than as numbers. Advisory.");

    /// <summary>
    /// An ordinary (non-ad) portrait post on Instagram, Facebook, YouTube Shorts or TikTok
    /// (MEASURED, ranges): the LARGER end of each measured range - top 200 px, bottom 370 px,
    /// left 60 px, right 164 px of a 1080 x 1920 frame.
    /// </summary>
    public static SafeAreaGuide OrdinaryPortraitPost { get; } = new SafeAreaGuide("Ordinary portrait post",
        new SafeAreaMargins(200f / 1920f, 370f / 1920f, 60f / 1080f, 164f / 1080f),
        "MEASURED by third parties for ordinary (non-ad) posts on the major portrait platforms; the figures are " +
        "ranges, not constants (top 120-200 px, bottom 250-370 px, right 120-164 px, left 50-60 px of 1080 x 1920), " +
        "and this guide uses the larger end of each. One measurement of organic Shorts found far more (about 380 px " +
        "top and 390 px bottom). Advisory.");

    /// <summary>
    /// The Instagram profile grid's view of a portrait Reel (REPORTED): a centered 3:4 crop,
    /// which on 1080 x 1920 keeps y 240..1680.
    /// </summary>
    public static SafeAreaGuide InstagramProfileGrid { get; } = new SafeAreaGuide("Instagram profile grid",
        SafeAreaMargins.None,
        "REPORTED by third-party guides (November 2026): the profile grid shows a centered 3:4 crop of a 9:16 Reel " +
        "(1080 x 1440, losing 240 px top and bottom). Some sources say the grid is still square (1:1) on desktop web. Advisory.",
        new[] { new SafeAreaCrop("profile grid 3:4 crop", 3f, 4f) });

    /// <summary>
    /// The Instagram feed preview of a portrait Reel (REPORTED): a centered 4:5 crop, which on
    /// 1080 x 1920 keeps y 285..1635.
    /// </summary>
    public static SafeAreaGuide InstagramFeedPreview { get; } = new SafeAreaGuide("Instagram feed preview",
        SafeAreaMargins.None,
        "REPORTED by third-party guides (November 2026): the feed preview shows a centered 4:5 crop of a 9:16 Reel " +
        "(1080 x 1350, losing 285 px top and bottom). Advisory.",
        new[] { new SafeAreaCrop("feed preview 4:5 crop", 4f, 5f) });

    /// <summary>
    /// Broadcast title-safe area (FACT, SMPTE ST 2046-1; EBU R95 and ITU-R BT.1848 agree): the
    /// central 90% of the width and height - a 5% margin on every edge.
    /// </summary>
    public static SafeAreaGuide BroadcastTitleSafe { get; } = new SafeAreaGuide("Broadcast title safe",
        SafeAreaMargins.Uniform(0.05f),
        "FACT: SMPTE ST 2046-1 title-safe area, 90% of the width and height (5% margin per edge); EBU R95 and " +
        "ITU-R BT.1848 give the same zone, and Netflix recommends it. The legacy pre-HD value was 80%.");

    /// <summary>
    /// Broadcast action-safe area (FACT, SMPTE ST 2046-1; EBU R95 and ITU-R BT.1848 agree): the
    /// central 93% of the width and height - a 3.5% margin on every edge.
    /// </summary>
    public static SafeAreaGuide BroadcastActionSafe { get; } = new SafeAreaGuide("Broadcast action safe",
        SafeAreaMargins.Uniform(0.035f),
        "FACT: SMPTE ST 2046-1 action-safe area, 93% of the width and height (3.5% margin per edge); EBU R95 and " +
        "ITU-R BT.1848 give the same zone, and Netflix recommends it. The legacy pre-HD value was 90%.");

    /// <summary>
    /// YouTube end screens (REPORTED): end-screen elements can appear anywhere during the final
    /// 5-20 seconds, so the whole frame is unsafe during the final 20 seconds of the video.
    /// Give a check or overlay the video's duration for this guide to apply only at the end.
    /// </summary>
    public static SafeAreaGuide YouTubeEndScreen { get; } = new SafeAreaGuide("YouTube end screen",
        SafeAreaMargins.WholeFrame,
        "REPORTED by third-party guides (November 2026): end screens occupy the last 5-20 seconds of a video (which " +
        "must be at least 25 s long; up to four elements on 16:9), so the safest rule is to keep the final 20 seconds " +
        "free of anything that matters. Info cards can appear at any time; no positions are published for the title " +
        "bar, progress bar, watermark or captions. Advisory.",
        timeRule: SafeAreaTimeRule.FinalSeconds(20));
}
