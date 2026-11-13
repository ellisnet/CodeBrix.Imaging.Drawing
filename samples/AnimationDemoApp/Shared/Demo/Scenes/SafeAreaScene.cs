using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Safe-area guides: a portrait 9:16 post and a landscape 16:9 frame, each a session of its own exported with a
/// <see cref="SafeAreaOverlay" /> and placed into the demo frame. The overlay cycles through the built-in presets
/// (and one combination), and the session's <see cref="DrawingSession.CheckSafeArea(SafeAreaGuide, Size, SafeAreaCheckOptions)" />
/// findings for the marked text are listed beside them. The landscape side ends with the YouTube end-screen guide,
/// which applies only in the final 20 seconds of a (pretend) 30-second video. The overlays use the library's default
/// look (a light tint and a thin line along the safe edge); the portrait one's <see cref="SafeAreaOverlay.Opacity" />
/// rises and falls to show the tint being turned up and down while the edge line stays.
/// </summary>
internal sealed class SafeAreaScene : DemoScene
{
    private const int PhoneWidth = 342;
    private const int PhoneHeight = 608;
    private const int WideWidth = 576;
    private const int WideHeight = 324;
    private const int PhoneX = 56;
    private const int PhoneY = 102;
    private const int WideX = 448;
    private const int WideY = 102;
    private const double PortraitSeconds = 2.5;

    private static readonly SafeAreaGuide[] PortraitGuides =
    {
        SafeAreaGuides.InstagramReelsAd,
        SafeAreaGuides.FacebookStoriesAd,
        SafeAreaGuides.YouTubeShortsAd,
        SafeAreaGuides.TikTok,
        SafeAreaGuides.OrdinaryPortraitPost,
        SafeAreaGuides.InstagramProfileGrid,
        SafeAreaGuides.InstagramFeedPreview,
        SafeAreaGuide.Combine("All portrait ad zones", SafeAreaGuides.InstagramReelsAd, SafeAreaGuides.YouTubeShortsAd, SafeAreaGuides.TikTok),
    };

    private static readonly SafeAreaGuide[] LandscapeGuides =
    {
        SafeAreaGuides.BroadcastTitleSafe,
        SafeAreaGuides.BroadcastActionSafe,
        SafeAreaGuides.YouTubeEndScreen,
    };

    private readonly DrawingSession frame;
    private readonly DrawingLayer notes;
    private readonly DrawingSession portrait;
    private readonly DrawingSession landscape;
    private readonly byte[] phonePixels = new byte[PhoneWidth * PhoneHeight * 4];
    private readonly byte[] widePixels = new byte[WideWidth * WideHeight * 4];

    /// <summary>Builds the two framed sessions and their content.</summary>
    public SafeAreaScene()
    {
        frame = CreateFrameSession();
        notes = frame.AddLayer("Notes", Ink);

        portrait = CreateSession(1080, 1920, Color.FromRgb(36, 28, 58));
        portrait.AddLayer("Art", Color.FromRgb(120, 90, 200));
        portrait.DrawCircle(540, 900, 330, thickness: 40, color: Color.FromRgb(150, 110, 230));
        portrait.DrawCircle(540, 900, 160, filled: true, color: Color.FromRgb(255, 190, 110));
        DrawingLayer words = portrait.AddLayer("Words", Ink);
        words.IsSafeAreaChecked = true;
        portrait.ActiveLayer = words;
        portrait.DrawText("Venus Rosette", 540, 330, 96, Ink, TextAlign.Center);
        portrait.DrawText("Eight years, thirteen Venus years", 540, 1420, 52, Color.FromRgb(200, 190, 230), TextAlign.Center);
        portrait.DrawText("Follow for more!", 80, 1780, 60, Color.FromRgb(255, 220, 120));
        portrait.DrawCircle(990, 1350, 44, filled: true, color: Color.FromRgb(255, 90, 120));

        landscape = CreateSession(1920, 1080, Color.FromRgb(22, 40, 52));
        landscape.AddLayer("Art", Color.FromRgb(60, 160, 170));
        landscape.DrawRectangle(0, 860, 1920, 220, filled: true, color: Color.FromRgb(30, 70, 80));
        landscape.DrawCircle(960, 480, 260, thickness: 30, color: Color.FromRgb(60, 160, 170));
        DrawingLayer captions = landscape.AddLayer("Captions", Ink);
        captions.IsSafeAreaChecked = true;
        landscape.ActiveLayer = captions;
        landscape.DrawText("Channel title", 70, 120, 72, Ink);
        landscape.DrawRectangle(120, 880, 900, 120, filled: true, color: Color.FromRgba(255, 255, 255, 230));
        landscape.DrawText("Lower third: a name and a role", 160, 960, 54, Color.FromRgb(20, 30, 40));
        landscape.DrawText("LOGO", 1850, 100, 48, Color.FromRgb(255, 200, 90), TextAlign.Right);
    }

    /// <inheritdoc />
    public override string Name => "safe-areas";

    /// <inheritdoc />
    public override string Title => "Safe-area guides";

    /// <inheritdoc />
    public override string Description => "SafeAreaGuides presets as an overlay (ExportPixels with a SafeAreaOverlay) and CheckSafeArea findings for the marked text";

    /// <inheritdoc />
    public override double DurationSeconds => PortraitGuides.Length * PortraitSeconds;

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        SafeAreaGuide portraitGuide = PortraitGuides[Math.Min(PortraitGuides.Length - 1, (int)(seconds / PortraitSeconds))];
        double landscapeSpan = DurationSeconds / LandscapeGuides.Length;
        int landscapeIndex = Math.Min(LandscapeGuides.Length - 1, (int)(seconds / landscapeSpan));
        SafeAreaGuide landscapeGuide = LandscapeGuides[landscapeIndex];

        // The end-screen guide plays against a pretend 30-second video: its clock runs 10 s to 30 s.
        // The landscape session's Time IS that clock: the overlay (no Time of its own) and the
        // check both evaluate the guide's time rule at the session's Time.
        double videoSeconds = 10 + (20 * ((seconds - (landscapeIndex * landscapeSpan)) / landscapeSpan));
        landscape.Time = videoSeconds;
        SafeAreaOverlay wideOverlay = new SafeAreaOverlay(landscapeGuide) { Duration = 30 };

        float tint = (float)PingPong(seconds, 5);
        portrait.ExportPixels(phonePixels, new Size(PhoneWidth, PhoneHeight), true, new SafeAreaOverlay(portraitGuide) { Opacity = tint });
        landscape.ExportPixels(widePixels, new Size(WideWidth, WideHeight), true, wideOverlay);

        notes.Clear();
        Describe(portraitGuide, portrait.CheckSafeArea(portraitGuide, new Size(1080, 1920)), WideX, WideY + WideHeight + 40);
        IReadOnlyList<SafeAreaFinding> wideFindings = landscape.CheckSafeArea(landscapeGuide, new Size(1920, 1080),
            new SafeAreaCheckOptions { Duration = 30 });
        Describe(landscapeGuide, wideFindings, WideX, WideY + WideHeight + 170);
        if (landscapeGuide.TimeRule != null)
        {
            Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "video at {0:0.0} s of 30 ({1})", videoSeconds,
                landscapeGuide.TimeRule.AppliesAt(videoSeconds, 30) ? "end screen" : "before it"), WideX + WideWidth + 20, WideY + 20, 13, Accent);
        }

        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "portrait overlay Opacity = {0:0.00}", tint), WideX + WideWidth + 20, WideY + 46, 13, Ink);
        Label(notes, frame, "the tint scales; the edge line stays", WideX + WideWidth + 20, WideY + 64, 13, Dim);

        ExportFrame(frame, rgba);
        FrameCompositor.Copy(phonePixels, PhoneWidth, PhoneHeight, rgba, PhoneX, PhoneY);
        FrameCompositor.Copy(widePixels, WideWidth, WideHeight, rgba, WideX, WideY);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        portrait.Dispose();
        landscape.Dispose();
    }

    private void Describe(SafeAreaGuide guide, IReadOnlyList<SafeAreaFinding> findings, float x, float y)
    {
        Label(notes, frame, guide.Name, x, y, 18, Ink);
        string kind = guide.Parts.Count > 0 ? "combined" : guide.Source.Split(' ', ':')[0];
        Label(notes, frame, kind + " - " + findings.Count + " finding(s) for the marked text", x, y + 22, 13, Dim);
        for (int i = 0; i < Math.Min(3, findings.Count); i++)
        {
            SafeAreaFinding f = findings[i];
            string line = string.Format(CultureInfo.InvariantCulture, "\"{0}\" in {1} {2}", Truncate((f.Element as TextShape)?.Text ?? f.Element.GetType().Name),
                f.GuideName == guide.Name ? string.Empty : f.GuideName + ":", f.ZoneName);
            Label(notes, frame, line, x + 10, y + 46 + (i * 20), 13, Accent);
        }
    }

    private static string Truncate(string text) => text.Length > 22 ? text.Substring(0, 21) + "..." : text;
}
