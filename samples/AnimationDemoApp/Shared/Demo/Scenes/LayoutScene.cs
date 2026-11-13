using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Layout anchors: ONE scene description - positions given as anchors of the frame or of a safe-area guide's safe
/// rectangle, with offsets as fractions and sizes from the frame's short side - built by the same method into a
/// portrait 1080x1920 session (against the Instagram Reels ad guide) and a landscape 1920x1080 session (against
/// broadcast title safe). Each is exported with its guide's overlay. A marker tours the nine safe anchors.
/// </summary>
internal sealed class LayoutScene : DemoScene
{
    private static readonly Size PortraitFrame = new Size(270, 480);
    private static readonly Size LandscapeFrame = new Size(784, 441);

    private static readonly LayoutAnchor[] Tour =
    {
        LayoutAnchor.TopLeft, LayoutAnchor.TopCenter, LayoutAnchor.TopRight, LayoutAnchor.CenterRight, LayoutAnchor.BottomRight,
        LayoutAnchor.BottomCenter, LayoutAnchor.BottomLeft, LayoutAnchor.CenterLeft, LayoutAnchor.Center,
    };

    private readonly DrawingSession frame;
    private readonly DrawingSession portrait;
    private readonly DrawingSession landscape;
    private readonly SafeAreaOverlay portraitOverlay = new SafeAreaOverlay(SafeAreaGuides.InstagramReelsAd);
    private readonly SafeAreaOverlay landscapeOverlay = new SafeAreaOverlay(SafeAreaGuides.BroadcastTitleSafe);
    private readonly byte[] portraitPixels = new byte[PortraitFrame.Width * PortraitFrame.Height * 4];
    private readonly byte[] landscapePixels = new byte[LandscapeFrame.Width * LandscapeFrame.Height * 4];

    /// <summary>Builds the same description into both formats.</summary>
    public LayoutScene()
    {
        frame = CreateFrameSession();
        DrawingLayer notes = frame.AddLayer("Notes", Dim);
        Label(notes, frame, "portrait 9:16, Instagram Reels ad guide", 64, 128, 13, Dim);
        Label(notes, frame, "landscape 16:9, broadcast title safe", 440, 128, 13, Dim);
        Label(notes, frame, "Both are the SAME code: positions are anchors of the safe area plus fractions; sizes come from the short side.", 64, 680, 15, Ink);

        portrait = Build(new DrawingLayout(new Size(1080, 1920), SafeAreaGuides.InstagramReelsAd));
        landscape = Build(new DrawingLayout(new Size(1920, 1080), SafeAreaGuides.BroadcastTitleSafe));
    }

    /// <inheritdoc />
    public override string Name => "layout";

    /// <inheritdoc />
    public override string Title => "Layout anchors";

    /// <inheritdoc />
    public override string Description => "DrawingLayout, LayoutAnchor, LayoutPosition: place against the frame or a safe area's safe rectangle; one scene, two formats";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        ExportFrame(frame, rgba);
        portrait.Time = seconds;
        landscape.Time = seconds;
        portrait.ExportPixels(portraitPixels, PortraitFrame, true, portraitOverlay);
        landscape.ExportPixels(landscapePixels, LandscapeFrame, true, landscapeOverlay);
        FrameCompositor.Copy(portraitPixels, PortraitFrame.Width, PortraitFrame.Height, rgba, 64, 140);
        FrameCompositor.Copy(landscapePixels, LandscapeFrame.Width, LandscapeFrame.Height, rgba, 440, 140);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        portrait.Dispose();
        landscape.Dispose();
    }

    //The one scene description, for any frame shape
    private static DrawingSession Build(DrawingLayout layout)
    {
        DrawingSession session = CreateSession(layout.CalibrationSize.Width, layout.CalibrationSize.Height, Color.FromRgb(34, 30, 56));
        float unit = layout.ShortSide;
        session.AddLayer("Art", Color.FromRgb(120, 90, 200));
        PointF centre = layout.Place(LayoutAnchor.Center, LayoutReference.SafeArea);
        float ring = 0.25f * Math.Min(layout.SafeArea.Width, layout.SafeArea.Height);
        session.DrawCircle(centre.X, centre.Y, ring, thickness: unit * 0.03f, color: Color.FromRgb(150, 110, 230));
        session.DrawCircle(centre.X, centre.Y, ring * 0.4f, filled: true, color: Color.FromRgb(255, 190, 110));

        DrawingLayer words = session.AddLayer("Words", Ink);
        words.IsSafeAreaChecked = true;
        session.ActiveLayer = words;
        PointF title = layout.Place(new LayoutPosition(LayoutAnchor.TopCenter, LayoutReference.SafeArea, fractionY: 0.08f));
        session.DrawText("Layout anchors", title.X, title.Y, unit * 0.06f, Ink, TextAlign.Center);
        PointF caption = layout.Place(new LayoutPosition(LayoutAnchor.BottomCenter, LayoutReference.SafeArea, fractionY: -0.05f));
        session.DrawText("centred, 5% above the safe bottom", caption.X, caption.Y, unit * 0.045f, Color.FromRgb(200, 190, 230), TextAlign.Center);

        RectangleF logo = layout.PlaceBox(new SizeF(unit * 0.1f, unit * 0.1f),
            new LayoutPosition(LayoutAnchor.TopLeft, LayoutReference.SafeArea, unit * 0.02f, unit * 0.02f));
        session.DrawRectangle(logo.X, logo.Y, logo.Width, logo.Height, filled: true, color: Color.FromRgb(90, 200, 255), cornerRadius: unit * 0.015f);
        RectangleF badge = layout.PlaceBox(new SizeF(unit * 0.16f, unit * 0.06f),
            new LayoutPosition(LayoutAnchor.TopRight, LayoutReference.SafeArea, -unit * 0.02f, unit * 0.02f));
        session.DrawRectangle(badge.X, badge.Y, badge.Width, badge.Height, filled: true, color: Color.FromRgb(255, 90, 120), cornerRadius: unit * 0.03f);

        // A marker touring the nine anchors of the safe area, one second each.
        session.DrawShape(new AnimatedShape(t =>
        {
            int stop = (int)Math.Floor(t) % Tour.Length;
            PointF at = layout.Place(Tour[stop], LayoutReference.SafeArea);
            return new CircleShape(at.X, at.Y, unit * 0.035f, 1, Color.FromRgb(255, 220, 120), isFilled: true);
        }));
        return session;
    }
}
