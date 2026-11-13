using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Captions;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Burned-in captions from an inline SRT text: <see cref="CaptionTrack.ParseSrt" /> reads the cues (markup and
/// character references cleaned), <see cref="DrawingSession.DrawCaptions" /> shows the cues active at the session's
/// time - wrapped to the safe area's width, on a rounded box, sitting on the bottom edge of the broadcast title-safe
/// area (drawn dashed) - and stacks two cues that overlap. A small "picture" moves underneath.
/// </summary>
internal sealed class CaptionScene : DemoScene
{
    private const string Srt =
        "1\n00:00:00,400 --> 00:00:02,600\nCaptions come from an <i>ordinary</i> SRT file,\nburned into every frame.\n\n" +
        "2\n00:00:02,900 --> 00:00:05,000\nLong cues wrap to the safe area and sit on its bottom edge, so a platform's buttons never cover them.\n\n" +
        "3\n00:00:04,300 --> 00:00:06,200\nOverlapping cues stack &amp; the earlier one stays on top.\n\n" +
        "4\n00:00:06,500 --> 00:00:07,900\nSame text, same frames, either package.\n";

    private const int PictureHeight = DemoFrame.Height - (int)ContentTop;

    private readonly DrawingSession header;
    private readonly DrawingSession session;
    private readonly byte[] scratch = new byte[DemoFrame.Width * PictureHeight * 4];

    /// <summary>Builds the picture, the safe-area guide and the captions (in a session of their own, below the header).</summary>
    public CaptionScene()
    {
        header = CreateFrameSession();

        // The "video": everything below the header, with its own frame, safe area and captions.
        var frame = new Size(DemoFrame.Width, PictureHeight);
        session = CreateSession(frame.Width, frame.Height, Backdrop);
        var layout = new DrawingLayout(frame, SafeAreaGuides.BroadcastTitleSafe);

        // A stand-in for the picture the captions are burned into.
        DrawingLayer picture = session.AddLayer("Picture", Ink);
        picture.AddShape(new RectangleShape(0, 0, frame.Width, frame.Height,
            new LinearGradientFill(new PointF(0, 0), new PointF(0, frame.Height), Color.FromRgb(40, 60, 110), Color.FromRgb(200, 110, 90))));
        picture.AddShape(new RectangleShape(0, 470, frame.Width, frame.Height - 470, 1, Color.FromRgb(30, 34, 52), isFilled: true));
        DrawingShape sun = new CircleShape(0, 0, 60, new RadialGradientFill(new PointF(0, 0), 60, Color.FromRgb(255, 236, 170), Color.FromRgba(255, 200, 120, 0)));
        picture.AddShape(sun);
        session.Animations.Bind(sun, AnimatedProperty.Position, new PointTrack { { 0, new PointF(200, 330) }, { 8, new PointF(1080, 150), EasingKind.SineInOut } });

        // The safe area the captions are placed in, dashed.
        DrawingLayer guide = session.AddLayer("Guide", Ink);
        RectangleF safe = layout.SafeArea;
        var dashes = new PathStrokeStyle(1.5f, PathStrokeCap.Butt, PathStrokeJoin.Miter, 4f, new[] { 8f, 6f });
        guide.AddShape(new PathShape(PathGeometry.Rectangle(safe), dashes, Color.FromRgba(255, 255, 255, 120)));
        Label(guide, session, "BroadcastTitleSafe", safe.Left + 8, safe.Top + 22, 13, Color.FromRgba(255, 255, 255, 160));

        DrawingLayer captions = session.AddLayer("Captions", Ink);
        session.ActiveLayer = captions;
        AnimatedShape shown = session.DrawCaptions(CaptionTrack.ParseSrt(Srt), layout, new CaptionStyle { Size = 32 });
        shown.IsSafeAreaChecked = true;
    }

    /// <inheritdoc />
    public override string Name => "captions";

    /// <inheritdoc />
    public override string Title => "Burned-in captions";

    /// <inheritdoc />
    public override string Description => "CaptionTrack.ParseSrt + DrawingSession.DrawCaptions: wrapped, boxed, placed on the safe area, stacked when they overlap";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        ExportFrame(header, rgba);
        session.Time = seconds;
        session.ExportPixels(scratch, new Size(DemoFrame.Width, PictureHeight));
        FrameCompositor.Copy(scratch, DemoFrame.Width, PictureHeight, rgba, 0, (int)ContentTop);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        header.Dispose();
        session.Dispose();
    }
}
